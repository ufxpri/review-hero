// 이세계 리뷰용사 — core 데이터 타입 (cards-v2.0.yaml / enemies-v1.0.yaml 스키마 대응)
// core는 fs 접근 금지 — 모든 데이터는 이 타입으로 변환되어 인자로 주입된다 (GDD §1.1).
//
// v2 (card-system-v2.md, ADR-011): 접두+접미 조합형 폐지 → 카드 1장 = 완성 리뷰.
// PrefixDef/SuffixDef/ModifierDef 삭제, ReviewCardDef 단일화 + origin(원산지) 필드 추가.
//
// ── TS→C# 이관 메모 (ADR-029) ────────────────────────────────
// · 문자열 리터럴 유니온(Suit·Judgement·TargetKind…)은 **enum** 으로 옮겼다. `Record<Suit, number>`
//   같은 표는 `Dictionary<Suit, …>` 가 된다. 한글 값(품질·성능…)은 C# 식별자로 그대로 쓸 수 있어
//   `Enum.Parse` 가 YAML 문자열을 바로 받는다. Disposition 만 값에 공백이 있어(「품질 논점」)
//   식별자에서 공백을 뺀 뒤 표시 문자열을 DispositionLabel 로 따로 들고 있다.
// · 멤버는 전부 PascalCase 다. **다만 EffectDef·EnemyEffectDef 는 YAML 키와 1:1 로 붙는 자리다** —
//   TS 로더가 변환 없이 통째로 넘기던 곳(`effect: c.effect`)이라 이름이 어긋나면 조용히 값이 사라진다.
//   C# 로더는 YamlDotNet 의 `UnderscoredNamingConvention` 으로 붙여야 한다
//   (`WeakenNextAction` ↔ `weaken_next_action`, `IfStealthBroken` ↔ `if_stealth_broken`).
//   **이 두 타입에 필드를 추가할 때는 YAML 키를 snake_case 로 되돌린 이름인지 확인할 것.**
//   나머지 타입은 로더가 필드별로 명시 매핑하므로 이 제약을 받지 않는다.
// · 구조적 타이핑 → 명목 타이핑: TS 에서 익명 객체 리터럴이던 것들(stealthGate·castingWeakness·
//   phase2)은 이름 있는 타입으로 승격했다.

using System.Globalization;

namespace ReviewHero.Engine;

public enum Suit
{
    품질,
    성능,
    배송,
    감성,
}

/// <summary>
/// 논점 (GDD §3.5) — 낸 리뷰 카드 계열의 argmax로 정해지는 <b>이번 전투의 화제</b>.
/// 인물 라벨이 아니라 무엇을 물고 늘어졌는지를 가리킨다 (ADR-028 ⑤).
/// 식별자 <c>Disposition</c>·<c>disposition</c>은 세이브·UI 계약 호환을 위해 유지한다.
/// (표시 문자열 「품질 논점」은 DispositionLabel 이 소유한다 — enum 식별자엔 공백을 쓸 수 없다.)
/// </summary>
public enum Disposition
{
    품질논점,
    성능논점,
    배송논점,
    감성논점,
}

public enum Rarity
{
    Basic,
    Common,
    Rare,
    Legendary,
}

/// <summary>태그 판정 4단계 (card-system-v2 §2) — 원산지가 최우선이며 무효 태그를 무시한다</summary>
public enum Judgement
{
    Origin,
    Fact,
    Normal,
    Fumble,
}

public enum TargetKind
{
    Enemy,
    EnemyEquipment,
    MyEquipment,
}

public enum CardKind
{
    Review,
    Special,
}

public enum EnemyTier
{
    Normal,
    Elite,
    Boss,
}

public enum EnemyActionType
{
    Attack,
    Buff,
    Steal,
    Stealth,
    Gimmick,
}

// ── 카드 (v2 — cards-v2.0.yaml) ──────────────────────

/// <summary>원산지 (card-system-v2 §2). 없으면 원산지 판정 영구 미발동 (Z##·X##·P해금 카드)</summary>
public sealed record OriginDef
{
    /// <summary>적 id — 적 본체 대상 제출 시 일치 판정</summary>
    public string? Enemy { get; init; }

    /// <summary>구성품명 — 구성품 대상 제출 시 일치 판정 (이름 완전 일치)</summary>
    public string? Equipment { get; init; }
}

