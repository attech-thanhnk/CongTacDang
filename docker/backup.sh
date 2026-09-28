#!/usr/bin/env bash
# =============================================================================
# SAO LƯU CSDL POSTGRESQL + THƯ MỤC UPLOAD — CongTacDang (Docker Compose)
#
# Mỗi lần chạy tạo một thư mục <BACKUP_DIR>/congtacdang-YYYYMMDD-HHMMSS/ gồm:
#   db.dump          pg_dump định dạng custom (khôi phục bằng pg_restore)
#   uploads.tar.gz   nén toàn bộ volume upload của backend
#   SHA256SUMS       checksum để kiểm tra toàn vẹn
#   manifest.txt     thông tin bản sao lưu
# Sau đó chỉ giữ lại KEEP bản gần nhất.
#
# Cách dùng:
#   docker/backup.sh [--dry-run] [--backup-dir DIR] [--keep N]
# Biến môi trường tương đương: BACKUP_DIR, BACKUP_KEEP, ENV_FILE, COMPOSE_FILE,
#   UPLOAD_VOLUME, HELPER_IMAGE.
# Mật khẩu CSDL không đi qua dòng lệnh: pg_dump chạy trong container postgres
# bằng biến môi trường sẵn có của container.
# =============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKUP_DIR="${BACKUP_DIR:-/var/backups/congtacdang}"
KEEP="${BACKUP_KEEP:-14}"
ENV_FILE="${ENV_FILE:-$SCRIPT_DIR/.env}"
COMPOSE_FILE="${COMPOSE_FILE:-$SCRIPT_DIR/docker-compose.yml}"
UPLOAD_VOLUME="${UPLOAD_VOLUME:-attech-dangbo-backend-uploads}"
HELPER_IMAGE="${HELPER_IMAGE:-alpine:3.20}"
PREFIX="congtacdang"
DRY_RUN=0

usage() {
    sed -n '2,20p' "$0" | sed 's/^# \{0,1\}//'
}

while [ $# -gt 0 ]; do
    case "$1" in
        --dry-run) DRY_RUN=1 ;;
        --backup-dir) BACKUP_DIR="${2:?Thiếu giá trị cho --backup-dir}"; shift ;;
        --keep) KEEP="${2:?Thiếu giá trị cho --keep}"; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "[LỖI] Tham số không hợp lệ: $1" >&2; usage >&2; exit 2 ;;
    esac
    shift
done

if ! [[ "$KEEP" =~ ^[0-9]+$ ]] || [ "$KEEP" -lt 1 ]; then
    echo "[LỖI] --keep / BACKUP_KEEP phải là số nguyên >= 1 (hiện: '$KEEP')." >&2
    exit 2
fi

log() { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*"; }

# In lệnh khi chạy khô, thực thi khi chạy thật.
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

if [ "$DRY_RUN" -eq 0 ]; then
    command -v docker >/dev/null 2>&1 || { echo "[LỖI] Không tìm thấy lệnh docker." >&2; exit 1; }
    [ -f "$ENV_FILE" ] || { echo "[LỖI] Không tìm thấy file môi trường: $ENV_FILE" >&2; exit 1; }
    [ -f "$COMPOSE_FILE" ] || { echo "[LỖI] Không tìm thấy compose file: $COMPOSE_FILE" >&2; exit 1; }
fi

TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
TARGET="$BACKUP_DIR/$PREFIX-$TIMESTAMP"
PARTIAL="$TARGET.partial"

cleanup_partial() {
    if [ "$DRY_RUN" -eq 0 ] && [ -d "$PARTIAL" ]; then
        log "Sao lưu thất bại — xóa bản dở dang $PARTIAL"
        rm -rf -- "$PARTIAL"
    fi
}
trap cleanup_partial EXIT

log "Bắt đầu sao lưu vào $TARGET"
run mkdir -p "$PARTIAL"
run chmod 700 "$PARTIAL"

log "[1/4] Dump PostgreSQL (pg_dump --format=custom)"
if [ "$DRY_RUN" -eq 1 ]; then
    echo "  (chạy khô) compose exec -T postgres pg_dump -U \$POSTGRES_USER -d \$POSTGRES_DB --format=custom > $PARTIAL/db.dump"
else
    compose exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom --no-password' \
        > "$PARTIAL/db.dump"
    # Kiểm tra bản dump đọc được (liệt kê mục lục) ngay sau khi tạo.
    compose exec -T postgres pg_restore --list < "$PARTIAL/db.dump" > /dev/null
fi

log "[2/4] Nén volume upload $UPLOAD_VOLUME"
if [ "$DRY_RUN" -eq 1 ]; then
    echo "  (chạy khô) docker run --rm -v $UPLOAD_VOLUME:/data:ro -v $PARTIAL:/backup $HELPER_IMAGE tar czf /backup/uploads.tar.gz -C /data ."
else
    docker volume inspect "$UPLOAD_VOLUME" >/dev/null
    docker run --rm \
        -v "$UPLOAD_VOLUME:/data:ro" \
        -v "$PARTIAL:/backup" \
        "$HELPER_IMAGE" sh -c 'tar czf /backup/uploads.tar.gz -C /data . && chown '"$(id -u):$(id -g)"' /backup/uploads.tar.gz'
    tar tzf "$PARTIAL/uploads.tar.gz" > /dev/null
fi

log "[3/4] Ghi checksum và manifest"
if [ "$DRY_RUN" -eq 0 ]; then
    (cd "$PARTIAL" && sha256sum db.dump uploads.tar.gz > SHA256SUMS)
    {
        echo "created_at=$(date -Iseconds)"
        echo "host=$(hostname)"
        echo "upload_volume=$UPLOAD_VOLUME"
        echo "db_dump_bytes=$(stat -c %s "$PARTIAL/db.dump")"
        echo "uploads_bytes=$(stat -c %s "$PARTIAL/uploads.tar.gz")"
        echo "git_commit=$(git -C "$SCRIPT_DIR" rev-parse --short HEAD 2>/dev/null || echo unknown)"
    } > "$PARTIAL/manifest.txt"
    mv -- "$PARTIAL" "$TARGET"
else
    echo "  (chạy khô) sha256sum db.dump uploads.tar.gz > SHA256SUMS; mv $PARTIAL $TARGET"
fi

log "[4/4] Giữ lại $KEEP bản gần nhất trong $BACKUP_DIR"
if [ -d "$BACKUP_DIR" ]; then
    # Tên thư mục chứa dấu thời gian nên sắp xếp theo tên = theo thời gian.
    mapfile -t ALL_BACKUPS < <(find "$BACKUP_DIR" -mindepth 1 -maxdepth 1 -type d \
        -regextype posix-extended -regex ".*/$PREFIX-[0-9]{8}-[0-9]{6}" | sort)
    COUNT=${#ALL_BACKUPS[@]}
    if [ "$COUNT" -gt "$KEEP" ]; then
        for old in "${ALL_BACKUPS[@]:0:$((COUNT - KEEP))}"; do
            log "  Xóa bản cũ: $old"
            run rm -rf -- "$old"
        done
    fi
fi

trap - EXIT
log "Hoàn tất: $TARGET"
