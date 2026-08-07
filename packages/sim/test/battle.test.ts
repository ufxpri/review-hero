// 전투 상태머신 규칙 검증 v2 (GDD §2·§3 + card-system-v2 판정 4단계 + 악용 검증 엣지)
// 실데이터(cards-v2.0.yaml / enemies-v1.0.yaml)를 로드해 core를 구동한다.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { Battle, mulberry32, type BattleConfig } from '../../core/src/index.ts';
import { loadAll, loadCards } from '../src/data.ts';

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

// ── §2 공통 계산 + v2 판정 4단계 ──────────────────────

test('v2 판정: 팩트 ×1.5 내림, 게이지 +3', () => {
  const b = makeBattle('E01', ['Z06']); // Z06 #마감 👍4 — E01 약점 [마감]
  const r = b.submitReview(uid(b, 'Z06'));
  assert.equal(r.judgement, 'fact');
  assert.equal(b.state.enemy.will, 14 - 6); // floor(4×1.5)=6
  assert.equal(b.state.player.gauge, 3);
  assert.equal(b.state.stats.judgements.fact, 1);
});

test('v2 판정: 원산지 ×1.5 + 고정 +1(내림 후·배율 비대상), 게이지 +4', () => {
  const b = makeBattle('E01', ['Q01']); // Q01 origin {E01, 짝퉁 단검} 👍7 — 적 본체 대상
  const r = b.submitReview(uid(b, 'Q01'));
  assert.equal(r.judgement, 'origin');
  // floor(7×1.5)+1 = 11. 고정 가산이 배율을 받으면 floor((7+1)×1.5)=12가 되므로 구분됨 (GDD §2)
  assert.equal(b.state.enemy.will, 14 - 11);
  assert.equal(b.state.player.gauge, 4);
  assert.equal(b.state.stats.judgements.origin, 1);
});

test('v2 판정: 헛소리 ×0.5(최소 1), 게이지 −2', () => {
  const b = makeBattle('E01', ['Z01'], { startGauge: 3 }); // Z01 #연비 👍5 — E01 무효 [연비,이펙트]
  const r = b.submitReview(uid(b, 'Z01'));
  assert.equal(r.judgement, 'fumble');
  assert.equal(b.state.enemy.will, 14 - 2); // floor(5×0.5)=2
  assert.equal(b.state.player.gauge, 1);
});

test('v2 원산지: 무효 태그 무시 (같은 태그 비원산지 카드는 헛소리)', () => {
  // 실데이터엔 "origin 태그 = 그 적 무효 태그" 조합이 없어 E02 무효 태그에 무게를 얹어 검증
  const enemyMod = { ...data.enemies.get('E02')!, nullTags: ['무게', '이펙트', '개연성'] };
  const b = makeBattle('E02', ['W01', 'A03'], { enemy: enemyMod });
  const r1 = b.submitReview(uid(b, 'W01')); // W01 origin E02 #무게 👍7 — 무효 태그여도 원산지
  assert.equal(r1.judgement, 'origin');
  assert.equal(b.state.enemy.will, 30 - 11); // floor(7×1.5)+1
  const r2 = b.submitReview(uid(b, 'A03')); // A03 origin E05 #무게 👍6 — 원산지 아님 → 헛소리
  assert.equal(r2.judgement, 'fumble');
  assert.equal(b.state.enemy.will, 30 - 11 - 3); // floor(6×0.5)=3
});

test('§2-2: 게이지 상한 10 초과 소실 / 하한 0', () => {
  const hi = makeBattle('E01', ['Z06'], { startGauge: 9 });
  hi.submitReview(uid(hi, 'Z06')); // 팩트 +3
  assert.equal(hi.state.player.gauge, 10); // 9+3=12 → 10 (초과 소실)

  const lo = makeBattle('E01', ['Z01'], { startGauge: 1 });
  lo.submitReview(uid(lo, 'Z01')); // 헛소리 −2
  assert.equal(lo.state.player.gauge, 0); // 1−2 → 0 (하한)
});

// ── v2 원산지 판정 범위 ───────────────────────────────

