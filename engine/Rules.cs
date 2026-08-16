// 밸런스 상수 단일 정본 (ADR-025).
//
// **코드 어디에도 밸런스 매직 넘버를 두지 않는다.** 규칙 수치는 전부 여기를 거치고,
// 밸런스 라운드는 코드가 아니라 이 표를 고친다. Battle 은 cfg.Rules 로 부분 오버라이드를
// 받으므로 시뮬레이터가 같은 시드에서 A/B 를 돌릴 수 있다.
//
//   new Battle(cfg with { Rules = new RulesOverride { { "judge.mult.normal", 0.75 } } })
//
// 적 의지·공격력, 카드 비용·수치는 여기가 아니라 YAML(enemies/cards)이 정본이다 —
// 그쪽은 콘텐츠이고 여기는 콘텐츠에 걸리는 규칙이다.
//
// ── TS→C# 이관 메모 (ADR-029) ────────────────────────────────
// TS 의 `RulesOverride` 는 부분 중첩 객체(`{ judge: { mult: { normal: 0.9 } } }`)였다.
// C# 에는 「구조가 같고 전부 optional 인 타입」을 공짜로 만드는 수단이 없어(TS 의 매핑 타입
// `Partial<T>` 부재), 옮기는 길이 셋이었다:
//   ① 부분 미러 클래스 12개를 손으로 유지    — 컴파일 타임 안전. 대신 RulesConfig 를 고칠 때마다
//      두 벌을 같이 고쳐야 하고, 그러다 어긋나면 ADR-025 가 없앤 규칙 드리프트가 되돌아온다.
//   ② 값을 전부 Dictionary<string, double> 로 평탄화 — 병합은 쉽지만 Battle 이
//      `rules["judge.mult.normal"]` 을 읽게 되어 「수치는 코드가 아니라 표에 있다」가 안 읽힌다.
//   ③ **점 표기 경로 목록 + 리플렉션 적용** ← 채택.
// ③을 고른 이유: 시뮬 CLI 의 `--rule judge.mult.normal=0.9` 가 이미 점 표기 경로이고, TS 의
// `applyRulePath` 도 문자열 경로를 객체에 심는 방식이었다. 경로 하나 = 레버 하나라는 ADR-025 의
// 단위와 그대로 일치하며, 테스트도 `new RulesOverride { { "judge.mult.normal", 0.75 } }` 로
// CLI 와 **같은 경로 문법**을 쓴다(문법이 갈리면 A/B 가 재현되지 않는다).
// 대가는 경로 오타가 컴파일이 아니라 실행 시점에 잡힌다는 것이다 — 그래서 잘못된 경로는
// 조용히 무시하지 않고 반드시 던진다. RulesConfig 자체는 평범한 속성이라 Battle 은
// `rules.Judge.Mult[judgement]` 처럼 정적 타입으로 읽는다(읽기 경로엔 리플렉션이 없다).

using System.Collections;
using System.Globalization;
using System.Reflection;

namespace ReviewHero.Engine;

/// <summary>플레이어 기본치 (GDD §3.1)</summary>
public sealed class PlayerRules
{
    public int Will { get; set; }
    public int EnergyPerTurn { get; set; }

    /// <summary>턴 시작 시 채우는 손패 수</summary>
    public int HandSize { get; set; }

    /// <summary>손패 상한 — 초과분은 드로우 중단(소멸 없음)</summary>
    public int HandMax { get; set; }

    /// <summary>퇴고 비용 (필력)</summary>
    public int ReviseCost { get; set; }

    /// <summary>택배 개봉 비용 (필력) — ADR-024 ③</summary>
    public int ParcelCost { get; set; }

    /// <summary>퇴고 1회로 뽑는 장수 (버리는 장수는 1로 고정 — uid 지정 교체)</summary>
    public int ReviseDraw { get; set; }

    /// <summary>장비당 부착 슬롯 (GDD §3.9)</summary>
    public int AttachSlots { get; set; }

    internal PlayerRules Clone() => (PlayerRules)MemberwiseClone();
}

/// <summary>태그 판정 4단계 (card-system-v2 §2 · GDD §3.3)</summary>
public sealed class JudgeRules
{
    /// <summary>좋아요 배율</summary>
    public Dictionary<Judgement, double> Mult { get; set; } = new();

