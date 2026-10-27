#!/usr/bin/env bash
# =============================================================================
# CHUYỂN DỮ LIỆU GỐC TỪ MÁY CŨ SANG CSDL MỚI, SẠCH — CongTacDang
#
# Dựng CSDL đích bằng bản mới (backend khởi động một lần trên CSDL trống để tạo đủ schema) rồi chỉ chép
# "dữ liệu gốc" (tài khoản, vai trò, quyền, bản gán, cây đơn vị, chức vụ, thông tin đơn vị, bộ tiêu chí, ...).
# Dữ liệu vận hành (kỳ, hồ sơ, nhật ký, tệp đính kèm, ...) KHÔNG chép. Phân loại bảng: docker/du-lieu-goc.txt.
#
# Cách dùng:
#   docker/chuyen-du-lieu-goc.sh [tùy chọn] <thư-mục-bản-sao-lưu | đường-dẫn-db.dump>
#   docker/chuyen-du-lieu-goc.sh [tùy chọn] --nguon-csdl TEN
#
# Tùy chọn:
#   --kiem-tra            chạy thử toàn bộ (kể cả chép dữ liệu) rồi hoàn tác, không ghi gì vào CSDL đích
#   --dich-csdl TEN       CSDL đích (mặc định: CSDL của container postgres, biến POSTGRES_DB)
#   --nguon-csdl TEN      nguồn là CSDL có sẵn trong cùng container (chỉ đọc), thay cho bản dump
#   --uploads FILE        uploads.tar.gz của máy cũ (mặc định: uploads.tar.gz cạnh db.dump)
#   --thu-muc-upload DIR  giải nén file mẫu Word vào thư mục này (thay cho volume upload của docker)
#   --volume-upload TEN   volume upload của backend (mặc định attech-dangbo-backend-uploads)
#   --repo-dir DIR        thư mục mã nguồn để so sánh migration (mặc định: thư mục cha của docker/)
#   --giu-csdl-tam        không xóa CSDL tạm ctd_nguon_tam khi xong (để gỡ lỗi)
#   -h, --help            xem hướng dẫn
# Biến môi trường: PG_CONTAINER (mặc định attech-dangbo-postgres), HELPER_IMAGE, APP_UID.
# Mật khẩu CSDL không đi qua dòng lệnh: psql/pg_restore chạy trong container postgres bằng biến môi trường của container.
#
# Quy trình: kiểm tra điều kiện -> khôi phục dump vào CSDL tạm ctd_nguon_tam -> so sánh cột từng bảng gốc
#   -> MỘT transaction trên đích (làm rỗng bảng gốc đã seed, chép cột chung, vá dữ liệu theo migration nguồn còn thiếu,
#   kiểm số dòng, đặt lại sequence, ANALYZE, ghi dấu đã chuyển) -> đối chiếu số dòng -> giải nén file mẫu Word.
# Sau đó khởi động lại backend: seeder tự bổ sung mã quyền mới, vai trò mặc định mới và quyền mới của vai trò mặc định.
# =============================================================================
set -euo pipefail

# Git Bash trên Windows không đổi đường dẫn đối số của docker.
export MSYS_NO_PATHCONV=1 MSYS2_ARG_CONV_EXCL='*'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONT="${PG_CONTAINER:-attech-dangbo-postgres}"
HELPER_IMAGE="${HELPER_IMAGE:-alpine:3.20}"
APP_UID="${APP_UID:-10001}"
LIST_FILE="$SCRIPT_DIR/du-lieu-goc.txt"
REPO_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
TEMP_DB="ctd_nguon_tam"
UPLOAD_VOLUME="attech-dangbo-backend-uploads"
UPLOAD_DIR=""
UPLOADS_FILE=""
SOURCE_ARG=""
SOURCE_DB=""
TARGET_DB=""
DRY=0
KEEP_TEMP=0
COMMITTED=0

usage() { sed -n '2,32p' "$0" | sed 's/^# \{0,1\}//'; }
log() { echo "[$(date '+%H:%M:%S')] $*"; }
die() { echo "[LỖI] $*" >&2; exit 1; }

while [ $# -gt 0 ]; do
    case "$1" in
        --kiem-tra) DRY=1 ;;
        --dich-csdl) TARGET_DB="${2:?Thiếu giá trị cho --dich-csdl}"; shift ;;
        --nguon-csdl) SOURCE_DB="${2:?Thiếu giá trị cho --nguon-csdl}"; shift ;;
        --uploads) UPLOADS_FILE="${2:?Thiếu giá trị cho --uploads}"; shift ;;
        --thu-muc-upload) UPLOAD_DIR="${2:?Thiếu giá trị cho --thu-muc-upload}"; shift ;;
        --volume-upload) UPLOAD_VOLUME="${2:?Thiếu giá trị cho --volume-upload}"; shift ;;
        --repo-dir) REPO_DIR="${2:?Thiếu giá trị cho --repo-dir}"; shift ;;
        --giu-csdl-tam) KEEP_TEMP=1 ;;
        -h|--help) usage; exit 0 ;;
        -*) echo "[LỖI] Tham số không hợp lệ: $1" >&2; usage >&2; exit 2 ;;
        *) [ -z "$SOURCE_ARG" ] || die "Chỉ được chỉ định một nguồn."; SOURCE_ARG="$1" ;;
    esac
    shift
