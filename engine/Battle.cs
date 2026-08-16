// 이세계 리뷰용사 — 전투 상태머신 (GDD §2 공통 계산, §3 전투 전체)
// UI 무의존 순수 상태머신. fs/network 접근 금지, DateTime.Now / 시드 없는 Random 금지 — rng 주입.
//
// v2 (card-system-v2.md §2·§9, ADR-011): 접두+접미 조합 폐지 → SubmitReview(cardUid, opts),
// 판정 3단계 → 4단계(원산지 최우선·무효 태그 무시), modifier 적용부 전량 삭제.
//
// C# 이관 (ADR-029): packages/core/src/battle.ts 의 기계적 이관이다. 분기·순서·수치를 바꾸지 않는다.
// 수치 연산은 Formula 가, 밸런스 수치는 Rules 가 소유한다 (ADR-025) — 여기서 다시 구현·재선언하지 않는다.
// 상태 타입은 BattleState.cs 로 분리했다(선언 길이 때문이며 규칙 변경이 아니다).

namespace ReviewHero.Engine;

// ── 전투 엔진 ─────────────────────────────────────────

public sealed class Battle
{
    // ── 판정 표 호환 별칭 (ADR-025) ─────────────────────
    // **정본은 Rules 다.** 아래 3종은 기존 외부 호출자(시뮬 정책·UI)를 위한 얇은 별칭이며
    // RulesConfig.Default 를 그대로 가리킨다 — 여기에 값을 적지 않는다. 전투가 실제로 쓰는 것은
    // `new Battle(new BattleConfig { Rules = ... })` 로 확정된 인스턴스별 rules 이므로,
    // A/B 오버라이드를 반영하려면 이 상수가 아니라 `battle.ActiveRules` 를 읽어야 한다.

    /// <summary>사용 금지 — <c>RulesConfig.Default.Judge.Mult</c> (또는 <c>battle.ActiveRules.Judge.Mult</c>) 를 쓸 것</summary>
    [Obsolete("RulesConfig.Default.Judge.Mult 또는 battle.ActiveRules.Judge.Mult 를 쓸 것")]
    public static IReadOnlyDictionary<Judgement, double> JudgeMult => RulesConfig.Default.Judge.Mult;

    /// <summary>사용 금지 — <c>RulesConfig.Default.Judge.Gauge</c> 를 쓸 것</summary>
    [Obsolete("RulesConfig.Default.Judge.Gauge 를 쓸 것")]
    public static IReadOnlyDictionary<Judgement, int> JudgeGauge => RulesConfig.Default.Judge.Gauge;

    /// <summary>사용 금지 — <c>RulesConfig.Default.Judge.Heal</c> 를 쓸 것</summary>
    [Obsolete("RulesConfig.Default.Judge.Heal 를 쓸 것")]
    public static IReadOnlyDictionary<Judgement, int> JudgeHeal => RulesConfig.Default.Judge.Heal;

    /// <summary>외부에서 읽는다 (TS 현행도 `readonly state` 로 노출)</summary>
    public BattleState State { get; }

    private readonly CardIndex _cards;
    private readonly Rng _rng;

    /// <summary>이 전투에 확정된 밸런스 수치 (ADR-025) — 코드에 수치를 박지 않고 전부 여기를 거친다</summary>
    private readonly RulesConfig _rules;

    private readonly PlayerEquipmentDef? _parcel;
    private readonly int _maxTurns;
    private readonly int _layer;
    private readonly int _sigmaP;
    private readonly double _onboardingEnemyMult;

    /// <summary>온보딩 1판 헛소리 게이지 완화값 — 미지정이면 rules.judge.gauge.fumble 를 그대로 쓴다</summary>
    private readonly int? _fumbleGaugeOverride;

    private readonly bool _buffNoJudgement;
    private readonly bool _noShuffle;
    private readonly bool _collectLog;
    private int _uidSeq = 1;

    /// <summary>계열 선언 순서 (품질→성능→배송→감성) — 논점 동률 tiebreak 이 이 순서에 의존한다</summary>
    private static readonly Suit[] SuitOrder = Enum.GetValues<Suit>();

    public Battle(BattleConfig cfg)
    {
        _cards = cfg.Cards;
        _rng = cfg.Rng;
        _rules = RulesConfig.Merge(RulesConfig.Default, cfg.Rules);
        // 보스전에만 택배가 따라온다 — 미지정이면 보스일 때 기본 보급품을 쓴다 (ADR-024 ③)
        _parcel = cfg.ParcelSpecified
            ? cfg.Parcel
            : (cfg.Enemy.Tier == EnemyTier.Boss ? Types.BossParcelEquipment : null);
        _maxTurns = cfg.MaxTurns ?? _rules.Battle.MaxTurns;
        _layer = cfg.Layer ?? 1;
        _sigmaP = cfg.SigmaP ?? 0;
        _onboardingEnemyMult = cfg.Onboarding?.EnemyDamageMult ?? 1;
        _fumbleGaugeOverride = cfg.Onboarding?.FumbleGaugeDelta;
        _buffNoJudgement = cfg.Onboarding?.BuffNoJudgement ?? false;
        _noShuffle = cfg.NoShuffle ?? false;
        _collectLog = cfg.CollectLog ?? false;

        var deck = cfg.Deck.Select(cardId => new CardInstance { Uid = _uidSeq++, CardId = cardId }).ToList();
        if (!_noShuffle) RngFactory.Shuffle(deck, _rng);

        var counters = new Dictionary<Suit, int>();
        foreach (var suit in SuitOrder)
        {
            counters[suit] = cfg.InitialSuitCounters is not null && cfg.InitialSuitCounters.TryGetValue(suit, out var v) ? v : 0;
        }

        var player = new PlayerState
        {
            Will = _rules.Player.Will,
            MaxWill = _rules.Player.Will,
            Energy = _rules.Player.EnergyPerTurn,
            EnergyNextTurnBonus = 0,
            EnergyNextTurnPenalty = 0,
            Gold = cfg.Gold ?? 0,
            Gauge = Formula.ClampGauge(cfg.StartGauge ?? 0, _rules),
            Deck = deck,
            ParcelOpened = false,
            CritUsedThisTurn = false,
            InconvenienceGoldUsed = false,
            ViralBonusGranted = 0,
            X05Armed = false,
            StoredDamageBonus = 0,
            DamageTakenThisTurn = 0,
            Reaction = null,
            SuitCounters = counters,
            LastSuit = cfg.InitialLastSuit,
            Disposition = Types.SuitDisposition[SuitOrder[0]], // 초기값 = 품질 논점 (아래에서 스냅샷 재계산)
        };
        foreach (var def in cfg.PlayerEquipment ?? Types.StartingEquipment)
        {
            player.Equipment.Add(new PlayerEquipmentState { Def = def, Defense = 0 });
        }

        var enemy = new EnemyState
        {
            Def = cfg.Enemy,
            Will = cfg.Enemy.Will,
            MaxWill = cfg.Enemy.Will,
            Equipment = cfg.Enemy.Equipment.Select(e => new EnemyEquipmentState
            {
                Name = e.Name,
                Tags = new List<string>(e.Tags),
                Durability = e.Durability,
                DisabledTurns = 0,
                Destroyed = false,
            }).ToList(),
            StunTurns = 0,
            StaggerImmunityTurns = 0,
            PendingDelay = false,
            Stealth = false,
            StealthEverBroken = false,
            Charging = null,
            WeakenNextActionPct = 0,
            DamageReductionNextHit = 0,
            ReflectNextHit = 0,
            PatternIndex = 0,
            IntentId = cfg.Enemy.Pattern[0],
            Phase2Done = false,
        };

        State = new BattleState
        {
            Turn = 1,
            Result = null,
            Player = player,
            Enemy = enemy,
            Stats = new BattleStats(),
        };

        // 논점 스냅샷 (GDD §3.5: argmax, 동률 = 최근 제출 계열, 초기값 = 품질 논점)
        State.Player.Disposition = ComputeDisposition();

        // [전투 시작] 셔플 → 손패 수만큼 드로우 → 인텐트 공개 (GDD §3.2)
        Draw(_rules.Player.HandSize);
    }

    /// <summary>이 전투에 적용된 밸런스 수치 (읽기 전용) — 시뮬 정책·UI 가 규칙을 재선언하지 않도록 노출</summary>
    public RulesConfig ActiveRules => _rules;

    // ── 유틸 ──

    private void Log(string msg)
    {
        if (_collectLog) State.Log.Add(msg);
    }

    /// <summary>디버프 종류의 원문 표기 — 로그 문자열을 TS 판과 동일하게 유지하기 위한 표시용 변환</summary>
    private static string KindLabel(EnemyDebuffKind kind) =>
        kind == EnemyDebuffKind.AttackDown ? "attack_down" : "attack_halve";

