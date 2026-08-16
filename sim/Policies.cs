// 플레이어 정책(AI) 3종 — v2 (card-system-v2.md, ADR-011): 접두+접미 2단 선택 →
// 단일 카드 선택 + 대상 지정. packages/sim/src/policies.ts 이관 (ADR-029).
//
// 정책 의도(v1 승계):
// - standard: 우위 판정 우선 (원산지 > 팩트 > 일반), 헛소리 회피 — 헛소리밖에 없으면 제출 대신
//   퇴고(태그 사냥 — card-system-v2 §7)로 손패를 교체한다.
// - skilled : standard + 대상 최적화(원산지 구성품·팩트 장비 선택), X06 리액션 타이밍, 초반 버프.
// - reckless: 완전 무작위 제출(헛소리 포함 — §3.4 "억지 플레이" 상당), 크리 롤 0.7.
//
// ⚠️ v1의 pFact/pFumble "목표 판정 롤"(의도적 오류 주입)은 폐기 — v2는 손패 5장 전부가 태그
// 선택지라 판정이 손패 가용성의 함수가 되고, 정책은 greedy 선택으로 그 상한을 실측한다.
// Telemetry.Attempted는 정책이 제출 시점에 기대한 판정(엔진 실현과 일치해야 정상),
// NoWantedFallbacks는 우위 판정(원산지/팩트) 없이 제출한 횟수다.
// 주의: 정책은 게이지 10 도달 즉시(critProb 롤 통과 시) 크리를 사용한다 — 타이밍 최적화 없음.
//
// ── TS→C# 이관 주의 ────────────────────────────────────
// · 후보 정렬은 반드시 **안정 정렬**이어야 한다. TS 의 `Array.prototype.sort` 는 ES2019 부터
//   안정이고 동순위 후보의 순서 = 손패 순서로 결정되는데, C# 의 List.Sort 는 불안정이라
//   같은 시드에서 다른 카드를 낸다. 그래서 LINQ OrderBy(안정)만 쓴다.
// · rng 소비 횟수가 어긋나면 그 뒤 전 수열이 밀린다 — pick 과 critProb 롤의 호출 지점·횟수를
//   TS 와 1:1 로 유지할 것 (critRolled 는 턴당 1회 롤을 보장하는 자물쇠다).

using ReviewHero.Engine;

namespace ReviewHero.Sim;

public enum PolicyName
{
    Standard,
    Skilled,
    Reckless,
}

public sealed record PolicyParams(double CritProb, bool Smart, bool Random);

/// <summary>의도(정책 기대 판정) vs 실현(엔진 판정) 대조용 계측 — CLI가 집계·출력</summary>
public sealed class PolicyTelemetry
{
    /// <summary>제출 시점 기대 판정 (random 정책은 미집계)</summary>
    public Dictionary<Judgement, int> Attempted { get; } = new()
    {
        [Judgement.Origin] = 0,
        [Judgement.Fact] = 0,
        [Judgement.Normal] = 0,
        [Judgement.Fumble] = 0,
    };

    /// <summary>우위 판정(원산지/팩트) 없이 제출한 횟수</summary>
    public int NoWantedFallbacks { get; set; }
}

public static class Policies
{
    public static readonly IReadOnlyDictionary<PolicyName, PolicyParams> All =
        new Dictionary<PolicyName, PolicyParams>
        {
            [PolicyName.Standard] = new(CritProb: 1.0, Smart: false, Random: false),
            [PolicyName.Skilled] = new(CritProb: 1.0, Smart: true, Random: false),
            [PolicyName.Reckless] = new(CritProb: 0.7, Smart: false, Random: true),
        };

    private static readonly IReadOnlyDictionary<Judgement, int> JudgeRank = new Dictionary<Judgement, int>
    {
        [Judgement.Origin] = 3,
        [Judgement.Fact] = 2,
        [Judgement.Normal] = 1,
        [Judgement.Fumble] = 0,
    };

    public static bool TryParseName(string s, out PolicyName name) =>
        Enum.TryParse(s, ignoreCase: true, out name) && Enum.IsDefined(name);