    /// <summary>신뢰도 게이지 증감</summary>
    public Dictionary<Judgement, int> Gauge { get; set; } = new();

    /// <summary>호응 회복 — 의지 (ADR-023 ②)</summary>
    public Dictionary<Judgement, int> Heal { get; set; } = new();

    /// <summary>원산지 고정 좋아요 — 내림 뒤 가산 (GDD §2)</summary>
    public int OriginFixedAdd { get; set; }

    internal JudgeRules Clone() => new()
    {
        Mult = new Dictionary<Judgement, double>(Mult),
        Gauge = new Dictionary<Judgement, int>(Gauge),
        Heal = new Dictionary<Judgement, int>(Heal),
        OriginFixedAdd = OriginFixedAdd,
    };
}

/// <summary>신뢰도 게이지 (GDD §2-2 · §3.4)</summary>
public sealed class GaugeRules
{
    public int Min { get; set; }

    /// <summary>이 값에 도달하면 크리티컬 발동 가능</summary>
    public int Max { get; set; }

    /// <summary>재반박 성공 시 게이지 (GDD §3.4/§3.8)</summary>
    public int CounterRebutGain { get; set; }

    internal GaugeRules Clone() => (GaugeRules)MemberwiseClone();
}

/// <summary>크리티컬 리뷰 (GDD §3.5)</summary>
public sealed class CriticalRules
{
    /// <summary>「팩트 폭격」 — 방어·저항 무시 고정 피해</summary>
    public int FactBomberDamage { get; set; }

    /// <summary>「힙스터 인증」 — 적 공격력 감소 %</summary>
    public int HipsterAttackDownPct { get; set; }

    /// <summary>그 디버프의 반박 저항 등급 (R22)</summary>
    public int HipsterTier { get; set; }

    /// <summary>「바이럴 확산」 가산 누적 상한 (크리 간 공유)</summary>
    public int ViralBonusCap { get; set; }

    /// <summary>「바이럴 확산」 — 버프 0개일 때 즉시 부착하는 바닥 보장 가산 (v1.1 제안 5)</summary>
    public int ViralFloorBonus { get; set; }

    /// <summary>「진상 접수」 — 기절 턴</summary>
    public int InconvenienceStunTurns { get; set; }

    /// <summary>「진상 접수」 — 다음 행동 위력 % (음수. v1.1 제안 6)</summary>
    public int InconvenienceWeakenPct { get; set; }

    /// <summary>「진상 접수」 — 등급별 골드 갈취(전투당 1회)</summary>
    public Dictionary<EnemyTier, int> InconvenienceGold { get; set; } = new();

    internal CriticalRules Clone()
    {
        var c = (CriticalRules)MemberwiseClone();
        c.InconvenienceGold = new Dictionary<EnemyTier, int>(InconvenienceGold);
        return c;
    }
}

/// <summary>온보딩 보정 (GDD §4.4) — 판 번호로 고르는 것이 아니라 값을 주입한다</summary>
public sealed class OnboardingRules
{
    /// <summary>1판 적 공격 배율</summary>
    public double EnemyDamageMult1 { get; set; }

    /// <summary>2판 적 공격 배율</summary>
    public double EnemyDamageMult2 { get; set; }

    /// <summary>1판 헛소리 게이지 (완화값)</summary>
    public int FumbleGauge1 { get; set; }

    internal OnboardingRules Clone() => (OnboardingRules)MemberwiseClone();
}

/// <summary>전투 진행</summary>
public sealed class BattleRules
{
    /// <summary>초과 시 timeout 패배</summary>
    public int MaxTurns { get; set; }

    /// <summary>기절 해제 후 경직 내성 지속 턴 (GDD §3.2)</summary>
    public int StaggerImmunityTurns { get; set; }

    /// <summary>
    /// 장비 전 비활성(S07) 봉인 불발 시 부여하는 경직 내성 — 적 턴 정리에서 1 감소하므로
    /// 「다음 플레이어 턴 1턴 유지」가 되려면 StaggerImmunityTurns + 1 이어야 한다.
    /// </summary>
    public int EquipmentLockImmunityTurns { get; set; }

    /// <summary>전투 중 부착한 일반 디버프의 반박 저항 등급 (「힙스터 인증」 크리만 3)</summary>
    public int AttachedDebuffTier { get; set; }

