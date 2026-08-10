# 복붙용 프롬프트 전량 — 「만물마켓」 (판타지 80 / 커머스 20)

- 파생: `image-prompts-v1.md` (설계 정본). 이 문서는 **`[STYLE]` 블록을 미리 합쳐 놓은 실행용 사본**이다.
- **코드 블록 하나 = 복붙 1회.** 접두를 따로 붙일 필요 없다.
- 전량 **58개**. 우선순위 순으로 배열했다.

---

## ⚠ 시작 전 3가지

**1. 네거티브 프롬프트** — 지원하는 툴이면 매번 같이 넣는다. 특히 `white background / product photography / catalog shot`이 빠지면 초안(커머스 상품컷) 쪽으로 끌려간다.

```
text, letters, words, korean characters, chinese characters, watermark, signature, logo,
ui, buttons, frames, borders, health bar, price tag, label,
anime, manga, cel shading, chibi, cute, comedic expression, goofy grin, wacky pose, caricature,
flat vector, clip art, low detail, plastic toy look,
white background, plain studio backdrop, product photography, catalog shot,
busy background, environment, landscape, multiple subjects, crowd,
gore, blood, dismemberment
```

**2. 순서** — §1의 **C02 오크를 제일 먼저** 뽑아 스타일 앵커로 확정하고, 나머지는 그 결과물을 **스타일 참조 이미지**(image reference / `--sref` / style reference)로 물려서 생성한다. 텍스트 프롬프트만으로는 58장의 톤이 반드시 어긋난다.

**3. 한글 금지** — 이미지에 글자가 들어가면 재생성한다. 모든 텍스트는 코드로 얹는다.

---

# 1. 1차 검증 세트 — 이 6장 먼저

스타일이 마음에 드는지부터 판단한다. 판단 기준: ① 판타지 게임으로 보이는가 ② 6장이 같은 세계로 보이는가 ③ 약점 태그가 이미지에서 읽히는가 ④ 이 위에 별점 UI를 얹으면 웃길 것 같은가.

### ① C02 오크 중량 전사 — 스타일 앵커 (이거 먼저)

```
High-quality dark fantasy character art for a video game.
Detailed painterly 3D render with rich physical materials: pitted iron, oiled leather,
coarse woven cloth, chipped stone, tarnished brass, weathered bone.
Dramatic controlled lighting — strong cool key light from the upper left, deep shadow fill,
a warm rim light along the right edge separating the figure from the background.
Moody desaturated palette: iron grey, mossy olive, oxblood, damp stone, with one saturated accent color.
Subject isolated on a simple dark atmospheric gradient backdrop with soft volumetric haze.
No environment, no props, no floor detail, no scenery.
Full body, centered, front-facing three-quarter stance, standing still and composed,
presented for display rather than caught mid-action.
Serious and imposing. Real weight, real threat. The rendering itself contains no comedy.
Vertical 4:5 composition, subject fills about 80 percent of the frame.

A colossal orc warrior, slab-muscled beneath scarred grey-green hide, heavy tusked jaw,
small deep-set eyes under a heavy brow, staring flat and unblinking.
He wears layered riveted iron plate over the shoulders and chest, blackened by fire and dented
from use, the straps cutting deep into the muscle beneath from sheer weight.
In one hand he holds a monstrous stone-headed war maul, the head nearly the size of his own torso,
lashed to a short thick haft with iron banding. The weight of it is unmistakable — his wrist is
bent under the load, the tendons of his forearm standing out, his stance planted wide and deep,
the arm holding it hanging noticeably lower than the other.
Immense, patient, and immovable. A siege engine that happens to be alive.
```

### ② C02 오크 — 스타일 B (하이 판타지 A/B 비교용)

```
High-quality high fantasy character art for a video game.
Detailed painterly 3D render with rich physical materials: polished iron, oiled leather,
woven cloth, carved stone, warm brass.
Luminous even lighting — broad soft key light from the upper left, luminous pale mist filling
the shadows, a clean bright rim separating the figure from the background.
Rich saturated palette: warm bronze, deep green, russet, sunlit gold, with clear color contrast.
Subject isolated on a luminous pale mist backdrop with soft atmospheric depth.
No environment, no props, no floor detail, no scenery.
Full body, centered, front-facing three-quarter stance, standing still and composed,
presented for display rather than caught mid-action.
Heroic and imposing. Real weight, real presence. The rendering itself contains no comedy.
Vertical 4:5 composition, subject fills about 80 percent of the frame.

A colossal orc warrior, slab-muscled beneath scarred grey-green hide, heavy tusked jaw,
small deep-set eyes under a heavy brow, staring flat and unblinking.
He wears layered riveted iron plate over the shoulders and chest, blackened by fire and dented
from use, the straps cutting deep into the muscle beneath from sheer weight.
In one hand he holds a monstrous stone-headed war maul, the head nearly the size of his own torso,
lashed to a short thick haft with iron banding. The weight of it is unmistakable — his wrist is
bent under the load, the tendons of his forearm standing out, his stance planted wide and deep,
the arm holding it hanging noticeably lower than the other.
Immense, patient, and immovable. A siege engine that happens to be alive.
```

### ③ C01 고블린 잡상인 — 약점 #마감이 눈에 보이는지

```
High-quality dark fantasy character art for a video game.
Detailed painterly 3D render with rich physical materials: pitted iron, oiled leather,
coarse woven cloth, chipped stone, tarnished brass, weathered bone.
Dramatic controlled lighting — strong cool key light from the upper left, deep shadow fill,
a warm rim light along the right edge separating the figure from the background.
Moody desaturated palette: iron grey, mossy olive, oxblood, damp stone, with one saturated accent color.
Subject isolated on a simple dark atmospheric gradient backdrop with soft volumetric haze.
No environment, no props, no floor detail, no scenery.
Full body, centered, front-facing three-quarter stance, standing still and composed,
presented for display rather than caught mid-action.
Serious and imposing. Real weight, real threat. The rendering itself contains no comedy.
Vertical 4:5 composition, subject fills about 80 percent of the frame.

A goblin. A small green-skinned goblinoid monster, not a human — bright mossy green skin, huge
long pointed ears swept back from a bald knobbed skull, a long hooked nose, a wide mouth crowded
with small crooked fangs, large yellow eyes under a heavy brow. Short and stunted, chest-high to
a man, hunched and wiry, all sinew stretched over sharp bone.
This little green goblin creature is draped in layered scavenged gear, all of it visibly badly made —
a patchwork leather jerkin stitched from mismatched hides, its seams burst wide open along one
shoulder with the raw stitching showing through, loose threads and frayed ends hanging everywhere,
rivets popped half out of the leather and dangling, a strap mended with knotted twine where a buckle
should be. A cheap curved dagger hangs at his belt, its pommel visibly loose and canted off the tang.
He stands his ground with the contained menace of something that has survived by being underestimated.
Not comical. Small, filthy, and genuinely dangerous.
```

### ④ C06 답글 없는 사장 (1막 보스) — 판타지 80의 효과가 가장 크게 나타날 자리

