#!/usr/bin/env python3
"""
ControlNet용 화면 레이아웃 와이어프레임 생성기.

design/ui-mockup-prompts.md 의 LAYOUT 블록을 기계가 읽을 수 있는 도형으로 옮긴 것.
출력물을 ControlNet(scribble/lineart)에 물리면 그림은 모델이 채우되
박스 위치는 정확히 지켜진다 — 같은 배치로 화풍만 A/B 비교가 가능해진다.

출력: 흰 배경 + 검은 선 (scribble 표준). SDXL 네이티브 해상도.

실행:
    ~/ComfyUI/venv/bin/python tools/wireframe.py
    ~/ComfyUI/venv/bin/python tools/wireframe.py --only s20a --out /tmp/wf
"""
from __future__ import annotations

import argparse
import math
from pathlib import Path

from PIL import Image, ImageDraw

# SDXL 네이티브 해상도 — 이 밖의 크기는 구도가 뭉개진다
LANDSCAPE = (1344, 768)
PORTRAIT = (768, 1344)
SQUARE = (1024, 1024)

STROKE = 5          # 기본 선 두께. scribble ControlNet은 두꺼운 선을 잘 읽는다
STROKE_THIN = 3


class Canvas:
    """와이어프레임 도형 헬퍼. 전부 흰 배경 위 검은 선으로만 그린다."""

    def __init__(self, size: tuple[int, int]):
        self.img = Image.new("RGB", size, "white")
        self.d = ImageDraw.Draw(self.img)
        self.w, self.h = size

    def box(self, x0, y0, x1, y1, width=STROKE, radius=0):
        if radius:
            self.d.rounded_rectangle([x0, y0, x1, y1], radius=radius, outline="black", width=width)
        else:
            self.d.rectangle([x0, y0, x1, y1], outline="black", width=width)

    def circle(self, cx, cy, r, width=STROKE):
        self.d.ellipse([cx - r, cy - r, cx + r, cy + r], outline="black", width=width)

    def line(self, x0, y0, x1, y1, width=STROKE_THIN):
        self.d.line([x0, y0, x1, y1], fill="black", width=width)

    def bar(self, x0, y0, x1, y1, segments=10):
        """분절된 게이지 바 — 체력·신뢰도용."""
        self.box(x0, y0, x1, y1, width=STROKE_THIN)
        step = (x1 - x0) / segments
        for i in range(1, segments):
            x = x0 + step * i
            self.line(x, y0, x, y1, width=2)

    def stars(self, cx, cy, count=5, r=13, gap=34):
        """별점 행 — 5각별."""
        start = cx - gap * (count - 1) / 2
        for i in range(count):
            self._star(start + gap * i, cy, r)

    def _star(self, cx, cy, r):
        pts = []
        for i in range(10):
            ang = math.pi / 2 + i * math.pi / 5
            rad = r if i % 2 == 0 else r * 0.42
            pts.append((cx + rad * math.cos(ang), cy - rad * math.sin(ang)))
        self.d.polygon(pts, outline="black", width=2)

    def placeholder_text(self, x0, y0, x1, rows=3, gap=15, jag=0.72):
        """자리표시 텍스트 획. 마지막 줄을 짧게 해 문단처럼 읽히게 한다."""
        for i in range(rows):
            end = x1 if i < rows - 1 else x0 + (x1 - x0) * jag
            self.line(x0, y0 + gap * i, end, y0 + gap * i, width=3)

    def card(self, cx, cy, w, h, angle=0.0, art_ratio=0.52):
        """카드 1장 — 상단 일러스트 패널 + 하단 자리표시 텍스트. angle°만큼 회전."""
        pad = 40
        layer = Image.new("RGBA", (w + pad * 2, h + pad * 2), (255, 255, 255, 0))
        ld = ImageDraw.Draw(layer)
        x0, y0, x1, y1 = pad, pad, pad + w, pad + h
        ld.rounded_rectangle([x0, y0, x1, y1], radius=12, outline="black", width=STROKE, fill="white")
        art_b = y0 + int(h * art_ratio)
        ld.rectangle([x0 + 12, y0 + 12, x1 - 12, art_b], outline="black", width=STROKE_THIN)
        for i in range(3):                                    # 카드 본문 자리표시
            yy = art_b + 22 + i * 16
            end = x1 - 12 if i < 2 else x0 + 12 + (w - 24) * 0.6
            ld.line([x0 + 12, yy, end, yy], fill="black", width=3)
        ld.ellipse([x1 - 34, y1 - 34, x1 - 12, y1 - 12], outline="black", width=3)  # 필력 표식

        if angle:
            layer = layer.rotate(angle, resample=Image.BICUBIC, expand=False)
        self.img.paste(layer, (int(cx - layer.width / 2), int(cy - layer.height / 2)), layer)

    def fan(self, cx, cy, n=5, w=155, h=225, spread=118, arc=26.0, lift=30):
        """손패 부채꼴. 가운데가 위로 솟는 호."""
        for i in range(n):
            t = (i - (n - 1) / 2) / max(1, (n - 1) / 2)       # -1 .. 1
            self.card(cx + t * spread, cy + abs(t) * lift, w, h, angle=-t * arc)

    def save(self, path: Path):
        path.parent.mkdir(parents=True, exist_ok=True)
        self.img.save(path)
        return path