    public static string ToCliName(PolicyName n) => n.ToString().ToLowerInvariant();

    private static T Pick<T>(IReadOnlyList<T> arr, Rng rng) => arr[(int)Math.Floor(rng() * arr.Count)];

    /// <summary>제출 후보 1건 — 카드 + 대상 지정 + 기대 판정 (엔진 판정 규칙과 동일하게 산출)</summary>
    private sealed record PlayOption(
        int Uid,
        ReviewCardDef Def,
        Judgement Judgement,
        int? MyEquipmentIndex,
        int? EnemyEquipmentIndex,
        /// <summary>기대 피해 상당치 (동순위 정렬용)</summary>
        int Score);

    /// <summary>엔진과 동일 규칙으로 카드의 최적 대상·판정을 평가한다. 제출 무의미(은신 빗나감·슬롯 만석·대상 없음)면 null</summary>
    private static PlayOption? Evaluate(Battle battle, int uid, ReviewCardDef def)
    {
        var st = battle.State;
        var e = st.Enemy;
        var gate = e.Def.StealthGate;
        // 정책도 전투에 확정된 rules 를 읽는다 (ADR-025) — 시뮬 A/B 에서 엔진만 바뀌고 정책이 옛 배율로
        // 카드를 고르면 오버라이드 효과가 흐려진다
        var rules = battle.ActiveRules;

        // E04 은신 게이트: 명중 불가 계열은 빗나감 — 제출하지 않는다
        if (e.Stealth && gate is not null
            && (def.Target == TargetKind.Enemy || def.Target == TargetKind.EnemyEquipment)
            && !gate.HittableSuits.Contains(def.Suit))
        {
            return null;
        }

        Judgement JudgeAgainst(IReadOnlyList<string> tags, IReadOnlyList<string> nulls, bool isOrigin)
        {
            if (isOrigin) return Judgement.Origin;
            if (nulls.Contains(def.Tag)) return Judgement.Fumble;
            if (tags.Contains(def.Tag)) return Judgement.Fact;
            return Judgement.Normal;
        }

        Judgement judgement;
        int? myEquipmentIndex = null;
        int? enemyEquipmentIndex = null;

        if (def.Target == TargetKind.MyEquipment)
        {
            // 판정 좋은 내 장비 우선. damage_buff는 부착 슬롯(2칸) 여유 필수 — 만석이면 제출 낭비
            (int Idx, Judgement J)? best = null;
            for (int i = 0; i < st.Player.Equipment.Count; i++)
            {
                var eq = st.Player.Equipment[i];
                // damage_buff만 부착 슬롯(2칸) 제약 — defense_buff는 슬롯 미사용 수치 누적이라 만석 개념이 없다 (ADR-023 ①)
                if (def.Effect.Type == "damage_buff"
                    && eq.Attachments.Count(a => a.UsesSlot) >= rules.Player.AttachSlots)
                {
                    continue;
                }
                var j = JudgeAgainst(eq.Def.Tags, eq.Def.NullTags, false);
                if (best is null || JudgeRank[j] > JudgeRank[best.Value.J]) best = (i, j);
            }
            if (best is null) return null;
            judgement = best.Value.J;
            myEquipmentIndex = best.Value.Idx;
        }
        else if (def.Target == TargetKind.EnemyEquipment)
        {
            // 원산지 일치 구성품 우선, 없으면 판정 최선 구성품
            (int Idx, Judgement J)? best = null;
            for (int i = 0; i < e.Equipment.Count; i++)
            {
                var eq = e.Equipment[i];
                if (eq.Destroyed) continue;
                bool isOrigin = def.Origin?.Equipment is not null && def.Origin.Equipment == eq.Name;
                var j = JudgeAgainst(eq.Tags, e.Def.NullTags, isOrigin);
                if (best is null || JudgeRank[j] > JudgeRank[best.Value.J]) best = (i, j);
            }
            if (best is null) return null; // 남은 구성품 없음
            judgement = best.Value.J;
            enemyEquipmentIndex = best.Value.Idx;
        }
        else
        {
            bool isOrigin = def.Origin?.Enemy is not null && def.Origin.Enemy == e.Def.Id;
            judgement = JudgeAgainst(e.Def.WeaknessTags, e.Def.NullTags, isOrigin);
        }

        // 기대 피해 상당치: 의지 피해(value/damage 동반) 또는 구성품 피해 × 판정 배율 (+원산지 +1)
        var ef = def.Effect;
        int bas = ef.Type is "damage" or "equipment_damage" ? ef.Value ?? 0 : ef.Damage ?? 0;
        double vanity = e.Def.SuitDamageMult is not null && e.Def.SuitDamageMult.TryGetValue(def.Suit, out var vm) ? vm : 1;
        int score = (int)Math.Floor(bas * rules.Judge.Mult[judgement] * vanity)
                    + (judgement == Judgement.Origin ? rules.Judge.OriginFixedAdd : 0);
        // 방어(ADR-023 ①)는 딜이 아니지만 "막은 만큼 = 안 맞은 좋아요"라 피해 등가로 환산해 정렬에 태운다.
        // 정렬은 판정 우선이라 같은 판정 안에서만 경합한다. (v2 카드 데이터에 defense_buff가 들어오면 재검토 대상)
        if (ef.Type == "defense_buff") score += (int)Math.Floor((ef.Value ?? 0) * rules.Judge.Mult[judgement]);

        return new PlayOption(uid, def, judgement, myEquipmentIndex, enemyEquipmentIndex, score);
    }