```
High-quality dark fantasy character art for a video game.
Detailed painterly 3D render with rich physical materials: pitted iron, oiled leather,
coarse woven cloth, chipped stone, tarnished brass, weathered bone.
Dramatic controlled lighting — strong cool key light from the upper left, deep shadow fill,
a warm rim light along the right edge separating the figure from the background.
Moody desaturated palette: iron grey, mossy olive, oxblood, damp stone, with one saturated accent color.
Subject isolated on a simple dark atmospheric gradient backdrop with soft volumetric haze.
No environment, no props, no floor detail, no scenery.
Full body, centered, front-facing three-quarter stance, standing still and composed,
presented for display rather than caught mid-action.
Serious and imposing. Real weight, real threat. The rendering itself contains no comedy.
Vertical 4:5 composition, subject fills about 80 percent of the frame.

A faceless figure. Its head is a completely smooth blank egg of pale bone-white porcelain — no eyes,
no nose, no mouth, no brow, no features of any kind, a polished unpainted mannequin head. There is
nothing there to appeal to.
The body beneath is towering and gaunt, dressed in an immaculate charcoal frock coat tailored sharp
and pressed without a single crease, worn over dark plate at the shoulders — part administrator,
part warlord. A small brass name plate is pinned to the lapel, its surface deliberately blank and
unengraved. Both gloved hands rest on the haft of an enormous great scythe — a huge hooked reaper's
scythe standing upright beside him, taller than he is, its long wooden haft planted on the ground
and its vast curved blade sweeping high overhead. That blade is not forged metal but a thick fused
stack of compressed contract parchment — dozens of laminated yellowed sheets pressed and hardened
into a curved cutting edge, the layered paper strata clearly visible along the spine, the edges
faintly stirring as though in a draft that isn't there.
Perfectly still, perfectly composed, absolutely silent. Corporate, immovable, and terrifying
in the way that something which will simply never answer you is terrifying.
```

### ⑤ P02 초대형 둔기 — 무생물 단독컷이 아이템으로 읽히는지

```
High-quality dark fantasy item art for a video game.
Detailed painterly 3D render with rich physical materials: pitted iron, oiled leather,
coarse woven cloth, chipped stone, tarnished brass, weathered bone.
Dramatic controlled lighting — strong cool key light from the upper left, deep shadow fill,
a warm rim light along the right edge separating the object from the background.
Moody desaturated palette: iron grey, mossy olive, oxblood, damp stone, with one saturated accent color.
Object isolated on a simple dark atmospheric gradient backdrop with soft volumetric haze.
No environment, no props, no floor detail, no scenery.
Single object only, no hands, no character, floating isolated and centered,
lit as a hero item shot. Square 1:1 composition.

A monstrous two-handed war maul, a rough hewn granite head easily the size of a human torso
bound with heavy iron banding onto a short thick wooden haft. Absurdly head-heavy proportions —
almost all head, barely any handle. Chipped along the striking face, blood-darkened in the
crevices, and enormously, obviously heavy.
```

### ⑥ B01 가짜 광고 배너 — 개그가 실제로 웃긴지

```
Horizontal promotional banner artwork, 3:1 aspect ratio.
Dark fantasy subject rendered seriously and in full detail, placed on the left third,
lit as a hero shot with a strong cool key light and warm rim light,
against a dark atmospheric gradient with soft volumetric haze.
The right two-thirds of the frame is kept visually quiet and near-empty for text overlay.
Moody desaturated palette with one saturated accent color.
No text, no letters, no logos, no price tags anywhere in the image.

A legendary greatsword, ornate and rune-etched along the fuller, its blade faintly luminous,
standing upright and magnificent on the left third of the frame — and on the ground beside it,
a plain flattened cardboard shipping box, mundane and slightly creased.
```

---

# 2. 적 히어로 아트 — 나머지 (S20 전투 · S11 맵 · S13)

> 아래부터는 지면상 `[STYLE]`을 첫 문단으로 포함해 두었다. 그대로 복사하면 된다.

### C03 엘프 이펙트 마법사 (E03 · 약점 #이펙트 #연비)

```
High-quality dark fantasy character art for a video game.
Detailed painterly 3D render with rich physical materials: pitted iron, oiled leather,
coarse woven cloth, chipped stone, tarnished brass, weathered bone.
Dramatic controlled lighting — strong cool key light from the upper left, deep shadow fill,
a warm rim light along the right edge separating the figure from the background.
Moody desaturated palette: iron grey, mossy olive, oxblood, damp stone, with one saturated accent color.
Subject isolated on a simple dark atmospheric gradient backdrop with soft volumetric haze.
No environment, no props, no floor detail, no scenery.
Full body, centered, front-facing three-quarter stance, standing still and composed,
presented for display rather than caught mid-action.
Serious and imposing. Real weight, real threat. The rendering itself contains no comedy.
Vertical 4:5 composition, subject fills about 80 percent of the frame.

A high elf archmage holding up an enormous ornate wizard staff, taller than he is, gripped upright in
both hands. The staff is the loudest thing in the picture: its tip is a massive top-heavy ball of
glowing faceted crystals in clashing colors, wrapped in gold filigree scrollwork with charms and
metal rings dangling off it — more chandelier than weapon, and far too heavy for its own shaft.
An enormous blinding fireworks display of wasted arcane light erupts out of that crystal ball and
fills the upper half of the picture — wild spiralling arcs, whipping ribbons of raw energy, great
showers of fat golden sparks raining down and dying uselessly in the air, blazing glare washing over
everything — vastly more spectacle than any spell could possibly require.
He is tall and slender with porcelain-pale skin, long straight silver hair, sharply pointed ears and
severe elegant features, his expression cold, detached and faintly bored, entirely unimpressed by
the fireworks he is producing. His layered ceremonial robes of deep indigo and gold are
extravagantly embroidered, with trailing ribbons, ornamental tassels and floor-length sleeves that
serve no practical purpose. Magnificent, expensive, and burning far more power than it produces.
```

### C04 야매 배송 도적 (E04 · 약점 #속도 · 은신)

```
High-quality dark fantasy character art for a video game.
Detailed painterly 3D render with rich physical materials: pitted iron, oiled leather,
coarse woven cloth, chipped stone, tarnished brass, weathered bone.
Dramatic controlled lighting — strong cool key light from the upper left, deep shadow fill,
a warm rim light along the right edge separating the figure from the background.
Moody desaturated palette: iron grey, mossy olive, oxblood, damp stone, with one saturated accent color.
Subject isolated on a simple dark atmospheric gradient backdrop with soft volumetric haze.
No environment, no props, no floor detail, no scenery.
Full body, centered, front-facing three-quarter stance, standing still and composed,
presented for display rather than caught mid-action.
Serious and imposing. Real weight, real threat. The rendering itself contains no comedy.
Vertical 4:5 composition, subject fills about 80 percent of the frame.

A hooded delivery courier who is also a thief, standing in deep darkness, half dissolving into it.
Slung across his chest is a huge overstuffed courier's mail bag, bulging with battered brown paper
parcels and crushed cardboard packages jammed in at every angle, several of them spilling out; its
shoulder strap has snapped and been retied in a crude knot instead of a buckle. Two more burst
parcels lie dropped and trampled at his feet, their contents scattered.
His face is entirely swallowed by the black shadow inside a deep drawn hood — no features readable
at all, only a hard jawline and the faint cold glint of one eye. He is built purely for speed:
narrow wiry frame, close-cut charcoal-black travel leathers, tightly wrapped forearms, soft-soled
boots, no armor anywhere on him — no plate, no pauldrons, no mail.
He holds a single curved shortblade low and ready. Its matching twin is stabbed into the ground far
off to one side, apart from him, its grip wrapping already unravelling — the pair clearly did not
arrive together. Everything about him is fast, and everything about him is hastily made.
Coiled, silent, one step from vanishing into the dark.
```

### C05 나르시시스트 기사 (E05 · 약점 #디자인 #감성)

