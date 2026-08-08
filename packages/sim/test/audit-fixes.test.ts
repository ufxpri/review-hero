// 감사 지적 반영분 검증 (2026-07-31 감사 — 은신 크리 게이트, 적 피해 배율 §2-1,
// 온보딩 보정, cooldown 하한, timeout 클램프, 게이지 도달/초과 소실 계측)
// v2 전환 노트: v1의 S07(장비 비활성화) 무한 락 테스트는 v2 카드 60장에 disable_equipment가
// 없어 제거 — 엔진의 disable_equipment 케이스·경직 내성 규칙은 예비로 유지된다 (battle.ts).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Battle, mulberry32, type BattleConfig } from '../../core/src/index.ts';
import { loadAll } from '../src/data.ts';

const data = loadAll();

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

/** 적 최대 의지는 밸런스 수치 — 규칙 검증이 그 값에 묶이지 않도록 YAML 에서 읽는다 */
const eWill = (id: string): number => data.enemies.get(id)!.will;

// ── E04 은신 게이트 — 크리티컬 리뷰 (§3.8 특성 문언 "리뷰만 명중") ────

test('E04: 은신 중 비배송 크리(팩트 폭격기)는 빗나감 — 게이지만 소모', () => {
  const b = makeBattle('E04', []); // 초기 성향 = 팩트 폭격기 (품질)
  b.endTurn(); // hide → 은신
  assert.equal(b.state.enemy.stealth, true);
  b.state.player.gauge = 10;
  b.useCritical();
  assert.equal(b.state.enemy.will, eWill('E04')); // 고정 20 미적용 (즉사 우회 봉쇄)
  assert.equal(b.state.player.gauge, 0); // 자원 소모는 유지 (빗나간 리뷰와 일관)
  assert.equal(b.state.stats.critMisses, 1);
});

test('E04: 은신 중 배송 크리(프로 불편러)는 명중 + 은신 해제', () => {
  const b = makeBattle('E04', [], { initialSuitCounters: { 배송: 1 } });
  assert.equal(b.state.player.disposition, '프로 불편러');
  b.endTurn(); // hide → 은신
  b.state.player.gauge = 10;
  b.useCritical();
  assert.equal(b.state.enemy.stealth, false); // breakOnHit
  assert.equal(b.state.enemy.stunTurns, 1);
  assert.equal(b.state.player.gold, 15); // 정예 15G (§3.5)
  assert.equal(b.state.stats.critMisses, 0);
});

// ── §2-1 "모든 배율은 내림, 최소 1" — 적 피해 배율 경로 ───────────────

test('§2-1: 적 피해도 배율 결과는 최소 1 (감산 하한 0은 유지)', () => {
  // E01 stab 5, attack_down 4 → 감산 후 1, 힙스터 ×0.5 → floor(0.5)=0이 아니라 최소 1
  const b = makeBattle('E01', []);
  b.state.enemy.debuffs.push(
    { uid: 900, kind: 'attack_down', value: 4, suit: '배송', tier: 1, suspended: false, beenRebutted: false, createdAt: 900 },
    { uid: 901, kind: 'attack_halve', value: 50, suit: '성능', tier: 3, suspended: false, beenRebutted: false, createdAt: 901 },
  );
  b.endTurn(); // stab: max(0, 5−4)=1 → applyMult(1, 0.5)=1
  assert.equal(b.state.player.will, 29);

  // 감산만으로 0이면 배율 없이 0 유지 (감산은 배율이 아님 — 가정)
  const b2 = makeBattle('E01', []);
  b2.state.enemy.debuffs.push({ uid: 902, kind: 'attack_down', value: 9, suit: '배송', tier: 1, suspended: false, beenRebutted: false, createdAt: 902 });
  b2.endTurn();
  assert.equal(b2.state.player.will, 30);
});

// ── 온보딩 보정 (§3.3 버프 무판정, §4.4 적 공격 ×0.75·헛소리 −1) ─────