    /// <summary>한 플레이어 턴을 정책대로 진행하고 EndTurn까지 수행한다</summary>
    public static void PlayTurn(Battle battle, CardIndex cards, PolicyName name, Rng rng, PolicyTelemetry? telemetry = null)
    {
        var pars = All[name];
        var rules = battle.ActiveRules; // 정책의 문턱값도 엔진과 같은 rules 를 본다 (ADR-025)
        int safety = 20;
        bool critRolled = false; // critProb 롤은 턴당 1회 (루프 반복마다 재롤하면 억지 정책의 0.7이 사실상 1.0이 됨)

        while (safety-- > 0)
        {
            var st = battle.State;
            if (st.Result is not null) return;
            var p = st.Player;

            // 크리티컬 (게이지 10, 필력 0, 턴당 1회)
            // E04 은신 게이트: 은신 중 명중 불가 계열의 크리는 빗나가므로(게이지만 소모) 시도하지 않는다
            var gate = st.Enemy.Def.StealthGate;
            bool critBlockedByStealth =
                st.Enemy.Stealth && gate is not null && p.Disposition != Disposition.감성논점
                && !gate.HittableSuits.Contains(Types.DispositionSuit[p.Disposition]);
            if (p.Gauge >= rules.Gauge.Max && !p.CritUsedThisTurn && !critRolled && !critBlockedByStealth)
            {
                critRolled = true;
                if (rng() < pars.CritProb)
                {
                    battle.UseCritical();
                    continue;
                }
            }

            var hand = battle.State.Player.Hand.Select(c => (Uid: c.Uid, Def: cards.ById[c.CardId])).ToList();
            var reviews = hand.Where(c => c.Def is ReviewCardDef).Select(c => (c.Uid, Def: (ReviewCardDef)c.Def)).ToList();
            var specials = hand.Where(c => c.Def is SpecialDef).Select(c => (c.Uid, Def: (SpecialDef)c.Def)).ToList();

            // TS 의 `specials.find(c => c.def.id === 'X0n')` — 없으면 undefined
            (int Uid, SpecialDef Def)? FindSpecial(string id)
            {
                foreach (var c in specials)
                {
                    if (c.Def.Id == id) return c;
                }
                return null;
            }

            // X08 별점 구걸: 초과 소실 없이 다 받을 수 있으면 사용 (전 정책 — 순수 이득)
            if (FindSpecial("X08") is { } x08
                && p.Energy >= x08.Def.Cost && p.Gauge <= rules.Gauge.Max - (x08.Def.Effect.Value ?? 0))
            {
                battle.PlaySpecial(x08.Uid);
                continue;
            }

            // skilled: 적 인텐트가 강공격이고 리액션 미설치면 X06 설치
            if (pars.Smart)
            {
                var x06 = FindSpecial("X06");
                var intent = st.Enemy.Def.Actions.FirstOrDefault(a => a.Id == st.Enemy.IntentId);
                int incoming = intent?.Effects.FirstOrDefault(e => e.Op == "damage")?.Value ?? 0;
                if (x06 is not null && p.Reaction is null && p.Energy >= x06.Value.Def.Cost
                    && intent?.AType == EnemyActionType.Attack && incoming >= 7)
                {
                    battle.PlaySpecial(x06.Value.Uid);
                    continue;
                }
            }

            // 제출 후보: 필력 내 리뷰 카드 × 최적 대상
            var options = reviews
                .Where(c => c.Def.Cost <= p.Energy)
                .Select(c => Evaluate(battle, c.Uid, c.Def))
                .Where(o => o is not null)
                .Select(o => o!)
                .ToList();

            // reckless: 완전 무작위 제출 (헛소리 회피 없음. 대상은 Evaluate 가 고른 그대로)
            if (pars.Random)
            {
                if (options.Count == 0) break;
                var o = Pick(options, rng);
                battle.SubmitReview(o.Uid, enemyEquipmentIndex: o.EnemyEquipmentIndex, myEquipmentIndex: o.MyEquipmentIndex);
                continue;
            }

            // standard/skilled: 헛소리 회피 — 헛소리가 아닌 후보만
            var pool = options.Where(o => o.Judgement != Judgement.Fumble).ToList();

            // skilled: 초반(1~2턴) 버프 부착 우선 (v1 S13/S14 조기 부착 의도 승계)
            if (pars.Smart && st.Turn <= 2)
            {
                var buffs = pool.Where(o => o.Def.Effect.Type == "damage_buff").ToList();
                if (buffs.Count > 0) pool = buffs;
            }

            if (pool.Count == 0)
            {
                // 은신 턴: 명중 계열이 없으면 낭비 방지 — 패스 (퇴고해도 이번 턴 명중 보장 없음)
                if (st.Enemy.Stealth && st.Enemy.Def.StealthGate is not null) break;
                // X03: 낼 카드가 없으면 무작위 카드 생성 시도
                if (FindSpecial("X03") is { } x03
                    && p.Energy >= x03.Def.Cost && p.Hand.Count < rules.Player.HandMax && options.Count == 0)
                {
                    battle.PlaySpecial(x03.Uid);
                    continue;
                }
                // 퇴고 (v2: 태그 사냥 — card-system-v2 §7): 헛소리/불용 카드를 버리고 교체
                if (p.Energy >= rules.Player.ReviseCost && p.Hand.Count > 0 && p.Deck.Count + p.Discard.Count > 0)
                {
                    var fumbleOpt = options.FirstOrDefault(o => o.Judgement == Judgement.Fumble);
                    int? targetUid = fumbleOpt?.Uid ?? (hand.Count > 0 ? hand[0].Uid : null);
                    if (targetUid is { } uid)
                    {
                        battle.Revise(uid);
                        continue;
                    }
                }
                break;
            }

            // 우위 판정 > 기대 피해 > 저비용 순 (안정 정렬 — 파일 상단 이관 주의 참조)
            var best = pool
                .OrderByDescending(o => JudgeRank[o.Judgement])
                .ThenByDescending(o => o.Score)
                .ThenBy(o => o.Def.Cost)
                .First();
            if (telemetry is not null)
            {
                telemetry.Attempted[best.Judgement]++;
                if (JudgeRank[best.Judgement] < JudgeRank[Judgement.Fact]) telemetry.NoWantedFallbacks++;
            }
            battle.SubmitReview(best.Uid, enemyEquipmentIndex: best.EnemyEquipmentIndex, myEquipmentIndex: best.MyEquipmentIndex);
        }

        if (battle.State.Result is null) battle.EndTurn();
    }
}