```
High-quality dark fantasy character art for a video game.
Detailed painterly 3D render with rich physical materials: pitted iron, oiled leather,
coarse woven cloth, chipped stone, tarnished brass, weathered bone.
Dramatic controlled lighting — strong cool key light from the upper left, deep shadow fill,
a warm rim light along the right edge separating the figure from the background.
Moody desaturated palette: iron grey, mossy olive, oxblood, damp stone, with one saturated accent color.
Subject isolated on a simple dark atmospheric gradient backdrop with soft volumetric haze.
No environment, no props, no floor detail, no scenery.
Full body, centered, standing still and composed,
presented for display rather than caught mid-action.
Serious and imposing. Real weight, real presence. The rendering itself contains no comedy.
Vertical 4:5 composition, subject fills about 80 percent of the frame.

A bare-headed man. His head is uncovered and his handsome symmetrical face is fully visible and
brightly lit — clean-shaven, flawless, high cheekbones, long wavy dark hair swept back to his
shoulders and perfectly arranged, chin lifted, gaze angled deliberately past the viewer toward some
imagined audience, wearing the faint self-satisfied half-smile of a man who knows he is being looked
at. Nothing covers his head or his face.
From the neck down he is encased in full gold-plated plate armor, mirror-polished to a blinding
showroom shine, catching and throwing the key light from every surface. The armor is lavishly
ornamented — engraved scrollwork, decorative fluting, oversized pauldrons shaped for silhouette
rather than defense. The plating is visibly thin, the articulated joints ornamental, built to be
looked at rather than struck. Not a single scratch anywhere on it.
He stands full length, head to boots inside the frame, one hip cocked in a practiced heroic stance,
one hand resting on his hip. Radiant, immaculate, and entirely occupied with being seen.
```

### C07 플레이어 아바타 — 시네마틱용 실체 (S51·S52 전용)

> 게임 내 기본 아바타는 **이미지 생성 없음** — 회색 실루엣 + 깨진 이미지 아이콘 + 영원히 도는 로딩 인디케이터를 CSS로 만든다. '평가 불가'를 UI가 직접 연기한다.

```
High-quality dark fantasy character art for a video game.
Detailed painterly 3D render with rich physical materials.
Dramatic controlled lighting — strong cool key light from the upper left, deep shadow fill,
a warm rim light along the right edge separating the figure from the background.
Moody desaturated palette: iron grey, mossy olive, oxblood, damp stone.
Subject isolated on a simple dark atmospheric gradient backdrop with soft volumetric haze.
No environment, no props, no floor detail, no scenery.
Full body, centered, front-facing, standing still and composed.
Vertical 4:5 composition, subject fills about 80 percent of the frame.

A lean man in his thirties in plain modern streetwear — worn hoodie and jacket — standing
incongruously amid the dark fantasy lighting, arms at his sides, facing the viewer.
His face and upper head are rendered as a soft grey unresolved void, as though the image data
for him failed to load: not smoke, not shadow, but flat missing information with a faint
pixel-grid fringe at its boundary.
Everything else about him is rendered in full detail. Ordinary, unremarkable, and unreadable.
```

### C09 플레이어 뒷모습 — 전투 화면 좌하단 (포켓몬 구도)

전투 화면의 주인공은 **뒤에서 본다.** 얼굴이 보이지 않으므로 '평가 불가'를 억지 연출 없이
그대로 성립시킨다(C07의 "얼굴이 로딩 실패한 공백"을 대체). 깃털펜과 양피지를 들려
**리뷰어의 유쾌한 필력**을 실루엣만으로 드러낸다.

> **1차 생성 실패 기록**: "VIEWED STRICTLY FROM BEHIND"만으로는 모델이 무시하고 얼굴이 보이는
> 3/4 측면을 그린다. 뒷모습은 확산 모델이 가장 잘 어기는 지시라 **첫 문장부터 반복해서 못 박고**,
> 네거티브에 `face, facial features, eyes, nose, mouth, looking at camera, front view, profile view`를
> 반드시 추가한다. 아래는 그 반영본.

```
Rear view from directly behind. Back of the head only. The face is not visible at all.
High-quality dark fantasy character art for a video game.
Detailed painterly 3D render with rich physical materials.
The subject stands with his back fully turned to the camera, facing away into the scene.
We see the back of his skull, the back of his neck, and his shoulder blades. No face. No profile.
Cool key light from the front-right beyond him, so he is rim-lit along the shoulders and the
side facing us falls into deep shadow — a foreground silhouette.
Moody desaturated palette: iron grey, mossy olive, oxblood, damp stone, with one warm amber accent.
Isolated on a plain flat dark backdrop, no environment, no floor, no scenery.
Cropped at the waist, close foreground framing, subject fills the frame.
Vertical 4:5 composition.

A lean man in a worn modern hooded jacket, hood down, short dark hair, shoulders loose and
relaxed, seen entirely from behind. His right hand is raised out to the side holding a long pale
quill pen, poised just about to write. Under his left arm he carries a thick roll of parchment,
its edge unfurling. He stands casually with his weight on one leg, entirely unbothered — the
back of someone who has done this many times and is already composing the opening line.
```

**이 카드 전용 추가 네거티브** (기존 네거티브에 이어 붙인다):
```
face, facial features, eyes, nose, mouth, beard, looking at camera, looking back over shoulder,
front view, three-quarter view, profile view, turned head, visible skin on face
```

---

# 3. 구성품(장비) 아트 — S20 구성품 · S31 진열대

> 5장 모두 **인물 없이 사물만**, 정사각 1:1.

### P01 짝퉁 단검

```
High-quality dark fantasy item art for a video game.
Detailed painterly 3D render with rich physical materials: pitted iron, oiled leather,
coarse woven cloth, chipped stone, tarnished brass.
Dramatic controlled lighting — strong cool key light from the upper left, deep shadow fill,
a warm rim light along the right edge separating the object from the background.
Moody desaturated palette: iron grey, mossy olive, oxblood, damp stone, with one saturated accent color.
Object isolated on a simple dark atmospheric gradient backdrop with soft volumetric haze.
No environment, no props, no floor detail, no scenery.
Single object only, no hands, no character, floating isolated and centered,
lit as a hero item shot. Square 1:1 composition.

A cheap counterfeit dagger. The pommel sits loose and slightly canted off the tang, the crossguard
is stamped from thin sheet metal rather than forged, the leather grip wrap is peeling away at one
end, and the blade edge is unevenly ground. Convincing at arm's length, obviously fake up close.
```

### P03 과장된 지팡이

```
High-quality dark fantasy item art for a video game.
Detailed painterly 3D render with rich physical materials: polished crystal, gold filigree,
carved wood, tarnished brass.
Dramatic controlled lighting — strong cool key light from the upper left, deep shadow fill,
a warm rim light along the right edge separating the object from the background.
Moody desaturated palette: iron grey, mossy olive, oxblood, damp stone, with one saturated accent color.
Object isolated on a simple dark atmospheric gradient backdrop with soft volumetric haze.
No environment, no props, no floor detail, no scenery.
Single object only, no hands, no character, floating isolated and centered,
lit as a hero item shot. Square 1:1 composition.

An extravagantly ornate wizard staff standing upright, crusted with dozens of faceted crystals,
gold filigree scrollwork and dangling charms — visibly top-heavy and impractical. Arcane light
spills wastefully from the crystals in bright bleeding arcs that dissipate into drifting motes,
far more radiance than any function requires.
```

### P04 삐걱거리는 쌍단검

```
High-quality dark fantasy item art for a video game.
Detailed painterly 3D render with rich physical materials: pitted iron, oiled leather,
frayed cord, tarnished brass.
Dramatic controlled lighting — strong cool key light from the upper left, deep shadow fill,
a warm rim light along the right edge separating the objects from the background.
Moody desaturated palette: iron grey, mossy olive, oxblood, damp stone, with one saturated accent color.
Objects isolated on a simple dark atmospheric gradient backdrop with soft volumetric haze.
No environment, no props, no floor detail, no scenery.
No hands, no character. Square 1:1 composition.

A pair of matched curved shortblades, deliberately NOT arranged as a pair — one placed at the
center of the frame, the other pushed far off to the edge, as though the two halves arrived in
separate shipments. The grip wrappings are unravelling on both, the rivets visibly loose,
the blades nicked along their edges.
```

