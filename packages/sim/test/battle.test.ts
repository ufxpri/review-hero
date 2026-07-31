// 전투 상태머신 규칙 검증 (GDD §2·§3 + 악용 검증 엣지)
// 실데이터(cards-v1.0.yaml / enemies-v1.0.yaml)를 로드해 core를 구동한다.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Battle, mulberry32, type BattleConfig } from '../../core/src/index.ts';
import { loadAll } from '../src/data.ts';

const data = loadAll();

/** noShuffle 덱으로 시작 손패를 고정한다 (handIds 순서대로 드로우) */
function makeBattle(enemyId: string, handIds: string[], opts: Partial<BattleConfig> = {}): Battle {
  const deck = [...(opts.deck ?? []), ...[...handIds].reverse()];
  return new Battle({
    cards: data.cards,
    enemy: data.enemies.get(enemyId)!,
    rng: mulberry32(1),
    noShuffle: true,
    ...opts,
    deck,
  });
}

function uid(b: Battle, cardId: string, nth = 0): number {
  const found = b.state.player.hand.filter((c) => c.cardId === cardId);
  assert.ok(found.length > nth, `손패에 ${cardId} 없음`);
  return found[nth]!.uid;
}

// ── §2 공통 계산 ──────────────────────────────────────

test('§3.3+§2-1: 팩트 ×1.5 내림 (S03 히트당 판정·게이지)', () => {
  const b = makeBattle('E01', ['P01', 'S03']);
  const r = b.submitReview(uid(b, 'P01'), uid(b, 'S03'));
  assert.equal(r.judgement, 'fact'); // 마감 ∈ E01 약점
  assert.equal(b.state.enemy.will, 14 - 8); // floor(3×1.5)=4 ×2히트
  assert.equal(b.state.player.gauge, 6); // 히트당 +3 ×2 (GDD §3.3, v1.1 제안 1)
  assert.equal(b.state.stats.judgements.fact, 2);
});

test('§3.3+§2-1: 헛소리 ×0.5 최소 1 + S11 인라인 게이지 −1', () => {
  const b = makeBattle('E01', ['P07', 'S11'], { startGauge: 3 });
  const r = b.submitReview(uid(b, 'P07'), uid(b, 'S11'));
  assert.equal(r.judgement, 'fumble'); // 연비 ∈ E01 무효 태그
  assert.equal(b.state.enemy.will, 13); // max(1, floor(1×0.5)) = 1
  assert.equal(b.state.player.gauge, 0); // 3 −2(헛소리) −1(S11) = 0
});

test('§2-2: 게이지 상한 10 초과 소실 / 하한 0', () => {
  const hi = makeBattle('E01', ['P01', 'S01'], { startGauge: 9 });
  hi.submitReview(uid(hi, 'P01'), uid(hi, 'S01')); // 팩트 +3 (v1.1)
  assert.equal(hi.state.player.gauge, 10); // 9+3=12 → 10 (초과 소실)

  const lo = makeBattle('E01', ['P07', 'S01'], { startGauge: 1 });
  lo.submitReview(uid(lo, 'P07'), uid(lo, 'S01')); // 헛소리 −2
  assert.equal(lo.state.player.gauge, 0); // 1−2 → 0 (하한)
});

test('§2-3: 필력 이월 없음 + S15는 다음 턴 가산(판정 배율 적용)', () => {
  const b = makeBattle('E01', ['P01', 'S15', 'P05', 'S01']);
  b.submitReview(uid(b, 'P01'), uid(b, 'S15'), { myEquipmentIndex: 0 }); // 롱소드[마감] 팩트 → floor(1×1.5)=1
  assert.equal(b.state.player.energy, 2); // 남은 필력은 이월되지 않는다
  b.endTurn();
  assert.equal(b.state.player.energy, 4); // 3 + 1 (S15)
  b.endTurn();
  assert.equal(b.state.player.energy, 3); // 보정 소멸
});

