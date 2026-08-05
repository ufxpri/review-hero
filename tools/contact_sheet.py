#!/usr/bin/env python3
"""
생성 결과 비교 시트.

같은 조건에서 뽑은 안(案)들을 한 장에 나란히 붙여 눈으로 고르기 위한 도구.
세로/가로처럼 비율이 다른 이미지도 같은 높이로 맞춰 공정하게 비교한다.

사용:
    python tools/contact_sheet.py --out assets/generated/compare.png \
        "A 가로 슬더스형:~/ComfyUI/output/review-hero/목업_A*.png" \
        "B 세로 모바일:~/ComfyUI/output/review-hero/목업_B*.png"
"""
from __future__ import annotations

import argparse
import glob
import os
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

FONT_CANDIDATES = [
    "/System/Library/Fonts/AppleSDGothicNeo.ttc",      # 한글
    "/System/Library/Fonts/Helvetica.ttc",
]
ROW_H = 620          # 각 행의 이미지 높이 (비율이 달라도 이 높이로 통일)
LABEL_H = 52
PAD = 18
BG = (24, 24, 28)
FG = (238, 238, 242)
DIM = (150, 150, 158)


def load_font(size: int):
    for p in FONT_CANDIDATES:
        if os.path.exists(p):
            try:
                return ImageFont.truetype(p, size)
            except Exception:
                continue
    return ImageFont.load_default()


def build(groups: list[tuple[str, list[Path]]], out: Path) -> Path:
    f_title = load_font(30)
    f_note = load_font(19)

    rows = []
    for label, paths in groups:
        imgs = []
        for p in paths:
            im = Image.open(p).convert("RGB")
            w = max(1, int(im.width * ROW_H / im.height))
            imgs.append((im.resize((w, ROW_H), Image.LANCZOS), p.name))
        rows.append((label, imgs))

    width = max(sum(im.width for im, _ in imgs) + PAD * (len(imgs) + 1)
                for _, imgs in rows)
    height = sum(ROW_H + LABEL_H + PAD * 2 for _ in rows) + PAD

    sheet = Image.new("RGB", (width, height), BG)
    d = ImageDraw.Draw(sheet)

    y = PAD
    for label, imgs in rows:
        d.text((PAD, y + 8), label, font=f_title, fill=FG)
        y += LABEL_H
        x = PAD
        for im, name in imgs:
            sheet.paste(im, (x, y))
            d.rectangle([x, y, x + im.width - 1, y + ROW_H - 1], outline=(70, 70, 78), width=1)
            d.text((x + 6, y + ROW_H - 24), f"{im.width}x{ROW_H}", font=f_note, fill=DIM)
            x += im.width + PAD
        y += ROW_H + PAD * 2

    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out)
    return out


def main():
    ap = argparse.ArgumentParser(description="생성 결과 비교 시트")
    ap.add_argument("--out", default="assets/generated/compare.png")
    ap.add_argument("groups", nargs="+", metavar="라벨:글롭",
                    help='예: "A 가로:~/ComfyUI/output/**/목업_A*.png"')
    args = ap.parse_args()

    groups = []
    for spec in args.groups:
        label, _, pattern = spec.partition(":")
        # 공백으로 구분된 여러 글롭을 받는다 (슬라이드처럼 접두가 제각각인 경우)
        paths = []
        for pat in pattern.split():
            paths += [Path(x) for x in glob.glob(os.path.expanduser(pat))]
        paths = sorted(set(paths))
        if not paths:
            print(f"  건너뜀 — 일치 없음: {pattern}")
            continue
        groups.append((label, paths))
        print(f"  {label}: {len(paths)}장")

    if not groups:
        raise SystemExit("비교할 이미지가 없다.")
    p = build(groups, Path(args.out))
    print(f"\n→ {p}  ({Image.open(p).size[0]}x{Image.open(p).size[1]})")


if __name__ == "__main__":
    main()
