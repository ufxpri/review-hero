// 이세계 리뷰용사 — 플레이어블 프로토타입 v2 (카드 체계 v2 — ADR-011, card-system-v2 §8)
// UI 흐름: 대상 우선 — 대상 탭(적 본체/구성품/내 장비) → 손패 전 카드에 판정 뱃지·예상 좋아요
// (원산지 ★ / 팩트 ● / 헛소리 ⚠) → 카드 탭 = 제출. 카드 1장 = 완성 리뷰.
// 엔진은 packages/core 그대로 사용. UI는 이 파일이 전부 (프레임워크 없음).
import {
  applyMult,
  Battle,
  buildCardIndex,
  mulberry32,
  type CardDef,
  type EnemyDef,
  type Judgement,
  type ReviewCardDef,
  type SpecialDef,
  type SubmitPreview,
  type TargetKind,
} from '../../core/src/index.ts';
import data from './data.json';

const cards = buildCardIndex(data.cards as unknown as CardDef[]);
const enemies = new Map<string, EnemyDef>((data.enemies as unknown as EnemyDef[]).map((e) => [e.id, e]));
const PLAYABLE = ['E01', 'E02', 'E03', 'E04', 'E05', 'B01'];
const THUMB: Record<string, string> = { E01: '👺', E02: '🪓', E03: '🧝', E04: '🥷', E05: '💂', B01: '🕴️' };
const SELLER: Record<string, string> = { normal: '일반 셀러', elite: '파워 셀러', boss: '본사 직영' };
const TARGET_LABEL: Record<TargetKind, string> = { enemy: '적 본체', enemy_equipment: '구성품', my_equipment: '내 장비' };
/** 판정 뱃지 (card-system-v2 §8: 원산지 ★ / 팩트 ● / 헛소리 ⚠) */
const BADGE: Record<Judgement, string> = { origin: '★ 원산지', fact: '● 팩트', normal: '일반', fumble: '⚠ 헛소리' };

/** 선택된 리뷰 대상 (대상 우선 — index는 구성품/내 장비에서만 의미) */
interface TargetSel {
  kind: TargetKind;
  index: number;
}

let battle: Battle | null = null;
let enemyId = '';
let sel: TargetSel = { kind: 'enemy', index: 0 };
let mode: 'play' | 'revise' | 'gift' = 'play';
let giftSrcUid = 0;

const app = document.getElementById('app')!;
const esc = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

function toast(msg: string): void {
  let t = document.querySelector<HTMLElement>('.toast');
  if (!t) { t = document.createElement('div'); t.className = 'toast'; document.body.appendChild(t); }
  t.textContent = msg;
  t.classList.add('show');
  setTimeout(() => t!.classList.remove('show'), 1600);
}

function stars(n: number): string {
  const k = Math.max(0, Math.min(5, n));
  return '★'.repeat(k) + '☆'.repeat(5 - k);
}

function def(cardId: string): CardDef { return cards.byId.get(cardId)!; }

// ── 상품 목록 (적 선택) ──────────────────────────────

function renderShop(): void {
  app.innerHTML = `
    <div class="topbar"><span class="logo">만물마켓</span>
      <span class="search">🔍 리뷰가 곧 법입니다 — 무엇이든 평가하세요</span></div>
    <div class="card-panel">
      <b style="font-size:16px">오늘의 특가 ⚡ 던전 직송</b>
      <div class="muted">상품(적)을 선택하면 리뷰 전투가 시작됩니다. 반품은 칼로만 받습니다.</div>
    </div>
    ${PLAYABLE.map((id) => {
      const e = enemies.get(id)!;
      return `<div class="shop-item" data-shop="${id}">
        <div class="thumb">${THUMB[id] ?? '📦'}</div>
        <div style="flex:1">
          <b>${esc(e.name)}</b> <span class="tiny">${SELLER[e.tier]}</span>
          <div class="stars" style="font-size:12px">★★★★★ <span class="tiny" style="color:var(--ink3)">리뷰 ${1000 + e.will * 7}</span></div>
          <div class="tiny">의지 ${e.will} · 평가 불가: ${e.nullTags.length ? e.nullTags.join(', ') : '없음'}${id === 'B01' ? ' · 표준 검증 덱 지급' : ''}</div>
        </div>
        <button class="btn-sub">담기</button>
      </div>`;
    }).join('')}
    <div class="card-panel tiny">프로토타입 v2 — 대상 우선 단일 카드 플레이 (card-system-v2 §8, packages/core 엔진 그대로) · 새로고침하면 새 시드</div>`;
  app.querySelectorAll<HTMLElement>('[data-shop]').forEach((el) =>
    el.addEventListener('click', () => startBattle(el.dataset.shop!)));
}

