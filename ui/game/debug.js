/* 개발용 디버그 패널 — 모든 게임 페이지에 뜬다 (` 키 또는 우하단 🐞).
 * 설정에서 debug=false 로 끄면 숨는다. 출시 빌드에서 이 파일을 빼면 된다.
 * 페이지 고유 액션은 window.RH_DEBUG_HOOKS = {win(), lose(), ...} 로 등록한다. */
(() => {
  if (!window.RH) return;
  if (!RH.settings().debug) return;

  const PAGES = ['index.html', 'map.html', 'combat.html?enemy=E01', 'combat.html?enemy=B01',
    'event.html', 'shop.html', 'rest.html', 'result.html?outcome=death',
    'result.html?outcome=clear', 'board.html', 'account.html', 'settings.html'];

  const fab = document.createElement('button');
  fab.id = 'rh-debug-fab'; fab.textContent = '🐞'; fab.title = '디버그 (`)';
  const panel = document.createElement('div');
  panel.id = 'rh-debug';

  function render() {
    const run = RH.run(); const meta = RH.meta();
    const hooks = window.RH_DEBUG_HOOKS || {};
    panel.innerHTML = `
      <h3>🐞 디버그 — 개발용</h3>
      <div class="kv">필명 <b>${RH.ui.esc(RH.penname() || '없음')}</b> · RP <b>${meta.rp}</b> · P <b>${meta.p}</b></div>
      <div class="kv">${run ? `런: 시드 <b>${run.seed}</b> · ${run.floor}층 · 🧠${run.will} · 🪙${run.gold} · 덱 ${run.deck.length}장` : '진행 중인 런 없음'}</div>
      <div class="sec">런 조작</div>
      <button data-a="gold">🪙 골드 +100</button>
      <button data-a="heal">🧠 의지 전체 회복</button>
      <button data-a="hurt">🧠 의지 = 1</button>
      <button data-a="skip">⏭ 다음 층으로 (노드 건너뛰기)</button>
      <button data-a="newrun">🔄 새 런 시작</button>
      <button data-a="delrun">🗑 런 삭제</button>
      <div class="sec">전투 (페이지 훅)</div>
      <button data-a="win" ${hooks.win ? '' : 'disabled'}>⚔️ 즉시 승리</button>
      <button data-a="lose" ${hooks.lose ? '' : 'disabled'}>💀 즉시 패배</button>
      <div class="sec">메타 조작</div>
      <button data-a="rp">RP +10 / P +10</button>
      <button data-a="pen">✍ 필명 임시 설정 (테스트용사)</button>
      <button data-a="wipemeta">🗑 메타 초기화</button>
      <button data-a="wipeall">☢️ 전체 초기화 (reviewhero.*)</button>
      <div class="sec">페이지 이동</div>
      <select data-a="goto">
        <option value="">이동…</option>
        ${PAGES.map((p) => `<option>${p}</option>`).join('')}
      </select>`;
  }

  function act(a, val) {
    const hooks = window.RH_DEBUG_HOOKS || {};
    const run = RH.run();
    switch (a) {
      case 'gold': if (run) { run.gold += 100; RH.saveRun(run); } break;
      case 'heal': if (run) { run.will = run.maxWill; RH.saveRun(run); } break;
      case 'hurt': if (run) { run.will = 1; RH.saveRun(run); } break;
      case 'skip': if (run) { run.pos = null; run.floor = Math.min(6, run.floor + 1); RH.saveRun(run); location.href = 'map.html'; return; } break;
      case 'newrun': RH.newRun(); location.href = 'map.html'; return;
      case 'delrun': RH.clearRun(); location.href = 'index.html'; return;
      case 'win': hooks.win && hooks.win(); return;
      case 'lose': hooks.lose && hooks.lose(); return;
      case 'rp': { const m = RH.meta(); m.rp += 10; m.p += 10; RH.saveMeta(m); break; }
      case 'pen': localStorage.setItem(RH.K.penname, '테스트용사'); break;
      case 'wipemeta': localStorage.removeItem(RH.K.meta); break;
      case 'wipeall':
        Object.keys(localStorage).filter((k) => k.startsWith('reviewhero.')).forEach((k) => localStorage.removeItem(k));
        location.href = 'index.html'; return;
      case 'goto': if (val) { location.href = val; } return;
    }
    // 상태가 바뀌었으니 화면을 새로 그린다 — 페이지별 부분 갱신 대신 통째로 리로드 (개발용)
    location.reload();
  }

  panel.addEventListener('click', (e) => {
    const b = e.target.closest('button[data-a]');
    if (b && !b.disabled) act(b.dataset.a);
  });
  panel.addEventListener('change', (e) => {
    const s = e.target.closest('select[data-a]');
    if (s) act(s.dataset.a, s.value);
  });
  fab.addEventListener('click', () => { panel.classList.toggle('open'); if (panel.classList.contains('open')) render(); });
  addEventListener('keydown', (e) => {
    if (e.key === '`' && !/INPUT|TEXTAREA/.test(document.activeElement?.tagName || '')) {
      panel.classList.toggle('open'); if (panel.classList.contains('open')) render();
    }
  });

  addEventListener('DOMContentLoaded', () => { document.body.append(fab, panel); });
  if (document.readyState !== 'loading') document.body.append(fab, panel);
})();
