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
    # 채택 사유는 각 줄 주석에. 변형을 3장씩 뽑아 눈으로 고른 결과다.
    "P01":  "P1_12_847건_00002_.png",          # 기존 컷 재활용 — 현대 원룸·후드티·모니터
    "P02":  "P02_쇼핑_00002_.png",              # 침대까지 상자에 잠식된 안
    "P03":  "P03_필력_00002_.png",              # 코드가 아니라 문서로 읽히는 안
    "P04":  "P04_네임드_00003_.png",             # 나무 탁자에 폰이 흩어진 안 (00004·5는 제품 목업 같다)
    "P05":  "P05_메일_00003_.png",              # 셋 중 유일하게 실제 메일 화면
    "P06":  "P06_그_물건_00002_.png",            # 무인쇄 골판지 한가운데 보조배터리
    "P07":  "P07_마지막_문장_00002_.png",         # 협탁 위 충전 중 + 노트북
    "P07b": "P07b_폭발__00004_.png",            # ⚡ 불기둥이 솟고 방이 밝아진다
    "P08":  "P08_소환_00001_.png",
    "P09":  "P09_이_세계의_규칙_00003_.png",      # 별이 새겨진 나무 패가 줄에 매달린 안
    "P10":  "P10_베스트_리뷰어_00004_.png",        # 빈 고리 줄에 별 하나만 남았다
    "P11":  "P11_그래서_당신_00004_.png",          # 표시 없는 맨손 (00003은 문신투성이)
    "P12":  "P12_명명_00001_.png",                # 별이 없는 빈 나무 패를 건넨다
    "P13":  "P13_자유이자_저주_00004_.png",       # 그림자 대비가 가장 읽히는 안
    "P14":  "P14_선언_00001_.png",
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
            title = m.group(2)
            impact = title.endswith("⚡")          # 전환 순간에 섬광·흔들림
            cur = {"key": m.group(1), "title": title.rstrip(" ⚡"),
                   "beats": [], "impact": impact}
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

    payload = [{"title": s["title"], "beats": s["beats"], "img": s["img"],
                "impact": s.get("impact", False)} for s in slides]
    html = (UI / "prologue.template.html").read_text(encoding="utf-8")
    html = html.replace("/*{{SLIDES}}*/[]", json.dumps(payload, ensure_ascii=False, indent=1))

    out = UI / "prologue.html"
    out.write_text(html, encoding="utf-8")
    beats = sum(len(s["beats"]) for s in slides)
    lines = sum(len(b) for s in slides for b in s["beats"])
    print(f"\n→ {out.relative_to(REPO)}  ({len(html):,}바이트)  비트 {beats}개 · 본문 {lines}줄")


if __name__ == "__main__":
    main()