// ── §3.2 턴 시퀀스·기절·리액션·지연 ──────────────────

test('§3.2: 기절 시 attack 불발, gimmick은 기절 무시 발동 (S09 락 봉쇄)', () => {
  const b = makeBattle('B01', ['P11', 'S01', 'P01', 'S09']);
  b.submitReview(uid(b, 'P11'), uid(b, 'S01')); // 응대 팩트 → floor(6×1.5)=9 → 51
  assert.equal(b.state.enemy.will, 51);
  b.endTurn(); // contract_slash 8
  assert.equal(b.state.player.will, 22);
  b.submitReview(uid(b, 'P01'), uid(b, 'S09')); // 기절 1턴
  assert.equal(b.state.enemy.stunTurns, 1);
  b.state.enemy.patternIndex = 2;
  b.state.enemy.intentId = 'owner_reply'; // 기믹 강제
  b.endTurn();
  assert.equal(b.state.enemy.will, 56); // 기절 무시 발동: 반박 대상 없음 → 의지 +5
  assert.equal(b.state.enemy.stunTurns, 0);
  assert.equal(b.state.enemy.staggerImmunityTurns, 1); // 기상 → 경직 내성 1턴
});

test('§3.2: 경직 내성 중 지연(X01) 면역 — 연쇄 락 불가', () => {
  const b = makeBattle('E01', ['P01', 'S09', 'X01', 'S01', 'P05']);
  b.submitReview(uid(b, 'P01'), uid(b, 'S09')); // 기절
  b.endTurn(); // stab 불발, 기상 + 경직 내성
  assert.equal(b.state.player.will, 30);
  assert.equal(b.state.enemy.staggerImmunityTurns, 1);
  b.playSpecial(uid(b, 'X01')); // 지연 시도 → 면역
  assert.equal(b.state.enemy.pendingDelay, false);
  b.endTurn(); // 행동 정상 실행 (stab 5)
  assert.equal(b.state.player.will, 25);
});

test('§3.2: X06 리액션 — attack에 자동 발동, −50% 후 50% 반사, 1회 소진', () => {
  const b = makeBattle('E01', ['X06']);
  b.playSpecial(uid(b, 'X06'));
  assert.ok(b.state.player.reaction);
  b.endTurn(); // stab 5 → floor(5×0.5)=2 피해, floor(2×0.5)=1 반사
  assert.equal(b.state.player.will, 28);
  assert.equal(b.state.enemy.will, 13);
  assert.equal(b.state.player.reaction, null); // 소진
});

test('§3.2: X01 지연 — 적 행동 스킵(인텐트 유지)', () => {
  const b = makeBattle('E01', ['X01'], { startGauge: 5 });
  b.playSpecial(uid(b, 'X01'));
  assert.equal(b.state.player.gauge, 4); // 게이지 −1 (§3.4)
  b.endTurn();
  assert.equal(b.state.player.will, 30); // stab 스킵
  assert.equal(b.state.enemy.intentId, 'stab'); // 인텐트 유지
  b.endTurn();
  assert.equal(b.state.player.will, 25); // 다음 턴 정상 실행
});

test('E02: 준비(charge) 중 지연 적중 시 내려찍기 캔슬 (cancel_on)', () => {
  const b = makeBattle('E02', ['X01', 'P01', 'S01', 'P05', 'S05'], { deck: ['S01', 'P01'] });
  b.endTurn(); // roar (공격력 +2)
  assert.equal(b.state.enemy.intentId, 'smash');
  b.endTurn(); // smash 준비 시작
  assert.ok(b.state.enemy.charging);
  b.playSpecial(uid(b, 'X01')); // 준비 중 지연 → 캔슬
  assert.equal(b.state.enemy.charging, null);
  assert.equal(b.state.enemy.intentId, 'roar'); // 행동 소멸, 패턴 진행
  assert.equal(b.state.player.will, 30); // 9딜 무효
});

