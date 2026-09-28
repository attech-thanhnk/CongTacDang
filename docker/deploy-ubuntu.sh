#!/bin/bash
# =============================================================================
# KỊCH BẢN TRIỂN KHAI PHẦN MỀM CÔNG TÁC ĐẢNG ATTECH TRÊN UBUNTU LINUX (DOCKER)
# Căn cứ Hướng dẫn số 03-HD/TVĐU ngày 10/9/2026 của BTV Đảng ủy VATM
# =============================================================================
set -e

echo "=================================================================="
echo "   TRIỂN KHAI HỆ THỐNG CÔNG TÁC ĐẢNG - CÔNG TY ATTECH (DOCKER)   "
echo "=================================================================="

# 1. Kiểm tra Docker và Docker Compose
if ! command -v docker &> /dev/null; then
    echo "[LỖI] Docker chưa được cài đặt. Vui lòng cài Docker trước!"
    echo "Hướng dẫn cài nhanh trên Ubuntu: sudo apt update && sudo apt install -y docker.io docker-compose-v2"
    exit 1
fi

cd "$(dirname "$0")"

if [ ! -f .env ]; then
    echo "[LỖI] Chưa có docker/.env. Hãy sao chép docker/.env.example thành docker/.env và đặt secret thật!"
    exit 1
fi

echo "[1/3] Đang xây dựng và khởi động các container..."
docker compose --env-file .env down --remove-orphans || true
docker compose --env-file .env build --no-cache
docker compose --env-file .env up -d

echo "[2/3] Đang kiểm tra trạng thái sức khỏe container..."
sleep 6
docker compose ps

echo "[3/3] Triển khai thành công!"
echo "------------------------------------------------------------------"
echo " Giao diện Web (Next.js):     http://<IP_MAY_CHU>:3001"
echo " Backend chỉ truy cập nội bộ qua mạng Docker (healthcheck /healthz)"
echo " Công cụ MinIO/pgAdmin:       docker compose --profile tools up -d"
echo " PostgreSQL chỉ bind localhost:5434 (cấu hình trong docker/.env)"
echo "------------------------------------------------------------------"
