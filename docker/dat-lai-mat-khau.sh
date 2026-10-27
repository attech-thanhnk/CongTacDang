#!/usr/bin/env bash
# =============================================================================
# ĐẶT LẠI MẬT KHẨU MỘT TÀI KHOẢN BẰNG DÒNG LỆNH — dùng khi mất quản trị
#
# Chạy trên máy chủ khi không còn ai đăng nhập được vào trang Hệ thống → Tài khoản.
# Mật khẩu tạm ngẫu nhiên được in ra MỘT lần; người dùng phải đổi ngay ở lần đăng nhập đầu.
#
# Cách dùng:
#   docker/dat-lai-mat-khau.sh TÊN_ĐĂNG_NHẬP [--mat-khau-tam GIÁ_TRỊ] [--kiem-tra]
#
# --mat-khau-tam: dùng giá trị này thay vì sinh ngẫu nhiên (từ 8 ký tự, có chữ và số; tối đa 72 byte).
#                 Giá trị gõ trên dòng lệnh có thể còn trong lịch sử shell, nên nên bỏ trống để sinh ngẫu nhiên.
# --kiem-tra:     chỉ kiểm tra tài khoản và môi trường, in việc sẽ làm, không đổi gì.
# Biến môi trường: PG_CONTAINER (mặc định attech-dangbo-postgres), DB_NAME (mặc định lấy từ container postgres).
#
# Chỉ đổi đúng tài khoản được chỉ định: mật khẩu, buộc đổi mật khẩu, xóa đếm sai và khóa tạm, đổi dấu phiên
# (đăng xuất mọi phiên, hiệu lực trong vòng một phút), thu hồi refresh token, ghi một dòng vào nhật ký (audit_logs).
# Không bật lại tài khoản bị tắt, không gán thêm vai trò hay quyền nào.
# =============================================================================
set -euo pipefail

PG_CONTAINER="${PG_CONTAINER:-attech-dangbo-postgres}"

die() { echo "[LỖI] $*" >&2; exit 1; }
log() { echo "[$(date +%H:%M:%S)] $*"; }
usage() { sed -n '2,18p' "$0" | sed 's/^# \{0,1\}//'; }

USERNAME=""; TEMP_PW=""; KIEM_TRA=0
while [ $# -gt 0 ]; do
    case "$1" in
        -h|--help) usage; exit 0 ;;
        --kiem-tra) KIEM_TRA=1 ;;
        --mat-khau-tam)
            [ $# -ge 2 ] || die "Thiếu giá trị sau --mat-khau-tam. Xem: $0 --help"
            TEMP_PW="$2"; shift ;;
        -*) die "Tham số không nhận ra: $1. Xem: $0 --help" ;;
        *)
            [ -z "$USERNAME" ] || die "Chỉ đặt lại được một tài khoản mỗi lần chạy. Xem: $0 --help"
            USERNAME="$1" ;;
    esac
    shift
done
[ -n "$USERNAME" ] || { usage; exit 1; }

# Cùng quy tắc chuẩn hóa của ứng dụng: bỏ khoảng trắng hai đầu, chữ thường, chỉ a-z 0-9 . _ -
USERNAME="$(printf %s "$USERNAME" | tr '[:upper:]' '[:lower:]' | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//')"
[[ "$USERNAME" =~ ^[a-z0-9._-]{3,50}$ ]] \
    || die "Tên đăng nhập '$USERNAME' không hợp lệ: chỉ gồm chữ không dấu, số và các ký tự . _ - (3 đến 50 ký tự). Nhập lại đúng tên đăng nhập."

GENERATED=0
if [ -n "$TEMP_PW" ]; then
    PW_BYTES="$(printf %s "$TEMP_PW" | wc -c | tr -d ' ')"
    { [ "$PW_BYTES" -ge 8 ] && [ "$PW_BYTES" -le 72 ]; } \
        || die "Mật khẩu tạm phải dài từ 8 đến 72 byte. Chọn mật khẩu khác."
    [[ "$TEMP_PW" =~ [A-Za-z] && "$TEMP_PW" =~ [0-9] ]] \
        || die "Mật khẩu tạm phải có cả chữ và số. Chọn mật khẩu khác."
