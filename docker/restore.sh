#!/usr/bin/env bash
# =============================================================================
# KHÔI PHỤC CSDL POSTGRESQL + THƯ MỤC UPLOAD TỪ MỘT BẢN SAO LƯU — CongTacDang
#
# Cách dùng:
#   docker/restore.sh [--dry-run] [--yes] [--skip-uploads] <thư-mục-bản-sao-lưu>
# Ví dụ:
#   docker/restore.sh /var/backups/congtacdang/congtacdang-20260928-020000
#
# Quy trình: kiểm tra checksum → xác nhận (gõ KHOI PHUC) → dừng frontend/backend
#   → pg_restore --clean --if-exists vào CSDL hiện tại → thay nội dung volume upload
#   → khởi động lại backend/frontend.
# CẢNH BÁO: ghi đè toàn bộ dữ liệu hiện tại. Nên chạy backup.sh trước khi khôi phục.
# Biến môi trường: ENV_FILE, COMPOSE_FILE, UPLOAD_VOLUME, HELPER_IMAGE, APP_UID.
# =============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="${ENV_FILE:-$SCRIPT_DIR/.env}"
COMPOSE_FILE="${COMPOSE_FILE:-$SCRIPT_DIR/docker-compose.yml}"
UPLOAD_VOLUME="${UPLOAD_VOLUME:-attech-dangbo-backend-uploads}"
HELPER_IMAGE="${HELPER_IMAGE:-alpine:3.20}"
APP_UID="${APP_UID:-10001}"   # uid của appuser trong Dockerfile.backend
DRY_RUN=0
ASSUME_YES=0
SKIP_UPLOADS=0
SOURCE=""

usage() {
    sed -n '2,15p' "$0" | sed 's/^# \{0,1\}//'
}

while [ $# -gt 0 ]; do
    case "$1" in
        --dry-run) DRY_RUN=1 ;;
        --yes) ASSUME_YES=1 ;;
        --skip-uploads) SKIP_UPLOADS=1 ;;
        -h|--help) usage; exit 0 ;;
        -*) echo "[LỖI] Tham số không hợp lệ: $1" >&2; usage >&2; exit 2 ;;
        *)
            [ -z "$SOURCE" ] || { echo "[LỖI] Chỉ được chỉ định một bản sao lưu." >&2; exit 2; }
            SOURCE="$1" ;;
    esac
    shift
done

[ -n "$SOURCE" ] || { usage >&2; exit 2; }
[ -d "$SOURCE" ] || { echo "[LỖI] Không tìm thấy thư mục sao lưu: $SOURCE" >&2; exit 1; }
SOURCE="$(cd "$SOURCE" && pwd)"

log() { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*"; }

run() {
    if [ "$DRY_RUN" -eq 1 ]; then
        echo "  (chạy khô) $*"
    else
        "$@"
    fi
}

compose() {
    docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" "$@"
}

log "[1/5] Kiểm tra bản sao lưu $SOURCE"
[ -f "$SOURCE/db.dump" ] || { echo "[LỖI] Thiếu $SOURCE/db.dump" >&2; exit 1; }
if [ "$SKIP_UPLOADS" -eq 0 ] && [ ! -f "$SOURCE/uploads.tar.gz" ]; then
    echo "[LỖI] Thiếu $SOURCE/uploads.tar.gz (dùng --skip-uploads nếu chỉ khôi phục CSDL)" >&2
    exit 1
fi
if [ -f "$SOURCE/SHA256SUMS" ]; then
    if [ "$SKIP_UPLOADS" -eq 1 ]; then
        (cd "$SOURCE" && grep -E '[ *]db\.dump$' SHA256SUMS | sha256sum -c -)
    else
        (cd "$SOURCE" && sha256sum -c SHA256SUMS)
    fi
else
    echo "[CẢNH BÁO] Không có SHA256SUMS — bỏ qua kiểm tra checksum."
fi
[ -f "$SOURCE/manifest.txt" ] && sed 's/^/  /' "$SOURCE/manifest.txt"

if [ "$DRY_RUN" -eq 0 ]; then
    command -v docker >/dev/null 2>&1 || { echo "[LỖI] Không tìm thấy lệnh docker." >&2; exit 1; }
    [ -f "$ENV_FILE" ] || { echo "[LỖI] Không tìm thấy file môi trường: $ENV_FILE" >&2; exit 1; }
fi

log "[2/5] Xác nhận"
echo "Thao tác này sẽ GHI ĐÈ toàn bộ CSDL hiện tại$([ "$SKIP_UPLOADS" -eq 0 ] && echo " và volume $UPLOAD_VOLUME")"
echo "bằng bản sao lưu: $SOURCE"
if [ "$DRY_RUN" -eq 0 ] && [ "$ASSUME_YES" -eq 0 ]; then
    read -r -p "Gõ KHOI PHUC để tiếp tục: " answer
    if [ "$answer" != "KHOI PHUC" ]; then
        echo "Đã hủy, không thay đổi dữ liệu."
        exit 1
    fi
fi

log "[3/5] Dừng frontend và backend"
run compose stop frontend backend
run compose up -d postgres

restart_app() {
    log "Khởi động lại backend và frontend"
    run compose up -d backend frontend
}
trap restart_app EXIT

log "[4/5] Khôi phục PostgreSQL (pg_restore --clean --if-exists --single-transaction)"
if [ "$DRY_RUN" -eq 1 ]; then
    echo "  (chạy khô) compose exec -T postgres pg_restore -U \$POSTGRES_USER -d \$POSTGRES_DB --clean --if-exists --no-owner --single-transaction < $SOURCE/db.dump"
else
    # Chờ postgres sẵn sàng.
    for _ in $(seq 1 30); do
        compose exec -T postgres sh -c 'pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"' >/dev/null 2>&1 && break
        sleep 2
    done
    compose exec -T postgres sh -c \
        'pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists --no-owner --no-password --single-transaction' \
        < "$SOURCE/db.dump"
fi

if [ "$SKIP_UPLOADS" -eq 0 ]; then
    log "[5/5] Khôi phục volume upload $UPLOAD_VOLUME"
    if [ "$DRY_RUN" -eq 1 ]; then
        echo "  (chạy khô) docker run --rm -v $UPLOAD_VOLUME:/data -v $SOURCE:/backup:ro $HELPER_IMAGE sh -c 'find /data -mindepth 1 -delete && tar xzf /backup/uploads.tar.gz -C /data && chown -R $APP_UID:$APP_UID /data'"
    else
        docker run --rm \
            -v "$UPLOAD_VOLUME:/data" \
            -v "$SOURCE:/backup:ro" \
            "$HELPER_IMAGE" sh -c "find /data -mindepth 1 -delete && tar xzf /backup/uploads.tar.gz -C /data && chown -R $APP_UID:$APP_UID /data"
    fi
else
    log "[5/5] Bỏ qua volume upload (--skip-uploads)"
fi

trap - EXIT
restart_app
log "Khôi phục hoàn tất. Kiểm tra: đăng nhập, mở một hồ sơ có minh chứng, gọi /api/healthz."