    private Disposition ComputeDisposition()
    {
        var p = State.Player;
        var suits = SuitOrder;
        int max = suits.Max(s => p.SuitCounters[s]);
        if (max == 0) return Types.SuitDisposition[SuitOrder[0]]; // 품질 논점
        var top = suits.Where(s => p.SuitCounters[s] == max).ToList();
        if (top.Count == 1) return Types.SuitDisposition[top[0]];
        if (p.LastSuit is { } last && top.Contains(last)) return Types.SuitDisposition[last];
        // 가정(GDD §3.5 침묵): 동률인데 최근 제출 계열이 동률군에 없거나 없음(null)이면
        // 품질→성능→배송→감성 선언 순서로 결정 (결정적. GDD에 1줄 명시 필요 — 에스컬레이션 대상)
        return Types.SuitDisposition[top[0]];
    }

    private CardDef Def(string cardId)
    {
        if (!_cards.ById.TryGetValue(cardId, out var d)) throw new InvalidOperationException($"카드 정의 없음: {cardId}");
        return d;
    }

    private void GaugeChange(int delta)
    {
        var p = State.Player;
        var s = State.Stats;
        int max = _rules.Gauge.Max;
        int before = p.Gauge;
        p.Gauge = Formula.ClampGauge(p.Gauge + delta, _rules); // 초과 소실 (GDD §2-2)
        int applied = p.Gauge - before;
        if (applied > 0) s.GaugeGained += applied;
        if (applied < 0) s.GaugeLost += -applied;
        if (delta > 0 && before + delta > max) s.GaugeOverflowLost += before + delta - max; // 초과 소실량 계측
        if (before < max && p.Gauge >= max) s.GaugeReached10++; // 크리 "발동 가능" 이벤트 (§3.4 검증용)
    }

    private void Draw(int n)
    {
        var p = State.Player;
        for (int i = 0; i < n; i++)
        {
            if (p.Hand.Count >= _rules.Player.HandMax) break; // 손패 상한 — 초과분 드로우 중단, 소멸 없음 (GDD §3.1)
            if (p.Deck.Count == 0)
            {
                if (p.Discard.Count == 0) break;
                p.Deck = p.Discard;
                p.Discard = new List<CardInstance>();
                if (!_noShuffle) RngFactory.Shuffle(p.Deck, _rng); // 묘지 셔플 순환 (GDD §3.6)
            }
            var last = p.Deck[^1];
            p.Deck.RemoveAt(p.Deck.Count - 1);
            p.Hand.Add(last);
        }
    }

    private CardInstance DiscardFromHand(int uid)
    {
        var p = State.Player;
        int idx = p.Hand.FindIndex(c => c.Uid == uid);
        if (idx < 0) throw new InvalidOperationException($"손패에 없음: uid {uid}");
        var card = p.Hand[idx];
        p.Hand.RemoveAt(idx);
        p.Discard.Add(card);
        return card;
    }

    private void CheckEnd()
    {
        if (State.Result is not null) return;
        var e = State.Enemy;
        if (e.Will <= 0)
        {
            State.Result = BattleResult.Win;
            return;
        }
        // 전 장비 파괴 → 항복 = 전투 승리 + 항복 보상 골드 (combat-model-v0.1, GDD §4.2)
        if (e.Equipment.Count > 0 && e.Equipment.All(eq => eq.Destroyed))
        {
            State.Result = BattleResult.Win;
            State.Stats.Surrender = true;
            State.Player.Gold += _rules.Battle.SurrenderGold;
            return;
        }
        if (State.Player.Will <= 0) State.Result = BattleResult.Lose;
    }

    private int EnemyAttackDownTotal() =>
        State.Enemy.Debuffs.Where(d => d.Kind == EnemyDebuffKind.AttackDown && !d.Suspended).Sum(d => d.Value);

    private bool EnemyHipsterActive() =>
        State.Enemy.Debuffs.Any(d => d.Kind == EnemyDebuffKind.AttackHalve && !d.Suspended);

    private int EnemyAttackUpTotal() => State.Enemy.Buffs.Sum(b => b.Value);

    private int DealWillDamageToEnemy(int amount, bool ignoreDefense = false)
    {
        var e = State.Enemy;
        int v = amount;
        if (!ignoreDefense)
        {
            if (e.DamageReductionNextHit > 0)
            {
                int reduced = Math.Min(v, e.DamageReductionNextHit);
                v -= reduced;
                e.DamageReductionNextHit = 0; // next_hit 소진
            }
            if (e.ReflectNextHit > 0)
            {
                PlayerTakeDamage(e.ReflectNextHit);
                e.ReflectNextHit = 0;
            }
        }
        if (v > 0) e.Will -= v;
        CheckEnd();
        return v;
    }

    /// <summary>내 장비 방어 총합 (ADR-023 ①)</summary>
    private int DefenseTotal() => State.Player.Equipment.Sum(eq => eq.Defense);

    /// <summary>
    /// 플레이어가 피해를 받는다 — <b>방어가 먼저 흡수하고 남은 만큼만 의지를 깎는다</b> (ADR-023 ①).
    /// 분배 계산은 Formula.ComputeAbsorb 가 소유한다(소모 순서의 결정 근거는 그쪽 주석 참조).
    /// 여기 남는 것은 상태 변경뿐 — 방어 차감·의지 차감·계측.
    ///
    /// 가정(GDD 침묵): 흡수된 몫은 「받은 피해」가 아니다 — DamageTakenThisTurn(X05 예약분 산정)에는
    /// 의지가 실제로 깎인 양만 넣는다. 방어로 막았다는 건 상처가 없다는 뜻이므로.
    /// </summary>
    private void PlayerTakeDamage(int v)
    {
        if (v <= 0) return;
        var p = State.Player;
        var (spent, absorbed, toWill) = Formula.ComputeAbsorb(v, p.Equipment.Select(eq => eq.Defense).ToArray());
        // 흡수한 만큼 소모 — 남은 방어는 전투 내내 유지(턴 리셋 없음)
        for (int i = 0; i < p.Equipment.Count; i++) p.Equipment[i].Defense -= spent[i];
        State.Stats.DefenseAbsorbed += absorbed;
        if (toWill > 0)
        {
            p.Will -= toWill;
            p.DamageTakenThisTurn += toWill;
        }
        else
        {
            Log($"방어가 좋아요 {v}를 전부 흡수");
        }
        CheckEnd();
    }

    /// <summary>의지 회복 (maxWill 클램프) — 실제 증가분을 돌려주고 stats에 누적한다</summary>
    private int HealPlayer(int amount)
    {
        var p = State.Player;
        int applied = Formula.ComputeHealApplied(p.Will, p.MaxWill, amount);
        if (applied <= 0) return 0;
        p.Will += applied;
        State.Stats.WillHealed += applied;
        return applied;
    }

    // ── 판정 (v2 4단계 — card-system-v2 §2) ──

    /// <summary>
    /// ①원산지 ②헛소리 ③팩트 ④일반.
    /// 원산지는 무효 태그를 무시한다 — 직접 산 사람의 증언에는 "평가 불가 항목" 반박이 통하지 않는다.
    /// tag는 정확히 1개(단일 초점 원칙)라 v1의 다중 태그 some() 검사가 단순 포함 검사로 바뀐다.
    /// 규칙 본체는 Formula.ComputeJudgement 가 소유한다 — 이 메서드는 외부 호출자용 얇은 래퍼다.
    /// </summary>
    public Judgement Judge(ReviewCardDef card, IReadOnlyList<string> targetTags, IReadOnlyList<string> targetNullTags, bool isOrigin)
        => Formula.ComputeJudgement(card, targetTags, targetNullTags, isOrigin);

    /// <summary>
    /// E04 은신 게이트 — 은신 중 명중 가능 계열(배송)이 아니면 빗나간다.
    /// 제출과 미리보기가 같은 판단을 쓰도록 분리했다.
    /// </summary>
    private bool StealthBlocks(ReviewCardDef card)
    {
        var e = State.Enemy;
        var gate = e.Def.StealthGate;
        return e.Stealth
            && gate is not null
            && (card.Target == TargetKind.Enemy || card.Target == TargetKind.EnemyEquipment)
            && !gate.HittableSuits.Contains(card.Suit);
    }

    /// <summary>대상 결정 결과 — 상태를 바꾸지 않는 순수 산출물</summary>
    private readonly record struct TargetResolution(
        /// <summary>구성품 대상인데 남은 구성품이 없다</summary>
        bool Void,
        IReadOnlyList<string> TargetTags,
        IReadOnlyList<string> TargetNull,
        bool IsOrigin,
        PlayerEquipmentState? MyEq,
        EnemyEquipmentState? EnemyEq);