### P05 도금 갑옷

```
High-quality dark fantasy item art for a video game.
Detailed painterly 3D render with rich physical materials: mirror-polished gold plating,
engraved metal, thin warped sheet.
Dramatic controlled lighting — strong cool key light from the upper left, deep shadow fill,
a warm rim light along the right edge separating the object from the background.
Moody desaturated palette: iron grey, damp stone, deep shadow, with brilliant gold as the accent.
Object isolated on a simple dark atmospheric gradient backdrop with soft volumetric haze.
No environment, no props, no floor detail, no scenery.
Single object only, no hands, no character, suspended on an invisible stand and centered,
lit as a hero item shot. Square 1:1 composition.

A gold-plated cuirass, mirror-polished to a blinding showroom shine and lavishly engraved with
decorative scrollwork. The plating is visibly thin and the edges slightly warped — clearly
decorative rather than protective. Not a single battle scar anywhere on it. Immaculately clean.
```

### P06 노예 계약서 낫

```
High-quality dark fantasy item art for a video game.
Detailed painterly 3D render with rich physical materials: compressed laminated parchment,
dark polished wood, tarnished brass.
Dramatic controlled lighting — strong cool key light from the upper left, deep shadow fill,
a warm rim light along the right edge separating the object from the background.
Moody desaturated palette: iron grey, aged bone-white, oxblood, damp stone, with one saturated accent color.
Object isolated on a simple dark atmospheric gradient backdrop with soft volumetric haze.
No environment, no props, no floor detail, no scenery.
Single object only, no hands, no character, standing upright and centered,
lit as a hero item shot. Square 1:1 composition.

A great scythe standing upright. Its blade is not forged metal but a thick fused stack of
compressed legal parchment — dozens of laminated sheets pressed into a hardened curved cutting
edge, the layered paper strata clearly visible along the spine, the edges yellowed and faintly
stirring as though in a draft that isn't there. The haft is dark polished wood.
Bureaucratic and lethal.
```

---

# 4. 전생 상품 4종 — 시작 덱 원산지 (S13 덱 뷰어)

> **카드 체계 v2에서 새로 확정된 에셋.** 시작 덱 12장의 원산지다. 현대 공산품을 판타지 유물처럼 조명하는 것 자체가 개그이므로 **스타일을 낮추지 않는다.**

### Z01 폭발한 보조배터리 (주인공을 죽인 물건)

```
High-quality dark fantasy item art for a video game.
Detailed painterly 3D render with rich physical materials.
Dramatic controlled lighting — strong cool key light from the upper left, deep shadow fill,
a warm rim light along the right edge separating the object from the background.
Moody desaturated palette: iron grey, oxblood, damp stone, with one saturated accent color.
Object isolated on a simple dark atmospheric gradient backdrop with soft volumetric haze.
No environment, no props, no floor detail, no scenery.
Single object only, no hands, no character, floating isolated and centered,
lit with the gravity of a legendary relic. Square 1:1 composition.

A cheap mass-market USB power bank, a plain rectangular plastic brick, its casing split and
bulged outward from internal swelling, one corner scorched black and blistered, a frayed charging
cable still attached. Utterly mundane, utterly ordinary — and lit like a cursed artifact.
```

### Z02 무선이어폰

```
High-quality dark fantasy item art for a video game.
Detailed painterly 3D render with rich physical materials.
Dramatic controlled lighting — strong cool key light from the upper left, deep shadow fill,
a warm rim light along the right edge separating the object from the background.
Moody desaturated palette: iron grey, oxblood, damp stone, with one saturated accent color.
Object isolated on a simple dark atmospheric gradient backdrop with soft volumetric haze.
No environment, no props, no floor detail, no scenery.
Single object only, no hands, no character, floating isolated and centered,
lit with the gravity of a legendary relic. Square 1:1 composition.

A cheap pair of wireless earbuds in their charging case, the case lid hanging visibly ajar and
unable to close properly, its hinge splayed. One earbud sits inside, the other lies loose beside
it. Scuffed plastic, a smudged charging contact. Utterly mundane — and lit like a sacred reliquary.
```

### Z03 배달음식

```
High-quality dark fantasy item art for a video game.
Detailed painterly 3D render with rich physical materials.
Dramatic controlled lighting — strong cool key light from the upper left, deep shadow fill,
a warm rim light along the right edge separating the object from the background.
Moody desaturated palette: iron grey, oxblood, damp stone, with one saturated accent color.
Object isolated on a simple dark atmospheric gradient backdrop with soft volumetric haze.
No environment, no props, no floor detail, no scenery.
Single object only, no hands, no character, floating isolated and centered,
lit with the gravity of a legendary relic. Square 1:1 composition.

A single-use plastic food delivery container, lid slightly askew, condensation beaded on the
inside, the contents cold and congealed and unappetizing. A crumpled plastic bag handle still
knotted around it. Utterly mundane — and lit like an offering on an altar.
```

### Z04 조립식 의자

```
High-quality dark fantasy item art for a video game.
Detailed painterly 3D render with rich physical materials.
Dramatic controlled lighting — strong cool key light from the upper left, deep shadow fill,
a warm rim light along the right edge separating the object from the background.
Moody desaturated palette: iron grey, oxblood, damp stone, with one saturated accent color.
Object isolated on a simple dark atmospheric gradient backdrop with soft volumetric haze.
No environment, no props, no floor detail, no scenery.
Single object only, no hands, no character, floating isolated and centered,
lit with the gravity of a legendary relic. Square 1:1 composition.

A cheap flat-pack particleboard chair, assembled slightly wrong — one leg visibly out of true,
the seat sitting at a faint tilt, a bolt head protruding where it should be flush. Three loose
screws rest on the ground beneath it, left over. Utterly mundane — and lit like a throne.
```

---

# 5. 배경 · 키비주얼 · 결과 화면

## 5.1 전투 무대 배경 플레이트 (S20 · 1344×768 고정)

> 전투 화면은 이 그림 위에 좌측 주인공 · 중앙우측 적 · 양옆 장비 패널 · 하단 손패를 얹고,
> 그 위에 다시 비네트(`radial-gradient(120% 92% at 58% 40%)`)를 덧씌운다. 그래서 세 가지가 규칙이다 —
> **① 중앙~중앙우측 바닥은 비워 둔다**(적이 설 자리) **② 구조물·디테일은 좌우 가장자리로 몰고 가장자리는 어둠에 잠긴다**
> **③ 인물·생물은 한 명도 넣지 않는다.** 일러스트가 아니라 연극 무대의 배경막이다.
>
> 캐릭터 프롬프트와 달리 **장소 서술을 맨 앞**에 둔다. 스타일 블록을 앞세우면 공간이 희석돼 인물 그림으로 끌려간다.
> 생성 시 네거티브 추가 권장: `person, people, character, creature, figure, portrait, large object in center, statue, foreground obstruction`
> 세피아 단색으로 쏠리면 `sepia, monochrome, warm light` 를, 현대 인테리어로 끌려가면
> `modern interior, contemporary architecture, minimalist, boutique, showroom, recessed spotlights` 를 더한다.
>
> 채택본 (juggernautXL · 1344x768 · 30스텝 · CFG 6.0):
> BG01→`ui/assets/scene-arcade.png` · BG02→`scene-warehouse.png` · BG03→`scene-basement.png`
> BG04→`scene-market.png` · BG05→`scene-boss.png` · BG06→`scene-rest.png`
> 구 `ui/assets/scene.png` 은 폴백으로 남긴다.

### BG01 상가 아케이드 (1층 입점 매장 · 기본 전투)

