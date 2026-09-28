# Báo cáo: Task 03 — Hạ tầng, Cấu hình & Vận hành

- **Branch:** `fix/security-auth` (branch hiện tại theo yêu cầu)
- **Commit cuối của task:** `bea7f6b`
- **Ngày:** 2026-09-28

## Kết quả theo mã lỗi

| Mã | Trạng thái | Tóm tắt thay đổi | File chính |
|---|---|---|---|
| T-05 | done | Xóa secret/connection string/IP/key MinIO khỏi appsettings; validate `Jwt:Secret` và `ConnectionStrings:Default` khi khởi động; thêm hướng dẫn user-secrets và `.env.example`. Secret cũ đã nằm trong lịch sử Git, cần rotate JWT secret và mật khẩu DB, không rewrite lịch sử. | `backend/src/CongTacDang.Api/Extensions/HostingExtensions.cs`, `backend/src/CongTacDang.Api/appsettings*.json`, `docker/.env.example`, `docs/deployment.md` |
| T-06 | done | Swagger chỉ bật ở Development hoặc `Swagger:Enabled=true`; mặc định production tắt. | `backend/src/CongTacDang.Api/Program.cs`, `appsettings.json` |
| T-07 | partial | Bỏ cấp quyền MinIO public-read; `minio`, `minio-init`, `pgadmin` chuyển vào profile `tools`; bucket được tạo riêng tư. Không sửa `MinioFileStorageService.cs` theo yêu cầu task; adapter còn cần xóa hoặc viết lại bằng SDK chính thức trước khi dùng. | `docker/docker-compose.yml`, `docs/deployment.md` |
| T-18 | done | Cấu hình `X-Forwarded-For`/`X-Forwarded-Proto`, parser `KnownProxies`/`KnownNetworks` từ config, đặt middleware đầu pipeline; middleware Next.js chuyển tiếp IP khi runtime cung cấp `request.ip`. | `HostingExtensions.cs`, `Program.cs`, `frontend/middleware.ts`, `appsettings.json` |
| T-19 | partial | Thêm security headers backend và CSP, frame policy, referrer/permissions policy frontend; HSTS backend chỉ thêm khi request HTTPS. Kiểm tra DB của `/healthz` thuộc phần persistence task 02, không xử lý tại task này. | `HostingExtensions.cs`, `frontend/next.config.mjs` |
| T-20 | done | Compose dùng `BACKEND_INTERNAL_URL`, backend chỉ `expose` mặc định với tùy chọn publish qua `BACKEND_PORTS`, PostgreSQL bind `127.0.0.1`, upload dùng named volume. | `docker/docker-compose.yml`, `Dockerfile.backend`, `Dockerfile.frontend` |
| T-21 | partial | Kiểm tra `git ls-files` không còn `publish*/`, `.vs/`, `.next-dev/`, `uploads/`, `*.log`, `scratch/`; bổ sung ignore `docker/.env`; thêm CI backend/frontend/Docker image và ESLint config để CI không dừng ở wizard. Không build được Docker image tại máy do thiếu lệnh Docker. | `.gitignore`, `.github/workflows/ci.yml`, `frontend/.eslintrc.json`, `frontend/package*.json` |

## Cổng kiểm tra

| Bước | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build` | pass | 0 warning, 0 error. |
| `dotnet test` | pass | 6/6 test pass. |
| `npx tsc --noEmit` | pass | Chạy lại sau `next build` để tạo `.next/types`. |
| `npm run lint` | pass | Có 7 warning hook/image hiện hữu, không có error. |
| `npm run build` | pass | Build Next.js production pass, gồm route `/change-password`. |
| `docker compose config` | không chạy | Máy kiểm tra không cài lệnh `docker`; đã kiểm tra cấu trúc file và `.env.example` trong source. |
| Kiểm tra file build/IDE/log/upload | pass | `git ls-files` không trả về file thuộc các mẫu bị cấm; `scratch/` không tồn tại. |
| Build Docker image / chạy `/healthz` | không chạy | Không có Docker daemon/CLI tại môi trường hiện tại. |

## Thay đổi schema (cần migration)
- Không có.

## Key config mới

| Key | Mặc định | Ý nghĩa |
|---|---|---|
| `Jwt:Secret` | Rỗng, bắt buộc từ secret store/env | JWT secret tối thiểu 32 byte, không dùng chuỗi mẫu cũ. |
| `ConnectionStrings:Default` | Rỗng, bắt buộc từ secret store/env | Chuỗi kết nối PostgreSQL. |
| `Swagger:Enabled` | `false` | Cho phép bật Swagger ngoài Development khi thật sự cần. |
| `ForwardedHeaders:KnownProxies` | `127.0.0.1` | Danh sách proxy được tin cậy. |
| `ForwardedHeaders:KnownNetworks` | `172.16.0.0/12` | Mạng proxy Docker được tin cậy. |
| `BACKEND_INTERNAL_URL` | `http://backend:5000` trong Compose | URL backend mà Next.js gọi nội bộ. |
| `BACKEND_PORTS` | `[]` | Không publish backend mặc định; chỉ đặt khi cần debug cục bộ. |
| `Storage:Local:Path` | fallback trong app; `/app/uploads` trong Compose | Thư mục lưu upload được giữ bằng named volume. |

## Thay đổi hành vi API / breaking change
- API fail-fast khi thiếu `Jwt:Secret` hoặc `ConnectionStrings:Default`, thay vì khởi động với cấu hình không an toàn.
- Swagger không còn public mặc định ở production.
- Backend không còn được publish cổng 5000 mặc định trong Compose; truy cập frontend qua reverse proxy/BFF.
- Header forwarded chỉ được chấp nhận từ proxy/network đã khai báo.

## Cần phối hợp
- Task 02 cần giữ health check DB trong persistence extension và xác nhận `/healthz` phản ánh trạng thái PostgreSQL sau khi merge.
- Người vận hành phải rotate JWT secret và mật khẩu PostgreSQL cũ từng xuất hiện trong lịch sử Git.
- Cần quyết định xóa `MinioFileStorageService` hay chuyển sang SDK MinIO chính thức có ký SigV4 trước khi bật lưu trữ MinIO.
- Khi có Docker, chạy `docker compose --env-file docker/.env -f docker/docker-compose.yml config`, build hai image, khởi động stack và gọi healthcheck thực tế.

## Phát hiện thêm
- `MinioFileStorageService.cs` vẫn chứa giá trị mặc định và adapter HTTP chưa ký request; không sửa vì task yêu cầu không chạm file này và service hiện không được đăng ký.
- Frontend còn các lint warning hook/image; không thuộc phạm vi hạ tầng và không chặn lint/build.
