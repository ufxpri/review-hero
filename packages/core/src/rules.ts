// 밸런스 상수 단일 정본 (ADR-025).
//
// **코드 어디에도 밸런스 매직 넘버를 두지 않는다.** 규칙 수치는 전부 여기를 거치고,
// 밸런스 라운드는 코드가 아니라 이 표를 고친다. Battle 은 cfg.rules 로 부분 오버라이드를
// 받으므로 시뮬레이터가 같은 시드에서 A/B 를 돌릴 수 있다.
//
//   new Battle({ ...cfg, rules: { judge: { mult: { normal: 0.75 } } } })
//
// 적 의지·공격력, 카드 비용·수치는 여기가 아니라 YAML(enemies/cards)이 정본이다 —
// 그쪽은 콘텐츠이고 여기는 콘텐츠에 걸리는 규칙이다.
import type { Judgement } from './types.ts';

export interface RulesConfig {
  /** 플레이어 기본치 (GDD §3.1) */
  player: {
    will: number;
    energyPerTurn: number;
    handSize: number; // 턴 시작 시 채우는 손패 수
    handMax: number; // 손패 상한 — 초과분은 드로우 중단(소멸 없음)
    reviseCost: number; // 퇴고 비용 (필력)
    parcelCost: number; // 택배 개봉 비용 (필력) — ADR-024 ③
    reviseDraw: number; // 퇴고 1회로 뽑는 장수 (버리는 장수는 1로 고정 — uid 지정 교체)
    attachSlots: number; // 장비당 부착 슬롯 (GDD §3.9)
  };
  /** 태그 판정 4단계 (card-system-v2 §2 · GDD §3.3) */
  judge: {
    mult: Record<Judgement, number>; // 좋아요 배율
    gauge: Record<Judgement, number>; // 신뢰도 게이지 증감
    heal: Record<Judgement, number>; // 호응 회복 — 의지 (ADR-023 ②)
    originFixedAdd: number; // 원산지 고정 좋아요 — 내림 뒤 가산 (GDD §2)
  };
  /** 신뢰도 게이지 (GDD §2-2 · §3.4) */
  gauge: {
    min: number;
    max: number; // 이 값에 도달하면 크리티컬 발동 가능
    counterRebutGain: number; // 재반박 성공 시 게이지 (GDD §3.4/§3.8)
  };
  /** 크리티컬 리뷰 (GDD §3.5) */
  critical: {
    factBomberDamage: number; // 팩트 폭격기 — 방어·저항 무시 고정 피해
    hipsterAttackDownPct: number; // 힙스터 — 적 공격력 감소 %
    hipsterTier: number; // 그 디버프의 반박 저항 등급 (R22)
    viralBonusCap: number; // 바이럴 가산 누적 상한 (크리 간 공유)
    viralFloorBonus: number; // 바이럴 — 버프 0개일 때 즉시 부착하는 바닥 보장 가산 (v1.1 제안 5)
    inconvenienceStunTurns: number; // 프로 불편러 — 기절 턴
    inconvenienceWeakenPct: number; // 프로 불편러 — 다음 행동 위력 % (음수. v1.1 제안 6)
    inconvenienceGold: Record<'normal' | 'elite' | 'boss', number>; // 프로 불편러 — 등급별 골드 갈취(전투당 1회)
  };
  /** 온보딩 보정 (GDD §4.4) — 판 번호로 고르는 것이 아니라 값을 주입한다 */
  onboarding: {
    enemyDamageMult1: number; // 1판 적 공격 배율
    enemyDamageMult2: number; // 2판 적 공격 배율
    fumbleGauge1: number; // 1판 헛소리 게이지 (완화값)
  };
  /** 전투 진행 */
  battle: {
    maxTurns: number; // 초과 시 timeout 패배
    staggerImmunityTurns: number; // 기절 해제 후 경직 내성 지속 턴 (GDD §3.2)
    /**
     * 장비 전 비활성(S07) 봉인 불발 시 부여하는 경직 내성 — 적 턴 정리에서 1 감소하므로
     * 「다음 플레이어 턴 1턴 유지」가 되려면 staggerImmunityTurns + 1 이어야 한다.
     */
    equipmentLockImmunityTurns: number;
    attachedDebuffTier: number; // 전투 중 부착한 일반 디버프의 반박 저항 등급 (힙스터 크리만 3)
    surrenderGold: number; // 전 장비 파괴 항복 승리 보상 (GDD §4.2)
  };
  /**
   * YAML 필드 누락 시의 스키마 기본값 — 콘텐츠 수치가 아니라 "값이 없을 때 이 규칙을 쓴다".
   * 카드·적 수치의 정본은 여전히 YAML 이다(ADR-025). 코드에 `?? 숫자`를 남기지 않기 위해 모은다.
   */
  effectDefaults: {
    stunTurns: number; // stun 카드의 value 생략 시
    weakenNextActionPct: number; // weaken_next_action 카드의 value 생략 시
    removeBuffCount: number; // remove_enemy_buff 개수 생략 시
    createCardCount: number; // X03 생성 장수 생략 시
    dotDuration: number; // equipment_dot duration 생략 시
    disableDuration: number; // disable_equipment duration 생략 시
    giftMultiplier: number; // X04 증정 배수 생략 시
    reactionWeakenPct: number; // X06 weaken_pct 생략 시
    reactionReflectPct: number; // X06 reflect_pct 생략 시
    penaltyCapPoints: number; // X09 cap_points 생략 시
    penaltyPerPoint: number; // X09 per_point 생략 시
  };
}