/// <summary>
/// 지속 시간 — TS 의 <c>number | 'combat'</c> 유니온. 숫자면 <see cref="Turns"/>, 문언이면
/// <see cref="Keyword"/>('combat'·'battle'·'next_hit')에 담긴다. 소비 측은
/// <c>ef.Duration is { Turns: int n }</c> 로 TS 의 <c>typeof … === 'number'</c> 분기를 그대로 쓴다.
/// </summary>
public sealed record DurationSpec
{
    public int? Turns { get; init; }
    public string? Keyword { get; init; }

    public static implicit operator DurationSpec(int turns) => new() { Turns = turns };
    public static implicit operator DurationSpec(string keyword) => new() { Keyword = keyword };

    public override string ToString() =>
        Turns?.ToString(CultureInfo.InvariantCulture) ?? Keyword ?? string.Empty;
}

/// <summary>
/// 카드 효과. <b>필드는 YAML 키와 1:1 이다</b> (snake_case ↔ PascalCase) — 로더가 변환 없이 붙는 자리다.
/// </summary>
public sealed record EffectDef
{
    /// <summary>
    /// 효과 종류. 리뷰 카드 실사용: damage / equipment_damage / equipment_dot / stun /
    /// delay_enemy_action / weaken_next_action / remove_enemy_buff / attack_down /
    /// damage_buff / <b>defense_buff</b>(ADR-023 ① — 내 장비에 방어 부여, target: my_equipment)
    /// </summary>
    public required string Type { get; init; }

    /// <summary>defense_buff에서는 「부여할 방어량」 (판정 배율 적용 대상 — Battle.ApplyReviewEffect 주석)</summary>
    public int? Value { get; init; }

    public DurationSpec? Duration { get; init; }

    // ── 동반 효과 (v2 복합 효과 — 판정 배율 적용 범위는 Battle 주석 참조) ──

    /// <summary>의지 피해 동반 (delay_enemy_action·stun·weaken_next_action·remove_enemy_buff)</summary>
    public int? Damage { get; init; }

    /// <summary>드로우 동반 (Z03·A01) — 판정 배율 미적용 (장수는 절대 수치 아님)</summary>
    public int? Draw { get; init; }

    /// <summary>내 의지 회복 동반 (G03) — 판정 배율 미적용</summary>
    public int? Heal { get; init; }

    /// <summary>신뢰도 게이지 동반 (B02c·A04) / X08은 주효과 type: gauge</summary>
    public int? Gauge { get; init; }

    /// <summary>다음 행동 위력 % 동반 (C02c)</summary>
    public int? WeakenNextAction { get; init; }

    // ── 특수 카드(X##) 전용 ──

    /// <summary>X03 create_card</summary>
    public string? Pool { get; init; }

    /// <summary>X04 gift_card</summary>
    public double? Multiplier { get; init; }

    /// <summary>X06 (생략 시 −50)</summary>
    public int? WeakenPct { get; init; }

    /// <summary>X06 (생략 시 50)</summary>
    public int? ReflectPct { get; init; }

    /// <summary>예비 (v1 X07 normal_battle_only 등)</summary>
    public string? Condition { get; init; }

    /// <summary>X09</summary>
    public int? PerPoint { get; init; }

    /// <summary>X09</summary>
    public int? CapPoints { get; init; }

    // ── 예비 (v2 데이터 미사용 — 하위 호환·향후 카드용) ──

    public int? Hits { get; init; }
    public string? TargetScope { get; init; }
    public bool? UsesAttachSlot { get; init; }
}

/// <summary>
/// 카드 공통부. TS 의 <c>CardDef = ReviewCardDef | SpecialDef</c> 판별 유니온을 상속으로 옮겼다 —
/// <c>card.kind !== 'review'</c> 분기는 <c>card is not ReviewCardDef</c> 패턴이 된다.
/// stars·rarity 는 SpecialDef 에서만 선택 필드라 공통부로 올리지 않았다(TS 선언 그대로).
/// </summary>
public abstract record CardDef
{
    public abstract CardKind Kind { get; }

    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>필력 (리뷰 카드는 최소 1 — card-system-v2 §5)</summary>
    public required int Cost { get; init; }

    public required TargetKind Target { get; init; }

    /// <summary>리뷰 본문 (UI 정본)</summary>
    public string? Text { get; init; }

    public required EffectDef Effect { get; init; }

    /// <summary>효과 요약 1줄</summary>
    public string? Ui { get; init; }

