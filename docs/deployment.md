# Triển khai CongTacDang

Tài liệu này là tài liệu triển khai duy nhất của hệ thống. Hệ thống chạy trong mạng LAN nội bộ bằng Docker Compose, gồm frontend Next.js, backend ASP.NET Core và PostgreSQL. MinIO và pgAdmin là công cụ tùy chọn, không chạy mặc định.

## Yêu cầu

- Ubuntu có Docker Engine và Docker Compose v2.
- DNS hoặc địa chỉ IP nội bộ trỏ tới máy chạy frontend.
- Không mở trực tiếp cổng backend ra mạng LAN nếu không có nhu cầu vận hành cụ thể.

## Cấu hình bắt buộc

Không ghi secret vào Git. Tạo file môi trường từ mẫu:

```bash
cd docker
cp .env.example .env
chmod 600 .env
```

Thay các giá trị `CHANGE_ME_*` trong `docker/.env`, tối thiểu:

- `POSTGRES_PASSWORD`: mật khẩu PostgreSQL.
- `MINIO_ROOT_USER` và `MINIO_ROOT_PASSWORD`: chỉ cần khi bật profile `tools`.
- `PGADMIN_DEFAULT_PASSWORD`: chỉ cần khi bật profile `tools`.

Sinh mật khẩu ngẫu nhiên trên Linux:

```bash
openssl rand -base64 32
```

Compose mặc định chỉ publish frontend ở `127.0.0.1:3001` và PostgreSQL ở `127.0.0.1:5434`. Backend chỉ có `expose: 5000` trong mạng Docker. Nếu cần debug backend cục bộ, đặt trong `.env`:

```dotenv
BACKEND_PORTS=["127.0.0.1:5000:5000"]
```

Không đặt backend bind vào `0.0.0.0` nếu không có firewall và reverse proxy phù hợp.

## Secret backend khi chạy trực tiếp

Khi chạy API ngoài Docker, bắt buộc cung cấp `ConnectionStrings:Default` và `Jwt:Secret`. Secret JWT phải có ít nhất 32 byte và không được dùng chuỗi mẫu cũ.

Khởi tạo user-secrets một lần:

```bash
dotnet user-secrets init --project backend/src/CongTacDang.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=congtacdang_db;Username=dangbo_admin;Password=<mat-khau>" --project backend/src/CongTacDang.Api
dotnet user-secrets set "Jwt:Secret" "$(openssl rand -base64 48)" --project backend/src/CongTacDang.Api
```

Trên PowerShell, thay lệnh sinh secret bằng:

```powershell
dotnet user-secrets set "Jwt:Secret" ([Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))) --project backend/src/CongTacDang.Api
```

Swagger chỉ bật ở Development hoặc khi đặt `Swagger:Enabled=true`. Không bật tùy chọn này trên môi trường production nếu không cần.

## Khởi động Compose

Kiểm tra cấu hình trước khi chạy:

```bash
docker compose --env-file docker/.env -f docker/docker-compose.yml config
```

Khởi động dịch vụ chính:

```bash
docker compose --env-file docker/.env -f docker/docker-compose.yml up -d
docker compose --env-file docker/.env -f docker/docker-compose.yml ps
```

Truy cập frontend tại `http://127.0.0.1:3001` hoặc địa chỉ reverse proxy nội bộ. Kiểm tra healthcheck:

```bash
curl --fail http://127.0.0.1:3001/api/healthz
```

Backend `/healthz` được container kiểm tra nội bộ. Kiểm tra kết nối database đầy đủ cần triển khai health check DB riêng sau khi hoàn tất phần migration/persistence.

## LibreOffice (xuất PDF)

Biểu mẫu Word/Excel được chuyển sang PDF ngay trên máy chủ bằng LibreOffice headless (không gọi dịch vụ bên ngoài). Thiếu LibreOffice thì API xuất PDF (`?format=pdf`) trả **503** kèm thông báo; xuất Word/Excel vẫn hoạt động bình thường.

- **Docker:** image backend (`docker/Dockerfile.backend`) đã cài `libreoffice-writer-nogui`, `libreoffice-calc-nogui` và font `fonts-liberation` (Liberation Serif tương thích kích thước với Times New Roman, đủ dấu tiếng Việt), `fonts-dejavu-core`. Không cần cấu hình thêm.
- **Chạy trực tiếp trên Ubuntu/Debian:**

  ```bash
  sudo apt-get install -y --no-install-recommends libreoffice-writer-nogui libreoffice-calc-nogui fonts-liberation fonts-dejavu-core
  soffice --version
  ```

  Nếu có bản quyền font Times New Roman, cài thêm để PDF giống Word nhất (ví dụ `ttf-mscorefonts-installer` hoặc chép tệp `.ttf` vào `/usr/local/share/fonts` rồi `fc-cache -f`).
