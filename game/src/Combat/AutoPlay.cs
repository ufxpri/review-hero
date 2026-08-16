// 자동 진행 — 헤드리스 완주 검증용 (ADR-029 2차 완료 기준).
//
// **화면이 누르는 것과 같은 버튼을 누른다** — CombatSession 의 공개 액션만 쓴다.
// 사람 손 없이 한 판을 끝까지 굴려 「전투가 실제로 굴러간다」를 증명하는 것이 목적이므로,
// 이기려고 머리를 쓰지 않는다. 판정 우선 greedy 하나면 족하다.
//
// 판정·좋아요는 전부 Battle.PreviewSubmit 이 준다 — 여기서 규칙을 다시 만들지 않는다 (ADR-025).
// 후보 정렬은 안정 정렬(LINQ OrderBy)만 쓴다 — 같은 시드에서 같은 수를 두게 하기 위함이다.

using ReviewHero.Engine;

namespace ReviewHero.Game.Combat;

public static partial class AutoPlay
{
    private static readonly IReadOnlyDictionary<Judgement, int> Rank = new Dictionary<Judgement, int>
    {
        [Judgement.Origin] = 3,
        [Judgement.Fact] = 2,
        [Judgement.Normal] = 1,
        [Judgement.Fumble] = 0,
    };

    private sealed record Option(int Uid, CardDef Def, int EnemyEq, int MyEq, SubmitPreview Pv);

    /// <summary>한 턴을 자동으로 두고 턴을 넘긴다. 전투가 끝나면 false</summary>
    public static bool PlayTurn(CombatSession s)
    {
        var st = s.St;
        if (st.Result is not null) return false;
        var rules = s.Battle.ActiveRules;
        int safety = 24;

        while (safety-- > 0 && st.Result is null)
        {
            var p = st.Player;

            // 크리티컬 — 게이지 만수위, 턴당 1회. 은신 명중 불가 계열이면 빗나가므로 참는다 (E04)
            var gate = st.Enemy.Def.StealthGate;
            bool critMisses = st.Enemy.Stealth && gate is not null
                && p.Disposition != Disposition.감성논점
                && !gate.HittableSuits.Contains(Types.DispositionSuit[p.Disposition]);
            if (p.Gauge >= rules.Gauge.Max && !p.CritUsedThisTurn && !critMisses)
            {
                if (s.Critical()) continue;
            }

            // 택배(보스전) — 필력에 여유가 있을 때 연다. 열어야 디자인 태그 찬양이 팩트가 된다 (ADR-024 ③)
            if (s.Battle.ParcelAvailable && p.Energy >= rules.Player.ParcelCost + 1)
            {
                if (s.Parcel()) continue;
            }

            var options = Enumerate(s).ToList();
            var playable = options.Where(o => o.Pv.Affordable && o.Pv.Blocked is null).ToList();
            var wanted = playable.Where(o => o.Pv.Judgement != Judgement.Fumble).ToList();

            if (wanted.Count == 0)
            {
                // 무판정 진상 화법(피해형)은 판정이 없으니 별도로 본다
                var special = options.FirstOrDefault(o =>
                    o.Def is SpecialDef sp && o.Pv.Affordable
                    && sp.Effect.Type is "damage" or "gauge" or "delay_enemy_action");
                if (special is not null)
                {
                    s.SelectCard(special.Uid);
                    if (s.Play()) continue;
                }
                // 퇴고로 손패를 갈아 본다 (card-system-v2 §7 태그 사냥)
                if (p.Energy >= rules.Player.ReviseCost && p.Hand.Count > 0
                    && p.Deck.Count + p.Discard.Count > 0)
                {
                    var junk = playable.FirstOrDefault() ?? options.FirstOrDefault();
                    if (junk is not null)
                    {
                        s.SelectCard(junk.Uid);
                        if (s.Revise()) continue;
                    }
                }
                break;
            }

            var best = wanted
                .OrderByDescending(o => Rank[o.Pv.Judgement!.Value])
                .ThenByDescending(o => o.Pv.Likes ?? 0)
                .ThenBy(o => o.Def.Cost)
                .First();

            s.SelectTarget(TargetSlot.EnemyEquipment, best.EnemyEq);
            s.SelectTarget(TargetSlot.MyEquipment, best.MyEq);
            s.SelectCard(best.Uid);
            if (!s.Play()) break; // 엔진이 거절하면(필력 부족 등) 이 턴은 여기까지
        }

        if (st.Result is not null) return false;
        s.EndTurn();
        return st.Result is null;
    }

    /// <summary>전투가 끝날 때까지 자동 진행. 돌린 턴 수를 돌려준다</summary>
    public static int RunToEnd(CombatSession s, int turnCap = 40)
    {
        int turns = 0;
        while (turns < turnCap && s.St.Result is null)
        {
            turns++;
            PlayTurn(s);
        }
        return turns;
    }

    /// <summary>손패 × 가능한 대상 전부 — 미리보기는 엔진이 계산한다</summary>
    private static IEnumerable<Option> Enumerate(CombatSession s)
    {
        var st = s.St;
        foreach (var c in st.Player.Hand.ToList())
        {
            var def = s.DefOf(c);
            if (def.Target == TargetKind.EnemyEquipment)
            {
                bool any = false;
                for (int i = 0; i < st.Enemy.Equipment.Count; i++)
                {
                    if (st.Enemy.Equipment[i].Destroyed) continue;
                    any = true;
                    yield return new Option(c.Uid, def, i, s.SelectedMyEq, s.Battle.PreviewSubmit(c.Uid, i, s.SelectedMyEq));
                }
                if (!any) yield return new Option(c.Uid, def, 0, s.SelectedMyEq, s.Battle.PreviewSubmit(c.Uid, 0, s.SelectedMyEq));
            }
            else if (def.Target == TargetKind.MyEquipment)
            {
                for (int i = 0; i < st.Player.Equipment.Count; i++)
                {
                    yield return new Option(c.Uid, def, s.SelectedEnemyEq, i, s.Battle.PreviewSubmit(c.Uid, s.SelectedEnemyEq, i));
                }
            }
            else
            {
                yield return new Option(c.Uid, def, s.SelectedEnemyEq, s.SelectedMyEq,
                    s.Battle.PreviewSubmit(c.Uid, s.SelectedEnemyEq, s.SelectedMyEq));
            }
        }
    }
}