// ── §3.3 판정 확장 ────────────────────────────────────

test('§3.3: S13 버프 가산은 "제출당 1회" (S03 다중 히트에 히트당 적용 금지)', () => {
  const b = makeBattle('E01', ['P01', 'S13', 'P01', 'S03'], { deck: ['P01'] });
  b.submitReview(uid(b, 'P01'), uid(b, 'S13'), { myEquipmentIndex: 0 }); // 롱소드[마감] 팩트 → 부착 +3
  assert.equal(b.state.player.equipment[0]!.attachments[0]!.value, 3); // floor(2×1.5)
  b.submitReview(uid(b, 'P01'), uid(b, 'S03'));
  // 히트1 (3+3)×1.5=9, 히트2 3×1.5=4 (가산은 첫 히트만) → 14−13=1
  assert.equal(b.state.enemy.will, 1);
});

// ── §3.5 크리티컬 ─────────────────────────────────────

test('§3.5: 크리티컬은 턴당 1회, 게이지 전량 소모', () => {
  const b = makeBattle('B01', ['P01', 'S01']);
  b.state.player.gauge = 10;
  b.useCritical(); // 팩트 폭격기 (초기값) — 고정 20
  assert.equal(b.state.enemy.will, 40);
  assert.equal(b.state.player.gauge, 0);
  b.state.player.gauge = 10;
  assert.throws(() => b.useCritical(), /턴당 1회/);
});

test('§3.5: 바이럴 앞잡이 — 버프 2배 가산, 크리 간 공유 상한 +12', () => {
  const b = makeBattle('B01', ['P01', 'S13'], { initialSuitCounters: { 감성: 2 } });
  assert.equal(b.state.player.disposition, '바이럴 앞잡이'); // 스냅샷 (argmax)
  b.submitReview(uid(b, 'P01'), uid(b, 'S13'), { myEquipmentIndex: 0 }); // 부착 +3
  const att = () => b.state.player.equipment[0]!.attachments[0]!.value;
  b.state.player.gauge = 10;
  b.useCritical(); // 3→6 (+3)
  assert.equal(att(), 6);
  b.endTurn();
  b.state.player.gauge = 10;
  b.useCritical(); // 6→12 (+6, 누적 9)
  assert.equal(att(), 12);
  b.endTurn();
  b.state.player.gauge = 10;
  b.useCritical(); // 잔여 예산 3 → 12→15 (누적 12)
  assert.equal(att(), 15);
  b.endTurn();
  b.state.player.gauge = 10;
  b.useCritical(); // 상한 소진 → 변화 없음
  assert.equal(att(), 15);
  assert.equal(b.state.player.viralBonusGranted, 12);
});

// ── §3.6 덱 규칙 ──────────────────────────────────────

test('§3.6: X04 증정 카드는 런 제외, 비용 ×4 데미지(0코는 최소 1)', () => {
  const b = makeBattle('E01', ['X04', 'S01', 'P01']);
  b.playSpecial(uid(b, 'X04'), { giftUid: uid(b, 'S01') });
  assert.equal(b.state.enemy.will, 10); // 1코 ×4 = 4
  assert.equal(b.state.player.removedFromRun.length, 1);
  assert.equal(b.state.player.removedFromRun[0]!.cardId, 'S01');
  assert.ok(!b.state.player.discard.some((c) => c.cardId === 'S01')); // 묘지 순환에서 제외
  const b2 = makeBattle('E01', ['X04', 'P01']);
  b2.playSpecial(uid(b2, 'X04'), { giftUid: uid(b2, 'P01') });
  assert.equal(b2.state.enemy.will, 13); // 0코 → max(1, 0×4) = 1 (가정: §2-1 최소 1)
});

// ── §3.8 적 규칙 ──────────────────────────────────────

