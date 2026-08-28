# 프롤로그 & 온보딩 v1 — 「이세계 리뷰어는 어떻게 시작했는가」

- 작성: 2026-08-05 · 지위: 도입부 시나리오의 정본. 설정 근거는 `worldview-v1.0.md` §2.5(주인공)·§1.3(만물대장 6원칙)이 우선한다.
- 화면: S00 프롤로그 슬라이드쇼 → **S05 리뷰어 등록(signature.html — 이름+서명 한 페이지)** → 온보딩 1판(GDD §4.4)
- 이미지: 각 슬라이드 프롬프트는 본 문서에 함께 둔다. `tools/comfy/generate.py`가 이 파일도 정본으로 읽는다.

---

## 1. 왜 프롤로그가 필요한가

이 게임은 **첫 30초 안에 세 가지를 납득시켜야** 한다.

1. 왜 리뷰가 무기인가 (평가가 물리법칙인 세계)
2. 왜 하필 이 사람인가 (전설적 악성 리뷰어)
3. 왜 그가 특별한가 (올리는 건 누구나 한다 — 그런데 그를 향해서는 아무도 못 쓴다)

셋 다 규칙 설명으로는 안 들어간다. **이야기로 먼저 심고 규칙은 나중에 확인시킨다.**

그리고 결정적으로, 프롤로그는 **실력으로 얻은 이름을 돈에 판 대가로 죽는다**는 이 게임의 주제를 30초에 요약한다. 이걸 모르고 시작하면 이후의 모든 자학 개그가 안 웃긴다.

## 2. 진행 규칙

- 슬라이드 **15장, 총 62비트**(개정 시 이 숫자도 갱신할 것). 마지막 P15가 플레이어 입력이다. 한 장은 1~7비트이고 비트마다 텍스트가 넘어간다.
- 자동 진행 없음 — 클릭/스페이스로 넘긴다. 읽는 속도는 플레이어가 정한다.
- **건너뛰기 항상 노출.** 2회차 플레이어를 붙잡지 않는다.
- **등록은 슬라이드쇼가 끝난 뒤 서명 페이지(S05) 한 장에서 한다.** 이름 칸과 서명란이
  같은 페이지에 있고(ADR-020의 '한 번의 행위' 원칙 유지), 등록 즉시 완료 화면을 거쳐
  온보딩으로 이어진다. 슬라이드 중간에는 어떤 입력도 끼우지 않는다(ADR-022).
- 슬라이드와 등록은 **한 흐름**이다 —
  "잘 써서 사람들이 눌러 주면 상대가 깎인다"는 P14의 결론이 곧 다음 화면의 이유가 된다.
- 톤: worldview §5 "세계는 진지하고, 그 세계의 규칙이 웃기다". **프롤로그는 진지하게 간다.**
  개그는 P11의 "배송 오류로 처리해서 넘겨 준다" 한 방으로 충분하다.

### 문체 규칙 — 설명하지 말고 보여준다

초고가 "AI 티가 난다"는 지적을 받았다. 원인은 네 가지였고, 전부 **소설이 아니라 보고서**의 특징이다.

| 증상 | 초고 | 개정 |
|---|---|---|
| 감정을 대신 요약 | "그때마다 아무렇지 않았다" | "그가 세는 건 리뷰 수뿐이었다" — 성격을 행동으로 |
| 통계 보고체 | "리뷰를 12,847건 썼다. 그중 아홉은…" | "12,847번째 리뷰를 올리고 그는 라면 물을 올렸다" |
| 균일한 문장 길이 | 세 줄이 모두 같은 무게 | 길게-짧게 흔든다. P07b는 두 줄로 끊는다 |
| 어미 단조로움 | "~였다/~것이었다" 반복 | 명사 종결·구어·시각 정보를 섞는다 |

**규칙 넷**
1. 결론을 내려주지 않는다. 장면을 놓고 독자가 느끼게 둔다.
2. 구체적 사물 하나로 전체를 암시한다 — 라면 물, 삼천 원, 새벽 세 시 십사 분.
3. 문장 길이를 흔든다. 가장 중요한 줄을 가장 짧게.
4. 주제문은 아이러니로만 말한다.

