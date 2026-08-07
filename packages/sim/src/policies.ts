// 플레이어 정책(AI) 3종 — v2 (card-system-v2.md, ADR-011): 접두+접미 2단 선택 →
// 단일 카드 선택 + 대상 지정.
//
// 정책 의도(v1 승계):
// - standard: 우위 판정 우선 (원산지 > 팩트 > 일반), 헛소리 회피 — 헛소리밖에 없으면 제출 대신
//   퇴고(태그 사냥 — card-system-v2 §7)로 손패를 교체한다.
// - skilled : standard + 대상 최적화(원산지 구성품·팩트 장비 선택), X06 리액션 타이밍, 초반 버프.
// - reckless: 완전 무작위 제출(헛소리 포함 — §3.4 "억지 플레이" 상당), 크리 롤 0.7.
//
// ⚠️ v1의 pFact/pFumble "목표 판정 롤"(의도적 오류 주입)은 폐기 — v2는 손패 5장 전부가 태그
// 선택지라 판정이 손패 가용성의 함수가 되고, 정책은 greedy 선택으로 그 상한을 실측한다.
// telemetry.attempted는 정책이 제출 시점에 기대한 판정(엔진 실현과 일치해야 정상),
// noWantedFallbacks는 우위 판정(원산지/팩트) 없이 제출한 횟수다.
// 주의: 정책은 게이지 10 도달 즉시(critProb 롤 통과 시) 크리를 사용한다 — 타이밍 최적화 없음.

import {
  Battle,
  DISPOSITION_SUIT,
  JUDGE_MULT,
  type CardIndex,
  type Judgement,
  type ReviewCardDef,
  type Rng,
  type SpecialDef,
} from '../../core/src/index.ts';

export type PolicyName = 'standard' | 'skilled' | 'reckless';

interface PolicyParams {
  critProb: number;
  smart: boolean; // 대상·순서 최적화 여부
  random: boolean; // 완전 무작위 제출 (헛소리 회피 없음)
}

export const POLICIES: Record<PolicyName, PolicyParams> = {
  standard: { critProb: 1.0, smart: false, random: false },
  skilled: { critProb: 1.0, smart: true, random: false },
  reckless: { critProb: 0.7, smart: false, random: true },
};

const JUDGE_RANK: Record<Judgement, number> = { origin: 3, fact: 2, normal: 1, fumble: 0 };

/** 의도(정책 기대 판정) vs 실현(엔진 판정) 대조용 계측 — CLI가 집계·출력 */
export interface PolicyTelemetry {
  attempted: { origin: number; fact: number; normal: number; fumble: number }; // 제출 시점 기대 판정 (random 정책은 미집계)
  noWantedFallbacks: number; // 우위 판정(원산지/팩트) 없이 제출한 횟수
}

export function newTelemetry(): PolicyTelemetry {
  return { attempted: { origin: 0, fact: 0, normal: 0, fumble: 0 }, noWantedFallbacks: 0 };
}

function pick<T>(arr: T[], rng: Rng): T {
  return arr[Math.floor(rng() * arr.length)]!;
}

/** 제출 후보 1건 — 카드 + 대상 지정 + 기대 판정 (엔진 판정 규칙과 동일하게 산출) */
interface PlayOption {
  uid: number;
  def: ReviewCardDef;
  judgement: Judgement;
  myEquipmentIndex?: number;
  enemyEquipmentIndex?: number;
  score: number; // 기대 피해 상당치 (동순위 정렬용)
}

