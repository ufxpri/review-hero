# ui/game/ 페이지 계약 — 병렬 작업 규약

모든 게임 페이지가 지키는 규약이다. 페이지를 만들거나 고치기 전에 반드시 읽는다.

## 파일 분담 (한 파일 = 한 소유자, 남의 파일 수정 금지)

| 파일 | 내용 |
|---|---|
| `index.html` | 메인(타이틀) — 이어하기/새 원정/명단/계정/설정. 필명 없으면 새 원정 → `../prologue.html` |
| `map.html` | 1막 지도 — 6층 노드 분기, 현재 층 선택 → `RH.enterNode(id)` |
| `combat.html` | 전투 — **packages/core 실제 엔진**(engine.js, `RHEngine.Battle`) 사용 |
| `event.html` `shop.html` `rest.html` | 노드 페이지 3종 |
| `result.html` | 런 종료 — 사망/클리어 공용. **사망 시 마지막 리뷰(유언) 작성** → `RH.finalizeRun()` |
| `board.html` | 원정대 명단 — NPC 대원 + 내 지난 런. 전원 익명 필명 (worldview §1.7) |
| `account.html` | 계정 — 필명·서명 미리보기·누적 통계·시작 덱 |
| `settings.html` | 설정 — 텍스트 속도/흔들림/디버그 토글/데이터 초기화 |
| 공유(수정 금지) | `shared.css` `state.js` `debug.js` `data.js` `engine.js` |

## 모든 페이지 공통 뼈대

```html
<meta charset="utf-8">
<title>이세계 리뷰용사 — {페이지명}</title>
<link rel="stylesheet" href="shared.css">
<style>/* 페이지 고유 스타일 */</style>
<script src="data.js"></script>
<script src="state.js"></script>
<body>
  <!-- 첫 줄에 상단바: document.body.insertAdjacentHTML('afterbegin', RH.ui.topbar('map')) -->
  ...
<script src="debug.js"></script>
</body>
```

- 스타일 토큰은 `shared.css` 의 CSS 변수만 쓴다. 미술 방향은 `ui/combat.html` 승계(어두운 배경 + 양피지 + 커머스 패러디).
- **UI 텍스트에서 피해는 예외 없이 「좋아요」**(GDD §2 규칙 7). 플레이어 HP는 「의지」, 에너지는 「필력」.
- 상단바는 `RH.ui.topbar(activeKey)` 를 쓴다. activeKey: `index|map|board|account|settings`.
- 마지막에 `debug.js` 를 싣는다. 전투처럼 즉시 승/패가 필요한 페이지는 `window.RH_DEBUG_HOOKS = {win, lose}` 를 등록한다.

## RH API (state.js — 전문을 읽을 것)

- `RH.run()` → 진행 중 런 또는 null. `RH.newRun(seed?)`, `RH.saveRun(run)`, `RH.clearRun()`
- 런 구조: `{seed, act, floor(1~6), pos, gold, will, maxWill, deck[], map:{floors[][]}, suitCounters, lastSuit, battlesWon}`
- 노드: `{id, type: battle|elite|event|shop|rest|boss, enemy?, visited?}` — 아이콘/라벨/페이지 매핑은 `RH.NODE_ICON/NODE_LABEL/NODE_PAGE`
- `RH.enterNode(id)` — 지도에서 호출. pos 기록 후 해당 페이지로 이동
- `RH.currentNode()` — 노드 페이지에서 자기 노드를 얻는다
- `RH.completeNode({gold?, will?, deckAdd?, deckRemoveIdx?})` → 다음 페이지 경로 반환(직접 `location.href` 에 대입). 보스면 `result.html?outcome=clear`
- `RH.finalizeRun('death'|'clear', {stars, text})` — result.html 전용. 메타 정산 + 명단 등재 + 런 삭제
- `RH.meta()` → `{runs, wins, bestFloor, rp, p, expedition[]}` · `RH.penname()` · `RH.sig()` → `{v, box:[660,236], strokes:[[[x,y],…],…]}` — 점은 `{x,y}` 객체가 아니라 **`[x,y]` 배열**이다 (signature.html 저장 형식)
- `RH.ui.topbar(k)` `RH.ui.stars(n)` `RH.ui.esc(s)`