test('v2 원산지 범위: 적 본체 대상 → origin.enemy 일치 (다른 적에겐 일반)', () => {
  const vs02 = makeBattle('E02', ['W01']);
  assert.equal(vs02.submitReview(uid(vs02, 'W01')).judgement, 'origin');
  const vs01 = makeBattle('E01', ['W01']); // #무게 — E01 약점/무효 아님
  assert.equal(vs01.submitReview(uid(vs01, 'W01')).judgement, 'normal');
  assert.equal(vs01.state.enemy.will, 14 - 7); // ×1.0
});

test('v2 원산지 범위: 구성품 대상 → origin.equipment 이름 완전 일치', () => {
  // E04 구성품: [삐걱거리는 쌍단검, 삐걱거리는 쌍단검 (한 짝)] — C03c origin은 전자만
  const b = makeBattle('E04', ['C03c']);
  const r = b.submitReview(uid(b, 'C03c'), { enemyEquipmentIndex: 0 });
  assert.equal(r.judgement, 'origin');
  assert.equal(b.state.enemy.equipment[0]!.destroyed, true); // floor(7×1.5)+1=11 ≥ 내구도 5
  assert.equal(b.state.player.gauge, 4);

  const b2 = makeBattle('E04', ['C03c']);
  const r2 = b2.submitReview(uid(b2, 'C03c'), { enemyEquipmentIndex: 1 }); // "(한 짝)" — 이름 불일치
  assert.equal(r2.judgement, 'normal'); // #내구도 ∉ [속도,마감], 무효 [감성,디자인] 아님
  assert.equal(b2.state.enemy.equipment[1]!.destroyed, true); // 7 ≥ 5
  assert.equal(b2.state.player.gauge, 0);
});

test('v2: 전생 카드(Z01~Z12)는 origin 없음 — 원산지 영구 미발동', () => {
  for (let i = 1; i <= 12; i++) {
    const id = `Z${String(i).padStart(2, '0')}`;
    const d = data.cards.byId.get(id)!;
    assert.equal(d.kind, 'review');
    assert.equal(d.kind === 'review' && d.origin, undefined, `${id}에 origin이 있으면 안 됨`);
  }
  const b = makeBattle('E04', ['Z07']); // Z07 #속도 = E04 약점 — 팩트까지만 (원산지 불가)
  assert.equal(b.submitReview(uid(b, 'Z07')).judgement, 'fact');
  assert.equal(b.state.enemy.will, 22 - 7); // floor(5×1.5)
});

test('v2: 단일 카드 제출 — 특수 카드는 submitReview 불가, 리뷰 카드는 playSpecial 불가', () => {
  const b = makeBattle('E01', ['X01', 'Z01']);
  assert.throws(() => b.submitReview(uid(b, 'X01')), /리뷰 카드가 아님/);
  assert.throws(() => b.playSpecial(uid(b, 'Z01')), /특수 카드가 아님/);
});

test('v2 로드 검증: tag 배열이면 로드 에러 (단일 초점 원칙)', () => {
  const here = dirname(fileURLToPath(import.meta.url));
  assert.throws(() => loadCards(join(here, 'fixtures', 'bad-tag.yaml')), /배열 금지/);
});

// ── §3.2 턴 시퀀스·기절·리액션·지연 ──────────────────

test('§3.2: 기절 시 attack 불발, gimmick은 기절 무시 발동', () => {
  const b = makeBattle('B01', ['B01c', 'W03']);
  b.submitReview(uid(b, 'B01c')); // 원산지: floor(9×1.5)+1=14 → 46
  assert.equal(b.state.enemy.will, 46);
  b.endTurn(); // contract_slash 8
  assert.equal(b.state.player.will, 22);
  b.submitReview(uid(b, 'W03')); // 기절 1턴 (판정 일반 — #무게)
  assert.equal(b.state.enemy.stunTurns, 1);
  b.state.enemy.patternIndex = 2;
  b.state.enemy.intentId = 'owner_reply'; // 기믹 강제
  b.endTurn();
  assert.equal(b.state.enemy.will, 51); // 기절 무시 발동: 반박 대상 없음 → 의지 +5
  assert.equal(b.state.enemy.stunTurns, 0);
  assert.equal(b.state.enemy.staggerImmunityTurns, 1); // 기상 → 경직 내성 1턴
});

