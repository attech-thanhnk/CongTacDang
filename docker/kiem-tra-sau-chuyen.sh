#!/usr/bin/env bash
# =============================================================================
# KIỂM TRA SAU KHI CHUYỂN DỮ LIỆU GỐC — CongTacDang
#
# Chạy sau docker/chuyen-du-lieu-goc.sh và sau khi backend đã khởi động lại trên CSDL đích.
# Kiểm bằng SQL và API, in ĐẠT / KHÔNG ĐẠT từng mục; thoát mã 1 nếu có mục không đạt.
#
# Cách dùng:
#   CTD_MAT_KHAU='<mật khẩu>' docker/kiem-tra-sau-chuyen.sh [tùy chọn]
#
# Tùy chọn:
#   --dich-csdl TEN     CSDL đích (mặc định: CSDL của container postgres, biến POSTGRES_DB)
#   --api-url URL       địa chỉ hệ thống để gọi API (mặc định http://127.0.0.1:3001; backend trực tiếp: http://127.0.0.1:5000)
#   --tai-khoan TEN     tên đăng nhập thử; mật khẩu lấy từ biến CTD_MAT_KHAU (hoặc --mat-khau, không khuyến nghị vì lộ ở dòng lệnh)
#   --repo-dir DIR      thư mục mã nguồn (mặc định: thư mục cha của docker/), dùng để so migration và danh sách vai trò mặc định
#   -h, --help          xem hướng dẫn
# Biến môi trường: PG_CONTAINER (mặc định attech-dangbo-postgres).
# =============================================================================
set -euo pipefail

export MSYS_NO_PATHCONV=1 MSYS2_ARG_CONV_EXCL='*'
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONT="${PG_CONTAINER:-attech-dangbo-postgres}"
LIST_FILE="$SCRIPT_DIR/du-lieu-goc.txt"
REPO_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
API_URL="http://127.0.0.1:3001"
TARGET_DB=""
ACCOUNT=""
PASSWORD="${CTD_MAT_KHAU:-}"

usage() { sed -n '2,20p' "$0" | sed 's/^# \{0,1\}//'; }
die() { echo "[LỖI] $*" >&2; exit 1; }

while [ $# -gt 0 ]; do
    case "$1" in
        --dich-csdl) TARGET_DB="${2:?Thiếu giá trị cho --dich-csdl}"; shift ;;
        --api-url) API_URL="${2:?Thiếu giá trị cho --api-url}"; shift ;;
        --tai-khoan) ACCOUNT="${2:?Thiếu giá trị cho --tai-khoan}"; shift ;;
        --mat-khau) PASSWORD="${2:?Thiếu giá trị cho --mat-khau}"; shift ;;
        --repo-dir) REPO_DIR="${2:?Thiếu giá trị cho --repo-dir}"; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "[LỖI] Tham số không hợp lệ: $1" >&2; usage >&2; exit 2 ;;
    esac
    shift
done

command -v docker >/dev/null 2>&1 || die "Không tìm thấy lệnh docker."
[ "$(docker inspect -f '{{.State.Running}}' "$CONT" 2>/dev/null || true)" = "true" ] || die "Container postgres '$CONT' không chạy (hoặc đặt PG_CONTAINER)."
[ -f "$LIST_FILE" ] || die "Không tìm thấy $LIST_FILE."
[ -n "$TARGET_DB" ] || TARGET_DB="$(docker exec "$CONT" sh -c 'printf %s "$POSTGRES_DB"')"
[[ "$TARGET_DB" =~ ^[a-z][a-z0-9_]{0,62}$ ]] || die "Tên CSDL đích '$TARGET_DB' không hợp lệ."

q() { docker exec -i "$CONT" sh -c 'exec psql -U "$POSTGRES_USER" -X -w -q -tA -v ON_ERROR_STOP=1 "$@"' sh -d "$TARGET_DB" -c "$1"; }

FAIL=0
ok() { echo "[ĐẠT]       $*"; }
ko() { echo "[KHÔNG ĐẠT] $*"; FAIL=$((FAIL + 1)); }
skip() { echo "[CHƯA KIỂM] $*"; }

GOC=(); VH=()
while IFS='|' read -r loai bang _; do
    bang="${bang%$'\r'}"
    case "$loai" in GOC) GOC+=("$bang") ;; VAN_HANH) VH+=("$bang") ;; esac
