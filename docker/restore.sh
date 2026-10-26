#!/usr/bin/env bash
# =============================================================================
# KHÔI PHỤC CSDL POSTGRESQL + THƯ MỤC UPLOAD TỪ MỘT BẢN SAO LƯU — CongTacDang
#
# Cách dùng:
#   docker/restore.sh [tùy chọn] <thư-mục-bản-sao-lưu>
# Ví dụ:
#   docker/restore.sh --check /var/backups/congtacdang/congtacdang-20260928-020000
#   docker/restore.sh /var/backups/congtacdang/congtacdang-20260928-020000
#
# Tùy chọn:
#   --check               chỉ kiểm tra bản sao lưu và so sánh migration, không thay đổi gì
#   --dry-run             in các lệnh sẽ chạy (vẫn đọc bản sao lưu để so sánh migration)
#   --yes                 bỏ các bước gõ xác nhận (kịch bản tự động đã kiểm soát)
#   --allow-upgrade       cùng --yes: chấp nhận bản sao lưu CŨ hơn mã nguồn (sẽ được nâng cấp schema)
#   --skip-uploads        chỉ khôi phục CSDL, giữ nguyên volume upload
#   --no-start            khôi phục xong để backend/frontend ở trạng thái dừng
#   --no-safety-backup    KHÔNG sao lưu dự phòng hiện trạng trước khi ghi đè (không khuyến nghị)
#   --safety-dir DIR      nơi lưu bản dự phòng (mặc định: <thư mục cha của bản sao lưu>/truoc-khoi-phuc)
#   --repo-dir DIR        thư mục mã nguồn đang triển khai (mặc định: thư mục cha của docker/)
#
# Quy trình: kiểm tra checksum → so sánh migration trong bản sao lưu với migration của mã nguồn
#   (bản sao lưu MỚI hơn mã nguồn → dừng, không khôi phục) → xác nhận → dừng frontend/backend
#   → sao lưu dự phòng hiện trạng (backup.sh) → khôi phục vào CSDL tạm <POSTGRES_DB>_khoi_phuc
#   (pg_restore --no-owner vào CSDL mới hoàn toàn, nên không sót đối tượng của phiên bản mới hơn)
#   → thành công mới đổi tên: CSDL hiện tại → <POSTGRES_DB>_truoc_khoi_phuc_<ngày_giờ>, CSDL tạm → <POSTGRES_DB>
#   → thay nội dung volume upload → khởi động lại backend/frontend (AutoMigrate nâng cấp schema nếu bản cũ hơn).
#   Lỗi trước khi đổi tên: CSDL hiện tại giữ nguyên, CSDL tạm bị xóa. Script KHÔNG tự xóa CSDL cũ được giữ lại.
# Biến môi trường: ENV_FILE, COMPOSE_FILE, UPLOAD_VOLUME, HELPER_IMAGE, PG_IMAGE, APP_UID, SAFETY_DIR, REPO_DIR.
# =============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="${ENV_FILE:-$SCRIPT_DIR/.env}"
COMPOSE_FILE="${COMPOSE_FILE:-$SCRIPT_DIR/docker-compose.yml}"
UPLOAD_VOLUME="${UPLOAD_VOLUME:-attech-dangbo-backend-uploads}"
HELPER_IMAGE="${HELPER_IMAGE:-alpine:3.20}"
PG_IMAGE="${PG_IMAGE:-postgres:16-alpine}"   # chỉ dùng để đọc bản dump khi container postgres chưa chạy
APP_UID="${APP_UID:-10001}"   # uid của appuser trong Dockerfile.backend
REPO_DIR="${REPO_DIR:-$(cd "$SCRIPT_DIR/.." && pwd)}"
SAFETY_DIR="${SAFETY_DIR:-}"
DRY_RUN=0
CHECK_ONLY=0
ASSUME_YES=0
ALLOW_UPGRADE=0
SKIP_UPLOADS=0
NO_START=0
SAFETY_BACKUP=1
SOURCE=""

usage() {
    sed -n '2,30p' "$0" | sed 's/^# \{0,1\}//'
}

