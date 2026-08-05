#!/usr/bin/env python3
"""
프롤로그 슬라이드쇼 빌드 — design/prologue-v1.md §3 을 읽어 ui/prologue.html 을 만든다.

본문을 HTML에 복사해두지 않는다. 문서가 정본이고 빌드가 읽는다
(generate.py가 프롬프트를, build.py가 카드를 읽는 것과 같은 규칙).

문서 형식 전제:
    ### P1 — 제목
    > 본문 줄
    > 본문 줄
    ```
    (이미지 프롬프트 — 여기서는 무시)
    ```

실행:
    ~/ComfyUI/venv/bin/python tools/ui/build_prologue.py
"""
from __future__ import annotations

import json
import re
import shutil
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
DOC = REPO / "design" / "prologue-v1.md"
UI = REPO / "ui"
ART_SRC = Path.home() / "ComfyUI" / "output" / "review-hero"


def parse_slides() -> list[dict]:
    """### P숫자 — 제목  +  뒤따르는 인용문(>) 을 슬라이드로 뽑는다."""
    slides, cur = [], None
    for line in DOC.read_text(encoding="utf-8").splitlines():
        m = re.match(r"^###\s+(P\d+)\s*—\s*(.+?)\s*$", line)
        if m:
            cur = {"key": m.group(1), "title": m.group(2), "lines": []}
            slides.append(cur)
            continue
        if cur is not None and line.startswith(">"):
            txt = line.lstrip("> ").strip()
            if txt:
                cur["lines"].append(txt)
    return slides


def pick_art(slides: list[dict]) -> None:
    """ComfyUI 출력에서 슬라이드별 최신 이미지를 골라 ui/assets/ 로 복사."""
    dst_dir = UI / "assets"
    dst_dir.mkdir(parents=True, exist_ok=True)
    for s in slides:
        hits = sorted(ART_SRC.glob(f"{s['key']}_*.png"), key=lambda p: p.stat().st_mtime)
        if hits:
            dst = f"pro-{s['key'].lower()}.png"
            shutil.copy2(hits[-1], dst_dir / dst)
            s["img"] = f"assets/{dst}"
            print(f"  {s['key']}  ← {hits[-1].name}")
        else:
            s["img"] = None
            print(f"  {s['key']}  — 생성 대기 (자리표시로 렌더)")


def main():
    slides = parse_slides()
    if not slides:
        raise SystemExit(f"슬라이드를 찾지 못했다: {DOC}")
    print(f"슬라이드 {len(slides)}장")
    pick_art(slides)

    payload = [{"title": s["title"], "lines": s["lines"], "img": s["img"]} for s in slides]
    html = (UI / "prologue.template.html").read_text(encoding="utf-8")
    html = html.replace("/*{{SLIDES}}*/[]", json.dumps(payload, ensure_ascii=False, indent=1))

    out = UI / "prologue.html"
    out.write_text(html, encoding="utf-8")
    total = sum(len(s["lines"]) for s in slides)
    print(f"\n→ {out.relative_to(REPO)}  ({len(html):,}바이트)  본문 {total}줄")


if __name__ == "__main__":
    main()
