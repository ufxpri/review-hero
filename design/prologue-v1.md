# 프롤로그 & 온보딩 v1 — 「이세계 리뷰어는 어떻게 시작했는가」

- 작성: 2026-08-05 · 지위: 도입부 시나리오의 정본. 설정 근거는 `worldview-v1.0.md` §2.1(빙의 경위)·§1.1(만물대장)이 우선한다.
- 화면: S00 프롤로그 슬라이드쇼(신규) → S05 서명 등록(신규) → 온보딩 1판(GDD §4.4)
- 이미지: 각 슬라이드 프롬프트는 본 문서에 함께 둔다. `tools/comfy/generate.py`가 이 파일도 정본으로 읽는다.

---

## 1. 왜 프롤로그가 필요한가

이 게임은 **첫 30초 안에 세 가지를 납득시켜야** 한다.

1. 왜 리뷰가 무기인가 (평가가 물리법칙인 세계)
2. 왜 하필 이 사람인가 (전설적 악성 리뷰어)
3. 왜 그가 특별한가 ('평가 불가' — 대장이 읽지 못하는 존재)

셋 다 규칙 설명으로는 안 들어간다. **이야기로 먼저 심고 규칙은 나중에 확인시킨다.**

그리고 결정적으로, 프롤로그는 **주인공이 자기 리뷰의 피해자가 되어 죽는다**는 이 게임의 주제를 30초에 요약한다. 이걸 모르고 시작하면 이후의 모든 자학 개그가 안 웃긴다.

## 2. 진행 규칙

- 슬라이드 6장. 각 장은 **이미지 1 + 본문 2~3줄**.
- 자동 진행 없음 — 클릭/스페이스로 넘긴다. 읽는 속도는 플레이어가 정한다.
- **건너뛰기 항상 노출.** 2회차 플레이어를 붙잡지 않는다.
- 마지막 슬라이드에서 서명 등록(S05)으로 이어진다. 슬라이드와 서명은 **한 흐름**이다 —
  "당신을 증명할 방법이 서명뿐"이라는 6번 슬라이드의 결론이 곧 다음 화면의 이유가 된다.
- 톤: worldview §5 "세계는 진지하고, 그 세계의 규칙이 웃기다". **프롤로그는 진지하게 간다.**
  개그는 P4 "오배송" 한 방으로 충분하다.

### 문체 규칙 — 설명하지 말고 보여준다

초고가 "AI 티가 난다"는 지적을 받았다. 원인은 네 가지였고, 전부 **소설이 아니라 보고서**의 특징이다.

| 증상 | 초고 | 개정 |
|---|---|---|
| 감정을 대신 요약 | "그때마다 아무렇지 않았다" | "그가 세는 건 리뷰 수뿐이었다" — 성격을 행동으로 |
| 통계 보고체 | "리뷰를 12,847건 썼다. 그중 아홉은…" | "12,847번째 리뷰를 올리고 그는 라면 물을 올렸다" |
| 균일한 문장 길이 | 세 줄이 모두 같은 무게 | 길게-짧게 흔든다. P3b는 두 줄로 끊는다 |
| 어미 단조로움 | "~였다/~것이었다" 반복 | 명사 종결·구어·시각 정보를 섞는다 |

**규칙 넷**
1. 결론을 내려주지 않는다. 장면을 놓고 독자가 느끼게 둔다.
2. 구체적 사물 하나로 전체를 암시한다 — 라면 물, 삼천 원, 새벽 세 시 십사 분.
3. 문장 길이를 흔든다. 가장 중요한 줄을 가장 짧게.
4. 주제문은 아이러니로만 말한다. "그는 자기 리뷰의 피해자가 되어 죽었다"(설명) →
   "이번에는 그가 평가받을 차례였다"(아이러니).

---

## 3. 서사 구조 v3 — 14장 (구성안)

> ⚠ **worldview-v1.0.md §2.1 개정 필요.** 현행 정본은 "리뷰 12,847건, 9할이 환불 목적 악성
> 리뷰"이고 "자기가 1위로 올려놓은 불량 보조배터리의 폭발"로 죽는다고 되어 있다.
> 아래 v3는 그와 다르다 — 조작이 아니라 **필력으로 얻은 명성**이고, 죽음의 원인은
> 자업자득이 아니라 **돈에 혹한 협찬**이다. 이 구성이 확정되면 세계관 정본을 함께 고친다.