    /// <summary>
    /// 대상 결정 + 원산지 판정 범위 (card-system-v2 §2):
    ///   적 본체 대상 제출 → origin.enemy 일치 / 구성품 대상 제출 → origin.equipment 일치(이름 완전 일치)
    ///   내 장비 대상·origin 없는 카드(Z##·X##·P해금)는 원산지 영구 미발동
    /// 상태를 바꾸지 않는다 — SubmitReview 와 PreviewSubmit 의 단일 경로다.
    /// </summary>
    private TargetResolution ResolveReviewTarget(ReviewCardDef card, int? enemyEquipmentIndex, int? myEquipmentIndex)
    {
        var p = State.Player;
        var e = State.Enemy;
        var empty = Array.Empty<string>();

        if (card.Target == TargetKind.MyEquipment)
        {
            // 버프 카드는 반드시 내 장비 1개 대상 (GDD §3.3)
            // TS: p.equipment[idx ?? 0] ?? p.equipment[0] — 범위 밖 인덱스는 0번으로 되돌아온다
            int idx = myEquipmentIndex ?? 0;
            var myEq = (idx >= 0 && idx < p.Equipment.Count) ? p.Equipment[idx] : p.Equipment[0];
            return new TargetResolution(false, myEq.Def.Tags, myEq.Def.NullTags, false, myEq, null);
        }
        if (card.Target == TargetKind.EnemyEquipment)
        {
            var alive = e.Equipment.Where(eq => !eq.Destroyed).ToList();
            // 가정(v1 승계): 구성품 대상 카드인데 남은 구성품이 없으면 제출 자체가 낭비(효과 없음)
            if (alive.Count == 0) return new TargetResolution(true, empty, empty, false, null, null);
            int idx = enemyEquipmentIndex ?? -1;
            var picked = (idx >= 0 && idx < e.Equipment.Count) ? e.Equipment[idx] : null;
            var enemyEq = (picked is not null && !picked.Destroyed) ? picked : alive[0];
            return new TargetResolution(
                false,
                enemyEq.Tags,
                // 가정(v1 승계): 구성품 대상의 무효 태그는 적 본체의 무효 태그를 따른다
                e.Def.NullTags,
                card.Origin?.Equipment is not null && card.Origin.Equipment == enemyEq.Name,
                null,
                enemyEq);
        }
        return new TargetResolution(
            false,
            e.Def.WeaknessTags,
            e.Def.NullTags,
            card.Origin?.Enemy is not null && card.Origin.Enemy == e.Def.Id,
            null,
            null);
    }

    /// <summary>내 장비에 붙은 damage_buff 가산 합 — "제출당 1회" (GDD §3.3)</summary>
    private int AttachDamageBuffTotal() =>
        State.Player.Equipment.Sum(eq => eq.Attachments.Where(a => a.Kind == AttachmentKind.DamageBuff).Sum(a => a.Value));

    /// <summary>
    /// 좋아요 환산식 (GDD §2) — 계산 본체는 Formula.ComputeLikes. 여기서는 상태(부착 버프·X05 예약분)만 모은다.
    /// 카드의 <b>첫</b> 의지 피해에만 부착 버프·고정 가산·X05 예약분이 붙는다.
    /// </summary>
    private int FirstWillDamage(int baseValue, double mult, double vanityMult, int fixedAdd) =>
        Formula.ComputeLikes(
            baseValue,
            attachBonus: AttachDamageBuffTotal(),
            mult: mult,
            vanityMult: vanityMult,
            fixedAdd: fixedAdd,
            storedBonus: State.Player.StoredDamageBonus);

    /// <summary>
    /// 판정 배율·의지 전용 배율·고정 가산 (Formula.ComputeMultipliers).
    /// PreviewSubmit 과 SubmitReview 가 이 하나를 공유한다 — 미리보기와 실제가 어긋날 자리가 없다.
    /// </summary>
    private (double Mult, double VanityMult, int FixedAdd) MultipliersFor(ReviewCardDef card, Judgement judgement)
    {
        var e = State.Enemy;
        return Formula.ComputeMultipliers(
            judgement: judgement,
            cardTag: card.Tag,
            cardSuit: card.Suit,
            charging: e.Charging is not null,
            castingWeakness: e.Def.CastingWeakness,
            suitDamageMult: e.Def.SuitDamageMult,
            rules: _rules);
    }

    /// <summary>카드가 내는 의지 피해의 기본 수치 (없으면 null)</summary>
    private static int? CardWillBase(ReviewCardDef card)
    {
        var ef = card.Effect;
        if (ef.Type == "damage") return ef.Value ?? 0;
        return ef.Damage;
    }

    /// <summary>
    /// 제출 미리보기 — 상태를 바꾸지 않고 판정·최종 좋아요·게이지를 계산한다.
    /// UI 가 규칙을 재구현하지 않도록 SubmitReview 와 같은 경로(ResolveReviewTarget·FirstWillDamage)를 쓴다.
    /// </summary>
    public SubmitPreview PreviewSubmit(int cardUid, int? enemyEquipmentIndex = null, int? myEquipmentIndex = null)
    {
        var p = State.Player;
        var inst = p.Hand.FirstOrDefault(c => c.Uid == cardUid);
        if (inst is null) throw new InvalidOperationException("손패에 없는 카드");
        var cardDef = Def(inst.CardId);
        bool affordable = p.Energy >= cardDef.Cost;
        // blocked 3종은 판정이 없으므로 회복도 0 (ADR-023 ② — 빗나감·void에 호응은 없다)
        if (cardDef is not ReviewCardDef card)
        {
            return Blocked(BlockedReason.NotReview, affordable);
        }
        if (StealthBlocks(card))
        {
            return Blocked(BlockedReason.Miss, affordable);
        }
        var t = ResolveReviewTarget(card, enemyEquipmentIndex, myEquipmentIndex);
        if (t.Void)
        {
            return Blocked(BlockedReason.Void, affordable);
        }

        var judgement = Judge(card, t.TargetTags, t.TargetNull, t.IsOrigin);
        if (_buffNoJudgement && card.Target == TargetKind.MyEquipment) judgement = Judgement.Normal;

        var (mult, vanityMult, fixedAdd) = MultipliersFor(card, judgement);

        // 화면에 띄울 수치 — 방어 부여 > 의지 피해 > 구성품 내구도 피해(내구도도 좋아요 단위 · ADR-015)
        int? likes = null;
        LikesKind? likesKind = null;
        int? willBase = CardWillBase(card);
        if (card.Effect.Type == "defense_buff" && t.MyEq is not null)
        {
            likes = Formula.ApplyMult(card.Effect.Value ?? 0, mult); // 원산지 고정 +1 비대상 (ApplyReviewEffect 주석 참조)
            likesKind = LikesKind.Defense;
        }
        else if (willBase is not null)
        {
            likes = FirstWillDamage(willBase.Value, mult, vanityMult, fixedAdd);
            likesKind = LikesKind.Will;
        }
        else if (card.Effect.Type == "equipment_damage" && t.EnemyEq is not null)
        {
            likes = Formula.ApplyMult(card.Effect.Value ?? 0, mult) + fixedAdd; // vanity(의지 전용) 비대상
            likesKind = LikesKind.Equipment;
        }

        // 게이지·회복 모두 **클램프 후 실제 증감**을 준다 (§2-2 초과 소실 / ADR-023 ② maxWill 상한).
        // 판정분과 카드 인라인분을 제출과 같은 순서로 따로 반영해야 값이 맞는다
        // (예: 게이지 0에서 헛소리 −2 → 0, 이어서 인라인 +2 → 2. 합산 후 클램프와 결과가 다르다) —
        // 그 순서 규칙을 Formula.ComputeGaugeDelta 가 소유한다.
        int gauge = Formula.ComputeGaugeDelta(
            current: p.Gauge,
            judgement: judgement,
            inlineGauge: card.Effect.Gauge ?? 0,
            fumbleOverride: _fumbleGaugeOverride,
            rules: _rules);

        // 가정: 제출 도중 의지가 줄어드는 경우(적 반사 reflect 피격)는 미리보기가 알 수 없어 반영하지 않는다.
        int judgeHeal = Formula.ComputeHeal(judgement, p.Will, p.MaxWill, _rules);
        int cardHeal = Formula.ComputeHealApplied(p.Will + judgeHeal, p.MaxWill, card.Effect.Heal ?? 0); // G03 동반
        int heal = judgeHeal + cardHeal;

        return new SubmitPreview
        {
            Judgement = judgement,
            Blocked = null,
            Likes = likes,
            LikesKind = likesKind,
            Gauge = gauge,
            Heal = heal,
            Affordable = affordable,
            Mult = mult,
            VanityMult = vanityMult,
            FixedAdd = fixedAdd,
        };

        static SubmitPreview Blocked(BlockedReason reason, bool affordable) => new()
        {
            Judgement = null,
            Blocked = reason,
            Likes = null,
            LikesKind = null,
            Gauge = 0,
            Heal = 0,
            Affordable = affordable,
            Mult = 0,
            VanityMult = 1,
            FixedAdd = 0,
        };
    }

    // ── 리뷰 제출 (v2 — 카드 1장 = 완성 리뷰) ──

