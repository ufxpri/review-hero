// 전투 수치 연산 — 순수 함수만 (ADR-025).
//
// **this 없음 · 상태 접근 없음 · 부수효과 없음.** 값을 받아 숫자를 돌려준다.
// Battle 은 상태 머신 역할만 하고 수치는 전부 여기에 위임한다. 그래서
//   ① 환산식 검증에 전투 상태를 만들 필요가 없고
//   ② 화면·시뮬·엔진이 같은 함수를 쓰므로 표시값이 실제와 어긋날 자리가 없다.
import type { Judgement, ReviewCardDef, Suit } from './types.ts';
import type { RulesConfig } from './rules.ts';

/** 배율 적용 — 내림 1회, 최소 1 (GDD §2-1) */
export function applyMult(value: number, mult: number): number {
  return Math.max(1, Math.floor(value * mult));
}

/**
 * 태그 판정 4단계 (card-system-v2 §2).
 * 검사 순서가 규칙이다 — 원산지가 최우선이며 **무효 태그를 무시한다.**
 */
export function computeJudgement(
  card: Pick<ReviewCardDef, 'tag'>,
  targetTags: readonly string[],
  targetNullTags: readonly string[],
  isOrigin: boolean,
): Judgement {
  if (isOrigin) return 'origin';
  if (targetNullTags.includes(card.tag)) return 'fumble';
  if (targetTags.includes(card.tag)) return 'fact';
  return 'normal';
}

/**
 * 좋아요 환산식에 들어가는 배율·가산 3종을 한 번에 산출한다 (GDD §2).
 * previewSubmit 과 submitReview 가 **같은 함수**를 부르므로 미리보기·실제 드리프트가 성립하지 않는다.
 *   mult       판정 배율 × 조건부(E03 영창 약점) — 절대 수치 전반에 걸린다
 *   vanityMult 계열별 의지 피해 배수(E05) — 의지 피해에만
 *   fixedAdd   원산지 고정 가산 — 내림 **뒤에** 더한다
 */
export function computeMultipliers(params: {
  judgement: Judgement;
  cardTag: string;
  cardSuit: Suit;
  charging: boolean; // 적이 준비(영창) 중인가
  castingWeakness?: { tag: string; multiplier: number };
  suitDamageMult?: Partial<Record<Suit, number>>;
  rules: RulesConfig;
}): { mult: number; vanityMult: number; fixedAdd: number } {
  const { judgement, cardTag, cardSuit, charging, castingWeakness, suitDamageMult, rules } = params;
  let mult = rules.judge.mult[judgement];
  if (castingWeakness && charging && cardTag === castingWeakness.tag) mult *= castingWeakness.multiplier;
  return {
    mult,
    vanityMult: suitDamageMult?.[cardSuit] ?? 1,
    fixedAdd: judgement === 'origin' ? rules.judge.originFixedAdd : 0,
  };
}

/**
 * 좋아요 환산식 (GDD §2) — 모든 피해의 단일 경로.
 *   최종 좋아요 = ⌊ 기본 × 판정 배율 × 기타 배율 ⌋ + 고정 가산
 * 배율은 전부 곱한 뒤 **한 번만** 내리고, 고정 가산은 내림 **뒤에** 더한다.
 */
export function computeLikes(params: {
  base: number; // 카드 인쇄 수치
  attachBonus?: number; // 부착 버프 가산 (제출당 1회)
  mult: number; // 판정 × 조건부(영창 약점 등)
  vanityMult?: number; // 의지 피해 전용 추가 배율
  fixedAdd?: number; // 원산지 +1 등 — 배율 비대상
  storedBonus?: number; // X05 예약분 — 내림 뒤 가산
}): number {
  const { base, attachBonus = 0, mult, vanityMult = 1, fixedAdd = 0, storedBonus = 0 } = params;
  return applyMult(base + attachBonus, mult * vanityMult) + fixedAdd + storedBonus;
}

/**
 * 신뢰도 게이지 증감 — **클램프 반영 실증감**을 돌려준다 (GDD §2-2 초과 소실).
 * 판정분과 카드 인라인분을 순서대로 각각 클램프해야 값이 맞는다
 * (게이지 0에서 헛소리 −2 → 0, 이어서 인라인 +2 → 2. 합산 후 클램프와 결과가 다르다).
 */
export function computeGaugeDelta(params: {
  current: number;
  judgement: Judgement;
  inlineGauge?: number;
  fumbleOverride?: number; // 온보딩 1판 완화값
  rules: RulesConfig;
}): number {
  const { current, judgement, inlineGauge = 0, fumbleOverride, rules } = params;
  const { min, max } = rules.gauge;
  let g = current;
  const step = (d: number): void => {
    g = Math.max(min, Math.min(max, g + d));
  };
  step(judgement === 'fumble' && fumbleOverride !== undefined ? fumbleOverride : rules.judge.gauge[judgement]);
  step(inlineGauge);
  return g - current;
}

/**
 * 회복 상한 적용 — 요청량 중 **실제로 들어가는 증가분**만 돌려준다 (maxWill 초과분은 버려진다).
 * 판정 회복·카드 heal 동반이 같은 클램프를 거치도록 하는 단일 경로.
 */