    /// <summary>덱 1장 제한 (런 레벨 검증)</summary>
    public bool Unique { get; init; }

    /// <summary>생략 시 1 (MVP)</summary>
    public int Layer { get; init; } = 1;
}

/// <summary>리뷰 카드 1장 = 완성 리뷰 (v2 단일 초점 원칙 — 태그 정확히 1개)</summary>
public sealed record ReviewCardDef : CardDef
{
    public override CardKind Kind => CardKind.Review;

    public OriginDef? Origin { get; init; }

    /// <summary>논점 산정 기준 (GDD §3.5)</summary>
    public required Suit Suit { get; init; }

    /// <summary>판정 태그 정확히 1개 (배열 금지 — 로드 시 검증)</summary>
    public required string Tag { get; init; }

    /// <summary>★ 1~5. 4 이상 = 찬양 = 버프 계열 (§6)</summary>
    public required int Stars { get; init; }

    public required Rarity Rarity { get; init; }
}

/// <summary>진상 화법 (X01~X09) — 무원산지·무판정. 배율(×1.5/×1.0/×0.5) 비대상</summary>
public sealed record SpecialDef : CardDef
{
    public override CardKind Kind => CardKind.Special;

    public int? Stars { get; init; }
    public Rarity? Rarity { get; init; }

    /// <summary>예비 (v2 데이터 미사용)</summary>
    public bool OncePerCombat { get; init; }
}

public sealed class CardIndex
{
    public required IReadOnlyDictionary<string, CardDef> ById { get; init; }

    /// <summary>X03 create_card(pool: any)용 전체 카드 id — 레이어 필터는 Battle이 수행</summary>
    public required IReadOnlyList<string> AllIds { get; init; }
}

// ── 적 ────────────────────────────────────────────────

/// <summary>
/// 적 행동 효과. <b>필드는 YAML 키와 1:1 이다</b> (EffectDef 와 같은 이유).
/// TS 의 인덱스 시그니처 <c>[k: string]: unknown</c> 은 <see cref="Extra"/> 로 옮겼다 —
/// 스키마에 없는 특성 키를 로더가 흘려 담는 자리다.
/// </summary>
public sealed record EnemyEffectDef
{
    public required string Op { get; init; }
    public int? Value { get; init; }
    public int? Floor { get; init; }

    /// <summary>'battle' | 'next_hit' | 숫자</summary>
    public DurationSpec? Duration { get; init; }

    public string? When { get; init; }
    public string? Condition { get; init; }
    public int? IfStealthBroken { get; init; }
    public string? Attachment { get; init; }
    public string? CounterCard { get; init; }

    /// <summary>스키마 밖의 키 (TS 인덱스 시그니처 대응)</summary>
    public IReadOnlyDictionary<string, object?> Extra { get; init; } =
        new Dictionary<string, object?>();
}

public sealed record EnemyActionDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required EnemyActionType AType { get; init; }
    public IReadOnlyList<EnemyEffectDef> Effects { get; init; } = Array.Empty<EnemyEffectDef>();

    /// <summary>생략 시 0</summary>
    public int ChargeTurns { get; init; }

    /// <summary>'delay_enemy_action' (구 표기 '지연' 하위 호환)</summary>
    public IReadOnlyList<string> CancelOn { get; init; } = Array.Empty<string>();

    public int? Cooldown { get; init; }
}

