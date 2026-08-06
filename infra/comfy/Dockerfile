# ComfyUI on CUDA — 원격 GPU 서버(A6000)용 이미지
#
# 공유 서버라 호스트를 오염시키지 않는 것이 목적이다. 파이썬·CUDA·커스텀 노드가 전부
# 컨테이너 안에 있고, 호스트에는 모델과 출력물만 볼륨으로 남는다.
FROM pytorch/pytorch:2.5.1-cuda12.4-cudnn9-runtime

ENV DEBIAN_FRONTEND=noninteractive \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1

# libgl1·libglib2.0-0 은 opencv(controlnet_aux 전처리)가 요구한다
RUN apt-get update && apt-get install -y --no-install-recommends \
        git libgl1 libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /opt
RUN git clone --depth 1 https://github.com/comfyanonymous/ComfyUI.git

WORKDIR /opt/ComfyUI
RUN pip install -r requirements.txt

# 커스텀 노드 — 로컬(맥) 환경과 동일하게 맞춘다
RUN cd custom_nodes \
    && git clone --depth 1 https://github.com/Comfy-Org/ComfyUI-Manager \
    && git clone --depth 1 https://github.com/cubiq/ComfyUI_IPAdapter_plus \
    && git clone --depth 1 https://github.com/Fannovel16/comfyui_controlnet_aux \
    && pip install opencv-python-headless

EXPOSE 8188
# --listen 0.0.0.0 은 컨테이너 내부 기준이다. 외부 노출은 compose에서
# 127.0.0.1 바인딩으로 막고, 접근은 SSH 터널로만 한다.
CMD ["python", "main.py", "--listen", "0.0.0.0", "--port", "8188"]