function startBattle(id: string): void {
  enemyId = id;
  const deck = [...data.startingDeck, ...(id === 'B01' ? data.bossExtra : [])];
  battle = new Battle({
    cards,
    enemy: enemies.get(id)!,
    deck,
    rng: mulberry32((Math.random() * 0xffffffff) >>> 0),
    collectLog: true,
  });
  sel = { kind: 'enemy', index: 0 };
  mode = 'play';
  render();
}

// ── 판정 미리보기 (엔진 submitReview의 판정·산식 재현 — battle.judge 공개 API 사용) ──

interface Preview {
  compatible: boolean; // 카드의 target 종류가 선택 대상과 일치하는가
  missed: boolean; // E04 은신 게이트 — 제출해도 빗나감 (필력·카드만 소모)
  judgement: Judgement | null;
  expect: string; // 예상 좋아요/효과 (엔진 좋아요 환산식 GDD §2 재현)
  gauge: number; // 판정 게이지 증감 (헛소리 −2는 온보딩 무보정 기본값)
}

/**
 * 판정 미리보기 — **계산은 엔진이 소유한다**(battle.previewSubmit).
 * 여기서 규칙을 재구현하면 밸런스를 고칠 때 표시값만 조용히 틀려진다.
 */
function preview(uid: number, d: ReviewCardDef): Preview {
  const none: Preview = { compatible: false, missed: false, judgement: null, expect: '', gauge: 0 };
  if (d.target !== sel.kind) return none;
  const opts =
    d.target === 'my_equipment' ? { myEquipmentIndex: sel.index }
    : d.target === 'enemy_equipment' ? { enemyEquipmentIndex: sel.index }
    : {};
  const pv = battle!.previewSubmit(uid, opts);
  if (pv.blocked === 'void') return none;
  if (pv.blocked === 'miss') {
    return { compatible: true, missed: true, judgement: null, expect: '🌫 빗나감', gauge: 0 };
  }
  return {
    compatible: true,
    missed: false,
    judgement: pv.judgement,
    expect: expectText(d, pv),
    gauge: pv.gauge,
  };
}

/** 예상 좋아요 — 엔진 applyReviewEffect의 좋아요 환산식(GDD §2) 재현. 피해 없는 효과는 수치만 배율 반영 */
function expectText(d: ReviewCardDef, pv: SubmitPreview): string {
  const st = battle!.state;
  const p = st.player;
  const ef = d.effect;
  const mult = pv.mult; // 판정 × E03 영창 약점 — 엔진이 계산한 값
  if (pv.likes !== null) return `👍 ${pv.likes}${pv.likesKind === 'equipment' ? ' 내구도' : ''}`;
  if (ef.type === 'equipment_dot') {
    const dur = typeof ef.duration === 'number' ? ef.duration : 2;
    return `도트 👍 ${applyMult(ef.value ?? 0, mult)}×${dur}턴`;
  }
  if (ef.type === 'damage_buff') {
    const eq = p.equipment[sel.index] ?? p.equipment[0]!;
    if (eq.attachments.filter((a) => a.usesSlot).length >= 2) return '⚠ 부착 슬롯 만석';
    return `버프 👍 +${applyMult(ef.value ?? 0, mult)}`;
  }
  if (ef.type === 'attack_down') return `적 공격 −${applyMult(ef.value ?? 0, mult)}`;
  return '';
}

// ── 전투 화면 ────────────────────────────────────────

function intentText(): string {
  const e = battle!.state.enemy;
  const a = e.def.actions.find((x) => x.id === e.intentId);
  if (!a) return '';
  const dmg = a.effects.find((f) => f.op === 'damage')?.value;
  const chg = e.charging ? ` <b>(준비 중 — ${e.charging.remaining}턴 후 발동)</b>` : a.chargeTurns > 0 ? ' (준비형)' : '';
  const icon = a.aType === 'attack' ? '📦' : a.aType === 'gimmick' ? '📢' : a.aType === 'stealth' ? '🌫' : '🛠';
  return `${icon} 발송 예정: <b>${esc(a.name)}</b>${dmg !== undefined ? ` · 좋아요 ${dmg}` : ''}${chg}`;
}

function targetLabel(): string {
  const st = battle!.state;
  if (sel.kind === 'enemy') return `${st.enemy.def.name} (적 본체)`;
  if (sel.kind === 'enemy_equipment') return `구성품 · ${st.enemy.equipment[sel.index]?.name ?? '?'}`;
  return `내 장비 · ${st.player.equipment[sel.index]?.def.name ?? '?'}`;
}