### v2에서 무엇이 틀렸나

v2(및 초기 7장)는 주인공을 **순위를 조작하는 악당**으로 잡았다. 그러면 두 가지가 깨진다.

1. **게임 메커닉과 어긋난다.** 이 게임의 자원은 「필력」이고 카드는 완성된 리뷰다.
   조작꾼이 아니라 **글을 잘 쓰는 사람**이어야 리뷰가 무기라는 설정이 성립한다.
2. **주제가 얕다.** "악플러가 자기 악플에 당한다"는 인과응보 우화에 그친다.

v3의 주제는 다르다 — **실력으로 얻은 명성을 돈에 팔았고, 그 물건이 그를 죽인다.**
뒷광고·협찬이라는 현대적 풍자 축이 들어오고, 이 게임이 계속 다룰 "평가의 신뢰"라는
문제와 직결된다. 그가 이세계에서 잃는 것이 **명성**인 이유도 여기서 나온다.

### 필수 전달 항목 12개

① 현실의 그는 폐인이다 ② 돈은 있지만 실력이 아니다 ③ 쇼핑 중독 ④ **글을 잘 쓴다**
⑤ **사람들이 그를 네임드로 만들었다** ⑥ **광고 협업 제안과 금액** ⑦ **리뷰를 쓰다 죽는다**
⑧ 세계의 법칙 ⑨ 심사위원 = 신 ⑩ 오배송·덮어쓰기 ⑪ 평가 불가의 의미 ⑫ 목표

---

### 1막 · 현실 — 필력으로 얻고 돈으로 잃는다 (7장)

- **P01 폐인, 그리고 우연한 돈**
  - 전달: 그가 누구인지 + 돈의 출처
  - 낮에도 커튼이 쳐진 방 / 주식이 한 번 터졌다. 실력이 아니라는 건 그도 안다 / 돈은 늘었고 나갈 일은 없다
  - 이미지: 커튼 친 방, 모니터의 붉고 푸른 호가창 불빛
- **P02 쇼핑**
  - 전달: 그의 중독. 살아 있음을 느끼는 유일한 순간
  - 살 이유가 없는 걸 산다 / 뜯는 삼 초가 좋아서 산다 / 뜯지도 않은 상자가 벽을 따라 쌓인다
  - 이미지: 방을 채운 미개봉 택배 상자 더미
- **P03 필력**
  - 전달: **왜 그가 리뷰로 싸울 수 있는가** — 게임 메커닉의 근거
  - 심심해서 후기를 하나 썼다 / 학교 다닐 때는 글 좀 쓴다는 소리를 들었다 / 신랄했고, 정확했고, 웃겼다
  - 이미지: 키보드 위의 손, 화면에 길게 쓰인 글
- **P04 네임드** ★핵심
  - 전달: 권력의 출처. **그가 조작한 게 아니라 사람들이 쥐어줬다**
  - 사람들이 상품이 아니라 그의 이름을 검색하기 시작했다 / 그가 별 하나를 주면 그 물건은 팔리지 않았다 / 그는 아무것도 조작하지 않았다
  - 이미지: 그의 리뷰 아래 길게 달린 반응들 / 혹은 그를 인용한 화면들
- **P05 메일** ★핵심 — 전환점
  - 전달: 타락의 순간. 유혹이 구체적이어야 한다
  - 광고 협업 제안이 왔다 / 본문 맨 아래에 금액이 적혀 있었다 / 그가 한 달에 쓰는 돈보다 컸다
  - 이미지: 메일 화면, 커서가 멈춘 자리
- **P06 그 물건**
  - 전달: 그는 알았다. 알면서도 했다
  - 상자에는 인증 표시가 없었다 / 그런 건 한눈에 안다. 삼 년을 봤으니까 / 그래도 뜯었다
  - 이미지: 인증 마크 없는 포장을 뜯는 손
- **P07 마지막 문장**
  - 전달: **리뷰를 쓰다 죽는다** — 이 게임 전체의 그림
  - 충전기에 꽂고 첫 문장을 적기 시작했다 / 「이 제품은」까지 썼을 때 뭔가 부푸는 소리가 났다 / 새벽 세 시 십사 분, 그의 마지막 리뷰는 끝내 완성되지 못했다
  - 이미지: 폭발 (기존 P3b 재활용) 또는 커서가 깜빡이는 미완성 문장 → 섬광