### 번역투 제거 — 2차 개정

"영어적이라 읽기 부자연스럽다"는 지적을 받고 전면 재작성했다. 원인은 셋이었다.

| 증상 | 예 (개정 전) | 개정 후 |
|---|---|---|
| **목적어 실종** | "그래서 그는 사기 시작했다." — 영어 *So he started buying* 직역. "뭘 사는데?"만 남는다 | "그래서 인터넷으로 물건을 사기 시작했다. 무선이어폰이나 조립식 의자, 쓰지도 않을 주방 도구 같은 것들이었다." |
| **대구로 폼 잡기** | "아는 것과 인정하는 것은 다른 문제지만" / "평생을 이름으로 살았던 사람이 // 이름이 존재하지 않는 곳에 도착했다" | "자기 실력이 아니라는 건 본인도 알고 있었다. 그래도 통장에 찍힌 숫자는 그대로였다." |
| **명사구 나열** | "낡은 외투, 옆구리에 낀 두꺼운 장부, 목에 건 나무 패." — 영어 동격 나열 | "외투가 낡았고, 옆구리에 두꺼운 장부를 끼고 있었다. 목에는 나무 패를 걸었는데…" |

**추가 규칙 셋**
5. **목적어를 빼고 끊지 않는다.** 끊어서 폼이 나는 것 같아도 독자에겐 의문만 남는다.
6. **서술은 1인칭이다.** 나레이션은 주인공 '나'의 목소리로 쓴다. '그는'을 쓰지 않는다.
   소환사를 가리킬 때만 3인칭('남자', '소환사')을 쓴다.
7. **대구·대칭으로 마무리하지 않는다.** 소위 중2병 문장은 거의 전부 이 구조에서 나온다.
8. **명사를 나열하지 말고 서술한다.** 한국어는 동사로 흘러가는 언어다.

가장 큰 변화는 P14 마지막이다. 「─ 그럼, 내가 쓰지.」(선언) → 대화로 교체:
「**소환사** 뭐라고 쓰실 겁니까?」 / 「**{필명}** 일단 좀 보고요.」
리뷰어라면 선언하지 않는다. 일단 본다.

---

## 3. 서사 구조 v3 — 15장 (구성안)

> ✅ **반영 완료.** worldview §2.1(현 §2.5)은 아래 v3 기준으로 개정됐다(2026-08-05).

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
⑧ 세계의 법칙 ⑨ 신은 잘못 올라간 것을 지워 주던 존재였다 ⑩ 신이 사라져 이제 아무것도 안 지워진다 ⑪ 평가 불가 = 되받을 데가 없다 ⑫ 목표

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
  - 이미지: 폭발 (기존 P07b 재활용) 또는 커서가 깜빡이는 미완성 문장 → 섬광

## 4. 본문 15장 (확정)

**형식**: 인용문 안에서 빈 `>` 줄이 **비트 구분**이다. 한 슬라이드 안에서 클릭할 때마다
텍스트가 비트 단위로 넘어가고, 마지막 비트에서 다시 클릭하면 다음 슬라이드로 간다.
이미지는 슬라이드 단위로 유지된다 — 비주얼 노벨의 기본 문법.

---

### 1막 · 현실

#### P01 — 폐인, 그리고 우연한 돈

> 커튼을 마지막으로 걷은 게 언제인지 기억나지 않았다.
> 낮인지 밤인지는 모니터를 봐야 알았다.
>
> 오 년 전, 친구가 술자리에서 하이맥스라는 회사 얘기를 했다. 곧 큰 계약이 터진다고 했다.
> 그런 얘기를 진지하게 들은 적은 한 번도 없었는데, 그날은 모아둔 돈을 거의 다 넣었다.
>
> 여섯 달 만에 네 배가 됐다. 언제 팔아야 할지 몰라서 들고만 있었던 게 맞아떨어졌다.
> 내가 잘해서 번 돈이 아니라는 건 나도 알았다.
>
> 월세 걱정이 없어졌고, 학자금을 한 번에 갚았다.
> 다음 달을 계산할 일이 없어지니까 밖에 나갈 일도 없어졌다.

