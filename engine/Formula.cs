// 전투 수치 연산 — 순수 함수만 (ADR-025).
//
// **this 없음 · 상태 접근 없음 · 부수효과 없음.** 값을 받아 숫자를 돌려준다.
// Battle 은 상태 머신 역할만 하고 수치는 전부 여기에 위임한다. 그래서
//   ① 환산식 검증에 전투 상태를 만들 필요가 없고
//   ② 화면·시뮬·엔진이 같은 함수를 쓰므로 표시값이 실제와 어긋날 자리가 없다.
//
// ── TS→C# 이관 메모 (ADR-029) ────────────────────────────────
// · TS 의 「인자 객체 + 기본값」(`computeLikes({ base, mult, fixedAdd })`)은 C# 의 선택적 인자로
//   옮겼다. 필수 인자를 앞에 두고 나머지는 이름 붙여 호출한다 —
//   `ComputeLikes(7, 1.5, fixedAdd: 1)`. 인자 이름 `base` 는 C# 예약어라 `baseValue` 로 바꿨다.
// · TS 는 전부 `number`(배정밀도)라 나눗셈이 자동으로 실수였다. C# 에서 `pct / 100` 을 정수로
//   쓰면 0 이 되어 조용히 규칙이 바뀐다 — **백분율 나눗셈은 전부 `100.0`** 으로 못박았다.
// · 여러 값을 돌려주던 객체 반환은 `readonly record struct` 로 옮겼다(할당 없음, 값 의미 유지).

namespace ReviewHero.Engine;

/// <summary>ComputeAbsorb 결과 — 장비별 소모량과 의지에 실제로 들어가는 피해</summary>
public readonly record struct AbsorbResult(IReadOnlyList<int> Spent, int Absorbed, int ToWill);

/// <summary>방어 소모 순서 (ComputeAbsorb 주석의 결정 근거 참조)</summary>
public enum AbsorbOrder
{
    /// <summary>장비 슬롯 선언 순 — 무기 → 방어구 → 장신구 (GDD §3.9)</summary>
    Slot,

    /// <summary>방어량 큰 것부터. 동률이면 슬롯 순</summary>
    Largest,
}

public static class Formula
{
    /// <summary>배율 적용 — 내림 1회, 최소 1 (GDD §2-1)</summary>
    public static int ApplyMult(double value, double mult) => Math.Max(1, (int)Math.Floor(value * mult));

    /// <summary>
    /// 태그 판정 4단계 (card-system-v2 §2).
    /// 검사 순서가 규칙이다 — 원산지가 최우선이며 <b>무효 태그를 무시한다.</b>
    /// </summary>
    /// <remarks>
    /// TS 는 <c>Pick&lt;ReviewCardDef, 'tag'&gt;</c> 구조적 타입을 받았다. C# 에는 그 표현이 없어
    /// 태그 문자열을 직접 받는다 — 검사에 쓰이는 것이 태그 하나뿐이라는 사실은 그대로다.
    /// </remarks>
    public static Judgement ComputeJudgement(
        string cardTag,
        IReadOnlyList<string> targetTags,
        IReadOnlyList<string> targetNullTags,
        bool isOrigin)
    {
        if (isOrigin) return Judgement.Origin;
        if (targetNullTags.Contains(cardTag)) return Judgement.Fumble;
        if (targetTags.Contains(cardTag)) return Judgement.Fact;
        return Judgement.Normal;
    }

    /// <inheritdoc cref="ComputeJudgement(string, IReadOnlyList{string}, IReadOnlyList{string}, bool)"/>
    public static Judgement ComputeJudgement(
        ReviewCardDef card,
        IReadOnlyList<string> targetTags,
        IReadOnlyList<string> targetNullTags,
        bool isOrigin) => ComputeJudgement(card.Tag, targetTags, targetNullTags, isOrigin);

