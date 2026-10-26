#!/usr/bin/env bash
# =============================================================================
# TÙY CHỈNH TRANG ĐĂNG NHẬP — XUẤT TỪ MỘT MÁY, ÁP DỤNG LÊN MÁY KHÁC
#
# Ảnh nền và vị trí khung đăng nhập là dữ liệu do quản trị đặt trên giao diện
# (Hệ thống → Thông tin đơn vị), không nằm trong seed hay mã nguồn, nên không tự
# đi theo khi dựng máy mới hoặc thay máy. Script này mang chúng đi.
#
# Cách dùng:
#   docker/tuy-chinh-giao-dien.sh xuat    THƯ_MỤC            Lấy từ máy đang chạy
#   docker/tuy-chinh-giao-dien.sh ap-dung THƯ_MỤC [--kiem-tra]  Đặt vào máy này
#
# THƯ_MỤC chứa: tuy-chinh.env (các giá trị) và anh-nen.* (ảnh nền, nếu có).
# --kiem-tra: chỉ kiểm tra và in việc sẽ làm, không đổi gì.
# Biến môi trường: PG_CONTAINER (mặc định attech-dangbo-postgres),
#   API_CONTAINER (attech-dangbo-api), DB_NAME (mặc định lấy từ container postgres).
# Sau khi áp dụng, backend được khởi động lại để xóa bộ nhớ đệm cài đặt.
# Chỉ đổi 5 cột đăng nhập của bảng organization_settings; không đụng gì khác.
# =============================================================================
set -euo pipefail

PG_CONTAINER="${PG_CONTAINER:-attech-dangbo-postgres}"
API_CONTAINER="${API_CONTAINER:-attech-dangbo-api}"
UPLOAD_DIR="/app/uploads"

die() { echo "[LỖI] $*" >&2; exit 1; }
log() { echo "[$(date +%H:%M:%S)] $*"; }

usage() { sed -n '2,19p' "$0" | sed 's/^# \{0,1\}//'; }

[ $# -ge 1 ] || { usage; exit 1; }
case "$1" in -h|--help) usage; exit 0 ;; esac
CMD="$1"; DIR="${2:-}"; shift || true
[ -n "$DIR" ] || die "Thiếu THƯ_MỤC. Xem: $0 --help"
shift || true
KIEM_TRA=0
for a in "$@"; do
    case "$a" in
        --kiem-tra) KIEM_TRA=1 ;;
        *) die "Tham số không nhận ra: $a" ;;
    esac
done

command -v docker >/dev/null || die "Chưa có lệnh docker."
docker inspect -f '{{.State.Running}}' "$PG_CONTAINER" 2>/dev/null | grep -q true \
    || die "Container PostgreSQL '$PG_CONTAINER' không chạy. Khởi động stack trước (hoặc đặt PG_CONTAINER)."
docker inspect -f '{{.State.Running}}' "$API_CONTAINER" 2>/dev/null | grep -q true \
    || die "Container backend '$API_CONTAINER' không chạy. Khởi động stack trước (hoặc đặt API_CONTAINER)."

DB_NAME="${DB_NAME:-$(docker exec "$PG_CONTAINER" sh -c 'printf %s "$POSTGRES_DB"')}"
[ -n "$DB_NAME" ] || die "Không xác định được tên CSDL. Đặt DB_NAME."
[[ "$DB_NAME" =~ ^[A-Za-z0-9_]+$ ]] || die "Tên CSDL '$DB_NAME' có ký tự không hợp lệ."

pg() { docker exec -i "$PG_CONTAINER" sh -c 'exec psql -U "$POSTGRES_USER" -X -w -q -v ON_ERROR_STOP=1 "$@"' sh -d "$DB_NAME" "$@"; }

case "$CMD" in
# -----------------------------------------------------------------------------
xuat)
    mkdir -p "$DIR"
    ROW="$(pg -At -F $'\t' -c 'select coalesce("LoginBackgroundKey",'"''"'), coalesce("LoginBackgroundFileName",'"''"'), coalesce("LoginBackgroundContentType",'"''"'), "LoginPanelPosition" from organization_settings limit 1')"
    [ -n "$ROW" ] || die "Bảng organization_settings chưa có dòng nào. Mở trang Thông tin đơn vị một lần rồi thử lại."
    IFS=$'\t' read -r KEY FNAME CTYPE POS <<< "$ROW"
    {
        echo "# Tùy chỉnh trang đăng nhập — tạo bởi tuy-chinh-giao-dien.sh xuat"
        echo "LOGIN_PANEL_POSITION=$POS"
        echo "LOGIN_BACKGROUND_KEY=$KEY"
        echo "LOGIN_BACKGROUND_FILE_NAME=$FNAME"
        echo "LOGIN_BACKGROUND_CONTENT_TYPE=$CTYPE"
    } > "$DIR/tuy-chinh.env"
    rm -f "$DIR"/anh-nen.*
    if [ -n "$KEY" ]; then
        EXT="${KEY##*.}"
        docker cp "$API_CONTAINER:$UPLOAD_DIR/$KEY" "$DIR/anh-nen.$EXT" >/dev/null 2>&1 \
            || die "Không chép được ảnh nền '$KEY' từ container $API_CONTAINER (kiểm tra tệp có trong $UPLOAD_DIR)."
        log "Đã xuất ảnh nền: $DIR/anh-nen.$EXT ($(wc -c < "$DIR/anh-nen.$EXT") byte)"
    else
        log "Máy này chưa đặt ảnh nền đăng nhập; chỉ xuất vị trí khung."
    fi
    log "Đã xuất tùy chỉnh vào $DIR (vị trí khung: $POS)."
    ;;