    /// <summary>전 장비 파괴 항복 승리 보상 (GDD §4.2)</summary>
    public int SurrenderGold { get; set; }

    internal BattleRules Clone() => (BattleRules)MemberwiseClone();
}

/// <summary>
/// YAML 필드 누락 시의 스키마 기본값 — 콘텐츠 수치가 아니라 "값이 없을 때 이 규칙을 쓴다".
/// 카드·적 수치의 정본은 여전히 YAML 이다(ADR-025). 코드에 <c>?? 숫자</c>를 남기지 않기 위해 모은다.
/// </summary>
public sealed class EffectDefaults
{
    /// <summary>stun 카드의 value 생략 시</summary>
    public int StunTurns { get; set; }

    /// <summary>weaken_next_action 카드의 value 생략 시</summary>
    public int WeakenNextActionPct { get; set; }

    /// <summary>remove_enemy_buff 개수 생략 시</summary>
    public int RemoveBuffCount { get; set; }

    /// <summary>X03 생성 장수 생략 시</summary>
    public int CreateCardCount { get; set; }

    /// <summary>equipment_dot duration 생략 시</summary>
    public int DotDuration { get; set; }

    /// <summary>disable_equipment duration 생략 시</summary>
    public int DisableDuration { get; set; }

    /// <summary>X04 증정 배수 생략 시</summary>
    public double GiftMultiplier { get; set; }

    /// <summary>X06 weaken_pct 생략 시</summary>
    public int ReactionWeakenPct { get; set; }

    /// <summary>X06 reflect_pct 생략 시</summary>
    public int ReactionReflectPct { get; set; }

    /// <summary>X09 cap_points 생략 시</summary>
    public int PenaltyCapPoints { get; set; }

    /// <summary>X09 per_point 생략 시</summary>
    public int PenaltyPerPoint { get; set; }

    internal EffectDefaults Clone() => (EffectDefaults)MemberwiseClone();
}

public sealed partial class RulesConfig
{
    public required PlayerRules Player { get; set; }
    public required JudgeRules Judge { get; set; }
    public required GaugeRules Gauge { get; set; }
    public required CriticalRules Critical { get; set; }
    public required OnboardingRules Onboarding { get; set; }
    public required BattleRules Battle { get; set; }
    public required EffectDefaults EffectDefaults { get; set; }

    /// <summary>구획까지 전부 새로 뜬 사본 — 오버라이드가 원본(Default)을 건드리지 못하게 한다</summary>
    public RulesConfig Clone() => new()
    {
        Player = Player.Clone(),
        Judge = Judge.Clone(),
        Gauge = Gauge.Clone(),
        Critical = Critical.Clone(),
        Onboarding = Onboarding.Clone(),
        Battle = Battle.Clone(),
        EffectDefaults = EffectDefaults.Clone(),
    };
}

/// <summary>
/// 부분 오버라이드 — <c>구획.필드</c> 또는 <c>구획.표.키</c> 점 표기 경로 하나가 레버 하나다
/// (ADR-025). 시뮬 A/B 에서 레버 하나만 바꿔 넣기 위함.
/// <code>
/// new RulesOverride { { "judge.mult.normal", 0.75 }, { "critical.factBomberDamage", 24 } }
/// </code>
/// 같은 경로를 두 번 넣으면 나중 값이 이긴다(TS 의 <c>over[section][key] = value</c> 와 동일).
/// 적용 순서를 보존하려고 Dictionary 가 아니라 목록으로 들고 있다 — 결정성이 우선이다.
/// </summary>
public sealed class RulesOverride : IEnumerable<KeyValuePair<string, double>>
{
    private readonly List<KeyValuePair<string, double>> _entries = new();

    public int Count => _entries.Count;

    /// <summary>컬렉션 이니셜라이저용 — <c>new RulesOverride { { "judge.mult.normal", 0.75 } }</c></summary>
    public void Add(string path, double value)
    {
        int i = _entries.FindIndex(e => string.Equals(e.Key, path, StringComparison.OrdinalIgnoreCase));
        var entry = new KeyValuePair<string, double>(path, value);
        if (i >= 0) _entries[i] = entry;
        else _entries.Add(entry);
    }

    /// <summary>체이닝용</summary>
    public RulesOverride Set(string path, double value)
    {
        Add(path, value);
        return this;
    }