done

[ -f "$LIST_FILE" ] || die "Không tìm thấy $LIST_FILE. Chạy script từ bản mã nguồn đầy đủ."
if [ -n "$SOURCE_ARG" ] && [ -n "$SOURCE_DB" ]; then die "Chỉ dùng một trong hai: thư mục/dump hoặc --nguon-csdl."; fi
if [ -z "$SOURCE_ARG" ] && [ -z "$SOURCE_DB" ]; then usage >&2; exit 2; fi
command -v docker >/dev/null 2>&1 || die "Không tìm thấy lệnh docker. Cài Docker hoặc chạy trên máy chủ có Docker."
[ "$(docker inspect -f '{{.State.Running}}' "$CONT" 2>/dev/null || true)" = "true" ] \
    || die "Container postgres '$CONT' không chạy. Khởi động: docker compose --env-file docker/.env -f docker/docker-compose.yml up -d postgres (hoặc đặt PG_CONTAINER)."

# --- Truy cập postgres trong container ---------------------------------------------------------------------------
# pg DB [đối số psql...]: ghi được. pgs: cùng cách nhưng phiên chỉ đọc, dùng cho nguồn.
pg() { docker exec -i -e PGOPTIONS="${PGOPT:-}" "$CONT" sh -c 'exec psql -U "$POSTGRES_USER" -X -w -q -v ON_ERROR_STOP=1 "$@"' sh "$@"; }
pgs() { PGOPT='-c default_transaction_read_only=on' pg "$@"; }
pgval() { local db="$1"; shift; pg -d "$db" -tA -F '|' -c "$*"; }
pgsval() { local db="$1"; shift; pgs -d "$db" -tA -F '|' -c "$*"; }
if [ -z "${TARGET_DB}" ]; then
    TARGET_DB="$(docker exec "$CONT" sh -c 'printf %s "$POSTGRES_DB"')"
fi
valid_name() { [[ "$1" =~ ^[a-z][a-z0-9_]{0,62}$ ]]; }
valid_name "$TARGET_DB" || die "Tên CSDL đích '$TARGET_DB' không hợp lệ (chỉ chữ thường, số, gạch dưới)."
[ -z "$SOURCE_DB" ] || valid_name "$SOURCE_DB" || die "Tên CSDL nguồn '$SOURCE_DB' không hợp lệ."
case "$TARGET_DB" in postgres|template0|template1) die "Không dùng CSDL hệ thống '$TARGET_DB' làm đích." ;; esac
[ "$TARGET_DB" != "$SOURCE_DB" ] || die "Nguồn và đích là cùng một CSDL."
[ "$TARGET_DB" != "$TEMP_DB" ] || die "Đích không được trùng CSDL tạm $TEMP_DB."

db_exists() { [ "$(pgval postgres "SELECT 1 FROM pg_database WHERE datname='$1'")" = "1" ]; }
db_exists "$TARGET_DB" || die "CSDL đích '$TARGET_DB' chưa có. Khởi động backend (Database__AutoMigrate=true) một lần trên CSDL trống để tạo schema rồi dừng backend và chạy lại."

TMP="$(mktemp -d)"
chmod 700 "$TMP"
TEMP_CREATED=0
cleanup() {
    local rc=$?
    rm -rf -- "$TMP"
    if [ "$TEMP_CREATED" -eq 1 ] && [ "$KEEP_TEMP" -eq 0 ]; then
        pg postgres -c "DROP DATABASE IF EXISTS $TEMP_DB WITH (FORCE)" >/dev/null 2>&1 || \
            echo "[CẢNH BÁO] Chưa xóa được CSDL tạm $TEMP_DB. Xóa tay: docker exec $CONT sh -c 'psql -U \"\$POSTGRES_USER\" -d postgres -c \"DROP DATABASE $TEMP_DB\"'" >&2
    fi
    if [ "$rc" -ne 0 ]; then
        if [ "$COMMITTED" -eq 1 ]; then echo "[LỖI] Dữ liệu CSDL đã ghi vào đích nhưng bước sau đó không thành công. Làm theo hướng dẫn phía trên; chưa khởi động backend cho tới khi xử lý xong." >&2
        else echo "[LỖI] Chuyển dữ liệu không thành công, CSDL đích giữ nguyên. Xem thông báo phía trên, sửa rồi chạy lại." >&2; fi
    fi
    return "$rc"
}
trap cleanup EXIT

# --- Danh sách phân loại bảng ---------------------------------------------------------------------------------------
GOC=(); VH=(); HT=()
while IFS='|' read -r loai bang _; do
    bang="${bang%$'\r'}"; loai="${loai%$'\r'}"
    case "$loai" in
        GOC) GOC+=("$bang") ;;
        VAN_HANH) VH+=("$bang") ;;
        HE_THONG) HT+=("$bang") ;;
        ''|'#'*) ;;
        *) die "Dòng không hợp lệ trong du-lieu-goc.txt: loại '$loai'." ;;
    esac
done < "$LIST_FILE"
[ "${#GOC[@]}" -gt 0 ] || die "du-lieu-goc.txt không có bảng GOC."
in_list() { local x="$1"; shift; local y; for y in "$@"; do [ "$x" = "$y" ] && return 0; done; return 1; }
sql_in_list() { local out="" t; for t in "$@"; do out+="'$t',"; done; printf '%s' "${out%,}"; }