done < "$LIST_FILE"

echo "Kiểm tra CSDL $TARGET_DB, hệ thống $API_URL"
echo

# 1. Dấu đã chuyển và số dòng bảng gốc.
MARK="$(q "SELECT coalesce(shobj_description(oid,'pg_database'),'') FROM pg_database WHERE datname='$TARGET_DB'")"
case "$MARK" in
    chuyen-du-lieu-goc*)
        ok "Đích đã nhận dữ liệu gốc (${MARK%% counts=*})"
        COUNTS="${MARK##* counts=}"
        BAD=""
        IFS=',' read -ra PAIRS <<< "$COUNTS"
        for pair in "${PAIRS[@]}"; do
            t="${pair%%:*}"; expected="${pair##*:}"
            actual="$(q "SELECT count(*) FROM \"$t\"")"
            case "$t" in
                permissions|roles|role_permissions) # seeder bổ sung mã quyền, vai trò, quyền mới khi khởi động lại
                    [ "$actual" -ge "$expected" ] || BAD+="$t (đích $actual < nguồn $expected); " ;;
                *) [ "$actual" -eq "$expected" ] || BAD+="$t (đích $actual, nguồn $expected); " ;;
            esac
        done
        if [ -z "$BAD" ]; then ok "Đủ số dòng ${#PAIRS[@]} bảng gốc so với lúc chuyển"
        else ko "Số dòng bảng gốc lệch: $BAD Xem lại bằng chuyen-du-lieu-goc.sh --kiem-tra hoặc dựng lại đích rồi chuyển lại."; fi ;;
    *) ko "Đích chưa có dấu đã chuyển dữ liệu gốc. Chạy docker/chuyen-du-lieu-goc.sh trước." ;;
esac

# 2. Không còn dữ liệu vận hành.
BAD=""
for t in "${VH[@]}"; do
    case "$t" in audit_logs|login_events|refresh_tokens) continue ;; esac # có thêm khi khởi động và đăng nhập
    n="$(q "SELECT count(*) FROM \"$t\"")"
    [ "$n" = "0" ] || BAD+="$t=$n "
done
NP="$(q 'SELECT count(*) FROM evaluation_periods')"; NR="$(q 'SELECT count(*) FROM evaluation_records')"
if [ -z "$BAD" ]; then ok "Không có dữ liệu vận hành (kỳ = $NP, hồ sơ = $NR, các bảng vận hành khác = 0)"
else ko "Còn dữ liệu vận hành: $BAD. Đích phải là CSDL mới, dựng lại đích sạch rồi chuyển lại."; fi

# 3. Migration.
MIG_DIR="$REPO_DIR/backend/src/CongTacDang.Infrastructure/Data/Migrations"
if [ -d "$MIG_DIR" ]; then
    q 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1' | sort > "${TMPDIR:-/tmp}/ctd_mig_dich.$$"
    ls "$MIG_DIR" | grep -E '^[0-9]+_.*\.cs$' | grep -v '\.Designer\.cs$' | sed 's/\.cs$//' | sort > "${TMPDIR:-/tmp}/ctd_mig_code.$$"
    MISSING="$(comm -13 "${TMPDIR:-/tmp}/ctd_mig_dich.$$" "${TMPDIR:-/tmp}/ctd_mig_code.$$" | tr '\n' ' ')"
    rm -f "${TMPDIR:-/tmp}/ctd_mig_dich.$$" "${TMPDIR:-/tmp}/ctd_mig_code.$$"
    if [ -z "$MISSING" ]; then ok "Không thiếu migration so với mã nguồn"
    else ko "Thiếu migration: $MISSING Khởi động backend với Database__AutoMigrate=true hoặc chạy dotnet ef database update."; fi
else
    skip "Không thấy thư mục migration của mã nguồn ($MIG_DIR); dùng --repo-dir"
fi

# 4. Backend.
HEALTH=""
for path in /healthz /api/healthz; do
    body="$(curl -s -m 10 "$API_URL$path" 2>/dev/null || true)"
    if [ "$body" = "Healthy" ]; then HEALTH="$path"; break; fi
done
if [ -n "$HEALTH" ]; then ok "Backend khỏe ($API_URL$HEALTH trả Healthy)"
else ko "Backend không trả Healthy tại $API_URL/healthz hoặc $API_URL/api/healthz. Khởi động backend trên đích rồi xem log."; fi

