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
- 런 구조: `{seed, act, floor(1~6), pos, characterId, gold, will, maxWill, deck[], map:{floors[][]}, suitCounters, lastSuit, path[], parcel, battlesWon, combat?}`
- 노드: `{id, type: battle|elite|event|shop|rest|boss, enemy?, visited?}` — 아이콘/라벨/페이지 매핑은 `RH.NODE_ICON/NODE_LABEL/NODE_PAGE`
- `RH.enterNode(id)` — 지도에서 호출. pos 기록 후 해당 페이지로 이동
- `RH.currentNode()` — 노드 페이지에서 자기 노드를 얻는다
- `RH.reachable(run?)` — 지금 고를 수 있는 노드 id 목록. **`run.pos` 가 남아 있으면(= 노드를 끝내지 않았다) 그 노드 하나만 돌려준다** — 아래 §중단·재개 참조
- `RH.completeNode({gold?, will?, deckAdd?, deckRemoveIdx?})` → 다음 페이지 경로 반환(직접 `location.href` 에 대입). 보스면 `result.html?outcome=clear`
- `RH.finalizeRun('death'|'clear', {stars, text})` — result.html 전용. 메타 정산 + 명단 등재 + 런 삭제
- `RH.meta()` → `{runs, wins, bestFloor, rp, p, expedition[], characterId, stats, seen[], badges[]}` · `RH.penname()` · `RH.sig()` → `{v, box:[660,236], strokes:[[[x,y],…],…]}` — 점은 `{x,y}` 객체가 아니라 **`[x,y]` 배열**이다 (signature.html 저장 형식)
- `RH.ui.topbar(k)` `RH.ui.stars(n)` `RH.ui.esc(s)`

## 중단·재개 — 세이브 스커밍 차단 (노드 갈아타기 금지)

전투 도중 탭을 닫아도 **런은 살린다**(브라우저 크래시로 런을 통째로 날리는 쪽이 더 나쁘다).
**재전은 허용하고, 막는 것은 노드 갈아타기뿐이다.** 근거는 `run.pos`: 노드를 끝내는 유일한
경로가 `completeNode()`(= path 에 적고 pos 를 비운다)라, pos 가 남아 있으면 곧 중도 이탈이다.

| 중단 지점 | 복원 | `RH.resumeInfo().label` |
|---|---|---|
| 지도 (노드 미선택) | 그대로 | `중단 지점: 지도` |
| 노드 진입 후 전투 전 | 그대로 (그 노드만 열림) | `중단 지점: 지도` |
| **전투 중** | **그 노드로 강제 복귀 + 전투 처음부터.** 노드 교체 불가 | `중단 지점: ⚔ 전투 — 처음부터 다시 붙습니다` |

- `RH.resumeInfo(run?)` → `{kind:'map'|'combat', nodeId, nodeType, floor, href, label}` (런 없으면 `null`).
  메인 허브가 이어하기 버튼의 문구·목적지로 쓴다. 전투 상태는 저장하지 않으므로 재개 = 처음부터.
- `RH.beginCombat(nodeId, run?)` / `RH.endCombat(run?)` — **combat.html 전용.** Battle 을 만든 직후 켜고
  승/패/항복 처리 시 끈다. 이 표시만이 「노드에 발만 들인 상태」와 「전투 중」을 가른다.

## 계측 — 업적·도감 (소급 계산이 불가능하므로 기록은 항상 켜 둔다)

`meta.stats` 누적 카운터 / `meta.seen` 등재 카드 id / `meta.badges` 등재 업적 id.
없는 필드는 `RH.meta()` 가 기본값으로 채우므로 **구 세이브도 그대로 산다.**
`meta.characterId` · `run.characterId` 는 ADR-028 대비로 지금은 `'default'` 고정.

```js
meta.stats = {
  submissions, judgements:{origin,fact,normal,fumble}, crits, critMisses,
  battlesWon, surrenderWins, retreats, cardsRemoved,
  minWillWin,          // 최소 의지 승리 기록 (미달성 null)
  defenseAbsorbed, willHealed, parcelsOpened,
}
```

**페이지는 localStorage 를 직접 만지지 않는다.** 기록은 아래 헬퍼로만 한다:

- `RH.recordSeen(idsOrId)` → 새로 등재한 장수. 중복은 무시. **카드를 얻는 모든 경로에서 부른다**
  (시작 덱 12장은 `newRun` 이, 전투 보상·이벤트 획득은 `completeNode({deckAdd})` 가 자동으로 처리한다.
  상점 구매처럼 `run.deck` 을 직접 만지는 경로는 그 페이지가 직접 부른다)
- `RH.bumpStat(key, n=1)` — 중첩 키는 점 표기(`'judgements.origin'`). `RH.recordStatMin(key, v)` 는 최솟값 기록
- `RH.recordBadges(idsOrId)` — 업적 등재 (판정 로직은 다음 단계)
- `RH.mergeBattleStats(battle.state.stats, {result, willLeft})` — 엔진 `BattleStats` 1판치를 계정 누적으로.
  combat.html 이 전투 종료 시 한 번 부른다
- `completeNode({deckRemoveIdx})` 는 `cardsRemoved` 를 자동으로 센다 (휴식 태우기).
  덱을 직접 splice 하는 상점 파쇄는 shop.html 이 직접 센다

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
- 논점 연속성: 생성 시 `initialSuitCounters: run.suitCounters, initialLastSuit: run.lastSuit`, 종료 시 되써넣기.
- 보스전 덱: `RH_DATA.bossExtra` 를 덱에 추가해 생성한다.
- 승리: 골드 보상 일반 15 / 정예 24 / 보스 50 (GDD §4.2), `run.battlesWon += 1`, `run.will = 전투 후 의지`.
- **승리 카드 보상 (ADR-011 근거 ② + ADR-027)**: 승리 오버레이에서 **3칸** 제시 → 1장
  선택(건너뛰기 가능). 칸 구성은 **대상 리뷰 2 + 내 장비 리뷰 1**이다.
  · 대상 리뷰 = `origin.enemy === 적 id` 또는 `origin.equipment ∈ 그 적 장비 이름`
  · 내 장비 리뷰 = `target === 'my_equipment'` (찬양·방어) — 상시 1칸 배정.
    찬양 카드는 origin 이 없어 대상 풀에 뜰 수 없고, 그 탓에 방어 축의 정규 획득 경로가
    없었다. **origin 을 주는 것이 아니라 보상 풀에만 넣는다** — 원산지는 여전히 영구 미발동.
  · 양쪽 다 미보유 필터. 한쪽이 비면 다른 쪽으로 칸을 채운다 →
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