    /// <summary>
    /// 좋아요 환산식에 들어가는 배율·가산 3종을 한 번에 산출한다 (GDD §2).
    /// PreviewSubmit 과 SubmitReview 가 <b>같은 함수</b>를 부르므로 미리보기·실제 드리프트가 성립하지 않는다.
    ///   Mult       판정 배율 × 조건부(E03 영창 약점) — 절대 수치 전반에 걸린다
    ///   VanityMult 계열별 의지 피해 배수(E05) — 의지 피해에만
    ///   FixedAdd   원산지 고정 가산 — 내림 <b>뒤에</b> 더한다
    /// </summary>
    public static (double Mult, double VanityMult, int FixedAdd) ComputeMultipliers(
        Judgement judgement,
        string cardTag,
        Suit cardSuit,
        bool charging,
        RulesConfig rules,
        CastingWeaknessDef? castingWeakness = null,
        IReadOnlyDictionary<Suit, double>? suitDamageMult = null)
    {
        double mult = rules.Judge.Mult[judgement];
        if (castingWeakness is not null && charging && cardTag == castingWeakness.Tag)
        {
            mult *= castingWeakness.Multiplier;
        }
        double vanity = suitDamageMult is not null && suitDamageMult.TryGetValue(cardSuit, out double v) ? v : 1;
        return (mult, vanity, judgement == Judgement.Origin ? rules.Judge.OriginFixedAdd : 0);
    }

    /// <summary>
    /// 좋아요 환산식 (GDD §2) — 모든 피해의 단일 경로.
    ///   최종 좋아요 = ⌊ 기본 × 판정 배율 × 기타 배율 ⌋ + 고정 가산
    /// 배율은 전부 곱한 뒤 <b>한 번만</b> 내리고, 고정 가산은 내림 <b>뒤에</b> 더한다.
    /// </summary>
    /// <param name="baseValue">카드 인쇄 수치 (TS 인자명 <c>base</c> — C# 예약어라 개명)</param>
    /// <param name="mult">판정 × 조건부(영창 약점 등)</param>
    /// <param name="attachBonus">부착 버프 가산 (제출당 1회)</param>
    /// <param name="vanityMult">의지 피해 전용 추가 배율</param>
    /// <param name="fixedAdd">원산지 +1 등 — 배율 비대상</param>
    /// <param name="storedBonus">X05 예약분 — 내림 뒤 가산</param>
    public static int ComputeLikes(
        double baseValue,
        double mult,
        int attachBonus = 0,
        double vanityMult = 1,
        int fixedAdd = 0,
        int storedBonus = 0)
        => ApplyMult(baseValue + attachBonus, mult * vanityMult) + fixedAdd + storedBonus;

    /// <summary>
    /// 신뢰도 게이지 증감 — <b>클램프 반영 실증감</b>을 돌려준다 (GDD §2-2 초과 소실).
    /// 판정분과 카드 인라인분을 순서대로 각각 클램프해야 값이 맞는다
    /// (게이지 0에서 헛소리 −2 → 0, 이어서 인라인 +2 → 2. 합산 후 클램프와 결과가 다르다).
    /// </summary>
    public static int ComputeGaugeDelta(
        int current,
        Judgement judgement,
        RulesConfig rules,
        int inlineGauge = 0,
        int? fumbleOverride = null)
    {
        int min = rules.Gauge.Min;
        int max = rules.Gauge.Max;
        int g = current;

        void Step(int d) => g = Math.Max(min, Math.Min(max, g + d));

        Step(judgement == Judgement.Fumble && fumbleOverride is int f ? f : rules.Judge.Gauge[judgement]);
        Step(inlineGauge);
        return g - current;
    }

    /// <summary>
    /// 회복 상한 적용 — 요청량 중 <b>실제로 들어가는 증가분</b>만 돌려준다 (maxWill 초과분은 버려진다).
    /// 판정 회복·카드 heal 동반이 같은 클램프를 거치도록 하는 단일 경로.
    /// </summary>
    public static int ComputeHealApplied(int will, int maxWill, int amount)
    {
        if (amount <= 0) return 0;
        return Math.Min(maxWill, will + amount) - will;
    }

    /// <summary>
    /// 호응 회복 (ADR-023 ②) — <b>상한 반영 실증가분</b>을 돌려준다.
    /// 잘 쓴 글에 좋아요가 눌리고 그 호응이 의지를 채운다. 헛소리·일반은 0.
    /// </summary>
    public static int ComputeHeal(Judgement judgement, int will, int maxWill, RulesConfig rules)
        => ComputeHealApplied(will, maxWill, rules.Judge.Heal[judgement]);

