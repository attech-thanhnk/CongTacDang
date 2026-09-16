#!/bin/bash
# =============================================================================
# KỊCH BẢN TRIỂN KHAI CONTAINERIZED MODULAR SERVICES - ĐẢNG BỘ ATTECH
# Căn cứ Hướng dẫn số 03-HD/TVĐU ngày 10/9/2026 của BTV Đảng ủy VATM
# =============================================================================
set -e

echo "=========================================================================="
echo "   TRIỂN KHAI CONTAINERIZED MODULAR SERVICES - CÔNG TY ATTECH (DOCKER)   "
echo "   Mô hình: Core API + Export (1GB RAM) + MinIO S3 + Auth + Nginx        "
echo "=========================================================================="

if ! command -v docker &> /dev/null; then
    echo "[LỖI] Docker chưa được cài đặt. Vui lòng cài Docker trước!"
    exit 1
fi

cd "$(dirname "$0")"

echo "[1/3] Đang khởi động kiến trúc Modular Services..."
docker compose -f docker-compose.modular.yml down --remove-orphans || true
docker compose -f docker-compose.modular.yml build
docker compose -f docker-compose.modular.yml up -d

echo "[2/3] Đang kiểm tra trạng thái các container..."
sleep 6
docker compose -f docker-compose.modular.yml ps

echo "[3/3] Triển khai Containerized Modular Services thành công!"
echo "--------------------------------------------------------------------------"
echo " Cổng vào duy nhất (Nginx Gateway): http://<IP_MAY_CHU>:80"
echo "   ├── Frontend Web:                http://<IP_MAY_CHU>/"
echo "   ├── Core API:                    http://<IP_MAY_CHU>/api/"
echo "   ├── Auth & User API:             http://<IP_MAY_CHU>/api/auth/"
echo "   └── Export Report API:           http://<IP_MAY_CHU>/api/export/"
echo " MinIO Storage Console:             http://<IP_MAY_CHU>:9001 (minio_admin / MinioAttech2026!Key)"
echo "--------------------------------------------------------------------------"