/** 엔진과 동일 규칙으로 카드의 최적 대상·판정을 평가한다. 제출 무의미(은신 빗나감·슬롯 만석·대상 없음)면 null */
function evaluate(battle: Battle, uid: number, def: ReviewCardDef): PlayOption | null {
  const st = battle.state;
  const e = st.enemy;
  const gate = e.def.stealthGate;

  // E04 은신 게이트: 명중 불가 계열은 빗나감 — 제출하지 않는다
  if (e.stealth && gate && (def.target === 'enemy' || def.target === 'enemy_equipment') && !gate.hittableSuits.includes(def.suit)) {
    return null;
  }

  const judgeAgainst = (tags: string[], nulls: string[], isOrigin: boolean): Judgement => {
    if (isOrigin) return 'origin';
    if (nulls.includes(def.tag)) return 'fumble';
    if (tags.includes(def.tag)) return 'fact';
    return 'normal';
  };

  let judgement: Judgement;
  let myEquipmentIndex: number | undefined;
  let enemyEquipmentIndex: number | undefined;

  if (def.target === 'my_equipment') {
    // 판정 좋은 내 장비 우선. damage_buff는 부착 슬롯(2칸) 여유 필수 — 만석이면 제출 낭비
    let best: { idx: number; j: Judgement } | null = null;
    for (let i = 0; i < st.player.equipment.length; i++) {
      const eq = st.player.equipment[i]!;
      if (def.effect.type === 'damage_buff' && eq.attachments.filter((a) => a.usesSlot).length >= 2) continue;
      const j = judgeAgainst(eq.def.tags, eq.def.nullTags, false);
      if (!best || JUDGE_RANK[j] > JUDGE_RANK[best.j]) best = { idx: i, j };
    }
    if (!best) return null;
    judgement = best.j;
    myEquipmentIndex = best.idx;
  } else if (def.target === 'enemy_equipment') {
    // 원산지 일치 구성품 우선, 없으면 판정 최선 구성품
    let best: { idx: number; j: Judgement } | null = null;
    for (let i = 0; i < e.equipment.length; i++) {
      const eq = e.equipment[i]!;
      if (eq.destroyed) continue;
      const isOrigin = def.origin?.equipment !== undefined && def.origin.equipment === eq.name;
      const j = judgeAgainst(eq.tags, e.def.nullTags, isOrigin);
      if (!best || JUDGE_RANK[j] > JUDGE_RANK[best.j]) best = { idx: i, j };
    }
    if (!best) return null; // 남은 구성품 없음
    judgement = best.j;
    enemyEquipmentIndex = best.idx;
  } else {
    const isOrigin = def.origin?.enemy !== undefined && def.origin.enemy === e.def.id;
    judgement = judgeAgainst(e.def.weaknessTags, e.def.nullTags, isOrigin);
  }

  // 기대 피해 상당치: 의지 피해(value/damage 동반) 또는 구성품 피해 × 판정 배율 (+원산지 +1)
  const ef = def.effect;
  const base = ef.type === 'damage' || ef.type === 'equipment_damage' ? (ef.value ?? 0) : (ef.damage ?? 0);
  const vanity = e.def.suitDamageMult?.[def.suit] ?? 1;
  const score = Math.floor(base * JUDGE_MULT[judgement] * vanity) + (judgement === 'origin' ? 1 : 0);

  return { uid, def, judgement, myEquipmentIndex, enemyEquipmentIndex, score };
}