# 5. Đăng nhập bằng mật khẩu băm đã chuyển.
if [ -n "$ACCOUNT" ] && [ -n "$PASSWORD" ]; then
    code="$(printf '{"username":"%s","password":"%s"}' "$ACCOUNT" "$PASSWORD" \
        | curl -s -m 20 -o /dev/null -w '%{http_code}' -H 'Content-Type: application/json' --data-binary @- "$API_URL/api/auth/login" 2>/dev/null || true)"
    if [ "$code" = "200" ]; then ok "Đăng nhập được tài khoản $ACCOUNT bằng mật khẩu thử"
    else ko "Đăng nhập tài khoản $ACCOUNT không thành công (mã HTTP ${code:-không phản hồi}). Kiểm tra tên đăng nhập, mật khẩu (biến CTD_MAT_KHAU), giới hạn tần suất đăng nhập, backend đã khởi động."; fi
else
    skip "Đăng nhập: chưa cung cấp --tai-khoan và biến CTD_MAT_KHAU"
fi

# 6. Vai trò mặc định (đọc mã từ DataSeeder của mã nguồn) và quyền mới của Văn phòng Đảng ủy.
SEEDER="$REPO_DIR/backend/src/CongTacDang.Infrastructure/Data/DataSeeder.cs"
ROLE_CODES=()
if [ -f "$SEEDER" ]; then
    while IFS= read -r c; do ROLE_CODES+=("$c"); done < <(sed -n '/static class RoleCodes/,/^    }/p' "$SEEDER" | grep -oE 'const string [A-Za-z]+ = "[A-Z_]+"' | grep -oE '"[A-Z_]+"' | tr -d '"')
fi
[ "${#ROLE_CODES[@]}" -gt 0 ] || ROLE_CODES=(CO_QUAN_CHUYEN_MON VAN_PHONG_DANG_UY)
MISSING=""
for c in "${ROLE_CODES[@]}"; do
    [ "$(q "SELECT count(*) FROM roles WHERE \"Code\"='$c' AND NOT \"IsDeleted\"")" = "1" ] || MISSING+="$c "
done
if [ -z "$MISSING" ]; then ok "Có đủ ${#ROLE_CODES[@]} vai trò mặc định (gồm Cơ quan chuyên môn)"
else ko "Thiếu vai trò mặc định: $MISSING Khởi động lại backend để seeder bổ sung (vai trò bị xóa mềm thì khôi phục trên giao diện)."; fi

MISSING=""
for p in evaluation.crosscheck.confirm evaluation.control.confirm evaluation.calibration.conclude; do
    n="$(q "SELECT count(*) FROM role_permissions rp JOIN roles r ON r.\"Id\"=rp.role_id JOIN permissions p ON p.\"Id\"=rp.permission_id WHERE r.\"Code\"='VAN_PHONG_DANG_UY' AND p.\"Code\"='$p'")"
    [ "$n" = "1" ] || MISSING+="$p "
done
if [ -z "$MISSING" ]; then ok "Vai trò Văn phòng Đảng ủy có đủ quyền mới"
else ko "Văn phòng Đảng ủy thiếu quyền: $MISSING Khởi động lại backend; nếu mã quyền đã có từ trước, gán quyền cho vai trò trên giao diện Hệ thống > Vai trò."; fi

# 7. Mã quyền của mã nguồn đều có trong danh mục.
PERM_FILE="$REPO_DIR/backend/src/CongTacDang.Application/Common/Security/PermissionCodes.cs"
if [ -f "$PERM_FILE" ]; then
    MISSING=""
    for p in $(grep -oE 'public const string [A-Za-z]+ = "[a-z]+(\.[a-z]+)+"' "$PERM_FILE" | grep -oE '"[a-z.]+"' | tr -d '"'); do
        [ "$(q "SELECT count(*) FROM permissions WHERE \"Code\"='$p'")" = "1" ] || MISSING+="$p "
    done
    if [ -z "$MISSING" ]; then ok "Danh mục quyền có đủ mã quyền của mã nguồn"
    else ko "Thiếu mã quyền: $MISSING Khởi động lại backend để seeder bổ sung."; fi
fi

echo
if [ "$FAIL" -eq 0 ]; then echo "KẾT LUẬN: ĐẠT"; else echo "KẾT LUẬN: KHÔNG ĐẠT ($FAIL mục)"; exit 1; fi