```
Wide 16:9 dark fantasy environment plate: a huge vaulted stone hall serving as the shopping
arcade of an underground dungeon mall. Two facing rows of merchant bays are cut into the
massive stone piers on the far left and far right — each bay a closed-up shopfront with a
rusted iron roll-down grille pulled halfway down over a thick stone counter, tiered wooden
goods shelves standing empty in the dark behind the grille, bare hanging chains where
signboards used to swing, blank uncarved plaques above the lintels. Rope-and-post queue
barriers stand abandoned along one side. Gothic vaulted ribs spring from the piers and meet
high overhead before dissolving into soot and haze. Between the two rows is a very wide bare
concourse of flagstone, worn glass-smooth by decades of foot traffic.
Detailed painterly concept art, digital matte painting for a video game, visible brushwork.
Cold pale daylight falls from a great height onto the empty middle of the concourse; small
guttering orange lanterns burn only on the piers at the far edges. Cool and warm in clear
opposition. Desaturated palette: slate blue-grey stone, damp black, tarnished brass, mossy
olive, with sparse warm lantern accents at the edges only.
Completely empty — no people, no creatures, no figures anywhere.
Staged like a theatre backdrop: the centre and centre-right of the frame is open bare
concourse with absolutely nothing standing on it, all architecture and clutter pushed hard
to the left and right edges, which fall away into deep shadow.
Camera at standing eye level, wide lens, horizon low in the lower third, tall airy space
filling the upper two thirds, deep volumetric haze.
Muted and atmospheric, intended to sit far behind interface elements.
No text, no letters, no signage, no ui.
```

### BG02 물류 창고 (배송 계열 전투 · 야매 배송 도적)

```
Wide 16:9 dark fantasy environment plate: the freight warehouse of an underground dungeon
shopping mall. Towering timber-and-iron racking runs down both sides, loaded with roped
crates, banded barrels, lashed sacks and stacked bales that recede into darkness. A heavy
black-iron chain-and-cog conveyor runs along one flank, still and cold. Pulley blocks and
loading hooks hang from the ceiling on long chains. Split pallets, torn packing straw and
one tipped crate litter the floor near the walls. A high loading hatch far above drops a
single shaft of dusty light onto the bare loading floor.
Detailed painterly concept art, digital matte painting for a video game, visible brushwork.
Cold pale daylight pours down the high shaft onto the bare loading floor; a couple of small
orange lamps burn on the racking at the far edges. Cool and warm in clear opposition.
Desaturated palette: iron grey, cold blue shadow, raw timber brown, sackcloth beige, oxblood.
Completely empty — no people, no creatures, no figures anywhere.
Staged like a theatre backdrop: the centre and centre-right of the frame is open empty
loading floor with nothing standing on it, all racking and cargo pushed hard to the left
and right edges, which fall away into deep shadow.
Camera at standing eye level, wide lens, low horizon, dust hanging in the air.
Muted and atmospheric, intended to sit far behind interface elements.
No text, no letters, no signage, no ui.
```

### BG03 지하 매장 (정예전 · 습하고 폐쇄된)

```
Wide 16:9 dark fantasy environment plate: the flooded sub-basement stockroom under a dungeon
shopping mall. Low, heavy brick groin vaults press down close overhead. Black standing water
covers the floor an ankle deep, dead flat and mirror-still, doubling the few weak lamps.
Tall rusted iron stockroom shelving lines both side walls, several bays buckled and half
collapsed, their shelves still loaded with swollen crates and barrels gone soft with rot,
sacks slumped and split, goods spilled into the water. Dripping pipes, mineral streaks and
pale salt bloom run down the seeping brickwork. A heavy iron vault door stands ajar in one
side wall, black inside.
Detailed painterly concept art, digital matte painting for a video game, visible brushwork.
Moody desaturated palette: wet black, cold slate blue, rust orange, sick green damp, with
one dim sodium lamp accent burning at the far edge.
Completely empty — no people, no creatures, no figures anywhere.
Staged like a theatre backdrop: the centre and centre-right of the frame is open flooded
floor with nothing standing on it, all shelving and wreckage pushed hard to the left and
right edges, which fall away into deep shadow. Reflections carry the only brightness.
Camera at standing eye level, wide lens, low horizon, close and airless, cold haze.
Muted and atmospheric, intended to sit far behind interface elements.
No text, no letters, no signage, no ui.
```

### BG04 노천 좌판 골목 (잡상인 전투 · 고블린 잡상인)

```
Wide 16:9 dark fantasy environment plate: a cramped night market alley inside a dungeon
shopping mall. Crooked trestle tables and plank counters crowd both walls under sagging
patched cloth awnings, propped on lashed poles. Ropes of small paper lanterns sag across
between the awnings, half of them dark. The stalls are heaped with goods — dented pots,
bundled cloth, wicker baskets, hanging tools and cheap trinkets on strings, tipped crates
of produce. Mud-tracked cobbles underfoot, puddles holding lantern reflections. The alley
opens out into a small bare clearing where the ground has been trampled flat.
Detailed painterly 3D render for a video game. Moody desaturated palette: soot brown, faded
awning ochre, mossy olive, damp cobble grey, with warm paper-lantern accents.
Completely empty — no people, no creatures, no figures anywhere, no vendors.
Staged like a theatre backdrop: the centre and centre-right of the frame is the open bare
clearing with nothing standing on it, all stalls and hanging goods pushed hard to the left
and right edges, which fall away into deep shadow.
Camera at standing eye level, wide lens, low horizon, smoke and steam drifting.
Muted and atmospheric, intended to sit far behind interface elements.
No text, no letters, no signage, no ui.
```

### BG05 본사 직영 · 지배인실 (6층 보스 · 답글 없는 사장)

```
Wide 16:9 dark fantasy environment plate: the manager's hall crowning an ancient underground
dungeon mall — a cathedral-scale audience chamber of colossal black stone that is also an
archive office. Down the far left side and down the far right side of the frame run
two immense facing walls of iron pigeonhole racking, towering thirty metres from the
flagstone up into total darkness, every single cell crammed with rolled parchment scrolls
and roped bundles of ledgers, tall iron rolling ladders leaning against them, carved black
stone piers rising between the stacks. Far away at the end, dwarfed by the height, sits one
long low black stone service counter with its iron shutter drawn fully down and a tarnished
brass rail along its front. Between the two walls of ledgers the vast flagstone floor is bare
and polished mirror-dark; a single narrow strip of cold pale light falls across it from an
arrow-slit far above.
Detailed painterly concept art, digital matte painting for a video game, visible brushwork.
Ancient, monumental, hand-cut stone — not modern architecture. No warmth at all: cold blue-grey
light only. Desaturated palette: black iron, cold bone-grey parchment, deep blue shadow,
tarnished brass.
Completely empty — no people, no creatures, no figures anywhere.
Staged like a theatre backdrop: the centre and centre-right of the frame is open polished
floor with absolutely nothing standing on it, the rear counter kept low and far back, all
shelving and mass pushed hard to the left and right edges, which fall away into deep shadow.
Symmetrical, frontal, crushing vertical scale. Camera at standing eye level, wide lens,
horizon low in the lower third, the towering stacks filling the upper two thirds, cold haze.
Institutional, silent, and airless — bureaucracy rendered as ancient architecture.
No text, no letters, no signage, no ui.
```

### BG06 휴게 공간 · 불 켜진 노점 (5층 휴게 시설 · 휴식 노드)