### 2막 · 이세계 — 여기가 어디인가 (4장)

> **여기서 화풍이 바뀐다.** P07(현실) → P08(이세계)의 단절이 전이 연출.

- **P08 만물대장** ★핵심 — 세계의 법칙
  - 전달: **왜 리뷰가 무기인가**
  - 창조신들은 세계를 만들고 유지보수를 하지 않았다 / 원성이 커지자 고치는 대신 원성을 시스템으로 흡수했다 — "그렇게 불만이면 너희가 평가해라" / 그날부터 별점이 축복과 저주를 대신했다
  - 이미지: 만물대장 전경 (기존 P6 재활용 가능)
- **P09 심사위원**
  - 전달: 신 = 운영자. 나중에 적이 될 대상
  - 창조신은 은퇴했고 계정 100개가 남았다 / 신성의 본질은 권능이 아니라 **리뷰 가중치**다 / 한 건이 필멸자 만 건과 같다
  - 이미지: 늘어선 심사대, 얼굴 없는 로브 인물들
- **P10 오배송**
  - 그날 열두 시간째 근무 / 손에 든 상자에는 이름이 없었다. 그런 건 가끔 온다 / 왼쪽 벨트에 올렸다. 오른쪽이었어야 했다
  - 이미지: 물류 창고 (기존 P4)
- **P11 덮어쓰기**
  - 도착한 곳에는 이름을 얻지 못한 채 꺼져가던 젊은이가 있었다 / 별 영 개 / 두 기록이 한자리에서 겹쳤고, 그 소란 중에 카드 한 장이 발급되지 않았다
  - 이미지: 겹쳐지는 두 형체 (기존 P5)

### 3막 · 각성 — 그래서 뭘 하나 (3장)

- **P12 평가 불가**
  - 눈을 떴을 때 대장은 그를 넘어갔다 / 빈칸 하나가 있을 뿐이었다
  - 이미지: 거대한 장부 앞의 작은 사람 (기존 P6)
- **P13 자유이자 저주**
  - 전달: 명성(RP)의 근거, 서명의 이유
  - 심판도 축복도 저주도 그를 비껴간다 / 대신 그가 무엇을 하든 대장에 남지 않는다 / 현실에서 명성으로 살았던 사람이, 이번에는 이름조차 기록되지 않는다
  - 이미지: 그림자가 없는 발밑 / 손을 통과하는 빛
- **P14 선언** — 목표 제시
  - 전달: 이 게임이 무엇을 하는 게임인가
  - 그가 아는 방법은 하나뿐이었다 / 여기서도 그는 쓰기로 한다
  - 이미지: 깃털펜을 집어 드는 손
  - → 서명 등록(S05)으로

### 세계관 정본에 반영해야 할 변경

| 항목 | 현행 worldview §2.1 | v3 |
|---|---|---|
| 명성의 출처 | 순위 조작 | **필력. 사람들이 추대** |
| 리뷰 성격 | 9할이 환불 목적 악성 | 신랄하지만 정확하고 재미있다 |
| 죽음의 원인 | 자기가 1위로 올린 물건 | **협찬 받은 미인증 배터리** |
| 죽는 순간 | 폭발 사고 | **리뷰를 쓰던 중** |
| 주제 | 인과응보 | **명성을 돈에 판 대가** |

### 분량

14장 × 약 8초 = **2분 내외**. 건너뛰기 상시 노출.
기존 이미지 재활용 4컷(P3b·P4·P5·P6), 신규 촬영 약 9컷.

---

## 3. 두 개의 화풍 — 단절이 곧 전이다

**1차 생성 실패 기록**: 6장 전부에 판타지 일러스트 접두를 붙였더니 P1~P3이 중세 로브·갑옷 기사·
벽돌 더미로 나왔다. 현대 원룸도, 노트북도, 보조배터리도 모델이 전부 판타지로 치환해 버린다.

원인은 프롬프트가 아니라 설계였다. **프롤로그 1~3은 판타지가 아니라 현실이다.**
주인공은 현대에서 죽는다(worldview §2.1). 그러니 화풍도 둘로 갈라야 한다.

