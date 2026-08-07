// 이세계 리뷰용사 — core 데이터 타입 (cards-v2.0.yaml / enemies-v1.0.yaml 스키마 대응)
// core는 fs 접근 금지 — 모든 데이터는 이 타입으로 변환되어 인자로 주입된다 (GDD §1.1).
//
// v2 (card-system-v2.md, ADR-011): 접두+접미 조합형 폐지 → 카드 1장 = 완성 리뷰.
// PrefixDef/SuffixDef/ModifierDef 삭제, ReviewCardDef 단일화 + origin(원산지) 필드 추가.

export type Suit = '품질' | '성능' | '배송' | '감성';

/** 계열 ↔ 태그 매핑 (combat-model-v0.1 §계열↔태그, enemies-v1.0 태그 12종) */
export const SUIT_TAGS: Record<Suit, readonly string[]> = {
  품질: ['마감', '내구도', '무게'],
  성능: ['출력', '연비', '이펙트'],
  배송: ['속도', '구성품', '응대'],
  감성: ['디자인', '감성', '개연성'],
};

export function tagToSuit(tag: string): Suit | undefined {
  for (const suit of Object.keys(SUIT_TAGS) as Suit[]) {
    if (SUIT_TAGS[suit].includes(tag)) return suit;
  }
  return undefined;
}

/** 크리티컬 성향 (GDD §3.5) */
export type Disposition = '팩트 폭격기' | '힙스터 평론가' | '프로 불편러' | '바이럴 앞잡이';

export const SUIT_DISPOSITION: Record<Suit, Disposition> = {
  품질: '팩트 폭격기',
  성능: '힙스터 평론가',
  배송: '프로 불편러',
  감성: '바이럴 앞잡이',
};

/** 성향 → 계열 역매핑 (E04 은신 게이트의 크리티컬 리뷰 계열 판정용 — §3.8) */
export const DISPOSITION_SUIT: Record<Disposition, Suit> = {
  '팩트 폭격기': '품질',
  '힙스터 평론가': '성능',
  '프로 불편러': '배송',
  '바이럴 앞잡이': '감성',
};

// ── 카드 (v2 — cards-v2.0.yaml) ──────────────────────

export type Rarity = 'basic' | 'common' | 'rare' | 'legendary';

export type TargetKind = 'enemy' | 'enemy_equipment' | 'my_equipment';

/** 원산지 (card-system-v2 §2). 없으면 원산지 판정 영구 미발동 (Z##·X##·P해금 카드) */
export interface OriginDef {
  /** 적 id — 적 본체 대상 제출 시 일치 판정 */
  enemy?: string;
  /** 구성품명 — 구성품 대상 제출 시 일치 판정 (이름 완전 일치) */
  equipment?: string;
}

export interface EffectDef {
  type: string;
  value?: number;
  duration?: number | 'combat';
  // ── 동반 효과 (v2 복합 효과 — 판정 배율 적용 범위는 battle.ts 주석 참조) ──
  /** 의지 피해 동반 (delay_enemy_action·stun·weaken_next_action·remove_enemy_buff) */
  damage?: number;
  /** 드로우 동반 (Z03·A01) — 판정 배율 미적용 (장수는 절대 수치 아님) */
  draw?: number;
  /** 내 의지 회복 동반 (G03) — 판정 배율 미적용 */
  heal?: number;
  /** 신뢰도 게이지 동반 (B02c·A04) / X08은 주효과 type: gauge */
  gauge?: number;
  /** 다음 행동 위력 % 동반 (C02c) */
  weaken_next_action?: number;
  // ── 특수 카드(X##) 전용 ──
  pool?: string; // X03 create_card
  multiplier?: number; // X04 gift_card
  weaken_pct?: number; // X06 (생략 시 −50)
  reflect_pct?: number; // X06 (생략 시 50)
  condition?: string; // 예비 (v1 X07 normal_battle_only 등)
  per_point?: number; // X09
  cap_points?: number; // X09
  // ── 예비 (v2 데이터 미사용 — 하위 호환·향후 카드용) ──
  hits?: number;
  target_scope?: string;
  uses_attach_slot?: boolean;
}