    /// <summary>
    /// 시뮬 CLI 의 <c>--rule 경로=값</c> 한 줄을 심는다 (TS <c>applyRulePath</c> 이관).
    /// 값은 불변 문화권(<c>0.75</c>)으로 읽는다 — 로캘에 따라 소수점이 바뀌면 A/B 가 재현되지 않는다.
    /// </summary>
    public void AddSpec(string spec)
    {
        int eq = spec.IndexOf('=');
        if (eq < 0) throw new ArgumentException($"--rule 형식은 경로=값 이다: {spec}", nameof(spec));
        string raw = spec[(eq + 1)..];
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            throw new ArgumentException($"--rule 값을 읽을 수 없다: {raw}", nameof(spec));
        }
        Add(spec[..eq], value);
    }

    public IEnumerator<KeyValuePair<string, double>> GetEnumerator() => _entries.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

// ── 기본값·병합 (TS rules.ts 의 모듈 레벨 DEFAULT_RULES·mergeRules 이관) ──
public sealed partial class RulesConfig
{
    /// <summary>
    /// 기본 수치 — GDD v1.2 기준. 밸런스 라운드 1(v2) 확정치.
    /// 근거·측정: design/balance-report-v2-round1.md
    ///
    /// <c>Judge.Mult[Normal]</c> 이 1.0 이 아닌 것은 오타가 아니다 — 플레이어 화력을 GDD §3.1 이 선언한
    /// 턴당 8~12 대역으로 되돌리는 레버다(실측 12.8 → 11.1). 팩트/일반 격차도 1.5배에서 1.67배로
    /// 벌어져 태그를 겨냥하는 플레이의 값이 올라간다. 자세한 것은 보고서 §3.
    ///
    /// TS 의 <c>DEFAULT_RULES</c> 와 마찬가지로 공유 인스턴스다. <see cref="Merge"/> 가 항상
    /// 사본을 만들므로 오버라이드는 이 값을 건드리지 않는다.
    /// </summary>
    public static readonly RulesConfig Default = CreateDefault();

    /// <summary>Default 와 같은 값의 <b>새</b> 인스턴스 (기본값에서 출발해 손으로 고칠 때)</summary>
    public static RulesConfig CreateDefault() => new()
    {
        Player = new PlayerRules
        {
            Will = 30,
            EnergyPerTurn = 3,
            HandSize = 5,
            HandMax = 8,
            ReviseCost = 1,
            ParcelCost = 1,
            ReviseDraw = 1,
            AttachSlots = 2,
        },
        Judge = new JudgeRules
        {
            Mult = new Dictionary<Judgement, double>
            {
                [Judgement.Origin] = 1.5,
                [Judgement.Fact] = 1.5,
                [Judgement.Normal] = 0.9,
                [Judgement.Fumble] = 0.5,
            },
            Gauge = new Dictionary<Judgement, int>
            {
                [Judgement.Origin] = 4,
                [Judgement.Fact] = 3,
                [Judgement.Normal] = 0,
                [Judgement.Fumble] = -2,
            },
            Heal = new Dictionary<Judgement, int>
            {
                [Judgement.Origin] = 2,
                [Judgement.Fact] = 1,
                [Judgement.Normal] = 0,
                [Judgement.Fumble] = 0,
            },
            OriginFixedAdd = 1,
        },
        Gauge = new GaugeRules { Min = 0, Max = 10, CounterRebutGain = 1 },
        Critical = new CriticalRules
        {
            FactBomberDamage = 20,
            HipsterAttackDownPct = 50,
            HipsterTier = 3,
            ViralBonusCap = 12,
            ViralFloorBonus = 3,
            InconvenienceStunTurns = 1,
            InconvenienceWeakenPct = -50,
            InconvenienceGold = new Dictionary<EnemyTier, int>
            {
                [EnemyTier.Normal] = 8,
                [EnemyTier.Elite] = 15,
                [EnemyTier.Boss] = 25,
            },
        },
        Onboarding = new OnboardingRules
        {
            EnemyDamageMult1 = 0.75,
            EnemyDamageMult2 = 0.9,
            FumbleGauge1 = -1,
        },
        Battle = new BattleRules
        {
            MaxTurns = 30,
            StaggerImmunityTurns = 1,
            EquipmentLockImmunityTurns = 2,
            AttachedDebuffTier = 1,
            SurrenderGold = 6,
        },
        EffectDefaults = new EffectDefaults
        {
            StunTurns = 1,
            WeakenNextActionPct = -50,
            RemoveBuffCount = 1,
            CreateCardCount = 1,
            DotDuration = 2,
            DisableDuration = 1,
            GiftMultiplier = 4,
            ReactionWeakenPct = -50,
            ReactionReflectPct = 50,
            PenaltyCapPoints = 5,
            PenaltyPerPoint = 3,
        },
    };