| | 슬라이드 | 화풍 | 근거 |
|---|---|---|---|
| **현실** | P1 · P2 · P3a · P3b | 판타지 요소 0. 차갑고 평범하고 조금 추한 현대 일러스트 | 죽기 전의 그는 그냥 사람이다 |
| **이세계** | P4 · P5 · P6 | 따뜻한 판타지 일러스트, 장엄한 스케일 | 만물대장의 세계 |

**P3b → P4에서 화풍이 끊기는 것이 전이 연출 그 자체다.** 자막 없이 "여기서 세계가 바뀌었다"가
읽힌다. 폭발(P3b)을 마지막 현실 컷으로 두고 바로 물류 창고(P4)로 넘긴다.

또한 P3 폭발은 한 컷으로 감당이 안 돼 **두 컷으로 쪼갠다** — 부풀어 오르는 예감(P3a)과
터지는 순간(P3b). 이 장면이 게임 주제의 핵심이라 시간을 쓸 값어치가 있다.

### 2차 생성 교훈 — 핵심 사물을 첫 문장으로

2차에서 7장 중 4장이 또 실패했다(P2 빈 선반 / P3a 멀쩡한 신품 / P3b 폭발 없는 방 / P6 대장 없는 인물).
공통점은 하나였다 — **실패한 4장 전부 그 컷의 핵심 사물이 문장 뒤에 있었다.**

확산 모델은 앞쪽 토큰에 가중치를 준다. "A dark modern bedroom at night. On the bedside table,
the power bank has erupted…"라고 쓰면 모델은 **방**을 그리고 폭발을 잊는다.

**규칙: 각 프롬프트의 첫 문장은 그 컷의 주어(사물 또는 사건)로 시작한다.**
배경·조명·분위기는 그 뒤에 붙인다. 슬라이드는 한 장에 하나만 말하므로 이 규칙이 특히 강하게 적용된다.

### `[REAL]` — 현실 화풍 접두 (P1~P3b)

```
Contemporary realistic digital illustration, present day, no fantasy elements whatsoever.
Painterly but restrained brushwork, muted naturalistic color, documentary framing.
Cold ordinary interior lighting — fluorescent, monitor glow, streetlight through blinds.
Palette: grey-green, dull beige, cold blue screen light, dirty white.
Modern everyday objects only: laptops, phones, plastic packaging, cheap furniture, cables.
Nothing is heroic. Nothing is magical. Slightly ugly and completely mundane.
Cinematic wide 16:9 composition.
```

**현실 슬라이드 전용 네거티브** (공용 네거티브에 이어 붙인다):
```
fantasy, medieval, armor, robe, cloak, hood, sword, castle, dungeon, torch, magic, glowing runes,
forest, ruins, stone architecture, adventurer, knight, warrior, painterly fantasy, epic lighting
```

---

## 3.1 슬라이드 — 현실 (P1 · P2 · P3a · P3b)

### P1 — 12,847건

> 12,847번째 리뷰를 올리고 그는 라면 물을 올렸다.
> 별 하나, 사진 세 장, 반품 사유는 '생각과 달랐음'.
> 문 닫은 가게가 몇 곳인지는 세지 않았다. 그가 세는 건 리뷰 수뿐이었다.

```
Contemporary realistic digital illustration, present day, no fantasy elements whatsoever.
Painterly but restrained brushwork, muted naturalistic color, documentary framing.
Cold monitor glow as the only light source in a dark room.
Palette: grey-green, dull beige, cold blue screen light, dirty white.
Cinematic wide 16:9 composition.

A cramped modern studio apartment at night, shot from behind a man in a plain grey hoodie
slumped in a cheap office chair at a particleboard desk. A laptop screen fills his silhouette
with cold blue light, showing an endless scrolling column of short review entries.
Instant noodle cups, a phone face down, tangled charging cables and a dead potted plant
crowd the desk. Unmade bed and a closed door behind him. No other light in the room.
Quiet, ordinary, and faintly monstrous.
No text, no readable letters, no logos.
```

### P2 — 1위

> 그날 밤에는 순위표를 정리했다. 1위부터 9위까지 별 하나씩.
> 사흘이면 자리가 빈다는 걸 그는 알고 있었다.
> 빈자리에 올려둔 건 삼천 원짜리 보조배터리였다. 이유는 없었다.