# ── 화면별 레이아웃 ───────────────────────────────────────────
# 각 함수는 ui-mockup-prompts.md 의 대응 LAYOUT 블록과 1:1로 맞춘다.

def s20a_battle_landscape() -> Canvas:
    """S20-A 전투 — 가로 슬더스형 (본안)."""
    c = Canvas(LANDSCAPE)
    c.box(0, 0, c.w - 1, 52)                                   # 상단 계정 상태바
    for x in (150, 330, 510):
        c.line(x, 12, x, 40, width=2)
    c.box(1150, 10, 1200, 44, width=STROKE_THIN)               # 지도 / 설정
    c.box(1215, 10, 1265, 44, width=STROKE_THIN)

    c.box(150, 190, 360, 545)                                  # 주인공 (좌)
    c.box(830, 120, 1140, 545)                                 # 적 (우)

    c.box(895, 62, 1075, 108, radius=8)                        # 발송 예정 플라크
    c.box(908, 74, 934, 96, width=STROKE_THIN)                 #  └ 택배상자 픽토그램

    c.bar(830, 560, 1140, 584, segments=10)                    # 적 의지
    c.stars(985, 606)                                          # 존재 등급 ★
    c.box(830, 628, 968, 660, radius=6, width=STROKE_THIN)     # 평가 불가 칩 ×2
    c.box(980, 628, 1118, 660, radius=6, width=STROKE_THIN)

    c.box(150, 570, 360, 600, width=STROKE_THIN)               # 주인공 의지
    c.bar(150, 612, 360, 636, segments=10)                     # 신뢰도 게이지

    c.fan(672, 690)                                            # 손패
    c.circle(88, 672, 52)                                      # 필력 오브
    c.box(1150, 640, 1310, 706, radius=16)                     # 영업 마감
    c.box(24, 726, 96, 760, radius=6, width=STROKE_THIN)       # 뽑을 카드
    c.box(1248, 726, 1320, 760, radius=6, width=STROKE_THIN)   # 버린 카드
    return c


