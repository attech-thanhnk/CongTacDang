# Thay thế tại chỗ máy đang chạy bản cũ

Tài liệu này dành cho người vận hành khi thay **bản cũ bằng bản mới ngay trên máy đang chạy** (Ubuntu 24.04, Docker Compose, mã nguồn là bản sao Git ở `/opt/congtacdang`). Kết quả cuối cùng:

- chỉ còn một thư mục `/opt/congtacdang`, mã nguồn ở đúng commit được giao;
- CSDL `congtacdang_db` **mới, sạch**, chỉ chứa dữ liệu gốc chuyển từ bản cũ (không có kỳ, hồ sơ, nhật ký, tệp đính kèm của bản cũ);
- thư mục tệp (volume upload) sạch;
- CSDL cũ được giữ dưới tên `congtacdang_db_cu_20261026` và ảnh Docker cũ được giữ dưới nhãn `cu-20261026` **cho tới khi người dùng đăng nhập thử xong và xác nhận**. Chỉ khi đó mới xóa (mục 7).

Mọi lệnh và thời gian trong tài liệu đã chạy thật trong buổi diễn tập (mục 8). Chỗ máy diễn tập khác máy thật được ghi rõ.

Tài liệu liên quan: [Chuyển dữ liệu gốc](chuyen-du-lieu-goc.md) (cách chép, điều kiện, phân loại bảng), [Triển khai máy mới](trien-khai-may-moi.md) (cấu hình `.env`, cổng, sự cố), [Nâng cấp phiên bản](nang-cap-phien-ban.md) (khi giữ nguyên dữ liệu).

Quy ước: thư mục cài đặt `/opt/congtacdang`; mọi lệnh `docker compose` chạy trong `/opt/congtacdang/docker`; `<COMMIT>` là mã commit nhóm phát triển giao; `<SAO_LUU>` là thư mục bản sao lưu cuối cùng của máy cũ (dạng `congtacdang-YYYYMMDD-HHMMSS`).

**Mục lục**