    public SubmitResult SubmitReview(int cardUid, int? enemyEquipmentIndex = null, int? myEquipmentIndex = null)
    {
        var st = State;
        if (st.Result is not null) throw new InvalidOperationException("전투 종료됨");
        var p = st.Player;
        var inst = p.Hand.FirstOrDefault(c => c.Uid == cardUid);
        if (inst is null) throw new InvalidOperationException("손패에 없는 카드");
        var cardDef = Def(inst.CardId);
        if (cardDef is not ReviewCardDef card) throw new InvalidOperationException("리뷰 카드가 아님 (진상 화법은 playSpecial)");

        if (p.Energy < card.Cost) throw new InvalidOperationException("필력 부족");
        p.Energy -= card.Cost;
        DiscardFromHand(cardUid);

        st.Stats.Submissions++;
        p.SuitCounters[card.Suit]++;
        p.LastSuit = card.Suit; // 스냅샷 이후의 누적 — 다음 전투용 (GDD §3.5)

        var e = st.Enemy;
        var gate = e.Def.StealthGate;

        // E04 은신: 은신 중에는 명중 가능 계열(배송)만 명중. 그 외는 빗나감.
        // 가정(v1 승계): 빗나간 리뷰는 판정·게이지 없이 소모만 된다 ("평가 불가" — 물건이 안 옴).
        if (StealthBlocks(card))
        {
            Log($"{card.Name}: 은신 중 — 빗나감");
            return new SubmitResult(true, null);
        }
        // 은신 중 명중 계열 명중 시 은신 해제
        if (e.Stealth && gate is not null && gate.BreakOnHit && (card.Target == TargetKind.Enemy || card.Target == TargetKind.EnemyEquipment))
        {
            e.Stealth = false;
            e.StealthEverBroken = true;
            Log("은신 해제!");
        }

        var t = ResolveReviewTarget(card, enemyEquipmentIndex, myEquipmentIndex);
        if (t.Void) return new SubmitResult(true, null);
        var myEq = t.MyEq;
        var enemyEq = t.EnemyEq;

        var judgement = Judge(card, t.TargetTags, t.TargetNull, t.IsOrigin);
        // 온보딩 1판 한정: 버프 카드(내 장비 대상)는 무판정 = 항상 일반 (GDD §3.3)
        if (_buffNoJudgement && card.Target == TargetKind.MyEquipment) judgement = Judgement.Normal;
        // 배율·고정 가산은 PreviewSubmit 과 같은 함수에서 나온다 (미리보기 드리프트 구조적 봉쇄)
        var (mult, vanityMult, fixedAdd) = MultipliersFor(card, judgement);

        // 판정·게이지는 제출당 1회 (v2는 다중 히트 카드 없음). 헛소리는 온보딩 1판 완화 가능 (§4.4)
        st.Stats.Judgements[judgement]++;
        GaugeChange(
            judgement == Judgement.Fumble && _fumbleGaugeOverride is not null
                ? _fumbleGaugeOverride.Value
                : _rules.Judge.Gauge[judgement]);

        // 호응 회복 (ADR-023 ②): 판정 성공 시 회복, maxWill 상한.
        // 대상 무관 — 내 장비 대상 찬양 리뷰의 팩트 판정도 회복한다("잘 쓴 글에 좋아요가 눌린다").
        HealPlayer(_rules.Judge.Heal[judgement]);

        // 재반박 (B01 counter_rebut): "같은 계열 팩트 리뷰 제출" — 해석: 원산지는 팩트의 상위 판정이므로 포함
        // (직접 산 사람의 증언이 일반 팩트보다 약한 재반박 근거일 수 없다)
        if (judgement == Judgement.Fact || judgement == Judgement.Origin) TryCounterRebut(card.Suit);

        // mult = 판정 배율 × E03 casting_weakness(영창 중 해당 태그 ×N) — v1의 P06 modifier를 적 특성으로 이관
        // vanityMult = E05 계열별 "의지 데미지" 배수 (내구도 등 비대상 — ApplyReviewEffect에서 의지 피해에만)
        ApplyReviewEffect(card, judgement, mult, vanityMult, fixedAdd, myEq, enemyEq);

        // 인라인 게이지 동반 (B02c·A04) — 가정(v1 승계): 제출당 1회, 판정 배율 미적용 고정치
        if (card.Effect.Gauge is int inlineGauge && inlineGauge != 0) GaugeChange(inlineGauge);

        CheckEnd();
        return new SubmitResult(false, judgement);
    }

