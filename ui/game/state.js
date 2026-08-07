/* 이세계 리뷰용사 — 런/메타 상태 관리. 모든 게임 페이지가 공유한다.
 *
 * 저장 구조 (localStorage, 서버 없음 — GDD §1.2 "싱글 코어는 네트워크 없이 100% 동작"):
 *   reviewhero.penname  필명 (signature.html 에서 기록)
 *   reviewhero.sig      서명 획 데이터 {v:1, box:[660,236], strokes}
 *   reviewhero.meta     계정 누적 {runs, wins, bestFloor, rp, p, expedition[]}
 *   reviewhero.run      진행 중인 런 (없으면 null)
 *   reviewhero.settings {textSpeed, shake, debug}
 *
 * 페이지 흐름: index → map → (combat|event|shop|rest) → map … → 6층 boss → result → board
 * 사망 시: combat → result.html?outcome=death — 마지막 리뷰(유언)를 남기고 명단에 오른다.
 */
window.RH = (() => {
  const K = {
    penname: 'reviewhero.penname', sig: 'reviewhero.sig',
    meta: 'reviewhero.meta', run: 'reviewhero.run', settings: 'reviewhero.settings',
  };
  const load = (k, d) => { try { const v = localStorage.getItem(k); return v ? JSON.parse(v) : d; } catch { return d; } };
  const save = (k, v) => localStorage.setItem(k, JSON.stringify(v));

  // 엔진(rng.ts)과 같은 mulberry32 — engine.js 를 안 싣는 페이지도 지도를 재현할 수 있다
  function mulberry32(seed) {
    let a = seed >>> 0;
    return () => {
      a |= 0; a = (a + 0x6D2B79F5) | 0;
      let t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }

  const META0 = { runs: 0, wins: 0, bestFloor: 0, rp: 0, p: 0, expedition: [] };
  const SETTINGS0 = { textSpeed: 1, shake: true, debug: true };

  // ── 지도 생성 — 1막 6층(일반 5 + 보스 1), 층당 2~3 분기 (GDD §4.1) ──
  // 보스 직전 5층은 휴식 1개 보장. 개발 단계 가중치이며 05 §1.2 정표는 밸런스 라운드에서 반영한다.
  const POOL_NORMAL = ['E01', 'E01', 'E03', 'E04'];
  const POOL_ELITE = ['E02', 'E05'];
  function genMap(seed) {
    const r = mulberry32(seed);
    const pick = (arr) => arr[Math.floor(r() * arr.length)];
    const floors = [];
    for (let f = 1; f <= 6; f++) {
      if (f === 6) { floors.push([{ id: 'f6n0', type: 'boss', enemy: 'B01' }]); continue; }
      const n = 2 + (r() < 0.5 ? 1 : 0);
      const row = [];
      for (let i = 0; i < n; i++) {
        let type = 'battle';
        if (f > 1) {
          const roll = r();
          if (roll < 0.40) type = 'battle';
          else if (roll < 0.55) type = f >= 3 ? 'elite' : 'battle';
          else if (roll < 0.75) type = 'event';
          else if (roll < 0.90) type = 'shop';
          else type = 'rest';
        }
        const node = { id: `f${f}n${i}`, type };
        // 1~2층은 E01만 — E03/E04는 tier:elite 라 초반 일반 전투에 넣으면 과중 (지도 에이전트 지적)
        if (type === 'battle') node.enemy = f <= 2 ? 'E01' : pick(POOL_NORMAL);
        if (type === 'elite') node.enemy = pick(POOL_ELITE);
        row.push(node);
      }
      if (f === 5 && row.every((x) => x.type !== 'rest')) {
        const i = Math.floor(r() * row.length);
        row[i] = { id: row[i].id, type: 'rest' };
      }
      floors.push(row);
    }
    return { floors };
  }

  const NODE_LABEL = { battle: '전투', elite: '정예', event: '이벤트', shop: '상점', rest: '휴식', boss: '보스' };
  const NODE_ICON = { battle: '⚔️', elite: '🛡️', event: '❓', shop: '🪙', rest: '🏕️', boss: '👑' };
  const NODE_PAGE = { battle: 'combat.html', elite: 'combat.html', boss: 'combat.html', event: 'event.html', shop: 'shop.html', rest: 'rest.html' };

  // ── 런 ──────────────────────────────────────────────────
  function getRun() { return load(K.run, null); }
  function saveRun(run) { save(K.run, run); }
  function clearRun() { localStorage.removeItem(K.run); }

  function newRun(seed) {
    seed = seed ?? Math.floor(Math.random() * 0xffffffff);
    const run = {
      seed, act: 1, floor: 1, pos: null,
      gold: 0, will: 30, maxWill: 30,
      deck: (window.RH_DATA ? RH_DATA.startingDeck.slice() : []),
      map: genMap(seed),
      suitCounters: {}, lastSuit: null,
      battlesWon: 0, startedAt: new Date().toISOString(),
    };
    saveRun(run);
    return run;
  }

  function currentNode(run) {
    run = run || getRun();
    if (!run || !run.pos) return null;
    const row = run.map.floors[run.floor - 1] || [];
    return row.find((n) => n.id === run.pos) || null;
  }

  /** 지도에서 노드 선택 → 해당 페이지로 이동 */
  function enterNode(nodeId) {
    const run = getRun(); if (!run) return;
    const row = run.map.floors[run.floor - 1] || [];
    const node = row.find((n) => n.id === nodeId);
    if (!node) return;
    run.pos = nodeId;
    saveRun(run);
    location.href = NODE_PAGE[node.type];
  }

  /** 노드 페이지가 끝났을 때 호출. patch = {gold?, will?, deckAdd?, deckRemoveIdx?}
   *  반환값 = 다음에 이동할 페이지. 보스 승리는 combat 쪽에서 result 로 직행한다. */
  function completeNode(patch = {}) {
    const run = getRun(); if (!run) return 'index.html';
    const node = currentNode(run);
    // 노드 진입 없이(URL 직접 접근) 호출되면 층을 공짜로 넘기지 않는다 (노드 에이전트 지적)
    if (!node) return 'map.html';
    node.visited = true;
    if (patch.gold) run.gold = Math.max(0, run.gold + patch.gold);
    if (patch.will) run.will = Math.max(1, Math.min(run.maxWill, run.will + patch.will));
    if (patch.deckAdd) run.deck.push(...[].concat(patch.deckAdd));
    if (typeof patch.deckRemoveIdx === 'number') run.deck.splice(patch.deckRemoveIdx, 1);
    const isBoss = node.type === 'boss';
    run.pos = null;
    if (!isBoss) run.floor += 1;
    saveRun(run);
    return isBoss ? 'result.html?outcome=clear' : 'map.html';
  }

  /** 런 종료 정산. result.html 이 리뷰(유언) 제출 시 호출한다.
   *  outcome: 'clear' | 'death', review: {stars, text}
   *  보상 수치는 GDD §4.2 를 개발용으로 단순화한 값 — 밸런스 라운드에서 재조정. */
  function finalizeRun(outcome, review) {
    const run = getRun(); if (!run) return null;
    const meta = getMeta();
    meta.runs += 1;
    const reachedFloor = run.floor;
    const newBest = reachedFloor > meta.bestFloor;
    if (newBest) meta.bestFloor = reachedFloor;
    if (outcome === 'clear') { meta.wins += 1; meta.rp += 40; meta.p += 23; }
    else { if (newBest) meta.rp += 5; meta.p += Math.min(8, reachedFloor); }
    meta.expedition.unshift({
      name: getPenname() || '무명', me: true,
      result: outcome, floor: reachedFloor,
      stars: review?.stars ?? 0, review: review?.text ?? '',
      // 전투 승리 0회인 런의 유언은 집계 제외(자살 파밍 차단, GDD §4.3) → 계류
      status: outcome === 'death' && run.battlesWon === 0 ? '계류' : '게시',
      date: new Date().toISOString().slice(0, 10),
    });
    saveMeta(meta);
    clearRun();
    return meta;
  }

  // ── 메타/설정/계정 ──────────────────────────────────────
  function getMeta() { return Object.assign({}, META0, load(K.meta, {})); }
  function saveMeta(m) { save(K.meta, m); }
  function getSettings() { return Object.assign({}, SETTINGS0, load(K.settings, {})); }
  function saveSettings(s) { save(K.settings, s); }
  function getPenname() { return localStorage.getItem(K.penname) || null; }
  function getSig() { return load(K.sig, null); }

  // ── 공용 UI 조각 ────────────────────────────────────────
  const esc = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  function stars(n, max = 5) {
    n = Math.max(0, Math.min(max, Math.round(n)));
    return `<span class="stars">${'★'.repeat(n)}<span class="off">${'★'.repeat(max - n)}</span></span>`;
  }
  /** active: 'index'|'map'|'board'|'account'|'settings' */
  function topbar(active = '') {
    const run = getRun();
    const pen = getPenname();
    const a = (k) => (active === k ? ' class="active"' : '');
    const runStat = run
      ? `<span class="stat">🧠 <b>${run.will}</b>/${run.maxWill}</span>
         <span class="stat">🪙 <b>${run.gold}</b></span>
         <span class="stat tiny">1막 ${run.floor}층</span>`
      : '';
    return `<div class="topbar">
      <a class="logo" href="index.html">이세계 리뷰용사</a>
      <span class="chip">✍ ${esc(pen || '미등록')}</span>
      ${runStat}
      <nav>
        <a href="map.html"${a('map')}>지도</a>
        <a href="board.html"${a('board')}>원정대 명단</a>
        <a href="account.html"${a('account')}>계정</a>
        <a href="settings.html"${a('settings')}>설정</a>
      </nav>
    </div>`;
  }

  return {
    K, load, save, mulberry32, genMap,
    NODE_LABEL, NODE_ICON, NODE_PAGE,
    run: getRun, saveRun, clearRun, newRun, currentNode, enterNode, completeNode, finalizeRun,
    meta: getMeta, saveMeta, settings: getSettings, saveSettings,
    penname: getPenname, sig: getSig,
    ui: { topbar, stars, esc },
  };
})();
