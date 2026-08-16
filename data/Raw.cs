// YAML 원시 DTO — design/*.yaml 의 모양 그대로다. 여기서 규칙을 해석하지 않는다.
//
// TS 로더는 `yaml.load()` 가 준 `Record<string, any>` 를 직접 더듬었다. C# 은 그 자리를 DTO 로
// 세워 오타가 컴파일에 걸리게 한다. **엔진 타입으로의 변환은 전부 Loader 가 명시로 한다** —
// DTO 를 엔진 타입에 직접 붙이지 않는 이유는 tag 배열 금지·no_judgement 같은 검증이
// 변환 지점에 있어야 하기 때문이다 (TS convertReview/convertSpecial 과 같은 자리).
//
// tag 가 string 이 아니라 object? 인 것이 핵심이다: 배열이면 로드 실패시켜야 하므로
// (단일 초점 원칙 — card-system-v2 §4) 타입 단계에서 막지 않고 값으로 받아 검사한다.

using ReviewHero.Engine;

namespace ReviewHero.Data;

internal sealed class RawCardsFile
{
    public List<RawStartingEntry> StartingDeck { get; set; } = new();
    public List<RawCard> PastLife { get; set; } = new();
    public List<RawCard> EnemyReviews { get; set; } = new();
    public List<RawCard> EquipmentReviews { get; set; } = new();
    public List<RawSpecial> Specials { get; set; } = new();
}

internal sealed class RawStartingEntry
{
    public string Id { get; set; } = string.Empty;
    public bool Irremovable { get; set; }
}

internal sealed class RawOrigin
{
    public string? Enemy { get; set; }
    public string? Equipment { get; set; }
}

internal sealed class RawCard
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public RawOrigin? Origin { get; set; }
    public string Suit { get; set; } = string.Empty;

    /// <summary>문자열 1개여야 한다. 배열이면 로드 실패 (단일 초점 원칙)</summary>
    public object? Tag { get; set; }

    public int Cost { get; set; }
    public int Stars { get; set; }
    public string Rarity { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string? Text { get; set; }
    public EffectDef Effect { get; set; } = null!;
    public string? Ui { get; set; }
    public bool? Unique { get; set; }
    public int? Layer { get; set; }

    /// <summary>리뷰 섹션에는 있으면 안 된다 (specials 전용)</summary>
    public bool? NoJudgement { get; set; }
}

internal sealed class RawSpecial
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Cost { get; set; }
    public int? Stars { get; set; }
    public string? Rarity { get; set; }
    public string? Target { get; set; }
    public string? Text { get; set; }
    public EffectDef Effect { get; set; } = null!;
    public string? Ui { get; set; }
    public bool? Unique { get; set; }
    public int? Layer { get; set; }
    public bool? NoJudgement { get; set; }
}

// ── 적 ────────────────────────────────────────────────

internal sealed class RawEnemiesFile
{
    public List<RawEnemy> Enemies { get; set; } = new();
    public List<RawEnemy> Bosses { get; set; } = new();
}

internal sealed class RawEnemy
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public int Will { get; set; }
    public List<string>? WeaknessTags { get; set; }
    public List<string>? NullTags { get; set; }
    public List<RawTrait>? Traits { get; set; }
    public List<RawEnemyEquipment>? Equipment { get; set; }
    public List<RawAction>? Actions { get; set; }
    public List<string> Pattern { get; set; } = new();
    public RawPhase2? Phase2 { get; set; }
}

/// <summary>상시 특성 — 기계 필드는 특성별 자유 확장이라 합집합으로 받는다 (TS 도 동일)</summary>
internal sealed class RawTrait
{
    public string? Id { get; set; }

    /// <summary>E05 vanity — 계열별 의지 데미지 배수</summary>
    public Dictionary<string, double>? DamageMultiplierFromSuit { get; set; }

    /// <summary>E04 stealth_gate</summary>
    public List<string>? HittableSuitsWhileStealth { get; set; }

    public bool? BreakStealthOnHit { get; set; }

    /// <summary>E03 casting_weakness</summary>
    public string? AppliesToTag { get; set; }

    public double? Multiplier { get; set; }
}

internal sealed class RawEnemyEquipment
{
    public string Name { get; set; } = string.Empty;
    public int Durability { get; set; }
    public List<string>? Tags { get; set; }
}

internal sealed class RawAction
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<EnemyEffectDef>? Effects { get; set; }
    public int? ChargeTurns { get; set; }
    public List<string>? CancelOn { get; set; }
    public int? Cooldown { get; set; }
}

internal sealed class RawPhase2
{
    public string Trigger { get; set; } = string.Empty;
    public List<EnemyEffectDef>? Effects { get; set; }
}