    /// <summary>
    /// 리뷰 효과 적용 — 좋아요 환산식 (GDD §2):
    ///   최종 좋아요 = ⌊ 기본 × 판정 배율 × 기타 배율 ⌋ + 고정 가산   (내림 1회·최소 1, 고정 가산은 내림 후)
    /// - 기본 = 카드 인쇄 수치 + 부착 버프 가산("제출당 1회" — GDD §3.3, 카드의 첫 의지 피해에만)
    /// - 기타 배율 = casting_weakness(E03, mult에 합산) × vanity(E05, 의지 피해에만)
    /// - 고정 가산 = 원산지 +1(card-system-v2 §2) + X05 예약분. 배율의 영향을 받지 않고 내림 후 더한다.
    ///   해석: 카드의 첫 피해 산출 1회에 적용 — 의지 피해 우선, 의지 피해가 없으면 구성품 내구도 피해
    ///   (내구도도 좋아요 단위 — ADR-015). 피해가 전혀 없는 카드(기절·버프·도트)에선 소멸.
    /// - 판정 배율은 절대 피해 수치에만: 지속 턴·%·개수·드로우 장수·회복량은 판정 무관 (v1 정교화 승계)
    /// </summary>
    /// <param name="mult">판정 × casting_weakness</param>
    /// <param name="originFixedAdd">원산지 고정 좋아요 (MultipliersFor 산출 — 첫 피해 1회에만 소비)</param>
    private void ApplyReviewEffect(
        ReviewCardDef card,
        Judgement judgement,
        double mult,
        double vanityMult,
        int originFixedAdd,
        PlayerEquipmentState? myEq,
        EnemyEquipmentState? enemyEq)
    {
        var st = State;
        var p = st.Player;
        var e = st.Enemy;
        var ef = card.Effect;
        var d = _rules.EffectDefaults;

        int fixedAdd = originFixedAdd;

        // 환산식은 FirstWillDamage 가 소유한다 (PreviewSubmit 과 공유 — 미리보기가 규칙을 재구현하지 않도록)
        void DealCardWillDamage(int baseValue)
        {
            int dmg = FirstWillDamage(baseValue, mult, vanityMult, fixedAdd);
            fixedAdd = 0;
            p.StoredDamageBonus = 0; // X05 예약분은 첫 피해에서 소진 (GDD §2)
            DealWillDamageToEnemy(dmg);
        }

        switch (ef.Type)
        {
            case "damage":
            {
                DealCardWillDamage(ef.Value ?? 0);
                if (st.Result is not null) break;
                if (ef.WeakenNextAction is int wna) e.WeakenNextActionPct = wna; // C02c 동반
                break;
            }
            case "delay_enemy_action":
            {
                // O02·L01·W02 (X01은 PlaySpecial 경유 동일 로직)
                if (ef.Damage is int dmg) DealCardWillDamage(dmg);
                if (st.Result is not null) break;
                ApplyDelayToEnemy();
                break;
            }
            case "stun":
            {
                // L03·W03 — v2는 기절 턴 수를 value로 표기. 경직 내성 면역 (GDD §3.2)
                if (ef.Damage is int dmg) DealCardWillDamage(dmg);
                if (st.Result is not null) break;
                if (e.StaggerImmunityTurns > 0) Log("경직 내성 — 기절 무효");
                else e.StunTurns = Math.Max(e.StunTurns, ef.Value ?? d.StunTurns);
                break;
            }
            case "weaken_next_action":
            {
                // D02·B03c·K04 — %는 판정 무관 고정 (배율은 절대 수치에만 — v1 정교화 승계)
                if (ef.Damage is int dmg) DealCardWillDamage(dmg);
                if (st.Result is not null) break;
                e.WeakenNextActionPct = ef.Value ?? d.WeakenNextActionPct;
                break;
            }
            case "remove_enemy_buff":
            {
                // O03·N02·B02c — 개수는 판정 무관. phase2 알바_리뷰(protectedBy)는 counter_card 일치 카드로만 제거
                if (ef.Damage is int dmg) DealCardWillDamage(dmg);
                if (st.Result is not null) break;
                for (int i = 0; i < (ef.Value ?? d.RemoveBuffCount); i++)
                {
                    // TS: filter 후 .pop() — 조건을 만족하는 **마지막(가장 최근)** 버프
                    int idx = -1;
                    for (int k = e.Buffs.Count - 1; k >= 0; k--)
                    {
                        var b = e.Buffs[k];
                        if (b.ProtectedBy is null || b.CounterCard == card.Id) { idx = k; break; }
                    }
                    if (idx < 0)
                    {
                        // 가정(v1 승계): 제거할 버프가 없으면 다음 받는 피해 감소/반사(포즈·마나 실드)를 대신 해제
                        if (e.DamageReductionNextHit > 0 || e.ReflectNextHit > 0)
                        {
                            e.DamageReductionNextHit = 0;
                            e.ReflectNextHit = 0;
                        }
                        break;
                    }
                    e.Buffs.RemoveAt(idx);
                }
                break;
            }
            case "equipment_damage":
            {
                // Q03·C03c — 내구도도 좋아요 단위 (ADR-015): 판정 배율 + 원산지 고정 +1 적용. vanity(의지 전용)는 비대상
                if (enemyEq is null) break;
                int dmg = Formula.ApplyMult(ef.Value ?? 0, mult) + fixedAdd;
                fixedAdd = 0;
                DamageEnemyEquipment(enemyEq, dmg);
                break;
            }
            case "equipment_dot":
            {
                // M03·A02 — 가정(v1 승계): 판정 배율은 틱 값에 적용(지속 턴 미적용), 기존 도트는 갱신(중첩 없음).
                // 원산지 +1은 미적용 (즉발 피해가 아님 — 해석)
                if (enemyEq is null) break;
                int dur = DurationTurns(ef, d.DotDuration);
                enemyEq.Dot = new EquipmentDot { Value = Formula.ApplyMult(ef.Value ?? 0, mult), Remaining = dur };
                break;
            }
            case "attack_down":
            {
                // K01 — 판정 적중 시 수치 강화 (GDD §3.3). duration: combat은 전투 스코프 상태라 별도 처리 불요
                int uid = _uidSeq++;
                e.Debuffs.Add(new EnemyDebuff
                {
                    Uid = uid,
                    Kind = EnemyDebuffKind.AttackDown,
                    Value = Formula.ApplyMult(ef.Value ?? 0, mult),
                    Suit = card.Suit,
                    Tier = _rules.Battle.AttachedDebuffTier, // 가정(v1 승계): 전투 중 부착 일반 디버프 (「힙스터 인증」 크리만 별도 등급)
                    Suspended = false,
                    BeenRebutted = false,
                    CreatedAt = _uidSeq, // TS 객체 리터럴 평가 순서 그대로 — uid 증가 **후**의 값
                });
                break;
            }
            case "disable_equipment":
            {
                // v2 실데이터 미사용 (YAML 스키마 예비). 경직 내성 면역 — S07 무한 락 봉쇄 규칙 유지
                if (enemyEq is null) break;
                if (e.StaggerImmunityTurns > 0)
                {
                    Log("경직 내성 — 비활성화 무효");
                    break;
                }
                int dur = DurationTurns(ef, d.DisableDuration);
                enemyEq.DisabledTurns = Math.Max(enemyEq.DisabledTurns, dur);
                break;
            }
            case "damage_buff":
            {
                // D03·N03·A01 — 부착은 슬롯 2칸 점유 (GDD §3.9).
                // v2 결정: 리뷰 유래 부착은 전부 슬롯 사용 (v1 uses_attach_slot 필드는 YAML에서 사라짐), 크리 산출물만 예외
                if (myEq is null) break;
                int used = myEq.Attachments.Count(a => a.UsesSlot);
                if (used >= _rules.Player.AttachSlots)
                {
                    Log("부착 슬롯 가득 참 — 부착 실패"); // GDD §3.9 (R15)
                    break;
                }
                myEq.Attachments.Add(new Attachment
                {
                    Kind = AttachmentKind.DamageBuff,
                    Value = Formula.ApplyMult(ef.Value ?? 0, mult),
                    UsesSlot = true,
                });
                break;
            }
            case "defense_buff":
            {
                // ADR-023 ① — 찬양 리뷰(★4~5)가 내 장비에 방어를 부여한다.
                // · 판정 배율을 받는다: 카드 태그가 그 장비 태그에 맞으면 팩트 ×1.5 (GDD §3.3 찬양 규칙과 동일 경로).
                //   mult 에는 E03 casting_weakness 조건 배율도 이미 곱해져 있다(다른 효과와 동일 취급).
                // · **원산지 고정 +1은 적용하지 않는다** — 고정 가산은 GDD §2 좋아요 환산식의 항이고,
                //   그 대상은 의지·내구도 피해(= 좋아요)다. 방어는 좋아요가 아니라 내 장비의 수치이므로
                //   환산식 밖이다. 어차피 내 장비 대상 카드는 원산지 판정이 영구 미발동이라(card-system-v2 §2)
                //   현재 데이터에선 도달 불가 경로지만, 해석을 명시해 둔다.
                // · vanity(E05 계열별 의지 피해 배수)도 비대상 — 적에게 가는 피해 전용이다.
                // · 부착 슬롯(GDD §3.9) 미사용 — PlayerEquipmentState.Defense 주석의 결정 근거 참조.
                if (myEq is null) break;
                int gain = Formula.ApplyMult(ef.Value ?? 0, mult);
                myEq.Defense += gain;
                st.Stats.DefenseGained += gain;
                break;
            }
            default:
                throw new InvalidOperationException($"미구현 리뷰 effect: {ef.Type}");
        }

        if (st.Result is not null) return;
        // 동반 효과 (판정 배율 미적용 — 드로우 장수·회복량은 절대 피해 수치가 아님)
        if (ef.Draw is int draw && draw != 0) Draw(draw); // Z03·A01
        if (ef.Heal is int heal && heal != 0) HealPlayer(heal); // G03 (판정 회복과 같은 클램프 경로 — stats.WillHealed 합산)
        _ = judgement; // TS `void judgement;` — 시그니처 유지를 위해 남긴 인자
    }

    /// <summary>
    /// TS `typeof ef.duration === 'number' ? ef.duration : 기본값` — duration 은 `number | 'combat'` 유니온이라
    /// 숫자일 때만 지속 턴으로 읽고, 'combat'(전투 스코프)이면 스키마 기본값을 쓴다.
    /// </summary>
    private static int DurationTurns(EffectDef ef, int fallback) => ef.Duration is { Turns: int n } ? n : fallback;

    /// <summary>
    /// 지연 적용 공용 경로 (X01·O02·L01·W02 — GDD §3.2). 경직 내성이면 면역.
    /// 준비(charge) 중 지연 적중 시 cancel_on 검사 — 선재 버그 수정: enemies-v1.0의 표기는
    /// 'delay_enemy_action'인데 구현이 구 표기 '지연'만 비교해 E02 내려찍기 캔슬이 불발했다. 양쪽 지원.
    /// </summary>
    private void ApplyDelayToEnemy()
    {
        var e = State.Enemy;
        if (e.StaggerImmunityTurns > 0)
        {
            Log("경직 내성 — 지연 무효");
            return;
        }
        if (e.Charging is not null)
        {
            var chargingAction = e.Def.Actions.FirstOrDefault(a => a.Id == e.Charging.ActionId);
            if (chargingAction is not null && chargingAction.CancelOn.Any(c => c == "delay_enemy_action" || c == "지연"))
            {
                // E02 내려찍기: 준비 중 지연 적중 시 발동 캔슬 (행동 소멸, 패턴 진행)
                e.Charging = null;
                AdvancePattern();
                Log("준비 행동 캔슬!");
                return;
            }
            e.PendingDelay = true; // 준비만 1턴 늦춤
            return;
        }
        e.PendingDelay = true;
    }

    private void DamageEnemyEquipment(EnemyEquipmentState eq, int dmg)
    {
        eq.Durability -= dmg;
        if (eq.Durability <= 0)
        {
            eq.Durability = 0;
            eq.Destroyed = true;
            Log($"장비 파괴: {eq.Name}");
        }
        CheckEnd(); // 전 장비 파괴 → 항복
    }

    /// <summary>
    /// 재반박: 정지된 디버프와 같은 계열의 팩트 리뷰 → 부활 + 게이지 +1 (§3.4/§3.8). 제출당 1개 (가정)
    /// 가정(문언 준수): §3.8·enemies-v1.0 counter_rebut 조건은 "같은 계열 팩트 판정 리뷰 제출"이 전부라
    /// 리뷰의 대상은 보지 않는다 — 내 장비 대상 찬양 리뷰(★4~5, 예: N03)의 팩트 판정도 재반박 성립.
    /// 대상 제한(적 대상 리뷰만)을 둘지는 GDD 명시 필요(에스컬레이션 대상).
    /// </summary>
    private void TryCounterRebut(Suit suit)
    {
        var target = State.Enemy.Debuffs.FirstOrDefault(d => d.Suspended && d.Suit == suit);
        if (target is null) return;
        target.Suspended = false;
        GaugeChange(_rules.Gauge.CounterRebutGain); // 재반박 성공 (GDD §3.4)
        Log($"재반박 성공: {KindLabel(target.Kind)} 부활");
    }

    // ── 특수 카드 (단독 사용, 무판정) ──