public sealed record EnemyEquipmentDef
{
    public required string Name { get; init; }
    public required int Durability { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}

/// <summary>E04 stealth_gate: 은신 중 명중 가능 계열 + 명중 시 은신 해제</summary>
public sealed record StealthGateDef
{
    public required IReadOnlyList<Suit> HittableSuits { get; init; }
    public bool BreakOnHit { get; init; } = true;
}

/// <summary>
/// E03 casting_weakness: 영창(준비) 중 해당 태그 리뷰 효과 ×N
/// (v1은 P06 modifier로 구현 — v2에서 적 특성으로 이관)
/// </summary>
public sealed record CastingWeaknessDef
{
    public required string Tag { get; init; }
    public required double Multiplier { get; init; }
}

/// <summary>
/// 보스 페이즈2 (B01 리뷰 조작). v1.1: 비례 트리거(TriggerPct, "의지 N% 이하") 우선,
/// 절대값(TriggerWill)은 하위 호환
/// </summary>
public sealed record Phase2Def
{
    public int? TriggerWill { get; init; }
    public int? TriggerPct { get; init; }
    public IReadOnlyList<EnemyEffectDef> Effects { get; init; } = Array.Empty<EnemyEffectDef>();
}

public sealed record EnemyDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required EnemyTier Tier { get; init; }
    public required int Will { get; init; }
    public IReadOnlyList<string> WeaknessTags { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> NullTags { get; init; } = Array.Empty<string>();
    public IReadOnlyList<EnemyEquipmentDef> Equipment { get; init; } = Array.Empty<EnemyEquipmentDef>();
    public IReadOnlyList<EnemyActionDef> Actions { get; init; } = Array.Empty<EnemyActionDef>();

    /// <summary>인텐트 공개 순서. 끝나면 처음부터 반복</summary>
    public required IReadOnlyList<string> Pattern { get; init; }

    /// <summary>E05 vanity: 계열별 의지 데미지 배수</summary>
    public IReadOnlyDictionary<Suit, double>? SuitDamageMult { get; init; }

    public StealthGateDef? StealthGate { get; init; }
    public CastingWeaknessDef? CastingWeakness { get; init; }
    public Phase2Def? Phase2 { get; init; }
}

// ── 플레이어 장비 ─────────────────────────────────────

public sealed record PlayerEquipmentDef
{
    public required string Name { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> NullTags { get; init; } = Array.Empty<string>();
}

// ── 상수·매핑 ─────────────────────────────────────────

/// <summary>types.ts 의 모듈 상수·함수 (C# 은 최상위 상수를 담을 그릇이 필요하다)</summary>
public static class Types
{
    /// <summary>계열 ↔ 태그 매핑 (combat-model-v0.1 §계열↔태그, enemies-v1.0 태그 12종)</summary>
    public static readonly IReadOnlyDictionary<Suit, IReadOnlyList<string>> SuitTags =
        new Dictionary<Suit, IReadOnlyList<string>>
        {
            [Suit.품질] = new[] { "마감", "내구도", "무게" },
            [Suit.성능] = new[] { "출력", "연비", "이펙트" },
            [Suit.배송] = new[] { "속도", "구성품", "응대" },
            [Suit.감성] = new[] { "디자인", "감성", "개연성" },
        };

    public static readonly IReadOnlyDictionary<Suit, Disposition> SuitDisposition =
        new Dictionary<Suit, Disposition>
        {
            [Suit.품질] = Disposition.품질논점,
            [Suit.성능] = Disposition.성능논점,
            [Suit.배송] = Disposition.배송논점,
            [Suit.감성] = Disposition.감성논점,
        };

    /// <summary>논점 → 계열 역매핑 (E04 은신 게이트의 크리티컬 리뷰 계열 판정용 — §3.8)</summary>
    public static readonly IReadOnlyDictionary<Disposition, Suit> DispositionSuit =
        new Dictionary<Disposition, Suit>
        {
            [Disposition.품질논점] = Suit.품질,
            [Disposition.성능논점] = Suit.성능,
            [Disposition.배송논점] = Suit.배송,
            [Disposition.감성논점] = Suit.감성,
        };

    /// <summary>
    /// 논점별 크리티컬 리뷰의 표시 명칭 (GDD §3.5). <b>표시 전용</b> —
    /// 판정·수치 로직은 이 표를 참조하지 않는다. 논점 = 화제, 크리티컬 = 그때 하는 행위.
    /// </summary>
    public static readonly IReadOnlyDictionary<Disposition, string> CriticalName =
        new Dictionary<Disposition, string>
        {
            [Disposition.품질논점] = "팩트 폭격",
            [Disposition.성능논점] = "힙스터 인증",
            [Disposition.배송논점] = "진상 접수",
            [Disposition.감성논점] = "바이럴 확산",
        };

    /// <summary>
    /// 논점의 표시 문자열 — TS 에서는 Disposition 유니온의 값 자체였다(「품질 논점」).
    /// enum 식별자에 공백을 쓸 수 없어 분리했고, 세이브·UI 계약은 이 문자열을 쓴다.
    /// </summary>
    public static readonly IReadOnlyDictionary<Disposition, string> DispositionLabel =
        new Dictionary<Disposition, string>
        {
            [Disposition.품질논점] = "품질 논점",
            [Disposition.성능논점] = "성능 논점",
            [Disposition.배송논점] = "배송 논점",
            [Disposition.감성논점] = "감성 논점",
        };

