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
  개그는 5번 슬라이드의 "오배송" 한 방으로 충분하다.

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

> 그는 리뷰를 12,847건 썼다.
> 그중 아홉은 환불을 받아내기 위한 것이었다.
> 별점 하나로 가게가 문을 닫는 걸 여러 번 봤고, 그때마다 아무렇지 않았다.

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

> 어느 날 그는 싸구려 보조배터리 하나를 1위로 올렸다.
> 경쟁 상품에 별 하나씩을 꽂아 순위를 비운 뒤,
> 그 자리에 이 물건을 밀어 넣었다. 재미로 한 일이었다.

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

> 석 달 뒤, 그것은 충전 중에 부풀기 시작했다.
> 그가 산 것이었다. 그가 1위로 올린 것이었다.

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

> 그리고 터졌다.
> 그는 자기 리뷰의 피해자가 되어 죽었다.

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

> 물류를 맡은 심사위원 「택배좌」가 그의 사후 데이터를 집어 들었다.
> 그리고 잘못된 컨베이어에 올렸다.
> 훗날 그는 그것이 실수가 아니었다는 걸 알게 된다.

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

> 도착지는 ★0으로 꺼져가던 무명 모험가의 몸이었다.
> 소멸하는 자리에 그의 데이터가 덮어써졌고,
> 그 소란 중에 리뷰 카드 발급이 실패했다.

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

> 만물대장은 그를 읽지 못했다.
> 신의 심판도, 축복도, 저주도 그를 비껴갔다.
> 이 세계에서 그를 증명할 수 있는 것은 이제 그가 직접 남기는 기록뿐이다.

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