    public void PlaySpecial(int uid, int? giftUid = null)
    {
        var st = State;
        if (st.Result is not null) throw new InvalidOperationException("전투 종료됨");
        var p = st.Player;
        var card = p.Hand.FirstOrDefault(c => c.Uid == uid);
        if (card is null) throw new InvalidOperationException("손패에 없는 카드");
        var specDef = Def(card.CardId);
        if (specDef is not SpecialDef spec) throw new InvalidOperationException("특수 카드가 아님");
        if (spec.Layer > _layer) throw new InvalidOperationException($"Layer {spec.Layer} 카드 — 현재 Layer {_layer}");
        if (p.Energy < spec.Cost) throw new InvalidOperationException("필력 부족");
        if (spec.OncePerCombat && p.OncePerCombatUsed.Contains(spec.Id)) throw new InvalidOperationException("전투당 1회 소진");

        var e = st.Enemy;
        var ef = spec.Effect;
        var d = _rules.EffectDefaults;

        // X04는 증정 대상 확인을 지불 전에
        CardInstance? giftCard = null;
        if (ef.Type == "gift_card")
        {
            giftCard = p.Hand.FirstOrDefault(c => giftUid is not null && c.Uid == giftUid.Value && c.Uid != uid);
            if (giftCard is null) throw new InvalidOperationException("증정할 카드 지정 필요");
        }

        p.Energy -= spec.Cost;
        DiscardFromHand(uid);
        if (spec.OncePerCombat) p.OncePerCombatUsed.Add(spec.Id);

        switch (ef.Type)
        {
            case "delay_enemy_action":
            {
                // X01 지연 (GDD §3.2) — 리뷰 카드 지연(O02 등)과 공용 경로 (경직 내성 면역·cancel_on 캔슬)
                ApplyDelayToEnemy();
                break;
            }
            case "damage":
            {
                // X02 별점 테러 — 무판정: 판정 배율·부착 버프 가산 비대상 (진상 화법은 팩트 원칙 바깥 — worldview §1.1).
                // 가정(v1 승계): 특수 카드는 "리뷰"가 아니므로 E04 은신 게이트("리뷰만 명중")의 비대상 — 은신 중에도 적용.
                DealWillDamageToEnemy(ef.Value ?? 0);
                break;
            }
            case "equipment_damage":
            {
                // v2 실데이터 미사용 (v1 X02 전체 장비 −3 잔재 — 스키마 예비로 유지)
                int dmg = ef.Value ?? 0;
                foreach (var eq in e.Equipment.Where(q => !q.Destroyed).ToList()) DamageEnemyEquipment(eq, dmg);
                break;
            }
            case "create_card":
            {
                // X03: pool: any — 전체 카드 풀에서 무작위 생성 (현재 레이어 초과 카드 제외)
                var pool = _cards.AllIds.Where(id => _cards.ById.TryGetValue(id, out var cd) && cd.Layer <= _layer).ToList();
                for (int i = 0; i < (ef.Value ?? d.CreateCardCount) && p.Hand.Count < _rules.Player.HandMax && pool.Count > 0; i++)
                {
                    string id = pool[(int)Math.Floor(_rng() * pool.Count)];
                    p.Hand.Add(new CardInstance { Uid = _uidSeq++, CardId = id });
                }
                break;
            }
            case "gift_card":
            {
                // X04: 증정 카드는 "런 동안" 제외 (GDD §3.6), 비용 ×multiplier 의지 데미지
                var gDef = Def(giftCard!.CardId);
                int idx = p.Hand.FindIndex(c => c.Uid == giftCard.Uid);
                p.Hand.RemoveAt(idx);
                p.RemovedFromRun.Add(giftCard);
                // 가정(GDD 침묵): 0코스트 증정도 §2-1 "최소 1" 적용 → 최소 1 데미지
                DealWillDamageToEnemy(Formula.ApplyMult(gDef.Cost, ef.Multiplier ?? d.GiftMultiplier));
                break;
            }
            case "store_damage_taken":
            {
                p.X05Armed = true; // 이번 턴 받은 피해량 → 다음 턴 시작 시 예약 확정 (GDD §3.2 step1)
                break;
            }
            case "reaction_counter":
            {
                // X06: 설치형, 대기 슬롯 1 (GDD §3.2)
                if (p.Reaction is not null) throw new InvalidOperationException("리액션 대기 슬롯 사용 중");
                p.Reaction = new ReactionState
                {
                    WeakenPct = ef.WeakenPct ?? d.ReactionWeakenPct,
                    ReflectPct = ef.ReflectPct ?? d.ReactionReflectPct,
                };
                break;
            }
            case "retreat":
            {
                // X07: 전투 이탈 (보상 포기). v2 YAML엔 condition이 없어 전 전투 허용 — 있으면 v1 규칙 존중
                if (ef.Condition == "normal_battle_only" && e.Def.Tier != EnemyTier.Normal) throw new InvalidOperationException("일반 전투에서만 이탈 가능");
                st.Result = BattleResult.Retreat;
                break;
            }
            case "gauge":
            {
                GaugeChange(ef.Value ?? 0); // X08 별점 구걸 +3 (v2)
                break;
            }
            case "damage_per_penalty":
            {
                // X09 (Layer 2): Σp(상한 cap_points) × per_point
                int sp = Math.Min(_sigmaP, ef.CapPoints ?? d.PenaltyCapPoints);
                if (sp > 0) DealWillDamageToEnemy(sp * (ef.PerPoint ?? d.PenaltyPerPoint));
                break;
            }
            default:
                throw new InvalidOperationException($"미구현 특수 effect: {ef.Type}");
        }

        if (ef.Gauge is int g && g != 0) GaugeChange(g);
        CheckEnd();
    }

    // ── 퇴고 (GDD §3.2 v1.1 신설 → v2에서 태그 사냥 도구로 승격 — card-system-v2 §7) ──

    /// <summary>
    /// 손패 1장을 버리고 1장 드로우(비용 = rules.player.reviseCost). 턴 제한 없음(필력이 상한).
    /// v2엔 교착이 없어 원하는 태그·원산지를 찾는 용도.
    /// </summary>
    public void Revise(int uid)
    {
        var st = State;
        if (st.Result is not null) throw new InvalidOperationException("전투 종료됨");
        var p = st.Player;
        int cost = _rules.Player.ReviseCost;
        if (p.Energy < cost) throw new InvalidOperationException("필력 부족");
        if (p.Deck.Count + p.Discard.Count == 0) throw new InvalidOperationException("뽑을 카드 없음");
        p.Energy -= cost;
        DiscardFromHand(uid);
        Draw(_rules.Player.ReviseDraw);
        Log("퇴고 — 손패 1장 교체");
    }

    /// <summary>개봉할 택배가 남아 있는가 — UI 가 버튼 노출을 판단한다</summary>
    public bool ParcelAvailable => _parcel is not null && !State.Player.ParcelOpened;

    /// <summary>
    /// 택배 개봉 (ADR-024 ③) — 보스에게 가던 보급품을 뜯어 내 장비로 쓴다.
    /// 필력을 쓰므로 <b>언제 여는가가 결정이 된다</b> — 일찍 열면 찬양 리뷰를 오래 굴리고,
    /// 미루면 그 턴의 필력을 딜에 쓴다. 전투당 1회.
    /// </summary>
    public PlayerEquipmentDef OpenParcel()
    {
        var st = State;
        if (st.Result is not null) throw new InvalidOperationException("전투 종료됨");
        if (_parcel is null) throw new InvalidOperationException("개봉할 택배가 없다");
        var p = st.Player;
        if (p.ParcelOpened) throw new InvalidOperationException("이미 개봉했다");
        int cost = _rules.Player.ParcelCost;
        if (p.Energy < cost) throw new InvalidOperationException("필력 부족");
        p.Energy -= cost;
        p.ParcelOpened = true;
        p.Equipment.Add(new PlayerEquipmentState { Def = _parcel, Defense = 0 });
        Log($"택배 개봉 — {_parcel.Name} 입수 (내 장비)");
        return _parcel;
    }

    // ── 크리티컬 리뷰 (GDD §3.5) ──

