// 이세계 리뷰용사 — 플레이어블 프로토타입 (전투 1판, 커머스 패러디 UI)
// 엔진은 packages/core 그대로 사용. UI는 이 파일이 전부 (프레임워크 없음).
import {
  Battle,
  buildCardIndex,
  mulberry32,
  type CardDef,
  type EnemyDef,
  type PrefixDef,
  type SpecialDef,
  type SuffixDef,
} from '../../core/src/index.ts';
import data from './data.json';

const cards = buildCardIndex(data.cards as CardDef[]);
const enemies = new Map<string, EnemyDef>((data.enemies as EnemyDef[]).map((e) => [e.id, e]));
const display = data.display as Record<string, { text: string; flavor: string; footer: string }>;
const PLAYABLE = ['E01', 'E02', 'E03', 'E04', 'E05', 'B01'];
const THUMB: Record<string, string> = { E01: '👺', E02: '🪓', E03: '🧝', E04: '🥷', E05: '💂', B01: '🕴️' };
const SELLER: Record<string, string> = { normal: '일반 셀러', elite: '파워 셀러', boss: '본사 직영' };

let battle: Battle | null = null;
let enemyId = '';
let selPrefix: number | null = null;
let selSuffix: number | null = null;
let myEqIdx = 0;
let enemyEqIdx = 0;
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