export function computeHealApplied(will: number, maxWill: number, amount: number): number {
  if (amount <= 0) return 0;
  return Math.min(maxWill, will + amount) - will;
}

/**
 * 호응 회복 (ADR-023 ②) — **상한 반영 실증가분**을 돌려준다.
 * 잘 쓴 글에 좋아요가 눌리고 그 호응이 의지를 채운다. 헛소리·일반은 0.
 */
export function computeHeal(params: {
  judgement: Judgement;
  will: number;
  maxWill: number;
  rules: RulesConfig;
}): number {
  const { judgement, will, maxWill, rules } = params;
  return computeHealApplied(will, maxWill, rules.judge.heal[judgement]);
}

/**
 * 방어 흡수 (ADR-023 ①) — 피해를 장비 방어로 먼저 상쇄한다.
 * 흡수한 만큼 방어가 소모되고 남은 방어는 전투 내내 유지된다(턴 리셋 없음).
 *
 * 소모 순서(기본 `'slot'` = **장비 슬롯 선언 순**, 무기 → 방어구 → 장신구, GDD §3.9):
 *   ① 배열 순서는 전투 내내 불변이라 리플레이 검증이 수치에 의존하지 않는다.
 *   ② UI 가 장비를 슬롯 순으로 보여주므로 "왼쪽부터 닳는다"가 화면과 일치한다.
 * `'largest'`(방어량 큰 것부터)도 결정적이며 작은 방어가 잘게 남는 것을 막지만, 어느 장비가
 * 닳는지가 수치에 따라 바뀌어 화면에서 읽기 어렵다. 방어는 현재 총량으로만 작동해 **어느 쪽이든
 * 의지에 들어가는 피해는 같다** — 달라지는 것은 장비별 잔량 분포뿐이다.
 * @returns 각 장비의 소모량과 의지에 실제로 들어가는 피해
 */
export function computeAbsorb(
  damage: number,
  defenses: readonly number[],
  order: 'slot' | 'largest' = 'slot',
): { spent: number[]; absorbed: number; toWill: number } {
  const spent = defenses.map(() => 0);
  let remain = damage;
  const seq = defenses.map((d, i) => ({ d, i })).filter((x) => x.d > 0);
  if (order === 'largest') seq.sort((a, b) => b.d - a.d || a.i - b.i); // 결정성 — 동률이면 슬롯 순
  for (const { d, i } of seq) {
    if (remain <= 0) break;
    const use = Math.min(d, remain);
    spent[i] = use;
    remain -= use;
  }
  return { spent, absorbed: damage - remain, toWill: remain };
}

/** 게이지 클램프 (GDD §2-2) — 게이지를 직접 세팅하는 경로용 */
export function clampGauge(value: number, rules: RulesConfig): number {
  return Math.max(rules.gauge.min, Math.min(rules.gauge.max, value));
}

/**
 * 적 공격 1발의 최종 위력.
 *   ① 가산·감산 먼저 — 감산(attack_down)은 배율이 아니므로 §2-1 미적용, 하한 0(피해 0 가능)
 *   ② 0이 아니면 배율들을 순서대로 — 힙스터 크리(−%) → 행동 위력 보정(S08·X06) → 온보딩(§4.4).
 *      배율 경로는 §2-1 "내림·최소 1"이라 배율만으로는 0이 되지 않는다.
 */
export function computeEnemyDamage(params: {
  base: number;
  attackUp: number;
  attackDown: number;
  hipsterActive: boolean;
  weaken: number; // 1 = 무보정
  onboardingMult: number; // 1 = 정상 난이도
  rules: RulesConfig;
}): number {
  const { base, attackUp, attackDown, hipsterActive, weaken, onboardingMult, rules } = params;
  let v = Math.max(0, base + attackUp - attackDown);
  if (v <= 0) return 0;
  if (hipsterActive) v = applyMult(v, 1 - rules.critical.hipsterAttackDownPct / 100);
  if (weaken !== 1) v = applyMult(v, weaken);
  if (onboardingMult !== 1) v = applyMult(v, onboardingMult);
  return v;
}

/** 반사 피해 (X06) — 받은 피해의 N% (내림). 배율이 아니라 비율 산출이라 §2-1 "최소 1" 비대상 */
export function computeReflect(damage: number, reflectPct: number): number {
  return Math.floor((damage * reflectPct) / 100);
}

/** 행동 위력 보정 배율 — 퍼센트 보정(S08 −50, X06 −50)을 곱셈 배율로 (v1.1) */
export function weakenMult(pct: number): number {
  return 1 + pct / 100;
}

/** 보스 페이즈2 발동 문턱 (v1.1) — 비례 트리거 우선, 없으면 절대값 */
export function computePhase2Threshold(maxWill: number, trigger: { triggerPct?: number; triggerWill?: number }): number {
  if (trigger.triggerPct !== undefined) return Math.floor((maxWill * trigger.triggerPct) / 100);
  return trigger.triggerWill ?? 0;
}