/** 한 플레이어 턴을 정책대로 진행하고 endTurn까지 수행한다 */
export function playTurn(battle: Battle, cards: CardIndex, name: PolicyName, rng: Rng, telemetry?: PolicyTelemetry): void {
  const params = POLICIES[name];
  let safety = 20;
  let critRolled = false; // critProb 롤은 턴당 1회 (루프 반복마다 재롤하면 억지 정책의 0.7이 사실상 1.0이 됨)

  while (safety-- > 0) {
    const st = battle.state;
    if (st.result) return;
    const p = st.player;

    // 크리티컬 (게이지 10, 필력 0, 턴당 1회)
    // E04 은신 게이트: 은신 중 명중 불가 계열의 크리는 빗나가므로(게이지만 소모) 시도하지 않는다
    const gate = st.enemy.def.stealthGate;
    const critBlockedByStealth =
      st.enemy.stealth && !!gate && p.disposition !== '바이럴 앞잡이' && !gate.hittableSuits.includes(DISPOSITION_SUIT[p.disposition]);
    if (p.gauge >= 10 && !p.critUsedThisTurn && !critRolled && !critBlockedByStealth) {
      critRolled = true;
      if (rng() < params.critProb) {
        battle.useCritical();
        continue;
      }
    }

    const hand = battle.state.player.hand.map((c) => ({ uid: c.uid, def: cards.byId.get(c.cardId)! }));
    const reviews = hand.filter((c): c is { uid: number; def: ReviewCardDef } => c.def.kind === 'review');
    const specials = hand.filter((c): c is { uid: number; def: SpecialDef } => c.def.kind === 'special');

    // X08 별점 구걸(+3): 게이지 여유 있으면 사용 (전 정책 — 순수 이득)
    const x08 = specials.find((c) => c.def.id === 'X08');
    if (x08 && p.energy >= x08.def.cost && p.gauge <= 7) {
      battle.playSpecial(x08.uid);
      continue;
    }

    // skilled: 적 인텐트가 강공격이고 리액션 미설치면 X06 설치
    if (params.smart) {
      const x06 = specials.find((c) => c.def.id === 'X06');
      const intent = st.enemy.def.actions.find((a) => a.id === st.enemy.intentId);
      const incoming = intent?.effects.find((e) => e.op === 'damage')?.value ?? 0;
      if (x06 && !p.reaction && p.energy >= x06.def.cost && intent?.aType === 'attack' && incoming >= 7) {
        battle.playSpecial(x06.uid);
        continue;
      }
    }

    // 제출 후보: 필력 내 리뷰 카드 × 최적 대상
    const options = reviews
      .filter((c) => c.def.cost <= p.energy)
      .map((c) => evaluate(battle, c.uid, c.def))
      .filter((o): o is PlayOption => o !== null);

    // reckless: 완전 무작위 제출 (헛소리 회피 없음. 대상은 evaluate가 고른 그대로)
    if (params.random) {
      if (options.length === 0) break;
      const o = pick(options, rng);
      battle.submitReview(o.uid, { myEquipmentIndex: o.myEquipmentIndex, enemyEquipmentIndex: o.enemyEquipmentIndex });
      continue;
    }

    // standard/skilled: 헛소리 회피 — 헛소리가 아닌 후보만
    let pool = options.filter((o) => o.judgement !== 'fumble');

    // skilled: 초반(1~2턴) 버프 부착 우선 (v1 S13/S14 조기 부착 의도 승계)
    if (params.smart && st.turn <= 2) {
      const buffs = pool.filter((o) => o.def.effect.type === 'damage_buff');
      if (buffs.length) pool = buffs;
    }

    if (pool.length === 0) {
      // 은신 턴: 명중 계열이 없으면 낭비 방지 — 패스 (퇴고해도 이번 턴 명중 보장 없음)
      if (st.enemy.stealth && st.enemy.def.stealthGate) break;
      // X03: 낼 카드가 없으면 무작위 카드 생성 시도
      const x03 = specials.find((c) => c.def.id === 'X03');
      if (x03 && p.energy >= x03.def.cost && p.hand.length < 8 && options.length === 0) {
        battle.playSpecial(x03.uid);
        continue;
      }
      // 퇴고 (v2: 태그 사냥 — card-system-v2 §7): 헛소리/불용 카드를 버리고 교체
      if (p.energy >= 1 && p.hand.length > 0 && p.deck.length + p.discard.length > 0) {
        const fumbleOpt = options.find((o) => o.judgement === 'fumble');
        const target = fumbleOpt ?? hand[0];
        if (target) {
          battle.revise(target.uid);
          continue;
        }
      }
      break;
    }

    // 우위 판정 > 기대 피해 > 저비용 순
    pool.sort((a, b) => JUDGE_RANK[b.judgement] - JUDGE_RANK[a.judgement] || b.score - a.score || a.def.cost - b.def.cost);
    const best = pool[0]!;
    if (telemetry) {
      telemetry.attempted[best.judgement]++;
      if (JUDGE_RANK[best.judgement] < JUDGE_RANK.fact) telemetry.noWantedFallbacks++;
    }
    battle.submitReview(best.uid, { myEquipmentIndex: best.myEquipmentIndex, enemyEquipmentIndex: best.enemyEquipmentIndex });
  }

  if (!battle.state.result) battle.endTurn();
}
