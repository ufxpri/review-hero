// design/*.yaml → 엔진 타입 변환 (packages/sim/src/data.ts 이관 — ADR-029).
// 엔진(core)은 fs 접근 금지이므로 파일을 읽는 자리는 여기 하나다 (GDD §1.1).
//
// v2 (card-system-v2.md, ADR-011): 5개 섹션(starting_deck / past_life / enemy_reviews /
// equipment_reviews / specials) → 카드 인덱스 + 시작 덱 id 배열 + irremovable 집합.
// 로드 시 검증: tag는 정확히 1개(단일 초점 원칙 — 배열이면 에러).
//
// 미구현(런 레벨 — 감사 기록): GDD §3.6 덱 구축 규칙(유니크 1장 제한 X04/X08,
// 생계형 리뷰 표식 irremovable)은 카드 획득/제거가 존재하는 런 레이어의 검증 사항이다.
// 전투 엔진(Battle)은 주어진 덱을 그대로 사용하며 현 시뮬은 시작 덱(+ CLI 주입 덱)이 고정이라
// 실해 없음. irremovable 정보는 런 레이어 착수 대비로 여기서 이미 파싱해 노출한다.
//
// ── design 디렉터리 찾기 ────────────────────────────────
// TS 는 `import.meta.url` 로 소스 위치에서 잡았다. C# 은 호출자마다 실행 위치가 다르다 —
// 테스트는 engine.tests/bin/Debug/net8.0 에서, CLI 는 `dotnet run` 이라 sim/bin/Debug/net8.0 에서,
// 사람이 직접 실행하면 저장소 루트에서 돈다. 그래서 **위로 올라가며 표식 파일을 찾는다**:
// design/cards-v2.0.yaml 이 있는 첫 조상이 저장소 루트다. 실행 디렉터리·출력 디렉터리 양쪽에서
// 시도하고, 둘 다 실패하면 컴파일 시점의 소스 경로(CallerFilePath)로 마지막 시도를 한다
// (바이너리만 딴 데로 복사한 경우). 「design 이라는 이름의 폴더」가 아니라 표식 파일로 판정하므로
// 이름이 같은 남의 폴더를 잘못 잡지 않는다.

using System.Runtime.CompilerServices;
using ReviewHero.Engine;

namespace ReviewHero.Data;

/// <summary>로드 결과 한 벌 (TS <c>LoadedData</c>)</summary>
public sealed record LoadedData(
    CardIndex Cards,
    IReadOnlyList<string> StartingDeck,
    /// <summary>생계형 리뷰(제거 불가) 표식 — 런 레이어 카드 제거 노드의 검증용</summary>
    IReadOnlySet<string> Irremovable,
    IReadOnlyDictionary<string, EnemyDef> Enemies);

/// <summary>카드 파일 1개의 전체 결과 (인덱스 + 시작 덱 + 제거 불가 표식)</summary>
public sealed record LoadedCards(
    CardIndex Index,
    IReadOnlyList<string> StartingDeck,
    IReadOnlySet<string> Irremovable);

public static class Loader
{
    public const string CardsFileName = "cards-v2.0.yaml";
    public const string EnemiesFileName = "enemies-v1.0.yaml";

    private static string? _designDir;

    /// <summary>저장소 design/ 디렉터리 (실행 위치 무관 — 파일 주석의 탐색 규칙 참조)</summary>
    public static string DesignDir => _designDir ??= ResolveDesignDir();

    public static LoadedData LoadAll(string? designDir = null)
    {
        string dir = designDir ?? DesignDir;
        var cards = LoadCardsFull(Path.Combine(dir, CardsFileName));
        var enemies = LoadEnemies(Path.Combine(dir, EnemiesFileName));
        return new LoadedData(cards.Index, cards.StartingDeck, cards.Irremovable, enemies);
    }

    /// <summary>카드 인덱스만 필요할 때 (tag 배열 금지 등 검증은 그대로 수행한다)</summary>
    public static CardIndex LoadCards(string yamlPath) => LoadCardsFull(yamlPath).Index;