## 전투 페이지 추가 계약 (combat.html) — 카드 체계 v2 (ADR-011)

- `engine.js` 를 추가로 싣고 `RHEngine.Battle` 을 쓴다. 사용법은 **`packages/sim/src/policies.ts` 를 정독**해서 따른다 (buildCardIndex, mulberry32, 대상 지정 형식 포함 실제 API).
- v2 API: 리뷰 카드 1장 = 완성 리뷰. `battle.submitReview(cardUid, {myEquipmentIndex?, enemyEquipmentIndex?})`,
  특수(진상 화법)는 `battle.playSpecial(uid, {giftUid?})`, 그 외 `revise(uid)` / `useCritical()` / `endTurn()`.
- 판정 4단 (card-system-v2 §2): **원산지**(대상이 카드 `origin` 과 일치 — ×1.5 +1, 무효 태그 무시, 게이지 +4) >
  헛소리(무효 태그 ×0.5, −2) > 팩트(약점 태그 ×1.5, +3) > 일반(×1.0) 순 검사. `battle.judge(card, tags, nulls, isOrigin)` 로 미리보기 가능.
- UI 흐름은 **대상 우선** (card-system-v2 §8): 대상 탭(적 본체/구성품/내 장비) → 손패 전 카드에 판정 뱃지
  (원산지 ★금색 / 팩트 ● / 헛소리 ⚠ / 일반 무표시, 특수는 「무판정」) + 예상 좋아요 → 카드 탭 = 즉시 제출.
- 적 결정: `RH.currentNode().enemy`. 런 없이 열리면 `?enemy=E01` 쿼리로 디버그 단독 전투 지원.
- 플레이어 의지는 런에서 잇는다: Battle 생성 직후 `battle.state.player.will = run.will; battle.state.player.maxWill = run.maxWill;`
- 성향 연속성: 생성 시 `initialSuitCounters: run.suitCounters, initialLastSuit: run.lastSuit`, 종료 시 되써넣기.
- 보스전 덱: `RH_DATA.bossExtra` 를 덱에 추가해 생성한다.
- 승리: 골드 보상 일반 15 / 정예 24 / 보스 50 (GDD §4.2), `run.battlesWon += 1`, `run.will = 전투 후 의지`.
- **승리 카드 보상 (ADR-011 근거 ② "이번 전투 대상의 리뷰를 등재")**: 승리 오버레이에서
  이번 전투 대상들의 리뷰 풀 — `origin.enemy === 적 id` 또는 `origin.equipment ∈ 그 적 장비 이름` — 중
  **미보유(`run.deck` 에 없는) 카드 최대 3장 제시 → 1장 선택(건너뛰기 가능)** →
  `location.href = RH.completeNode({gold:+n, deckAdd:cardId})`. 풀이 비면 보상 없이 즉시 정산.
  **디버그 전투(런 없음)와 `RH_DEBUG_HOOKS.win()` 은 카드 보상을 생략하고 즉시 정산**한다 (통합 E2E 전제).
- 패배(의지 0): `run.will = 0` 저장 후 `result.html?outcome=death&enemy={id}` 로 이동. **런은 지우지 않는다** — result 가 정산한다.
- 항복(retreat): 의지 유지 + 6G (GDD §4.2), completeNode 로 복귀. 카드 보상 없음은 X07 이탈(retreat 결과)에도 동일.
- 카드 제거 노드 공통: `RH_DATA.irremovable`(시작 덱 12장 — 생계형 리뷰)은 shop 파쇄·rest 태우기
  목록에서 **제거 불가**(회색 + 「생계형 리뷰」 칩)로 표시한다.

## 확인 절차 (필수)

빌드 성공 ≠ 동작 확인. 스크린샷으로 눈으로 본다:

```sh
PY=~/ncloud-cla-downloader/.venv/bin/python
$PY tools/ui/shot.py ui/game/{페이지}.html --out assets/generated/game-{페이지}.png
```

콘솔 오류가 있으면 종료 코드 1로 실패한다. **localStorage 를 쓰므로 상태가 필요한 페이지는
쿼리 파라미터 폴백(예: combat.html?enemy=E01)이나 "런 없음" 안내 화면이 반드시 있어야
스크린샷 검증이 가능하다.**