    /// <summary>시작 장비 3종 (GDD §3.9 — 01 §3 표 그대로, 패시브 없음)</summary>
    public static readonly IReadOnlyList<PlayerEquipmentDef> StartingEquipment = new[]
    {
        new PlayerEquipmentDef { Name = "이세계 보급형 롱소드", Tags = new[] { "마감" }, NullTags = new[] { "연비" } },
        new PlayerEquipmentDef { Name = "물려받은 가죽 갑옷", Tags = new[] { "내구도", "무게" }, NullTags = new[] { "이펙트" } },
        new PlayerEquipmentDef { Name = "위조 인증 목걸이", Tags = new[] { "감성" }, NullTags = new[] { "출력" } },
    };

    /// <summary>
    /// 보스에게 가던 보급품 — 보스전에서 개봉해 내 장비가 된다 (ADR-024 ③).
    /// <c>#디자인</c>을 갖는 유일한 장비다: 시작 장비 3종에 디자인 태그가 없어 디자인 찬양 카드가
    /// 항상 일반 판정이었는데, 이걸 열면 그 카드들이 팩트로 바뀐다 — <b>개봉이 곧 덱 해금이다.</b>
    /// 무효 태그가 <c>응대</c>인 것은 농담이자 설정이다. 답글을 단 적이 없으니 평가할 응대가 없다.
    /// </summary>
    public static readonly PlayerEquipmentDef BossParcelEquipment = new()
    {
        Name = "본사 직영 금박 명패",
        Tags = new[] { "디자인", "감성" },
        NullTags = new[] { "응대" },
    };

    public static Suit? TagToSuit(string tag)
    {
        foreach (var (suit, tags) in SuitTags)
        {
            if (tags.Contains(tag)) return suit;
        }
        return null;
    }

    public static CardIndex BuildCardIndex(IEnumerable<CardDef> cards)
    {
        var byId = new Dictionary<string, CardDef>(StringComparer.Ordinal);
        var allIds = new List<string>();
        foreach (var c in cards)
        {
            byId[c.Id] = c;
            allIds.Add(c.Id);
        }
        return new CardIndex { ById = byId, AllIds = allIds };
    }

    // ── YAML 문자열 ↔ enum (로더용) ────────────────────
    // TS 에서는 문자열 유니온이라 변환이 없었다. C# enum 으로 옮기면서 생긴 경계이므로
    // **여기 한 곳에만** 둔다 — 로더가 각자 문자열을 비교하기 시작하면 표기가 갈린다.

    public static Suit ParseSuit(string s) => ParseEnum<Suit>(s, "계열");

    public static Judgement ParseJudgement(string s) => ParseEnum<Judgement>(s, "판정");

    public static Rarity ParseRarity(string s) => ParseEnum<Rarity>(s, "희귀도");

    public static EnemyTier ParseEnemyTier(string s) => ParseEnum<EnemyTier>(s, "적 등급");

    public static EnemyActionType ParseEnemyActionType(string s) => ParseEnum<EnemyActionType>(s, "적 행동 종류");

    /// <summary>YAML 표기는 snake_case (enemy / enemy_equipment / my_equipment)</summary>
    public static TargetKind ParseTargetKind(string s) => s switch
    {
        "enemy" => TargetKind.Enemy,
        "enemy_equipment" => TargetKind.EnemyEquipment,
        "my_equipment" => TargetKind.MyEquipment,
        _ => throw new ArgumentException($"알 수 없는 대상: {s} (enemy|enemy_equipment|my_equipment)", nameof(s)),
    };

    public static string ToYaml(TargetKind t) => t switch
    {
        TargetKind.Enemy => "enemy",
        TargetKind.EnemyEquipment => "enemy_equipment",
        TargetKind.MyEquipment => "my_equipment",
        _ => throw new ArgumentOutOfRangeException(nameof(t)),
    };

    /// <summary>논점 표시 문자열(「품질 논점」·공백 없는 「품질논점」 모두) → enum</summary>
    public static Disposition ParseDisposition(string s)
    {
        var compact = s.Replace(" ", string.Empty);
        return ParseEnum<Disposition>(compact, "논점");
    }

    private static T ParseEnum<T>(string s, string what) where T : struct, Enum
    {
        if (Enum.TryParse<T>(s, ignoreCase: true, out var v) && Enum.IsDefined(v)) return v;
        throw new ArgumentException($"알 수 없는 {what}: {s} ({string.Join("|", Enum.GetNames<T>())})", nameof(s));
    }
}