function stars(ratio: number): string {
  const n = Math.max(0, Math.min(5, Math.ceil(ratio * 5)));
  return '★'.repeat(n) + '☆'.repeat(5 - n);
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
    <div class="card-panel tiny">프로토타입 v1 — 전투 1판 (GDD v1.1 규칙, packages/core 엔진 그대로) · 새로고침하면 새 시드</div>`;
  app.querySelectorAll<HTMLElement>('[data-shop]').forEach((el) =>
    el.addEventListener('click', () => startBattle(el.dataset.shop!)));
}

function startBattle(id: string): void {
  enemyId = id;
  const deck = [...(data.startingDeck as string[]), ...(id === 'B01' ? (data.bossExtra as string[]) : [])];
  battle = new Battle({
    cards,
    enemy: enemies.get(id)!,
    deck,
    rng: mulberry32((Math.random() * 0xffffffff) >>> 0),
    collectLog: true,
  });
  selPrefix = selSuffix = null; myEqIdx = 0; enemyEqIdx = 0; mode = 'play';
  render();
}

// ── 전투 화면 ────────────────────────────────────────

function targetInfo(suffix: SuffixDef): { tags: string[]; nulls: string[]; label: string } {
  const st = battle!.state;
  if (suffix.target === 'my_equipment') {
    const eq = st.player.equipment[myEqIdx] ?? st.player.equipment[0]!;
    return { tags: eq.def.tags, nulls: eq.def.nullTags, label: `내 ${eq.def.name}` };
  }
  if (suffix.target === 'enemy_equipment') {
    const alive = st.enemy.equipment.map((q, i) => ({ q, i })).filter(({ q }) => !q.destroyed);
    const pick = alive.find(({ i }) => i === enemyEqIdx) ?? alive[0];
    if (!pick) return { tags: [], nulls: st.enemy.def.nullTags, label: '(남은 구성품 없음)' };
    return { tags: pick.q.tags, nulls: st.enemy.def.nullTags, label: pick.q.name };
  }
  return { tags: st.enemy.def.weaknessTags, nulls: st.enemy.def.nullTags, label: st.enemy.def.name };
}

function intentText(): string {
  const e = battle!.state.enemy;
  const a = e.def.actions.find((x) => x.id === e.intentId);
  if (!a) return '';
  const dmg = a.effects.find((f) => f.op === 'damage')?.value;
  const chg = e.charging ? ` <b>(준비 중 — ${e.charging.remaining}턴 후 발동)</b>` : a.chargeTurns > 0 ? ' (준비형)' : '';
  const icon = a.aType === 'attack' ? '📦' : a.aType === 'gimmick' ? '📢' : a.aType === 'stealth' ? '🌫' : '🛠';
  return `${icon} 발송 예정: <b>${esc(a.name)}</b>${dmg !== undefined ? ` · 피해 ${dmg}` : ''}${chg}`;
}

function render(): void {
  if (!battle) return renderShop();
  const st = battle.state;
  const p = st.player;
  const e = st.enemy;
  const prefixSel = selPrefix !== null ? p.hand.find((c) => c.uid === selPrefix) : null;
  const suffixSel = selSuffix !== null ? p.hand.find((c) => c.uid === selSuffix) : null;
  const pd = prefixSel ? (def(prefixSel.cardId) as PrefixDef) : null;
  const sd = suffixSel ? (def(suffixSel.cardId) as SuffixDef) : null;
  const cost = (pd?.cost ?? 0) + (sd?.cost ?? 0);
  let judgeHtml = '';
  if (pd && sd) {
    const t = targetInfo(sd);
    const j = battle.judge(pd, t.tags, t.nulls);
    const label = j === 'fact' ? '팩트! ×1.5' : j === 'fumble' ? '헛소리… ×0.5' : '일반 ×1.0';
    judgeHtml = `<span class="judge ${j}">${label}</span> · 대상: ${esc(t.label)} · 필력 ${cost}`;
  }

  app.innerHTML = `
    <div class="topbar"><span class="logo">만물마켓</span>
      <span class="search">🔍 ${esc(e.def.name)} 리뷰 ${1000 + e.maxWill * 7}건</span>
      <span class="stat">🧠 <b>${p.will}</b>/${p.maxWill} · ✍ <b>${p.energy}</b> · 🪙 ${p.gold}</span></div>

    <div class="card-panel">
      <div style="display:flex;gap:14px;align-items:flex-start">
        <div style="font-size:56px">${THUMB[enemyId] ?? '📦'}</div>
        <div style="flex:1">
          <b style="font-size:16px">${esc(e.def.name)}</b> <span class="tiny">${SELLER[e.def.tier]} · 턴 ${st.turn}</span>
          <div><span class="stars">${stars(e.will / e.maxWill)}</span> <span class="tiny">존재 등급 (의지 ${e.will}/${e.maxWill})</span></div>
          <div class="bar hp" style="margin-top:4px"><i style="width:${(e.will / e.maxWill) * 100}%"></i></div>
          <div style="margin-top:6px">${e.def.nullTags.map((t) => `<span class="chip null">평가 불가: ${t}</span>`).join('') || '<span class="tiny">평가 불가 항목 없음</span>'}</div>
        </div>
      </div>
      <div class="intent">${e.stealth ? '🌫 <b>판매자가 잠적했습니다</b> — 배송/CS 문의(리뷰)만 도달합니다<br>' : ''}${intentText()}</div>
      ${e.buffs.length ? `<div class="review-item">📈 판매자 버프: ${e.buffs.map((b) => `공격 +${b.value}${b.protectedBy ? ' (알바 리뷰 — P11로만 저격)' : ''}`).join(', ')}</div>` : ''}
      ${e.debuffs.map((d) => `<div class="review-item">${d.suspended ? '💬' : '😡'} 내 악평: ${d.kind === 'attack_halve' ? '공격력 −50%' : `공격력 −${d.value}`} <span class="tiny">[${d.suit}]</span>${d.suspended ? ' — <b>사장님 답글로 정지됨</b> (같은 계열 팩트로 재반박 가능)' : ''}</div>`).join('')}
      <div class="tiny" style="margin-top:8px">구성품 (탭하여 장비 리뷰 대상 지정):</div>
      ${e.equipment.map((q, i) => `<div class="equip ${q.destroyed ? 'dead' : ''} ${i === enemyEqIdx && !q.destroyed ? 'sel' : ''}" data-eeq="${i}">
        <b>${esc(q.name)}</b> ${q.destroyed ? '<span class="tiny" style="color:var(--bad)">품절(파괴)</span>' : `<span class="tiny">내구도 ${q.durability}</span>`}
        ${q.dot ? `<span class="tiny">· 도트 −${q.dot.value}(${q.dot.remaining}턴)</span>` : ''}
        ${q.disabledTurns > 0 ? '<span class="tiny">· 반품 접수중(비활성)</span>' : ''}
        <div>${q.tags.map((t) => `<span class="chip">#${t}</span>`).join('')}</div>
      </div>`).join('')}
    </div>

    <div class="card-panel">
      <div style="display:flex;justify-content:space-between;align-items:center;gap:8px;flex-wrap:wrap">
        <div><b>내 리뷰어 계정</b> <span class="tiny">성향: ${p.disposition}</span>
          <div class="gauge" style="margin-top:4px">${Array.from({ length: 10 }, (_, i) => `<i class="${i < p.gauge ? 'on' : ''}">★</i>`).join('')}</div>
          <div class="tiny">신뢰도 ${p.gauge}/10 ${p.reaction ? '· 🛡 피해보상 청구 대기중' : ''}${p.storedDamageBonus ? `· 💢 보상 예약 +${p.storedDamageBonus}` : ''}</div></div>
        <div style="display:flex;gap:6px;flex-wrap:wrap">
          ${p.gauge >= 10 && !p.critUsedThisTurn ? `<button class="btn-crit" data-act="crit">🔥 베스트 리뷰 등극</button>` : ''}
          <button class="btn-sub" data-act="revise" ${p.energy < 1 ? 'disabled' : ''}>퇴고 ✍1</button>
          <button class="btn-sub" data-act="end">영업 마감 (턴 종료)</button>
        </div>
      </div>
      <div class="tiny" style="margin-top:8px">내 장비 (버프 리뷰 대상 지정):</div>
      ${p.equipment.map((q, i) => `<div class="equip ${i === myEqIdx ? 'sel' : ''}" data-meq="${i}">
        <b>${esc(q.def.name)}</b> ${q.def.tags.map((t) => `<span class="chip">#${t}</span>`).join('')}
        ${q.def.nullTags.map((t) => `<span class="chip null">불가:${t}</span>`).join('')}
        ${q.attachments.length ? `<span class="tiny">· 부착: ${q.attachments.map((a) => `+${a.value}`).join(', ')}</span>` : ''}
      </div>`).join('')}
    </div>

    <div class="card-panel"><b class="tiny">댓글 (전투 로그)</b>
      <div class="log">${st.log.slice(-30).reverse().map((l) => `<div>${esc(l)}</div>`).join('') || '<div>아직 댓글이 없습니다.</div>'}</div></div>

    <div class="hand"><div class="hand-inner">
      ${mode !== 'play' ? `<div class="mode-note">${mode === 'revise' ? '퇴고: 버릴 카드를 선택하세요 (필력 1)' : '무료 나눔: 증정할 카드를 선택하세요'} <button class="btn-sub" data-act="cancelmode">취소</button></div>` : ''}
      <div class="composer">
        <div class="preview">${pd || sd
          ? `<b>${pd ? esc(pd.name) : '〔접두〕'}</b> + <b>${sd ? esc(sd.name) : '〔접미〕'}</b> ${judgeHtml ? '→ ' + judgeHtml : ''}`
          : '접두 카드와 접미 카드를 골라 리뷰를 완성하세요'}</div>
        <button class="btn-main" data-act="submit" ${pd && sd && cost <= p.energy ? '' : 'disabled'}>리뷰 제출</button>
      </div>
      <div class="cards-row">
        ${p.hand.map((c) => {
          const d = def(c.cardId);
          const dis = display[c.cardId] ?? { text: '', flavor: '', footer: '' };
          const sel = c.uid === selPrefix || c.uid === selSuffix ? 'sel' : '';
          if (d.kind === 'prefix') return `<div class="pcard prefix ${sel}" data-card="${c.uid}"><span class="cost">✍${d.cost}</span><b>${esc(d.name)}</b><div class="tiny">${(d as PrefixDef).tags.map((t) => `#${t}`).join(' ')}</div>${dis.text ? `<div class="foot">${esc(dis.text)}</div>` : ''}</div>`;
          if (d.kind === 'suffix') return `<div class="pcard ${sel}" data-card="${c.uid}"><span class="cost">✍${d.cost}</span><b>${esc(d.name)}</b><div class="foot">${esc(dis.text || '')}</div></div>`;
          const s = d as SpecialDef;
          return `<div class="pcard special ${sel}" data-card="${c.uid}"><span class="cost">✍${s.cost}</span><b>${esc(s.name)}</b><div class="foot">${esc(dis.text || dis.flavor || '')}</div><div class="tiny">진상 · 무판정 ${s.oncePerCombat ? '· 전투당 1회' : ''}</div></div>`;
        }).join('')}
      </div>
      <div class="tiny">덱 ${p.deck.length} · 묘지 ${p.discard.length} · 손패 ${p.hand.length}/8 — 접두는 파란 테두리, 진상 카드는 단독 사용(더블탭)</div>
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
  if (d.kind === 'prefix') { selPrefix = selPrefix === uid ? null : uid; render(); return; }
  if (d.kind === 'suffix') { selSuffix = selSuffix === uid ? null : uid; render(); return; }
  // 특수 카드: 한 번 탭 = 선택(정보), 선택 상태에서 다시 탭 = 사용
  if (selSuffix === uid) {
    const s = d as SpecialDef;
    if (s.effect.type === 'gift_card') { mode = 'gift'; giftSrcUid = uid; selSuffix = null; render(); return; }
    selSuffix = null;
    tryRun(() => battle!.playSpecial(uid));
    return;
  }
  selSuffix = uid; selPrefix = null; render();
}

function tryRun(fn: () => void): void {
  try { fn(); selPrefix = selSuffix = null; } catch (err) { toast((err as Error).message); }
  render();
}

function bindEvents(): void {
  app.querySelectorAll<HTMLElement>('[data-card]').forEach((el) =>
    el.addEventListener('click', () => onCardClick(Number(el.dataset.card))));
  app.querySelectorAll<HTMLElement>('[data-eeq]').forEach((el) =>
    el.addEventListener('click', () => { enemyEqIdx = Number(el.dataset.eeq); render(); }));
  app.querySelectorAll<HTMLElement>('[data-meq]').forEach((el) =>
    el.addEventListener('click', () => { myEqIdx = Number(el.dataset.meq); render(); }));
  app.querySelectorAll<HTMLElement>('[data-act]').forEach((el) =>
    el.addEventListener('click', () => {
      const act = el.dataset.act!;
      if (act === 'submit' && selPrefix !== null && selSuffix !== null) {
        const suffix = def(battle!.state.player.hand.find((c) => c.uid === selSuffix)!.cardId) as SuffixDef;
        void suffix;
        tryRun(() => battle!.submitReview(selPrefix!, selSuffix!, { myEquipmentIndex: myEqIdx, enemyEquipmentIndex: enemyEqIdx }));
      } else if (act === 'crit') tryRun(() => { battle!.useCritical(); });
      else if (act === 'end') tryRun(() => battle!.endTurn());
      else if (act === 'revise') { mode = 'revise'; render(); }
      else if (act === 'cancelmode') { mode = 'play'; render(); }
      else if (act === 'shop') { battle = null; render(); }
      else if (act === 'retry') startBattle(enemyId);
    }));
}

renderShop();
