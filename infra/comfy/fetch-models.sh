#!/bin/bash
# 모델 내려받기 — 컨테이너 밖(호스트)에서 실행한다. 볼륨으로 마운트되므로 재빌드해도 남는다.
# 로컬 맥과 같은 구성을 맞춘다 (총 22GB).
#
#   원격에서:  bash ~/review-hero-comfy/fetch-models.sh
set -u
C="$(cd "$(dirname "$0")" && pwd)/models"
mkdir -p "$C"/{checkpoints,vae,controlnet,ipadapter,clip_vision}
HF=https://huggingface.co

get() {  # dir file url
  local out="$C/$1/$2"
  if [ -s "$out" ]; then echo "SKIP $2"; return; fi
  echo "GET  $2"
  curl -L --fail --retry 3 --retry-delay 5 -C - -sS -o "$out.part" "$3" \
    && mv "$out.part" "$out" && echo "DONE $2 ($(du -h "$out" | cut -f1))" \
    || echo "FAIL $2"
}

get checkpoints juggernautXL_v9.safetensors             $HF/RunDiffusion/Juggernaut-XL-v9/resolve/main/Juggernaut-XL_v9_RunDiffusionPhoto_v2.safetensors
get checkpoints dreamshaperXL_turbo_v2_1.safetensors    $HF/Lykon/dreamshaper-xl-v2-turbo/resolve/main/DreamShaperXL_Turbo_v2_1.safetensors
get vae         sdxl_vae.safetensors                    $HF/madebyollin/sdxl-vae-fp16-fix/resolve/main/sdxl.vae.safetensors
get controlnet  controlnet_scribble_sdxl.safetensors    $HF/xinsir/controlnet-scribble-sdxl-1.0/resolve/main/diffusion_pytorch_model.safetensors
get controlnet  controlnet_union_sdxl_promax.safetensors $HF/xinsir/controlnet-union-sdxl-1.0/resolve/main/diffusion_pytorch_model_promax.safetensors
get ipadapter   ip-adapter_sdxl_vit-h.safetensors       $HF/h94/IP-Adapter/resolve/main/sdxl_models/ip-adapter_sdxl_vit-h.safetensors
get ipadapter   ip-adapter-plus_sdxl_vit-h.safetensors  $HF/h94/IP-Adapter/resolve/main/sdxl_models/ip-adapter-plus_sdxl_vit-h.safetensors
get clip_vision CLIP-ViT-H-14.safetensors               $HF/h94/IP-Adapter/resolve/main/models/image_encoder/model.safetensors

echo "MODELS_DONE"
du -sh "$C"