# --- 1. Nguồn -----------------------------------------------------------------------------------------------------
if [ -n "$SOURCE_ARG" ]; then
    [ -e "$SOURCE_ARG" ] || die "Không tìm thấy $SOURCE_ARG. Chỉ đúng thư mục do docker/backup.sh sinh ra (chứa db.dump) hoặc đường dẫn db.dump."
    if [ -d "$SOURCE_ARG" ]; then
        SRC_DIR="$(cd "$SOURCE_ARG" && pwd)"; DUMP="$SRC_DIR/db.dump"
    else
        DUMP="$SOURCE_ARG"; SRC_DIR="$(cd "$(dirname "$DUMP")" && pwd)"
    fi
    [ -f "$DUMP" ] || die "Không tìm thấy bản dump: $DUMP. Chỉ đúng thư mục do docker/backup.sh sinh ra hoặc đường dẫn db.dump."
    [ -n "$UPLOADS_FILE" ] || { [ -f "$SRC_DIR/uploads.tar.gz" ] && UPLOADS_FILE="$SRC_DIR/uploads.tar.gz" || true; }
    log "[1/7] Kiểm tra bản sao lưu $SRC_DIR"
    if [ -f "$SRC_DIR/SHA256SUMS" ]; then
        (cd "$SRC_DIR" && grep -E '[ *](db\.dump|uploads\.tar\.gz)$' SHA256SUMS | sha256sum -c -) \
            || die "Checksum bản sao lưu không khớp: bản sao lưu hỏng hoặc bị sửa. Chép lại bản sao lưu từ máy cũ rồi chạy lại."
    else
        echo "[CẢNH BÁO] Không có SHA256SUMS, bỏ qua kiểm tra checksum."
    fi
    docker exec -i "$CONT" pg_restore --list < "$DUMP" > /dev/null \
        || die "Không đọc được $DUMP bằng pg_restore: tệp không phải bản dump của backup.sh hoặc bị hỏng. Chép lại bản sao lưu."
    log "  Khôi phục vào CSDL tạm $TEMP_DB"
    pg postgres -c "DROP DATABASE IF EXISTS $TEMP_DB WITH (FORCE)" >/dev/null
    pg postgres -c "CREATE DATABASE $TEMP_DB TEMPLATE template0" >/dev/null
    TEMP_CREATED=1
    docker exec -i "$CONT" sh -c 'pg_restore -U "$POSTGRES_USER" -d '"$TEMP_DB"' --no-owner --no-privileges --no-password --exit-on-error' < "$DUMP" \
        || die "Khôi phục bản dump vào $TEMP_DB không thành công. Kiểm tra bản dump còn nguyên và phiên bản PostgreSQL của container không thấp hơn máy cũ."
    SOURCE_DB="$TEMP_DB"
else
    log "[1/7] Nguồn là CSDL '$SOURCE_DB' có sẵn trong container (chỉ đọc)"
    db_exists "$SOURCE_DB" || die "Không có CSDL nguồn '$SOURCE_DB' trong container."
fi
[ "$(pgsval "$SOURCE_DB" "SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL")" = "t" ] \
    || die "Nguồn không có bảng __EFMigrationsHistory: không phải CSDL của CongTacDang."

# --- 2. Kiểm tra đích ---------------------------------------------------------------------------------------------
log "[2/7] Kiểm tra CSDL đích $TARGET_DB"
PROBLEMS=0
problem() { echo "  [LỖI] $*" >&2; PROBLEMS=$((PROBLEMS + 1)); }

pgsval "$SOURCE_DB" 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1' > "$TMP/mig_nguon.txt"
pgval "$TARGET_DB" 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1' > "$TMP/mig_dich.txt"
MIG_DIR="$REPO_DIR/backend/src/CongTacDang.Infrastructure/Data/Migrations"
if [ -d "$MIG_DIR" ]; then
    ls "$MIG_DIR" | grep -E '^[0-9]+_.*\.cs$' | grep -v '\.Designer\.cs$' | sed 's/\.cs$//' | sort > "$TMP/mig_code.txt"
    MISSING_IN_TARGET="$(comm -13 "$TMP/mig_dich.txt" "$TMP/mig_code.txt" | tr '\n' ' ')"
    [ -z "$MISSING_IN_TARGET" ] || problem "Đích thiếu migration của mã nguồn: $MISSING_IN_TARGET. Khởi động backend (Database__AutoMigrate=true) trên đích để áp dụng đủ migration rồi dừng backend."
else
    echo "  [CẢNH BÁO] Không thấy thư mục migration của mã nguồn ($MIG_DIR); bỏ qua so sánh với mã nguồn (dùng --repo-dir)."