else
    # Bỏ ký tự dễ nhầm như ứng dụng; lặp tới khi có cả chữ và số.
    while :; do
        TEMP_PW="$(LC_ALL=C tr -dc 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789' < /dev/urandom | head -c 12 || true)"
        if [ "${#TEMP_PW}" -eq 12 ] && [[ "$TEMP_PW" =~ [A-Za-z] && "$TEMP_PW" =~ [0-9] ]]; then break; fi
    done
    GENERATED=1
fi

command -v docker >/dev/null || die "Chưa có lệnh docker."
docker inspect -f '{{.State.Running}}' "$PG_CONTAINER" 2>/dev/null | grep -q true \
    || die "Container PostgreSQL '$PG_CONTAINER' không chạy. Khởi động stack trước (hoặc đặt PG_CONTAINER)."

DB_NAME="${DB_NAME:-$(docker exec "$PG_CONTAINER" sh -c 'printf %s "$POSTGRES_DB"')}"
[ -n "$DB_NAME" ] || die "Không xác định được tên CSDL. Đặt DB_NAME."
[[ "$DB_NAME" =~ ^[A-Za-z0-9_]+$ ]] || die "Tên CSDL '$DB_NAME' có ký tự không hợp lệ."

pg() { docker exec -i "$PG_CONTAINER" sh -c 'exec psql -U "$POSTGRES_USER" -X -w -q -v ON_ERROR_STOP=1 "$@"' sh -d "$DB_NAME" "$@"; }

[ "$(pg -At -c "select to_regclass('public.party_member_profiles') is not null")" = "t" ] \
    || die "CSDL '$DB_NAME' không có bảng party_member_profiles: không phải CSDL của CongTacDang. Kiểm tra DB_NAME."

# Tên đăng nhập đã được kiểm theo mẫu hẹp ở trên nên đưa thẳng vào SQL là an toàn.
ROW="$(pg -At -F $'\t' -c "select \"Id\", \"FullName\", \"IsActive\", \"IsDeleted\", (\"LockoutEnd\" is not null and \"LockoutEnd\" > now()) from party_member_profiles where lower(\"Username\") = '$USERNAME'")"
[ -n "$ROW" ] || die "Không có tài khoản '$USERNAME' trong CSDL '$DB_NAME'. Kiểm tra lại tên đăng nhập; không có gì bị thay đổi."
IFS=$'\t' read -r USER_ID FULL_NAME IS_ACTIVE IS_DELETED LOCKED <<< "$ROW"
[ "$IS_DELETED" = "f" ] || die "Tài khoản '$USERNAME' đã bị xóa. Script không khôi phục tài khoản đã xóa; không có gì bị thay đổi."

echo "Tài khoản: $USERNAME ($FULL_NAME), mã $USER_ID"
[ "$IS_ACTIVE" = "t" ] || echo "[CẢNH BÁO] Tài khoản đang bị tắt: sau khi đặt lại vẫn chưa đăng nhập được. Script không bật lại tài khoản; dùng trang Hệ thống → Tài khoản của một quản trị khác."
[ "$LOCKED" = "f" ] || echo "Tài khoản đang bị khóa tạm do nhập sai; sẽ được mở khóa."

HAS_PGCRYPTO="$(pg -At -c "select exists(select 1 from pg_extension where extname='pgcrypto') or exists(select 1 from pg_available_extensions where name='pgcrypto')")"
[ "$HAS_PGCRYPTO" = "t" ] || die "PostgreSQL trong container thiếu tiện ích pgcrypto để băm mật khẩu. Dùng image postgres chuẩn (có gói contrib) hoặc đặt lại mật khẩu qua trang Hệ thống → Tài khoản."

if [ "$KIEM_TRA" -eq 1 ]; then
    echo "[--kiem-tra] Sẽ đặt mật khẩu tạm mới (băm BCrypt cost 10), bắt buộc đổi ở lần đăng nhập đầu, xóa đếm sai và khóa tạm,"
    echo "             đăng xuất mọi phiên của '$USERNAME' và ghi một dòng vào nhật ký. Chưa thay đổi gì."
    exit 0