#### P02 — 쇼핑

> 돈은 있는데 쓸 데가 없었다. 그래서 물건을 샀다.
> 무선이어폰, 조립식 의자, 쓰지도 않을 주방 도구. 필요해서가 아니라 주문해 놓으면 기다릴 게 생겨서였다.
>
> 택배 상자를 뜯는 그 잠깐이 하루 중에 제일 나은 시간이었다.
> 밥은 세 끼 다 배달로 시켰다. 문 앞에 두고 가시라고 적어 놓으면 사람을 볼 일도 없었다.
>
> 나중에는 뜯지도 않은 상자가 벽을 따라 쌓였다.
> 치우지는 않았다. 치우면 방이 너무 조용해질 것 같았다.

```
Contemporary realistic digital illustration, present day, no fantasy elements whatsoever.
Painterly but restrained brushwork, muted naturalistic color, documentary framing.
Cold ordinary interior lighting. Palette: grey-green, dull beige, cold blue screen light, dirty white.
Cinematic wide 16:9 composition.

A dozen unopened cardboard delivery boxes stacked in a messy pile, filling the left half of the
frame and rising above head height, tape still sealed, dust on the top ones.
They are crammed into the corner of a small cluttered bedroom — an unmade bed with tangled
sheets on the right, a cheap desk with a laptop, clothes on the floor, one lamp on.
Shot from standing height inside the room. Lived-in and claustrophobic, not a warehouse.
The boxes fill roughly half the frame. Nobody is in it.
No text, no readable letters, no logos, no barcodes.
```

#### P03 — 필력

> 어느 날은 할 일이 정말 없어서, 그날 온 무선이어폰의 후기를 써 봤다.
> 학교 다닐 때 문예부였다. 십몇 년 만에 쓰는데도 문장은 쉽게 나왔다.
>
> **후기** '공간을 가득 채우는 사운드'라고 적혀 있는데, 제 방은 여섯 평입니다.
> 여섯 평을 채우는 데 실패한 스피커는 아직 못 봤습니다.
>
> 사람들이 재밌다고 했다. 그래서 다음 물건도 썼고, 그다음 것도 썼다.

```
Contemporary realistic digital illustration, present day, no fantasy elements whatsoever.
Painterly but restrained brushwork, muted naturalistic color, documentary framing.
Cold ordinary interior lighting. Palette: grey-green, dull beige, cold blue screen light, dirty white.
Cinematic wide 16:9 composition.

A laptop screen showing a review being written — a white page with several paragraphs of dark
body text and a row of small star shapes at the top, like an online shopping review form.
The screen fills about 60 percent of the frame, shot from slightly above and behind two hands
resting on the keyboard. Warm room light, a dark bedroom behind.
A pair of cheap wireless earbuds and their open case lie next to the laptop.
Clearly a document being typed, not code, not a terminal.
No readable letters — text renders as abstract grey line strokes. No logos.
```

#### P04 — 네임드

> 반년쯤 지나자 사람들이 물건 이름 대신 내 아이디를 검색했다.
>
> 내가 별 하나를 주면 그 제품은 안 팔렸다. 별 넷을 준 날은 밤에 품절이 났다.

```
Contemporary realistic digital illustration, present day, no fantasy elements whatsoever.
Painterly but restrained brushwork, muted naturalistic color, documentary framing.
Cold ordinary interior lighting. Palette: grey-green, dull beige, cold blue screen light, dirty white.
Cinematic wide 16:9 composition.

Six smartphones lying scattered and overlapping on a dark wooden table, seen from directly
above, filling the whole frame. Every screen is on and every one shows the same thing — the same
block of paragraphs with a row of small star shapes above it, copied and reposted across all of
them. The screens are the only light source, glowing up onto the table.
Casual and messy, like a pile of phones tossed down, not a product display.
No readable letters — text renders as abstract grey line strokes. No logos.
```

#### P05 — 메일

