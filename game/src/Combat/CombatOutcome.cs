// 전투 종료 정산 — 승/패/이탈을 런에 되써넣고 다음 씬을 정한다 (ADR-029 2차).
//
// 화면(CombatScene)은 여기가 만든 CombatOutcome 을 그리기만 한다. Godot 무의존이라
// 헤드리스 자동 진행(AutoPlay)도 사람과 같은 경로로 정산까지 완주한다.
//
// 정산 규칙의 출처:
//   · 승리 골드 일반 15 / 정예 24 / 보스 50 — GDD §4.2 (ui/game/CONTRACT.md 승계)
//   · 전 구성품 파괴 항복 위로금 6 은 **엔진이 이미 State.Player.Gold 에 더한다**(Battle.CheckEnd) —
//     여기서 또 더하지 않는다. 전투 중 골드 증감은 전부 「전투 후 골드 − 전투 전 골드」로 넘어간다.
//   · 카드 보상 3칸 = 대상 리뷰 2 + 내 장비 리뷰 1 — ADR-027
//   · 패배·시간 초과는 런을 지우지 않는다. MarkEnded("death") 후 결과 씬이 정산한다 — CONTRACT

using ReviewHero.Data;
using ReviewHero.Engine;

namespace ReviewHero.Game.Combat;

public sealed class CombatOutcome
{
    public required BattleResult Result { get; init; }
    public required string Icon { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public required string GoldLine { get; init; }

    /// <summary>보상 카드 후보 (없으면 즉시 정산)</summary>
    public IReadOnlyList<CardDef> RewardPool { get; init; } = Array.Empty<CardDef>();

    /// <summary>정산에 넘길 골드 (전투 중 증감 + 승리 보상)</summary>
    public int GoldDelta { get; init; }

    /// <summary>보상 선택이 필요 없는 경우 미리 확정된 다음 씬 (없으면 카드 선택 후 결정)</summary>
    public string? NextScene { get; init; }

    /// <summary>런 없이 도는 디버그 전투 — 다음 씬이 없다</summary>
    public bool DebugOnly { get; init; }
}

public static class CombatEnd
{
    /// <summary>패배 시 갈 곳. 런 쪽 SceneRouter 가 확정되면 그쪽 상수로 옮긴다</summary>
    public const string ResultScene = "res://scenes/Result.tscn";

    public const string MapScene = "res://scenes/Map.tscn";

    /// <summary>승리 골드 (GDD §4.2)</summary>
    public static int WinGold(EnemyTier tier) => tier switch
    {
        EnemyTier.Boss => 50,
        EnemyTier.Elite => 24,
        _ => 15,
    };

    /// <summary>
    /// 전투가 끝났다 — 런에 되써넣고 다음 씬을 정한다.
    /// <paramref name="run"/> 이 null 이면 디버그 단독 전투라 아무것도 저장하지 않는다.
    /// </summary>
    public static CombatOutcome Resolve(CombatSession s, RunBridge? run, Rng rewardRng)
    {
        var st = s.St;
        var result = st.Result ?? BattleResult.Lose;
        int goldBefore = s.Context.Gold;
        int goldDelta = st.Player.Gold - goldBefore;
        bool isBoss = s.Enemy.Tier == EnemyTier.Boss;

        run?.EndCombat();

        switch (result)
        {
            case BattleResult.Win:
            {
                int reward = WinGold(s.Enemy.Tier);
                bool surrender = st.Stats.Surrender;
                string body = surrender
                    ? "전 구성품이 품절되어 판매자가 영업을 포기했다. 위로금을 뜯어냈다."
                    : $"{s.Enemy.Name}의 존재 등급이 바닥났다. ★5 「정복 후기(구매 확정)」 작성 권한 획득.";
                string goldLine = $"정산: 🪙 +{reward}" + (surrender ? " (+위로금 6)" : string.Empty);
                int total = goldDelta + reward;

                if (run is null)
                {
                    return new CombatOutcome
                    {
                        Result = result, Icon = surrender ? "🏳" : "🛍",
                        Title = "리뷰가 게시되었다", Body = body, GoldLine = goldLine,
                        GoldDelta = total, DebugOnly = true,
                    };
                }

                run.BumpBattlesWon();
                run.WriteBack(st, alive: true);
                run.Save();

                var pool = BuildRewards(s, run, rewardRng);
                return new CombatOutcome
                {
                    Result = result, Icon = surrender ? "🏳" : "🛍",
                    Title = "리뷰가 게시되었다", Body = body, GoldLine = goldLine,
                    GoldDelta = total, RewardPool = pool,
                    // 보상이 있으면 카드를 고른 뒤에 CompleteNode 한다 (deckAdd 를 같이 넘겨야 하므로)
                    NextScene = pool.Count > 0 ? null : (run.CompleteNode(total, null) ?? MapScene),
                };
            }

            case BattleResult.Retreat:
            {
                // X07 전투 이탈 — 보상 포기, 의지 유지 (GDD §4.2)
                string? next = null;
                if (run is not null)
                {
                    run.WriteBack(st, alive: true);
                    run.Save();
                    next = run.CompleteNode(goldDelta, null) ?? MapScene;
                }
                return new CombatOutcome
                {
                    Result = result, Icon = "🚪", Title = "주문 취소",
                    Body = "\"내가 여길 다시 오나 봐라.\" 보상 없이 전장을 빠져나왔다.",
                    GoldLine = goldDelta != 0 ? $"정산: 🪙 {goldDelta:+#;-#;0}" : "정산: 없음",
                    GoldDelta = goldDelta, NextScene = next, DebugOnly = run is null,
                };
            }

            default:
            {
                // lose / timeout — 런은 지우지 않는다. 결과 씬이 정산한다 (CONTRACT)
                string? next = null;
                if (run is not null)
                {
                    run.WriteBack(st, alive: false);
                    run.Save();
                    run.MarkEnded("death");
                    next = ResultScene;
                }
                return new CombatOutcome
                {
                    Result = result, Icon = "★", Title = "별이 꺼졌다",
                    Body = result == BattleResult.Timeout
                        ? "상담 시간이 다 갔다. 끝맺지 못한 리뷰는 「계류」로 남아 아무것도 깎지 못한다."
                        : "의지가 바닥났다. 다음 문장을 쓸 힘이 없어, 서명하지 못한 글만 남았다.",
                    GoldLine = string.Empty, GoldDelta = 0, NextScene = next, DebugOnly = run is null,
                };
            }
        }
    }