while [ $# -gt 0 ]; do
    case "$1" in
        --dry-run) DRY_RUN=1 ;;
        --check) CHECK_ONLY=1 ;;
        --yes) ASSUME_YES=1 ;;
        --allow-upgrade) ALLOW_UPGRADE=1 ;;
        --skip-uploads) SKIP_UPLOADS=1 ;;
        --no-start) NO_START=1 ;;
        --no-safety-backup) SAFETY_BACKUP=0 ;;
        --safety-dir) SAFETY_DIR="${2:?Thiếu giá trị cho --safety-dir}"; shift ;;
        --repo-dir) REPO_DIR="${2:?Thiếu giá trị cho --repo-dir}"; shift ;;
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
[ -n "$SAFETY_DIR" ] || SAFETY_DIR="$(dirname "$SOURCE")/truoc-khoi-phuc"
MIGRATIONS_DIR="$REPO_DIR/backend/src/CongTacDang.Infrastructure/Data/Migrations"

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

# Hỏi xác nhận bằng một cụm từ; --yes bỏ qua. Không khớp → hủy, không thay đổi dữ liệu.
confirm() {
    local phrase="$1" prompt="$2" answer
    if [ "$ASSUME_YES" -eq 1 ] || [ "$DRY_RUN" -eq 1 ]; then
        return 0
    fi
    read -r -p "$prompt (gõ $phrase để tiếp tục): " answer
    if [ "$answer" != "$phrase" ]; then
        echo "Đã hủy, không thay đổi dữ liệu."
        exit 1
    fi
}

postgres_running() {
    command -v docker >/dev/null 2>&1 || return 1
    [ -f "$ENV_FILE" ] || return 1
    [ -n "$(compose ps --status running -q postgres 2>/dev/null)" ]
}

# In danh sách MigrationId trong bảng __EFMigrationsHistory của bản dump (đọc dump, không đụng CSDL).
dump_migrations() {
    local reader
    if postgres_running; then
        reader=(compose exec -T postgres pg_restore --data-only --table=__EFMigrationsHistory --file=-)
    else
        reader=(docker run --rm -i "$PG_IMAGE" pg_restore --data-only --table=__EFMigrationsHistory --file=-)
    fi
    "${reader[@]}" < "$SOURCE/db.dump" | awk -F '\t' '
        /^COPY .*__EFMigrationsHistory/ { inside = 1; next }
        inside && /^\\\.$/ { inside = 0; next }
        inside && NF > 0 { print $1 }' | sort -u
}

# In danh sách migration của mã nguồn đang triển khai (tên file <MigrationId>.cs, bỏ .Designer.cs).
code_migrations() {
    find "$MIGRATIONS_DIR" -maxdepth 1 -type f -name '[0-9]*_*.cs' ! -name '*.Designer.cs' -printf '%f\n' \
        | sed 's/\.cs$//' | sort -u
}

# In danh sách migration CSDL hiện tại đã áp dụng (chỉ để thông tin).
current_db_migrations() {
    compose exec -T postgres sh -c \
        'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tA -c "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY 1"' \
        2>/dev/null | sort -u
}

log "[1/7] Kiểm tra bản sao lưu $SOURCE"
[ -f "$SOURCE/db.dump" ] || { echo "[LỖI] Thiếu $SOURCE/db.dump" >&2; exit 1; }
if [ "$SKIP_UPLOADS" -eq 0 ] && [ ! -f "$SOURCE/uploads.tar.gz" ]; then
    echo "[LỖI] Thiếu $SOURCE/uploads.tar.gz (dùng --skip-uploads nếu chỉ khôi phục CSDL)" >&2
    exit 1
fi
if [ -f "$SOURCE/SHA256SUMS" ]; then
    if [ "$SKIP_UPLOADS" -eq 1 ]; then
        (cd "$SOURCE" && grep -E '[ *]db\.dump$' SHA256SUMS | sha256sum -c -)
    else
        (cd "$SOURCE" && grep -E '[ *](db\.dump|uploads\.tar\.gz)$' SHA256SUMS | sha256sum -c -)
    fi
else
    echo "[CẢNH BÁO] Không có SHA256SUMS — bỏ qua kiểm tra checksum."
fi
[ -f "$SOURCE/manifest.txt" ] && sed 's/^/  /' "$SOURCE/manifest.txt"

command -v docker >/dev/null 2>&1 || { echo "[LỖI] Không tìm thấy lệnh docker (cần để đọc bản dump)." >&2; exit 1; }
if [ "$DRY_RUN" -eq 0 ] && [ "$CHECK_ONLY" -eq 0 ]; then
    [ -f "$ENV_FILE" ] || { echo "[LỖI] Không tìm thấy file môi trường: $ENV_FILE" >&2; exit 1; }
fi

