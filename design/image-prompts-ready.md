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

A wiry goblin peddler, chest-high to a man, sinewy and quick-eyed, sallow olive-green skin
stretched over sharp bone, long notched ears, a mouth of crooked teeth held in a hard flat line.
He is draped in layered scavenged gear — a patchwork leather jerkin stitched from mismatched hides,
the seams crooked and puckered, loose threads hanging, one shoulder strap knotted where a buckle
should be, a rivet already pulling free of the leather.
A cheap curved dagger hangs at his belt, its pommel visibly loose on the tang.
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

A towering gaunt figure in an immaculate charcoal frock coat, tailored sharp and pressed without
a single crease, worn over dark plate at the shoulders — part administrator, part warlord.
Where the face should be there is a smooth featureless expanse of pale bone-white surface —
no eyes, no mouth, no seam, no expression. Nothing to appeal to.
A small brass name plate is pinned to the lapel, its surface deliberately blank and unengraved.
One gloved hand rests on the haft of a great scythe whose blade is not forged metal but a thick
fused stack of compressed contract parchment — dozens of laminated sheets pressed and hardened
into a curved cutting edge, the layered strata visible along the spine, edges yellowed and
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

A tall slender high elf mage, porcelain-pale skin, severe elegant features, long straight silver
hair, expression cold and detached.
Wearing layered ceremonial robes of deep indigo and gold — extravagantly embroidered, trailing
ribbons, ornamental tassels, floor-length sleeves that serve no practical purpose.
He holds upright an enormously over-designed staff, crusted with dozens of faceted crystals,
gold filigree scrollwork and hanging charms — top-heavy, ostentatious, more jewelry than tool.
Arcane light spills from the crystals in bright wasteful arcs, far more radiance than any spell
requires, the excess bleeding off into the air as drifting motes that dissipate uselessly.
Magnificent, expensive, and burning far more power than it produces.
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

A lean hooded rogue built entirely for speed — narrow frame, light dark travel leathers cut close
to the body, wrapped forearms, soft-soled boots. A courier's satchel is slung tight across the back,
its strap repaired with a crude knot instead of a buckle.
His face is lost in the shadow of the hood, only a hard jawline and the faint glint of one eye visible.
He holds a single curved shortblade low and ready. The matching second blade is thrust into the
ground beside him, apart from him, its grip wrapping already unravelling.
Everything about him is fast, and everything about him is hastily made.
Coiled, silent, one step from vanishing.
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

A knight in full gold-plated plate armor, mirror-polished to a blinding showroom shine, catching
and throwing the key light from every surface. The armor is lavishly ornamented — engraved
scrollwork, decorative fluting, an extravagant plume, pauldrons oversized for silhouette rather
than defense. The plating is visibly thin, the articulated joints ornamental, built to be looked
at rather than struck. Not a single scratch anywhere on it.
Helmet held under one arm. A handsome symmetrical face, chin lifted, gaze angled deliberately
past the viewer toward some imagined audience, one hip cocked in a practiced heroic stance.
Radiant, immaculate, and entirely occupied with being seen.
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
> - I12 힙스터 평론가 = `a pair of round eyeglasses`
> - I13 프로 불편러 = `a raised hand with one finger extended in objection`
> - I14 바이럴 앞잡이 = `three small dots connected by lines spreading outward from one point`

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