function cardHtml(uid: number, d: CardDef): string {
  const p = battle!.state.player;
  const poor = d.cost > p.energy ? ' nope' : '';
  if (d.kind === 'special') {
    const s = d as SpecialDef;
    return `<div class="pcard special" data-card="${uid}">
      <span class="cost${poor}">✍${s.cost}</span>
      <span class="badge troll">진상 · 무판정</span>
      <b>${esc(s.name)}</b>
      ${s.text ? `<div class="body">${esc(s.text)}</div>` : ''}
      ${s.ui ? `<div class="uiline">${esc(s.ui)}</div>` : ''}
    </div>`;
  }
  const r = d as ReviewCardDef;
  const pv = preview(uid, r);
  const jc = pv.missed ? 'missed' : pv.judgement ?? '';
  const badge = !pv.compatible
    ? `<span class="badge off">대상: ${TARGET_LABEL[r.target]}</span>`
    : pv.missed
      ? '<span class="badge missed">🌫 빗나감</span>'
      : `<span class="badge ${pv.judgement}">${BADGE[pv.judgement!]}</span>`;
  const expect = pv.compatible && !pv.missed
    ? `<div class="expect">${esc(pv.expect)}${pv.gauge ? ` · 게이지 ${pv.gauge > 0 ? '+' : ''}${pv.gauge}` : ''}</div>`
    : pv.missed ? '<div class="expect">필력·카드만 소모됩니다</div>' : '';
  return `<div class="pcard ${jc} ${pv.compatible ? '' : 'off'}" data-card="${uid}" data-ct="${r.target}">
    <span class="cost${poor}">✍${r.cost}</span>
    ${badge}
    <b>${esc(r.name)}</b>
    <div class="starline"><span class="stars">${stars(r.stars)}</span> <span class="tag">#${esc(r.tag)}</span></div>
    ${r.text ? `<div class="body">${esc(r.text)}</div>` : ''}
    ${r.ui ? `<div class="uiline">${esc(r.ui)}</div>` : ''}
    ${expect}
  </div>`;
}