test('§3.2: 경직 내성 중 지연(X01) 면역 — 연쇄 락 불가', () => {
  const b = makeBattle('E01', ['W03', 'X01']);
  b.submitReview(uid(b, 'W03')); // 기절
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

test('§3.2: X01 지연 — 적 행동 스킵(인텐트 유지), v2 X01은 게이지 비용 없음', () => {
  const b = makeBattle('E01', ['X01'], { startGauge: 5 });
  b.playSpecial(uid(b, 'X01'));
  assert.equal(b.state.player.gauge, 5); // v1 X01의 인라인 게이지 −1은 v2 YAML에서 사라짐
  b.endTurn();
  assert.equal(b.state.player.will, 30); // stab 스킵
  assert.equal(b.state.enemy.intentId, 'stab'); // 인텐트 유지
  b.endTurn();
  assert.equal(b.state.player.will, 25); // 다음 턴 정상 실행
});

test('E02: 준비(charge) 중 지연 적중 시 내려찍기 캔슬 (cancel_on: delay_enemy_action)', () => {
  const b = makeBattle('E02', ['X01', 'Z01', 'Z02', 'Z03', 'Z04'], { deck: ['Z05', 'Z06'] });
  b.endTurn(); // roar (공격력 +2)
  assert.equal(b.state.enemy.intentId, 'smash');
  b.endTurn(); // smash 준비 시작
  assert.ok(b.state.enemy.charging);
  b.playSpecial(uid(b, 'X01')); // 준비 중 지연 → 캔슬
  assert.equal(b.state.enemy.charging, null);
  assert.equal(b.state.enemy.intentId, 'roar'); // 행동 소멸, 패턴 진행
  assert.equal(b.state.player.will, 30); // 9딜 무효
});

test('E02: 리뷰 카드의 delay_enemy_action(W02)도 준비 캔슬 + 원산지 피해 동반', () => {
  const b = makeBattle('E02', ['W02', 'Z01', 'Z02', 'Z03', 'Z04'], { deck: ['Z05', 'Z06'] });
  b.endTurn(); // roar
  b.endTurn(); // smash 준비
  assert.ok(b.state.enemy.charging);
  const r = b.submitReview(uid(b, 'W02')); // origin E02: 동반 피해 floor(5×1.5)+1=8
  assert.equal(r.judgement, 'origin');
  assert.equal(b.state.enemy.will, 30 - 8);
  assert.equal(b.state.enemy.charging, null); // 캔슬
  assert.equal(b.state.enemy.intentId, 'roar');
});

test('E03: casting_weakness — 영창 중 #이펙트 리뷰 효과 ×2 (적 특성으로 이관)', () => {
  const b = makeBattle('E03', ['L03']);
  b.endTurn(); // fireball 영창 시작
  assert.ok(b.state.enemy.charging);
  const r = b.submitReview(uid(b, 'L03')); // origin E03 · #이펙트 · 👍6 + 기절 1
  assert.equal(r.judgement, 'origin');
  assert.equal(b.state.enemy.will, 24 - 19); // floor(6×1.5×2)=18, +1 = 19
  assert.equal(b.state.enemy.stunTurns, 1);
});

// ── §3.3 버프 부착 ────────────────────────────────────

test('§3.3: damage_buff 부착(슬롯 점유) — 가산은 기본 좋아요에 합산 후 판정 배율', () => {
  const b = makeBattle('E01', ['D03', 'Z06']);
  b.submitReview(uid(b, 'D03'), { myEquipmentIndex: 0 }); // 롱소드[마감] vs #속도 → 일반 → 부착 +3
  const att = b.state.player.equipment[0]!.attachments[0]!;
  assert.equal(att.value, 3);
  assert.equal(att.usesSlot, true); // v2: 리뷰 유래 부착은 슬롯 사용 (GDD §3.9)
  b.submitReview(uid(b, 'Z06')); // 팩트: (4+3)×1.5 → floor(10.5)=10
  assert.equal(b.state.enemy.will, 14 - 10);
});

test('§2(GDD): X05 예약 가산은 고정 가산 — 내림 후 더한다', () => {
  const b = makeBattle('E01', ['X05', 'Z06']);
  b.playSpecial(uid(b, 'X05'));
  b.endTurn(); // stab 5 피격 → 예약 확정 5
  assert.equal(b.state.player.will, 25);
  b.submitReview(uid(b, 'Z06')); // 팩트 floor(4×1.5)=6, +5(고정) = 11
  assert.equal(b.state.enemy.will, 14 - 11);
  assert.equal(b.state.player.storedDamageBonus, 0); // 소진
});

// ── §3.5 크리티컬 ─────────────────────────────────────

test('§3.5: 크리티컬은 턴당 1회, 게이지 전량 소모', () => {
  const b = makeBattle('B01', []);
  b.state.player.gauge = 10;
  b.useCritical(); // 팩트 폭격기 (초기값) — 고정 20
  assert.equal(b.state.enemy.will, 40);
  assert.equal(b.state.player.gauge, 0);
  b.state.player.gauge = 10;
  assert.throws(() => b.useCritical(), /턴당 1회/);
});

test('§3.5: 바이럴 앞잡이 — 버프 2배 가산, 크리 간 공유 상한 +12', () => {
  const b = makeBattle('B01', ['D03'], { initialSuitCounters: { 감성: 2 } });
  assert.equal(b.state.player.disposition, '바이럴 앞잡이'); // 스냅샷 (argmax)
  b.submitReview(uid(b, 'D03'), { myEquipmentIndex: 0 }); // 부착 +3 (일반)
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
  const b = makeBattle('E01', ['X04', 'G01']);
  b.playSpecial(uid(b, 'X04'), { giftUid: uid(b, 'G01') });
  assert.equal(b.state.enemy.will, 10); // 1코 ×4 = 4
  assert.equal(b.state.player.removedFromRun.length, 1);
  assert.equal(b.state.player.removedFromRun[0]!.cardId, 'G01');
  assert.ok(!b.state.player.discard.some((c) => c.cardId === 'G01')); // 묘지 순환에서 제외
  const b2 = makeBattle('E01', ['X04', 'X03']);
  b2.playSpecial(uid(b2, 'X04'), { giftUid: uid(b2, 'X03') });
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

test('E04: 은신 중 배송 계열만 명중(카드 suit 기준), 명중 시 은신 해제 → 기습 12→6', () => {
  const b = makeBattle('E04', ['Z06', 'D01']);
  b.endTurn(); // hide → 은신
  assert.equal(b.state.enemy.stealth, true);
  const miss = b.submitReview(uid(b, 'Z06')); // 품질 계열 → 빗나감
  assert.equal(miss.missed, true);
  assert.equal(b.state.enemy.will, 22);
  const j = b.state.stats.judgements;
  assert.equal(j.origin + j.fact + j.normal + j.fumble, 0); // 빗나간 리뷰는 무판정
  const hit = b.submitReview(uid(b, 'D01')); // 배송(D01, origin E04) → 명중 + 은신 해제
  assert.equal(hit.judgement, 'origin');
  assert.equal(b.state.enemy.stealth, false);
  assert.equal(b.state.enemy.will, 22 - 10); // floor(6×1.5)+1 = 10
  b.endTurn(); // ambush: 은신 해제 상태 → 6
  assert.equal(b.state.player.will, 24);
});

test('E05: 감성 계열 의지 데미지 ×2(vanity) + 찬양 강요 게이지 −1(하한 0)', () => {
  const b = makeBattle('E05', ['Z05'], { startGauge: 5 });
  b.submitReview(uid(b, 'Z05')); // #감성 팩트 → floor(4×1.5×2)=12
  assert.equal(b.state.enemy.will, 28 - 12);
  assert.equal(b.state.player.gauge, 8); // 5 + 팩트 +3
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

test('B01: 사장님 답글 — 정지 → 같은 계열 원산지/팩트로 재반박(부활+게이지+1) → 재정지 불가', () => {
  const b = makeBattle('B01', ['K01', 'B01c']);
  b.submitReview(uid(b, 'K01')); // 원산지(B01) → 공격력 −4 디버프(배송 계열): floor(3×1.5)=4
  const debuff = b.state.enemy.debuffs[0]!;
  assert.equal(debuff.value, 4);
  b.state.enemy.patternIndex = 2;
  b.state.enemy.intentId = 'owner_reply';
  b.endTurn();
  assert.equal(debuff.suspended, true); // 이번 전투 한정 정지
  const g = b.state.player.gauge;
  b.submitReview(uid(b, 'B01c')); // 같은 계열(배송) 원산지 → 재반박 (원산지는 팩트의 상위 판정 — 해석)
  assert.equal(debuff.suspended, false); // 부활
  assert.equal(b.state.player.gauge, g + 4 + 1); // 원산지 +4, 재반박 성공 +1 (§3.4)
  const will = b.state.enemy.will;
  b.state.enemy.patternIndex = 2;
  b.state.enemy.intentId = 'owner_reply';
  b.state.enemy.cooldownLastFired['owner_reply'] = -10; // cooldown 3 경과 가정
  b.endTurn();
  assert.equal(debuff.suspended, false); // 디버프당 반박 1회 소진 → 재정지 불가
  assert.equal(b.state.enemy.will, Math.min(60, will + 5)); // 대상 없음 → 의지 +5만
});

test('B01: 반박 우선순위 — 힙스터 크리(Tier 3)를 일반 디버프(Tier 1)보다 먼저 정지', () => {
  const b = makeBattle('B01', ['K01'], { initialSuitCounters: { 성능: 1 } });
  assert.equal(b.state.player.disposition, '힙스터 평론가');
  b.submitReview(uid(b, 'K01')); // Tier 1 attack_down 4 (원산지 강화)
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
  assert.equal(b.state.player.will, 30 - (8 - 4)); // 힙스터 정지 → −50% 미적용, attack_down 4만
});

test('전 장비 파괴 → 항복 승리 + 6G (원산지 구성품 피해 +1 포함)', () => {
  const b = makeBattle('E01', ['Q03'], { gold: 0 });
  b.submitReview(uid(b, 'Q03'), { enemyEquipmentIndex: 0 });
  // 짝퉁 단검 원산지: floor(8×1.5)+1=13 ≥ 내구도 6 → 파괴 (내구도도 좋아요 단위 — ADR-015)
  assert.equal(b.state.result, 'win');
  assert.equal(b.state.stats.surrender, true);
  assert.equal(b.state.player.gold, 6);
});

test('X02: 별점 테러 — 무판정 의지 12 (배율·게이지 비대상)', () => {
  const b = makeBattle('E01', ['X02']);
  b.playSpecial(uid(b, 'X02'));
  assert.equal(b.state.enemy.will, 14 - 12);
  const j = b.state.stats.judgements;
  assert.equal(j.origin + j.fact + j.normal + j.fumble, 0);
  assert.equal(b.state.player.gauge, 0);
});

test('X08: 별점 구걸 — 신뢰도 +3 (v2)', () => {
  const b = makeBattle('E01', ['X08']);
  b.playSpecial(uid(b, 'X08'));
  assert.equal(b.state.player.gauge, 3);
});

test('X09(Layer 2): Σp(상한 5)×3 데미지, Layer 1에선 사용 불가', () => {
  const b = makeBattle('B01', ['X09'], { layer: 2, sigmaP: 5 });
  b.playSpecial(uid(b, 'X09'));
  assert.equal(b.state.enemy.will, 45); // 5×3 = 15
  const b2 = makeBattle('B01', ['X09'], { layer: 2, sigmaP: 9 });
  b2.playSpecial(uid(b2, 'X09'));
  assert.equal(b2.state.enemy.will, 45); // cap_points 5 → min(9,5)×3
  const b3 = makeBattle('B01', ['X09'], { layer: 1 });
  assert.throws(() => b3.playSpecial(uid(b3, 'X09')), /Layer/); // MVP(Layer 1)에서는 사용 불가
});

test('결정성: 같은 시드·같은 정책 입력 → 같은 결과', () => {
  const run = () => {
    const b = new Battle({
      cards: data.cards,
      enemy: data.enemies.get('E03')!,
      deck: data.startingDeck,
      rng: mulberry32(123),
    });
    // 고정 스크립트: 매턴 낼 수 있는 첫 리뷰 카드 제출
    let guard = 200;
    while (!b.state.result && guard-- > 0) {
      const c = b.state.player.hand.find((h) => {
        const d = data.cards.byId.get(h.cardId)!;
        return d.kind === 'review' && d.cost <= b.state.player.energy;
      });
      if (c) b.submitReview(c.uid);
      else b.endTurn();
    }
    return JSON.stringify([b.state.result, b.state.turn, b.state.player.will, b.state.enemy.will, b.state.stats]);
  };
  assert.equal(run(), run());
});
