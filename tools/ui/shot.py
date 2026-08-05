#!/usr/bin/env python3
"""
UI 스크린샷 — 빌드 결과를 실제 브라우저로 렌더해 눈으로 확인한다.

"빌드 성공 ≠ 동작 확인" 원칙 때문에 존재한다. HTML이 오류 없이 생성되는 것과
화면이 의도대로 보이는 것은 별개다.

playwright가 필요하다. 이 저장소는 파이썬 의존을 두지 않으므로 외부 인터프리터를 받는다:
    PY=~/ncloud-cla-downloader/.venv/bin/python
    $PY tools/ui/shot.py ui/combat.html --out assets/generated/combat.png

콘솔 오류가 있으면 종료 코드 1로 실패시킨다 — 조용한 JS 오류를 놓치지 않기 위함.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

from playwright.sync_api import sync_playwright

REPO = Path(__file__).resolve().parents[2]


def main():
    ap = argparse.ArgumentParser(description="HTML을 렌더해 PNG로 저장")
    ap.add_argument("page", help="HTML 경로 (저장소 기준 상대경로 허용)")
    ap.add_argument("--out", default="assets/generated/shot.png")
    ap.add_argument("--width", type=int, default=1400)
    ap.add_argument("--height", type=int, default=900)
    ap.add_argument("--click", help="렌더 후 클릭할 CSS 선택자 (상호작용 상태 촬영)")
    ap.add_argument("--js", help="렌더 후 실행할 JS. 애니메이션 도중을 잡을 때 쓴다")
    ap.add_argument("--after", type=int, default=0, help="--js 실행 뒤 대기 ms (프레임 선택)")
    ap.add_argument("--full", action="store_true", help="전체 페이지 촬영")
    args = ap.parse_args()

    page_path = (REPO / args.page).resolve()
    if not page_path.exists():
        sys.exit(f"파일 없음: {page_path}")
    out = (REPO / args.out).resolve()
    out.parent.mkdir(parents=True, exist_ok=True)

    errors: list[str] = []
    with sync_playwright() as p:
        # playwright 번들 브라우저 버전이 어긋나면 시스템 Chrome으로 대체한다.
        # (이 저장소는 playwright를 직접 관리하지 않고 외부 인터프리터를 빌려 쓰므로
        #  번들 버전이 맞지 않는 상황이 정상적으로 발생한다)
        try:
            browser = p.chromium.launch()
        except Exception:
            browser = p.chromium.launch(channel="chrome")
        pg = browser.new_page(viewport={"width": args.width, "height": args.height},
                              device_scale_factor=2)
        pg.on("console", lambda m: errors.append(m.text) if m.type == "error" else None)
        pg.on("pageerror", lambda e: errors.append(str(e)))
        pg.goto(page_path.as_uri())
        pg.wait_for_timeout(600)
        if args.click:
            pg.click(args.click)
            pg.wait_for_timeout(350)
        if args.js:
            pg.evaluate(args.js)
            pg.wait_for_timeout(args.after or 300)
        pg.screenshot(path=str(out), full_page=args.full)
        browser.close()

    print(f"→ {out.relative_to(REPO)}")
    if errors:
        print("\n콘솔 오류:")
        for e in errors[:10]:
            print("  " + e)
        sys.exit(1)
    print("콘솔 오류 없음")


if __name__ == "__main__":
    main()