fi
NEWER_IN_SOURCE="$(comm -23 "$TMP/mig_nguon.txt" "$TMP/mig_dich.txt" | tr '\n' ' ')"
[ -z "$NEWER_IN_SOURCE" ] || problem "Nguồn MỚI hơn đích (migration đích không có: $NEWER_IN_SOURCE). Triển khai bản mã nguồn mới hơn lên máy đích rồi chạy lại."
PENDING_FOR_SOURCE="$(comm -13 "$TMP/mig_nguon.txt" "$TMP/mig_dich.txt" | tr '\n' ' ')"
echo "  Nguồn: $(wc -l < "$TMP/mig_nguon.txt" | tr -d ' ') migration, mới nhất $(tail -n 1 "$TMP/mig_nguon.txt"); đích: $(wc -l < "$TMP/mig_dich.txt" | tr -d ' ') migration."
source_lacks() { ! grep -qx "$1" "$TMP/mig_nguon.txt"; }

MARK="$(pgval "$TARGET_DB" "SELECT coalesce(shobj_description(oid,'pg_database'),'') FROM pg_database WHERE datname='$TARGET_DB'")"
case "$MARK" in
    chuyen-du-lieu-goc*) problem "Đích đã nhận dữ liệu gốc trước đó (${MARK%% counts=*}). Không chuyển lại lên đích đang dùng vì sẽ ghi đè thay đổi sau đó (đổi mật khẩu, gán vai trò...). Cần chuyển lại: xóa CSDL đích, dựng đích trống mới (backend khởi động một lần) rồi chạy lại." ;;
esac

# Bảng trong đích mà danh sách chưa phân loại: có thể là bảng mới của bản mã nguồn mới hơn script.
pgval "$TARGET_DB" "SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY 1" > "$TMP/bang_dich.txt"
for t in $(cat "$TMP/bang_dich.txt"); do
    in_list "$t" "${GOC[@]}" "${VH[@]}" "${HT[@]}" || problem "Bảng '$t' của đích chưa được phân loại trong docker/du-lieu-goc.txt. Thêm một dòng GOC/VAN_HANH rồi chạy lại."
done
for t in "${GOC[@]}" "${VH[@]}"; do
    grep -qx "$t" "$TMP/bang_dich.txt" || problem "Đích thiếu bảng '$t'. Đích chưa migrate đủ hoặc du-lieu-goc.txt lệch mã nguồn."
done

# Dữ liệu vận hành phải trống; riêng nhật ký, lịch sử đăng nhập và phiên chỉ có do seeder/đăng nhập thử nên được dọn.
CLEARABLE=(audit_logs login_events refresh_tokens)
for t in "${VH[@]}"; do
    in_list "$t" "${CLEARABLE[@]}" && continue
    grep -qx "$t" "$TMP/bang_dich.txt" || continue
    n="$(pgval "$TARGET_DB" "SELECT count(*) FROM \"$t\"")"
    [ "$n" = "0" ] || problem "Đích có dữ liệu vận hành: bảng $t có $n dòng. Đích phải là CSDL mới, sạch; dựng lại đích trống rồi chạy lại."
done
NU="$(pgval "$TARGET_DB" 'SELECT count(*) FROM party_member_profiles')"
NA="$(pgval "$TARGET_DB" 'SELECT count(*) FROM user_role_assignments')"
{ [ "$NU" -le 1 ] && [ "$NA" -le 1 ]; } || problem "Đích đã có $NU tài khoản và $NA bản gán vai trò (tối đa 1 tài khoản quản trị khởi tạo). Dựng lại đích trống rồi chạy lại."

OTHERS="$(pgval postgres "SELECT count(*) FROM pg_stat_activity WHERE datname='$TARGET_DB' AND pid <> pg_backend_pid() AND backend_type='client backend'")"
if [ "$OTHERS" != "0" ]; then
    if [ "$DRY" -eq 1 ]; then
        echo "  [CẢNH BÁO] Có $OTHERS kết nối khác vào $TARGET_DB (backend đang chạy?). Khi chuyển thật phải dừng backend: docker compose --env-file docker/.env -f docker/docker-compose.yml stop backend frontend."
    else
        problem "Có $OTHERS kết nối khác đang vào $TARGET_DB (backend còn chạy?). Dừng backend trước: docker compose --env-file docker/.env -f docker/docker-compose.yml stop backend frontend, rồi chạy lại."
    fi
fi
[ "$PROBLEMS" -eq 0 ] || { echo "[LỖI] Đích chưa đủ điều kiện ($PROBLEMS vấn đề ở trên). Chưa thay đổi gì." >&2; exit 1; }

# --- 3. So sánh cột -----------------------------------------------------------------------------------------------
log "[3/7] So sánh cột nguồn và đích của bảng gốc"
LIST_SQL="$(sql_in_list "${GOC[@]}")"
COLQ="SELECT table_name, column_name, udt_name, is_nullable, (column_default IS NOT NULL OR is_identity='YES' OR is_generated<>'NEVER')::text
      FROM information_schema.columns WHERE table_schema='public' AND table_name IN ($LIST_SQL) ORDER BY table_name, ordinal_position"
pgsval "$SOURCE_DB" "$COLQ" > "$TMP/cot_nguon.txt"
pgval "$TARGET_DB" "$COLQ" > "$TMP/cot_dich.txt"