    /// <summary>부분 오버라이드를 병합한다 — 시뮬 A/B 에서 레버 하나만 바꿔 넣기 위함</summary>
    public static RulesConfig Merge(RulesConfig baseRules, RulesOverride? over)
    {
        if (over is null || over.Count == 0) return baseRules;
        var result = baseRules.Clone();
        foreach (var (path, value) in over) ApplyPath(result, path, value);
        return result;
    }

    /// <summary>경로 하나를 덮는다. 잘못된 경로는 <b>조용히 무시하지 않고 던진다</b> — 오타 난 A/B 는 A/A 다.</summary>
    private static void ApplyPath(RulesConfig target, string path, double value)
    {
        var parts = path.Split('.');
        if (parts.Length is < 2 or > 3)
        {
            throw new ArgumentException(
                $"알 수 없는 rules 경로: {path} (구획.필드 또는 구획.표.키 — 구획: {SectionNames()})", nameof(path));
        }

        var sectionProp = FindProp(typeof(RulesConfig), parts[0])
            ?? throw new ArgumentException($"알 수 없는 rules 구획: {parts[0]} (구획: {SectionNames()})", nameof(path));
        object section = sectionProp.GetValue(target)!;

        var fieldProp = FindProp(section.GetType(), parts[1])
            ?? throw new ArgumentException(
                $"알 수 없는 rules 필드: {parts[0]}.{parts[1]} (필드: {MemberNames(section.GetType())})", nameof(path));

        if (parts.Length == 2)
        {
            if (fieldProp.GetValue(section) is IDictionary)
            {
                throw new ArgumentException(
                    $"rules 경로가 짧다: {path} ({parts[0]}.{parts[1]} 는 표라서 키가 필요하다)", nameof(path));
            }
            fieldProp.SetValue(section, ConvertValue(value, fieldProp.PropertyType, path));
            return;
        }

        if (fieldProp.GetValue(section) is not IDictionary table)
        {
            throw new ArgumentException(
                $"rules 경로가 너무 깊다: {path} ({parts[0]}.{parts[1]} 는 표가 아니다)", nameof(path));
        }

        var args = fieldProp.PropertyType.GetGenericArguments();
        if (!Enum.TryParse(args[0], parts[2], ignoreCase: true, out object? key) || key is null)
        {
            throw new ArgumentException(
                $"알 수 없는 rules 표 키: {path} (키: {string.Join('|', Enum.GetNames(args[0])).ToLowerInvariant()})",
                nameof(path));
        }
        table[key] = ConvertValue(value, args[1], path);
    }

    /// <summary>
    /// 경로 값은 언제나 double 로 들어온다(CLI 도 테스트도 숫자 하나다). 정수 규칙에 소수를 넣으면
    /// 반올림으로 조용히 삼키지 않고 던진다 — A/B 가 의도한 값과 다른 값으로 돌면 보고서가 거짓이 된다.
    /// </summary>
    private static object ConvertValue(double value, Type t, string path)
    {
        if (t == typeof(double)) return value;
        if (t == typeof(int))
        {
            if (Math.Abs(value - Math.Truncate(value)) > double.Epsilon)
            {
                throw new ArgumentException(
                    $"정수 규칙에 소수를 넣을 수 없다: {path}={value.ToString(CultureInfo.InvariantCulture)}", nameof(path));
            }
            return (int)value;
        }
        if (t == typeof(bool)) return value != 0;
        throw new ArgumentException($"지원하지 않는 rules 값 타입: {path} ({t.Name})", nameof(path));
    }

    private static PropertyInfo? FindProp(Type t, string name) =>
        t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

    private static string SectionNames() => MemberNames(typeof(RulesConfig));

    private static string MemberNames(Type t) =>
        string.Join(", ", t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..]));
}