test('E01: 골드 갈취 하한 0 (음수 골드 없음)', () => {
  const b = makeBattle('E01', [], { gold: 2 });
  b.state.enemy.patternIndex = 2;
  b.state.enemy.intentId = 'ripoff';
  b.endTurn();
  assert.equal(b.state.player.gold, 0);
});

test('E04: 은신 중 배송 계열만 명중, 명중 시 은신 해제 → 기습 12→6', () => {
  const b = makeBattle('E04', ['P01', 'S01', 'P09', 'S01'], { deck: ['P05'] });
  b.endTurn(); // hide → 은신
  assert.equal(b.state.enemy.stealth, true);
  const miss = b.submitReview(uid(b, 'P01'), uid(b, 'S01')); // 품질 → 빗나감
  assert.equal(miss.missed, true);
  assert.equal(b.state.enemy.will, 22); // E04 의지 22 (v1.1)
  assert.equal(b.state.stats.judgements.fact + b.state.stats.judgements.normal + b.state.stats.judgements.fumble, 0);
  const hit = b.submitReview(uid(b, 'P09'), uid(b, 'S01')); // 배송(속도) → 팩트, 은신 해제
  assert.equal(hit.judgement, 'fact');
  assert.equal(b.state.enemy.stealth, false);
  assert.equal(b.state.enemy.will, 13); // 22 − floor(6×1.5)=9
  b.endTurn(); // ambush: 은신 해제 상태 → 6
  assert.equal(b.state.player.will, 24);
});

test('E05: 감성 계열 의지 데미지 ×2 + 찬양 강요 게이지 −1(하한 0)', () => {
  const b = makeBattle('E05', ['P13', 'S01'], { startGauge: 5 });
  b.submitReview(uid(b, 'P13'), uid(b, 'S01')); // 디자인 팩트 → floor(6×1.5×2)=18
  assert.equal(b.state.enemy.will, 10); // E05 의지 28 (v1.1) − 18
  assert.equal(b.state.player.gauge, 8); // 5 + 팩트 +3 (v1.1)
  b.state.enemy.patternIndex = 2;
  b.state.enemy.intentId = 'demand_praise';
  b.endTurn();
  assert.equal(b.state.player.gauge, 7); // −1 (§3.4)
  const lo = makeBattle('E05', []);
  lo.state.enemy.patternIndex = 2;
  lo.state.enemy.intentId = 'demand_praise';
  lo.endTurn();
  assert.equal(lo.state.player.gauge, 0); // 하한 0
});

test('B01: 사장님 답글 — 정지 → 같은 계열 팩트로 재반박(부활+게이지+1) → 재정지 불가', () => {
  const b = makeBattle('B01', ['P11', 'S12', 'P11', 'S01'], { deck: ['P01', 'S01'] });
  b.submitReview(uid(b, 'P11'), uid(b, 'S12')); // 응대 팩트 → 공격력 −3 디버프(배송 계열)
  const debuff = b.state.enemy.debuffs[0]!;
  assert.equal(debuff.value, 3); // floor(2×1.5)
  b.state.enemy.patternIndex = 2;
  b.state.enemy.intentId = 'owner_reply';
  b.endTurn();
  assert.equal(debuff.suspended, true); // 이번 전투 한정 정지
  const g = b.state.player.gauge;
  b.submitReview(uid(b, 'P11'), uid(b, 'S01')); // 같은 계열(배송) 팩트 → 재반박
  assert.equal(debuff.suspended, false); // 부활
  assert.equal(b.state.player.gauge, g + 3 + 1); // 팩트 +3(v1.1), 재반박 성공 +1 (§3.4)
  const will = b.state.enemy.will;
  b.state.enemy.patternIndex = 2;
  b.state.enemy.intentId = 'owner_reply';
  b.state.enemy.cooldownLastFired['owner_reply'] = -10; // cooldown 3 경과 가정 (하한 강제 규칙 우회)
  b.endTurn();
  assert.equal(debuff.suspended, false); // 디버프당 반박 1회 소진 → 재정지 불가
  assert.equal(b.state.enemy.will, Math.min(60, will + 5)); // 대상 없음 → 의지 +5만
});

