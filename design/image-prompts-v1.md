# 이미지 생성 프롬프트 v1 — 판타지 80 / 커머스 20

- 작성: 2026-08-03 · 목적: 그래픽을 실제로 생성해 아트 방향을 눈으로 검증한 뒤 UI 구현에 착수
- 비율 결정: **판타지 80% : 커머스 20%** (사용자 결정)
- 정본 관계: `art-direction-v1.md`(ADR-010)의 **부분 개정 제안**. 2절의 경계선이 확정되면 ADR-011로 박제한다. 그전까지 이 문서는 검증용.
- 상태: 검증 대기 — 5절 「1차 검증 세트」를 먼저 뽑고 스타일 확정 후 전량 진행

---

## 1. 무엇이 바뀌나

ADR-010은 "UI 전체가 쇼핑몰 앱, **판타지 UI 금지**"였다. 이 방향의 위험은 실제로 확인됐다 — 텍스트만 남은 프로토타입은 판타지 게임으로 안 보이고, 그냥 폼(form)이었다.

**80/20으로 재조정한다. 다만 비율을 화면 면적으로 나누는 게 아니라, 층으로 나눈다.**

| 층 | 비율 | 내용 |
|---|---|---|
| **아트워크** (상품 이미지·히어로·시네마틱·배경) | **판타지 100%** | 제대로 된 판타지 아트. 무게감·질감·위압감·마법 이펙트 전부 허용 |
| **UI 크롬** (버튼·리스트·뱃지·타이포·레이아웃) | **커머스 100%** | 리뷰 작성 폼, 별점, 진행바, 카테고리 — 여기는 그대로 쇼핑몰 |

화면 전체로 보면 아트워크가 시선의 대부분을 먹고 UI는 얇게 얹히므로 **체감 비율이 대략 80:20**이 된다.

### 이 분리가 오히려 개그를 강화한다

기존안(상품 사진처럼 촬영된 오크)은 **몬스터를 우스꽝스럽게 만들어서** 웃겼다. 그러면 판타지의 무게가 죽는다.

새 방향은 반대다:

> 오크는 **진짜 무섭게** 그린다. 그 위에 **★★★☆☆ 별점 3.2 (리뷰 1,210건)** 이 붙는다.

worldview §5의 톤 원칙 "**세계는 진지하고, 그 세계의 규칙이 웃기다**"에 훨씬 정확하다. 세계(아트워크)는 진지하고, 규칙(UI)이 웃긴다. 개그를 아트가 아니라 **UI가 전담**한다.

### 그래서 아트워크에서 해제되는 금지 사항

ADR-010 §부수결정의 "판타지 이펙트(폭발·번쩍임) 금지"는 **UI 모션에만 적용**하는 것으로 축소한다. 아트워크 안의 마법광·불꽃·마력은 허용. 단 **UI 위젯에서 번쩍이는 이펙트는 여전히 금지** — 버튼이 빛나면 그 순간 소셜게임이 된다.

---

## 2. 페이지 총계

`art-direction-v1.md` §2 화면 인벤토리 확정 집계. **총 31개 화면.**

| 그룹 | 화면 수 | P0 | P1 | P2 |
|---|---:|---:|---:|---:|
| 셸·진입 (S01~S04) | 4 | 3 | 0 | 1 |
| 런 진행 (S10~S14) | 5 | 5 | 0 | 0 |
| **전투 (S20~S23)** ★코어 | 4 | 4 | 0 | 0 |
| 노드 (S30~S36) | 7 | 4 | 2 | 1 |
| 비동기·UGC (S40~S47) | 8 | 0 | 0 | 8 |
| 세계관 (S50~S52) | 3 | 0 | 3 | 0 |
| **합계** | **31** | **16** | **5** | **10** |

- **P0 16개**가 MVP(1막 완결 싱글 런) 범위. 이미지 작업도 여기 집중.
- 이미지가 필요한 화면은 31개 중 **14개**. 나머지는 리스트·모달이라 CSS로 끝난다.

---