def s20b_battle_portrait() -> Canvas:
    """S20-B 전투 — 세로 모바일 (기존 확정안, 비교용)."""
    c = Canvas(PORTRAIT)
    c.box(0, 0, c.w - 1, 62)                                   # 상태바
    for x in (200, 400, 580):
        c.line(x, 16, x, 46, width=2)

    c.box(40, 86, c.w - 40, 640)                               # 상품 히어로 패널
    c.box(c.w - 260, 106, c.w - 62, 152, radius=8)             #  └ 발송 예정 (우상단)

    c.stars(c.w // 2, 682)                                     # 존재 등급
    c.bar(40, 706, c.w - 40, 734, segments=10)                 # 의지
    c.box(40, 750, 350, 786, radius=6, width=STROKE_THIN)      # 평가 불가 칩 ×2
    c.box(370, 750, 680, 786, radius=6, width=STROKE_THIN)
    c.box(40, 802, c.w - 40, 862, width=STROKE_THIN)           # 구성품 리스트 1행
    c.bar(500, 820, 690, 844, segments=6)

    c.box(0, 900, c.w - 1, c.h - 1)                            # 하단 고정 시트
    c.bar(40, 926, c.w - 40, 950, segments=10)                 # 신뢰도
    for i in range(5):                                         # 손패 (부채꼴 아님 — 세로 나열)
        c.card(88 + i * 148, 1090, 132, 200)
    c.circle(84, 1268, 46)                                     # 필력
    c.box(430, 1236, 700, 1300, radius=16)                     # 영업 마감
    return c


def s20c_boss_landscape() -> Canvas:
    """S20-C 보스전 — 가로. 우측에 타 유저 보스 리뷰 슬롯."""
    c = Canvas(LANDSCAPE)
    c.box(0, 0, c.w - 1, 52)

    c.box(120, 300, 290, 545)                                  # 주인공 — 작게, 압도당하게
    c.box(690, 88, 1010, 545)                                  # 보스 — 크게

    c.box(760, 20, 940, 62, radius=8)                          # 발송 예정 ×2 (스택)
    c.box(760, 70, 940, 112, radius=8, width=STROKE_THIN)

    c.bar(660, 560, 1040, 588, segments=14)                    # 보스 의지 — 더 길게
    c.stars(850, 612)

    for i in range(3):                                         # 보스 리뷰 슬롯 (우측 세로)
        y = 150 + i * 116
        c.box(1080, y, 1320, y + 96, radius=8, width=STROKE_THIN)
        c.placeholder_text(1096, y + 24, 1304, rows=3)

    c.fan(600, 690)
    c.circle(88, 672, 52)
    c.box(1150, 640, 1310, 706, radius=16)
    return c


def s11_map() -> Canvas:
    """S11 맵 — 배송 경로. 아래에서 위로 6단."""
    c = Canvas(LANDSCAPE)
    c.box(0, 0, c.w - 1, 52)
    c.box(24, 74, 250, 620, width=STROKE_THIN)                 # 좌측 캐릭터 패널
    c.box(48, 98, 226, 276, width=STROKE_THIN)
    c.placeholder_text(48, 310, 226, rows=5, gap=26)

    tiers = [3, 4, 3, 4, 2, 1]                                 # 6단, 위로 갈수록 수렴
    cx0, cx1 = 360, 1260
    positions = []
    for ti, n in enumerate(tiers):
        y = 660 - ti * 106
        row = []
        for i in range(n):
            x = cx0 + (cx1 - cx0) * ((i + 0.5) / n)
            r = 46 if ti == len(tiers) - 1 else 30             # 최상단 = 보스, 크게
            c.circle(x, y, r)
            c.box(x - r * 0.42, y - r * 0.42, x + r * 0.42, y + r * 0.42, width=STROKE_THIN)
            row.append((x, y))
        positions.append(row)

    for lower, upper in zip(positions, positions[1:]):         # 분기 연결선
        for i, (x0, y0) in enumerate(lower):
            for j, (x1, y1) in enumerate(upper):
                if abs(i / max(1, len(lower) - 1) - j / max(1, len(upper) - 1)) < 0.5:
                    c.line(x0, y0 - 30, x1, y1 + 30, width=2)
    return c


def s30_card_reward() -> Canvas:
    """S30 카드 보상 — 3택1. 가운데가 선택 상태."""
    c = Canvas(LANDSCAPE)
    c.box(430, 92, 914, 152, radius=10)                        # 배너 플레이트
    c.placeholder_text(470, 112, 874, rows=2, gap=20)
    for i, dx in enumerate((-330, 0, 330)):
        lift = 34 if i == 1 else 0                             # 가운데만 들림
        c.card(672 + dx, 400 - lift, 232, 330)
    c.box(556, 640, 788, 704, radius=16)                       # 확인 버튼
    return c


def s13_deck_viewer() -> Canvas:
    """S13 덱 뷰어 — 3×4 그리드."""
    c = Canvas(LANDSCAPE)
    for i in range(4):                                         # 상단 탭
        c.box(60 + i * 200, 34, 240 + i * 200, 86, radius=8, width=STROKE_THIN)
    for r in range(3):
        for col in range(4):
            c.card(220 + col * 290, 216 + r * 200, 168, 168, art_ratio=0.62)
    c.box(1300, 120, 1318, 700, width=STROKE_THIN)             # 스크롤 인디케이터
    c.box(1160, 700, 1310, 752, radius=14, width=STROKE_THIN)  # 닫기
    return c


def s31_shop() -> Canvas:
    """S31 상점 — 진열대. 좌측 상인, 우측 3단 선반."""
    c = Canvas(LANDSCAPE)
    c.box(80, 150, 380, 560)                                   # 상인
    c.box(60, 560, 400, 640, width=STROKE_THIN)                # 카운터

    c.box(450, 90, 1300, 620)                                  # 선반 프레임
    for y in (270, 450):
        c.line(450, y, 1300, y, width=STROKE)
    for i in range(3):                                         # 1단: 카드 3
        c.card(600 + i * 230, 180, 150, 148, art_ratio=0.6)
    for i in range(2):                                         # 2단: 카드 2
        c.card(700 + i * 230, 360, 150, 148, art_ratio=0.6)
    for i in range(3):                                         # 3단: 아이템 3 + 가격표
        x = 600 + i * 230
        c.box(x - 56, 480, x + 56, 566, width=STROKE_THIN)
        c.box(x - 44, 578, x + 44, 606, radius=12, width=2)
        c.placeholder_text(x - 32, 592, x + 32, rows=1)

    c.circle(96, 700, 34)                                      # 골드
    c.box(1150, 668, 1310, 730, radius=16)                     # 나가기
    return c


def s01_title() -> Canvas:
    """S01 타이틀 — 로고 자리는 비워둔다."""
    c = Canvas(LANDSCAPE)
    c.box(400, 96, 944, 240, width=STROKE)                     # 로고 플레이트 (내부 비움)
    for i in range(4):                                         # 좌하단 메뉴 4
        c.box(120, 400 + i * 78, 470, 462 + i * 78, radius=14, width=STROKE_THIN)
        c.placeholder_text(150, 424 + i * 78, 400, rows=1)
    c.box(1230, 730, 1320, 756, width=2)                       # 버전 표식
    return c


def s23_defeat() -> Canvas:
    """S23 회원 탈퇴 = 패배."""
    c = Canvas(LANDSCAPE)
    c.box(430, 74, 914, 140, radius=10)                        # 상단 플레이트
    c.box(560, 250, 790, 470)                                  # 빈 장바구니
    c.circle(608, 500, 26)
    c.circle(744, 500, 26)
    c.placeholder_text(552, 560, 792, rows=3, gap=26)          # 통계 3행
    c.box(430, 672, 650, 736, radius=16)                       # 버튼 ×2
    c.box(694, 672, 914, 736, radius=16)
    return c


SCREENS = {
    "s20a": ("S20-A 전투 가로(슬더스형)", s20a_battle_landscape),
    "s20b": ("S20-B 전투 세로(모바일)", s20b_battle_portrait),
    "s20c": ("S20-C 보스전 가로", s20c_boss_landscape),
    "s11":  ("S11 맵", s11_map),
    "s30":  ("S30 카드 보상", s30_card_reward),
    "s13":  ("S13 덱 뷰어", s13_deck_viewer),
    "s31":  ("S31 상점", s31_shop),
    "s01":  ("S01 타이틀", s01_title),
    "s23":  ("S23 패배", s23_defeat),
}


def main():
    ap = argparse.ArgumentParser(description="ControlNet용 레이아웃 와이어프레임 생성")
    ap.add_argument("--out", default="assets/wireframe", help="출력 디렉터리")
    ap.add_argument("--only", nargs="*", choices=sorted(SCREENS), help="일부만 생성")
    args = ap.parse_args()

    out = Path(args.out)
    keys = args.only or list(SCREENS)
    for key in keys:
        label, fn = SCREENS[key]
        c = fn()
        p = c.save(out / f"{key}.png")
        print(f"  {key:5s} {label:24s} {c.w}x{c.h}  → {p}")
    print(f"\n{len(keys)}장 생성. ControlNet scribble/lineart 입력으로 사용한다.")


if __name__ == "__main__":
    main()
