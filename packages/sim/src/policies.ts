// 플레이어 정책(AI) 3종 — GDD §3.4 시뮬 전제(표준/숙련/억지)의 "목표 판정 롤" 구현
//
// ⚠️ pFact/pFumble은 "목표 판정 롤"일 뿐이다: 원하는 판정의 접두가 손패에 없으면 일반(skilled는
// 팩트 우선)으로 폴백하므로, 실측 판정 비율은 §3.4 전제(표준 50%/10%, 숙련 75%/5%, 억지
// 30%/25%)에 크게 못 미친다. 실측(시작 덱, seed 42/각 500~1000판): standard 팩트 2.4~28.0%·
// 헛소리 0.5~12.5%, skilled 팩트 12.2~37.9%, reckless 팩트 20~26%로 매치업 의존 변동이 크다.
// 특히 시작 덱 접두 태그와 B01 약점 태그(응대/개연성)는 교집합이 없어 보스 시뮬은 --deck 주입
// 없이는 §3.4 검증·R11 재시뮬 도구로 부적합하다. CLI가 "의도(attempted) vs 실현(achieved)"
// 판정 비율을 병기하니 결과 해석 시 반드시 대조할 것.
// 주의: 정책은 게이지 10 도달 즉시(critProb 롤 통과 시) 크리를 사용한다 — 타이밍 최적화 없음.

import {
  Battle,
  DISPOSITION_SUIT,
  type CardIndex,
  type Judgement,
  type PrefixDef,
  type Rng,
  type SpecialDef,
  type SuffixDef,
} from '../../core/src/index.ts';

export type PolicyName = 'standard' | 'skilled' | 'reckless';

interface PolicyParams {
  pFact: number;
  pFumble: number;
  critProb: number;
  smart: boolean; // 대상·순서 최적화 여부
  random: boolean; // 완전 무작위 제출
}

export const POLICIES: Record<PolicyName, PolicyParams> = {
  standard: { pFact: 0.5, pFumble: 0.1, critProb: 1.0, smart: false, random: false },
  skilled: { pFact: 0.75, pFumble: 0.05, critProb: 1.0, smart: true, random: false },
  reckless: { pFact: 0.3, pFumble: 0.25, critProb: 0.7, smart: false, random: true },
};

interface HandCard {
  uid: number;
  def: PrefixDef | SuffixDef | SpecialDef;
}

function handCards(battle: Battle, cards: CardIndex): HandCard[] {
  return battle.state.player.hand.map((c) => ({ uid: c.uid, def: cards.byId.get(c.cardId)! }));
}

/** 대상 태그·무효 태그 계산 (엔진과 동일 규칙) */
function targetTagsFor(
  battle: Battle,
  suffix: SuffixDef,
  myEqIdx: number,
  enemyEqIdx: number,
): { tags: string[]; nulls: string[] } {
  const st = battle.state;
  if (suffix.target === 'my_equipment') {
    const eq = st.player.equipment[myEqIdx] ?? st.player.equipment[0]!;
    return { tags: eq.def.tags, nulls: eq.def.nullTags };
  }
  if (suffix.target === 'enemy_equipment') {
    const eq = st.enemy.equipment[enemyEqIdx];
    return { tags: eq && !eq.destroyed ? eq.tags : [], nulls: st.enemy.def.nullTags };
  }
  return { tags: st.enemy.def.weaknessTags, nulls: st.enemy.def.nullTags };
}

function judgeOf(prefix: PrefixDef, tags: string[], nulls: string[]): Judgement {
  if (prefix.tags.some((t) => tags.includes(t))) return 'fact';
  if (prefix.tags.some((t) => nulls.includes(t)) && prefix.modifier?.type !== 'no_fumble') return 'fumble';
  return 'normal';
}

function pick<T>(arr: T[], rng: Rng): T {
  return arr[Math.floor(rng() * arr.length)]!;
}

/** 의도(목표 롤) vs 실현(엔진 판정) 대조용 계측 — CLI가 집계·출력 */
export interface PolicyTelemetry {
  attempted: { fact: number; normal: number; fumble: number }; // 목표 롤 결과 (random 정책은 미집계)
  noWantedFallbacks: number; // 목표 판정 접두가 손패에 없어 폴백한 횟수
}