    /// <summary>
    /// 방어 흡수 (ADR-023 ①) — 피해를 장비 방어로 먼저 상쇄한다.
    /// 흡수한 만큼 방어가 소모되고 남은 방어는 전투 내내 유지된다(턴 리셋 없음).
    ///
    /// 소모 순서(기본 <see cref="AbsorbOrder.Slot"/> = <b>장비 슬롯 선언 순</b>, 무기 → 방어구 → 장신구, GDD §3.9):
    ///   ① 배열 순서는 전투 내내 불변이라 리플레이 검증이 수치에 의존하지 않는다.
    ///   ② UI 가 장비를 슬롯 순으로 보여주므로 "왼쪽부터 닳는다"가 화면과 일치한다.
    /// <see cref="AbsorbOrder.Largest"/>(방어량 큰 것부터)도 결정적이며 작은 방어가 잘게 남는 것을 막지만,
    /// 어느 장비가 닳는지가 수치에 따라 바뀌어 화면에서 읽기 어렵다. 방어는 현재 총량으로만 작동해
    /// <b>어느 쪽이든 의지에 들어가는 피해는 같다</b> — 달라지는 것은 장비별 잔량 분포뿐이다.
    /// </summary>
    /// <returns>각 장비의 소모량과 의지에 실제로 들어가는 피해</returns>
    public static AbsorbResult ComputeAbsorb(
        int damage,
        IReadOnlyList<int> defenses,
        AbsorbOrder order = AbsorbOrder.Slot)
    {
        var spent = new int[defenses.Count];
        int remain = damage;

        var seq = defenses
            .Select((d, i) => (D: d, I: i))
            .Where(x => x.D > 0);
        // 결정성 — 동률이면 슬롯 순 (OrderByDescending/ThenBy 는 안정 정렬)
        if (order == AbsorbOrder.Largest) seq = seq.OrderByDescending(x => x.D).ThenBy(x => x.I);

        foreach (var (d, i) in seq)
        {
            if (remain <= 0) break;
            int use = Math.Min(d, remain);
            spent[i] = use;
            remain -= use;
        }
        return new AbsorbResult(spent, damage - remain, remain);
    }

    /// <summary>게이지 클램프 (GDD §2-2) — 게이지를 직접 세팅하는 경로용</summary>
    public static int ClampGauge(int value, RulesConfig rules)
        => Math.Max(rules.Gauge.Min, Math.Min(rules.Gauge.Max, value));

    /// <summary>
    /// 적 공격 1발의 최종 위력.
    ///   ① 가산·감산 먼저 — 감산(attack_down)은 배율이 아니므로 §2-1 미적용, 하한 0(피해 0 가능)
    ///   ② 0이 아니면 배율들을 순서대로 — 「힙스터 인증」 크리(−%) → 행동 위력 보정(S08·X06) → 온보딩(§4.4).
    ///      배율 경로는 §2-1 "내림·최소 1"이라 배율만으로는 0이 되지 않는다.
    /// </summary>
    /// <param name="weaken">1 = 무보정</param>
    /// <param name="onboardingMult">1 = 정상 난이도</param>
    public static int ComputeEnemyDamage(
        int baseValue,
        int attackUp,
        int attackDown,
        bool hipsterActive,
        double weaken,
        double onboardingMult,
        RulesConfig rules)
    {
        int v = Math.Max(0, baseValue + attackUp - attackDown);
        if (v <= 0) return 0;
        if (hipsterActive) v = ApplyMult(v, 1 - rules.Critical.HipsterAttackDownPct / 100.0);
        if (weaken != 1) v = ApplyMult(v, weaken);
        if (onboardingMult != 1) v = ApplyMult(v, onboardingMult);
        return v;
    }

    /// <summary>반사 피해 (X06) — 받은 피해의 N% (내림). 배율이 아니라 비율 산출이라 §2-1 "최소 1" 비대상</summary>
    public static int ComputeReflect(int damage, double reflectPct)
        => (int)Math.Floor(damage * reflectPct / 100.0);

    /// <summary>행동 위력 보정 배율 — 퍼센트 보정(S08 −50, X06 −50)을 곱셈 배율로 (v1.1)</summary>
    public static double WeakenMult(double pct) => 1 + pct / 100.0;

    /// <summary>보스 페이즈2 발동 문턱 (v1.1) — 비례 트리거 우선, 없으면 절대값</summary>
    public static int ComputePhase2Threshold(int maxWill, Phase2Def trigger)
    {
        if (trigger.TriggerPct is int pct) return (int)Math.Floor(maxWill * pct / 100.0);
        return trigger.TriggerWill ?? 0;
    }
}