```
Wide 16:9 dark fantasy environment plate: a small lit rest stop tucked into a dungeon
shopping mall concourse. On one side a covered timber food stall with a big copper cauldron
steaming under its hood, ladles and bowls stacked on the counter, a brazier of live coals
glowing beside it. On the other side low worn benches and stools, folded blankets, a stone
water basin, a tall lantern post. A single string of warm lamps loops overhead between them.
The stone floor between is swept bare and empty.
Detailed painterly 3D render for a video game. Palette darker at the edges but warmed at the
centre: ember orange, copper, worn timber, damp stone grey, deep shadow beyond.
Completely empty — no people, no creatures, no figures anywhere, no cook, no patrons.
Staged like a theatre backdrop: the centre and centre-right of the frame is the swept bare
floor with nothing standing on it, stall and benches pushed hard to the left and right edges,
the concourse beyond them falling away into deep shadow.
Camera at standing eye level, wide lens, low horizon, steam and warm haze drifting.
Quiet, safe, and small against a very large dark space.
No text, no letters, no signage, no ui.
```

## 5.2 지도 = 배송 조회 화면 배경 플레이트 (S11 · 1344×768 고정)

> 지도는 택배 배송 추적 화면이다(ADR-024). 운송장 정보·경로 그래프·노드 카드가 **화면 전면을
> 덮으므로** 전투 배경(§5.1)과 규칙이 다르다 — 중앙을 비우는 것이 아니라 **전면이 고르게 어둡고
> 저대비**여야 한다. 세 가지가 규칙이다:
> **① 밝은 핫스팟·강한 초점을 만들지 않는다**(값을 어두운 절반에 묶어 둔다)
> **② 구조는 정면 벽면 하나로 단순하게 — 원근이 깊으면 그래프 선과 충돌한다**
> **③ 인물·생물은 한 명도 넣지 않는다.**
>
> 함정: 따뜻한 악센트만 지정하면 화면 전체가 세피아 단색으로 흐른다. **한랭 키라이트와 온색 등불을
> 문장으로 명시적으로 대립**시킨다(`Cool and warm in clear opposition`). 네거티브를 과하게 쌓으면
> 격자·선반 같은 구조물 자체가 지워지므로 인물 계열만 추가한다:
> `person, people, character, creature, figure, portrait, statue`
>
> 채택본 (juggernautXL · 1344x768 · 30스텝 · CFG 6.0): BG07→`ui/assets/map-dispatch.png`

### BG07 배송 상황판 · 물류 접수 창구 벽면 (S11 지도 배경)

```
Wide 16:9 dark fantasy environment plate, digital matte painting for a video game, detailed
painterly concept art with visible brushwork — a painting, not a photograph.
Subject: the consignment wall of the freight guild inside an underground dungeon mall, seen
straight on. A colossal floor-to-ceiling rack of hundreds of identical small square wooden
pigeonhole slots fills almost the whole frame and runs off past the left and right edges, its
top dissolving into soot and darkness above; every cell is packed with folded blank parchment
slips, rolled dockets and roped bundles of consignment paper, plain uncarved brass ring-tags
hanging on nails between the cells. Carved black stone piers rise between the sections of
racking, with tall iron rolling ladders leaning against them. A low scarred timber counter runs
along the base of the rack with ink pots, a wooden stamp block and a coil of twine on it; a
narrow strip of worn flagstone shows only at the very bottom edge of the frame.
The racking is the whole subject and fills the frame from top to bottom — no blackboard, no
slate panel, no single large object breaking it up.
Lighting: cold pale blue-grey light rakes flat across the whole wall from the left and sets the
overall colour cast — the frame reads cool, never sepia, never warm overall. Two tiny guttering
orange oil-lamp points burn far apart near the right edge as isolated warm accents against that
cold field. Cool and warm in clear opposition, never blended, and the cold dominates.
Desaturated palette: cold blue-grey, damp black stone, weathered timber brown, tarnished brass,
sackcloth beige.
Completely empty — no people, no creatures, no figures anywhere.
Deliberately low contrast, all values held in the darker half, no bright hotspot and no
strong focal point anywhere in the frame — one quiet even dark field of repeating texture,
the outer border falling away into deep shadow.
Camera square on to the wall at standing eye level, wide flat lens, very shallow depth, almost
no perspective recession, faint dust haze.
Underground and ancient — no sky, no daylight, no modern building.
Muted and atmospheric, intended to sit far behind dense interface text.
No text, no letters, no numbers, no signage, no ui.
```

### BG08 화물 터미널 · 분류 구역 벽면 (S11 지도 배경 대안)

```
Wide 16:9 dark fantasy environment plate, digital matte painting for a video game, detailed
painterly concept art with visible brushwork — a painting, not a photograph.
Subject: the sorting frontage of the freight terminal beneath an underground dungeon mall, seen
straight on. A long unbroken row of identical loading bays is cut into massive hand-hewn stone
piers and fills almost the whole frame, running off past the left and right edges, the vaulted
stonework above dissolving into soot and darkness; every bay is sealed by a rusted iron
roll-down grille drawn fully closed, with a blank uncarved stone plaque set above each lintel
and a heavy padlocked hasp at its base. Between the bays hang grids of iron pegboard strung
with taut waxed routing cord and rows of blank brass tally tags. Roped crates, banded barrels
and lashed sackcloth bales are stacked waist-high against the grilles; torn packing straw and
a narrow strip of worn flagstone show only at the very bottom edge of the frame.
Lighting: cold pale blue-grey light falls flat across the whole frontage and sets the overall
colour cast — the frame reads cool, never sepia, never warm overall. Two tiny guttering orange
lamp points burn far apart at the outer edges as isolated warm accents against that cold field.
Cool and warm in clear opposition, never blended, and the cold dominates.
Desaturated palette: iron grey, cold blue shadow, oxidised rust, weathered timber brown,
sackcloth beige.
Completely empty — no people, no creatures, no figures anywhere.
Deliberately low contrast, all values held in the darker half, no bright hotspot and no strong
focal point anywhere in the frame — one quiet even dark band of repeating structure, the outer
border falling away into deep shadow.
Camera square on at standing eye level, wide flat lens, very shallow depth, almost no
perspective recession, dust hanging in the air.
Underground and ancient — no sky, no daylight, no modern building, no garage.
Muted and atmospheric, intended to sit far behind dense interface text.
No text, no letters, no numbers, no signage, no ui.
```

## 5.3 운송장 용지 · UI 표면 텍스처 (S11 · 1344×768 고정)

> 지도 상단의 운송장 블록은 **어두운 패널이 아니라 종이 한 장**이다(ADR-024). 배경 플레이트(§5.2)와
> 목적이 반대다 — 저 뒤에 까는 그림이 아니라 **글자가 직접 얹히는 표면**이므로 밝고 균일해야 한다.
> 세 가지가 규칙이다:
> **① 값을 밝은 절반에 묶는다** — 어두운 잉크 글자가 위에 얹히므로 대비를 종이가 아니라 글자가 만든다.
> **② 초점·비네트·조명 얼룩을 만들지 않는다** — 모서리부터 모서리까지 밝기가 같아야 텍스트 블록이 고르게 읽힌다.
> **③ 인쇄물을 그리지 않는다** — 칸·괘선·바코드·도장이 이미지에 들어가면 코드로 얹는 실제 서식과 이중으로 겹친다.
>
> 함정 셋:
> ① 온색만 지정하면 §5.2와 같은 이유로 세피아 단색 판때기가 되어 종이로 안 읽힌다. **접힌 자국과
> 카본 얼룩에 한랭색을 명시**해 크림색 바탕과 대립시킨다. 네거티브는 인쇄물 계열만 얇게 얹는다:
> `text, letters, numbers, watermark, person` — 더 쌓으면 천공·접힘 같은 구조 자체가 지워진다.
> ② "종이 한 장"이라고만 하면 **종이가 아니라 석고·콘크리트 벽면**으로 흐르고, 종이의 가장자리와
> 그 밑의 바닥까지 그려 넣는다. `extreme close macro crop` + "가장자리가 안 보이고 사방으로 잘려
> 나간다"를 명시하고 `stone, concrete, plaster, wall, granular, deckle edge, border` 를 덧붙인다.
> ③ 뽑은 결과에는 **사진의 저주파 조명 얼룩(비네트·그림자)이 남아** 한쪽 모서리가 어둡다. UI 표면은
> 그 얼룩이 텍스트 블록 밝기 차이로 보인다. 원본을 크게 흐린 판으로 나눠 조명만 평탄화하고(flat-field)
> 접힘·결 같은 고주파는 남긴 뒤 쓴다. 처리 후 밝기 p2~p98 이 170~184 로 붙는 것이 목표다.
>
> 채택본 (juggernautXL · 30스텝 · CFG 6.0):
> T01(1344x768, seed 31)→`ui/assets/paper-waybill.png` · C08b(1216x832, seed 88)→`ui/assets/issue-hand.png`
>
> C08b 는 슬립을 손 밑에 깔아 달라는 지시를 모델이 끝내 무시한다(종이를 그리지 않는다). **종이는 UI 가
> 얹으므로 그대로 채택**했다 — 이미지는 카운터와 장갑 낀 손까지만 맡고, 그 위로 실제 운송장이 미끄러져 들어온다.