```
Contemporary realistic digital illustration, present day, no fantasy elements whatsoever.
Painterly but restrained brushwork, muted naturalistic color, product-shelf framing.
Cold retail fluorescent lighting from directly above.
Palette: dull beige, cold blue, plastic white, dirty grey.
Cinematic wide 16:9 composition.

A single cheap white plastic USB power bank, close to camera and filling the center of the frame,
sitting alone on a bare retail shelf under a hard fluorescent strip. It is the only product left
standing. Behind and below it, other boxed consumer electronics lie toppled, shoved aside and
half fallen off the shelves into shadow, packaging creased and torn.
The power bank is spotlit and absurdly glorified; everything else is discarded.
Played completely straight — the joke is that this is a plastic brick on a shop shelf.
No text, no readable letters, no logos, no price tags, no star icons.
```

### P3a — 예감

> 석 달 뒤, 머리맡에서 플라스틱이 천천히 갈라지는 소리가 났다.
> 삼천 원짜리였다. 그가 별 다섯을 주고 1위로 올려둔 바로 그것이었다.
> 그는 소리를 등지고 돌아누웠다.

```
Contemporary realistic digital illustration, present day, no fantasy elements whatsoever.
Painterly but restrained brushwork, muted naturalistic color, tight macro framing.
Only light is a small orange charging indicator and dim streetlight through a window.
Palette: near-black, dull plastic white, one small orange point.
Cinematic wide 16:9 composition.

A grotesquely swollen and deformed plastic power bank, its casing bulging outward like an
overinflated balloon, the seam split open along one edge, the top panel lifted and warped
away from the body, plastic stressed and whitened at the crease.
Extreme close-up, filling the frame, lying on a dark bedside table at night with a charging
cable still plugged in and a tiny orange LED glowing. Everything around it is black.
Utterly still, utterly ordinary, and wrong.
No text, no readable letters, no logos.
```

### P3b — 폭발

> 새벽 세 시 십사 분, 방이 한 번 밝아졌다.
> 이번에는 그가 평가받을 차례였다.

```
Contemporary realistic digital illustration, present day, no fantasy elements whatsoever.
Painterly brushwork with violent value contrast, documentary framing.
Palette: near-black room, searing orange-white at the center, hard cast shadows.
Cinematic wide 16:9 composition.

A violent explosion of searing white-orange fire bursting out of a small plastic power bank,
flame and sparks erupting from the split seam in a hard bright bloom that blows out the center
of the frame. Molten plastic spraying, a charging cable whipping away, debris in the air.
It sits on a bedside table in an otherwise pitch-dark modern bedroom, the blast throwing
hard shadows up the wall and across a rumpled bed. No person visible.
The fire is the subject and the brightest thing in the image by far.
Sudden, ugly, and small — the scale of a domestic accident, not a spectacle.
No text, no readable letters, no logos.
```

---

## 3.2 슬라이드 — 이세계 (P4 · P5 · P6)

여기서 화풍이 바뀐다. 따뜻해지고, 커지고, 장엄해진다.

### P4 — 오배송

> 물류를 맡은 심사위원은 그날 열두 시간째 근무 중이었다.
> 손에 든 상자에는 이름이 없었다. 그런 건 가끔 온다.
> 그는 왼쪽 벨트에 상자를 올렸다. 오른쪽이었어야 했다.

```
Hand-painted digital illustration for a stylized fantasy card game.
Bold confident brushwork with visible painterly strokes, monumental scale.
Palette: aged bronze, parchment cream, cold slate, one warm amber accent.
Visible canvas grain and ink texture. Cinematic wide 16:9 composition.

The interior of a cathedral-scaled celestial sorting warehouse, impossibly tall, ranks of
conveyor belts running off into golden haze in every direction, stacked with parcels and
sealed ledgers. In the foreground close to camera, a robed archivist with a blank unmarked
face holds one small plain parcel in both hands, arm extended, deliberately lowering it onto
the left-hand belt while the right-hand belt runs beside it. The gesture is the focus of the frame.
Monumental architecture, bureaucratic calm, one small deliberate wrong choice.
No text, no readable letters, no logos.
```

### P5 — 덮어쓰기

> 도착한 곳에는 이름을 얻지 못한 채 꺼져가던 젊은이가 있었다.
> 별 영 개. 소멸까지 얼마 남지 않은 몸이었다.
> 두 기록이 한자리에서 겹쳤고, 그 소란 중에 카드 한 장이 발급되지 않았다.

