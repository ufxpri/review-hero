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

# 채택본 — 변형을 여러 장 뽑은 뒤 눈으로 고른 결과를 여기 고정한다.
# 비워두면 해당 슬라이드는 가장 최근 파일을 쓴다.
PICK = {
    # 기존 컷 재활용 — 새 서사에서도 그림이 맞는 것들
    "P01": "P1_12_847건_00002_.png",      # 커튼 친 방, 모니터 앞 뒷모습
    "P07": "P3b_폭발_00002_.png",          # 협탁 위 폭발
    "P10": "P4_오배송_00002_.png",         # 물류 창고
    "P11": "P5_덮어쓰기_00002_.png",        # 겹쳐지는 두 형체
    "P12": "P6_평가_불가_00003_.png",       # 거대한 장부 앞의 작은 사람
    # 아래는 신규 촬영 대상 — 비면 자리표시로 렌더된다
    # P02 쇼핑 / P03 필력 / P04 네임드 / P05 메일 / P06 그 물건
    # P08 만물대장 / P09 심사위원 / P13 자유이자 저주 / P14 선언
}


def parse_slides() -> list[dict]:
    """### / #### P키 — 제목  +  뒤따르는 인용문을 비트 단위로 뽑는다.

    인용문 안의 빈 `>` 줄이 비트 구분이다. 비주얼 노벨처럼 한 슬라이드 안에서
    텍스트가 여러 번 넘어가고, 이미지는 슬라이드 단위로 유지된다.
    """
    slides, cur, beat = [], None, []

    def flush():
        if cur is not None and beat:
            cur["beats"].append(list(beat))
        beat.clear()

    for line in DOC.read_text(encoding="utf-8").splitlines():
        m = re.match(r"^#{3,4}\s+(P\d+[a-z]?)\s*—\s*(.+?)\s*$", line)
        if m:
            flush()
            cur = {"key": m.group(1), "title": m.group(2), "beats": []}
            slides.append(cur)
            continue
        if line.startswith("## "):          # 절이 바뀌면 수집 중단
            flush(); cur = None
            continue
        if cur is None:
            continue
        if line.startswith(">"):
            txt = line.lstrip(">").strip()
            if txt:
                beat.append(md(txt))
            else:
                flush()
        elif line.strip() == "":
            continue
        else:                                # 인용문이 끝나면 그 슬라이드 수집 종료
            flush()
    flush()
    return [s for s in slides if s["beats"]]


def md(t: str) -> str:
    """최소 마크다운 — **강조**만 변환한다."""
    return re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", t)


def pick_art(slides: list[dict]) -> None:
    """ComfyUI 출력에서 슬라이드별 최신 이미지를 골라 ui/assets/ 로 복사."""
    dst_dir = UI / "assets"
    dst_dir.mkdir(parents=True, exist_ok=True)
    for s in slides:
        chosen = None
        if PICK.get(s["key"]) and (ART_SRC / PICK[s["key"]]).exists():
            chosen = ART_SRC / PICK[s["key"]]
        else:
            hits = sorted(ART_SRC.glob(f"{s['key']}_*.png"), key=lambda p: p.stat().st_mtime)
            chosen = hits[-1] if hits else None
        if chosen:
            dst = f"pro-{s['key'].lower()}.png"
            shutil.copy2(chosen, dst_dir / dst)
            s["img"] = f"assets/{dst}"
            print(f"  {s['key']:4s} ← {chosen.name}")
        else:
            s["img"] = None
            print(f"  {s['key']}  — 생성 대기 (자리표시로 렌더)")


def main():
    slides = parse_slides()
    if not slides:
        raise SystemExit(f"슬라이드를 찾지 못했다: {DOC}")
    print(f"슬라이드 {len(slides)}장")
    pick_art(slides)

    payload = [{"title": s["title"], "beats": s["beats"], "img": s["img"]} for s in slides]
    html = (UI / "prologue.template.html").read_text(encoding="utf-8")
    html = html.replace("/*{{SLIDES}}*/[]", json.dumps(payload, ensure_ascii=False, indent=1))

    out = UI / "prologue.html"
    out.write_text(html, encoding="utf-8")
    beats = sum(len(s["beats"]) for s in slides)
    lines = sum(len(b) for s in slides for b in s["beats"])
    print(f"\n→ {out.relative_to(REPO)}  ({len(html):,}바이트)  비트 {beats}개 · 본문 {lines}줄")


if __name__ == "__main__":
    main()
