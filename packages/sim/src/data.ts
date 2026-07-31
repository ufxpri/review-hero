// YAML(design/cards-v1.0.yaml, enemies-v1.0.yaml) → core 타입 변환 (fs 접근은 sim만 담당)
//
// 미구현(런 레벨 — 감사 기록): GDD §3.6 덱 구축 규칙(유니크 1장 제한 X08/X04/S09/X09,
// 생계형 리뷰 표식 irremovable, 1막 보상 풀 제외 excluded_from_act1_rewards)은 카드 획득/
// 제거가 존재하는 런 레이어의 검증 사항이다. 전투 엔진(Battle)은 주어진 덱을 그대로 사용하며
// 현 시뮬은 시작 덱(+ CLI 주입 덱)이 고정이라 실해 없음. 런 레이어(카드 획득/제거/판매)
// 착수 시 이 파일에서 해당 필드를 파싱하고 덱 검증 함수를 추가할 것.
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
  type Suit,
} from '../../core/src/index.ts';

const DESIGN_DIR = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'design');

/* eslint-disable @typescript-eslint/no-explicit-any */
type Raw = Record<string, any>;

export interface LoadedData {
  cards: CardIndex;
  startingDeck: string[];
  enemies: Map<string, EnemyDef>;
}

export function loadCards(path = join(DESIGN_DIR, 'cards-v1.0.yaml')): { index: CardIndex; startingDeck: string[] } {
  const raw = yaml.load(readFileSync(path, 'utf8')) as Raw;
  const defs: CardDef[] = [];

  for (const p of raw.prefixes as Raw[]) {
    defs.push({
      kind: 'prefix',
      id: p.id,
      name: p.name,
      suit: p.suit as Suit,
      tags: Array.isArray(p.tag) ? p.tag : [p.tag],
      cost: p.cost,
      modifier: p.modifier,
    });
  }
  for (const s of raw.suffixes as Raw[]) {
    defs.push({
      kind: 'suffix',
      id: s.id,
      name: s.name,
      cost: s.cost,
      sType: s.type,
      target: s.target,
      effect: s.effect,
      unique: s.unique ?? false,
    });
  }
  for (const x of raw.specials as Raw[]) {
    defs.push({
      kind: 'special',
      id: x.id,
      name: x.name,
      cost: x.cost,
      layer: x.layer ?? 1,
      unique: x.unique ?? false,
      oncePerCombat: x.once_per_combat ?? false,
      effect: x.effect,
    });
  }
  const startingDeck = (raw.starting_deck as Raw[]).map((e) => e.id as string);
  return { index: buildCardIndex(defs), startingDeck };
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
  // 특성 파싱 (E05 vanity, E04 stealth_gate)
  for (const t of (e.traits ?? []) as Raw[]) {
    if (t.damage_multiplier_from_suit) def.suitDamageMult = t.damage_multiplier_from_suit;
    if (t.hittable_suits_while_stealth) {
      def.stealthGate = { hittableSuits: t.hittable_suits_while_stealth, breakOnHit: t.break_stealth_on_hit ?? true };
    }
    // casting_weakness(E03)는 P06 modifier(vs_casting_mult)로 이미 구현됨 — 중복 적용 안 함
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
  const { index, startingDeck } = loadCards();
  return { cards: index, startingDeck, enemies: loadEnemies() };
}