# -----------------------------------------------------------------------------
ap-dung)
    [ -f "$DIR/tuy-chinh.env" ] || die "Không thấy $DIR/tuy-chinh.env. Chạy 'xuat' trước hoặc kiểm đường dẫn."
    get() { grep -E "^$1=" "$DIR/tuy-chinh.env" | head -1 | cut -d= -f2-; }
    POS="$(get LOGIN_PANEL_POSITION)"; KEY="$(get LOGIN_BACKGROUND_KEY)"
    FNAME="$(get LOGIN_BACKGROUND_FILE_NAME)"; CTYPE="$(get LOGIN_BACKGROUND_CONTENT_TYPE)"

    case "$POS" in left|center|right) ;; *) die "Vị trí khung '$POS' không hợp lệ (chấp nhận: left, center, right).";; esac
    if [ -n "$KEY" ]; then
        [[ "$KEY" =~ ^login-background/[A-Za-z0-9._-]+$ ]] || die "Khóa ảnh nền '$KEY' không hợp lệ."
        [[ "$FNAME" =~ ^[A-Za-z0-9._-]*$ ]] || die "Tên tệp ảnh '$FNAME' có ký tự không hợp lệ."
        [[ "$CTYPE" =~ ^image/(jpeg|png|webp)$ ]] || die "Loại ảnh '$CTYPE' không hợp lệ (chấp nhận: jpeg, png, webp)."
        IMG="$(ls "$DIR"/anh-nen.* 2>/dev/null | head -1 || true)"
        [ -n "$IMG" ] && [ -s "$IMG" ] || die "Thiếu file ảnh nền anh-nen.* trong $DIR."
        MAGIC="$(head -c 4 "$IMG" | od -An -tx1 | tr -d ' \n')"
        case "$MAGIC" in ffd8ff*|89504e47|52494646) ;; *) die "File $IMG không phải ảnh JPEG/PNG/WebP hợp lệ.";; esac
    fi

    log "Môi trường: CSDL $DB_NAME, backend $API_CONTAINER"
    log "Sẽ đặt: vị trí khung = $POS; ảnh nền = ${KEY:-<không>}"
    if [ "$KIEM_TRA" -eq 1 ]; then
        log "Chế độ --kiem-tra: kiểm tra đạt, không đổi gì."
        exit 0
    fi

    if [ -n "$KEY" ]; then
        docker exec -u root "$API_CONTAINER" sh -c "mkdir -p '$UPLOAD_DIR/login-background'"
        docker cp "$IMG" "$API_CONTAINER:$UPLOAD_DIR/$KEY" >/dev/null
        docker exec -u root "$API_CONTAINER" sh -c "chown -R appuser:appuser '$UPLOAD_DIR/login-background' && chmod 644 '$UPLOAD_DIR/$KEY'"
        log "Đã chép ảnh nền vào $UPLOAD_DIR/$KEY"
        pg -v pos="$POS" -v key="$KEY" -v fname="$FNAME" -v ctype="$CTYPE" <<'SQL'
UPDATE organization_settings
   SET "LoginPanelPosition" = :'pos', "LoginBackgroundKey" = :'key',
       "LoginBackgroundFileName" = :'fname', "LoginBackgroundContentType" = :'ctype',
       "LoginBackgroundUpdatedAt" = now(), "UpdatedAt" = now();
SQL
    else
        pg -v pos="$POS" <<'SQL'
UPDATE organization_settings SET "LoginPanelPosition" = :'pos', "UpdatedAt" = now();
SQL
    fi
    log "Đã cập nhật cài đặt đơn vị. Khởi động lại backend để xóa bộ nhớ đệm..."
    docker restart "$API_CONTAINER" >/dev/null
    for _ in $(seq 1 40); do
        [ "$(docker inspect -f '{{.State.Health.Status}}' "$API_CONTAINER" 2>/dev/null)" = "healthy" ] && break
        sleep 2
    done
    [ "$(docker inspect -f '{{.State.Health.Status}}' "$API_CONTAINER" 2>/dev/null)" = "healthy" ] \
        || die "Backend chưa khỏe sau khi khởi động lại. Xem log: docker logs --tail 50 $API_CONTAINER"
    log "Xong. Mở trang đăng nhập và bấm Ctrl+F5 để thấy thay đổi."
    ;;
*)
    die "Lệnh '$CMD' không nhận ra. Dùng: xuat | ap-dung. Xem: $0 --help"
    ;;
esac
