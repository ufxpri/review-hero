#!/usr/bin/env python3
"""
ComfyUI 배치 생성 드라이버.

프롬프트 정본은 design/*.md 이고 이 스크립트가 그것을 읽는다 — 프롬프트를 코드에
복사해두지 않는다. 문서를 고치면 생성 결과가 따라 바뀌는 단일 정본 구조.

두 가지 모드:
  txt2img   — 캐릭터·아이템 에셋 (image-prompts-ready.md)
  controlnet — 화면 목업. 와이어프레임으로 배치를 강제 (ui-mockup-prompts.md)

사용:
    # ComfyUI 서버를 먼저 띄운다
    ~/ComfyUI/venv/bin/python ~/ComfyUI/main.py

    # 어떤 프롬프트가 있는지
    python tools/comfy/generate.py --list

    # 화면 목업 — 와이어프레임으로 배치 강제
    python tools/comfy/generate.py --match "목업 A" --wireframe assets/wireframe/s20a.png

    # 에셋 — 오크 앵커 4장
    python tools/comfy/generate.py --match "C02 오크" --batch 4

MEMO — M1 Pro 16GB 실측 (2026-08-05)
    1344x768 배치2 + ControlNet : 295초/장. 로그에 "Unloaded partially" 반복 = 메모리 스래싱
    1152x640 배치1 + ControlNet : 137초/장. 구도·화질 차이 없음
  통합 메모리 16GB에서 SDXL UNet(~5GB) + ControlNet(~2.3GB)을 동시에 물면 배치를 키우거나
  해상도를 올리는 순간 스왑이 걸린다. 배치를 늘리지 말고 여러 번 호출하는 편이 빠르다.
  최종 에셋은 낮은 해상도로 구도를 확정한 뒤 업스케일하는 것이 총 시간에서 유리하다.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
# 기본은 로컬 맥. 원격 A6000은 SSH 터널을 열고 --server 로 가리킨다.
#   bash infra/comfy/deploy.sh tunnel        (localhost:8189 → 원격 8188)
#   generate.py --server http://127.0.0.1:8189 ...
# 또는 환경변수 COMFY_SERVER 로 고정한다.
SERVER = os.environ.get("COMFY_SERVER", "http://127.0.0.1:8188")
REMOTE = False          # --server 를 주면 True. 결과를 로컬로 내려받는다

DOCS = [
    REPO / "design" / "ui-mockup-prompts.md",
    REPO / "design" / "image-prompts-ready.md",
    REPO / "design" / "prologue-v1.md",
]

# 문서 §0/§4의 네거티브를 여기 한 벌로 둔다 (두 문서가 공유하는 값)
NEGATIVE = (
    "text, letters, words, korean characters, chinese characters, watermark, signature, logo, "
    "price tag, label, readable writing, "
    "anime, manga, cel shading, chibi, cute, comedic expression, goofy grin, wacky pose, caricature, "
    "flat vector, clip art, low detail, plastic toy look, "
    "white background, plain studio backdrop, product photography, catalog shot, "
    "busy background, crowd, extra limbs, deformed hands, "
    "gore, blood, dismemberment, lowres, jpeg artifacts, blurry"
)

# 화풍 — 문서 프롬프트의 첫 문단(스타일 서술)을 통째로 교체한다.
# design/*.md 는 "무엇을 그릴지"(주제)의 정본이고, "어떻게 그릴지"(화풍)는 여기서 갈아끼운다.
STYLES = {
    "illust": (
        "Hand-painted digital illustration for a stylized fantasy card game.\n"
        "Bold confident brushwork with visible painterly strokes, clean readable shapes,\n"
        "slightly exaggerated stylized proportions rather than photorealistic anatomy.\n"
        "Warm characterful lighting with clear separation of light and shadow, soft bounce in the shadows.\n"
        "Rich but controlled palette: warm ochre, mossy green, oxblood, ink brown, parchment cream,\n"
        "with one saturated accent. Visible canvas grain and ink texture.\n"
        "Isolated on a simple flat dark backdrop with a soft painted vignette, no environment, no floor.\n"
        "Full figure, centered, with a clear silhouette that reads instantly at small size.\n"
        "Characterful and expressive — the world is played straight, but the brush has personality.\n"
        "Vertical 4:5 composition."
    ),
}
# 일러스트 화풍에서는 공용 네거티브의 사진·만화 금지어가 오히려 방해가 된다
STYLE_NEG_DROP = {
    "illust": ["anime", "manga", "cel shading", "flat vector", "clip art",
               "white background", "plain studio backdrop", "product photography", "catalog shot"],
}


def restyle(prompt: str, style: str) -> str:
    """첫 문단(스타일)만 교체하고 주제 문단은 보존한다."""
    if style not in STYLES:
        return prompt
    parts = prompt.split("\n\n", 1)
    subject = parts[1] if len(parts) == 2 else prompt
    return STYLES[style] + "\n\n" + subject


def strip_neg(neg: str, style: str) -> str:
    drop = STYLE_NEG_DROP.get(style, [])
    return ", ".join(t for t in (x.strip() for x in neg.split(","))
                     if t and t not in drop)


# 체크포인트별 권장 샘플러 설정. Turbo 계열은 스텝·CFG가 완전히 다르다.
CHECKPOINTS = {
    "juggernaut": dict(
        file="juggernautXL_v9.safetensors",
        steps=30, cfg=6.0, sampler="dpmpp_2m", scheduler="karras",
        note="반실사 렌더 — 적 히어로·구성품 에셋용",
    ),
    "dreamshaper": dict(
        file="dreamshaperXL_turbo_v2_1.safetensors",
        steps=8, cfg=2.0, sampler="dpmpp_sde", scheduler="karras",
        note="유화풍 Turbo — UI 목업 반복용 (약 4배 빠름)",
    ),
}


# ── 프롬프트 정본 파싱 ────────────────────────────────────────

def parse_prompts(paths=DOCS) -> list[tuple[str, str, Path]]:
    """마크다운에서 (제목, 프롬프트, 출처) 목록을 뽑는다.

    직전에 나온 ## / ### 제목을 그 다음 코드 블록의 이름으로 삼는다.
    네거티브 프롬프트 블록과 저장 규칙 같은 비프롬프트 블록은 제외한다.
    """
    out: list[tuple[str, str, Path]] = []
    for path in paths:
        if not path.exists():
            continue
        heading = ""
        in_block, buf = False, []
        for line in path.read_text(encoding="utf-8").splitlines():
            if line.startswith("```"):
                if in_block:
                    body = "\n".join(buf).strip()
                    if _is_prompt(body):
                        out.append((heading, body, path))
                    in_block, buf = False, []
                else:
                    in_block = True
                continue
            if in_block:
                buf.append(line)
            elif line.startswith("#"):
                heading = line.lstrip("#").strip()
    return out


def _is_prompt(body: str) -> bool:
    """네거티브 블록·설정 스니펫을 걸러낸다."""
    if not body or len(body) < 60:
        return False
    if body.lstrip().startswith("text, letters"):        # 공용 네거티브 프롬프트
        return False
    if body.lstrip().startswith(("assets/", "~/", "npm ", "node ")):
        return False
    # 카드 전용 추가 네거티브는 문장이 아니라 쉼표 키워드 나열이다.
    # 마침표 없이 쉼표가 8개를 넘으면 프롬프트가 아니라 금지어 목록으로 본다.
    if "." not in body and body.count(",") > 8:
        return False
    return True


# ── 워크플로 그래프 ──────────────────────────────────────────

NEG_EXTRA = ""   # --neg-extra 로 주입. 카드별 금지어를 공용 NEGATIVE 뒤에 덧붙인다


def _base(ckpt: dict, positive: str, seed: int, w: int, h: int, batch: int) -> dict:
    """체크포인트 + 프롬프트 인코딩 + 빈 latent. 두 모드가 공유하는 앞단."""
    return {
        "1": {"class_type": "CheckpointLoaderSimple",
              "inputs": {"ckpt_name": ckpt["file"]}},
        "2": {"class_type": "CLIPTextEncode",
              "inputs": {"text": positive, "clip": ["1", 1]}},
        "3": {"class_type": "CLIPTextEncode",
              "inputs": {"text": (NEGATIVE + ", " + NEG_EXTRA).strip(", "), "clip": ["1", 1]}},
        "4": {"class_type": "EmptyLatentImage",
              "inputs": {"width": w, "height": h, "batch_size": batch}},
    }


def _tail(ckpt: dict, seed: int, pos_node: str, neg_node: str, prefix: str) -> dict:
    """샘플러 + 디코드 + 저장. 두 모드가 공유하는 뒷단."""
    return {
        "10": {"class_type": "KSampler",
               "inputs": {"seed": seed, "steps": ckpt["steps"], "cfg": ckpt["cfg"],
                          "sampler_name": ckpt["sampler"], "scheduler": ckpt["scheduler"],
                          "denoise": 1.0, "model": ["1", 0],
                          "positive": [pos_node, 0], "negative": [neg_node, 0],
                          "latent_image": ["4", 0]}},
        "11": {"class_type": "VAEDecode",
               "inputs": {"samples": ["10", 0], "vae": ["1", 2]}},
        "12": {"class_type": "SaveImage",
               "inputs": {"filename_prefix": prefix, "images": ["11", 0]}},
    }


def workflow_txt2img(ckpt, positive, seed, w, h, batch, prefix) -> dict:
    g = _base(ckpt, positive, seed, w, h, batch)
    g.update(_tail(ckpt, seed, "2", "3", prefix))
    return g


def workflow_controlnet(ckpt, positive, seed, w, h, batch, prefix,
                        wireframe: str, strength: float, end_percent: float) -> dict:
    """와이어프레임으로 배치를 강제한다. 그림은 모델이 채우되 박스 위치는 고정."""
    g = _base(ckpt, positive, seed, w, h, batch)
    g["5"] = {"class_type": "LoadImage", "inputs": {"image": wireframe}}
    g["6"] = {"class_type": "ControlNetLoader",
              "inputs": {"control_net_name": "controlnet_scribble_sdxl.safetensors"}}
    g["7"] = {"class_type": "ControlNetApplyAdvanced",
              "inputs": {"positive": ["2", 0], "negative": ["3", 0],
                         "control_net": ["6", 0], "image": ["5", 0],
                         "strength": strength, "start_percent": 0.0,
                         "end_percent": end_percent}}
    g.update(_tail(ckpt, seed, "7", "7", prefix))
    g["10"]["inputs"]["negative"] = ["7", 1]      # ApplyAdvanced는 pos/neg 둘 다 반환
    return g


# ── 서버 통신 ────────────────────────────────────────────────

def _post(path: str, payload: dict) -> dict:
    req = urllib.request.Request(
        SERVER + path, data=json.dumps(payload).encode(),
        headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.load(r)


def _get(path: str) -> dict:
    with urllib.request.urlopen(SERVER + path, timeout=30) as r:
        return json.load(r)


def server_alive() -> bool:
    try:
        _get("/system_stats")
        return True
    except Exception:
        return False


def upload_image(path: Path) -> str:
    """와이어프레임을 ComfyUI input 디렉터리로 올린다. 반환값은 LoadImage용 파일명."""
    boundary = "----comfyupload"
    body = b"".join([
        f"--{boundary}\r\n".encode(),
        f'Content-Disposition: form-data; name="image"; filename="{path.name}"\r\n'.encode(),
        b"Content-Type: image/png\r\n\r\n",
        path.read_bytes(), b"\r\n",
        f"--{boundary}\r\n".encode(),
        b'Content-Disposition: form-data; name="overwrite"\r\n\r\ntrue\r\n',
        f"--{boundary}--\r\n".encode(),
    ])
    req = urllib.request.Request(
        SERVER + "/upload/image", data=body,
        headers={"Content-Type": f"multipart/form-data; boundary={boundary}"})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.load(r)["name"]


LOCAL_OUT = Path.home() / "ComfyUI" / "output"


def fetch(images: list[dict]) -> None:
    """원격 서버에서 생성한 이미지를 로컬 출력 디렉터리로 내려받는다.

    원격 GPU를 쓰면 결과가 그쪽에 남는다. 빌드 스크립트(tools/ui/*.py)는 로컬
    ~/ComfyUI/output 을 보므로, 여기서 받아와 로컬-원격 구분 없이 같은 흐름이 되게 한다.
    """
    for im in images:
        sub = im.get("subfolder", "")
        dst = LOCAL_OUT / sub / im["filename"]
        dst.parent.mkdir(parents=True, exist_ok=True)
        q = urllib.parse.urlencode({"filename": im["filename"], "subfolder": sub,
                                    "type": im.get("type", "output")})
        with urllib.request.urlopen(f"{SERVER}/view?{q}", timeout=120) as r:
            dst.write_bytes(r.read())


def run(graph: dict, label: str) -> list[str]:
    pid = _post("/prompt", {"prompt": graph})["prompt_id"]
    t0 = time.time()
    while True:
        hist = _get(f"/history/{pid}")
        if pid in hist:
            metas = [im for node in hist[pid]["outputs"].values()
                     for im in node.get("images", [])]
            outs = [im["filename"] for im in metas]
            if REMOTE:
                fetch(metas)
            print(f"  ✓ {label}  {time.time()-t0:.0f}초  →  {', '.join(outs)}")
            return outs
        time.sleep(2)
        if time.time() - t0 > 1800:
            print(f"  ✗ {label} 시간 초과")
            return []


# ── CLI ──────────────────────────────────────────────────────

def main():
    global NEGATIVE, NEG_EXTRA, SERVER, REMOTE
    ap = argparse.ArgumentParser(description="ComfyUI 배치 생성")
    ap.add_argument("--list", action="store_true", help="프롬프트 목록만 출력")
    ap.add_argument("--match", help="제목 부분 일치로 프롬프트 선택")
    ap.add_argument("--wireframe", help="ControlNet 입력 PNG (지정 시 배치 강제 모드)")
    ap.add_argument("--ckpt", choices=sorted(CHECKPOINTS), help="기본: 목업=dreamshaper, 에셋=juggernaut")
    ap.add_argument("--batch", type=int, default=1, help="한 번에 뽑을 장수")
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--size", help="WxH. 기본: 와이어프레임 크기 또는 832x1216")
    ap.add_argument("--cn-strength", type=float, default=0.75, help="ControlNet 강도")
    ap.add_argument("--cn-end", type=float, default=0.65,
                    help="ControlNet 적용 종료 시점. 낮출수록 후반부를 모델이 자유롭게 그린다")
    ap.add_argument("--server", help="ComfyUI 주소. 원격 GPU는 터널 주소(예: http://127.0.0.1:8189)")
    ap.add_argument("--style", choices=sorted(STYLES), help="화풍 교체 (첫 문단만 갈아끼움)")
    ap.add_argument("--neg-extra", default="",
                    help="이 생성에만 덧붙일 네거티브. 카드별 금지어(예: 뒷모습의 face 계열)")
    args = ap.parse_args()
    if args.server:
        SERVER = args.server.rstrip('/')
        REMOTE = True

    prompts = parse_prompts()
    if args.list or not args.match:
        print(f"프롬프트 {len(prompts)}개\n")
        src = None
        for h, body, path in prompts:
            if path != src:
                print(f"\n── {path.name}")
                src = path
            print(f"   {h[:66]:68s} {len(body):>4}자")
        if not args.match:
            print("\n--match 로 제목 일부를 지정해 생성한다.")
        return

    hits = [(h, b) for h, b, _ in prompts if args.match in h]
    if not hits:
        print(f"'{args.match}' 에 맞는 프롬프트 없음. --list 로 확인.", file=sys.stderr)
        sys.exit(1)

    if not server_alive():
        print(f"ComfyUI 서버에 연결할 수 없다: {SERVER}\n"
              "  로컬: ~/ComfyUI/venv/bin/python ~/ComfyUI/main.py\n"
              "  원격: bash infra/comfy/deploy.sh tunnel  (다른 터미널에서)", file=sys.stderr)
        sys.exit(1)

    if args.style: NEGATIVE = strip_neg(NEGATIVE, args.style)
    NEG_EXTRA = args.neg_extra.strip()
    key = args.ckpt or ("dreamshaper" if args.wireframe else "juggernaut")
    ckpt = CHECKPOINTS[key]

    # 해상도 기본값은 M1 Pro 16GB 실측 기준(아래 MEMO 참조)이지 화질 상한이 아니다.
    if args.size:
        w, h = (int(v) for v in args.size.lower().split("x"))
    elif args.wireframe:
        from PIL import Image
        ww, hh = Image.open(args.wireframe).size
        w, h = (1152, 640) if ww > hh else (640, 1152)     # 와이어프레임 비율만 따르고 크기는 낮춘다
    else:
        w, h = 832, 1216                                  # SDXL 세로 네이티브

    wf_name = upload_image(Path(args.wireframe)) if args.wireframe else None

    print(f"체크포인트 {ckpt['file']}  ({ckpt['note']})")
    print(f"설정      {w}x{h}  {ckpt['steps']}스텝  CFG {ckpt['cfg']}  배치 {args.batch}")
    if wf_name:
        print(f"레이아웃  {wf_name}  강도 {args.cn_strength}  종료 {args.cn_end}")
    print()

    for h_title, body in hits:
        prefix = "review-hero/" + re.sub(r"[^\w가-힣]+", "_", h_title)[:48]
        body = restyle(body, args.style) if args.style else body
        if wf_name:
            g = workflow_controlnet(ckpt, body, args.seed, w, h, args.batch, prefix,
                                    wf_name, args.cn_strength, args.cn_end)
        else:
            g = workflow_txt2img(ckpt, body, args.seed, w, h, args.batch, prefix)
        run(g, h_title[:52])

    print(f"\n출력: {LOCAL_OUT}/review-hero/" + ("  (원격에서 수신)" if REMOTE else ""))


if __name__ == "__main__":
    main()