> 어느 날 메일이 왔다. 제목은 [보조배터리 체험단]이었다.
> 제품은 무료, 후기 한 편에 원고료는 따로. 액수가 내 한 달 생활비보다 많았다.
>
> 그 줄을 한참 봤다.
>
> 어차피 물건 받아서 써 보고 쓰는 건 삼 년째 하던 일이었다.
> 돈을 받는다고 없는 말을 쓸 것도 아니고.
>
> **답장** 보내주시면 써 보고 솔직하게 쓰겠습니다.

```
Contemporary realistic digital illustration, present day, no fantasy elements whatsoever.
Painterly but restrained brushwork, muted naturalistic color, documentary framing.
Cold ordinary interior lighting. Palette: grey-green, dull beige, cold blue screen light, dirty white.
Cinematic wide 16:9 composition.

A computer monitor filling almost the entire frame, shot straight on from close range,
displaying an open email — a white message window with a header block at the top and four
paragraphs of dark body text below it. Near the bottom, set apart with blank space around it,
sits one short isolated line. A blinking text cursor rests beside that line.
The screen is the only light; a dark room barely visible at the edges.
Unmistakably an email client on a desktop.
No readable letters — all text renders as abstract grey line strokes. No logos, no numbers.
```

#### P06 — 그 물건

> 사흘 뒤에 보조배터리가 왔다.
>
> 딱 봐도 허접하게 생겼다.
> 삼 년 동안 온갖 물건을 뜯어 봤으니 이런 건 만져 보면 안다.

```
Contemporary realistic digital illustration, present day, no fantasy elements whatsoever.
Painterly but restrained brushwork, muted naturalistic color, documentary framing.
Cold ordinary interior lighting. Palette: grey-green, dull beige, cold blue screen light, dirty white.
Cinematic wide 16:9 composition.

A rectangular white plastic power bank sitting in the middle of an opened cardboard box,
filling the center of the frame and clearly the subject, seen from directly above.
It rests on thin crumpled packing paper. The cardboard is plain brown with no printing at all.
Two hands hold the box flaps open at the edges of the frame.
A dark desk surface around the box, one warm lamp above.
The power bank is a plain white brick with a USB port on one edge, nothing printed on it.
No text, no readable letters, no logos, no certification marks, no stickers.
```

#### P07 — 마지막 문장

*(⚠ 현재 채택 이미지에 주황 충전 표시등이 안 보인다. 아래 프롬프트로 재생성 필요.)*

> 충전기에 꽂아 놓고 노트북을 열었다.
> 후기는 써 보면서 쓰는 편이다.
>
> 협탁 위에서 충전 표시등이 주황색으로 깜빡이고 있었다.
>
> 커서를 놓고 첫 문장을 쳤다.
>
> **「이 제품은」**

```
Contemporary realistic digital illustration, present day, no fantasy elements whatsoever.
Painterly but restrained brushwork, muted naturalistic color, documentary framing.
Palette: near-black room, cold laptop screen glow, one small orange indicator point.
Cinematic wide 16:9 composition.

A white plastic power bank sitting on a wooden bedside table in the foreground, close to camera
and sharply lit, a charging cable plugged into it and one small orange indicator light glowing on
its side. Behind it and slightly out of focus, an open laptop sits on a rumpled bed, its screen
throwing cold blue light up the wall of a dark bedroom.
The power bank occupies the lower third of the frame and is unmistakably the subject.
Completely still. Nothing has happened yet.
No text, no readable letters, no logos.
```

#### P07b — 폭발 ⚡

> 협탁 쪽에서 뭔가 부푸는 소리가 났다.
>
> 새벽 세 시 십사 분. 나는 폭발에 휩쓸려 죽었다.
>
> 내 마지막 후기는 거기서 멈췄다.

```
Contemporary realistic digital illustration, present day, no fantasy elements whatsoever.
Painterly brushwork with violent value contrast, documentary framing.
Palette: near-black room, searing orange-white at the center, hard cast shadows.
Cinematic wide 16:9 composition.

A white plastic power bank on a wooden bedside table splitting open and exploding — a hard
bright fireball of white-orange flame bursting straight up out of its cracked seam, with a spray
of orange sparks flying outward in all directions and a plume of dark smoke above.
The power bank itself is still visible at the base of the flame, cracked and deformed.
The blast lights a dark bedroom: hard shadows thrown up the wall, a rumpled bed, an open laptop
beside it with its screen washed out. A charging cable whipping through the air.
Sharp defined flame shapes and distinct individual sparks, not a soft glow or a blur.
No person visible. No text, no readable letters, no logos.
```