## 3. 이미지로 만들 것 vs CSS로 만들 것

**생성 이미지를 남발하면 오히려 싸구려로 보인다.** 판타지 비중을 80으로 올려도 이 원칙은 유지된다 — 아트워크는 **적고 강하게**.

### 이미지로 만든다 (판타지 아트)
- 적 = 상품 히어로 아트 (6종) ← **여기에 예산의 절반을 쓴다**
- 구성품 = 장비 아트 (6종)
- 타이틀 히어로 / 막 전환 / 엔딩 (4종)
- 맵 배경 (1종)
- 가짜 광고 배너 (4종) ← 유일하게 커머스 비중이 높은 아트

### 이미지로 만든다 (아이콘 — 커머스 층)
- 맵 노드 아이콘, 계열 심볼, 성향 뱃지, 앱 아이콘

### CSS·타이포로 만든다 (이미지 생성 금지)
- **모든 한글 텍스트.** 이미지 모델은 한글을 못 쓴다. 「만물마켓」 로고조차 심볼만 생성하고 글자는 웹폰트로 얹는다.
- 별점 ★, 의지 바, 신뢰도 10칸, 판정 뱃지, 가격표, 버튼, 태그 칩, 프레임, 그림자
- **카드 41장 전부.** 접두·접미는 타이포 + 태그 칩이 정답이다. 카드마다 일러스트를 넣으면 "리뷰 앱에서 글 쓰는 감각"이 깨지고 32장 일관성 비용도 감당 안 된다.
  - 예외: **특수 카드 9종(X01~X09)만 스팟 일러스트** → 8절

> **⚠ 프롬프트 철칙**: 생성 이미지에 글자·UI·프레임·워터마크가 들어가면 재생성한다. 이미지는 **피사체만** 담고, 화면 요소는 전부 그 위에 코드로 얹는다.

---

## 4. 스타일 바이블

**모든 프롬프트 앞에 `[STYLE]` 블록을 그대로 붙인다.** 세트 일관성을 잡는 유일한 장치다.