# Giá trị cho cột chỉ có ở đích (NOT NULL, không mặc định). Mặc định trống: gặp cột như vậy là dừng, không tự bịa giá trị.
declare -A DEFAULT_VALUE=()
# Cột nguồn chép nguyên văn; riêng criteria_sets.SourceSetId trỏ về bộ bị lọc thì đặt NULL.
declare -A COL_EXPR=()
declare -A ROW_FILTER=()
if source_lacks "20261023095959_BoChamTheoTruc09B"; then
    # Migration BoChamTheoTruc09B xóa bộ tiêu chí chấm theo trục (09B) và bản ghi file mẫu MAU_09B; nguồn cũ phải bị lọc tương tự.
    if grep -q '^criteria_sets|SelfScoreForm|' "$TMP/cot_nguon.txt"; then
        ROW_FILTER[criteria_sets]="\"SelfScoreForm\" <> '09B'"
        COL_EXPR[criteria_sets.SourceSetId]="CASE WHEN \"SourceSetId\" IN (SELECT \"Id\" FROM criteria_sets WHERE \"SelfScoreForm\" = '09B') THEN NULL ELSE \"SourceSetId\" END"
    fi
    ROW_FILTER[word_template_versions]="\"TemplateCode\" <> 'MAU_09B'"
fi

declare -A SRC_HAS=() COLS=() SELECT_LIST=() N_SRC=() N_COPY=()
SKIPPED=(); COPIED=()
for t in "${GOC[@]}"; do
    awk -F'|' -v t="$t" '$1==t' "$TMP/cot_nguon.txt" > "$TMP/c_s.txt"
    awk -F'|' -v t="$t" '$1==t' "$TMP/cot_dich.txt" > "$TMP/c_d.txt"
    if [ ! -s "$TMP/c_s.txt" ]; then
        SKIPPED+=("$t"); echo "  [BỎ QUA] Nguồn không có bảng $t; giữ nguyên dữ liệu mặc định của đích."
        continue
    fi
    cols=(); sel=(); only_src=""; only_dst=""
    while IFS='|' read -r _ col udt nullable hasdef; do
        s_line="$(awk -F'|' -v c="$col" '$2==c' "$TMP/c_s.txt")"
        if [ -z "$s_line" ]; then
            if [ -n "${DEFAULT_VALUE[$t.$col]:-}" ]; then
                cols+=("\"$col\""); sel+=("${DEFAULT_VALUE[$t.$col]} AS \"$col\"")
            elif [ "$nullable" = "NO" ] && [ "$hasdef" = "false" ]; then
                problem "Bảng $t: cột $col chỉ có ở đích, NOT NULL và không có giá trị mặc định. Khai báo giá trị mặc định có lý do trong DEFAULT_VALUE của script này hoặc thêm mặc định ở migration."
            else
                only_dst+="$col "
            fi
            continue
        fi
        s_udt="$(printf '%s' "$s_line" | cut -d'|' -f3)"
        if [ "$s_udt" != "$udt" ]; then
            problem "Bảng $t: cột $col khác kiểu (nguồn $s_udt, đích $udt). Cần viết phép đổi kiểu riêng trong COL_EXPR."
            continue
        fi
        cols+=("\"$col\"")
        sel+=("${COL_EXPR[$t.$col]:-\"$col\"}")
    done < "$TMP/c_d.txt"
    while IFS='|' read -r _ col _; do
        awk -F'|' -v c="$col" '$2==c' "$TMP/c_d.txt" | grep -q . || only_src+="$col "
    done < "$TMP/c_s.txt"
    [ -z "$only_dst" ] || echo "  $t: cột chỉ có ở đích (lấy mặc định): $only_dst"
    [ -z "$only_src" ] || echo "  $t: cột chỉ có ở nguồn (bỏ): $only_src"
    COLS[$t]="$(IFS=,; echo "${cols[*]}")"
    SELECT_LIST[$t]="$(IFS=,; echo "${sel[*]}")"
    COPIED+=("$t")
done
[ "$PROBLEMS" -eq 0 ] || { echo "[LỖI] Cột nguồn và đích chưa khớp ($PROBLEMS vấn đề ở trên). Chưa thay đổi gì." >&2; exit 1; }

# --- 4. Xuất dữ liệu nguồn ------------------------------------------------------------------------------------------
log "[4/7] Đọc dữ liệu gốc từ nguồn"
for t in "${COPIED[@]}"; do
    where=""; [ -z "${ROW_FILTER[$t]:-}" ] || where=" WHERE ${ROW_FILTER[$t]}"
    N_SRC[$t]="$(pgsval "$SOURCE_DB" "SELECT count(*) FROM \"$t\"")"
    N_COPY[$t]="$(pgsval "$SOURCE_DB" "SELECT count(*) FROM \"$t\"$where")"
    pgs -d "$SOURCE_DB" -c "COPY (SELECT ${SELECT_LIST[$t]} FROM \"$t\"$where) TO STDOUT" > "$TMP/$t.copy"
done

