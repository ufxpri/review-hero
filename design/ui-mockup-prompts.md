# 화면 구도 목업 프롬프트 — 슬더스형 레이아웃 × 만물마켓 크롬

- 작성: 2026-08-03 · 계기: 에셋 컷아웃이 아니라 **게임 화면 전체**를 보고 싶다는 요청 + 슬더스 스타일 검토
- 지위: 검증용. 확정되면 `art-direction-v1.md` §2를 개정하고 ADR로 박제한다.
- 관계: `image-prompts-ready.md`(에셋 단품)와 **상호 보완**. 이 문서로 구도를 정하고, 그 안에 들어갈 그림은 저쪽에서 뽑는다.

---

## 0. 먼저 — 이 프롬프트로 나오는 건 "목업"이지 "UI"가 아니다

이미지 모델은 **읽을 수 있는 UI를 못 만든다.** 글자는 반드시 깨지고, 정렬은 픽셀 단위로 안 맞고, 같은 화면을 두 번 뽑으면 레이아웃이 달라진다.

그래서 목표를 정확히 잡는다:

| 이 프롬프트로 얻는 것 | 얻지 못하는 것 |
|---|---|
| 구도·비율·시선 흐름 | 실제 구현용 UI 에셋 |
| 분위기와 색 밸런스 | 읽을 수 있는 텍스트 |
| "이 배치가 재밌어 보이나" 판단 | 픽셀 단위 스펙 |

**모든 프롬프트에 "글자는 추상적 자리표시 획으로"** 를 넣었다. 깨진 한글이 나오는 것보다 낫고, 어차피 실제 UI는 HTML/CSS로 만들면서 §`image-prompts-ready.md`의 에셋을 끼워 넣는다.

---

## 1. 확정해야 할 갈림길 두 개

목업을 보고 판단할 것.

**① 화면비 — 가로 16:9 vs 세로 모바일**
기존 확정은 세로(최대 폭 760px, "한 손으로 스크롤하며 읽는 게임"). 슬더스형은 가로다. 가로로 가면 텍스트 읽는 양이 줄고 카드 조작감이 올라간다. 아래 §2에 **양쪽 다** 준비했다.

**② 커머스 비중이 어디까지 내려가나**
슬더스 레이아웃을 쓰면 "UI 전체가 쇼핑몰 앱"(ADR-010)은 성립하지 않는다. 커머스는 **표기·아이콘·용어**로 남는다 — 적 체력이 `★3.2 · 리뷰 1,210건`, 인텐트가 `📦 발송 예정`, 턴 종료가 `영업 마감`, 필력이 `✍`. 세계는 던전인데 HUD가 쇼핑몰인 셈이라, 톤 원칙("세계는 진지하고 규칙이 웃기다")과는 오히려 더 잘 맞는다.

---

## 2. S20 전투 화면 ★ 코어

### 목업 A — 슬더스형 가로 16:9 (본안)

```
Concept mockup of a roguelike deckbuilder battle screen, 16:9 landscape, full screen composition.

LAYOUT, strictly followed:
Upper two-thirds is a painterly dark fantasy scene. On the LEFT stands the player character:
a lean man in plain modern streetwear whose face is a soft grey unresolved void, as if his image
data failed to load. On the RIGHT stands the enemy: a colossal orc warrior in dented iron plate
holding a monstrous stone-headed war maul so heavy his wrist bends under it.
Floating directly ABOVE the orc is a small rectangular notification plaque with a parcel-box
pictogram and an abstract numeral — a delivery notice pinned in midair.
Beneath the orc, a horizontal segmented health bar and a small row of five star pictograms.
Beneath that, two small warning-colored chips.
Lower third is the interface band: five ornate rectangular playing cards fanned in a shallow arc
across the bottom center, angled and overlapping, each card face divided into a small illustration
panel on top and a block of abstract placeholder text lines below.
Bottom LEFT: a single glowing circular resource orb with a quill pictogram.
Bottom RIGHT: a wide rounded button plate.
Far bottom corners: two small stacked-card pile icons.

STYLE: hand-painted digital illustration, rich painterly brushwork, dark moody dungeon interior
receding into haze behind the characters, warm torchlight from the left, cool shadow.
Muted desaturated palette — iron grey, mossy olive, oxblood, damp stone — with one warm amber
accent used only on the interface elements.
UI panels are dark semi-transparent slabs with thin warm metal edging, sitting cleanly over the scene.

CRITICAL: no readable text anywhere. All labels, numbers and card text rendered as abstract
placeholder strokes and blocks. No watermark, no logo, no signature.
```