### T01 운송장 용지 (S11 운송장 블록 배경)

```
Wide 16:9 flat surface texture plate for a game interface, extreme close macro crop of the middle
of a sheet of paper, deliberately almost featureless — paper and nothing else.
Subject: old blank logistics consignment paper, thin cheap sheet stock, filling the entire frame
edge to edge and running off past every edge, seen flat from directly above. No edge of the sheet
is visible anywhere and there is no surface underneath it — the paper is the whole image. The
stock is smooth and yellowed — warm oatmeal cream going to deeper tan where it has aged, soft and
fibrous, never granular, never mineral. Two soft horizontal fold creases run the full width where
the sheet was folded in thirds and one vertical crease sits near the left, each crease a faint
bright ridge with a thin cool grey shadow beside it. A faint regular
grid of dot-matrix impact printer strikes is pressed into the surface as bare embossing with no
ink in it, readable only as raking texture. Pale violet-grey carbon copy smudges bloom in two or
three places, a dull ring stain sits near one corner, with scattered paper fibre flecks, foxing
specks and a few short scuffs. A single narrow band of tractor-feed sprocket perforations runs
along the bottom edge — small evenly spaced punched round holes with slightly torn rims.
Lighting: soft even raking light from the upper left, no lamp in frame, no hotspot, no vignette,
brightness constant from corner to corner.
Palette: aged cream, oatmeal beige, warm tan, with cool grey-violet held in the creases and the
carbon smudges so the sheet never collapses into one flat sepia tone.
Very low contrast with all values held close together in the brighter half, no dark region and no
glare anywhere — a quiet even surface meant to sit directly under dark ink-coloured interface text.
Camera perfectly square on, orthographic, no perspective, no depth of field, no background visible
around the sheet, the paper fills the frame completely.
Nothing is printed on the paper — no print, no text, no letters, no numbers, no stamps, no
barcodes, no ruled lines, no boxes, no forms, no logos.
```

### C08b 택배좌의 손 — 운송장을 건네는 순간 (S11 발급 연출 · 3:2)

```
Wide 3:2 dark fantasy interior for a video game, digital matte painting with visible brushwork.
Subject: a low scarred timber freight counter seen from the customer's side at chest height. One
single gloved hand — exactly one hand and no other, entering from the far side out of deep shadow
— rests its fingertips flat on top of a small pale folded blank paper slip lying on the timber,
sliding it forward across the counter toward the viewer. The pale slip is the brightest thing in
the frame and sits fully visible under the fingers. Only that hand and part of the forearm are
lit — a worn heavy leather work glove, cuff frayed, brass buckle dulled. Everything beyond the
wrist falls away into black: the body behind it is an unlit silhouette with no face, no head and
no shoulders resolved, unreadable.
Lighting: one cold pale blue-grey light falls across the counter top from the left and sets the
overall cast — the frame reads cool, never sepia. A single small warm oil-lamp point glows far
back on the right as an isolated accent. Cool and warm in clear opposition, and the cold dominates.
Desaturated palette: cold blue-grey, damp black stone, weathered timber brown, tarnished brass,
sackcloth beige, with the pale slip as the one bright value.
Low contrast overall, values held in the darker half apart from the slip and the glove, the outer
border falling away into deep shadow.
Camera square on to the counter at seated eye level, wide flat lens, shallow depth, dust in the air.
Underground and ancient — no sky, no daylight, no modern building.
No face anywhere in frame, no portrait, no eyes, no second person.
No text, no letters, no numbers, no writing on the slip, no signage, no ui.
```

---

### B05 맵 배경 (S11 · 9:16)

```
Wide vertical 9:16 environment plate for a dark fantasy video game.
Detailed painterly 3D render. Moody desaturated palette: iron grey, damp stone, oxblood.
A vast dark fantasy dungeon interior receding upward into darkness — colossal stone arches,
worn flagstone, guttering torchlight, deep volumetric haze, a scale that dwarfs anything human.
Completely empty of characters.
Composed so that the central vertical band stays visually quiet and uncluttered, with all
detail concentrated toward the left and right edges.
Muted and atmospheric, intended to sit far behind interface elements.
No text, no letters, no ui, no characters.
```

### V01 타이틀 히어로 (S01 · 16:9) — 80:20이 한 장에 들어간 이미지

```
Wide 16:9 key visual for a dark fantasy video game.
Detailed painterly 3D render. Moody desaturated palette: iron grey, storm blue, damp stone,
with one warm accent.
A colossal dungeon gate of black stone and iron rising out of drifting mist, carved with ancient
sigils, torch sconces guttering along its flanks, monumental and oppressive in scale.
A storm-dark sky above, a broken causeway leading up to it.
Nailed at eye level onto the ancient iron of the gate is one small, mundane, perfectly ordinary
delivery notice slip — a plain pale rectangle of modern paper, crisp and slightly curling at one
corner, entirely out of place. It is small within the frame but lit just enough to be found.
Everything else in the image is played completely straight: awe, dread, and grandeur.
No text, no letters, no logos, no writing on the paper slip.
```

### V02 회원 탈퇴 = 패배 (S23 · 4:3)

```
Wide 4:3 composition for a dark fantasy video game.
Detailed painterly 3D render. Moody desaturated palette: iron grey, cold blue, deep shadow.
A single empty steel wire shopping cart standing alone at the center of a vast dark void,
tipped very slightly, one wheel turned outward. Completely empty.
A shaft of cold pale light falls on it from far above; everything beyond it dissolves into darkness.
No figure, no debris, no environment, nothing else in the frame.
Still, quiet, and final.
No text, no letters, no ui.
```

### V03 구매 확정 = 승리 (S23 · 4:3)

```
Wide 4:3 composition for a dark fantasy video game.
Detailed painterly 3D render. Moody palette warmed by torchlight: aged stone, amber, deep shadow.
A sealed cardboard shipping box resting squarely at the center of ancient worn flagstone,
packing tape neatly applied, corners crisp and undamaged. A single blank paper slip rests on its lid.
Warm torchlight falls from the left; the darkness beyond recedes.
Orderly, complete, and quietly satisfying in the plain way a finished delivery is satisfying.
No text, no letters, no writing on the slip, no ui.
```

---

# 6. 만물대장 레이어 (P1) — 유일하게 다른 톤

> 명조체 + 장부 톤. 스타일 바이블을 쓰지 않는다.

### V04 만물대장 열람 (S50 · 16:9)