# --- 5. Dựng và chạy script SQL một transaction -----------------------------------------------------------------------
# Vá dữ liệu: việc các migration mới hơn nguồn làm trên dữ liệu gốc; nguồn đã có migration thì bỏ qua.
declare -A PATCH_SQL=()
PATCH_SQL[20261023020159_AddPositionIsHead]="UPDATE positions SET \"IsHead\" = TRUE WHERE NOT \"IsDeleted\" AND \"Name\" IN (
 'Bí thư Đảng ủy Tổng công ty', 'Bí thư Đảng ủy', 'Bí thư Chi bộ trực thuộc Đảng ủy bộ phận Văn phòng Tổng công ty',
 'Chủ tịch Công ty', 'Giám đốc', 'Bí thư Đảng bộ bộ phận', 'Bí thư Chi bộ', 'Bí thư Chi bộ trực thuộc Đảng bộ bộ phận',
 'Trưởng phòng', 'Trưởng trung tâm', 'Quản đốc', 'Giám đốc đơn vị trực thuộc', 'Chánh Văn phòng', 'Đội trưởng',
 'Đài trưởng', 'Trạm trưởng');"
PATCH_SQL[20261023091047_PositionDefaultWorkflowProfile]="UPDATE positions SET \"DefaultWorkflowProfileCode\" = 'pho-giam-doc'
 WHERE NOT \"IsDeleted\" AND \"Name\" = 'Phó Giám đốc' AND \"DefaultWorkflowProfileCode\" IS NULL;"