log "[2/7] So sánh migration của bản sao lưu với mã nguồn đang triển khai"
[ -d "$MIGRATIONS_DIR" ] || { echo "[LỖI] Không tìm thấy thư mục migration của mã nguồn: $MIGRATIONS_DIR (dùng --repo-dir)" >&2; exit 1; }
BACKUP_MIGRATIONS="$(dump_migrations)" || { echo "[LỖI] Không đọc được bản dump bằng pg_restore." >&2; exit 1; }
CODE_MIGRATIONS="$(code_migrations)"
CODE_COMMIT="$(git -C "$REPO_DIR" rev-parse --short HEAD 2>/dev/null || echo unknown)"
BACKUP_COMMIT="$(sed -n 's/^git_commit=//p' "$SOURCE/manifest.txt" 2>/dev/null || true)"
echo "  Mã nguồn: commit $CODE_COMMIT, $(printf '%s\n' "$CODE_MIGRATIONS" | grep -c . || true) migration, mới nhất: $(printf '%s\n' "$CODE_MIGRATIONS" | tail -n 1)"
echo "  Bản sao lưu: commit ${BACKUP_COMMIT:-không rõ}, $(printf '%s\n' "$BACKUP_MIGRATIONS" | grep -c . || true) migration, mới nhất: $(printf '%s\n' "$BACKUP_MIGRATIONS" | tail -n 1)"
if postgres_running; then
    CURRENT_LAST="$(current_db_migrations | tail -n 1)"
    echo "  CSDL hiện tại (được giữ lại dưới tên khác): migration mới nhất ${CURRENT_LAST:-không có}"
fi

if [ -z "$BACKUP_MIGRATIONS" ]; then
    echo "[LỖI] Bản sao lưu không có dữ liệu __EFMigrationsHistory — không xác định được phiên bản schema, không khôi phục." >&2
    exit 1
fi

# Migration có trong bản sao lưu nhưng mã nguồn không có → dữ liệu MỚI hơn mã nguồn.
NEWER_IN_BACKUP="$(comm -23 <(printf '%s\n' "$BACKUP_MIGRATIONS") <(printf '%s\n' "$CODE_MIGRATIONS"))"
# Migration mã nguồn có mà bản sao lưu chưa có → bản sao lưu CŨ hơn, sẽ được AutoMigrate nâng cấp.
PENDING_UPGRADE="$(comm -13 <(printf '%s\n' "$BACKUP_MIGRATIONS") <(printf '%s\n' "$CODE_MIGRATIONS"))"

if [ -n "$NEWER_IN_BACKUP" ]; then
    echo "[LỖI] Bản sao lưu MỚI hơn mã nguồn đang triển khai — có migration mã nguồn không biết:" >&2
    printf '%s\n' "$NEWER_IN_BACKUP" | sed 's/^/    /' >&2
    echo "  Mã nguồn cũ không chạy được trên dữ liệu mới. Triển khai đúng phiên bản mã nguồn của bản sao lưu" >&2
    echo "  (commit ${BACKUP_COMMIT:-ghi trong manifest.txt}) rồi chạy lại script. Không thay đổi dữ liệu." >&2
    exit 1
fi

if [ -n "$PENDING_UPGRADE" ]; then
    echo "[CẢNH BÁO] Bản sao lưu CŨ hơn mã nguồn. Khi backend khởi động (Database__AutoMigrate=true) schema sẽ được"
    echo "  nâng cấp tự động qua các migration sau; sau đó không quay lại phiên bản cũ được nếu không khôi phục lại:"
    printf '%s\n' "$PENDING_UPGRADE" | sed 's/^/    /'
    echo "  Nếu Database__AutoMigrate=false: chạy 'dotnet ef database update' trước khi dùng (xem docs/database-migrations.md)."
else
    echo "  Migration của bản sao lưu khớp mã nguồn."
fi

if [ "$CHECK_ONLY" -eq 1 ]; then
    log "Chỉ kiểm tra (--check): không thay đổi dữ liệu."
    exit 0
fi

log "[3/7] Xác nhận"
echo "Thao tác này thay CSDL hiện tại bằng bản sao lưu: $SOURCE"
echo "CSDL hiện tại được đổi tên và giữ lại (script không tự xóa)$([ "$SKIP_UPLOADS" -eq 0 ] && echo "; volume $UPLOAD_VOLUME bị GHI ĐÈ")."
if [ "$SAFETY_BACKUP" -eq 1 ]; then
    echo "Hiện trạng được sao lưu dự phòng trước vào: $SAFETY_DIR"
else
    echo "[CẢNH BÁO] --no-safety-backup: KHÔNG sao lưu dự phòng hiện trạng trước khi ghi đè."
