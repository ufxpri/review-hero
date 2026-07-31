// 프로토타입 스모크 테스트: jsdom으로 prototype/index.html을 실제 로드해
// 상점 → 전투 진입 → 카드 선택 → 리뷰 제출 → 턴 종료가 예외 없이 도는지 확인한다.
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

// 2. 전투 진입 (E01)
click(shopItems[0]);
assert.ok($('.hand'), '손패 바가 렌더되어야 한다');
const hand = $$('[data-card]');
assert.ok(hand.length >= 5, `손패 5장 이상이어야 한다 (실제 ${hand.length})`);
assert.ok($('.gauge'), '신뢰도 게이지가 있어야 한다');

// 3. 접두 + 접미 선택 → 판정 미리보기
const prefix = $$('.pcard.prefix')[0];
assert.ok(prefix, '접두 카드가 손패에 있어야 한다');
click(prefix);
const suffix = $$('.pcard:not(.prefix):not(.special)')[0];
assert.ok(suffix, '접미 카드가 손패에 있어야 한다');
click(suffix);
const preview = $('.composer .preview').textContent;
assert.ok(/팩트|일반|헛소리/.test(preview), `판정 미리보기가 나와야 한다: ${preview}`);

// 4. 제출 → 카드 2장 소모 + 효과 반영(적 의지·구성품 내구도·내 장비 부착 중 하나가 변화)
const before = {
  hand: $$('[data-card]').length,
  body: document.body.textContent.replace(/댓글[\s\S]*/, ''), // 로그 영역 제외한 상태 스냅샷
};
const submit = $('[data-act="submit"]');
assert.ok(!submit.disabled, '제출 버튼이 활성이어야 한다');
click(submit);
assert.equal($$('[data-card]').length, before.hand - 2, '제출 시 접두·접미 2장이 소모되어야 한다');
assert.notEqual(
  document.body.textContent.replace(/댓글[\s\S]*/, ''),
  before.body,
  '제출 결과가 화면에 반영되어야 한다 (의지·내구도·부착 등)',
);

// 5. 턴 종료 → 턴 진행
click($('[data-act="end"]'));
assert.ok(/턴 2/.test(document.body.textContent) || $('.overlay'), '턴이 진행되거나 전투가 끝나야 한다');

// 6. 퇴고 모드 토글
if (!$('.overlay')) {
  click($('[data-act="revise"]'));
  assert.ok($('.mode-note'), '퇴고 모드 안내가 떠야 한다');
  click($('[data-act="cancelmode"]'));
  assert.ok(!$('.mode-note'), '취소 시 안내가 사라져야 한다');
}

assert.equal(errors.length, 0, `JS 오류 발생: ${errors.join(' / ')}`);
console.log('✅ 프로토타입 스모크 통과 — 상점 6종, 전투 진입, 판정 미리보기, 제출, 턴 종료, 퇴고 모드');