fi

# Mật khẩu đi vào SQL dạng hex qua stdin: không lọt vào danh sách tiến trình và không cần thoát ký tự.
PW_HEX="$(printf %s "$TEMP_PW" | od -An -v -tx1 | tr -d ' \n')"

# Chỉ tạo pgcrypto khi chưa có và gỡ lại ngay sau khi băm, để không để lại thay đổi nào ngoài tài khoản này.
SQL_FILE="$(mktemp)"
trap 'rm -f -- "$SQL_FILE"' EXIT
cat > "$SQL_FILE" <<'SQL'
BEGIN;
SELECT exists(SELECT 1 FROM pg_extension WHERE extname = 'pgcrypto') AS had_ext \gset
CREATE EXTENSION IF NOT EXISTS pgcrypto;
WITH old AS (
    SELECT "Id", "MustChangePassword", "FailedLoginCount", "LockoutEnd"
    FROM party_member_profiles WHERE "Id" = :'id'::uuid AND NOT "IsDeleted"
), upd AS (
    UPDATE party_member_profiles p
       SET "PasswordHash" = crypt(convert_from(decode(:'pwhex', 'hex'), 'UTF8'), gen_salt('bf', 10)),
           "MustChangePassword" = true,
           "FailedLoginCount" = 0,
           "LockoutEnd" = NULL,
           "SecurityStamp" = replace(gen_random_uuid()::text, '-', ''),
           "UpdatedAt" = now()
      FROM old WHERE p."Id" = old."Id"
    RETURNING p."Id"
), rev AS (
    UPDATE refresh_tokens SET "IsRevoked" = true, "RevokedAt" = now()
     WHERE "UserId" IN (SELECT "Id" FROM upd) AND NOT "IsRevoked"
    RETURNING 1
), aud AS (
    INSERT INTO audit_logs ("Id", "ActorId", "ActorName", "Action", "EntityType", "EntityId", "OldValues", "NewValues",
                            "IpAddress", "UserAgent", "RequestPath", "CreatedAt")
    SELECT gen_random_uuid(), NULL, 'dat-lai-mat-khau.sh', 'Update', 'PartyMemberProfile', 'Id=' || old."Id",
           jsonb_build_object('MustChangePassword', old."MustChangePassword", 'FailedLoginCount', old."FailedLoginCount",
                              'LockoutEnd', old."LockoutEnd")::text,
           jsonb_build_object('PasswordHash', '(đã đặt lại)', 'SecurityStamp', '(đã đổi)', 'MustChangePassword', true,
                              'FailedLoginCount', 0, 'LockoutEnd', NULL)::text,
           NULL, 'dòng lệnh trên máy chủ', 'docker/dat-lai-mat-khau.sh', now()
    FROM old JOIN upd ON upd."Id" = old."Id"
    RETURNING 1
)
SELECT (SELECT count(*) FROM upd) || ' ' || (SELECT count(*) FROM aud);
\if :had_ext
\else
DROP EXTENSION pgcrypto;
\endif
COMMIT;
SQL

RESULT="$(pg -At -v id="$USER_ID" -v pwhex="$PW_HEX" < "$SQL_FILE")" \
    || die "Đặt lại mật khẩu không thành công; CSDL giữ nguyên. Xem thông báo phía trên rồi chạy lại."

[ "$RESULT" = "1 1" ] || die "Kết quả bất thường ('$RESULT'); không xác nhận được việc đặt lại mật khẩu. Chạy lại với --kiem-tra rồi báo nhóm phát triển."

log "Đã đặt lại mật khẩu cho '$USERNAME'. Các phiên đang mở bị đăng xuất trong vòng một phút."
if [ "$GENERATED" -eq 1 ]; then
    echo "Mật khẩu tạm (chỉ hiện lần này, hãy chép lại ngay): $TEMP_PW"
else
    echo "Mật khẩu tạm là giá trị bạn đã truyền."
fi
echo "Đăng nhập bằng mật khẩu này, hệ thống sẽ bắt đổi mật khẩu mới ngay."