    public Disposition UseCritical()
    {
        var st = State;
        if (st.Result is not null) throw new InvalidOperationException("전투 종료됨");
        var p = st.Player;
        var crit = _rules.Critical;
        if (p.Gauge < _rules.Gauge.Max) throw new InvalidOperationException("게이지 부족");
        if (p.CritUsedThisTurn) throw new InvalidOperationException("크리티컬은 턴당 1회");
        int spent = p.Gauge;
        p.Gauge = _rules.Gauge.Min; // 게이지 전량 소모 (에너지 비용 0)
        st.Stats.GaugeLost += spent;
        p.CritUsedThisTurn = true;
        var d = p.Disposition; // 전투 시작 스냅샷 (GDD §3.5)
        st.Stats.Crits.Add(d);
        var e = st.Enemy;

        // E04 은신 게이트 (§3.8 특성 문언 "은신 중에는 배송/CS 계열 리뷰만 명중" — 크리티컬 리뷰도 리뷰다)
        // 가정(GDD 침묵): 적을 향하지 않는 크리(감성 논점 = 내 버프 대상)는 은신 무관.
        // 빗나간 크리도 게이지·턴 사용은 소모된다(빗나간 일반 리뷰가 필력·카드를 소모하는 것과 일관).
        var gate = e.Def.StealthGate;
        if (e.Stealth && gate is not null && d != Types.SuitDisposition[Suit.감성])
        {
            if (!gate.HittableSuits.Contains(Types.DispositionSuit[d]))
            {
                st.Stats.CritMisses++;
                Log("은신 중 — 크리티컬 리뷰 빗나감");
                return d;
            }
            if (gate.BreakOnHit)
            {
                e.Stealth = false;
                e.StealthEverBroken = true;
                Log("은신 해제! (크리티컬 리뷰 명중)");
            }
        }

        // TS 는 논점 문자열로 switch 한다 — C# 에서는 논점의 계열로 분기해 같은 4갈래를 만든다
        switch (Types.DispositionSuit[d])
        {
            case Suit.품질:
                // 「팩트 폭격」 — 방어·저항 무시 고정 피해. 가정(GDD 침묵): 반사(포즈)도 무시
                DealWillDamageToEnemy(crit.FactBomberDamage, ignoreDefense: true);
                break;
            case Suit.성능:
            {
                // 「힙스터 인증」
                int uid = _uidSeq++;
                e.Debuffs.Add(new EnemyDebuff
                {
                    Uid = uid,
                    Kind = EnemyDebuffKind.AttackHalve,
                    Value = crit.HipsterAttackDownPct,
                    Suit = Suit.성능,
                    Tier = crit.HipsterTier, // 사장님 답글 최우선 반박 대상 (R22)
                    Suspended = false,
                    BeenRebutted = false,
                    CreatedAt = _uidSeq, // TS 객체 리터럴 평가 순서 그대로
                });
                break;
            }
            case Suit.배송:
            {
                // 「진상 접수」
                if (e.StaggerImmunityTurns > 0) Log("경직 내성 — 크리 기절 무효");
                else e.StunTurns = Math.Max(e.StunTurns, crit.InconvenienceStunTurns);
                // v1.1(제안 6): 기절과 별개로 다음 행동 위력 감소 — 기절 면역(경직 내성)·기믹 대상에도
                // 크리 가치가 남도록 피해 등가 보강 (보스전 등가 재설계)
                e.WeakenNextActionPct = Math.Min(e.WeakenNextActionPct, crit.InconvenienceWeakenPct);
                // GDD §3.5 (v1.1 명문화): "전투당 1회"는 골드 갈취에만 적용, 기절·위력 감소는 크리마다
                if (!p.InconvenienceGoldUsed)
                {
                    p.Gold += crit.InconvenienceGold[e.Def.Tier];
                    p.InconvenienceGoldUsed = true;
                }
                break;
            }
            case Suit.감성:
            {
                // 「바이럴 확산」 — 현재 버프 효과 2배. 가산 합산 상한, 크리 간 상한 공유 (GDD §3.5)
                int budget = crit.ViralBonusCap - p.ViralBonusGranted;
                bool hasBuff = p.Equipment.Any(eq => eq.Attachments.Any(a => a.Kind == AttachmentKind.DamageBuff));
                if (!hasBuff)
                {
                    // v1.1(제안 5): 바닥 보장 — 버프 0개면 S13 상당 가산 버프 1개 즉시 부착
                    // (크리 산출물이므로 부착 슬롯 미점유, 상한 공유)
                    int add = Math.Min(crit.ViralFloorBonus, budget);
                    if (add > 0 && p.Equipment.Count > 0)
                    {
                        p.Equipment[0].Attachments.Add(new Attachment { Kind = AttachmentKind.DamageBuff, Value = add, UsesSlot = false });
                        p.ViralBonusGranted += add;
                    }
                    break;
                }
                foreach (var eq in p.Equipment)
                {
                    foreach (var a in eq.Attachments)
                    {
                        if (a.Kind != AttachmentKind.DamageBuff || budget <= 0) continue;
                        int add = Math.Min(a.Value, budget);
                        a.Value += add;
                        budget -= add;
                        p.ViralBonusGranted += add;
                    }
                }
                break;
            }
        }
        CheckEnd();
        return d;
    }

    // ── 턴 종료 → 적 턴 → 다음 플레이어 턴 (GDD §3.2) ──

    public void EndTurn()
    {
        var st = State;
        if (st.Result is not null) return;
        EnemyTurn();
        if (st.Result is not null) return;
        StartPlayerTurn();
    }

    private void AdvancePattern()
    {
        var e = State.Enemy;
        e.PatternIndex = (e.PatternIndex + 1) % e.Def.Pattern.Count;
        e.IntentId = e.Def.Pattern[e.PatternIndex];
    }

    private void EnemyTurn()
    {
        var st = State;
        var e = st.Enemy;
        var intent = e.Def.Actions.FirstOrDefault(a => a.Id == e.IntentId);
        if (intent is null) throw new InvalidOperationException($"행동 정의 없음: {e.IntentId}");

        if (e.PendingDelay)
        {
            // X01 지연: 이번 행동 스킵, 인텐트 유지 (가정: 지연 = 1턴 늦춤, 행동 소멸 아님)
            e.PendingDelay = false;
            Log($"지연 — {intent.Name} 스킵");
        }
        else if (e.StunTurns > 0 && intent.AType != EnemyActionType.Gimmick)
        {
            // 기절: attack/buff/steal/stealth 불가. gimmick은 기절 무시 (GDD §3.2)
            // 가정(GDD 침묵): 기절로 막힌 행동은 소멸하고 패턴은 진행된다.
            Log($"기절 — {intent.Name} 불발");
            if (e.Charging is not null) e.Charging = null;
            AdvancePattern();
        }
        else if (intent.AType == EnemyActionType.Gimmick && GimmickOnCooldown(intent))
        {
            // 가정(GDD 침묵): cooldown("N턴마다 발동")은 하한으로 강제 — 기절 불발 등으로 패턴이
            // 앞당겨져도 마지막 발동 후 N턴 전엔 불발(패턴 진행). 정상 패턴(길이 3)에서는 무영향.
            Log($"{intent.Name} — 재사용 대기(cooldown)");
            AdvancePattern();
        }
        else if (AllEquipmentDisabled())
        {
            // S07 비활성화: 해당 행동 봉인 — 가정(GDD 침묵): 장비↔행동 매핑 미정의라
            // "활성 장비가 하나도 없으면 그 턴 비(非)기믹 행동 봉인"으로 해석.
            // 가정(§3.2 악용 #3 봉쇄 의도 준용): 봉인 불발된 적은 장비 재활성 후 1턴간 경직 내성
            // (기절·지연·비활성화 면역) — S07 매턴 재시전 무한 락(S09 락과 동형) 봉쇄. GDD 개정 필요.
            if (intent.AType != EnemyActionType.Gimmick)
            {
                Log($"장비 비활성 — {intent.Name} 봉인");
                // 이번 턴 정리에서 1 감소 → 다음 플레이어 턴까지 유지 (rules.battle.equipmentLockImmunityTurns 주석 참조)
                e.StaggerImmunityTurns = Math.Max(e.StaggerImmunityTurns, _rules.Battle.EquipmentLockImmunityTurns);
                AdvancePattern();
            }
            else
            {
                ExecuteEnemyAction(intent);
            }
        }
        else
        {
            ExecuteEnemyAction(intent);
        }
        if (st.Result is not null) return;

        // 7. 정리: 지속효과 tick, 기믹 카운터, 다음 인텐트 공개 (GDD §3.2)
        foreach (var eq in e.Equipment) if (eq.DisabledTurns > 0) eq.DisabledTurns--;
        if (e.StaggerImmunityTurns > 0) e.StaggerImmunityTurns--;
        if (e.StunTurns > 0)
        {
            e.StunTurns--;
            if (e.StunTurns == 0) e.StaggerImmunityTurns = _rules.Battle.StaggerImmunityTurns; // 기상 → 경직 내성 (GDD §3.2)
        }
        // 보스 페이즈2 (B01 리뷰 조작): 의지 트리거 도달 시 1회 발동 — 가정: 정리 단계에서 체크
        // v1.1(제안 3): 비례 트리거("의지 N% 이하") 우선 — 의지 하향이 실제 완화가 되게 함 (R11 해소)
        if (e.Def.Phase2 is not null && !e.Phase2Done && e.Will <= Phase2Threshold(e))
        {
            e.Phase2Done = true;
            foreach (var ef in e.Def.Phase2.Effects) ApplyEnemyEffect(ef, weaken: 1, reactionApplied: false);
            Log("페이즈2: 리뷰 조작");
        }
    }

    /// <summary>페이즈2 발동 문턱 (v1.1): triggerPct는 maxWill 비례(내림), 없으면 절대값 triggerWill</summary>
    private static int Phase2Threshold(EnemyState e) => Formula.ComputePhase2Threshold(e.MaxWill, e.Def.Phase2!);

