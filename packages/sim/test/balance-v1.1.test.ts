// 밸런스 라운드 1 (GDD v1.1) 신규 규칙 검증 — 제안 1·3·5·6 + 퇴고
// v2 전환 노트: 카드 참조만 v2(단일 리뷰 카드)로 교체. 규칙 자체는 판정 하류라 무변경.
// 퇴고는 v1의 "교착 안전장치"에서 v2의 "태그 사냥 도구"로 역할이 승격됐다 (card-system-v2 §7).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Battle, DEFAULT_RULES, mulberry32, type BattleConfig } from '../../core/src/index.ts';
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

test('v1.1 제안 1: 팩트 판정 게이지 +3', () => {
  const b = makeBattle('E01', ['Z06']);
  b.submitReview(uid(b, 'Z06')); // #마감 ∈ E01 약점 → 팩트
  assert.equal(b.state.player.gauge, 3);
});

// 밸런스 라운드 1(v2) 확정 수치 잠금 — 여기가 "의지 수치 자체"를 검증하는 유일한 자리다.
// 규칙 테스트(battle.test.ts)는 이 값을 YAML 에서 읽으므로, 수치가 바뀌면 이 테스트만 깨진다.
// 근거: design/balance-report-v2-round1.md (일반 2~4턴 / 정예 5~6턴 / 보스 6~7턴, GDD §3.1)
test('밸런스 v2-r1: 적 의지 확정치 (E01 30 / E02 55 / E03 65 / E04 55 / E05 70 / B01 78)', () => {
  assert.equal(data.enemies.get('E01')!.will, 30);
  assert.equal(data.enemies.get('E02')!.will, 55);
  assert.equal(data.enemies.get('E03')!.will, 65);
  assert.equal(data.enemies.get('E04')!.will, 55);
  assert.equal(data.enemies.get('E05')!.will, 70);
  assert.equal(data.enemies.get('B01')!.will, 78);
});

// 판정 배율 — 우위 판정(원산지·팩트)과 일반 판정의 간격이 밸런스의 중심축이다 (card-system-v2 §2)
test('밸런스 v2-r1: 판정 배율 (원산지·팩트 ×1.5 / 일반 ×0.9 / 헛소리 ×0.5)', () => {
  assert.equal(DEFAULT_RULES.judge.mult.origin, 1.5);
  assert.equal(DEFAULT_RULES.judge.mult.fact, 1.5);
  assert.equal(DEFAULT_RULES.judge.mult.normal, 0.9);
  assert.equal(DEFAULT_RULES.judge.mult.fumble, 0.5);
});

test('v1.1 제안 3: 페이즈2 비례 트리거 — 의지 48이면 50% = 24에서 발동', () => {
  const base = data.enemies.get('B01')!;
  assert.equal(base.phase2!.triggerPct, 50); // YAML "의지 50% 이하" 파싱
  const enemy = { ...base, will: 48 };
  const b = makeBattle('B01', [], { enemy } as Partial<BattleConfig>);
  // 의지를 25까지 깎아도 미발동 (문턱 = floor(48×50%) = 24)
  b.state.enemy.will = 25;
  b.endTurn();
  assert.equal(b.state.enemy.phase2Done, false);
  // 24 이하로 깎이면 발동 (직전 턴 답글 회복 +5 가능성 배제 위해 직접 설정)
  b.state.enemy.will = 24;
  b.endTurn();
  assert.equal(b.state.enemy.phase2Done, true);
});

test('v1.1 제안 5: 「바이럴 확산」 바닥 보장 — 버프 0개면 +3 가산 버프 부착 (+12 상한 공유)', () => {
  const b = makeBattle('E01', [], { initialSuitCounters: { 감성: 5 } });
  assert.equal(b.state.player.disposition, '감성 논점');
  b.state.player.gauge = 10;
  b.useCritical();
  const buffs = b.state.player.equipment.flatMap((eq) => eq.attachments.filter((a) => a.kind === 'damage_buff'));
  assert.equal(buffs.length, 1);
  assert.equal(buffs[0]!.value, 3); // 기본 버프 상당
  assert.equal(buffs[0]!.usesSlot, false); // 크리 산출물 — 부착 슬롯 미점유
  assert.equal(b.state.player.viralBonusGranted, 3); // +12 상한 공유
});

test('v1.1 제안 6: 「진상 접수」 — 기절 + 다음 행동 위력 −50% (기절 면역 시에도 −50%는 적용)', () => {
  const b = makeBattle('E01', [], { initialSuitCounters: { 배송: 5 } });
  assert.equal(b.state.player.disposition, '배송 논점');
  // 경직 내성 상태를 만들어 기절이 무효인 상황에서 −50%가 남는지 확인
  b.state.enemy.staggerImmunityTurns = 1;
  b.state.player.gauge = 10;
  b.useCritical();
  assert.equal(b.state.enemy.stunTurns, 0); // 기절 무효 (경직 내성)
  assert.equal(b.state.enemy.weakenNextActionPct, -50); // 위력 감소는 적용
  b.endTurn(); // E01 stab 5 → floor(5×0.5)=2
  assert.equal(b.state.player.will, 28);
});

test('v2 퇴고: 필력 1로 손패 1장 교체 — 태그 사냥 (card-system-v2 §7)', () => {
  const b = makeBattle('E01', ['Z01', 'Z02', 'Z04', 'Z05', 'Z06'], { deck: ['G01'] });
  // E01전에서 헛소리(Z01 #연비)를 버리고 원하는 태그를 찾는 용도
  const before = b.state.player.energy;
  b.revise(uid(b, 'Z01'));
  assert.equal(b.state.player.energy, before - 1);
  assert.ok(b.state.player.hand.some((c) => c.cardId === 'G01'));
  assert.equal(b.state.player.hand.length, 5);
  // 뽑을 카드가 없으면 사용 불가
  const empty = makeBattle('E01', ['Z01', 'Z02', 'Z04', 'Z05', 'Z06']);
  assert.throws(() => empty.revise(uid(empty, 'Z01')), /뽑을 카드 없음/);
});