test('§4.4: 온보딩 1판 보정 — 헛소리 −1, 버프 카드 무판정, 적 공격 ×0.75', () => {
  // 시작 장비 태그로는 v2 버프 카드(#속도·#디자인)가 팩트일 수 없어 검증용 장비를 주입
  const b = makeBattle('E01', ['Z01', 'D03'], {
    startGauge: 3,
    playerEquipment: [{ name: '중고 러닝화', tags: ['속도'], nullTags: [] }],
    onboarding: { enemyDamageMult: 0.75, fumbleGaugeDelta: -1, buffNoJudgement: true },
  });
  b.submitReview(uid(b, 'Z01')); // #연비 → 헛소리
  assert.equal(b.state.player.gauge, 2); // −2 대신 −1
  assert.equal(b.state.enemy.will, eWill('E01') - 2); // 피해 규칙은 정상: max(1, floor(5×0.5)) = 2
  b.submitReview(uid(b, 'D03'), { myEquipmentIndex: 0 }); // 러닝화[속도] — 팩트감이지만 무판정
  assert.equal(b.state.player.equipment[0]!.attachments[0]!.value, 2); // ×1.5 미적용 (항상 일반 — floor(3×0.9))
  assert.equal(b.state.player.gauge, 2); // 게이지 변화 없음
  b.endTurn(); // stab 5 → applyMult(5, 0.75) = 3
  assert.equal(b.state.player.will, 27);
});

// ── B01 사장님 답글 cooldown 하한 (가정: "3턴마다 발동" 강제) ─────────

test('B01: owner_reply는 마지막 발동 후 3턴 전 재도래 시 불발 (패턴 앞당김 방어)', () => {
  const b = makeBattle('B01', ['B01c']);
  b.submitReview(uid(b, 'B01c')); // 원산지 floor(9×1.5)+1=14
  const afterReply = eWill('B01') - 14 + 5;
  b.state.enemy.patternIndex = 2;
  b.state.enemy.intentId = 'owner_reply';
  b.endTurn(); // 턴1 발동: 반박 대상 없음 → 의지 +5
  assert.equal(b.state.enemy.will, afterReply);
  b.state.enemy.patternIndex = 2;
  b.state.enemy.intentId = 'owner_reply';
  b.endTurn(); // 턴2: 2−1=1 < 3 → 재사용 대기 (불발)
  assert.equal(b.state.enemy.will, afterReply);
  b.state.enemy.patternIndex = 2;
  b.state.enemy.intentId = 'owner_reply';
  b.endTurn(); // 턴3: 3−1=2 < 3 → 불발
  assert.equal(b.state.enemy.will, afterReply);
  b.state.enemy.patternIndex = 2;
  b.state.enemy.intentId = 'owner_reply';
  b.endTurn(); // 턴4: 4−1=3 ≥ 3 → 발동 (+5)
  assert.equal(b.state.enemy.will, afterReply + 5);
});

// ── 시뮬 통계 정확성 ─────────────────────────────────────────────────

test('timeout 시 기록 턴은 maxTurns로 클램프 (off-by-one 방지)', () => {
  const b = makeBattle('E01', [], { maxTurns: 3 });
  b.endTurn();
  b.endTurn();
  b.endTurn();
  assert.equal(b.state.result, 'timeout');
  assert.equal(b.state.turn, 3);
});

test('§2-2 계측: 게이지 10 도달(크리 가능)·초과 소실이 stats에 기록된다', () => {
  const b = makeBattle('E01', ['Z06'], { startGauge: 9 });
  b.submitReview(uid(b, 'Z06')); // 팩트 +3 → 9+3=12 → 10 (2 소실)
  assert.equal(b.state.player.gauge, 10);
  assert.equal(b.state.stats.gaugeReached10, 1);
  assert.equal(b.state.stats.gaugeOverflowLost, 2);
});
