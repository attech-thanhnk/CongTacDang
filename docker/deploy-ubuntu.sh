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

echo "[1/3] Đang xây dựng và khởi động các container..."
cd "$(dirname "$0")"
docker compose down --remove-orphans || true
docker compose build --no-cache
docker compose up -d

echo "[2/3] Đang kiểm tra trạng thái sức khỏe container..."
sleep 6
docker compose ps

echo "[3/3] Triển khai thành công!"
echo "------------------------------------------------------------------"
echo " Giao diện Web (Next.js):     http://<IP_MAY_CHU>:3001"
echo " Backend Web API (Swagger):   http://<IP_MAY_CHU>:5000/swagger"
echo " MinIO Web Console (Storage): http://<IP_MAY_CHU>:9001 (minioadmin / minioadmin)"
echo " pgAdmin Quản trị CSDL:       http://<IP_MAY_CHU>:5050 (admin@attech.com.vn / AttechAdmin2026!)"
echo " PostgreSQL Cổng nội bộ:      5434 (Host=localhost;Database=congtacdang_db)"
echo "------------------------------------------------------------------"
