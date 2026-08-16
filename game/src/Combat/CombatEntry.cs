// 전투 진입 — 「누구와 어떤 덱으로 싸우는가」를 정한다 (ADR-029 2차).
//
// 우선순위: ① 런 노드의 적 → ② 씬 전환 직전에 지정된 CombatEntry.PendingEnemyId →
//           ③ 명령줄 `--rh-enemy=B01` / 환경변수 RH_ENEMY → ④ E01
// ③④가 **RunStore 없이도 도는 디버그 모드**다. 헤드리스 완주 검증이 이 경로로 돈다.

using ReviewHero.Data;
using ReviewHero.Engine;

namespace ReviewHero.Game.Combat;

public static class CombatEntry
{
    /// <summary>런·지도 쪽에서 씬 전환 직전에 지정할 수 있는 적 id (없으면 런 노드 → 디버그 순)</summary>
    public static string? PendingEnemyId { get; set; }

    /// <summary>디버그 단독 전투의 기본 상대</summary>
    public const string DefaultEnemyId = "E01";

    /// <summary>
    /// 보스전 추가 덱 — ui/game/CONTRACT.md 「보스전 덱」 계약 승계.
    /// design/ YAML 이 아니라 UI 층 상수라 이관 중에도 여기 둔다(정본화는 설계 판단 대상).
    /// </summary>
    public static readonly string[] BossExtraDeck = { "G01", "G02", "D02", "X02", "H02", "H04" };

    public static CombatContext Build(LoadedData data, RunBridge? run, IReadOnlyList<string> cmdline, out string note)
    {
        string? enemyId = run?.EnemyId() ?? PendingEnemyId ?? ArgValue(cmdline, "enemy") ?? EnvValue("RH_ENEMY");
        string picked = enemyId ?? DefaultEnemyId;
        if (!data.Enemies.TryGetValue(picked, out var enemy))
        {
            note = $"알 수 없는 상품 「{picked}」 — {DefaultEnemyId} 로 대신한다";
            enemy = data.Enemies[DefaultEnemyId];
            picked = DefaultEnemyId;
        }
        else
        {
            note = run is null ? $"디버그 단독 전투 — {picked}" : $"런 전투 — {picked}";
        }

        var deck = new List<string>(run?.Deck ?? data.StartingDeck);
        if (enemy.Tier == EnemyTier.Boss) deck.AddRange(BossExtraDeck);

        // 시드: 런이면 런 시드에 층·전투 수를 섞어 판마다 다르되 재현 가능하게 (리플레이 검증 전제).
        // 디버그면 `--rh-seed=N` 또는 고정값 — Godot.GD.Randi() 로 규칙을 만들지 않는다.
        uint seed = run is not null
            ? unchecked(run.Seed ^ ((uint)run.Floor * 2654435761u) ^ ((uint)run.BattlesWon * 40503u))
            : ParseSeed(ArgValue(cmdline, "seed") ?? EnvValue("RH_SEED"));

        return new CombatContext
        {
            Enemy = enemy,
            Deck = deck,
            Seed = seed,
            Gold = run?.Gold ?? 0,
            Will = run?.Will,
            MaxWill = run?.MaxWill,
            SuitCounters = run?.SuitCounters,
            LastSuit = run?.LastSuit,
            RunMode = run is not null,
        };
    }

    /// <summary>보상 추첨용 rng — 전투 rng 와 분리하되 같은 시드에서 재현된다</summary>
    public static Rng RewardRng(uint seed) => RngFactory.Mulberry32(unchecked(seed + 0x9E3779B9u));

    /// <summary><c>--rh-이름=값</c> 형태의 인자 하나</summary>
    public static string? ArgValue(IReadOnlyList<string> args, string name)
    {
        string prefix = $"--rh-{name}=";
        foreach (var a in args)
        {
            if (a.StartsWith(prefix, StringComparison.Ordinal)) return a[prefix.Length..];
        }
        return null;
    }

    public static bool HasFlag(IReadOnlyList<string> args, string name) =>
        args.Contains($"--rh-{name}", StringComparer.Ordinal) || EnvValue($"RH_{name.ToUpperInvariant()}") is not null;

    private static string? EnvValue(string key)
    {
        string? v = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrEmpty(v) ? null : v;
    }

    private static uint ParseSeed(string? s) =>
        uint.TryParse(s, out uint v) ? v : 42u;
}
