// 감사 지적 반영분 검증 (2026-07-31 감사 — S07 락, 은신 크리 게이트, 적 피해 배율 §2-1,
// 온보딩 보정, cooldown 하한, timeout 클램프, 게이지 도달/초과 소실 계측)
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

// ── S07 무한 락 봉쇄 (가정: §3.2 악용 #3 봉쇄 의도 준용) ──────────────

test('S07: 봉인 불발 후 경직 내성 — 매턴 재시전 락 불가 (S09 락과 동형 봉쇄)', () => {
  const b = makeBattle('E01', ['P01', 'S07', 'P01', 'S07']);
  b.submitReview(uid(b, 'P01'), uid(b, 'S07')); // 장비 1개 비활성화 → 전 장비 비활성
  b.endTurn(); // stab 봉인 (피해 0) + 경직 내성 부여
  assert.equal(b.state.player.will, 30);
  assert.equal(b.state.enemy.staggerImmunityTurns, 1); // 정리에서 1 감소 → 1턴 유지
  b.submitReview(uid(b, 'P01'), uid(b, 'S07')); // 경직 내성 중 재시전 → 무효
  assert.equal(b.state.enemy.equipment[0]!.disabledTurns, 0);
  b.endTurn(); // 행동 정상 실행 (stab 5) — 락 성립 불가
  assert.equal(b.state.player.will, 25);
});

// ── E04 은신 게이트 — 크리티컬 리뷰 (§3.8 특성 문언 "리뷰만 명중") ────

test('E04: 은신 중 비배송 크리(팩트 폭격기)는 빗나감 — 게이지만 소모', () => {
  const b = makeBattle('E04', []); // 초기 성향 = 팩트 폭격기 (품질)
  b.endTurn(); // hide → 은신
  assert.equal(b.state.enemy.stealth, true);
  b.state.player.gauge = 10;
  b.useCritical();
  assert.equal(b.state.enemy.will, 22); // 고정 20 미적용 (즉사 우회 봉쇄) — E04 의지 22 (v1.1)
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

test('§4.4: 온보딩 1판 보정 — 헛소리 −1, 버프 접미 무판정, 적 공격 ×0.75', () => {
  const b = makeBattle('E01', ['P07', 'S01', 'P01', 'S13'], {
    startGauge: 3,
    onboarding: { enemyDamageMult: 0.75, fumbleGaugeDelta: -1, buffNoJudgement: true },
  });
  b.submitReview(uid(b, 'P07'), uid(b, 'S01')); // 연비 → 헛소리
  assert.equal(b.state.player.gauge, 2); // −2 대신 −1
  assert.equal(b.state.enemy.will, 11); // 피해 규칙은 정상: max(1, floor(6×0.5)) = 3
  b.submitReview(uid(b, 'P01'), uid(b, 'S13'), { myEquipmentIndex: 0 }); // 롱소드[마감] — 팩트감이지만 무판정
  assert.equal(b.state.player.equipment[0]!.attachments[0]!.value, 2); // ×1.5 미적용 (항상 일반)
  assert.equal(b.state.player.gauge, 2); // 게이지 변화 없음
  b.endTurn(); // stab 5 → applyMult(5, 0.75) = 3
  assert.equal(b.state.player.will, 27);
});

// ── B01 사장님 답글 cooldown 하한 (가정: "3턴마다 발동" 강제) ─────────

test('B01: owner_reply는 마지막 발동 후 3턴 전 재도래 시 불발 (패턴 앞당김 방어)', () => {
  const b = makeBattle('B01', ['P11', 'S01']);
  b.submitReview(uid(b, 'P11'), uid(b, 'S01')); // 응대 팩트 9 → 51
  b.state.enemy.patternIndex = 2;
  b.state.enemy.intentId = 'owner_reply';
  b.endTurn(); // 턴1 발동: 반박 대상 없음 → 의지 +5 = 56
  assert.equal(b.state.enemy.will, 56);
  b.state.enemy.patternIndex = 2;
  b.state.enemy.intentId = 'owner_reply';
  b.endTurn(); // 턴2: 2−1=1 < 3 → 재사용 대기 (불발)
  assert.equal(b.state.enemy.will, 56);
  b.state.enemy.patternIndex = 2;
  b.state.enemy.intentId = 'owner_reply';
  b.endTurn(); // 턴3: 3−1=2 < 3 → 불발
  assert.equal(b.state.enemy.will, 56);
  b.state.enemy.patternIndex = 2;
  b.state.enemy.intentId = 'owner_reply';
  b.endTurn(); // 턴4: 4−1=3 ≥ 3 → 발동 (+5, 상한 60)
  assert.equal(b.state.enemy.will, 60);
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
  const b = makeBattle('E01', ['P01', 'S01'], { startGauge: 9 });
  b.submitReview(uid(b, 'P01'), uid(b, 'S01')); // 팩트 +3(v1.1) → 9+3=12 → 10 (2 소실)
  assert.equal(b.state.player.gauge, 10);
  assert.equal(b.state.stats.gaugeReached10, 1);
  assert.equal(b.state.stats.gaugeOverflowLost, 2);
});