fi
if [ -n "$PENDING_UPGRADE" ]; then
    if [ "$ASSUME_YES" -eq 1 ] && [ "$ALLOW_UPGRADE" -eq 0 ] && [ "$DRY_RUN" -eq 0 ]; then
        echo "[LỖI] Bản sao lưu cũ hơn mã nguồn: chạy tự động (--yes) phải kèm --allow-upgrade." >&2
        exit 1
    fi
    confirm "NANG CAP" "Bản sao lưu cũ hơn mã nguồn và sẽ được nâng cấp schema khi khởi động"
fi
confirm "KHOI PHUC" "Thay dữ liệu hiện tại bằng bản sao lưu"

log "[4/7] Dừng frontend và backend (tránh ghi dữ liệu và tự migrate trong lúc khôi phục)"
run compose stop frontend backend
run compose up -d postgres
if [ "$DRY_RUN" -eq 0 ]; then
    # Chờ postgres sẵn sàng.
    for _ in $(seq 1 30); do
        compose exec -T postgres sh -c 'pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"' >/dev/null 2>&1 && break
        sleep 2
    done
fi

start_app() {
    if [ "$NO_START" -eq 1 ]; then
        log "Giữ backend/frontend ở trạng thái dừng (--no-start). Khởi động: docker compose --env-file $ENV_FILE -f $COMPOSE_FILE up -d"
        return 0
    fi
    log "Khởi động lại backend và frontend"
    run compose up -d backend frontend
}

if [ "$SAFETY_BACKUP" -eq 1 ]; then
    log "[5/7] Sao lưu dự phòng hiện trạng vào $SAFETY_DIR"
    SAFETY_ARGS=(--backup-dir "$SAFETY_DIR" --keep 10)
    [ "$DRY_RUN" -eq 1 ] && SAFETY_ARGS+=(--dry-run)
    if ! ENV_FILE="$ENV_FILE" COMPOSE_FILE="$COMPOSE_FILE" UPLOAD_VOLUME="$UPLOAD_VOLUME" HELPER_IMAGE="$HELPER_IMAGE" \
        "$SCRIPT_DIR/backup.sh" "${SAFETY_ARGS[@]}"; then
        echo "[LỖI] Sao lưu dự phòng thất bại — dừng khôi phục, dữ liệu hiện tại giữ nguyên." >&2
        start_app
        exit 1
    fi
else
    log "[5/7] Bỏ qua sao lưu dự phòng (--no-safety-backup)"
fi

# Lỗi giữa chừng: CSDL hiện tại chưa bị đụng tới; vẫn khởi động lại ứng dụng.
trap start_app EXIT

if [ "$DRY_RUN" -eq 1 ]; then
    DB_NAME='<POSTGRES_DB>'
else
    DB_NAME="$(compose exec -T postgres sh -c 'printf %s "$POSTGRES_DB"')"
    if ! [[ "$DB_NAME" =~ ^[A-Za-z0-9_]+$ ]]; then
        echo "[LỖI] Tên CSDL '$DB_NAME' có ký tự ngoài chữ, số, gạch dưới — đặt lại POSTGRES_DB trong $ENV_FILE rồi chạy lại. Dữ liệu giữ nguyên." >&2
        exit 1
    fi
fi
TMP_DB="${DB_NAME}_khoi_phuc"
OLD_DB="${DB_NAME}_truoc_khoi_phuc_$(date +%Y%m%d_%H%M%S)"

# Chạy SQL (đọc từ stdin) trên CSDL quản trị `postgres` trong container.
admin_sql() {
    compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d postgres -v ON_ERROR_STOP=1 -q --no-password'
}

drop_tmp_db() {
    printf 'DROP DATABASE IF EXISTS "%s";\n' "$TMP_DB" | admin_sql >/dev/null 2>&1 || true
}

log "[6/7] Khôi phục PostgreSQL vào CSDL tạm $TMP_DB rồi đổi tên thay $DB_NAME"
if [ "$DRY_RUN" -eq 1 ]; then
    echo "  (chạy khô) psql -d postgres: DROP DATABASE IF EXISTS \"$TMP_DB\"; CREATE DATABASE \"$TMP_DB\""
    echo "  (chạy khô) compose exec -T postgres pg_restore -U \$POSTGRES_USER -d $TMP_DB --no-owner --single-transaction --exit-on-error < $SOURCE/db.dump"
    echo "  (chạy khô) psql -d postgres: ngắt kết nối còn lại; ALTER DATABASE \"$DB_NAME\" RENAME TO \"$OLD_DB\"; ALTER DATABASE \"$TMP_DB\" RENAME TO \"$DB_NAME\""