    /// <summary>보상 카드를 골랐다(또는 건너뛰었다) → 다음 씬 경로</summary>
    public static string PickReward(RunBridge? run, CombatOutcome o, string? cardId) =>
        run?.CompleteNode(o.GoldDelta, cardId) ?? o.NextScene ?? MapScene;

    // ── 카드 보상 (ADR-027) ────────────────────────────

    /// <summary>
    /// 3칸 = 대상 리뷰 2 + 내 장비 리뷰 1. 한쪽이 비면 다른 쪽으로 채운다 (칸을 비우지 않는다).
    /// 무작위는 반드시 주입 rng — Godot.GD.Randi() 로 게임 규칙을 만들지 않는다.
    /// </summary>
    public static IReadOnlyList<CardDef> BuildRewards(CombatSession s, RunBridge run, Rng rng, int n = 3)
    {
        var owned = new HashSet<string>(run.Deck ?? Array.Empty<string>(), StringComparer.Ordinal);
        var eqNames = new HashSet<string>(s.Enemy.Equipment.Select(q => q.Name), StringComparer.Ordinal);

        // ① 이번 전투 대상의 리뷰 — origin.enemy 일치 또는 origin.equipment 가 이 적의 구성품
        var target = s.Data.Cards.AllIds
            .Select(id => s.Data.Cards.ById[id])
            .OfType<ReviewCardDef>()
            .Where(c => !owned.Contains(c.Id) && c.Origin is not null
                && (c.Origin.Enemy == s.Enemy.Id || (c.Origin.Equipment is { } q && eqNames.Contains(q))))
            .Cast<CardDef>().ToList();

        // ② 내 장비 리뷰 (찬양·방어) — origin 을 주는 것이 아니라 보상 풀에만 넣는다 (ADR-027)
        var gear = s.Data.Cards.AllIds
            .Select(id => s.Data.Cards.ById[id])
            .OfType<ReviewCardDef>()
            .Where(c => !owned.Contains(c.Id) && c.Target == TargetKind.MyEquipment)
            .Cast<CardDef>().ToList();

        var outp = new List<CardDef>();
        outp.AddRange(Sample(gear, 1, rng));
        var seen = new HashSet<string>(outp.Select(c => c.Id), StringComparer.Ordinal);
        outp.AddRange(Sample(target.Where(c => !seen.Contains(c.Id)).ToList(), n - outp.Count, rng));
        if (outp.Count < n)
        {
            seen = new HashSet<string>(outp.Select(c => c.Id), StringComparer.Ordinal);
            outp.AddRange(Sample(gear.Where(c => !seen.Contains(c.Id)).ToList(), n - outp.Count, rng));
        }
        return outp;
    }

    private static List<T> Sample<T>(IReadOnlyList<T> src, int n, Rng rng)
    {
        if (n <= 0 || src.Count == 0) return new List<T>();
        var a = src.ToList();
        RngFactory.Shuffle(a, rng);
        return a.Take(n).ToList();
    }
}