    private bool AllEquipmentDisabled()
    {
        var alive = State.Enemy.Equipment.Where(eq => !eq.Destroyed).ToList();
        return alive.Count > 0 && alive.All(eq => eq.DisabledTurns > 0);
    }

    /// <summary>cooldown 있는 행동이 아직 재사용 대기인지 (마지막 발동 턴 기준 — enemies-v1.0 "N턴마다 발동")</summary>
    private bool GimmickOnCooldown(EnemyActionDef intent)
    {
        if (intent.Cooldown is null) return false;
        if (!State.Enemy.CooldownLastFired.TryGetValue(intent.Id, out int last)) return false;
        return State.Turn - last < intent.Cooldown.Value;
    }

    private void ExecuteEnemyAction(EnemyActionDef intent)
    {
        var e = State.Enemy;
        // 준비(charge) 행동: 준비 턴 → 발동 턴 (2턴 점유)
        if (intent.ChargeTurns > 0)
        {
            if (e.Charging is null || e.Charging.ActionId != intent.Id)
            {
                e.Charging = new ChargingState { ActionId = intent.Id, Remaining = intent.ChargeTurns };
                Log($"{intent.Name} 준비 중…");
                return; // 패턴 유지 — 발동 시 진행
            }
            e.Charging.Remaining--;
            if (e.Charging.Remaining > 0) return;
            e.Charging = null;
        }
        FireEnemyAction(intent);
        AdvancePattern();
    }

    private void FireEnemyAction(EnemyActionDef intent)
    {
        var st = State;
        var p = st.Player;
        var e = st.Enemy;

        if (intent.Cooldown is not null) e.CooldownLastFired[intent.Id] = st.Turn; // cooldown 하한 강제용 기록

        // 행동 단위 위력 보정: S08(다음 행동 −50%) + X06 리액션(attack에만, −50% 후 반사)
        double weaken = 1;
        if (e.WeakenNextActionPct != 0)
        {
            weaken *= Formula.WeakenMult(e.WeakenNextActionPct);
            e.WeakenNextActionPct = 0; // 소진
        }
        ReactionState? reaction = null;
        if (intent.AType == EnemyActionType.Attack && p.Reaction is not null)
        {
            // 가정(GDD §3.2 문언 준수): type: attack이면 데미지 0인 행동(E05 찬양 강요)에도 리액션 발동·소진
            reaction = p.Reaction;
            p.Reaction = null;
            weaken *= Formula.WeakenMult(reaction.WeakenPct);
        }

        bool noRebutTarget = false;
        foreach (var ef in intent.Effects)
        {
            if (ef.Condition == "no_rebut_target" && !noRebutTarget) continue;
            bool result = ApplyEnemyEffect(ef, weaken, reaction is not null, reaction?.ReflectPct ?? 0);
            if (result) noRebutTarget = true;
            if (st.Result is not null) return;
        }
    }

    /// <summary>TS 의 반환 `'no_rebut_target' | void` — C# 에서는 true 가 'no_rebut_target' 이다</summary>
    private bool ApplyEnemyEffect(EnemyEffectDef ef, double weaken, bool reactionApplied, int reflectPct = 0)
    {
        var st = State;
        var p = st.Player;
        var e = st.Enemy;

        switch (ef.Op)
        {
            case "damage":
            {
                // E04 기습: 은신이 해제된 상태면 if_stealth_broken 값으로 (가정: 행동 시점에 은신 아님 = 해제됨)
                int baseValue = ef.Value ?? 0;
                if (ef.IfStealthBroken is int broken && !e.Stealth) baseValue = broken;
                // 가·감산과 배율 순서는 Formula.ComputeEnemyDamage 가 소유한다 (「힙스터 인증」 크리 → S08/X06 → 온보딩)
                int v = Formula.ComputeEnemyDamage(
                    baseValue,
                    attackUp: EnemyAttackUpTotal(),
                    attackDown: EnemyAttackDownTotal(),
                    hipsterActive: EnemyHipsterActive(),
                    weaken: weaken,
                    onboardingMult: _onboardingEnemyMult, // §4.4 적 공격 배율
                    rules: _rules);
                PlayerTakeDamage(v);
                if (reactionApplied && reflectPct != 0)
                {
                    int refl = Formula.ComputeReflect(v, reflectPct); // 받은 피해의 N% 반사 (X06)
                    if (refl > 0)
                    {
                        e.Will -= refl;
                        CheckEnd();
                    }
                }
                // E04: 기습 후 은신 종료·해제 플래그 리셋 (다음 사이클 다시 은신)
                if (ef.IfStealthBroken is not null)
                {
                    e.Stealth = false;
                    e.StealthEverBroken = false;
                }
                break;
            }
            case "gold_steal":
            {
                int floor = ef.Floor ?? 0;
                p.Gold = Math.Max(floor, p.Gold - (ef.Value ?? 0)); // 골드 하한 0 (GDD §3.8)
                break;
            }
            case "attack_up":
            {
                e.Buffs.Add(new EnemyBuff
                {
                    Uid = _uidSeq++,
                    Kind = EnemyBuffKind.AttackUp,
                    Value = ef.Value ?? 0,
                    ProtectedBy = ef.Attachment,
                    CounterCard = ef.CounterCard, // phase2 알바_리뷰 → B02c로만 제거
                });
                break;
            }
            case "damage_reduction":
            {
                e.DamageReductionNextHit = ef.Value ?? 0;
                break;
            }
            case "reflect":
            {
                e.ReflectNextHit = ef.Value ?? 0;
                break;
            }
            case "gauge_down":
            {
                GaugeChange(-(ef.Value ?? 0)); // E05 찬양 강요 (GDD §3.4)
                break;
            }
            case "gauge_up":
            {
                GaugeChange(ef.Value ?? 0);
                break;
            }
            case "energy_down":
            {
                if (ef.When == "next_turn") p.EnergyNextTurnPenalty += ef.Value ?? 0; // B01 야근 강요
                break;
            }
            case "stealth_on":
            {
                e.Stealth = true;
                e.StealthEverBroken = false;
                break;
            }
            case "heal":
            {
                e.Will = Math.Min(e.MaxWill, e.Will + (ef.Value ?? 0));
                break;
            }
            case "rebut_debuff":
            {
                // B01 사장님 답글 (GDD §3.8, R22): 활성 디버프 중 Tier/위력 최상위를 "이번 전투 한정 정지"
                var pool = e.Debuffs.Where(d => !d.Suspended && !d.BeenRebutted).ToList();
                if (pool.Count == 0) return true; // 'no_rebut_target' → 의지 +5 효과로
                // 동률: (좋아요 — 시뮬 밖) → 최근. CreatedAt 이 유일하므로 전순서가 성립한다(정렬 안정성 무관)
                pool.Sort((a, b) =>
                {
                    int c = b.Tier - a.Tier;
                    if (c != 0) return c;
                    c = b.Value - a.Value;
                    if (c != 0) return c;
                    return b.CreatedAt - a.CreatedAt;
                });
                var target = pool[0];
                target.Suspended = true;
                target.BeenRebutted = true; // 디버프당 반박 1회 — 재반박 부활 후 다시 반박 불가
                Log($"사장님 답글: {KindLabel(target.Kind)} 정지");
                break;
            }
            default:
                throw new InvalidOperationException($"미구현 적 effect op: {ef.Op}");
        }
        return false;
    }

    private void StartPlayerTurn()
    {
        var st = State;
        var p = st.Player;
        var e = st.Enemy;
        st.Turn++;
        if (st.Turn > _maxTurns)
        {
            st.Turn = _maxTurns; // 기록 턴 클램프 (통계 off-by-one 방지 — 실제 진행된 마지막 턴)
            st.Result = BattleResult.Timeout;
            return;
        }
        p.CritUsedThisTurn = false;

        // 1. 턴 시작: 내 도트 발동·만료 (S06), 예약 효과(X05) — GDD §3.2
        foreach (var eq in e.Equipment)
        {
            if (eq.Dot is not null && !eq.Destroyed)
            {
                DamageEnemyEquipment(eq, eq.Dot.Value);
                eq.Dot.Remaining--;
                if (eq.Dot.Remaining <= 0) eq.Dot = null;
                if (st.Result is not null) return;
            }
        }
        if (p.X05Armed)
        {
            p.StoredDamageBonus += p.DamageTakenThisTurn; // "이번 턴 받은 피해량" 확정
            p.X05Armed = false;
        }
        p.DamageTakenThisTurn = 0;

        // 2. 드로우: 손패 수까지 / 3. 필력: 기본치 + 보정 (이월 없음 — GDD §2-3)
        Draw(Math.Max(0, _rules.Player.HandSize - p.Hand.Count));
        p.Energy = Math.Max(0, _rules.Player.EnergyPerTurn + p.EnergyNextTurnBonus - p.EnergyNextTurnPenalty);
        p.EnergyNextTurnBonus = 0;
        p.EnergyNextTurnPenalty = 0;
    }
}