/**
 * 기본 수치 — GDD v1.2 기준.
 * ⚠ 방어·회복 축(ADR-023)은 신설이라 **잠정치**다. 밸런스 라운드 1(v2)에서 확정한다.
 */
export const DEFAULT_RULES: RulesConfig = {
  player: {
    will: 30,
    energyPerTurn: 3,
    handSize: 5,
    handMax: 8,
    reviseCost: 1,
    parcelCost: 1,
    reviseDraw: 1,
    attachSlots: 2,
  },
  judge: {
    mult: { origin: 1.5, fact: 1.5, normal: 1.0, fumble: 0.5 },
    gauge: { origin: 4, fact: 3, normal: 0, fumble: -2 },
    heal: { origin: 2, fact: 1, normal: 0, fumble: 0 },
    originFixedAdd: 1,
  },
  gauge: { min: 0, max: 10, counterRebutGain: 1 },
  critical: {
    factBomberDamage: 20,
    hipsterAttackDownPct: 50,
    hipsterTier: 3,
    viralBonusCap: 12,
    viralFloorBonus: 3,
    inconvenienceStunTurns: 1,
    inconvenienceWeakenPct: -50,
    inconvenienceGold: { normal: 8, elite: 15, boss: 25 },
  },
  onboarding: {
    enemyDamageMult1: 0.75,
    enemyDamageMult2: 0.9,
    fumbleGauge1: -1,
  },
  battle: {
    maxTurns: 30,
    staggerImmunityTurns: 1,
    equipmentLockImmunityTurns: 2,
    attachedDebuffTier: 1,
    surrenderGold: 6,
  },
  effectDefaults: {
    stunTurns: 1,
    weakenNextActionPct: -50,
    removeBuffCount: 1,
    createCardCount: 1,
    dotDuration: 2,
    disableDuration: 1,
    giftMultiplier: 4,
    reactionWeakenPct: -50,
    reactionReflectPct: 50,
    penaltyCapPoints: 5,
    penaltyPerPoint: 3,
  },
};

/** 부분 오버라이드를 재귀로 병합한다 — 시뮬 A/B 에서 레버 하나만 바꿔 넣기 위함 */
export type RulesOverride = {
  [K in keyof RulesConfig]?: Partial<RulesConfig[K]>;
};

export function mergeRules(base: RulesConfig, over?: RulesOverride): RulesConfig {
  if (!over) return base;
  const out = { ...base } as RulesConfig;
  for (const key of Object.keys(over) as (keyof RulesConfig)[]) {
    const section = over[key];
    if (section) {
      // 판정 표처럼 한 단계 더 들어가는 구획은 개별 키만 덮는다
      const merged: Record<string, unknown> = { ...(base[key] as Record<string, unknown>) };
      for (const [k, v] of Object.entries(section)) {
        const cur = merged[k];
        merged[k] =
          v !== null && typeof v === 'object' && !Array.isArray(v) && cur !== null && typeof cur === 'object'
            ? { ...(cur as Record<string, unknown>), ...(v as Record<string, unknown>) }
            : v;
      }
      (out[key] as unknown) = merged;
    }
  }
  return out;
}