---

### 2막 · 이세계 — 소환

*(작성 규칙 — 인용문에서 `**이름**` 으로 시작하는 줄은 대사다. 빌드가 감지해 화자를 금색으로
렌더하고, 이어지는 줄은 화자 이름 폭만큼 들여쓴다. 인용문 밖에 두어야 슬라이드에 섞이지 않는다.)*

#### P08 — 소환

> 눈을 떠 보니 바닥에 그려진 원 안이었다.
> 원 밖에 낡은 외투의 남자가 서 있었다. 옆구리에는 두꺼운 장부,
> 목에는 별이 하나 새겨진 나무 패를 걸고 있었다.
>
> **소환사** 숨은 쉬어집니까?
>
> 대답이 안 나왔다. 조금 전까지 나는 내 방에 있었다.
>
> **소환사** 괜찮습니다. 몸이 아직 덜 붙은 겁니다. 숨만 쉬어지면 됩니다.
>
> 손을 들어 봤다. 내 손이 아니었다. 손가락이 더 길고, 없던 흉터가 있었다.
>
> **소환사** 기록만 불러올 수는 없어서요. 담을 그릇이 필요했습니다.
> 마침 비어 있는 사람이 있었습니다. 평가가 한 건도 올라간 적 없는 몸이에요.
> 그 사람한테는 미안하게 됐지만, 그래서 당신이 들어올 수 있었습니다.

```
Hand-painted digital illustration for a stylized fantasy card game.
Bold confident brushwork with visible painterly strokes, clean readable shapes.
Warm characterful lighting, visible canvas grain and ink texture.
Palette: aged bronze, parchment cream, cold slate, oxblood, one warm amber accent.
Cinematic wide 16:9 composition.

A summoning circle burned into a cracked stone floor, glowing faintly along its carved
grooves, seen at a low three-quarter angle. A man in a modern grey hoodie sits at its center,
one hand on the floor, looking down at himself. Just outside the circle stands a robed figure
holding a thick ledger under one arm, face in shadow, watching him.
A cold vaulted hall around them, mostly dark. The circle is the brightest thing in the frame.
No text, no readable letters, no legible runes — abstract carved marks only.
```

#### P09 — 이 세계의 규칙

> **소환사** 여기가 어딘지부터 말씀드리죠.
>
> **소환사** 이 세계는 리뷰로 돌아갑니다.
> 리뷰를 쓰면 그 대상은 쓴 대로 좋아지거나 나빠져요.
>
> **소환사** 대상마다 별점이 있습니다. 악평에 좋아요를 누르는 사람이 많을수록
> 별점이 실제로 내려가고, 별점이 0이 되면 세상에서 사라집니다. 비유가 아니라 진짜로요.
>
> **소환사** 리뷰는 만물대장이라는 장부에 올리는 방식으로 동작합니다.
> 올리는 건 아무나 할 수 있지만, 잘못되거나 악의적인 리뷰는 신이 지워 줬어요.
> 그래서 공정했습니다.
>
> **소환사** 그런데 어느 날 신이 사라졌습니다.
> 악성 리뷰가 안 지워지니, 악의 있는 리뷰가 판을 치게 됐죠.

```
Hand-painted digital illustration for a stylized fantasy card game.
Bold confident brushwork with visible painterly strokes, clean readable shapes.
Warm characterful lighting, visible canvas grain and ink texture.
Palette: aged bronze, parchment cream, cold slate, oxblood, one warm amber accent.
Cinematic wide 16:9 composition.

Hundreds of small flat wooden tags hanging on thin cords at many different heights, densely
filling the whole frame from top to bottom like a forest of wind chimes, receding into warm haze.
Each tag is carved with a row of small star shapes — a few have five, most have one or two,
some are blank and split. The tags are close to camera in the foreground and tiny in the distance.
Far below at the bottom edge, two small robed figures stand looking up.
Warm light filters down between the hanging tags.
No text, no readable letters — carved star shapes only.
```

