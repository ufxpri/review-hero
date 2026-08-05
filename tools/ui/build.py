#!/usr/bin/env python3
"""
전투 화면 빌드 — 템플릿 + 정본 YAML + 생성 에셋 → 단일 HTML.

카드 텍스트·수치를 HTML에 복사해두지 않는다. design/*.yaml 이 정본이고
빌드가 그것을 주입한다 (tools/comfy/generate.py 가 프롬프트를 문서에서 읽는 것과 같은 규칙).

실행:
    ~/ComfyUI/venv/bin/python tools/ui/build.py
    open ui/combat.html
"""
from __future__ import annotations

import json
import shutil
from pathlib import Path

import yaml

REPO = Path(__file__).resolve().parents[2]
DESIGN = REPO / "design"
UI = REPO / "ui"
ART_SRC = Path.home() / "ComfyUI" / "output" / "review-hero"

# 시연용 상대 — 오크 중량 전사. 약점 #무게 / 무효 #이펙트 #개연성
ENEMY_ID = "E02"

# 판정 4단계를 한 손패에서 전부 보여주도록 고른 시연 손패
DEMO_HAND = [
    "W01",   # 손목 나감      origin E02 · #무게    → 원산지
    "A03",   # 무겁기만 해요   origin E05 · #무게    → 팩트
    "Q01",   # 손잡이 3주      origin E01 · #마감    → 일반
    "Z11",   # 삐걱거려요      전생     · #내구도   → 일반
    "L03",   # 이펙트만 화려함 origin E03 · #이펙트  → 헛소리
]

# 생성 에셋 → UI 경로. 파일이 없으면 화면에 자리표시가 뜬다.
ART = {
    "enemy":  ("_C02_오크", "enemy-orc.png"),
    "hero":   ("C09_플레이어_뒷모습", "hero-back.png"),   # 포켓몬 구도 — 좌하단 뒷모습
    "player": ("C07_플레이어_아바타", "player.png"),      # 시네마틱용 (뒷모습 없을 때 대체)
    "scene":  ("B05_맵_배경", "scene.png"),
}


def load(name: str) -> dict:
    return yaml.safe_load((DESIGN / name).read_text(encoding="utf-8"))


def all_cards(cards: dict) -> dict[str, dict]:
    out = {}
    for group in ("past_life", "enemy_reviews", "equipment_reviews", "specials"):
        for c in cards.get(group) or []:
            out[c["id"]] = c
    return out


def pick_art() -> dict[str, str]:
    """ComfyUI 출력에서 가장 최근 파일을 골라 ui/assets/ 로 복사."""
    dst_dir = UI / "assets"
    dst_dir.mkdir(parents=True, exist_ok=True)
    found = {}
    for key, (needle, dst) in ART.items():
        hits = sorted(ART_SRC.glob(f"*{needle}*.png"), key=lambda p: p.stat().st_mtime)
        if hits:
            shutil.copy2(hits[-1], dst_dir / dst)
            found[key] = f"assets/{dst}"
            print(f"  {key:7s} ← {hits[-1].name}")
        else:
            found[key] = None
            print(f"  {key:7s} — 생성 대기 (자리표시로 렌더)")
    return found


def main():
    cards = all_cards(load("cards-v2.0.yaml"))
    edata = load("enemies-v1.0.yaml")
    pool = [*edata["enemies"], *edata["bosses"]]
    enemy = next(e for e in pool if e["id"] == ENEMY_ID)
    names = {e["id"]: e["name"] for e in pool}

    print("에셋")
    art = pick_art()

    hand = []
    for cid in DEMO_HAND:
        c = dict(cards[cid])
        c["stars"] = c.get("stars", 1)
        hand.append(c)

    # 인텐트 = pattern 첫 행동
    act = next(a for a in enemy["actions"] if a["id"] == enemy["pattern"][0])
    dmg = next((e.get("value") for e in act.get("effects", []) if e.get("op") == "damage"), None)

    data = {
        "enemy": {
            "id": enemy["id"], "name": enemy["name"],
            "will": int(enemy["will"] * 0.8), "maxWill": enemy["will"],
            "weakness_tags": enemy.get("weakness_tags", []),
            "null_tags": enemy.get("null_tags", []),
            "equipment": enemy.get("equipment", []),
        },
        "enemyNames": names,
        "intent": {"name": act["name"], "value": dmg},
        "hand": hand,
        "trust": 4,
        "deckSize": 12,
        "drawCount": 7,
        "art": art,
    }

    html = (UI / "combat.template.html").read_text(encoding="utf-8")
    html = html.replace("/*{{DATA}}*/{}", json.dumps(data, ensure_ascii=False, indent=1))
    if art["scene"]:
        html = html.replace("var(--scene-bg)", f"url('{art['scene']}')")
    else:
        html = html.replace("var(--scene-bg)", "linear-gradient(#1b1712,#0a0806)")

    out = UI / "combat.html"
    out.write_text(html, encoding="utf-8")
    print(f"\n→ {out.relative_to(REPO)}  ({len(html):,}바이트)")
    print(f"   상대 {enemy['name']} · 손패 {len(hand)}장 · 카드 정본 {len(cards)}장")


if __name__ == "__main__":
    main()