- **Chạy trực tiếp trên Windows:** cài LibreOffice bản ổn định (mặc định `C:\Program Files\LibreOffice`). Backend tự tìm `soffice` trong `PATH` và thư mục cài đặt mặc định.

Cấu hình (tùy chọn, đặt trong `appsettings` hoặc biến môi trường `Documents__Pdf__...`):

| Key | Mặc định | Ý nghĩa |
|---|---|---|
| `Documents:Pdf:SofficePath` | tự tìm | Đường dẫn tệp chạy `soffice` khi cài ở vị trí khác. |
| `Documents:Pdf:TimeoutSeconds` | `60` | Thời gian tối đa một lần chuyển; quá hạn thì dừng tiến trình và trả 503. |
| `Documents:Pdf:MaxConcurrency` | `2` | Số lần chuyển chạy đồng thời. |
| `Documents:Pdf:WorkDirectory` | thư mục tạm hệ thống | Thư mục làm việc tạm; mỗi lần chuyển dùng thư mục con riêng và được xóa sau khi xong. Tài khoản chạy backend phải có quyền ghi. |
| `Documents:TemplatePath` | `Templates/Word` cạnh ứng dụng | Thư mục template Word (xem `docs/bieu-mau.md`). |

Kiểm tra nhanh sau khi triển khai: đăng nhập, mở một hồ sơ đánh giá → “Xem PDF” (hoặc gọi `GET /api/reports/docx/mau-01/{recordId}?format=pdf`).

## Công cụ tùy chọn

MinIO và pgAdmin nằm trong profile `tools`, không được khởi động cùng stack mặc định:

```bash
docker compose --env-file docker/.env -f docker/docker-compose.yml --profile tools up -d minio minio-init pgadmin
```

Bucket MinIO được tạo riêng tư. Không dùng `mc anonymous set download`; quyền truy cập phải đi qua adapter lưu trữ đã được ký/xác thực. Hiện backend đang đăng ký local storage, vì vậy cần xóa hẳn adapter MinIO nếu không dùng hoặc viết lại bằng SDK chính thức trước khi chuyển sang MinIO.

## Backup và khôi phục

Dùng hai script trong `docker/` (chạy trên máy chủ Docker, bằng user có quyền dùng `docker`):

| Script | Việc làm |
|---|---|
| `docker/backup.sh` | `pg_dump --format=custom` CSDL + nén volume `attech-dangbo-backend-uploads`, ghi `SHA256SUMS` và `manifest.txt`, giữ `N` bản gần nhất |
| `docker/restore.sh` | Kiểm tra checksum, yêu cầu gõ `KHOI PHUC` để xác nhận, dừng frontend/backend, `pg_restore --clean --if-exists --single-transaction`, thay nội dung volume upload, khởi động lại |

Mật khẩu CSDL không đi qua dòng lệnh: `pg_dump`/`pg_restore` chạy bên trong container `postgres` bằng biến môi trường của container.

### Sao lưu

```bash
# Mặc định lưu vào /var/backups/congtacdang, giữ 14 bản
sudo mkdir -p /var/backups/congtacdang && sudo chown "$USER" /var/backups/congtacdang
docker/backup.sh
docker/backup.sh --backup-dir /mnt/nas/congtacdang --keep 30
docker/backup.sh --dry-run          # chỉ in các lệnh sẽ chạy
```

Mỗi bản là một thư mục `congtacdang-YYYYMMDD-HHMMSS/` gồm `db.dump`, `uploads.tar.gz`, `SHA256SUMS`, `manifest.txt`. Bản đang chạy dở có hậu tố `.partial` và bị xóa nếu lỗi; chỉ thư mục đúng mẫu tên mới được tính khi xoay vòng.

Biến môi trường tùy chọn: `BACKUP_DIR`, `BACKUP_KEEP`, `ENV_FILE` (mặc định `docker/.env`), `COMPOSE_FILE`, `UPLOAD_VOLUME`, `HELPER_IMAGE` (mặc định `alpine:3.20`).

### Lịch sao lưu khuyến nghị

