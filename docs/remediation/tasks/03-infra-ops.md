# Task 03 — Hạ tầng, Cấu hình & Vận hành

- **Branch:** `chore/infra-ops`
- **Mã lỗi:** T-05, T-06, T-07, T-18, T-19 (trừ kiểm tra DB), T-20, T-21 (trừ project test)
- **Báo cáo:** `docs/remediation/reports/03-infra-ops.md`
- Tuân thủ `docs/remediation/RULES.md`. Không sửa service, repository, entity, DbContext, `*.csproj`, `frontend/services/*`, component React.

## T-05 Secret & cấu hình
- Xóa khỏi `appsettings.json`: JWT secret, chuỗi kết nối có mật khẩu, IP nội bộ, key MinIO. Giữ placeholder rỗng; giá trị thật lấy từ biến môi trường / `dotnet user-secrets`.
- Khi khởi động: `Jwt:Secret` phải ≥ 32 byte và không phải chuỗi mẫu cũ; phải có `ConnectionStrings:Default`. Thiếu → dừng với thông báo rõ ràng. Logic trong `Api/Extensions/HostingExtensions.cs`.
- `appsettings.Development.json` + tài liệu: hướng dẫn `dotnet user-secrets` cho dev.
- `docker/.env.example`; `docker-compose.yml` đọc mọi mật khẩu từ `.env`, bỏ giá trị mặc định chứa mật khẩu thật.
- Báo cáo phải ghi: secret cũ đã nằm trong lịch sử git → cần **rotate** JWT secret và mật khẩu DB. **Không** viết lại lịch sử git.

## T-06 Swagger
- Bỏ `|| true`; chỉ bật ở Development hoặc khi `Swagger:Enabled=true`.

## T-07 MinIO
- Bỏ `mc anonymous set download` (bucket public-read). Đưa `minio`, `minio-init`, `pgadmin` vào profile `tools` (không chạy mặc định).
- Không sửa `MinioFileStorageService.cs` (không được đăng ký dùng); đề xuất trong báo cáo: xóa hẳn hay viết lại bằng SDK chính thức.

## T-18 ForwardedHeaders
- Cấu hình `ForwardedHeaders` (X-Forwarded-For, X-Forwarded-Proto), `KnownProxies`/`KnownNetworks` từ config, đặt đầu pipeline, để `RemoteIpAddress` là IP người dùng thật.
- Kiểm tra Next.js rewrites có chuyển tiếp `X-Forwarded-For` không. Nếu không, cài cách khắc phục trong phạm vi file của task này (ví dụ `frontend/middleware.ts` gắn header) hoặc ghi đề xuất.

## T-19 Security headers
- Backend: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY` / `frame-ancestors 'none'`, `Referrer-Policy: no-referrer`. HSTS chỉ khi HTTPS.
- `frontend/next.config.mjs` (`headers()`): CSP phù hợp với Bootstrap/PrimeReact/next/font đang dùng (lưu ý iframe xem PDF dùng `blob:` → `frame-src 'self' blob:`), `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`. Chạy `npm run build && npm start` hoặc dev để chắc CSP không làm hỏng trang (không có lỗi CSP trong console ở trang login và evaluations).

## T-20 Docker
- Bỏ `NEXT_PUBLIC_API_URL=http://localhost:5000/api`; dùng `BACKEND_INTERNAL_URL=http://backend:5000` (khớp `next.config.mjs`).
- Backend không publish cổng 5000 ra ngoài (chỉ `expose`) trừ khi bật qua biến môi trường; Postgres chỉ bind `127.0.0.1`.
- Volume cho thư mục upload của backend (`Storage:Local:Path`), để file không mất khi build lại container.
- Dockerfile: chạy bằng user không phải root; `HEALTHCHECK` (backend `/healthz`); frontend dùng `npm ci` + `package-lock.json`.
- Rà `docker/deploy-ubuntu.sh` cho khớp.

## T-21 Dọn repo & CI
- Phần gỡ file build/IDE/log/upload khỏi git và cập nhật `.gitignore` **đã làm trên `main`** trước khi chạy agent. Chỉ kiểm tra lại bằng `git ls-files` rằng không còn file thuộc `publish*/`, `.vs/`, `.next-dev/`, `uploads/`, `*.log`; bổ sung `.gitignore` cho `docker/.env` nếu chưa có.
- `scratch/`: xem nội dung, ghi đề xuất trong báo cáo, **không tự xóa**.
- `.github/workflows/ci.yml`:
  - Backend: setup .NET 10 → restore → build → `dotnet test backend/CongTacDang.slnx` (phải chạy được cả khi chưa có project test).
  - Frontend: Node 20 → `npm ci` → `npx tsc --noEmit` → `npm run lint` → `npm run build`.
  - Build 2 Docker image (không push).

## Tài liệu
- Tạo `docs/deployment.md`: biến môi trường bắt buộc, cách sinh secret, chạy compose (kèm profile `tools`), backup Postgres + thư mục upload, rotate secret.
- `docs/deployment.md` là tài liệu triển khai duy nhất (HDSD cũ đã bị xóa có chủ ý — không tạo lại).

## Kiểm tra thêm
- `docker compose -f docker/docker-compose.yml config` hợp lệ (kèm `.env` tạo từ `.env.example`).
- Nếu có Docker: `docker compose up -d` và gọi `/healthz` qua frontend/backend; ghi kết quả.
- `cd frontend && npm run build` pass.