/** 리뷰 카드 1장 = 완성 리뷰 (v2 단일 초점 원칙 — 태그 정확히 1개) */
export interface ReviewCardDef {
  kind: 'review';
  id: string;
  name: string;
  origin?: OriginDef;
  suit: Suit; // 성향 산정 기준 (GDD §3.5)
  tag: string; // 판정 태그 정확히 1개 (배열 금지 — 로드 시 검증)
  cost: number; // 필력 (최소 1 — card-system-v2 §5)
  stars: number; // ★ 1~5. 4 이상 = 찬양 = 버프 계열 (§6)
  rarity: Rarity;
  target: TargetKind;
  text?: string; // 리뷰 본문 (UI 정본)
  effect: EffectDef;
  ui?: string; // 효과 요약 1줄
  unique?: boolean; // 덱 1장 제한 (런 레벨 검증)
  layer: number; // 생략 시 1 (MVP)
}

/** 진상 화법 (X01~X09) — 무원산지·무판정. 배율(×1.5/×1.0/×0.5) 비대상 */
export interface SpecialDef {
  kind: 'special';
  id: string;
  name: string;
  cost: number;
  stars?: number;
  rarity?: Rarity;
  target: TargetKind; // v2 실데이터는 전부 enemy
  text?: string;
  effect: EffectDef;
  ui?: string;
  unique?: boolean;
  layer: number; // 생략 시 1. X09는 2
  oncePerCombat?: boolean; // 예비 (v2 데이터 미사용)
}

export type CardDef = ReviewCardDef | SpecialDef;

export interface CardIndex {
  byId: Map<string, CardDef>;
  /** X03 create_card(pool: any)용 전체 카드 id — 레이어 필터는 Battle이 수행 */
  allIds: string[];
}

export function buildCardIndex(cards: CardDef[]): CardIndex {
  const byId = new Map<string, CardDef>();
  for (const c of cards) byId.set(c.id, c);
  return { byId, allIds: cards.map((c) => c.id) };
}

// ── 적 ────────────────────────────────────────────────

export interface EnemyEffectDef {
  op: string;
  value?: number;
  floor?: number;
  duration?: 'battle' | 'next_hit' | number;
  when?: string;
  condition?: string;
  if_stealth_broken?: number;
  attachment?: string;
  counter_card?: string;
  [k: string]: unknown;
}

export interface EnemyActionDef {
  id: string;
  name: string;
  aType: 'attack' | 'buff' | 'steal' | 'stealth' | 'gimmick';
  effects: EnemyEffectDef[];
  chargeTurns: number; // 생략 시 0
  cancelOn: string[]; // 'delay_enemy_action' (구 표기 '지연' 하위 호환)
  cooldown?: number;
}

export interface EnemyEquipmentDef {
  name: string;
  durability: number;
  tags: string[];
}

export interface EnemyDef {
  id: string;
  name: string;
  tier: 'normal' | 'elite' | 'boss';
  will: number;
  weaknessTags: string[];
  nullTags: string[];
  equipment: EnemyEquipmentDef[];
  actions: EnemyActionDef[];
  pattern: string[];
  /** E05 vanity: 계열별 의지 데미지 배수 */
  suitDamageMult?: Partial<Record<Suit, number>>;
  /** E04 stealth_gate: 은신 중 명중 가능 계열 + 명중 시 은신 해제 */
  stealthGate?: { hittableSuits: Suit[]; breakOnHit: boolean };
  /** E03 casting_weakness: 영창(준비) 중 해당 태그 리뷰 효과 ×N (v1은 P06 modifier로 구현 — v2에서 적 특성으로 이관) */
  castingWeakness?: { tag: string; multiplier: number };
  /** 보스 페이즈2 (B01 리뷰 조작). v1.1: 비례 트리거(triggerPct, "의지 N% 이하") 우선, 절대값(triggerWill)은 하위 호환 */
  phase2?: { triggerWill?: number; triggerPct?: number; effects: EnemyEffectDef[] };
}

// ── 플레이어 장비 ─────────────────────────────────────

export interface PlayerEquipmentDef {
  name: string;
  tags: string[];
  nullTags: string[];
}

/** 시작 장비 3종 (GDD §3.9 — 01 §3 표 그대로, 패시브 없음) */
export const STARTING_EQUIPMENT: PlayerEquipmentDef[] = [
  { name: '이세계 보급형 롱소드', tags: ['마감'], nullTags: ['연비'] },
  { name: '물려받은 가죽 갑옷', tags: ['내구도', '무게'], nullTags: ['이펙트'] },
  { name: '위조 인증 목걸이', tags: ['감성'], nullTags: ['출력'] },
];