#### P10 — 베스트 리뷰어

> **소환사** 보통 사람들은 뭐가 공정한 리뷰인지 모릅니다.
> 그냥 잘 쓴 글을 믿고 좋아요를 눌러요.
>
> **소환사** 그래서 체험단이 있었습니다. 물건을 먼저 받아 써 보고
> 믿을 수 있는 리뷰를 쓰는 사람들이요. 저희가 쓰고 신이 지워 주고, 그렇게 균형이 맞았습니다.
>
> **소환사** 신이 사라지자, 그중에 제일 잘 쓰던 자가 그냥 계속 썼습니다.
> 신의 자리를 잇겠다고 한 적도, 걸러 주겠다고 한 적도 없어요. 그냥 쓴 겁니다.
> 지워 줄 존재가 없으니 뭐가 맞는지는 사람들이 믿는 쪽으로 정해졌고, 다들 제일 잘 쓴 글을 믿었습니다.
>
> **소환사** 그게 이상하다고 한 리뷰어들 앞으로는 그자가 별 하나짜리를 써서 올렸습니다.
> 그 글도 좋아요를 받았어요. 사람들이 눌러 줬고, 별점 0이 되어 사라졌죠.
> 그자가 지운 게 아닙니다. 그자가 쓰고 사람들이 지웠어요.
>
> **소환사** 지금도 그자를 까는 글은 계속 올라갑니다. 막을 방법이 없으니까요.
> 그런데 아무도 안 누릅니다. 안 눌린 글은 올라가 있어도 아무것도 못 깎아요.
>
> **소환사** 그자는 매주 뽑던 「베스트 리뷰어」였습니다. 삼 년째, 그 자리가 그대로예요.

```
Hand-painted digital illustration for a stylized fantasy card game.
Bold confident brushwork with visible painterly strokes, monumental scale.
Palette: aged bronze, parchment cream, cold slate, oxblood, one warm amber accent.
Visible canvas grain and ink texture. Cinematic wide 16:9 composition.

A long horizontal row of bare iron hooks on a stone wall, filling the width of the frame,
nearly every hook holding nothing. Dust and cobwebs stretch between them.
Only three wooden tags are still hanging, spaced far apart along the row — the one on the left
crowded with five carved stars, the other two carved with a single star each, all three cords
grey with dust. Every hook between them is empty.
Shot straight on from a few paces back so the long emptiness of the row dominates.
Warm dim light from one side, a neglected ceremonial hall behind.
Nobody is in the frame. The empty hooks are the subject.
No text, no readable letters — carved star shapes only.
```

*(P10 시점 주의 — 「그냥 계속 썼습니다」는 **소환사의 제한 시점**이다. 밖에서 본 그자는
그냥 썼고, 시작이 선의였다는 것(worldview §2.1, ADR-032)은 3막에서 그자 본인의 입으로
밝혀진다. 이 대사를 정본 쪽 서술로 '정정'하지 말 것 — 어긋남이 의도다. ADR-034)*

#### P11 — 그래서 당신

> **소환사** 여기 사람은 두 번을 못 씁니다. 한 번 올리면 대장에 작성자가 남아서 바로 조회당하고,
> 그다음은 저희가 당한 그대로예요.
> 당신은 다른 세계에서 왔으니까, 아무리 조회해도 안 나옵니다.
>
> 나는 목을 만져 봤다. 아무것도 걸려 있지 않았다.
>
> **소환사** 조회만 그런 게 아닙니다. 그 몸에도 카드는 있어요. 대장에 그대로 있고,
> 누구든 당신을 대상으로 써서 올릴 수 있습니다.
> 그런데 안에 든 사람이 바뀌었으니, 대장이 그 글을 어디로 보낼지 모릅니다.
> 올라가긴 해도 붕 뜬 채로 남아요.
>
> **소환사** 당신은 되받을 데가 없습니다. 그래서 밖에서 부릅니다.
>
> **소환사** 그자 밑에서 물류를 쥔 분이 계십니다. 다들 「택배좌」라고 부르는데,
> 그분이 당신을 이곳으로 이끌어 주셨어요.
> 죽는 순간까지 뭘 평가하고 있던 사람의 정보를 배송 오류로 처리해서 넘겨 주십니다.
>
> **소환사** 당신이 마지막으로 쓴 문장도 압니다. 「이 제품은」.
>
> **소환사** ……그 뒤가 궁금해서 견딜 수가 없었어요.