export function newTelemetry(): PolicyTelemetry {
  return { attempted: { fact: 0, normal: 0, fumble: 0 }, noWantedFallbacks: 0 };
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

    const hand = handCards(battle, cards);
    const prefixes = hand.filter((c): c is { uid: number; def: PrefixDef } => c.def.kind === 'prefix');
    const suffixes = hand.filter((c): c is { uid: number; def: SuffixDef } => c.def.kind === 'suffix');
    const specials = hand.filter((c): c is { uid: number; def: SpecialDef } => c.def.kind === 'special');

    // X08 별점 구걸: 게이지 여유 있으면 사용 (전 정책 — 순수 이득)
    const x08 = specials.find((c) => c.def.id === 'X08');
    if (x08 && p.energy >= x08.def.cost && p.gauge <= 8) {
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

    // 접두+접미 조합 선택
    const minPrefixCost = prefixes.length ? Math.min(...prefixes.map((c) => c.def.cost)) : Infinity;
    let affordable = suffixes.filter((s) => s.def.cost + minPrefixCost <= p.energy);

    // E04 은신 중: 배송 접두가 없으면 적 대상 접미는 빗나감 — skilled는 버프 접미로 전환
    const stealth = st.enemy.stealth;
    const hasDeliveryPrefix = prefixes.some((c) => c.def.suit === '배송' && c.def.cost + (affordable[0]?.def.cost ?? 0) <= p.energy);
    if (params.smart && stealth && !hasDeliveryPrefix) {
      const buffs = affordable.filter((s) => s.def.target === 'my_equipment');
      if (buffs.length) affordable = buffs;
      else {
        // 낭비 방지: 은신 턴은 패스
        battle.endTurn();
        return;
      }
    }

    if (affordable.length === 0) {
      // X03: 접미가 없으면 무작위 접미 생성 시도
      const x03 = specials.find((c) => c.def.id === 'X03');
      if (x03 && p.energy >= x03.def.cost && p.hand.length < 8 && suffixes.length === 0) {
        battle.playSpecial(x03.uid);
        continue;
      }
      break;
    }

    // 접미 선택
    let suffix: { uid: number; def: SuffixDef };
    if (params.random) {
      suffix = pick(affordable, rng);
    } else {
      const damages = affordable
        .filter((s) => s.def.effect.type === 'damage')
        .sort((a, b) => (b.def.effect.value ?? 0) * (b.def.effect.hits ?? 1) - (a.def.effect.value ?? 0) * (a.def.effect.hits ?? 1));
      const buffsEarly = params.smart && st.turn <= 2 ? affordable.filter((s) => ['S13', 'S14'].includes(s.def.id)) : [];
      suffix = buffsEarly[0] ?? damages[0] ?? affordable[0]!;
    }

    // 대상 선택
    let myEqIdx = 0;
    let enemyEqIdx = st.enemy.equipment.findIndex((eq) => !eq.destroyed);
    if (enemyEqIdx < 0) enemyEqIdx = 0;
    if (suffix.def.target === 'my_equipment') {
      if (params.smart) {
        // 팩트 가능한 장비 우선 (부착 슬롯 여유 고려)
        for (let i = 0; i < p.equipment.length; i++) {
          const eq = p.equipment[i]!;
          if (suffix.def.effect.uses_attach_slot && eq.attachments.filter((a) => a.usesSlot).length >= 2) continue;
          if (prefixes.some((pf) => judgeOf(pf.def, eq.def.tags, eq.def.nullTags) === 'fact' && pf.def.cost + suffix.def.cost <= p.energy)) {
            myEqIdx = i;
            break;
          }
        }
      } else {
        myEqIdx = Math.floor(rng() * p.equipment.length);
      }
    }

    // 접두 선택 — 목표 판정 롤 (의도적 오류 주입)
    const { tags, nulls } = targetTagsFor(battle, suffix.def, myEqIdx, enemyEqIdx);
    const candidates = prefixes.filter((c) => c.def.cost + suffix.def.cost <= p.energy);
    if (candidates.length === 0) break;

    let prefix: { uid: number; def: PrefixDef };
    if (params.random) {
      prefix = pick(candidates, rng);
    } else {
      const r = rng();
      const want: Judgement = r < params.pFact ? 'fact' : r < params.pFact + params.pFumble ? 'fumble' : 'normal';
      if (telemetry) telemetry.attempted[want]++;
      const byJudge = (j: Judgement) => candidates.filter((c) => judgeOf(c.def, tags, nulls) === j);
      const wanted = byJudge(want);
      if (wanted.length) prefix = pick(wanted, rng);
      else {
        if (telemetry) telemetry.noWantedFallbacks++;
        if (params.smart) prefix = byJudge('fact')[0] ?? byJudge('normal')[0] ?? candidates[0]!;
        else prefix = byJudge('normal')[0] ?? candidates[0]!;
      }
    }

    battle.submitReview(prefix.uid, suffix.uid, { myEquipmentIndex: myEqIdx, enemyEquipmentIndex: enemyEqIdx });
  }

  if (!battle.state.result) battle.endTurn();
}