    public static LoadedCards LoadCardsFull(string yamlPath)
    {
        var raw = Yaml.Load<RawCardsFile>(yamlPath);
        var defs = new List<CardDef>();

        // 섹션 순서가 곧 CardIndex.AllIds 순서다 — X03(무작위 카드 생성)이 이 목록을 rng 로 뽑으므로
        // 순서가 바뀌면 같은 시드에서 다른 카드가 나온다. TS 와 같은 순서를 유지할 것.
        foreach (var c in raw.PastLife) defs.Add(ConvertReview(c, "past_life"));
        foreach (var c in raw.EnemyReviews) defs.Add(ConvertReview(c, "enemy_reviews"));
        foreach (var c in raw.EquipmentReviews) defs.Add(ConvertReview(c, "equipment_reviews"));
        foreach (var x in raw.Specials) defs.Add(ConvertSpecial(x));

        var index = Types.BuildCardIndex(defs);

        var startingDeck = raw.StartingDeck.Select(e => e.Id).ToList();
        var irremovable = raw.StartingDeck.Where(e => e.Irremovable).Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in startingDeck)
        {
            if (!index.ById.ContainsKey(id)) throw new InvalidDataException($"starting_deck의 미정의 카드: {id}");
        }
        return new LoadedCards(index, startingDeck, irremovable);
    }

    public static IReadOnlyDictionary<string, EnemyDef> LoadEnemies(string yamlPath)
    {
        var raw = Yaml.Load<RawEnemiesFile>(yamlPath);
        var map = new Dictionary<string, EnemyDef>(StringComparer.Ordinal);
        foreach (var e in raw.Enemies.Concat(raw.Bosses)) map[e.Id] = ConvertEnemy(e);
        return map;
    }

    // ── 카드 ──────────────────────────────────────────

    private static ReviewCardDef ConvertReview(RawCard c, string section)
    {
        // 단일 초점 원칙 (card-system-v2 §4): tag는 정확히 1개 — 배열이면 로드 실패
        if (c.Tag is not string and not null)
        {
            throw new InvalidDataException($"카드 {c.Id}: tag 배열 금지 — 단일 초점 원칙 (card-system-v2 §4)");
        }
        if (c.Tag is not string tag || tag.Length == 0)
        {
            throw new InvalidDataException($"카드 {c.Id}: tag는 문자열 1개 필수 ({section})");
        }
        if (c.NoJudgement is true)
        {
            throw new InvalidDataException($"카드 {c.Id}: 리뷰 섹션({section})에 no_judgement 불가 — specials로 이동");
        }

        return new ReviewCardDef
        {
            Id = c.Id,
            Name = c.Name,
            Origin = c.Origin is null ? null : new OriginDef { Enemy = c.Origin.Enemy, Equipment = c.Origin.Equipment },
            Suit = Types.ParseSuit(c.Suit),
            Tag = tag,
            Cost = c.Cost,
            Stars = c.Stars,
            Rarity = Types.ParseRarity(c.Rarity),
            Target = Types.ParseTargetKind(c.Target),
            Text = c.Text,
            Effect = c.Effect,
            Ui = c.Ui,
            Unique = c.Unique ?? false,
            Layer = c.Layer ?? 1,
        };
    }

    private static SpecialDef ConvertSpecial(RawSpecial x)
    {
        if (x.NoJudgement is not true)
        {
            throw new InvalidDataException($"특수 카드 {x.Id}: no_judgement: true 필수 (진상 화법 — 무판정)");
        }

        return new SpecialDef
        {
            Id = x.Id,
            Name = x.Name,
            Cost = x.Cost,
            Stars = x.Stars,
            Rarity = x.Rarity is null ? null : Types.ParseRarity(x.Rarity),
            Target = Types.ParseTargetKind(x.Target ?? "enemy"),
            Text = x.Text,
            Effect = x.Effect,
            Ui = x.Ui,
            Unique = x.Unique ?? false,
            Layer = x.Layer ?? 1,
        };
    }

    // ── 적 ────────────────────────────────────────────

    private static EnemyActionDef ConvertAction(RawAction a) => new()
    {
        Id = a.Id,
        Name = a.Name,
        AType = Types.ParseEnemyActionType(a.Type),
        Effects = a.Effects ?? new List<EnemyEffectDef>(),
        ChargeTurns = a.ChargeTurns ?? 0,
        CancelOn = a.CancelOn ?? new List<string>(),
        Cooldown = a.Cooldown,
    };

    private static EnemyDef ConvertEnemy(RawEnemy e)
    {
        // 특성 파싱 (E05 vanity, E04 stealth_gate, E03 casting_weakness)
        IReadOnlyDictionary<Suit, double>? suitDamageMult = null;
        StealthGateDef? stealthGate = null;
        CastingWeaknessDef? castingWeakness = null;

        foreach (var t in e.Traits ?? new List<RawTrait>())
        {
            if (t.DamageMultiplierFromSuit is { Count: > 0 })
            {
                suitDamageMult = t.DamageMultiplierFromSuit.ToDictionary(kv => Types.ParseSuit(kv.Key), kv => kv.Value);
            }
            if (t.HittableSuitsWhileStealth is not null)
            {
                stealthGate = new StealthGateDef
                {
                    HittableSuits = t.HittableSuitsWhileStealth.Select(Types.ParseSuit).ToList(),
                    BreakOnHit = t.BreakStealthOnHit ?? true,
                };
            }
            // v2: casting_weakness(E03)는 접두 modifier(P06) 폐지로 적 특성 판정으로 이관 — 엔진이 태그 대조
            if (t.AppliesToTag is not null && t.Multiplier is { } mult)
            {
                castingWeakness = new CastingWeaknessDef { Tag = t.AppliesToTag, Multiplier = mult };
            }
        }

        Phase2Def? phase2 = null;
        if (e.Phase2 is { } p2)
        {
            // v1.1(제안 3): "의지 50% 이하" 비례 트리거 지원 — %가 있으면 TriggerPct, 없으면 절대값 TriggerWill
            var m = System.Text.RegularExpressions.Regex.Match(p2.Trigger, @"(\d+)");
            int n = m.Success ? int.Parse(m.Groups[1].Value) : 0;
            var effects = p2.Effects ?? new List<EnemyEffectDef>();
            phase2 = p2.Trigger.Contains('%')
                ? new Phase2Def { TriggerPct = n, Effects = effects }
                : new Phase2Def { TriggerWill = n, Effects = effects };
        }

        return new EnemyDef
        {
            Id = e.Id,
            Name = e.Name,
            Tier = Types.ParseEnemyTier(e.Tier),
            Will = e.Will,
            WeaknessTags = e.WeaknessTags ?? new List<string>(),
            NullTags = e.NullTags ?? new List<string>(),
            Equipment = (e.Equipment ?? new List<RawEnemyEquipment>())
                .Select(q => new EnemyEquipmentDef { Name = q.Name, Durability = q.Durability, Tags = q.Tags ?? new List<string>() })
                .ToList(),
            Actions = (e.Actions ?? new List<RawAction>()).Select(ConvertAction).ToList(),
            Pattern = e.Pattern,
            SuitDamageMult = suitDamageMult,
            StealthGate = stealthGate,
            CastingWeakness = castingWeakness,
            Phase2 = phase2,
        };
    }

    // ── design/ 탐색 ──────────────────────────────────

    private static string ResolveDesignDir()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory, SourceDir() })
        {
            if (SearchUp(start) is { } found) return found;
        }
        throw new DirectoryNotFoundException(
            $"design/{CardsFileName} 을 찾지 못했다 — 실행 위치({Environment.CurrentDirectory})·출력 위치" +
            $"({AppContext.BaseDirectory}) 어느 쪽 조상에도 없다. designDir 을 직접 넘길 것.");
    }

    private static string? SearchUp(string? start)
    {
        var dir = start is null ? null : new DirectoryInfo(start);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "design");
            if (File.Exists(Path.Combine(candidate, CardsFileName))) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>컴파일 시점의 이 파일 위치 = data/Loader.cs (TS 의 import.meta.url 대응)</summary>
    private static string? SourceDir([CallerFilePath] string path = "") => Path.GetDirectoryName(path);
}