```
Hand-painted digital illustration for a stylized fantasy card game.
Bold confident brushwork with visible painterly strokes, clean readable shapes.
Warm characterful lighting, visible canvas grain and ink texture.
Palette: aged bronze, parchment cream, cold slate, oxblood, one warm amber accent.
Cinematic wide 16:9 composition.

Two bare open palms held up close to camera, filling most of the frame, seen from above as the
owner looks down at his own hands. Plain ordinary skin — smooth, clean, entirely blank.
Absolutely nothing is drawn, printed, tattooed, branded or carved on them.
Faint warm light falls across the palms. In the blurred background a robed figure stands
watching, and a stone hall recedes into darkness beyond.
The blankness of the skin is the subject of the image.
No text, no letters, no symbols, no runes, no tattoos, no markings, no lines drawn on the skin.
```

#### P12 — 이름

> **소환사** 만물대장에 리뷰를 쓰려면 이름이 필요합니다.
> 여기 이름은 추적당하니까, 가명이 필요해요.
> 저쪽 세계에서 쓰시던 이름이면 됩니다.
>
> 있었다. 삼 년을 그 이름으로 썼다.
> 그 이름 앞에 사람들이 줄을 섰다. 배터리를 보낸 쪽도 그중 하나였다.
>
> **소환사** 미리 말씀드리자면, 당신이 처음은 아닙니다.
> 삼 년 동안 계속 사람을 불렀고, 지금 여기 계신 분은 당신뿐입니다.
>
> **소환사** 장부에는 그분들이 쓰시던 자리가 그대로 있습니다.
> 이름을 올리시면 그 자리를 이어받으시는 겁니다 — 앞선 분들이 남긴 삼 년치 글도 같이요.
> 지울 수는 없으니까… 읽으면서 가시면 됩니다.
>
> **소환사** 그분들하고 저를 묶어서 부르는 말이 있습니다. 제가 지었어요.
>
> **소환사** ─ 환불원정대.
> 그자한테 받아낼 건 사과나 반성이 아니라 뺏긴 별점이니까요. 그건 환불이죠.
>
> **소환사** 그자를 끌어내리는 게 끝이 아닙니다.
> 정직한 리뷰에 좋아요가 달리고, 엉터리는 좋아요를 못 받아 묻히는 세상.
> 저희가 만들려는 건 그겁니다. 원래 신이 지키던 게 그거였고요.
>
> **소환사** 같이 가 주시겠습니까.

```
Hand-painted digital illustration for a stylized fantasy card game.
Bold confident brushwork with visible painterly strokes, clean readable shapes.
Warm characterful lighting, visible canvas grain and ink texture.
Palette: aged bronze, parchment cream, cold slate, oxblood, one warm amber accent.
Cinematic wide 16:9 composition.

A plain blank wooden tag on a leather cord, held out on an open palm in the center of the frame,
close to camera and filling a third of it. The tag is completely uncarved — smooth, pale,
not a single star mark on it, unlike the worn star-carved tag hanging at the neck of the figure
offering it. A second hand reaches in from the opposite edge to take it.
Warm low light, a dark stone hall behind, everything else out of focus.
The empty tag is the subject.
No text, no readable letters — the tag surface is blank.
```

---

### 3막 · 각성


#### P14 — 선언

> 여기서는 리뷰가 곧 힘이다.
> 잘 쓴 리뷰로 공감을 사면 사람들이 좋아요를 눌러 주고, 그만큼 상대가 깎인다.
>
> 저쪽에서 삼 년 동안 하던 일이 그거였다.
>
> 나는 깃펜을 집었다.
>
> **소환사** 마지막으로, 만물대장에 리뷰를 올리려면 이름과 서명을 등록해야 합니다.
> 등록하고 나면 바로 쓰실 수 있어요.

