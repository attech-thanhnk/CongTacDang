#!/usr/bin/env bash
# =============================================================================
# KHẢO SÁT MÁY ĐANG CHẠY BẢN CŨ — CHỈ ĐỌC, KHÔNG THAY ĐỔI GÌ
#
# Thu thập thông tin để lập kế hoạch thay thế: hệ điều hành, Docker, nơi đặt mã
# nguồn, phiên bản CSDL (danh sách migration), số liệu các bảng chính, dung lượng
# thư mục tệp, bản sao lưu hiện có.
# Không in mật khẩu hay giá trị cấu hình: chỉ in TÊN các biến trong file .env.
#
# Cách chạy (trên máy bạn, nhập mật khẩu SSH khi được hỏi):
#   ssh attech@192.168.22.149 'bash -s' < docker/khao-sat-may-cu.sh > khao-sat.txt
# hoặc chép file lên máy cũ rồi chạy: bash khao-sat-may-cu.sh > khao-sat.txt
# Gửi nội dung khao-sat.txt cho người hỗ trợ (không chứa mật khẩu).
# =============================================================================
set -u

muc() { printf '\n===== %s =====\n' "$1"; }

DOCKER="docker"
if ! docker ps >/dev/null 2>&1; then
    if sudo -n docker ps >/dev/null 2>&1; then
        DOCKER="sudo docker"
    else
        DOCKER="sudo docker"
        echo "[Lưu ý] Cần quyền sudo để chạy docker; nếu được hỏi mật khẩu, hãy nhập."
    fi
fi

muc "1. Máy và hệ điều hành"
hostname
(. /etc/os-release 2>/dev/null && echo "$PRETTY_NAME") || uname -a
uname -r
uptime -p 2>/dev/null || true
echo "Ngày giờ máy: $(date '+%Y-%m-%d %H:%M:%S %Z')"
df -h / /var/lib/docker 2>/dev/null | sed 's/^/  /'
free -h 2>/dev/null | sed 's/^/  /'

muc "2. Docker"
$DOCKER version --format 'Docker {{.Server.Version}}' 2>&1 | head -2
$DOCKER compose version 2>&1 | head -1
$DOCKER ps -a --format 'table {{.Names}}\t{{.Image}}\t{{.Status}}\t{{.Ports}}' 2>&1

muc "3. Nơi đặt mã nguồn và cấu hình"
COMPOSE_FILES="$(find /opt /srv /home /root /var/www -maxdepth 5 -name 'docker-compose.yml' 2>/dev/null | head -5)"
if [ -z "$COMPOSE_FILES" ]; then
    echo "Không tìm thấy docker-compose.yml trong /opt /srv /home /var/www (thử: sudo find / -name docker-compose.yml)."
else
    echo "$COMPOSE_FILES"
fi
for f in $COMPOSE_FILES; do
    d="$(dirname "$f")"
    echo "--- Thư mục: $d"
    if [ -d "$d/../.git" ]; then
        (cd "$d/.." && echo "Git: nhánh $(git rev-parse --abbrev-ref HEAD 2>/dev/null), commit $(git log -1 --format='%h %ad %s' --date=short 2>/dev/null)")
    elif [ -d "$d/.git" ]; then
        (cd "$d" && echo "Git: nhánh $(git rev-parse --abbrev-ref HEAD 2>/dev/null), commit $(git log -1 --format='%h %ad %s' --date=short 2>/dev/null)")
    else
        echo "Không có thư mục .git (mã nguồn được chép tay hoặc gói phát hành)."
    fi
    if [ -f "$d/.env" ]; then
        echo "File .env có các biến (chỉ tên, không in giá trị):"
        sed -n 's/^\([A-Za-z_][A-Za-z0-9_]*\)=.*/  \1/p' "$d/.env" | sort | tr '\n' ' '; echo
    else
        echo "Không có file .env cạnh docker-compose.yml."
    fi
done

muc "4. Container và ảnh (image)"
$DOCKER images --format 'table {{.Repository}}\t{{.Tag}}\t{{.CreatedAt}}\t{{.Size}}' 2>&1 | head -12

muc "5. CSDL PostgreSQL"
PG="$($DOCKER ps --format '{{.Names}}' 2>/dev/null | grep -i postgres | head -1)"
if [ -z "$PG" ]; then
    echo "Không thấy container PostgreSQL đang chạy."
else
    echo "Container: $PG"
    PGUSER="$($DOCKER exec "$PG" printenv POSTGRES_USER 2>/dev/null)"
    echo "Người dùng CSDL: ${PGUSER:-<không đọc được>}"
    psqlq() { $DOCKER exec "$PG" psql -U "$PGUSER" -d "$1" -At -F ' | ' -c "$2" 2>&1; }
    echo "--- Các cơ sở dữ liệu:"
    psqlq postgres "select datname, pg_size_pretty(pg_database_size(datname)) from pg_database where datname not like 'template%' order by 1"
    for DB in $(psqlq postgres "select datname from pg_database where datname not like 'template%' and datname<>'postgres'"); do
        echo
        echo "### CSDL: $DB"
        echo "--- 6 migration mới nhất:"
        psqlq "$DB" 'select "MigrationId" from "__EFMigrationsHistory" order by 1 desc limit 6'
        echo "--- Số migration đã áp dụng:"
        psqlq "$DB" 'select count(*) from "__EFMigrationsHistory"'
        echo "--- Số dòng các bảng chính:"
        for T in party_member_profiles roles user_role_assignments administrative_departments party_cells positions member_positions org_unit_types criteria_sets evaluation_periods evaluation_records; do
            printf '  %-32s ' "$T"; psqlq "$DB" "select count(*) from $T" | head -1
        done
        echo "--- Kỳ đánh giá:"
        psqlq "$DB" 'select "Name","Year","Quarter","Status" from evaluation_periods order by "StartDate"' 2>&1 | head -10
    done
fi

muc "6. Thư mục tệp (upload, file mẫu Word)"
$DOCKER volume ls --format '{{.Name}}' 2>/dev/null | grep -iE 'attech|dangbo|congtacdang' | while read -r V; do
    SIZE="$($DOCKER run --rm -v "$V":/v alpine:3.20 du -sh /v 2>/dev/null | cut -f1)"
    echo "  $V : ${SIZE:-<không đo được>}"
done

muc "7. Bản sao lưu hiện có"
for D in /var/backups/congtacdang "$HOME/backups" /opt/backups; do
    if [ -d "$D" ]; then
        echo "--- $D"
        ls -1t "$D" 2>/dev/null | head -5 | sed 's/^/  /'
    fi
done
(crontab -l 2>/dev/null | grep -i -E 'backup|congtacdang' | sed 's/^/  cron: /') || true

muc "8. Cổng đang lắng nghe"
(ss -ltn 2>/dev/null || netstat -ltn 2>/dev/null) | grep -E ':(80|443|3000|3001|5000|5432|5434|8080)\b' | sed 's/^/  /'

muc "Xong"
echo "Đây là toàn bộ nội dung khảo sát. Không có gì bị thay đổi trên máy này."