### `[STYLE]` — 공통 접두 (복사해서 매번 앞에 붙이기)

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
```

### 네거티브 프롬프트 (지원하는 툴이면 반드시 입력)

```
text, letters, words, korean characters, chinese characters, watermark, signature, logo,
ui, buttons, frames, borders, health bar, price tag, label,
anime, manga, cel shading, chibi, cute, comedic expression, goofy grin, wacky pose, caricature,
flat vector, clip art, low detail, plastic toy look,
white background, plain studio backdrop, product photography, catalog shot,
busy background, environment, landscape, multiple subjects, crowd,
gore, blood, dismemberment
```

> 이전 버전(커머스 상품컷)의 흔적인 `white background / product photography / catalog shot`을 **네거티브에 넣어야** 한다. 안 그러면 "상품"이라는 단어 없이도 모델이 흰 배경으로 끌려간다.

### 커머스 20%는 프롬프트가 아니라 **구도 규칙**으로 들어간다

아트워크에 쇼핑몰 요소를 그려 넣지 않는다. 대신 이 세 가지만 지킨다 — 이게 20%다.

1. **단독 피사체 · 정면 3/4 · 중앙 정렬** — 상품 상세 페이지의 대표컷 구도. 액션 신이 아니다.
2. **배경 최소화 · 실루엣 명확** — UI 슬롯에 떨어뜨렸을 때 잘리지 않고, 배경 제거도 가능해야 한다.
3. **세로 4:5** — 모바일 세로 상품 페이지 비율(art-direction §2.3 "최대 폭 760px, 한 손으로 스크롤").

### ★ 약점 태그는 이미지에서 눈에 보여야 한다 (설계 규칙)

이건 스타일이 아니라 **게임 규칙**이다. 적의 `weakness_tags`가 아트에 시각적으로 드러나야 플레이어가 이미지만 보고 올바른 접두 카드를 고를 수 있다. **이미지가 튜토리얼을 겸한다.**

| 적 | 약점 | 이미지에서 보여야 할 것 |
|---|---|---|
| E01 고블린 | #마감 | 삐뚤어진 바느질, 풀린 실밥, 어긋난 조립 |
| E02 오크 | #무게 | 눌린 손목, 땅에 박힌 발, 처지는 무기 각도 |
| E03 엘프 | #이펙트 #연비 | 과잉 장식, 화려하지만 비효율적인 마력 낭비 |
| E04 도적 | #속도 | 속도 특화 실루엣, 그런데 급조된 마감 |
| E05 기사 | #디자인 #감성 | 방어보다 외관 우선인 갑옷, 자아도취 포즈 |
| B01 사장 | #응대 #개연성 | **응답 없음의 시각화** — 얼굴 없음, 빈 이름표 |

### 생성 순서와 일관성 유지법

1. **C02 오크(정보량 최대)를 먼저 뽑아 스타일 앵커로 확정.**
2. 나머지는 그 결과물을 **스타일 참조 이미지**(image reference / `--sref` / style reference)로 물려서 생성. 텍스트 프롬프트만으론 6종 톤이 반드시 어긋난다.
3. 같은 시드 고정 + 피사체 문장만 교체하는 방식 병행.
4. 최종 에셋은 **배경 제거 투명 PNG** + **배경 포함 원본** 둘 다 보관. 전투 히어로는 배경 포함, 리스트 썸네일은 투명.
5. 출력 1024px 이상, `assets/enemy/C02_orc.png` 형태로 저장.

---

## 5. 1차 검증 세트 — 이 6장만 먼저 뽑아보세요

전량 생성 전에 **스타일이 마음에 드는지부터** 판단해야 한다.

| 순서 | 에셋 | 왜 이걸로 판단하나 |
|---|---|---|
| 1 | **C02 오크** (스타일 A: 다크 판타지) | 스타일 앵커. 크고 무거워 스타일의 한계가 드러남 |
| 2 | **C02 오크** (스타일 B: 밝은 하이 판타지) | A/B 비교. `[STYLE]`에서 `dark fantasy → high fantasy`, `moody desaturated → rich saturated`, `dark atmospheric backdrop → luminous pale mist backdrop`로 교체 |
| 3 | **C01 고블린** | 작고 마른 피사체에서도 톤이 유지되는지 + **약점(#마감)이 눈에 보이는지** |
| 4 | **C06 답글 없는 사장** | 보스의 위압감. 판타지 80으로 올린 효과가 가장 크게 나타날 자리 |
| 5 | **P02 초대형 둔기** | 무생물 단독컷. 장비가 "아이템"으로 읽히는지 |
| 6 | **B01 가짜 광고 배너** | 유일한 커머스 우위 아트. 개그가 실제로 웃긴지 |

**판단 기준 4개**
1. 판타지 게임으로 보이는가 (← 이게 이번 변경의 목적)
2. 6장이 같은 세계의 것으로 보이는가
3. 약점 태그가 이미지에서 읽히는가
4. 이 위에 별점·리뷰수 UI를 얹었을 때 웃길 것 같은가

---

## 6. 에셋 전량 목록 (31개 화면 매핑)

| ID | 에셋 | 비율 | 사용 화면 | 우선 |
|---|---|---|---|---|
| **C01~C06** | 적 히어로 아트 6종 | 4:5 | S20 전투, S11 맵, S13 | P0 |
| **C07** | 플레이어 아바타 (평가 불가) | 1:1 | S02, S20 | P0 |
| **C08** | 「택배좌」 | 4:5 | S51, S50 | P1 |
| **P01~P06** | 장비 아트 6종 | 1:1 | S20 구성품, S31 진열대 | P0 |
| **I01~I06** | 맵 노드 아이콘 6종 | 1:1 | S11 | P0 |
| **I07~I10** | 4계열 심볼 | 1:1 | S13, S20, S30 | P0 |
| **I11~I14** | 성향 4종 뱃지 | 1:1 | S20, S22 | P0 |
| **I15** | 앱 아이콘 / 로고 심볼 | 1:1 | S01, S02 | P0 |
| **B01~B04** | 가짜 광고 배너 4종 | 3:1 | S12 로딩, S33 이벤트 | P0 |
| **B05** | 맵 배경 (던전 전경) | 9:16 | S11 | P0 |
| **V01** | 타이틀 히어로 | 16:9 | S01 | P0 |
| **V02** | 회원 탈퇴(패배) | 4:3 | S23 | P0 |
| **V03** | 구매 확정(승리) | 4:3 | S23 | P0 |
| **V04** | 만물대장 열람 | 16:9 | S50 | P1 |
| **V05** | 엔딩 | 16:9 | S52 | P1 |
| **X01~X09** | 특수 카드 스팟 | 1:1 소형 | S13, S20, S30 | P1 |

**합계 42개 에셋** (P0 35 / P1 7). 화면 31개보다 많은 건 적·장비·아이콘이 화면을 가로질러 재사용되기 때문.

> **콘텐츠 한계**: 일반 적이 E01 하나뿐이라 1막 6층을 채울 목록이 빈약하다. 이건 이미지가 아니라 **콘텐츠 TBD**(GDD §11)이고, 지금은 확정된 6종만 뽑는 게 맞다.

---

## 7. 페이지별 프롬프트

> 사용법: **`[STYLE]` 블록을 먼저 붙이고 아래 본문을 이어 붙인다.** 네거티브 프롬프트도 함께 입력.

---

### 7.1 S20 전투 = 상품 상세 페이지 ★ 코어

플레이어가 시간의 80%를 보내는 화면. 이미지 품질이 게임 인상의 전부를 결정한다.

#### C01 — 고블린 잡상인 (E01 · 튜토리얼 · 약점 #마감)

```
[STYLE]
A wiry goblin peddler, chest-high to a man, sinewy and quick-eyed, sallow olive-green skin
stretched over sharp bone, long notched ears, a mouth of crooked teeth held in a hard flat line.
He is draped in layered scavenged gear — a patchwork leather jerkin stitched from mismatched hides,
the seams crooked and puckered, loose threads hanging, one shoulder strap knotted where a buckle
should be, a rivet already pulling free of the leather.
A cheap curved dagger hangs at his belt, its pommel visibly loose on the tang.
He stands his ground with the contained menace of something that has survived by being underestimated.
Not comical. Small, filthy, and genuinely dangerous.
```
**약점 시각화(#마감)**: 삐뚤어진 바느질, 풀린 실밥, 빠지려는 리벳, 헐거운 손잡이. 플레이어가 이미지만 보고 「마감이…」 카드를 집을 수 있으면 성공.

#### C02 — 오크 중량 전사 (E02 · 정예 · 약점 #무게) ★ 스타일 앵커

```
[STYLE]
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
**약점 시각화(#무게)**: 눌린 손목, 벌어진 스탠스, 한쪽으로 처진 어깨, 살을 파고드는 갑옷 끈.

#### C03 — 엘프 이펙트 마법사 (E03 · 정예 · 약점 #이펙트 #연비)

```
[STYLE]
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
**약점 시각화(#이펙트 #연비)**: 마법광은 허용하되 **낭비되는 형태**로 — 새어 나가 흩어지는 마력이 "연비 나쁨"의 그림이다.

#### C04 — 야매 배송 도적 (E04 · 정예 · 약점 #속도 · 은신)

```
[STYLE]
A lean hooded rogue built entirely for speed — narrow frame, light dark travel leathers cut close
to the body, wrapped forearms, soft-soled boots. A courier's satchel is slung tight across the back,
its strap repaired with a crude knot instead of a buckle.
His face is lost in the shadow of the hood, only a hard jawline and the faint glint of one eye visible.
He holds a single curved shortblade low and ready. The matching second blade is thrust into the
ground beside him, apart from him, its grip wrapping already unravelling.
Everything about him is fast, and everything about him is hastily made.
Coiled, silent, one step from vanishing.
```
**약점 시각화(#속도)**: 속도 특화 실루엣 + 급조된 마감. `fixed_review`의 "한 짝씩 배송됨" 농담을 **쌍단검을 떨어뜨려 놓아** 이미지에 박아 넣는다.

#### C05 — 나르시시스트 기사 (E05 · 정예 · 약점 #디자인 #감성)

```
[STYLE]
A knight in full gold-plated plate armor, mirror-polished to a blinding showroom shine, catching
and throwing the light from every surface. The armor is lavishly ornamented — engraved scrollwork,
decorative fluting, an extravagant plume, pauldrons oversized for silhouette rather than defense.
The plating is visibly thin, the articulated joints ornamental, built to be looked at rather than
struck. Not a single scratch on it.
Helmet held under one arm. A handsome symmetrical face, chin lifted, gaze angled deliberately past
the viewer toward some imagined audience, one hip cocked in a practiced heroic stance.
Radiant, immaculate, and entirely occupied with being seen.
```
**약점 시각화(#디자인 #감성)**: 흠집 하나 없는 갑옷 = 실전용이 아니라는 증거. 시선을 정면에서 비껴서 **혼자 화보를 찍고 있다.**

#### C06 — 던전 지배인 「답글 없는 사장」 (B01 · 1막 보스 · 약점 #응대 #개연성)

```
[STYLE]
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
**약점 시각화(#응대 #개연성)**: **얼굴 없음 + 빈 이름표 = 무응답의 그림.** 판타지 80으로 올린 효과가 가장 크게 나타날 에셋이다.

#### C07 — 플레이어 아바타 (주인공 · 평가 불가)

세계관상 주인공은 만물대장이 읽지 못하는 존재다. **아바타는 "이미지를 불러올 수 없음" 상태 그 자체**로 만든다 — 프롬프트가 아니라 설계 결정.

- **기본형(권장·이미지 생성 불필요)**: 회색 실루엣 + 깨진 이미지 아이콘 + 영원히 도는 로딩 인디케이터. **전부 CSS.** '평가 불가'를 UI가 직접 연기한다.
- **시네마틱용 실체(S51·S52 전용)**:
```
[STYLE]
A lean man in his thirties in plain modern streetwear — worn hoodie and jacket — standing
incongruously amid the dark fantasy lighting, arms at his sides, facing the viewer.
His face and upper head are rendered as a soft grey unresolved void, as though the image data
for him failed to load: not smoke, not shadow, but flat missing information with a faint
pixel-grid fringe at its boundary.
Everything else about him is rendered in full detail. Ordinary, unremarkable, and unreadable.
```

#### P01~P06 — 장비 아트 6종

전투 화면 ④구성품 리스트와 S31 진열대. **인물 없이 사물만.**

**공통 접미** (아래 6개 모두에 추가):
```
Single object only, no hands, no character, floating isolated against the dark atmospheric
backdrop, lit as a hero item shot. Square 1:1 composition.
```

| ID | 장비 | 프롬프트 본문 |
|---|---|---|
| P01 | 짝퉁 단검 | `A cheap counterfeit dagger, the pommel loose and slightly canted off the tang, crossguard stamped from thin sheet metal, leather grip wrap peeling away at one end, blade edge unevenly ground. Convincing at arm's length, obviously fake up close.` |
| P02 | 초대형 둔기 | `A monstrous two-handed war maul, a rough hewn granite head the size of a torso bound with heavy iron banding onto a short thick haft. Absurdly head-heavy proportions. Chipped, blood-darkened, and enormously heavy.` |
| P03 | 과장된 지팡이 | `An extravagantly ornate wizard staff, crusted with dozens of faceted crystals, gold filigree and hanging charms, top-heavy and impractical. Arcane light spills wastefully from the crystals in bright bleeding arcs that dissipate into drifting motes.` |
| P04 | 삐걱거리는 쌍단검 | `A pair of matched curved shortblades, deliberately NOT arranged as a pair — one at center, the other pushed to the far edge of the frame as if delivered separately. Grip wrappings unravelling, rivets visibly loose, blades nicked.` |
| P05 | 도금 갑옷 | `A gold-plated cuirass suspended on an invisible stand, mirror-polished to a blinding shine, lavishly engraved with scrollwork. Plating visibly thin, edges slightly warped, not a single battle scar on it. Decorative rather than protective.` |
| P06 | 노예 계약서 낫 | `A great scythe standing upright. Its blade is not forged metal but a thick fused stack of compressed legal parchment — dozens of laminated sheets pressed into a hardened curved edge, layered strata visible along the spine, edges yellowed and faintly stirring. Dark polished haft. Bureaucratic and lethal.` |

---

### 7.2 S11 맵 — 카테고리 탐색 / 배송 조회

#### B05 — 맵 배경 (9:16)

```
[STYLE]
Wide vertical 9:16 environment plate. A vast dark fantasy dungeon interior receding upward
into darkness — colossal stone arches, worn flagstone, guttering torchlight, deep volumetric haze,
scale that dwarfs anything human.
Empty of characters. Composed so that the center vertical band stays visually quiet and uncluttered,
with detail concentrated at the left and right edges.
Muted and atmospheric, meant to sit far behind interface elements.
```
**포인트**: **중앙 세로 대역을 비운다** — 그 위에 노드 트리(배송 추적 진행선)가 올라간다. 배경은 어둡고 조용해야 UI가 읽힌다.

#### I01~I06 — 노드 아이콘 6종

아이콘은 **커머스 층**이라 스타일 바이블을 쓰지 않는다.

**`[ICON]` 전용 접두**:
```
Minimal app category icon, simple geometric symbol, single flat color on transparent background,
uniform 2px stroke weight, rounded line caps, no gradient, no shadow, no perspective,
centered with even padding, clean modern e-commerce app iconography.
```

| ID | 노드 | 커머스 표현 | 프롬프트 본문 |
|---|---|---|---|
| I01 | 일반 전투 | 일반 배송 | `a closed cardboard shipping box seen at a slight angle` |
| I02 | 정예 전투 | 특급 배송 | `a shipping box with a small speed chevron mark beside it` |
| I03 | 상점 | 진열대 | `a retail shelf rack holding three items` |
| I04 | 휴식 | 고객센터 | `a headset with a microphone boom` |
| I05 | 이벤트 | 푸시 알림 | `a bell with a small notification dot` |
| I06 | 보스 | 본사 직영 | `a plain office tower, flat front elevation` |

#### I07~I10 — 4계열 심볼

| ID | 계열 | 프롬프트 본문 |
|---|---|---|
| I07 | 품질/마감 | `[ICON] a magnifying glass over a small stitched seam` |
| I08 | 성능/최적화 | `[ICON] a gauge dial with a needle` |
| I09 | 배송/CS | `[ICON] a delivery truck in side silhouette` |
| I10 | 감성/디자인 | `[ICON] a single star with one softly rounded edge` |

#### I11~I14 — 성향 4종 뱃지

**`[BADGE]` 접두**: `Circular achievement badge for a shopping app, simple emblem inside a plain circle, two flat colors, no gradient, no text, transparent background.`

| ID | 성향 | 프롬프트 본문 |
|---|---|---|
| I11 | 팩트 폭격 | `[BADGE] a downward arrow striking a small target dot dead center` |
| I12 | 힙스터 인증 | `[BADGE] a pair of round eyeglasses` |
| I13 | 진상 접수 | `[BADGE] a raised hand with one finger extended in objection` |
| I14 | 바이럴 확산 | `[BADGE] three small dots connected by lines spreading outward from one point` |

---

### 7.3 S01 스플래시 / S02 메인 메뉴

#### I15 — 앱 아이콘 / 로고 심볼

```
[ICON]
A single symbol merging a shopping bag silhouette with a five-pointed star, the star occupying
the position of the bag's fold or handle. Solid single color, perfectly geometric,
legible at 24 pixels.
```
**⚠ 「만물마켓」 글자는 이미지로 만들지 않는다.** 심볼만 생성하고 워드마크는 웹폰트로 조판. 이미지 모델이 쓴 한글은 반드시 깨진다.

#### V01 — 타이틀 히어로 (16:9)

```
[STYLE]
Wide 16:9 key visual. A colossal dungeon gate of black stone and iron rising out of drifting mist,
carved with ancient sigils, torch sconces guttering along its flanks, the scale monumental and
oppressive. Storm-dark sky above, a broken causeway leading up to it.
Nailed at eye level onto the ancient iron of the gate is one small, mundane, perfectly ordinary
delivery notice slip — a plain rectangle of pale paper, crisp and modern, slightly curling at one
corner, utterly out of place. It is small in the frame but lit just enough to be found.
Everything else in the image is played completely straight: awe, dread, and grandeur.
```
**포인트**: **80:20이 한 장에 그대로 들어간 이미지다.** 화면의 95%는 진지한 다크 판타지, 딱 한 군데 부재중 배송 안내문. 이 게임 전체가 이 구도다. 문구는 이미지에 넣지 않고 코드로 얹는다.

---

### 7.4 S12 층 진입 로딩 / S33 이벤트 — 가짜 광고 배너 ★ 개그 핵심

`art-direction-v1.md` §2.2의 "짧은 팁 = 가짜 광고 배너" 자리. **유일하게 커머스가 우위인 아트**이고, 유머 밀도가 가장 높다.

**`[BANNER]` 접두**:
```
Horizontal promotional banner artwork, 3:1 aspect ratio.
Dark fantasy subject rendered seriously and in full detail, placed on the left third,
lit as a hero product shot against a dark atmospheric gradient.
The right two-thirds of the frame is kept visually quiet and near-empty for text overlay.
No text, no letters, no logos, no price tags anywhere in the image.
```

| ID | 카피 (코드로 얹을 문구) | 프롬프트 본문 |
|---|---|---|
| B01 | 「전설의 검, 오늘만 무료배송」 | `[BANNER] A legendary greatsword, ornate and rune-etched, blade faintly luminous, standing upright and magnificent on the left — and beside it on the ground, a plain flattened cardboard shipping box.` |
| B02 | 「용사님, 장바구니에 담긴 상품이 있어요」 | `[BANNER] A dented iron warhelm and a rolled sigil scroll resting inside a plain steel wire shopping cart, abandoned in the dark, dust settling on them.` |
| B03 | 「★5 리뷰 이벤트 — 참여만 해도 적립금」 | `[BANNER] A heaped spill of ancient gold coins on the left, glinting in the dark, with a single folded blank parchment slip resting on top of the pile.` |
| B04 | 「부활 정기구독, 첫 달 50% 할인」 | `[BANNER] A full suit of plate armor lying collapsed and empty on the ground, neatly arranged as if folded for retail display, a small blank tag ring hooked to the gorget.` |

**설계 규칙**: 카피는 **이미지에 넣지 않고 코드로 오버레이**한다. 배너 4장 × 카피 20개 = 80조합. 로딩할 때마다 랜덤.

---

### 7.5 S23 전투 종료 모달

#### V02 — 회원 탈퇴 (패배) · 4:3

```
[STYLE]
Wide 4:3 composition. A single empty steel wire shopping cart standing alone at the center of a
vast dark void, tipped very slightly, one wheel turned outward. Completely empty.
A shaft of cold pale light falls on it from far above; everything beyond it dissolves into darkness.
No figure, no debris, nothing else in the frame.
Still, quiet, and final.
```
**포인트**: 죽음을 시체나 묘비로 그리지 않는다. **어둠 속에 홀로 남은 빈 장바구니.** 커머스 문법 안에서 상실을 표현하는 게 이 게임의 톤이다.

#### V03 — 구매 확정 (승리) · 4:3

```
[STYLE]
Wide 4:3 composition. A sealed cardboard shipping box resting squarely at center on ancient
flagstone, packing tape neatly applied, corners crisp and undamaged. A single blank paper slip
rests on its lid. Warm torchlight from the left, the darkness beyond receding.
Orderly, complete, and quietly satisfying in the plain way a finished delivery is satisfying.
```

---

### 7.6 S50 만물대장 / S51 시네마틱 / S52 엔딩 (P1)

아트 방향상 **유일한 예외 레이어** — 명조체 + 장부 톤(ADR-010 기각안의 부분 채택). 스타일 바이블 대신 별도 톤.

**`[LEDGER]` 전용 접두**:
```
Aged paper ledger aesthetic. Sepia and bone-white palette, fine ruled grid lines, paper fiber
texture, faint ink bleed, engraved-line illustration style, restrained and archival.
No color accents, no modern elements, no text.
```

| ID | 화면 | 프롬프트 |
|---|---|---|
| V04 | S50 만물대장 | `[LEDGER] An immense vertical ledger standing open, pages taller than a cathedral, ruled columns receding upward beyond sight into darkness, every line of every column filled with dense uniform entry marks. Vast, orderly, indifferent.` |
| C08 | S51 「택배좌」 | `[LEDGER] A seated figure in heavy formal robes in engraved-line style, the face left entirely blank and unmarked. On the desk before them rests a single parcel with its shipping slip torn away. Symmetrical front-facing archival portrait.` |
| V05 | S52 탈퇴 엔딩 | `[LEDGER] A single ledger page torn cleanly from its binding, lying flat and completely blank — every ruled line present, not one entry written on any of them. Empty space around it. Quiet finality.` |

**포인트**: V05는 엔딩 A의 요약이다 — 평가받지 않을 권리 = **아무것도 기록되지 않은 한 장.**

---

## 8. X01~X09 특수 카드 스팟 (P1)

카드 41장 중 **특수 카드 9종만** 스팟 일러스트. 접두 16 + 접미 16은 타이포 처리(3절).

**`[SPOT]` 접두**:
```
Tiny spot illustration for a card game, single object or simple gesture, engraved-line style
with one flat warm accent color, transparent background, no frame, no border, no text,
legible at 64 pixels.
```

| ID | 카드 | 프롬프트 본문 |
|---|---|---|
| X01 | 배송 지연 | `[SPOT] a shipping crate with a small clock face set into its side` |
| X02 | 별점 테러 | `[SPOT] a single star with a crack splitting through it` |
| X03 | 안 시켰는데 이게 왔네요 | `[SPOT] an opened crate with an unidentifiable lumpy shape rising out of it` |
| X04 | 무료 나눔 | `[SPOT] an open palm offering a small wrapped bundle` |
| X05 | 정신적 피해보상 청구 | `[SPOT] a parchment sheet bearing a wax seal, torn at one corner` |
| X06 | 우리 애가 다쳤잖아요 | `[SPOT] a small bandaged doll held in an adult hand` |
| X07 | 내가 여길 다시 오나 봐라 | `[SPOT] a heavy shop door swinging shut, viewed from outside` |
| X08 | 별점 구걸 | `[SPOT] a hand with thumb raised, reaching in from the frame edge` |
| X09 | 결함의 무기화 | `[SPOT] a cracked blade held up like a tool, the fracture turned outward` |

---

## 9. 다음 단계

1. **5절 1차 검증 세트 6장 생성** → 스타일 A(다크) / B(하이 판타지) 확정
2. 확정 후 **ADR-011 기록** — 「아트워크 판타지 / UI 크롬 커머스」 층 분리로 ADR-010 부분 개정
3. 확정 스타일로 **P0 35개 에셋 생성** (앵커를 스타일 참조로 물려서)
4. 배경 제거 → `assets/`에 투명 PNG + 원본 병행 보관
5. **S20 전투 화면부터 그래픽 적용** — 프로토타입에 이미지만 끼워 넣어 체감 확인
6. 통과하면 MVP-1(S11 맵 → S30 보상 → S13 덱 뷰어) 본격 구현

> 밸런스 라운드 2는 이 트랙과 독립이다. 병행하거나, 플레이 체감 피드백이 모인 뒤 돌려도 된다.