```
Hand-painted digital illustration for a stylized fantasy card game.
Bold confident brushwork with visible painterly strokes, clean readable shapes.
Warm characterful lighting, visible canvas grain and ink texture.
Palette: aged bronze, parchment cream, cold slate, oxblood, one warm amber accent.
Cinematic wide 16:9 composition.

A long pale quill pen resting on a worn stone ledge, close to camera, with a hand reaching
down into frame to pick it up — fingers just about to close on it. Beside the quill sits a
squat ink pot and a single blank sheet of parchment.
Warm low light from one side, the background falling into soft dark.
No text, no readable letters, no writing on the parchment.
```


#### P15 — 등록

*(프롤로그 마지막 슬라이드. 여기서 게이트 「서명 남기러 가기」로 넘어가고,
등록(이름+서명)은 signature.html 한 페이지에서 이루어진다 — ADR-022.
등록을 마치면 완료 화면에서 곧장 온보딩 1판으로 이어진다.)*

> 소환사가 장부를 밀어 놓았다. 이름 칸이 비어 있었다.

```
Hand-painted digital illustration for a stylized fantasy card game.
Bold confident brushwork with visible painterly strokes, clean readable shapes.
Warm characterful lighting, visible canvas grain and ink texture.
Palette: aged bronze, parchment cream, cold slate, oxblood, one warm amber accent.
Cinematic wide 16:9 composition.

A huge open ledger of pale parchment lying flat and filling the lower two thirds of the frame,
close to camera, its two facing pages ruled with long empty lines. One line near the centre is
completely blank and waiting. A hand holding a long pale quill hovers just above that blank line,
the nib almost touching the paper, ink gathered at the tip about to fall.
Warm candlelight from the left, a dark stone hall dissolving into shadow behind.
The blank line and the poised quill are the subject.
No text, no readable letters, no writing on the pages — only empty ruled lines.
```

---

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

- ~~택배좌 오배송 설정~~ → **해소**. worldview §2.2 — 택배좌는 살아남은 단원이며 베스트 리뷰어
  밑에서 물류를 맡고 있다. 소환사가 다른 세계 데이터에 접근할 수 있었던 경로가 그다.
  "왜 도왔는가"만 3막 문제로 남긴다.
- ~~최종 보스의 이름과 정체~~ → **해소**(ADR-013). worldview §2.1 — 「베스트 리뷰어」.
  체험단원이었고, 신이 사라진 뒤 스스로 리뷰를 올리기 시작했다. 자기 것만 올리고 남의 것은
  덮었으며, 항의한 동료들에게 별 하나짜리를 써서 올렸고 사람들이 눌러 지웠다.
- ~~B01과 최종 보스의 관계~~ → **해소**(ADR-031). worldview §4.2 — B01은 그자가 세운 것이 아니라
  **같은 병의 다른 환자**다. 신이 사라져 아무것도 지워지지 않게 된 세계가 알아서 낳았다.
  둘 다 남은 수단이 「덮음」뿐이라, 그자는 좋아요로 세계를 덮고 B01은 답글로 상세페이지 한 장을 덮는다.

- 2회차 이후 프롤로그 자동 건너뛰기(설정에서 다시 보기)
- 슬라이드 전환 연출: 현재 페이드. 종이 넘김/잉크 번짐 중 택일 검토
- **P13은 결번이다**(구성 개정에서 삭제됨). 이미지 프롬프트·빌드가 슬라이드 번호를 참조하므로
  재부여하지 않는다 — 15장 구성은 P07b 포함으로 성립한다(ADR-034).
- **주인공의 참전 동기는 프롤로그에서 얇게 둔다**(「일단 좀 보고요」). 속죄의 인식은 본편
  아크(전생 죄 ↔ 최종 보스 대비, worldview §2.1·§2.5 주제)로 미룬다 — 프롤로그에서
  각성·결의를 앞당기지 말 것(ADR-034).