### 목업 B — 세로 모바일 (기존 확정안과의 비교용)

```
Concept mockup of a mobile roguelike deckbuilder battle screen, vertical 9:16, full screen composition.

LAYOUT, strictly followed:
Top: a slim status bar with small pictograms and abstract numerals.
Upper middle: a large framed portrait panel filling the width, containing a colossal orc warrior
in dented iron plate holding a monstrous stone-headed war maul, painted as a dark fantasy scene.
Directly below the portrait: a row of five star pictograms and a horizontal segmented bar.
Below that: two small warning-colored chips, then a single-row list item showing a weapon
pictogram with a small durability bar.
Floating over the top-right corner of the portrait: a small rectangular notification plaque
with a parcel-box pictogram.
Lower third: a fixed bottom sheet holding five vertical rectangular cards side by side,
each divided into a small illustration panel on top and abstract placeholder text lines below.
Bottom edge: a circular resource orb with a quill pictogram on the left, a wide rounded
button plate on the right.

STYLE: hand-painted digital illustration for the portrait scene, dark moody dungeon lighting,
muted desaturated palette with one warm amber accent. The surrounding interface is a clean,
modern, dark app shell — flat panels, generous padding, thin dividers — deliberately contrasting
with the painterly artwork inside the portrait frame.

CRITICAL: no readable text anywhere. All labels, numbers and card text rendered as abstract
placeholder strokes and blocks. No watermark, no logo, no signature.
```

### 목업 C — 가로형 + 보스전 (긴장도 확인용)

```
Concept mockup of a roguelike deckbuilder boss battle screen, 16:9 landscape, full screen composition.

LAYOUT, strictly followed:
Upper two-thirds: on the LEFT, small and dwarfed, the player character — a lean man in plain
modern streetwear whose face is a soft grey unresolved void. On the RIGHT, towering over him,
the boss: a tall gaunt figure in an immaculate charcoal frock coat whose face is a smooth
featureless expanse of pale bone-white, holding a great scythe whose blade is a fused stack of
compressed parchment.
Floating above the boss: two small rectangular notification plaques stacked vertically.
Beneath the boss: a long segmented health bar noticeably wider than a normal enemy's,
and a row of five star pictograms.
To the RIGHT EDGE of the screen, a narrow vertical column of three small stacked review-slip
panels, each a thin horizontal card with abstract placeholder text lines — other players' reviews
pinned to the boss.
Lower third: five ornate cards fanned in a shallow arc, a circular quill resource orb bottom left,
a wide rounded button plate bottom right.

STYLE: hand-painted digital illustration, vast dark hall receding into darkness, cold pale light
from high above, deep shadow. Muted palette — charcoal, bone white, oxblood — with one warm
amber accent on interface elements only. Oppressive scale, heavy atmosphere.

CRITICAL: no readable text anywhere. All labels and card text rendered as abstract placeholder
strokes and blocks. No watermark, no logo, no signature.
```

---

## 3. S11 맵 — 배송 경로

