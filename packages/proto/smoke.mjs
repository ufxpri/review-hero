// 프로토타입 스모크 테스트 (v2 — 대상 우선 단일 카드 플레이, card-system-v2 §8):
// jsdom으로 prototype/index.html을 실제 로드해 상점 → 전투 진입 → 대상 선택 →
// 손패 판정 뱃지 확인 → 카드 1장 제출(1장만 소모) → 대상 전환 → 턴 종료 → 퇴고가 도는지 확인한다.
// 실행: node packages/proto/smoke.mjs (저장소 루트, build.mjs 이후)
import { readFileSync } from 'node:fs';
import { JSDOM } from 'jsdom';
import assert from 'node:assert/strict';

const html = readFileSync('prototype/index.html', 'utf8');
const errors = [];
const dom = new JSDOM(html, { runScripts: 'dangerously', pretendToBeVisual: true });
dom.virtualConsole.on('jsdomError', (e) => errors.push(e.message));
const { document } = dom.window;
const $ = (sel) => document.querySelector(sel);
const $$ = (sel) => [...document.querySelectorAll(sel)];
const click = (el) => el.dispatchEvent(new dom.window.MouseEvent('click', { bubbles: true }));

// 1. 상점 렌더
const shopItems = $$('[data-shop]');
assert.equal(shopItems.length, 6, '상품 6종이 목록에 나와야 한다');
assert.ok($('.logo').textContent.includes('만물마켓'));

// 2. 전투 진입 (E01) — 기본 대상은 적 본체
click(shopItems[0]);
assert.ok($('.hand'), '손패 바가 렌더되어야 한다');
assert.ok($$('[data-card]').length >= 5, `손패 5장 이상이어야 한다 (실제 ${$$('[data-card]').length})`);
assert.ok($('.gauge'), '신뢰도 게이지가 있어야 한다');
assert.ok($('.target-bar').textContent.includes('적 본체'), '기본 대상이 적 본체여야 한다');
assert.ok($('[data-tgt="enemy"]').classList.contains('sel'), '적 본체가 선택 표시되어야 한다');

// 3. 대상 선택(적 본체 탭) → 손패 전 카드에 판정 뱃지·예상 좋아요
click($('[data-tgt="enemy"]'));
const badged = $$('.pcard[data-ct="enemy"] .badge');
assert.ok(badged.length >= 1, '적 본체 대상 카드에 판정 뱃지가 붙어야 한다');
assert.ok(
  badged.every((b) => /원산지|팩트|일반|헛소리|빗나감/.test(b.textContent)),
  `뱃지 문구가 판정이어야 한다: ${badged.map((b) => b.textContent).join(', ')}`,
);
assert.ok($$('.pcard .expect').some((x) => x.textContent.includes('👍')), '예상 좋아요(👍)가 표시되어야 한다');

// 4. 카드 1장 탭 = 제출 → 카드 1장만 소모 + 효과 반영 (v2: 접두+접미 2장 조합 폐지)
const before = {
  hand: $$('[data-card]').length,
  body: document.body.textContent.replace(/댓글[\s\S]*/, ''), // 로그 영역 제외한 상태 스냅샷
};
// 드로우 동반 카드(Z03 등)는 소모 즉시 1장을 다시 뽑아 손패 수가 안 줄므로 제외
const playable = $$('.pcard[data-ct="enemy"]:not(.off)').find(
  (c) => !/드로우/.test(c.querySelector('.uiline')?.textContent ?? ''),
);
assert.ok(playable, '선택 대상과 일치하는 카드가 손패에 있어야 한다');
click(playable);
assert.equal($$('[data-card]').length, before.hand - 1, '제출 시 카드 1장만 소모되어야 한다');
assert.notEqual(
  document.body.textContent.replace(/댓글[\s\S]*/, ''),
  before.body,
  '제출 결과가 화면에 반영되어야 한다 (의지·게이지·필력 등)',
);

// 5. 대상 전환(내 장비 탭) → 적 본체용 카드는 흐려지고(off), 탭해도 제출되지 않는다
click($('[data-tgt="meq:0"]'));
assert.ok($('[data-tgt="meq:0"]').classList.contains('sel'), '내 장비가 선택 표시되어야 한다');
assert.ok($('.target-bar').textContent.includes('내 장비'), '대상 바에 내 장비가 표시되어야 한다');
const offCard = $$('.pcard[data-ct="enemy"]')[0];
if (offCard) {
  assert.ok(offCard.classList.contains('off'), '대상 불일치 카드는 흐림(off) 처리되어야 한다');
  const handBefore = $$('[data-card]').length;
  click(offCard);
  assert.equal($$('[data-card]').length, handBefore, '대상 불일치 카드는 탭해도 소모되지 않아야 한다');
}
click($('[data-tgt="enemy"]')); // 대상 복원

// 6. 턴 종료 → 턴 진행
click($('[data-act="end"]'));
assert.ok(/턴 2/.test(document.body.textContent) || $('.overlay'), '턴이 진행되거나 전투가 끝나야 한다');

// 7. 퇴고 모드 토글
if (!$('.overlay')) {
  click($('[data-act="revise"]'));
  assert.ok($('.mode-note'), '퇴고 모드 안내가 떠야 한다');
  click($('[data-act="cancelmode"]'));
  assert.ok(!$('.mode-note'), '취소 시 안내가 사라져야 한다');
}

assert.equal(errors.length, 0, `JS 오류 발생: ${errors.join(' / ')}`);
console.log('✅ 프로토타입 스모크 통과 — 상점 6종, 전투 진입, 대상 선택→판정 뱃지, 카드 1장 제출, 대상 전환 차단, 턴 종료, 퇴고 모드');
