# 원격 GPU 이미지 생성 (A6000)

맥(M1 Pro 16GB)에서 SDXL 1장에 **484초**가 걸렸다. 같은 그림이 A6000에서 **7~13초**다.
약 **40~50배**. 프롤로그·에셋이 수십 장 남은 시점에 이 차이는 작업 방식 자체를 바꾼다.

## 구성

| | |
|---|---|
| 호스트 | `A6000-1-002` (192.168.100.4) — `~/.ssh/config` 별칭 |
| GPU | NVIDIA RTX A6000 48GB (공용 — 다른 컨테이너가 상시 사용 중) |
| 방식 | Docker. 호스트에 파이썬·CUDA를 설치하지 않는다 |
| 노출 | `127.0.0.1:8188` 만 바인딩. 접근은 **SSH 터널** |
| 영속 | `~/review-hero-comfy/{models,output,input}` 볼륨 |

## 사용

```bash
bash infra/comfy/deploy.sh            # 전송 · 빌드 · 기동
bash infra/comfy/deploy.sh models     # 모델 22GB (백그라운드)
bash infra/comfy/deploy.sh status     # 컨테이너 · 모델 · GPU 상태
bash infra/comfy/deploy.sh logs

# 다른 터미널에서 터널을 열어두고
bash infra/comfy/deploy.sh tunnel     # localhost:8189 → 원격 8188

# 생성 (결과는 로컬 ~/ComfyUI/output 으로 자동 수신)
python tools/comfy/generate.py --server http://127.0.0.1:8189 --match "P02" ...
```

`COMFY_SERVER=http://127.0.0.1:8189` 를 환경변수로 두면 `--server` 를 매번 안 써도 된다.

## 설계 근거

- **왜 Docker인가**: 공용 장비다. llama.cpp·headscale·prometheus 등이 이미 돌고 있어
  호스트 파이썬을 건드리면 남의 작업을 깰 수 있다. 컨테이너는 지우면 흔적이 없다.
- **왜 포트를 열지 않는가**: ComfyUI에는 인증이 없다. 사내망이라도 열어두지 않고
  SSH 터널로만 접근한다.
- **`--reserve-vram 2.0`**: 다른 작업이 VRAM을 쓰고 있으므로 ComfyUI가 전부 선점하지 않게 한다.
- **결과 자동 수신**: `generate.py` 가 `--server` 를 받으면 `/view` API로 결과를 로컬
  `~/ComfyUI/output` 에 내려받는다. 빌드 스크립트는 로컬 경로만 보므로 원격/로컬 구분 없이 같은 흐름이 된다.

## 로컬(맥) 환경은 남겨둔다

`~/ComfyUI` 로컬 설치는 그대로 유지한다. 원격이 막히거나 사외에서 작업할 때의 대안이고,
`--server` 없이 실행하면 자동으로 로컬을 쓴다.
