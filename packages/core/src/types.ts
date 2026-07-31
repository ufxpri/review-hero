// 이세계 리뷰용사 — core 데이터 타입 (cards-v1.0.yaml / enemies-v1.0.yaml 스키마 대응)
// core는 fs 접근 금지 — 모든 데이터는 이 타입으로 변환되어 인자로 주입된다 (GDD §1.1).

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

// ── 카드 ──────────────────────────────────────────────

export interface ModifierDef {
  type: string;
  value?: number;
  condition?: string;
}

export interface EffectDef {
  type: string;
  value?: number;
  hits?: number;
  duration?: number | 'combat';
  per_submission?: boolean;
  uses_attach_slot?: boolean;
  gauge?: number;
  target_scope?: string;
  pool?: string;
  multiplier?: number;
  removed_for_run?: boolean;
  weaken_pct?: number;
  reflect_pct?: number;
  condition?: string;
  per_point?: number;
  cap_points?: number;
}

export interface PrefixDef {
  kind: 'prefix';
  id: string;
  name: string;
  suit: Suit;
  tags: string[]; // yaml tag: string|[string] → 배열 정규화
  cost: number;
  modifier?: ModifierDef;
}

export interface SuffixDef {
  kind: 'suffix';
  id: string;
  name: string;
  cost: number;
  sType: 'damage' | 'equipment' | 'control' | 'buff';
  target: 'enemy' | 'enemy_equipment' | 'my_equipment';
  effect: EffectDef;
  unique?: boolean;
}

export interface SpecialDef {
  kind: 'special';
  id: string;
  name: string;
  cost: number;
  layer: number; // 생략 시 1
  unique?: boolean;
  oncePerCombat?: boolean;
  effect: EffectDef;
}

export type CardDef = PrefixDef | SuffixDef | SpecialDef;

export interface CardIndex {
  byId: Map<string, CardDef>;
  /** X03 create_card용 접미 풀 */
  suffixIds: string[];
}

export function buildCardIndex(cards: CardDef[]): CardIndex {
  const byId = new Map<string, CardDef>();
  for (const c of cards) byId.set(c.id, c);
  return { byId, suffixIds: cards.filter((c) => c.kind === 'suffix').map((c) => c.id) };
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
  cancelOn: string[]; // '둔화' | '지연'
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
  /** 보스 페이즈2 (B01 리뷰 조작) */
  phase2?: { triggerWill: number; effects: EnemyEffectDef[] };
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