```
Concept mockup of a roguelike run map screen, 16:9 landscape, full screen composition.

LAYOUT, strictly followed:
A branching path graph fills the center of the screen, reading from the BOTTOM edge upward to
the TOP edge across six horizontal tiers. Each tier holds two to four circular node medallions
connected by curving lines to the tier above. Node medallions carry simple pictograms:
a cardboard box, a box with a chevron, a shelf rack, a headset, a bell, and at the very top a
single large medallion showing an office tower.
The path already travelled is drawn as a solid bright line with a small marker at the current
position; paths not yet taken are dim.
LEFT EDGE: a narrow vertical panel with a small character portrait and abstract stat rows.
TOP EDGE: a slim status bar with pictograms and abstract numerals.

STYLE: hand-painted digital illustration. Background is a vast dark fantasy dungeon interior
receding upward into haze — colossal stone arches, worn flagstone, guttering torchlight — kept
muted and low-contrast so the path graph reads clearly on top of it.
Node medallions are warm brass discs with thin metal rims. Muted desaturated palette with one
warm amber accent.

CRITICAL: no readable text anywhere. All labels rendered as abstract placeholder strokes.
No watermark, no logo, no signature.
```

---

## 4. S30 카드 보상 — 적립 쿠폰 3택1

```
Concept mockup of a card reward selection screen for a deckbuilder, 16:9 landscape.

LAYOUT, strictly followed:
The scene is dimmed and blurred behind a dark overlay. Centered in the upper area, a short
horizontal banner plate. Below it, THREE ornate rectangular playing cards displayed side by side,
evenly spaced, upright and facing the viewer, the middle one slightly raised and glowing as if
hovered. Each card face is divided into a small painted illustration panel on top and a block of
abstract placeholder text lines below, with a small star row and a tiny quill pictogram in the corner.
Below the three cards, a single narrow rounded button plate, centered.

STYLE: hand-painted digital illustration. Cards have thick warm brass frames with a subtle
parchment texture on the face. Soft warm rim light behind the raised middle card. Everything else
falls into deep shadow so the three cards dominate completely.
Muted desaturated palette with one warm amber accent.

CRITICAL: no readable text anywhere. All card text rendered as abstract placeholder strokes
and blocks. No watermark, no logo, no signature.
```

---

## 5. S13 덱 뷰어 — 내가 쓴 리뷰 목록

```
Concept mockup of a deck viewer screen for a deckbuilder, 16:9 landscape.

LAYOUT, strictly followed:
A dark full-screen overlay panel. Along the TOP, a row of four small tab plates.
Filling the body, a grid of twelve ornate rectangular playing cards laid out in three rows of
four, evenly spaced with generous gutters, all upright and facing the viewer. Each card face is
divided into a small painted illustration panel on top and abstract placeholder text lines below,
with a small star row in the corner. A few cards carry a small brass corner seal.
RIGHT EDGE: a narrow vertical scroll indicator.
BOTTOM RIGHT: a small rounded close button plate.

STYLE: hand-painted digital illustration. Cards have warm brass frames over parchment faces.
Even soft lighting across the grid, no dramatic shadow. Background is a deep neutral dark slab
with a faint paper-fiber texture. Muted desaturated palette with one warm amber accent.

CRITICAL: no readable text anywhere. All card text rendered as abstract placeholder strokes
and blocks. No watermark, no logo, no signature.
```

---

## 6. S31 상점 — 진열대

```
Concept mockup of a shop screen for a roguelike deckbuilder, 16:9 landscape.

LAYOUT, strictly followed:
Centered in the upper LEFT, a merchant figure: a wiry goblin peddler in a crooked patchwork
leather jerkin, standing behind a low counter, arms open in a sales gesture.
Occupying the RIGHT two-thirds, a tall wooden shelf rack of three levels. The top two levels hold
five ornate rectangular cards standing upright in a row; the bottom level holds three small
item objects — a flask, a folded parchment, a dagger. Each item has a small oval price tag plate
hanging beneath it bearing abstract placeholder marks.
BOTTOM LEFT: a small circular coin pictogram with an abstract numeral beside it.
BOTTOM RIGHT: a wide rounded button plate.

STYLE: hand-painted digital illustration. A cramped warm dungeon alcove lit by hanging lanterns,
deep shadow at the edges. The shelf and counter are worn dark wood with brass fittings.
Muted desaturated palette with warm amber lantern light as the accent.

CRITICAL: no readable text anywhere. All labels and price tags rendered as abstract placeholder
strokes. No watermark, no logo, no signature.
```

