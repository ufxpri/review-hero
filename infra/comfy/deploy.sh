#!/bin/bash
# 원격 GPU 서버에 ComfyUI 컨테이너를 배포한다.
#
#   bash infra/comfy/deploy.sh            # 전송 + 빌드 + 기동
#   bash infra/comfy/deploy.sh models     # 모델까지 내려받기(22GB, 백그라운드)
#   bash infra/comfy/deploy.sh tunnel     # 로컬 8189 → 원격 8188 터널
#   bash infra/comfy/deploy.sh status     # 상태 확인
set -euo pipefail

HOST="${COMFY_HOST:-A6000-1-002}"     # ~/.ssh/config 의 별칭
REMOTE_DIR="review-hero-comfy"
LOCAL_PORT="${COMFY_PORT:-8189}"
HERE="$(cd "$(dirname "$0")" && pwd)"

# grep -v 는 전부 걸러내면 종료 코드 1을 낸다. set -e + pipefail 과 함께 쓰면
# 정상 동작인데도 스크립트가 죽으므로 || true 로 흡수한다.
ssh_q() {
  ssh -o BatchMode=yes "$HOST" "$@" 2>&1 \
    | { grep -v "post-quantum\|store now\|openssh.com\|^\*\*" || true; }
}

case "${1:-deploy}" in
  deploy)
    echo "▸ 전송 → $HOST:~/$REMOTE_DIR"
    ssh_q "mkdir -p ~/$REMOTE_DIR"
    scp -q "$HERE/Dockerfile" "$HERE/docker-compose.yml" "$HERE/fetch-models.sh" \
        "$HOST:~/$REMOTE_DIR/"
    echo "▸ 빌드 (첫 회 5~10분)"
    ssh_q "cd ~/$REMOTE_DIR && docker compose build 2>&1 | tail -3"
    echo "▸ 기동"
    ssh_q "cd ~/$REMOTE_DIR && docker compose up -d && docker ps --filter name=review-hero-comfy --format '{{.Names}}  {{.Status}}'"
    ;;
  models)
    echo "▸ 모델 22GB 내려받기 (백그라운드)"
    ssh_q "cd ~/$REMOTE_DIR && chmod +x fetch-models.sh && nohup bash fetch-models.sh > fetch.log 2>&1 & echo 시작"
    ;;
  tunnel)
    echo "▸ 터널 localhost:$LOCAL_PORT → $HOST:8188  (Ctrl+C 로 종료)"
    exec ssh -N -L "$LOCAL_PORT:127.0.0.1:8188" "$HOST"
    ;;
  status)
    ssh_q "cd ~/$REMOTE_DIR 2>/dev/null && echo '── 컨테이너' && docker ps -a --filter name=review-hero-comfy --format '{{.Status}}' && echo '── 모델' && du -sh models/* 2>/dev/null && echo '── 다운로드' && tail -3 fetch.log 2>/dev/null"
    ssh_q "nvidia-smi --query-gpu=memory.used,memory.total --format=csv,noheader"
    ;;
  logs)
    ssh_q "cd ~/$REMOTE_DIR && docker compose logs --tail=40 comfy"
    ;;
  *)
    echo "사용법: deploy.sh [deploy|models|tunnel|status|logs]"; exit 1;;
esac