- Hằng ngày lúc 02:00, giữ 14 bản; trước mỗi lần nâng cấp/triển khai chạy thêm một bản thủ công.
- Sao chép thư mục sao lưu sang máy/ổ khác (NAS, ổ ngoài) — bản sao lưu nằm cùng máy chủ không bảo vệ được khi hỏng đĩa.
- Trong kỳ đánh giá cao điểm (cuối quý) có thể tăng lên 2 lần/ngày.

Ví dụ crontab (`crontab -e` của user vận hành):

```cron
0 2 * * * /opt/congtacdang/docker/backup.sh >> /var/log/congtacdang-backup.log 2>&1
```

### Kiểm tra bản sao lưu

`backup.sh` đã tự kiểm tra `pg_restore --list` và `tar tzf` ngay sau khi tạo. Định kỳ (ít nhất mỗi tháng) kiểm tra thêm:

```bash
cd /var/backups/congtacdang/congtacdang-YYYYMMDD-HHMMSS
sha256sum -c SHA256SUMS                    # toàn vẹn file
cat manifest.txt                           # thời điểm, kích thước
tar tzf uploads.tar.gz | head              # danh sách file upload
```

Và **khôi phục thử** vào một môi trường riêng (máy thử nghiệm hoặc compose project khác, không phải CSDL đang dùng): chạy `restore.sh`, đăng nhập, mở một hồ sơ có minh chứng, xuất một biểu mẫu.

### Khôi phục

```bash
docker/backup.sh                                                        # sao lưu hiện trạng trước
docker/restore.sh --dry-run /var/backups/congtacdang/congtacdang-20260928-020000
docker/restore.sh /var/backups/congtacdang/congtacdang-20260928-020000  # gõ KHOI PHUC để xác nhận
docker/restore.sh --skip-uploads <thư-mục>                              # chỉ khôi phục CSDL
```

`--yes` bỏ bước xác nhận (chỉ dùng trong kịch bản tự động đã kiểm soát). `pg_restore` chạy trong một transaction: lỗi giữa chừng thì CSDL giữ nguyên. Sau khi khôi phục, script khởi động lại backend/frontend; kiểm tra `/api/healthz`, đăng nhập và mở file minh chứng.

## Log

Backend ghi log có cấu trúc (JSON) ra console và ra file xoay vòng theo ngày `congtacdang-YYYYMMDD.log`. Trong Docker, thư mục log là `/var/log/congtacdang`, mount volume `attech-dangbo-backend-logs`; số ngày giữ đặt bằng `LOG_RETAINED_DAYS` trong `docker/.env` (mặc định 30).

Mỗi request có correlation id: backend nhận header `X-Request-Id` nếu hợp lệ (tối đa 64 ký tự chữ/số/`-_.:`), không thì sinh mới; id được trả lại trong response header `X-Request-Id` và gắn vào trường `CorrelationId` của mọi dòng log của request đó.

```bash
# Tìm toàn bộ log của một request
docker run --rm -v attech-dangbo-backend-logs:/logs:ro alpine grep -h '"CorrelationId":"<id>"' /logs/congtacdang-*.log
docker compose --env-file docker/.env -f docker/docker-compose.yml logs backend | grep '<id>'
```

Cấu hình (biến môi trường dạng `Logging__File__Path`):

| Key | Mặc định | Ý nghĩa |
|---|---|---|
| `Logging:File:Enabled` | `true` | Bật/tắt log ra file |
| `Logging:File:Path` | thư mục `logs` cạnh thư mục publish (Docker: `/var/log/congtacdang`) | Thư mục log |
| `Logging:File:RetainedDays` | `30` | Số ngày giữ file log |
| `Logging:Console:Json` | `true` ngoài Development | Log console dạng JSON |

Log không ghi body request, header, cookie hay token.

## Rotate secret

Secret cũ từng nằm trong lịch sử Git. Cần rotate cả JWT secret và mật khẩu database, không rewrite lịch sử Git:

1. Sinh JWT secret và mật khẩu database mới.
2. Cập nhật secret trong secret manager, `dotnet user-secrets`, hoặc `docker/.env` trên máy triển khai.
3. Khởi động lại backend và kiểm tra đăng nhập, healthcheck.
4. Thu hồi secret cũ và xác nhận các phiên/token cũ không còn được chấp nhận.
5. Giới hạn quyền đọc file `.env` ở `600`, không gửi file này qua chat hoặc commit vào repository.

Sau khi đổi mật khẩu PostgreSQL, cập nhật chuỗi kết nối backend cùng lúc và giữ lại backup trước khi rotate.