1. [Tổng quan trình tự và thời gian](#1-tổng-quan-trình-tự-và-thời-gian)
2. [Phiếu kiểm trước khi bắt đầu](#2-phiếu-kiểm-trước-khi-bắt-đầu)
3. [Chuẩn bị (hệ thống cũ vẫn chạy)](#3-chuẩn-bị-hệ-thống-cũ-vẫn-chạy)
4. [Thay thế (bắt đầu ngừng dịch vụ)](#4-thay-thế-bắt-đầu-ngừng-dịch-vụ)
5. [Kiểm tra sau khi thay](#5-kiểm-tra-sau-khi-thay)
6. [Đường lui](#6-đường-lui)
7. [Làm sạch sau khi xác nhận](#7-làm-sạch-sau-khi-xác-nhận)
8. [Kết quả diễn tập và khác biệt với máy thật](#8-kết-quả-diễn-tập-và-khác-biệt-với-máy-thật)

---

## 1. Tổng quan trình tự và thời gian

Thời gian đo khi diễn tập (máy Windows, Docker Desktop, mạng nội bộ, lớp ảnh nền đã có sẵn trong bộ nhớ đệm). Máy thật có thể chậm hơn; số liệu chỉ để lập kế hoạch.

| Bước | Việc | Ngừng dịch vụ | Đo được |
|---|---|---|---|
| 3.1 | Sao lưu thường kỳ, chép về máy khác, kiểm checksum | Không | vài giây (CSDL nhỏ) |
| 3.2 | `git fetch` + `git checkout --detach <COMMIT>` | Không | 1 giây |
| 3.3 | Gắn nhãn dự phòng ảnh cũ | Không | dưới 1 giây |
| 3.4 | `docker compose build backend frontend` | Không | 44 giây (đã có bộ nhớ đệm); 98 giây khi có một phần; dựng từ đầu hoàn toàn: chưa đo, tài liệu ước 10 đến 20 phút |
| 3.5 | Chuẩn bị `.env` | Không | 1 phút (làm tay) |
| 4.1 | Dừng bản cũ, sao lưu lần cuối | **Bắt đầu** | khoảng 10 giây |
| 4.2 | Đổi tên CSDL cũ, tạo CSDL mới, làm sạch thư mục tệp | có | khoảng 5 giây |
| 4.3 | `up -d`: backend migrate 23 migration và seed trên CSDL trống | có | 14 giây |
| 4.4 | Dừng backend và frontend | có | khoảng 5 giây |
| 4.5 | `chuyen-du-lieu-goc.sh --kiem-tra` | có | 44 giây |
| 4.6 | `chuyen-du-lieu-goc.sh` (chuyển thật) | có | 47 giây |
| 4.7 | Khởi động lại backend và frontend | có | 13 giây |
| 5 | Kiểm tra tự động và bằng tay | **Kết thúc khi mở lại cho người dùng** | khoảng 5 phút làm tay |

**Thời gian ngừng dịch vụ ước tính**: phần máy chạy từ 4.1 đến 4.7 khoảng 2,5 phút (không tính bước kiểm tra thử 4.5 thì còn khoảng 2 phút); cộng thao tác tay và kiểm tra sau thay thì **khoảng 10 đến 15 phút**. Hãy báo người dùng cửa sổ **30 phút** để có dư.

Trước khi người dùng quay lại, mọi người **phải đăng nhập lại** (phiên đăng nhập cũ không chuyển sang).

---

## 2. Phiếu kiểm trước khi bắt đầu

In ra, đánh dấu từng dòng, ghi ngày và người làm.

| Giai đoạn | Việc | Đạt khi |
|---|---|---|
| **Trước** | Có `<COMMIT>` do nhóm phát triển giao, và đã đọc danh sách thay đổi (3.2) | Có mã commit, đã đọc |
| Trước | Báo người dùng giờ ngừng dịch vụ và việc phải đăng nhập lại | Đã báo |
| Trước | Ổ đĩa còn trống: `df -h /` và `df -h /var/backups` | Còn ít nhất 10 GB (hai ảnh Docker mới khoảng 1,2 GB) |
| Trước | Máy có Internet (tải ảnh nền và gói khi dựng) hoặc đã có sẵn bộ nhớ đệm dựng | Dựng thử được |
| Trước | Container cũ khỏe: `docker compose ps` | Ba container `(healthy)` |
| Trước | Biết mật khẩu (hoặc có quản trị đăng nhập được) để thử đăng nhập sau khi thay | Đã có |
| Trước | Biết mật khẩu một tài khoản thường của bản cũ để thử | Đã có |
| **Trong** | Sao lưu lần cuối thành công, `sha256sum -c` đều `OK` (4.1) | Hai dòng `OK` |
| Trong | Kiểm tra thử `--kiem-tra` đạt (4.5) | Đã đạt |
| Trong | Bảng đối chiếu của lần chuyển thật khớp mọi dòng (4.6) | Cột kết luận toàn `khớp` |
| **Sau** | `docker compose ps`: ba container `(healthy)` | Đạt |
| Sau | `kiem-tra-sau-chuyen.sh` | `KẾT LUẬN: ĐẠT` (9 mục) |
| Sau | Đăng nhập thật bằng quản trị và một tài khoản thường | Vào được trang chủ |
| Sau | Trang Người dùng có đúng số cán bộ như bản cũ | Khớp |
| Sau | Báo cáo → xuất Danh sách cán bộ (Excel) và xuất PDF | Tải được; PDF trả `200` |
| Sau | Đặt lịch sao lưu và chạy thử một lần (mục 5.4) | Có thư mục sao lưu mới |
| Sau | Ghi vào sổ vận hành: commit đang chạy, ngày thay, tên CSDL cũ còn giữ | Đã ghi |
| Sau | Người dùng đăng nhập thử OK và **xác nhận** | Có xác nhận, mới sang mục 7 |

---

## 3. Chuẩn bị (hệ thống cũ vẫn chạy)

### 3.1 Sao lưu thường kỳ và đưa ra khỏi máy

```bash
cd /opt/congtacdang
docker/backup.sh --backup-dir /var/backups/congtacdang/truoc-thay-the --keep 5
```

Mong đợi: dòng cuối `Hoàn tất: /var/backups/congtacdang/truoc-thay-the/congtacdang-...`. Chép thư mục vừa tạo sang máy khác (`scp -r`). Đây là đường lui cuối cùng, ngoài bản CSDL cũ giữ tại chỗ.

### 3.2 Lấy mã nguồn mới trong thư mục dự án (không tạo thư mục mới)

```bash
cd /opt/congtacdang
git log -1 --format='%H  %ad  %s' --date=short     # ghi lại: commit đang chạy
git status --short                                 # phải không in gì
git fetch --all --tags
git log --oneline HEAD..<COMMIT>                   # danh sách thay đổi sẽ vào
git checkout --detach <COMMIT>
git status --short                                 # phải không in gì
git log -1 --format='%h %s'                        # phải đúng <COMMIT>
```

Mong đợi: `HEAD is now at <mã> ...`; hai lệnh `git status --short` không in gì. `git checkout` báo `would be overwritten`: có file bị sửa tại chỗ, **dừng lại** và hỏi nhóm phát triển. Diễn tập: nhảy từ commit `029cb16` lên `7e7ae17` (127 commit, 11 migration mới, không đổi `docker-compose.yml` và Dockerfile).

Xem thay đổi cấu hình cần bổ sung vào `.env`:

```bash
git diff <COMMIT-cũ> HEAD -- docker/.env.example docker/docker-compose.yml docker/Dockerfile.backend docker/Dockerfile.frontend
```

Diễn tập: chỉ thêm ghi chú `FRONTEND_BIND_IP` và `FRONTEND_PORT` vào `.env.example`.

### 3.3 Gắn nhãn dự phòng cho ảnh Docker bản cũ

Dựng ảnh mới sẽ ghi đè nhãn `latest`. Phải gắn nhãn ảnh cũ **trước khi dựng**, nếu không sẽ không còn ảnh cũ để quay lại.

```bash
docker images --format '{{.Repository}}:{{.Tag}}  {{.ID}}  {{.CreatedSince}}' | grep -E '^docker-(backend|frontend):latest'
docker tag docker-backend:latest  docker-backend:cu-20261026
docker tag docker-frontend:latest docker-frontend:cu-20261026
docker images --format '{{.Repository}}:{{.Tag}}  {{.ID}}' | grep -E '^docker-(backend|frontend):'
```

Mong đợi: mỗi ảnh có hai nhãn (`latest` và `cu-20261026`) cùng mã ảnh. Tên ảnh `docker-backend`, `docker-frontend` lấy theo tên thư mục chứa compose (`docker`); khác thì dùng đúng tên lệnh đầu in ra.

### 3.4 Dựng ảnh mới (bản cũ vẫn đang chạy)

```bash
cd /opt/congtacdang/docker
time docker compose build backend frontend
docker images --format '{{.Repository}}:{{.Tag}}  {{.Size}}' | grep -E '^docker-(backend|frontend):latest'
```

Mong đợi: kết thúc `Image docker-backend Built`, `Image docker-frontend Built`, không có chữ `ERROR`. Diễn tập: backend (.NET 10, kèm LibreOffice 24.2) 953 MB, giao diện (Next.js) 229 MB. Dựng lỗi thì bản cũ chưa bị ảnh hưởng; sửa nguyên nhân (mạng, ổ đĩa) rồi dựng lại, hoặc dừng quy trình và báo nhóm phát triển. Không dùng `deploy-ubuntu.sh` ở đây (nó dừng cả hệ thống rồi dựng lại không dùng bộ nhớ đệm).

### 3.5 Chuẩn bị `docker/.env`

Giữ `.env` đang dùng của máy cũ, chỉ sửa các việc sau (không in giá trị bí mật ra màn hình):

```bash
cd /opt/congtacdang/docker
cp -p .env /var/backups/congtacdang/truoc-thay-the/env.truoc-thay-the   # bản dự phòng, quyền 600
grep -E '^(POSTGRES_DB|POSTGRES_USER|POSTGRES_PORT|FRONTEND_BIND_IP|FRONTEND_PORT|Database__AutoMigrate|Database__SeedSampleData)=' .env
```

Cần đạt:

| Biến | Giá trị | Lý do |
|---|---|---|
| `POSTGRES_DB` | `congtacdang_db` | Cùng tên với CSDL cũ (CSDL cũ sẽ được đổi tên) |
| `POSTGRES_USER`, `POSTGRES_PASSWORD` | **giữ nguyên** | Volume dữ liệu PostgreSQL cũ vẫn dùng; `POSTGRES_PASSWORD` chỉ có tác dụng ở lần tạo volume đầu |
| `Jwt__Secret` | giữ hoặc sinh mới (`openssl rand -base64 48`) | Đổi thì mọi người đăng nhập lại, vốn đã phải đăng nhập lại |
| `FRONTEND_BIND_IP` | giữ giá trị đang dùng (IP trong LAN, hoặc `0.0.0.0`) | Người dùng trong LAN vào được qua cổng `3001` |
| `Database__AutoMigrate` | `true` | Backend tự migrate CSDL trống |
| `Database__SeedSampleData` | `false` | Không có dữ liệu mẫu trong CSDL thật |
| `Seed__InitialAdmin__Password` | **để trống** (bỏ giá trị) | Xem kết luận bên dưới |

```bash
sed -i 's/^Seed__InitialAdmin__Password=.*/Seed__InitialAdmin__Password=/' .env
sed -i 's/^Seed__InitialAdmin__Username=.*/Seed__InitialAdmin__Username=/' .env
grep -c '^Seed__InitialAdmin__Password=.\+' .env     # phải in 0
grep -c 'CHANGE_ME' .env                             # phải in 0
docker compose config --quiet && echo CAU_HINH_HOP_LE
```

**Kết luận về tài khoản quản trị khởi tạo** (đã kiểm bằng mã nguồn `DataSeeder` và chạy thật): trên CSDL trống, nếu không đặt `Seed__InitialAdmin__*` thì backend **vẫn khởi động bình thường**, chỉ ghi một dòng cảnh báo "Hệ thống chưa có tài khoản quản trị nào đang hoạt động". Với bản chuyển dữ liệu không cần đặt gì, vì tài khoản quản trị (và mật khẩu băm) lấy từ máy cũ. Chỉ khi bản sao lưu cũ **không có** tài khoản quản trị hoạt động mới phải đặt `Seed__InitialAdmin__*` trước bước 4.7. Mật khẩu quản trị khởi tạo cũng không được để lại trong `.env`.

---

## 4. Thay thế (bắt đầu ngừng dịch vụ)

Ghi giờ bắt đầu: ______. Làm lần lượt, chưa xong bước trước thì chưa làm bước sau.

### 4.1 Dừng bản cũ và sao lưu lần cuối

```bash
cd /opt/congtacdang/docker
docker compose stop frontend backend
docker/backup.sh --backup-dir /var/backups/congtacdang/thay-the --keep 5
ls -d /var/backups/congtacdang/thay-the/congtacdang-* | tail -n 1       # đây là <SAO_LUU>
cd "$(ls -d /var/backups/congtacdang/thay-the/congtacdang-* | tail -n 1)"
sha256sum -c SHA256SUMS
cat manifest.txt
cd /opt/congtacdang/docker
```

Mong đợi: `docker compose ps -a` thấy backend và frontend `Exited`, postgres vẫn `(healthy)`; sao lưu in `Hoàn tất`; `sha256sum -c` hai dòng `OK`; `manifest.txt` có `last_migration` mới nhất của bản cũ. Dừng trước khi sao lưu để bản sao lưu là dữ liệu cuối cùng.

`chuyen-du-lieu-goc.sh` chỉ đọc `db.dump` và `uploads.tar.gz` (và chỉ kiểm checksum của hai tệp này). Tệp khác trong thư mục sao lưu (ví dụ `env.sao-luu` nếu bản `backup.sh` cũ có tạo) **không bị đọc**; tệp đó chứa bí mật, không chép đi nơi khác.

### 4.2 Đổi tên CSDL cũ, tạo CSDL mới, làm sạch thư mục tệp

Định nghĩa hàm gọi psql (chạy trong container, mật khẩu không đi qua dòng lệnh):

```bash
P() { docker exec -i attech-dangbo-postgres sh -c 'exec psql -U "$POSTGRES_USER" -X -q -tA -v ON_ERROR_STOP=1 "$@"' sh "$@"; }
```

Giữ bản sao tệp của bản cũ (chỉ vài MB) trước khi xóa trắng thư mục tệp:

```bash
docker run --rm -v attech-dangbo-backend-uploads:/data:ro -v /var/backups/congtacdang/thay-the:/backup alpine:3.20 \
  tar czf /backup/uploads-cu-20261026.tar.gz -C /data .
ls -l /var/backups/congtacdang/thay-the/uploads-cu-20261026.tar.gz
```

Đổi tên và tạo mới (không còn kết nối nào vì backend đã dừng):

```bash
P -d postgres -c "select count(*) from pg_stat_activity where datname='congtacdang_db'"      # phải in 0
P -d postgres -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='congtacdang_db' AND pid<>pg_backend_pid()"
P -d postgres -c 'ALTER DATABASE congtacdang_db RENAME TO congtacdang_db_cu_20261026'
P -d postgres -c 'CREATE DATABASE congtacdang_db OWNER dangbo_admin'
P -d postgres -c "select datname, pg_get_userbyid(datdba) from pg_database order by 1"
```

Mong đợi: danh sách có cả `congtacdang_db` và `congtacdang_db_cu_20261026`, cùng owner `dangbo_admin` (đúng `POSTGRES_USER` trong `.env`). Diễn tập đã chạy các lệnh này thành công.

Làm sạch thư mục tệp (volume upload). Lệnh dưới là lệnh `restore.sh` vẫn dùng; **chưa chạy trong diễn tập** (công cụ diễn tập không được phép xóa), nên lần đầu chạy trên máy thật hãy chạy `... find /data | wc -l` trước để xem số tệp:

```bash
docker run --rm -v attech-dangbo-backend-uploads:/data alpine:3.20 sh -c 'find /data -mindepth 1 -delete; ls -A /data | wc -l'
```

Mong đợi: in `0`. Thư mục tệp trống là đúng: tệp đính kèm hồ sơ của bản cũ không chuyển; file mẫu Word do giao diện tải lên (nếu bản sao lưu có) sẽ được giải nén ở bước 4.6.

### 4.3 Khởi động trên CSDL trống để backend tạo schema

```bash
docker compose up -d
until [ "$(docker inspect -f '{{.State.Health.Status}}' attech-dangbo-web)" = healthy ]; do sleep 3; done; echo GIAO_DIEN_KHOE
docker compose logs backend 2>&1 | grep -c 'Applying migration'
```

Mong đợi: `GIAO_DIEN_KHOE` sau khoảng 15 giây; số migration áp dụng là 23 (bằng số file migration của bản mới). Ba container `(healthy)`.

Trong log lần khởi động đầu có **một dòng mức Error** về câu lệnh `SELECT ... FROM "__EFMigrationsHistory"`: đó là EF kiểm tra bảng lịch sử migration còn chưa có trên CSDL trống, **bình thường**. Chỉ dừng khi có `Critical` hoặc `[LỖI CẤU HÌNH]`. Có thêm cảnh báo chưa có tài khoản quản trị: bình thường (xem 3.5).

### 4.4 Dừng backend và frontend

```bash
docker compose stop frontend backend
```

Mong đợi: hai container `Exited`. Chuyển dữ liệu yêu cầu không còn kết nối vào CSDL đích.

### 4.5 Chạy thử chuyển dữ liệu (không ghi gì)

```bash
cd /opt/congtacdang
docker/chuyen-du-lieu-goc.sh --kiem-tra <SAO_LUU>
```

Mong đợi: `db.dump: OK`, `uploads.tar.gz: OK`; "Nguồn: 12 migration ... đích: 23 migration" (số nguồn tùy bản cũ); cột "Kết luận" ghi `chạy thử đạt, đích chưa đổi`; dòng cuối `Chạy thử đạt`. Thoát mã 0. Sau lệnh này đích **không đổi**: không có CSDL tạm `ctd_nguon_tam` (đã kiểm), không có dấu đã chuyển, số tài khoản vẫn 0.

### 4.6 Chuyển thật

```bash
docker/chuyen-du-lieu-goc.sh <SAO_LUU>
```

Mong đợi: bảng đối chiếu cuối **khớp ở mọi dòng**; dòng cuối `Chuyển dữ liệu gốc hoàn tất`. Lệch hoặc lỗi: xem 6.2. Diễn tập với bản sao lưu thật của máy cũ:

| Bảng | Nguồn | Lọc bỏ | Đích |
|---|---:|---:|---:|
| org_unit_types | 11 | 0 | 11 |
| administrative_departments | 31 | 0 | 31 |
| party_cells | 24 | 0 | 24 |
| form_layouts | 3 | 0 | 3 |
| positions | 58 | 0 | 58 |
| roles | 9 | 0 | 9 |
| permissions | 31 | 0 | 31 |
| role_permissions | 46 | 0 | 46 |
| party_member_profiles | 55 | 0 | 55 |
| member_positions | 138 | 0 | 138 |
| user_role_assignments | 139 | 0 | 139 |
| organization_settings | 1 | 0 | 1 |
| criteria_sets | 2 | 1 (bộ chấm theo trục) | 1 |
| holidays | 0 | 0 | 0 |
| word_template_versions | 0 | 0 | 0 |

Mật khẩu băm được chép nguyên: băm MD5 của (Id, Username, PasswordHash, SecurityStamp) của 55 tài khoản **trùng** giữa bản sao lưu và đích. Kỳ, hồ sơ và nhật ký của bản cũ (1 kỳ, 11 hồ sơ, 883 dòng nhật ký) không sang đích. Bản sao lưu thật không có `word-templates`, nên không giải nén file mẫu nào (hệ thống dùng file gốc kèm theo).

### 4.7 Khởi động lại bản mới

```bash
cd /opt/congtacdang/docker
docker compose up -d backend frontend
until [ "$(docker inspect -f '{{.State.Health.Status}}' attech-dangbo-web)" = healthy ]; do sleep 3; done; echo GIAO_DIEN_KHOE
docker compose ps
```

Mong đợi: ba container `(healthy)` sau khoảng 13 giây. Seeder bổ sung mã quyền mới, vai trò mặc định mới và quyền mới của vai trò mặc định. Từ lúc này người dùng đăng nhập được; kiểm tra ở mục 5 trước khi báo mở lại.

---

## 5. Kiểm tra sau khi thay

### 5.1 Kiểm tra tự động

```bash
cd /opt/congtacdang
CTD_MAT_KHAU='<mật khẩu của tài khoản thường>' docker/kiem-tra-sau-chuyen.sh --tai-khoan <tên-đăng-nhập>
```

Mong đợi: 9 mục `[ĐẠT]`, `KẾT LUẬN: ĐẠT`. Mật khẩu đi qua biến môi trường, không để vào dòng lệnh. Tài khoản bị buộc đổi mật khẩu (`MustChangePassword`) sẽ không đăng nhập thử được qua API; dùng tài khoản khác.

### 5.2 Kiểm tra qua cổng 3001

```bash
curl -s -o /dev/null -w '%{http_code}\n' http://127.0.0.1:3001/login                # 200
curl -s -c /tmp/ck -o /dev/null -w '%{http_code}\n' -H 'Content-Type: application/json' \
     -d '{"username":"<tên-đăng-nhập>","password":"<mật khẩu>"}' http://127.0.0.1:3001/api/auth/login   # 200
curl -s -b /tmp/ck 'http://127.0.0.1:3001/api/users?pageSize=1' | grep -o '"totalCount":[0-9]*'        # số cán bộ
```

Mong đợi: `200`, `200`, `"totalCount":55` (đúng số cán bộ của bản cũ). Tệp cookie `/tmp/ck` dùng tiếp ở 5.3, xóa sau khi xong (`rm -f /tmp/ck`). Sai mật khẩu phải trả `401`. Đường `/api/healthz` qua cổng giao diện **không** dùng được (trả 404); kiểm tra backend bằng `docker compose exec backend curl --fail --silent http://localhost:5000/healthz`.

### 5.3 Xuất biểu mẫu

Đăng nhập bằng tài khoản có quyền xuất báo cáo rồi thử (hoặc dùng giao diện: menu **Báo cáo**):

```bash
curl -s -b /tmp/ck -o /tmp/ds.xlsx -w '%{http_code} %{content_type}\n' http://127.0.0.1:3001/api/reports/cadres              # Excel
curl -s -b /tmp/ck -o /tmp/m14.pdf  -w '%{http_code} %{content_type}\n' 'http://127.0.0.1:3001/api/reports/form-14?format=pdf'   # PDF
docker compose exec -T backend soffice --version
```

Mong đợi: `200` với Excel, `200 application/pdf` (tệp bắt đầu bằng `%PDF`), `LibreOffice 24.2...`. PDF trả `503` là LibreOffice trong ảnh không chạy ([trien-khai-may-moi.md](trien-khai-may-moi.md#15-sự-cố-thường-gặp)). Biểu mẫu Word theo hồ sơ (Mẫu 01, 02...) chưa xuất thử được vì CSDL sạch chưa có hồ sơ; xuất thử khi đã có kỳ thật.

### 5.4 Cài lịch sao lưu và thử một lần

Cú pháp cron và cách cài: [trien-khai-may-moi.md, mục 10](trien-khai-may-moi.md#10-sao-lưu-định-kỳ). Thử:

```bash
/opt/congtacdang/docker/backup.sh --backup-dir /var/backups/congtacdang --keep 14
cd "$(ls -d /var/backups/congtacdang/congtacdang-* | tail -n 1)" && sha256sum -c SHA256SUMS && cat manifest.txt
/opt/congtacdang/docker/restore.sh --check "$(pwd)"
```

Mong đợi: `Hoàn tất`, hai dòng `OK`, `git_commit` đúng `<COMMIT>` rút gọn, `last_migration` là migration mới nhất; `--check` in `Migration của bản sao lưu khớp mã nguồn.` Diễn tập đã chạy `backup.sh` (2 giây), `restore.sh --check`, `restore.sh --dry-run` và `backup.sh --dry-run` trong môi trường tối giản kiểu cron (`env -i`).

### 5.5 Báo mở lại và ghi sổ

Ghi vào sổ vận hành: `git -C /opt/congtacdang log -1 --format='%h %ad %s' --date=short`, ngày thay, tên CSDL cũ còn giữ (`congtacdang_db_cu_20261026`), nhãn ảnh cũ còn giữ (`cu-20261026`). Báo người dùng mở lại, nhắc đăng nhập lại.

---

## 6. Đường lui

Các lệnh `P` dưới đây dùng hàm định nghĩa ở 4.2 (định nghĩa lại nếu mở phiên mới). Đã diễn tập cả hai chiều (quay lại bản cũ rồi tiến lại bản mới) trong 16 giây mỗi chiều.

### 6.1 Quay về bản cũ sau khi đã chuyển (bản mới có sự cố)

Điều kiện: CSDL cũ `congtacdang_db_cu_20261026` và nhãn ảnh `cu-20261026` còn nguyên (chưa làm mục 7). Trước khi quay lại, gắn nhãn ảnh mới để còn tiến lại được:

```bash
cd /opt/congtacdang/docker
docker tag docker-backend:latest  docker-backend:moi-20261026
docker tag docker-frontend:latest docker-frontend:moi-20261026
docker compose stop frontend backend
P -d postgres -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname IN ('congtacdang_db','congtacdang_db_cu_20261026') AND pid<>pg_backend_pid()"
P -d postgres -c 'ALTER DATABASE congtacdang_db RENAME TO congtacdang_db_moi_loi_20261026'
P -d postgres -c 'ALTER DATABASE congtacdang_db_cu_20261026 RENAME TO congtacdang_db'
docker tag docker-backend:cu-20261026  docker-backend:latest
docker tag docker-frontend:cu-20261026 docker-frontend:latest
docker compose up -d --no-build backend frontend
```

`--no-build` bắt buộc: không có nó Compose có thể dựng lại ảnh từ mã nguồn mới. Mã nguồn trong `/opt/congtacdang` vẫn ở commit mới; ảnh cũ chạy độc lập với mã nguồn, nên không cần `git checkout` lại. Muốn đồng bộ thì `git checkout --detach <COMMIT-cũ>` (chỉ để ghi sổ; không dựng lại).

Kiểm tra: ba container `(healthy)`; `docker inspect -f '{{.Image}}' attech-dangbo-api` ra đúng mã ảnh cũ; `P -d congtacdang_db -c 'select count(*) from "__EFMigrationsHistory"'` ra số migration của bản cũ (diễn tập: 13); trang `/login` trả 200. Thư mục tệp: nếu đã làm sạch ở 4.2, khôi phục từ bản đã giữ:

```bash
docker run --rm -v attech-dangbo-backend-uploads:/data -v /var/backups/congtacdang/thay-the:/backup:ro alpine:3.20 \
  sh -c 'find /data -mindepth 1 -delete; tar xzf /backup/uploads-cu-20261026.tar.gz -C /data && chown -R 10001:10001 /data'
```

Dữ liệu nhập vào bản mới (nếu đã có người dùng) vẫn nằm ở CSDL `congtacdang_db_moi_loi_20261026`.

**Tiến lại bản mới** (sau khi sửa xong): đảo hai lần đổi tên và nhãn ảnh:

```bash
docker compose stop frontend backend
P -d postgres -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname IN ('congtacdang_db','congtacdang_db_moi_loi_20261026') AND pid<>pg_backend_pid()"
P -d postgres -c 'ALTER DATABASE congtacdang_db RENAME TO congtacdang_db_cu_20261026'
P -d postgres -c 'ALTER DATABASE congtacdang_db_moi_loi_20261026 RENAME TO congtacdang_db'
docker tag docker-backend:moi-20261026  docker-backend:latest
docker tag docker-frontend:moi-20261026 docker-frontend:latest
docker compose up -d --no-build backend frontend
```

Diễn tập: sau khi tiến lại, `kiem-tra-sau-chuyen.sh` vẫn `ĐẠT`, migration 23.

### 6.2 Chuyển dữ liệu không thành công giữa chừng

`chuyen-du-lieu-goc.sh` chép trong **một transaction**: lỗi ở đâu cũng hoàn tác, đích giữ nguyên, CSDL tạm tự xóa. Không cần quay lại bản cũ; sửa nguyên nhân rồi chạy lại.

| Tình huống | Script làm gì | Việc cần làm |
|---|---|---|
| Bản dump cắt cụt hoặc hỏng | Dừng ở bước 1 (`pg_restore --list`), đích không đổi, không để lại `ctd_nguon_tam` (diễn tập: dump cắt còn 100 KB) | Chép lại bản sao lưu |
| Checksum không khớp | Dừng ở bước 1 | Chép lại bản sao lưu |
| Đích đã có dữ liệu hoặc đã nhận dữ liệu gốc | Từ chối và liệt kê lý do, không đổi gì (diễn tập: chạy lại lên đích đã chuyển bị từ chối 3 lý do) | Dựng lại đích trống (bên dưới) |
| Lỗi ràng buộc khi chép (ví dụ dòng mồ côi) | Hoàn tác transaction; đích vẫn như sau bước 4.3 | Báo nhóm phát triển; chưa khởi động bản mới |

**Dựng lại đích trống** (sau lần chuyển đã ghi dấu hoặc đích bị làm bẩn):

```bash
docker compose stop frontend backend
P -d postgres -c 'DROP DATABASE congtacdang_db WITH (FORCE)'
P -d postgres -c 'CREATE DATABASE congtacdang_db OWNER dangbo_admin'
# rồi làm lại từ bước 4.3
```

Chỉ chạy `DROP DATABASE congtacdang_db` khi chắc `congtacdang_db_cu_20261026` còn nguyên và là CSDL cũ (kiểm tên bằng `P -d postgres -c '\l'`).

### 6.3 Bản sao lưu cuối cùng

Còn bản sao lưu 4.1 và 3.1 (đã chép ra máy khác). Khôi phục bằng [deployment.md, mục Khôi phục sao lưu](deployment.md#khôi-phục-sao-lưu), cần mã nguồn và ảnh về đúng bản cũ.

---

## 7. Làm sạch sau khi xác nhận

Chỉ làm khi người dùng đã đăng nhập thử OK **và nói rõ** là đồng ý xóa; nên chờ vài ngày. Sau đó **không còn đường lui nhanh** (chỉ còn bản sao lưu).

```bash
cd /opt/congtacdang/docker
P -d postgres -c 'DROP DATABASE congtacdang_db_cu_20261026 WITH (FORCE)'
docker rmi docker-backend:cu-20261026 docker-frontend:cu-20261026
docker rmi docker-backend:moi-20261026 docker-frontend:moi-20261026 2>/dev/null || true
docker image prune -f
docker builder prune -f
```

Không chạy `docker volume prune` hay `docker system prune --volumes` (xóa dữ liệu). Nếu đã tạo `congtacdang_db_moi_loi_*` trong lúc quay lại bản cũ, xóa tương tự sau khi không cần nữa.

Kiểm không còn gì thừa:

```bash
P -d postgres -c "select datname from pg_database where datname not in ('postgres','template0','template1') order by 1"   # chỉ còn congtacdang_db
docker images --format '{{.Repository}}:{{.Tag}}' | grep -E '^docker-(backend|frontend):'                                  # chỉ còn :latest
docker ps -a --format '{{.Names}}  {{.Status}}'                                                                           # ba container attech-dangbo-*
docker volume ls --format '{{.Name}}' | grep attech-dangbo                                                                  # postgres-data, backend-uploads, backend-logs
ls /opt                                                                                                                    # chỉ có congtacdang (không có thư mục mã nguồn khác)
git -C /opt/congtacdang status --short                                                                                      # không in gì
grep -c '^Seed__InitialAdmin__Password=.\+' /opt/congtacdang/docker/.env                                                    # 0
```

Xóa các tệp tạm chứa dữ liệu cũ khi đã ổn định: `uploads-cu-20261026.tar.gz`, `env.truoc-thay-the` (chứa bí mật; xóa an toàn bằng `shred -u`), thư mục sao lưu trung gian trên máy khác theo chính sách đơn vị. Bản sao lưu bản cũ nên giữ theo kỳ hạn lưu trữ chung, không xóa cùng lúc.

---

## 8. Kết quả diễn tập và khác biệt với máy thật

Diễn tập ngày 26/10/2026 trên máy Windows (Git Bash, Docker Desktop, 8 GB RAM cấp cho Docker). Mã nguồn là bản sao Git cục bộ nhảy từ `029cb16` lên `7e7ae17`; đầu vào chuyển là bản sao lưu thật của máy cũ (12 migration, 55 cán bộ). Mọi bước 3.2 đến 6.1 chạy đạt. Dùng chính bộ Compose của dự án (tên `attech-dangbo-*`, cổng 3001).

Khác biệt cần nhớ:

| Nội dung | Diễn tập | Máy thật |
|---|---|---|
| Ảnh bản cũ | Ảnh `docker-backend`, `docker-frontend` dựng từ mã phát triển 4 ngày trước, dùng làm "ảnh cũ" | Ảnh dựng từ `029cb16` (kiểm bằng 3.3) |
| CSDL cũ | `congtacdang_db` cục bộ 13 migration, dữ liệu thử | CSDL thật 12 migration |
| Bộ nhớ đệm dựng | Có sẵn lớp ảnh nền (44 giây) | Có từ lần dựng cũ; thiếu thì lâu hơn (ước 10 đến 20 phút) |
| Làm sạch thư mục tệp (4.2) | Chưa chạy (không được phép xóa); chỉ sao lưu và liệt kê | Chạy theo 4.2 |
| Mật khẩu để thử đăng nhập | Đặt mật khẩu thử bằng SQL cho hai tài khoản trên CSDL mới (chỉ máy diễn tập) vì không biết mật khẩu thật | Dùng mật khẩu thật của người dùng; **không** đặt bằng SQL |
| Windows và Git Bash | Cần `MSYS_NO_PATHCONV=1`; `backup.sh` cần `ENV_FILE`, `COMPOSE_FILE` ở dạng `D:/...` và `--backup-dir` dạng `/d/...`; `git_commit` trong manifest ra `unknown` | Ubuntu không cần; `git_commit` ghi đúng |
| `deploy-ubuntu.sh` | Không dùng | Không dùng (dừng cả hệ thống, dựng lại không có bộ nhớ đệm) |

Chưa kiểm chứng được: chạy trên Ubuntu thật (quyền tệp, `sudo`, thời gian dựng từ đầu), làm sạch thư mục tệp ở 4.2, các lệnh mục 7 (chưa chạy vì chưa có xác nhận), tải file mẫu Word từ bản sao lưu có `word-templates` trên bản sao lưu thật (bản sao lưu thật không có), và thử đăng nhập bằng mật khẩu thật.