else
    if ! printf 'SET client_min_messages = warning;\nDROP DATABASE IF EXISTS "%s";\nCREATE DATABASE "%s";\n' "$TMP_DB" "$TMP_DB" | admin_sql; then
        drop_tmp_db
        echo "[LỖI] Tạo CSDL tạm $TMP_DB không thành công — CSDL $DB_NAME giữ nguyên. Kiểm tra quyền của POSTGRES_USER và dung lượng đĩa rồi chạy lại." >&2
        exit 1
    fi
    if ! compose exec -T postgres sh -c \
        'pg_restore -U "$POSTGRES_USER" -d "$1" --no-owner --no-password --single-transaction --exit-on-error' _ "$TMP_DB" \
        < "$SOURCE/db.dump"; then
        drop_tmp_db
        echo "[LỖI] Khôi phục vào CSDL tạm không thành công (bản dump hỏng hoặc thiếu) — đã xóa CSDL tạm, CSDL $DB_NAME giữ nguyên. Dùng bản sao lưu khác hoặc kiểm tra lại db.dump." >&2
        exit 1
    fi
    # Hai lệnh đổi tên nằm trong một giao dịch: hoặc đổi cả hai, hoặc không đổi gì.
    if ! printf '%s\n' \
        "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname IN ('$DB_NAME', '$TMP_DB') AND pid <> pg_backend_pid();" \
        "BEGIN;" \
        "ALTER DATABASE \"$DB_NAME\" RENAME TO \"$OLD_DB\";" \
        "ALTER DATABASE \"$TMP_DB\" RENAME TO \"$DB_NAME\";" \
        "COMMIT;" | admin_sql >/dev/null; then
        drop_tmp_db
        echo "[LỖI] Đổi tên CSDL không thành công (có thể còn kết nối tới $DB_NAME) — đã xóa CSDL tạm, $DB_NAME giữ nguyên. Dừng ứng dụng đang kết nối rồi chạy lại." >&2
        exit 1
    fi
    log "Đã thay CSDL: $DB_NAME là dữ liệu của bản sao lưu; dữ liệu trước khôi phục còn ở CSDL $OLD_DB."
fi

if [ "$SKIP_UPLOADS" -eq 0 ]; then
    log "[7/7] Khôi phục volume upload $UPLOAD_VOLUME"
    if [ "$DRY_RUN" -eq 1 ]; then
        echo "  (chạy khô) docker run --rm -v $UPLOAD_VOLUME:/data -v $SOURCE:/backup:ro $HELPER_IMAGE sh -c 'find /data -mindepth 1 -delete && tar xzf /backup/uploads.tar.gz -C /data && chown -R $APP_UID:$APP_UID /data'"
    else
        docker run --rm \
            -v "$UPLOAD_VOLUME:/data" \
            -v "$SOURCE:/backup:ro" \
            "$HELPER_IMAGE" sh -c "find /data -mindepth 1 -delete && tar xzf /backup/uploads.tar.gz -C /data && chown -R $APP_UID:$APP_UID /data"
    fi
else
    log "[7/7] Bỏ qua volume upload (--skip-uploads)"
fi

trap - EXIT
start_app
if [ -n "$PENDING_UPGRADE" ] && [ "$NO_START" -eq 0 ]; then
    log "Backend đang nâng cấp schema khi khởi động — theo dõi: docker compose --env-file $ENV_FILE -f $COMPOSE_FILE logs -f backend"
fi
log "Khôi phục hoàn tất. Kiểm tra sau khi container healthy:"
echo "  docker compose --env-file $ENV_FILE -f $COMPOSE_FILE exec backend curl --fail --silent http://localhost:5000/healthz   (in Healthy)"
echo "  curl -s -o /dev/null -w '%{http_code}\n' http://127.0.0.1:\${FRONTEND_PORT:-3001}/login   (in 200)"
echo "  rồi đăng nhập và mở một hồ sơ có minh chứng."
if [ "$DRY_RUN" -eq 0 ]; then
    log "CSDL trước khôi phục được giữ lại: $OLD_DB. Khi đã yên tâm, xóa để lấy lại dung lượng:"
    echo "  docker compose --env-file $ENV_FILE -f $COMPOSE_FILE exec postgres sh -c 'psql -U \"\$POSTGRES_USER\" -d postgres -c \"DROP DATABASE \\\"$OLD_DB\\\";\"'"
fi
log "Bản dự phòng hiện trạng trước khôi phục (nếu có): $SAFETY_DIR"
