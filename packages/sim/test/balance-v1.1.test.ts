// 밸런스 라운드 1 (GDD v1.1) 신규 규칙 검증 — 제안 1·3·5·6 + 퇴고(교착 안전장치)
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

test('v1.1 제안 1: 팩트 판정 게이지 +3', () => {
  const b = makeBattle('E01', ['P01', 'S01']);
  b.submitReview(uid(b, 'P01'), uid(b, 'S01')); // 마감 ∈ E01 약점 → 팩트
  assert.equal(b.state.player.gauge, 3);
});

test('v1.1 제안 2: 정예 의지 상향 (E02 30 / E03 24 / E04 22 / E05 28)', () => {
  assert.equal(data.enemies.get('E02')!.will, 30);
  assert.equal(data.enemies.get('E03')!.will, 24);
  assert.equal(data.enemies.get('E04')!.will, 22);
  assert.equal(data.enemies.get('E05')!.will, 28);
});

test('v1.1 제안 3: 페이즈2 비례 트리거 — 의지 48이면 50% = 24에서 발동', () => {
  const base = data.enemies.get('B01')!;
  assert.equal(base.phase2!.triggerPct, 50); // YAML "의지 50% 이하" 파싱
  const enemy = { ...base, will: 48 };
  const b = makeBattle('B01', ['P01', 'S01'], { enemy } as Partial<BattleConfig>);
  // 의지를 25까지 깎아도 미발동 (문턱 = floor(48×50%) = 24)
  b.state.enemy.will = 25;
  b.endTurn();
  assert.equal(b.state.enemy.phase2Done, false);
  // 24 이하로 깎이면 발동 (직전 턴 답글 회복 +5 가능성 배제 위해 직접 설정)
  b.state.enemy.will = 24;
  b.endTurn();
  assert.equal(b.state.enemy.phase2Done, true);
});

test('v1.1 제안 5: 바이럴 크리 바닥 보장 — 버프 0개면 +3 가산 버프 부착 (+12 상한 공유)', () => {
  const b = makeBattle('E01', ['P13', 'S01'], { initialSuitCounters: { 감성: 5 } });
  assert.equal(b.state.player.disposition, '바이럴 앞잡이');
  b.state.player.gauge = 10;
  b.useCritical();
  const buffs = b.state.player.equipment.flatMap((eq) => eq.attachments.filter((a) => a.kind === 'damage_buff'));
  assert.equal(buffs.length, 1);
  assert.equal(buffs[0]!.value, 3); // S13 상당
  assert.equal(buffs[0]!.usesSlot, false); // 크리 산출물 — 부착 슬롯 미점유
  assert.equal(b.state.player.viralBonusGranted, 3); // +12 상한 공유
});

test('v1.1 제안 6: 불편러 크리 — 기절 + 다음 행동 위력 −50% (기절 면역 시에도 −50%는 적용)', () => {
  const b = makeBattle('E01', ['P09', 'S01'], { initialSuitCounters: { 배송: 5 } });
  assert.equal(b.state.player.disposition, '프로 불편러');
  // 경직 내성 상태를 만들어 기절이 무효인 상황에서 −50%가 남는지 확인
  b.state.enemy.staggerImmunityTurns = 1;
  b.state.player.gauge = 10;
  b.useCritical();
  assert.equal(b.state.enemy.stunTurns, 0); // 기절 무효 (경직 내성)
  assert.equal(b.state.enemy.weakenNextActionPct, -50); // 위력 감소는 적용
  b.endTurn(); // E01 stab 5 → floor(5×0.5)=2
  assert.equal(b.state.player.will, 28);
});

test('v1.1 퇴고: 필력 1로 손패 1장 교체 — 접두 고착 해소 (GDD §3.2)', () => {
  const b = makeBattle('E01', ['S01', 'S03', 'S05', 'S13', 'S14'], { deck: ['P01'] });
  // 손패에 접두 0장 — 구판이면 교착. 퇴고로 S14를 버리고 P01 드로우
  const before = b.state.player.energy;
  b.revise(uid(b, 'S14'));
  assert.equal(b.state.player.energy, before - 1);
  assert.ok(b.state.player.hand.some((c) => c.cardId === 'P01'));
  assert.equal(b.state.player.hand.length, 5);
  // 뽑을 카드가 없으면 사용 불가
  const empty = makeBattle('E01', ['S01', 'S03', 'S05', 'S13', 'S14']);
  assert.throws(() => empty.revise(uid(empty, 'S01')), /뽑을 카드 없음/);
});
