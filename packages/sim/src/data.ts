// YAML(design/cards-v2.0.yaml, enemies-v1.0.yaml) → core 타입 변환 (fs 접근은 sim만 담당)
//
// v2 (card-system-v2.md, ADR-011): 5개 섹션(starting_deck / past_life / enemy_reviews /
// equipment_reviews / specials) → 카드 인덱스 + 시작 덱 id 배열 + irremovable 집합.
// 로드 시 검증: tag는 정확히 1개(단일 초점 원칙 — 배열이면 에러).
//
// 미구현(런 레벨 — 감사 기록): GDD §3.6 덱 구축 규칙(유니크 1장 제한 X04/X08,
// 생계형 리뷰 표식 irremovable)은 카드 획득/제거가 존재하는 런 레이어의 검증 사항이다.
// 전투 엔진(Battle)은 주어진 덱을 그대로 사용하며 현 시뮬은 시작 덱(+ CLI 주입 덱)이 고정이라
// 실해 없음. irremovable 정보는 런 레이어 착수 대비로 여기서 이미 파싱해 노출한다.
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import yaml from 'js-yaml';
import {
  buildCardIndex,
  type CardDef,
  type CardIndex,
  type EnemyDef,
  type EnemyActionDef,
  type EnemyEffectDef,
  type ReviewCardDef,
  type SpecialDef,
  type Suit,
} from '../../core/src/index.ts';

const DESIGN_DIR = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'design');

/* eslint-disable @typescript-eslint/no-explicit-any */
type Raw = Record<string, any>;

export interface LoadedData {
  cards: CardIndex;
  startingDeck: string[];
  /** 생계형 리뷰(제거 불가) 표식 — 런 레이어 카드 제거 노드의 검증용 */
  irremovable: Set<string>;
  enemies: Map<string, EnemyDef>;
}

function convertReview(c: Raw, section: string): ReviewCardDef {
  // 단일 초점 원칙 (card-system-v2 §4): tag는 정확히 1개 — 배열이면 로드 실패
  if (Array.isArray(c.tag)) throw new Error(`카드 ${c.id}: tag 배열 금지 — 단일 초점 원칙 (card-system-v2 §4)`);
  if (typeof c.tag !== 'string' || c.tag.length === 0) throw new Error(`카드 ${c.id}: tag는 문자열 1개 필수 (${section})`);
  if (c.no_judgement) throw new Error(`카드 ${c.id}: 리뷰 섹션(${section})에 no_judgement 불가 — specials로 이동`);
  return {
    kind: 'review',
    id: c.id,
    name: c.name,
    origin: c.origin,
    suit: c.suit as Suit,
    tag: c.tag,
    cost: c.cost,
    stars: c.stars,
    rarity: c.rarity,
    target: c.target,
    text: c.text,
    effect: c.effect,
    ui: c.ui,
    unique: c.unique ?? false,
    layer: c.layer ?? 1,
  };
}

function convertSpecial(x: Raw): SpecialDef {
  if (x.no_judgement !== true) throw new Error(`특수 카드 ${x.id}: no_judgement: true 필수 (진상 화법 — 무판정)`);
  return {
    kind: 'special',
    id: x.id,
    name: x.name,
    cost: x.cost,
    stars: x.stars,
    rarity: x.rarity,
    target: x.target ?? 'enemy',
    text: x.text,
    effect: x.effect,
    ui: x.ui,
    unique: x.unique ?? false,
    layer: x.layer ?? 1,
  };
}

export function loadCards(
  path = join(DESIGN_DIR, 'cards-v2.0.yaml'),
): { index: CardIndex; startingDeck: string[]; irremovable: Set<string> } {
  const raw = yaml.load(readFileSync(path, 'utf8')) as Raw;
  const defs: CardDef[] = [];

  for (const section of ['past_life', 'enemy_reviews', 'equipment_reviews'] as const) {
    for (const c of (raw[section] ?? []) as Raw[]) defs.push(convertReview(c, section));
  }
  for (const x of (raw.specials ?? []) as Raw[]) defs.push(convertSpecial(x));

  const index = buildCardIndex(defs);

  const entries = (raw.starting_deck ?? []) as Raw[];
  const startingDeck = entries.map((e) => e.id as string);
  const irremovable = new Set<string>(entries.filter((e) => e.irremovable).map((e) => e.id as string));
  for (const id of startingDeck) {
    if (!index.byId.has(id)) throw new Error(`starting_deck의 미정의 카드: ${id}`);
  }
  return { index, startingDeck, irremovable };
}

function convertAction(a: Raw): EnemyActionDef {
  return {
    id: a.id,
    name: a.name,
    aType: a.type,
    effects: (a.effects ?? []) as EnemyEffectDef[],
    chargeTurns: a.charge_turns ?? 0,
    cancelOn: a.cancel_on ?? [],
    cooldown: a.cooldown,
  };
}

function convertEnemy(e: Raw): EnemyDef {
  const def: EnemyDef = {
    id: e.id,
    name: e.name,
    tier: e.tier,
    will: e.will,
    weaknessTags: e.weakness_tags ?? [],
    nullTags: e.null_tags ?? [],
    equipment: (e.equipment ?? []).map((q: Raw) => ({ name: q.name, durability: q.durability, tags: q.tags ?? [] })),
    actions: (e.actions ?? []).map(convertAction),
    pattern: e.pattern,
  };
  // 특성 파싱 (E05 vanity, E04 stealth_gate, E03 casting_weakness)
  for (const t of (e.traits ?? []) as Raw[]) {
    if (t.damage_multiplier_from_suit) def.suitDamageMult = t.damage_multiplier_from_suit;
    if (t.hittable_suits_while_stealth) {
      def.stealthGate = { hittableSuits: t.hittable_suits_while_stealth, breakOnHit: t.break_stealth_on_hit ?? true };
    }
    // v2: casting_weakness(E03)는 접두 modifier(P06) 폐지로 적 특성 판정으로 이관 — 엔진이 태그 대조
    if (t.applies_to_tag && t.multiplier) def.castingWeakness = { tag: t.applies_to_tag, multiplier: t.multiplier };
  }
  if (e.phase2) {
    // v1.1(제안 3): "의지 50% 이하" 비례 트리거 지원 — %가 있으면 triggerPct, 없으면 절대값 triggerWill
    const trigger = e.phase2.trigger as string;
    const m = /(\d+)/.exec(trigger);
    const n = m ? parseInt(m[1]!, 10) : 0;
    def.phase2 = trigger.includes('%')
      ? { triggerPct: n, effects: (e.phase2.effects ?? []) as EnemyEffectDef[] }
      : { triggerWill: n, effects: (e.phase2.effects ?? []) as EnemyEffectDef[] };
  }
  return def;
}

export function loadEnemies(path = join(DESIGN_DIR, 'enemies-v1.0.yaml')): Map<string, EnemyDef> {
  const raw = yaml.load(readFileSync(path, 'utf8')) as Raw;
  const map = new Map<string, EnemyDef>();
  for (const e of [...(raw.enemies ?? []), ...(raw.bosses ?? [])] as Raw[]) map.set(e.id, convertEnemy(e));
  return map;
}

export function loadAll(): LoadedData {
  const { index, startingDeck, irremovable } = loadCards();
  return { cards: index, startingDeck, irremovable, enemies: loadEnemies() };
}