test('B01: 반박 우선순위 — 힙스터 크리(Tier 3)를 일반 디버프(Tier 1)보다 먼저 정지', () => {
  const b = makeBattle('B01', ['P11', 'S12', 'P05', 'S01'], { initialSuitCounters: { 성능: 1 } });
  assert.equal(b.state.player.disposition, '힙스터 평론가');
  b.submitReview(uid(b, 'P11'), uid(b, 'S12')); // Tier 1 attack_down
  b.state.player.gauge = 10;
  b.useCritical(); // Tier 3 attack_halve
  b.state.enemy.patternIndex = 2;
  b.state.enemy.intentId = 'owner_reply';
  b.endTurn();
  const halve = b.state.enemy.debuffs.find((d) => d.kind === 'attack_halve')!;
  const down = b.state.enemy.debuffs.find((d) => d.kind === 'attack_down')!;
  assert.equal(halve.suspended, true); // 최우선 반박 (R22)
  assert.equal(down.suspended, false);
  b.state.enemy.patternIndex = 0;
  b.state.enemy.intentId = 'contract_slash';
  b.endTurn();
  assert.equal(b.state.player.will, 30 - (8 - 3)); // 힙스터 정지 → −50% 미적용, attack_down 3만
});

test('전 장비 파괴 → 항복 승리 + 6G (P02 장비 데미지 보정 포함)', () => {
  const b = makeBattle('E01', ['P02', 'S05'], { gold: 0 });
  b.submitReview(uid(b, 'P02'), uid(b, 'S05'), { enemyEquipmentIndex: 0 });
  // 짝퉁 단검[마감,내구도] 팩트: floor((4+1)×1.5)=7 ≥ 내구도 6 → 파괴
  assert.equal(b.state.result, 'win');
  assert.equal(b.state.stats.surrender, true);
  assert.equal(b.state.player.gold, 6);
});

test('X09(Layer 2): Σp×3 데미지 + 게이지 +1, 전투당 1회', () => {
  const b = makeBattle('B01', ['X09', 'P01'], { layer: 2, sigmaP: 5 });
  b.playSpecial(uid(b, 'X09'));
  assert.equal(b.state.enemy.will, 45); // 5×3 = 15
  assert.equal(b.state.player.gauge, 1);
  assert.ok(b.state.player.oncePerCombatUsed.has('X09'));
  const b2 = makeBattle('B01', ['X09'], { layer: 1 });
  assert.throws(() => b2.playSpecial(uid(b2, 'X09')), /Layer/); // MVP(Layer 1)에서는 사용 불가
});

test('결정성: 같은 시드·같은 정책 입력 → 같은 결과', () => {
  const run = () => {
    const b = new Battle({
      cards: data.cards,
      enemy: data.enemies.get('E03')!,
      deck: data.startingDeck,
      rng: mulberry32(123),
    });
    // 고정 스크립트: 매턴 첫 접두+첫 데미지 접미 제출
    let guard = 40;
    while (!b.state.result && guard-- > 0) {
      const hand = b.state.player.hand;
      const pre = hand.find((c) => data.cards.byId.get(c.cardId)!.kind === 'prefix');
      const suf = hand.find((c) => {
        const d = data.cards.byId.get(c.cardId)!;
        return d.kind === 'suffix' && d.effect.type === 'damage' && d.cost + (pre ? data.cards.byId.get(pre.cardId)!.cost : 9) <= b.state.player.energy;
      });
      if (pre && suf) b.submitReview(pre.uid, suf.uid);
      else b.endTurn();
    }
    return JSON.stringify([b.state.result, b.state.turn, b.state.player.will, b.state.enemy.will, b.state.stats]);
  };
  assert.equal(run(), run());
});