PATCH_SQL[20261026081315_ChoVanPhongDangUyXacNhanPhieuDoiSoat]="INSERT INTO role_permissions (role_id, permission_id)
 SELECT r.\"Id\", p.\"Id\" FROM roles r CROSS JOIN permissions p
 WHERE r.\"Code\" = 'VAN_PHONG_DANG_UY' AND r.\"IsDeleted\" = FALSE AND p.\"Code\" = 'evaluation.crosscheck.confirm'
 AND NOT EXISTS (SELECT 1 FROM role_permissions rp WHERE rp.role_id = r.\"Id\" AND rp.permission_id = p.\"Id\");"
PATCH_SQL[20261027025334_DoiTenVaiTroMacDinhTheoThuatNgu]="UPDATE roles r SET \"Name\" = v.new_name,
 \"Description\" = CASE WHEN r.\"Description\" = v.old_desc THEN v.new_desc ELSE r.\"Description\" END, \"UpdatedAt\" = now()
 FROM (VALUES
 ('LANH_DAO_PHONG', 'Lãnh đạo Phòng', 'Lãnh đạo đơn vị',
  'Duyệt danh mục sản phẩm, đề xuất mức, lập kế hoạch 30-60-90 ngày (Mẫu 17) cho cán bộ của Phòng.',
  'Duyệt danh mục sản phẩm, đề xuất mức, lập kế hoạch 30-60-90 ngày (Mẫu 17) cho cán bộ của đơn vị.'),
 ('THU_KY_TAP_THE', 'Thư ký tập thể lãnh đạo', 'Thư ký hội nghị',
  'Ghi nhận đề xuất của tập thể lãnh đạo, lập biên bản hội nghị.',
  'Ghi nhận đề xuất của tập thể lãnh đạo, lập biên bản hội nghị.'),
 ('CHI_UY_CHI_BO', 'Chi ủy / Bí thư Chi bộ', 'Chi bộ',
  'Xác nhận phiếu tự chấm, lập hồ sơ tập thể của Chi bộ.',
  'Chi ủy, Bí thư Chi bộ: xác nhận phiếu tự chấm, lập hồ sơ tập thể của Chi bộ.'),
 ('CO_QUAN_THAM_DINH', 'Phòng TCCB-LĐ (mở kỳ, thẩm định)', 'Cơ quan thẩm định',
  'Thẩm định hồ sơ, quản lý kỳ đánh giá và bộ tiêu chí.',
  'Mở kỳ và thẩm định hồ sơ, quản lý kỳ đánh giá và bộ tiêu chí.'),
 ('CAP_TRUC_TIEP_SU_DUNG', 'Cấp trực tiếp sử dụng (Giám đốc/Chủ tịch)', 'Cấp trực tiếp sử dụng',
  'Nhận xét, đề xuất mức; lập kế hoạch 30-60-90 ngày (Mẫu 17).',
  'Giám đốc, Chủ tịch: nhận xét, đề xuất mức; lập kế hoạch 30-60-90 ngày (Mẫu 17).'),
 ('CAP_UY_VIEN', 'Cấp ủy viên Đảng ủy', 'Cấp ủy viên',
  'Xem hồ sơ, biên bản, báo cáo.',
  'Cấp ủy viên Đảng ủy: xem hồ sơ, biên bản, báo cáo.'),
 ('VAN_PHONG_DANG_UY', 'Văn phòng Đảng ủy (quyết định, công bố)', 'Văn phòng Đảng ủy',
  'Ghi nhận quyết định và kết quả của cấp trên, công bố, mở lại hồ sơ, xử lý kiến nghị; xác nhận phiếu đối soát do cơ quan ngoài hệ thống (người yêu cầu nhập thay).',
  'Quyết định, công bố: ghi nhận quyết định và kết quả của cấp trên, công bố, mở lại hồ sơ, xử lý kiến nghị; xác nhận phiếu đối soát do cơ quan ngoài hệ thống (người yêu cầu nhập thay).')
 ) AS v(code, old_name, new_name, old_desc, new_desc)
 WHERE r.\"Code\" = v.code AND r.\"Name\" = v.old_name AND r.\"IsDeleted\" = FALSE
 AND NOT EXISTS (SELECT 1 FROM roles o WHERE o.\"Name\" = v.new_name AND o.\"IsDeleted\" = FALSE AND o.\"Id\" <> r.\"Id\");"
PATCH_ORDER=(20261023020159_AddPositionIsHead 20261023091047_PositionDefaultWorkflowProfile 20261026081315_ChoVanPhongDangUyXacNhanPhieuDoiSoat 20261027025334_DoiTenVaiTroMacDinhTheoThuatNgu)

log "[5/7] $([ "$DRY" -eq 1 ] && echo 'Chạy thử trên đích (hoàn tác cuối cùng)' || echo 'Chép vào đích trong một transaction')"
SQL="$TMP/chuyen.sql"
STAMP="$(date '+%Y-%m-%d %H:%M:%S%z')"
{
    echo "BEGIN;"
    echo "SET LOCAL client_min_messages = notice;"
    # Dọn dữ liệu vận hành sinh ra khi khởi tạo/đăng nhập thử rồi làm rỗng bảng gốc (ngược thứ tự phụ thuộc).
    for t in "${CLEARABLE[@]}"; do echo "DELETE FROM \"$t\";"; done
    for ((i=${#COPIED[@]}-1; i>=0; i--)); do echo "DELETE FROM \"${COPIED[$i]}\";"; done
    for t in "${COPIED[@]}"; do
        echo "COPY \"$t\" (${COLS[$t]}) FROM stdin;"
        cat "$TMP/$t.copy"
        echo '\.'
    done
    for t in "${COPIED[@]}"; do
        echo "DO \$\$ BEGIN IF (SELECT count(*) FROM \"$t\") <> ${N_COPY[$t]} THEN RAISE EXCEPTION 'Số dòng bảng $t sau khi chép khác kỳ vọng (${N_COPY[$t]})'; END IF; END \$\$;"
    done
    for m in "${PATCH_ORDER[@]}"; do
        if source_lacks "$m"; then echo "-- vá theo migration $m"; echo "${PATCH_SQL[$m]}"; fi
    done
    # Đặt lại sequence (nếu cột dùng sequence) về giá trị lớn nhất đã có.
    for t in "${COPIED[@]}"; do
        echo "DO \$\$ DECLARE r record; BEGIN FOR r IN SELECT a.attname AS col, pg_get_serial_sequence('\"$t\"', a.attname) AS seq FROM pg_attribute a WHERE a.attrelid = '\"$t\"'::regclass AND a.attnum > 0 AND NOT a.attisdropped AND pg_get_serial_sequence('\"$t\"', a.attname) IS NOT NULL LOOP EXECUTE format('SELECT setval(%L, coalesce((SELECT max(%I) FROM \"$t\"), 1))', r.seq, r.col); END LOOP; END \$\$;"
    done
    for t in "${COPIED[@]}"; do echo "ANALYZE \"$t\";"; done
    if [ "$DRY" -eq 0 ]; then
        counts=""; for t in "${COPIED[@]}"; do counts+="$t:${N_COPY[$t]},"; done
        echo "COMMENT ON DATABASE \"$TARGET_DB\" IS 'chuyen-du-lieu-goc $STAMP counts=${counts%,}';"
        echo "COMMIT;"
    else
        echo "ROLLBACK;"
    fi
} > "$SQL"
pg -d "$TARGET_DB" -f - < "$SQL" || die "Chép dữ liệu vào đích không thành công; transaction đã hoàn tác, đích giữ nguyên. Đọc lỗi phía trên (thường là khóa ngoại hoặc ràng buộc không khớp dữ liệu nguồn) rồi xử lý."
[ "$DRY" -eq 1 ] || COMMITTED=1

# --- 6. Đối chiếu -------------------------------------------------------------------------------------------------
log "[6/7] Đối chiếu số dòng bảng gốc"
printf '  %-28s %8s %8s %8s  %s\n' "Bảng" "Nguồn" "Lọc bỏ" "Đích" "Kết luận"
LECH=0
for t in "${GOC[@]}"; do
    if in_list "$t" "${SKIPPED[@]}"; then
        printf '  %-28s %8s %8s %8s  %s\n' "$t" "-" "-" "$(pgval "$TARGET_DB" "SELECT count(*) FROM \"$t\"")" "bỏ qua (nguồn không có bảng)"
        continue
    fi
    if [ "$DRY" -eq 1 ]; then d="(${N_COPY[$t]})"; ok="chạy thử đạt, đích chưa đổi"; else
        d="$(pgval "$TARGET_DB" "SELECT count(*) FROM \"$t\"")"
        if [ "$d" = "${N_COPY[$t]}" ]; then ok="khớp"; else ok="LỆCH"; LECH=1; fi
    fi
    printf '  %-28s %8s %8s %8s  %s\n' "$t" "${N_SRC[$t]}" "$(( ${N_SRC[$t]} - ${N_COPY[$t]} ))" "$d" "$ok"
done
[ "$LECH" -eq 0 ] || die "Số dòng đích lệch so với nguồn. Kiểm tra bảng ghi LỆCH; nếu cần dựng lại đích trống và chạy lại."

# --- 7. File mẫu Word -----------------------------------------------------------------------------------------------
log "[7/7] File mẫu Word (thư mục word-templates) và ảnh nền đăng nhập"
if [ -z "$UPLOADS_FILE" ] || [ ! -f "$UPLOADS_FILE" ]; then
    echo "  Không có uploads.tar.gz của máy cũ: bỏ qua. Nếu máy cũ có file mẫu Word đã tải lên, chép thư mục word-templates vào kho tệp của đích bằng tay."
else
    tar tzf "$UPLOADS_FILE" > "$TMP/tar_ds.txt" || die "Không đọc được $UPLOADS_FILE. Chép lại bản sao lưu."
    KEEP_PATHS=()
    for d in word-templates login-background; do
        if grep -qE "^(\./)?$d(/|\$)" "$TMP/tar_ds.txt"; then
            KEEP_PATHS+=("$(grep -E "^(\./)?$d/?\$" "$TMP/tar_ds.txt" | head -n 1 | sed 's#/$##')")
        fi
    done
    NFILES=0
    [ "${#KEEP_PATHS[@]}" -eq 0 ] || NFILES="$(grep -cE '^(\./)?(word-templates|login-background)/.+[^/]$' "$TMP/tar_ds.txt" || true)"
    if [ "${#KEEP_PATHS[@]}" -eq 0 ]; then
        echo "  Bản sao lưu không có word-templates: không có file mẫu Word tải lên (hệ thống dùng file gốc kèm theo)."
    elif [ "$DRY" -eq 1 ]; then
        echo "  (kiểm tra) sẽ giải nén ${KEEP_PATHS[*]} ($NFILES tệp) vào ${UPLOAD_DIR:-volume $UPLOAD_VOLUME}; tệp đính kèm hồ sơ không khôi phục."
    elif [ -n "$UPLOAD_DIR" ]; then
        mkdir -p "$UPLOAD_DIR"
        tar xzf "$UPLOADS_FILE" -C "$UPLOAD_DIR" "${KEEP_PATHS[@]}" || die "Giải nén file mẫu Word không thành công. Dữ liệu CSDL đã chuyển xong; giải nén tay: tar xzf '$UPLOADS_FILE' -C '$UPLOAD_DIR' ${KEEP_PATHS[*]}"
        echo "  Đã giải nén ${KEEP_PATHS[*]} ($NFILES tệp) vào $UPLOAD_DIR."
    else
        docker volume inspect "$UPLOAD_VOLUME" >/dev/null 2>&1 || die "Không có volume $UPLOAD_VOLUME (dùng --volume-upload hoặc --thu-muc-upload). Dữ liệu CSDL đã chuyển xong; giải nén file mẫu Word sau."
        UP_DIR="$(cd "$(dirname "$UPLOADS_FILE")" && pwd)"; UP_BASE="$(basename "$UPLOADS_FILE")"
        docker run --rm -v "$UPLOAD_VOLUME:/data" -v "$UP_DIR:/backup:ro" "$HELPER_IMAGE" \
            sh -c "tar xzf '/backup/$UP_BASE' -C /data ${KEEP_PATHS[*]} && chown -R $APP_UID:$APP_UID ${KEEP_PATHS[*]/#//data/}" \
            || die "Giải nén file mẫu Word vào volume không thành công. Dữ liệu CSDL đã chuyển xong; chạy lại riêng bước này bằng docker run tương tự."
        echo "  Đã giải nén ${KEEP_PATHS[*]} ($NFILES tệp) vào volume $UPLOAD_VOLUME."
    fi
fi
if [ "$DRY" -eq 0 ]; then
    # Bản ghi phiên bản file mẫu trỏ tới tệp chưa có trên đĩa thì xuất biểu mẫu sẽ hỏng: báo để người vận hành xử lý.
    pgval "$TARGET_DB" 'SELECT "ObjectKey" FROM word_template_versions' > "$TMP/khoa_tep.txt"
    THIEU=0
    while IFS= read -r key; do
        [ -n "$key" ] || continue
        if [ -n "$UPLOAD_DIR" ]; then [ -f "$UPLOAD_DIR/$key" ] || { echo "  [CẢNH BÁO] Thiếu tệp mẫu trên đĩa: $key"; THIEU=1; }
        else docker run --rm -v "$UPLOAD_VOLUME:/data:ro" "$HELPER_IMAGE" test -f "/data/$key" 2>/dev/null || { echo "  [CẢNH BÁO] Thiếu tệp mẫu trong volume: $key"; THIEU=1; }
        fi
    done < "$TMP/khoa_tep.txt"
    [ "$THIEU" -eq 0 ] || echo "  Chép tệp thiếu vào kho tệp hoặc tải lại mẫu trên trang Hệ thống > Biểu mẫu Word."
fi

echo
if [ "$DRY" -eq 1 ]; then
    log "Chạy thử đạt: toàn bộ thao tác chép chạy được và đã hoàn tác. Bỏ --kiem-tra để chuyển thật."
else
    log "Chuyển dữ liệu gốc hoàn tất. Việc tiếp theo:"
    echo "  1. Khởi động backend (seeder bổ sung mã quyền, vai trò mặc định và quyền mới): docker compose --env-file docker/.env -f docker/docker-compose.yml up -d backend frontend"
    echo "  2. Kiểm tra: docker/kiem-tra-sau-chuyen.sh --dich-csdl $TARGET_DB --tai-khoan <tên đăng nhập> (mật khẩu qua biến CTD_MAT_KHAU)"
fi