```
Aged paper ledger aesthetic illustration, 16:9.
Sepia and bone-white palette, fine ruled grid lines, paper fiber texture, faint ink bleed,
engraved-line illustration style, restrained and archival. No color accents, no modern elements.
An immense vertical ledger book standing open, its pages taller than a cathedral, ruled columns
receding upward beyond sight into darkness. Every line of every column is filled with dense
uniform entry marks. Vast, orderly, and indifferent.
No text, no readable letters, no words — entry marks only.
```

### C08 심사위원 「택배좌」 (S51 · 4:5)

```
Aged paper ledger aesthetic illustration, vertical 4:5.
Sepia and bone-white palette, fine ruled grid lines, paper fiber texture, faint ink bleed,
engraved-line illustration style, restrained and archival. No color accents, no modern elements.
A seated figure in heavy formal robes rendered in engraved-line style, the face left entirely
blank and unmarked. On the desk before them rests a single parcel with its shipping slip torn away.
Symmetrical, front-facing, archival portrait composition.
No text, no readable letters, no words.
```

### V05 탈퇴 엔딩 (S52 · 16:9)

```
Aged paper ledger aesthetic illustration, 16:9.
Sepia and bone-white palette, fine ruled grid lines, paper fiber texture, faint ink bleed,
engraved-line illustration style, restrained and archival. No color accents, no modern elements.
A single ledger page, torn cleanly out of its binding and lying flat, completely blank —
every ruled line present, not one entry written on any of them.
Wide empty space around it. Quiet finality.
No text, no readable letters, no words, no entry marks.
```

---

# 7. 가짜 광고 배너 나머지 (S12 로딩 · S33 이벤트)

> 카피는 이미지에 넣지 않고 **코드로 오버레이**한다. 배너 4장 × 카피 20개 = 80조합.

### B02 「용사님, 장바구니에 담긴 상품이 있어요」

```
Horizontal promotional banner artwork, 3:1 aspect ratio.
Dark fantasy subject rendered seriously and in full detail, placed on the left third,
lit as a hero shot with a strong cool key light and warm rim light,
against a dark atmospheric gradient with soft volumetric haze.
The right two-thirds of the frame is kept visually quiet and near-empty for text overlay.
Moody desaturated palette with one saturated accent color.
No text, no letters, no logos, no price tags anywhere in the image.

A dented iron warhelm and a rolled sigil scroll resting inside a plain steel wire shopping cart,
abandoned in the dark, a film of dust settled over both.
```

### B03 「★5 리뷰 이벤트 — 참여만 해도 적립금」

```
Horizontal promotional banner artwork, 3:1 aspect ratio.
Dark fantasy subject rendered seriously and in full detail, placed on the left third,
lit as a hero shot with a strong cool key light and warm rim light,
against a dark atmospheric gradient with soft volumetric haze.
The right two-thirds of the frame is kept visually quiet and near-empty for text overlay.
Moody desaturated palette with one saturated accent color.
No text, no letters, no logos, no price tags anywhere in the image.

A heaped spill of ancient gold coins on the left third, glinting in the darkness, with a single
folded blank parchment slip resting on top of the pile.
```

### B04 「부활 정기구독, 첫 달 50% 할인」

```
Horizontal promotional banner artwork, 3:1 aspect ratio.
Dark fantasy subject rendered seriously and in full detail, placed on the left third,
lit as a hero shot with a strong cool key light and warm rim light,
against a dark atmospheric gradient with soft volumetric haze.
The right two-thirds of the frame is kept visually quiet and near-empty for text overlay.
Moody desaturated palette with one saturated accent color.
No text, no letters, no logos, no price tags anywhere in the image.

A full suit of plate armor lying collapsed and empty on the ground, neatly arranged as though
folded for retail display, with a small blank tag ring hooked to the gorget.
```

---

# 8. 아이콘 (커머스 층) — 스타일 바이블 미적용

> 여기서부터는 **얇고 기하학적인 앱 아이콘**이다. 판타지 렌더가 아니다. 전부 투명 배경.

### I01~I06 맵 노드 아이콘 (S11)

```
Minimal app category icon, simple geometric symbol, single flat color on transparent background,
uniform 2px stroke weight, rounded line caps, no gradient, no shadow, no perspective,
centered with even padding, clean modern e-commerce app iconography, no text.

Draw: a closed cardboard shipping box seen at a slight angle.
```
> **나머지 5개는 마지막 `Draw:` 줄만 교체한다.**
> - I02 정예 = `a shipping box with a small speed chevron mark beside it`
> - I03 상점 = `a retail shelf rack holding three items`
> - I04 휴식 = `a headset with a microphone boom`
> - I05 이벤트 = `a bell with a small notification dot`
> - I06 보스 = `a plain office tower, flat front elevation`

### I07~I10 4계열 심볼 (S13 · S20 · S30)

```
Minimal app category icon, simple geometric symbol, single flat color on transparent background,
uniform 2px stroke weight, rounded line caps, no gradient, no shadow, no perspective,
centered with even padding, clean modern e-commerce app iconography, no text.

Draw: a magnifying glass held over a small stitched seam.
```
> - I08 성능 = `a gauge dial with a needle`
> - I09 배송 = `a delivery truck in side silhouette`
> - I10 감성 = `a single star with one softly rounded edge`

### I11~I14 성향 뱃지 (S20 · S22)

```
Circular achievement badge for a shopping app, simple emblem inside a plain circle,
two flat colors, no gradient, no shadow, no text, transparent background.

Draw: a downward arrow striking a small target dot dead center.
```
> - I12 힙스터 인증 = `a pair of round eyeglasses`
> - I13 진상 접수 = `a raised hand with one finger extended in objection`
> - I14 바이럴 확산 = `three small dots connected by lines spreading outward from one point`

### I15 앱 아이콘 / 로고 심볼 (S01 · S02)

```
Minimal app icon symbol, single flat color on transparent background,
uniform stroke weight, rounded line caps, no gradient, no shadow, no perspective,
perfectly geometric, legible at 24 pixels, no text, no letters.

Draw: a single symbol merging a shopping bag silhouette with a five-pointed star,
the star occupying the position where the bag's fold or handle would be.
```
> **「만물마켓」 글자는 절대 이미지로 만들지 않는다.** 심볼만 생성하고 워드마크는 웹폰트로 조판한다.

---

# 9. 특수 카드 스팟 (P1) — X01~X09

> 리뷰 카드 60장 중 **진상 화법 9종만** 스팟 일러스트. 나머지 51장은 타이포로 처리한다(카드 체계 v2 §3).

```
Tiny spot illustration for a card game, single object or simple gesture, engraved-line style
with one flat warm accent color, transparent background, no frame, no border, no text,
legible at 64 pixels.

Draw: a shipping crate with a small clock face set into its side.
```
> **마지막 `Draw:` 줄만 교체해서 9번 생성한다.**
> - X02 별점 테러 = `a single star with a crack splitting through it`
> - X03 안 시켰는데 이게 왔네요 = `an opened crate with an unidentifiable lumpy shape rising out of it`
> - X04 무료 나눔 = `an open palm offering a small wrapped bundle`
> - X05 정신적 피해보상 청구 = `a parchment sheet bearing a wax seal, torn at one corner`
> - X06 우리 애가 다쳤잖아요 = `a small bandaged doll held in an adult hand`
> - X07 내가 여길 다시 오나 봐라 = `a heavy shop door swinging shut, viewed from outside`
> - X08 별점 구걸 = `a hand with thumb raised, reaching in from the frame edge`
> - X09 결함의 무기화 = `a cracked blade held up like a tool, the fracture turned outward`

---

## 저장 규칙

- 1024px 이상, `assets/<그룹>/<ID>_<이름>.png`
- 적·구성품은 **배경 제거 투명 PNG + 배경 포함 원본** 둘 다 보관 (전투 히어로는 배경 포함, 리스트 썸네일은 투명)
- 배너·키비주얼·배경은 원본만