```
Hand-painted digital illustration for a stylized fantasy card game.
Bold confident brushwork with visible painterly strokes, ethereal light handling.
Palette: cold slate blue, dissolving pale gold, oxblood, ink brown.
Visible canvas grain. Cinematic wide 16:9 composition.

A young adventurer in worn leather collapsed on cracked stone in an empty hall, the body
already dissolving from the feet upward into drifting motes of pale light. Overlapping the
same space and slightly misaligned from it, a second translucent silhouette of a man in a
modern grey hoodie is settling in — the two figures ghosting through each other for one moment,
edges doubled. Beside them a small blank card lies face up on the stone, entirely empty.
Quiet, cold, and unceremonious.
No text, no readable letters, no logos, no writing on the card.
```

### P6 — 평가 불가

> 눈을 떴을 때, 대장은 그를 넘어갔다.
> 심판도 축복도 저주도 그의 이름을 찾지 못하고 지나쳤다.
> 이 세계에 그의 자리는 없었다. 빈칸 하나가 있을 뿐이었다.

```
Hand-painted digital illustration for a stylized fantasy card game.
Bold confident brushwork with visible painterly strokes, overwhelming vertical scale.
Palette: sepia, bone white, aged bronze, deep shadow, one warm amber accent.
Visible canvas grain and paper fiber texture. Cinematic wide 16:9 composition.

A colossal open ledger book, so vast it fills the entire frame and rises far beyond the top edge
into darkness, its two ruled pages towering like cliff faces. The pages are covered edge to edge
in dense uniform rows of tiny abstract entry marks receding upward into golden haze.
At the very bottom of the frame, dwarfed to almost nothing, a tiny lone figure in a modern grey
hoodie stands with his back to us, looking up at it. Directly at his eye level one single ruled
row is completely blank and empty, faintly glowing — a gap in the record.
The book is the subject; the person is a speck.
Overwhelming scale, absolute silence, one small absence.
No text, no readable letters, no logos, no legible writing — abstract entry marks only.
```

---

## 4. 서명 등록으로의 연결 (S05)

P6의 마지막 문장 "그를 증명할 수 있는 것은 그가 직접 남기는 기록뿐"이 곧 다음 화면의 지문이 된다.

> **리뷰어 등록 — 서명을 남겨주세요**
> 당신은 만물대장이 읽지 못하는 존재입니다. 대장에 이름이 오르지 않으니
> 이 서명이 당신을 증명하는 유일한 기록이 됩니다.

플레이어가 그린 서명은 `localStorage`에 획 단위로 저장되고, 이후 **모든 리뷰 카드 하단에 획순 그대로 다시 그어진다**. 카드를 낼 때마다 자기 손글씨가 재생되므로, "내가 쓴 리뷰"라는 감각이 매 턴 반복된다.

이것은 worldview §2.2의 **명성 = 만물대장 바깥의 기록**을 조작으로 옮긴 것이다. 대장에 못 남기니 손으로 남긴다.

## 5. 온보딩 1판으로의 연결

서명 등록 직후 GDD §4.4의 온보딩 1판으로 들어간다. 이미 설계된 것을 그대로 쓴다 —
E01T(무효 태그 없음, 팩트/일반 2종만으로 판정 학습) → … → B01T(의지 45, 야근 강요 미사용).

프롤로그가 심어야 할 것과 온보딩이 가르칠 것의 분담:

| | 담당 |
|---|---|
| **프롤로그** | 왜 리뷰가 무기인가 · 왜 이 사람인가 · 왜 '평가 불가'인가 |
| **서명 등록** | 이 리뷰는 내 것이다 |
| **온보딩 1판** | 태그 판정 3단계 · 인텐트 읽기 · 대상 지정 · 항복 |

프롤로그에서 규칙을 설명하지 않는다. 온보딩에서 세계관을 설명하지 않는다.

## 6. TBD

- 2회차 이후 프롤로그 자동 건너뛰기(설정에서 다시 보기)
- P4의 "실수가 아니었다"는 3막 복선(worldview §2.1) — 3막 공개 시점의 회수 연출 미설계
- 슬라이드 전환 연출: 현재 페이드. 종이 넘김/잉크 번짐 중 택일 검토