function render(): void {
  if (!battle) return renderShop();
  const st = battle.state;
  const p = st.player;
  const e = st.enemy;

  app.innerHTML = `
    <div class="topbar"><span class="logo">만물마켓</span>
      <span class="search">🔍 ${esc(e.def.name)} 리뷰 ${1000 + e.maxWill * 7}건</span>
      <span class="stat">🧠 <b>${p.will}</b>/${p.maxWill} · ✍ <b>${p.energy}</b> · 🪙 ${p.gold}</span></div>

    <div class="card-panel">
      <div class="tgt ${sel.kind === 'enemy' ? 'sel' : ''}" data-tgt="enemy" style="display:flex;gap:14px;align-items:flex-start">
        <div style="font-size:56px">${THUMB[enemyId] ?? '📦'}</div>
        <div style="flex:1">
          <b style="font-size:16px">${esc(e.def.name)}</b> <span class="tiny">${SELLER[e.def.tier]} · 턴 ${st.turn}</span>
          <div><span class="stars">${stars(Math.ceil((e.will / e.maxWill) * 5))}</span> <span class="tiny">존재 등급 (의지 ${e.will}/${e.maxWill})</span></div>
          <div class="bar hp" style="margin-top:4px"><i style="width:${(e.will / e.maxWill) * 100}%"></i></div>
          <div style="margin-top:6px">${e.def.weaknessTags.map((t) => `<span class="chip">약점 #${t}</span>`).join('')}${e.def.nullTags.map((t) => `<span class="chip null">평가 불가: ${t}</span>`).join('') || ''}</div>
          <div class="tiny">탭하여 적 본체를 리뷰 대상으로</div>
        </div>
      </div>
      <div class="intent">${e.stealth ? '🌫 <b>판매자가 잠적했습니다</b> — 배송/CS 문의(리뷰)만 도달합니다<br>' : ''}${intentText()}</div>
      ${e.buffs.length ? `<div class="review-item">📈 판매자 버프: ${e.buffs.map((b) => `공격 +${b.value}${b.protectedBy ? ` (알바 리뷰 — ${b.counterCard ?? '?'}로만 저격)` : ''}`).join(', ')}</div>` : ''}
      ${e.debuffs.map((d) => `<div class="review-item">${d.suspended ? '💬' : '😡'} 내 악평: ${d.kind === 'attack_halve' ? '공격력 −50%' : `공격력 −${d.value}`} <span class="tiny">[${d.suit}]</span>${d.suspended ? ' — <b>사장님 답글로 정지됨</b> (같은 계열 팩트/원산지로 재반박 가능)' : ''}</div>`).join('')}
      <div class="tiny" style="margin-top:8px">구성품 (탭하여 리뷰 대상 지정):</div>
      ${e.equipment.map((q, i) => `<div class="equip tgt ${q.destroyed ? 'dead' : ''} ${sel.kind === 'enemy_equipment' && sel.index === i && !q.destroyed ? 'sel' : ''}" ${q.destroyed ? '' : `data-tgt="eeq:${i}"`}>
        <b>${esc(q.name)}</b> ${q.destroyed ? '<span class="tiny" style="color:var(--bad)">품절(파괴)</span>' : `<span class="tiny">내구도 ${q.durability}</span>`}
        ${q.dot ? `<span class="tiny">· 도트 −${q.dot.value}(${q.dot.remaining}턴)</span>` : ''}
        ${q.disabledTurns > 0 ? '<span class="tiny">· 반품 접수중(비활성)</span>' : ''}
        <div>${q.tags.map((t) => `<span class="chip">#${t}</span>`).join('')}</div>
      </div>`).join('')}
    </div>

    <div class="card-panel">
      <div style="display:flex;justify-content:space-between;align-items:center;gap:8px;flex-wrap:wrap">
        <div><b>내 리뷰어 계정</b> <span class="tiny">${p.disposition}</span>
          <div class="gauge" style="margin-top:4px">${Array.from({ length: 10 }, (_, i) => `<i class="${i < p.gauge ? 'on' : ''}">★</i>`).join('')}</div>
          <div class="tiny">신뢰도 ${p.gauge}/10 ${p.reaction ? '· 🛡 피해보상 청구 대기중' : ''}${p.storedDamageBonus ? `· 💢 보상 예약 👍 +${p.storedDamageBonus}` : ''}</div></div>
        <div style="display:flex;gap:6px;flex-wrap:wrap">
          ${p.gauge >= 10 && !p.critUsedThisTurn ? `<button class="btn-crit" data-act="crit">🔥 베스트 리뷰 등극</button>` : ''}
          <button class="btn-sub" data-act="revise" ${p.energy < 1 ? 'disabled' : ''}>퇴고 ✍1</button>
          <button class="btn-sub" data-act="end">영업 마감 (턴 종료)</button>
        </div>
      </div>
      <div class="tiny" style="margin-top:8px">내 장비 (탭하여 찬양 리뷰 대상 지정):</div>
      ${p.equipment.map((q, i) => `<div class="equip tgt ${sel.kind === 'my_equipment' && sel.index === i ? 'sel' : ''}" data-tgt="meq:${i}">
        <b>${esc(q.def.name)}</b> ${q.def.tags.map((t) => `<span class="chip">#${t}</span>`).join('')}
        ${q.def.nullTags.map((t) => `<span class="chip null">불가:${t}</span>`).join('')}
        ${q.attachments.length ? `<span class="tiny">· 부착: ${q.attachments.map((a) => `👍 +${a.value}`).join(', ')}</span>` : ''}
      </div>`).join('')}
    </div>

    <div class="card-panel"><b class="tiny">댓글 (전투 로그)</b>
      <div class="log">${st.log.slice(-30).reverse().map((l) => `<div>${esc(l)}</div>`).join('') || '<div>아직 댓글이 없습니다.</div>'}</div></div>

    <div class="hand"><div class="hand-inner">
      ${mode !== 'play' ? `<div class="mode-note">${mode === 'revise' ? '퇴고: 버릴 카드를 선택하세요 (필력 1)' : '무료 나눔: 증정할 카드를 선택하세요'} <button class="btn-sub" data-act="cancelmode">취소</button></div> ` : ''}
      <div class="target-bar">🎯 대상: <b>${esc(targetLabel())}</b> <span class="tiny">— 카드를 탭하면 바로 제출 · 뱃지: ★원산지 ●팩트 ⚠헛소리</span></div>
      <div class="cards-row">
        ${p.hand.map((c) => cardHtml(c.uid, def(c.cardId))).join('')}
      </div>
      <div class="tiny">덱 ${p.deck.length} · 묘지 ${p.discard.length} · 손패 ${p.hand.length}/8 — 흐린 카드는 다른 대상용, 진상 카드는 대상 무관 즉시 사용</div>
    </div></div>
    ${st.result ? overlayHtml(st.result) : ''}`;

  bindEvents();
}

function overlayHtml(result: string): string {
  const e = battle!.state.enemy;
  const map: Record<string, [string, string, string]> = {
    win: battle!.state.stats.surrender
      ? ['🏳️', '판매자 폐업 (항복)', '전 구성품이 품절되어 판매자가 영업을 포기했습니다. 위로금 6골드를 뜯어냈습니다.']
      : ['🛍️', '구매 확정!', `${e.def.name} 처치. ★5 「정복 후기(구매 확정)」 작성 권한을 얻었습니다.`],
    lose: ['👋', '회원 탈퇴 처리되었습니다', '만물대장이 당신을 읽지 못하게 되었습니다. …원래도 못 읽었지만요.'],
    retreat: ['🚪', '주문 취소', '"내가 여길 다시 오나 봐라." 지인 열 명에게 소문내러 갑니다.'],
    timeout: ['⏰', '고객센터 상담 시간 종료', '전투가 너무 길어졌습니다.'],
  };
  const [icon, title, sub] = map[result] ?? ['?', result, ''];
  return `<div class="overlay"><div class="box">
    <div style="font-size:44px">${icon}</div><h2>${title}</h2>
    <div class="muted">${sub}</div>
    <div style="margin-top:16px;display:flex;gap:8px;justify-content:center">
      <button class="btn-main" data-act="shop">다른 상품 보기</button>
      <button class="btn-sub" data-act="retry">같은 상품 재구매</button>
    </div></div></div>`;
}

function onCardClick(uid: number): void {
  const p = battle!.state.player;
  const card = p.hand.find((c) => c.uid === uid);
  if (!card) return;
  const d = def(card.cardId);
  if (mode === 'revise') { mode = 'play'; tryRun(() => battle!.revise(uid)); return; }
  if (mode === 'gift') { mode = 'play'; tryRun(() => battle!.playSpecial(giftSrcUid, { giftUid: uid })); return; }
  if (d.kind === 'special') {
    // 진상 화법: 무판정·대상 무관 즉시 사용 (X04 증정만 카드 지정 모드 경유)
    if (d.effect.type === 'gift_card') {
      if (p.energy < d.cost) { toast('필력 부족'); return; }
      mode = 'gift'; giftSrcUid = uid; render(); return;
    }
    tryRun(() => battle!.playSpecial(uid));
    return;
  }
  // 리뷰 카드: 대상 우선 — 선택된 대상 종류와 일치해야 제출
  if (d.target !== sel.kind) { toast(`이 카드의 대상은 「${TARGET_LABEL[d.target]}」 — 대상을 먼저 탭하세요`); return; }
  if (p.energy < d.cost) { toast('필력 부족'); return; }
  const opts = sel.kind === 'my_equipment' ? { myEquipmentIndex: sel.index }
    : sel.kind === 'enemy_equipment' ? { enemyEquipmentIndex: sel.index } : {};
  tryRun(() => battle!.submitReview(uid, opts));
}

function tryRun(fn: () => void): void {
  try { fn(); } catch (err) { toast((err as Error).message); }
  // 선택한 구성품이 파괴되면 대상을 적 본체로 되돌린다
  if (battle && sel.kind === 'enemy_equipment' && (battle.state.enemy.equipment[sel.index]?.destroyed ?? true)) {
    sel = { kind: 'enemy', index: 0 };
  }
  render();
}

function bindEvents(): void {
  app.querySelectorAll<HTMLElement>('[data-card]').forEach((el) =>
    el.addEventListener('click', () => onCardClick(Number(el.dataset.card))));
  app.querySelectorAll<HTMLElement>('[data-tgt]').forEach((el) =>
    el.addEventListener('click', () => {
      const t = el.dataset.tgt!;
      if (t === 'enemy') sel = { kind: 'enemy', index: 0 };
      else {
        const [k, i] = t.split(':');
        sel = { kind: k === 'eeq' ? 'enemy_equipment' : 'my_equipment', index: Number(i) };
      }
      render();
    }));
  app.querySelectorAll<HTMLElement>('[data-act]').forEach((el) =>
    el.addEventListener('click', (ev) => {
      ev.stopPropagation(); // 대상 영역 안의 버튼이 대상 선택을 함께 트리거하지 않게
      const act = el.dataset.act!;
      if (act === 'crit') tryRun(() => { battle!.useCritical(); });
      else if (act === 'end') tryRun(() => battle!.endTurn());
      else if (act === 'revise') { mode = 'revise'; render(); }
      else if (act === 'cancelmode') { mode = 'play'; render(); }
      else if (act === 'shop') { battle = null; render(); }
      else if (act === 'retry') startBattle(enemyId);
    }));
}

renderShop();
