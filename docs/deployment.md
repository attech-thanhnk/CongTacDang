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

## Công cụ tùy chọn

MinIO và pgAdmin nằm trong profile `tools`, không được khởi động cùng stack mặc định:

```bash
docker compose --env-file docker/.env -f docker/docker-compose.yml --profile tools up -d minio minio-init pgadmin
```

Bucket MinIO được tạo riêng tư. Không dùng `mc anonymous set download`; quyền truy cập phải đi qua adapter lưu trữ đã được ký/xác thực. Hiện backend đang đăng ký local storage, vì vậy cần xóa hẳn adapter MinIO nếu không dùng hoặc viết lại bằng SDK chính thức trước khi chuyển sang MinIO.

## Backup và khôi phục

Backup PostgreSQL, ví dụ:

```bash
docker compose --env-file docker/.env -f docker/docker-compose.yml exec -T postgres pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom > "backup-$(date +%Y%m%d-%H%M%S).dump"
```

Khi lệnh chạy từ máy host, đọc giá trị tương ứng trong `docker/.env` hoặc truyền trực tiếp giá trị cho `-U` và `-d`; không đưa mật khẩu vào command history.

Sao lưu volume upload cùng lịch backup database:

```bash
docker run --rm -v attech-dangbo-backend-uploads:/data -v "$PWD":/backup alpine tar czf /backup/uploads-$(date +%Y%m%d-%H%M%S).tar.gz -C /data .
```

Khôi phục database bằng `pg_restore` sau khi dừng hoặc cô lập ứng dụng. Khôi phục upload vào volume `attech-dangbo-backend-uploads`, sau đó kiểm tra quyền đọc/ghi của backend.

## Rotate secret

Secret cũ từng nằm trong lịch sử Git. Cần rotate cả JWT secret và mật khẩu database, không rewrite lịch sử Git:

1. Sinh JWT secret và mật khẩu database mới.
2. Cập nhật secret trong secret manager, `dotnet user-secrets`, hoặc `docker/.env` trên máy triển khai.
3. Khởi động lại backend và kiểm tra đăng nhập, healthcheck.
4. Thu hồi secret cũ và xác nhận các phiên/token cũ không còn được chấp nhận.
5. Giới hạn quyền đọc file `.env` ở `600`, không gửi file này qua chat hoặc commit vào repository.

Sau khi đổi mật khẩu PostgreSQL, cập nhật chuỗi kết nối backend cùng lúc và giữ lại backup trước khi rotate.