---

## 7. S01 타이틀 화면

```
Concept mockup of a video game title screen, 16:9 landscape, full screen composition.

LAYOUT, strictly followed:
The full frame is a painterly key visual: a colossal dungeon gate of black stone and iron rising
out of drifting mist, carved with ancient sigils, torch sconces guttering along its flanks,
monumental and oppressive, a broken causeway leading up to it under a storm-dark sky.
Nailed at eye level onto the ancient iron of the gate is one small, mundane, perfectly ordinary
delivery notice slip — a plain pale rectangle of modern paper, crisp and slightly curling at one
corner, entirely out of place, lit just enough to be found.
Overlaid across the UPPER CENTER, a large empty horizontal plate reserved for a logo — leave it
blank. Below it, LEFT-ALIGNED in the lower left quadrant, a vertical stack of four narrow
rounded menu button plates.
BOTTOM RIGHT corner: a tiny abstract version mark.

STYLE: hand-painted digital illustration, awe and dread played completely straight.
Muted desaturated palette — iron grey, storm blue, damp stone — with one warm torchlight accent.
Menu plates are dark semi-transparent slabs with thin warm metal edging.

CRITICAL: no readable text anywhere, and the logo plate must be left completely EMPTY.
All menu labels rendered as abstract placeholder strokes. No watermark, no signature.
```

---

## 8. S23 전투 종료 — 회원 탈퇴(패배)

```
Concept mockup of a game over screen for a roguelike, 16:9 landscape.

LAYOUT, strictly followed:
The scene behind is dimmed almost to black. Centered, a single empty steel wire shopping cart
standing alone in a vast dark void, tipped very slightly, one wheel turned outward, completely
empty, lit by a shaft of cold pale light falling from far above.
Overlaid across the UPPER CENTER, a wide horizontal plate. Below the cart, a compact block of
three short abstract statistic rows, centered. At the BOTTOM CENTER, two narrow rounded button
plates side by side.

STYLE: hand-painted digital illustration. Cold, still, and final. Almost monochrome —
iron grey and cold blue with a single faint warm point. Enormous negative space around the cart.

CRITICAL: no readable text anywhere. All labels and statistics rendered as abstract placeholder
strokes. No watermark, no logo, no signature.
```

---

## 9. 뽑는 순서와 판단 기준

1. **§2 목업 A(가로 슬더스형)를 먼저.** 이게 본안이다.
2. **§2 목업 B(세로 모바일)를 나란히.** 기존 확정안이 정말 못한지 눈으로 확인한다.
3. 마음에 드는 쪽으로 **§2 목업 C(보스전)** → §3 맵 → §4 보상 순으로 확장.

**판단 기준 네 개**
1. 카드가 화면에서 **주인공**인가 — 덱빌더는 카드가 주인공이어야 한다
2. 적의 **발송 예정(인텐트)** 이 한눈에 들어오는가 — 이 게임 puzzle의 입력값이다
3. 판타지 그림과 커머스 HUD가 **같이 있어도 안 촌스러운가**
4. 세로/가로 중 어느 쪽이 "리뷰를 읽는 게임"에 맞는가

## 10. 확정되면 개정할 문서

- `art-direction-v1.md` §2.3 — "모바일 세로 우선, 최대 폭 760px" → 화면비 결정 반영
- `ADR-010` — "UI 전체가 쇼핑몰 앱" 범위 축소 (커머스는 표기·아이콘·용어 층으로)
- 화면 인벤토리 S01~S52 — 레이아웃 전제 재검토
- `prototype/index.html` — 현재는 세로 스크롤 상품 페이지 구조
