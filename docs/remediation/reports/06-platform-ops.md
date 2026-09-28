# Báo cáo: 06 — Hoàn thiện nền dữ liệu, frontend & vận hành

- **Branch:** `chore/platform-ops`
- **Commit cuối (trước commit báo cáo):** `330974d`
- **Ngày:** 2026-09-28

> Ghi chú môi trường: worktree ban đầu được tạo từ commit cũ `86cd795` (chưa có `docs/remediation`). Branch `chore/platform-ops` được đặt lại về `main` (`1877805`) trước khi sửa file đầu tiên; không có thay đổi nào bị mất.

## Kết quả theo mã lỗi

| Mã | Trạng thái | Tóm tắt thay đổi | File chính |
|---|---|---|---|
| T-24 | partial | Thêm `version` vào DTO đọc/ghi. `evaluationService` tự ghi nhớ version của lần đọc gần nhất (kỳ, hồ sơ, nhiệm vụ) và gửi lại trong mọi lệnh cập nhật (kích hoạt kỳ, đổi trạng thái kỳ, Bước 1–5). 409 → thông báo "Dữ liệu đã được người khác cập nhật…", phát sự kiện `evaluations:conflict`; `EvaluationPeriodHeader` hiện cảnh báo kèm nút "Tải lại" (dùng `onRefresh` sẵn có). Form đang nhập được giữ nguyên cho tới khi người dùng chủ động tải lại. | `frontend/services/evaluationService.ts`, `components/evaluations/EvaluationPeriodHeader.tsx` |
| T-26 | skipped | **Chưa xóa được khối DDL**: lệnh xóa bị bộ phân loại quyền của môi trường chạy agent từ chối. Cần người điều phối xóa thủ công (chi tiết ở "Cần phối hợp"). | `Infrastructure/Data/DataSeeder.cs` |
| T-29 | done | Thêm `.gitattributes` (`* text=auto eol=lf`; `*.ps1/*.psm1/*.cmd/*.bat` → CRLF; `*.sh` → LF; docx/xlsx/pdf/ảnh/font/nén/dump → `binary`). Không chạy renormalize. | `.gitattributes` |
| T-39 | done (chưa build kiểm chứng) | `ForwardedHeaders`: dùng `KnownIPNetworks` + `System.Net.IPNetwork.TryParse` thay `KnownNetworks`/`HttpOverrides.IPNetwork` (obsolete). `DocxTemplateEngine.cs` không sửa (task 05 viết lại). | `Api/Extensions/HostingExtensions.cs` |
| T-40 | done (chưa build kiểm chứng) | Log console JSON có scope (ngoài Development) + provider file tự viết: JSON mỗi dòng, xoay vòng theo ngày, xóa file quá `RetainedDays`, ghi nền qua hàng đợi có giới hạn. Middleware correlation id (`X-Request-Id`), gắn `CorrelationId` vào scope log. Docker: thư mục `/var/log/congtacdang` + volume `attech-dangbo-backend-logs`. | `Api/Logging/RollingFileLoggerProvider.cs`, `Api/Middlewares/CorrelationIdMiddleware.cs`, `Program.cs`, `docker/*` |
| T-41 | done | `docker/backup.sh` (pg_dump custom + tar upload + SHA256SUMS + manifest, giữ N bản, tự kiểm tra `pg_restore --list`/`tar tzf`, hỗ trợ `--dry-run`) và `docker/restore.sh` (kiểm checksum, xác nhận gõ `KHOI PHUC`, dừng app, `pg_restore --clean --if-exists --single-transaction`, thay volume upload, khởi động lại). Cập nhật `docs/deployment.md`: lịch khuyến nghị, kiểm tra, khôi phục, mục Log. | `docker/backup.sh`, `docker/restore.sh`, `docs/deployment.md` |
| T-42 (phần 06) | done (chưa chạy) | 6 test (8 trường hợp): nhận/sinh/thay id không hợp lệ, id quá dài, `CorrelationId` có trên mọi dòng log của request (qua file logger thật) và không có ngoài request, xóa file log quá hạn. | `backend/tests/CongTacDang.UnitTests/CorrelationIdMiddlewareTests.cs` |

**T-24 — phần còn lại:**
- Lệnh gọi API cập nhật nằm trong `frontend/app/evaluations/page.tsx` (thuộc task 04). Service tự đính version nên không cần sửa page. Nhưng "tải lại chỉ phần đọc, giữ form" cần tách `loadPeriodData` thành phần đọc/phần form trong page. Hiện nút "Tải lại" gọi `loadPeriodData` nên sẽ đặt lại form; cảnh báo đã nói rõ điều này để người dùng tự quyết định.
- Hồ sơ tập thể (M06–M08) và biên bản (M12–M13) hiện chỉ có API **tạo mới**, không có lệnh cập nhật → chỉ thêm `version` vào kiểu DTO, chưa có luồng gửi. `app/collective-evaluations/page.tsx` không phải sửa.
- `version` trong DTO đọc để tùy chọn (`version?: number`) vì `page.tsx` tự dựng object `EvaluationTaskDto` không có `version`.
- Cơ chế: sau 409, service **không** tự làm mới version. Người dùng bấm lưu lại mà không tải lại sẽ tiếp tục nhận 409, nên không có chuyện ghi đè mà không biết.

**T-26:** xem "Cần phối hợp".

**T-41:** chưa viết bản `.ps1`. Máy chủ triển khai là Ubuntu (`deploy-ubuntu.sh`, `docs/deployment.md`), Docker trên Windows cũng chạy được script bash qua Git Bash/WSL. Viết thêm một bản PowerShell không kiểm thử được sẽ tăng rủi ro hai bản lệch nhau.

**T-40 — vì sao tự viết provider thay vì Serilog:** .NET không có sẵn provider ghi file. Serilog cần thêm 3–4 package (sửa csproj, restore), mà môi trường agent không restore/build được để kiểm chứng. Provider tự viết khoảng 300 dòng, không phụ thuộc gì, dùng cơ chế filter chuẩn (`Logging:File:LogLevel:*` áp dụng qua alias `File`). Nếu sau này cần sink khác (Seq, Elasticsearch), nên chuyển sang Serilog.

## Cổng kiểm tra

| Bước | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build` | **không chạy** | Lệnh bị bộ phân loại quyền của môi trường agent từ chối ("Modify Shared Resources"). Code C# mới **chưa được biên dịch** → người điều phối phải build trước khi merge. |
| `dotnet test` | **không chạy** | Cùng lý do; 6 test mới (8 trường hợp) chưa chạy. |
| `npx tsc --noEmit` | pass | Chạy sau `npm ci`. Worktree mới không có `frontend/next-env.d.ts` (file sinh tự động, nằm trong `.gitignore`), nên tsc báo lỗi `<style jsx>` ở `EvaluationPdfModal.tsx`. Đã tạo file này cục bộ với nội dung chuẩn (không commit), sau đó pass. |
| `bash -n docker/backup.sh docker/restore.sh` | pass | |
| Chạy khô `backup.sh --dry-run` | pass | Thư mục giả trong scratchpad: xoay vòng chỉ chọn thư mục đúng mẫu `congtacdang-YYYYMMDD-HHMMSS`, bỏ qua thư mục khác. |
| Chạy khô `restore.sh --dry-run` | pass | Checksum đúng → OK. File bị sửa → dừng (exit 1). `--skip-uploads` chỉ kiểm `db.dump`. Thiếu tham số → in hướng dẫn (exit 2). Không chạy vào CSDL thật. |
| `docker compose config` | không chạy | Máy không dùng Docker cho task này. |

## Thay đổi schema (cần migration)
- Không có.
- Lưu ý khi xóa DDL (T-26): khối DDL trong seeder đang vá cột/bảng cho **DB cũ đã baseline** (`PersistenceExtensions.BaselineLegacyDatabaseAsync` đánh dấu `InitialCreate` là đã chạy mà không tạo gì). DB cũ đã khởi động ít nhất một lần với seeder hiện tại thì đã đủ cột. DB cũ chưa từng chạy seeder mới sẽ thiếu cột/bảng sau khi xóa DDL. Liên quan T-25.

## Key config mới
| Key | Mặc định | Ý nghĩa |
|---|---|---|
| `Logging:File:Enabled` | `true` | Bật/tắt log ra file |
| `Logging:File:Path` | thư mục `logs` cạnh thư mục publish (Docker: `/var/log/congtacdang`) | Thư mục log |
| `Logging:File:RetainedDays` | `30` | Số ngày giữ file log |
| `Logging:File:FilePrefix` | `congtacdang` | Tên file `{prefix}-yyyyMMdd.log` |
| `Logging:File:QueueCapacity` | `10000` | Số dòng chờ ghi tối đa (đầy thì bỏ dòng cũ nhất) |
| `Logging:File:LogLevel:*` | theo `Logging:LogLevel` | Mức log riêng cho file (filter chuẩn) |
| `Logging:Console:Json` | `true` ngoài Development | Log console dạng JSON có scope |
| `LOG_RETAINED_DAYS` (docker/.env) | `30` | Đã thêm vào `docker/.env.example` và compose |
| `BACKUP_DIR`, `BACKUP_KEEP`, `ENV_FILE`, `COMPOSE_FILE`, `UPLOAD_VOLUME`, `HELPER_IMAGE` | `/var/backups/congtacdang`, `14`, `docker/.env`, `docker/docker-compose.yml`, `attech-dangbo-backend-uploads`, `alpine:3.20` | Biến môi trường của script backup/restore |

## Thay đổi hành vi API / breaking change
- Mọi response có header `X-Request-Id`. Nếu request gửi `X-Request-Id` hợp lệ (≤ 64 ký tự `[A-Za-z0-9-_.:]`) thì dùng lại, không thì sinh mới (GUID 32 ký tự hex). `HttpContext.TraceIdentifier` = correlation id.
- `ForwardedHeaders:KnownNetworks` kiểm tra chặt hơn: CIDR có bit host (ví dụ `10.0.0.1/8`) bị từ chối khi khởi động. Giá trị hiện tại `172.16.0.0/12` hợp lệ.
- Frontend gửi thêm `version` (body hoặc query `?version=`) trong các lệnh cập nhật. Backend đã chấp nhận `version` tùy chọn, nên từ giờ lỗi 409 sẽ thực sự xảy ra khi hai người sửa cùng lúc.
- Log console ở Production chuyển sang dạng JSON.

## Cần phối hợp
- **T-26 (người điều phối):** xóa khối `try { await context.Database.ExecuteSqlRawAsync(@"DO $$ … "); } catch { }` ở đầu `DataSeeder.SeedAsync` (dòng 17–240 trên `1877805`: từ comment `// 0. Chuẩn hóa tên bảng…` tới hết `catch`), giữ nguyên từ `// 1. Seed Permissions` trở đi. Khối này nằm tách biệt với phần đồng bộ quyền (T-33, task 04), nên không xung đột. Trước khi xóa, cần chắc rằng các DB cũ đã baseline đều đã chạy seeder hiện tại ít nhất một lần (xem "Thay đổi schema").
- **Người điều phối:** chạy `dotnet build` và `dotnet test` trên branch này trước khi merge (agent không chạy được).
- **Người điều phối, sau khi merge (T-29):** `git add --renormalize . && git commit -m "chore(T-29): chuẩn hóa xuống dòng theo .gitattributes"`, chạy khi không còn branch nào đang mở, để tránh xung đột.
- **Task 04 / sau merge (T-24):** nếu muốn "chỉ làm mới phần đọc, giữ form" hoàn toàn, tách `loadPeriodData` trong `app/evaluations/page.tsx` thành hai phần: (a) cập nhật `myRecord` / `branchRecords` / `allRecords` / `periods`, (b) khởi tạo state form. Khi 409 chỉ gọi (a). Có thể lắng nghe `EVALUATION_CONFLICT_EVENT` hoặc dùng `isConcurrencyConflict(err)` trong `catch`.
- **Task 03 / người điều phối:** thêm các key `Logging:File:*` vào `appsettings.json` nếu muốn tài liệu hóa trong file (code đã có giá trị mặc định an toàn).
- **Task 05:** warning nullable trong `DocxTemplateEngine.cs` (phần còn lại của T-39) xử lý khi viết lại file.

## Phát hiện thêm
- **Đề xuất T-43 (⚪):** `frontend/next-env.d.ts` bị `.gitignore` nên checkout mới (CI, worktree) chạy `npx tsc --noEmit` trước `next build` sẽ báo lỗi `<style jsx>` ở `EvaluationPdfModal.tsx`. Nên commit file này (Next khuyến nghị) hoặc để CI chạy `next build`/sinh file trước tsc. Vị trí: `.gitignore`, `.github/workflows/ci.yml`.
- **Đề xuất T-44 (🟡):** `restore.sh` khôi phục CSDL xong thì backend sẽ tự migrate khi khởi động (`Database:AutoMigrate`). Bản sao lưu cũ hơn phiên bản code sẽ được migrate lên. Bản sao lưu mới hơn code (rollback) có thể không tương thích. Nên ghi `git_commit` (đã có trong `manifest.txt`) và kiểm tra khớp phiên bản trước khi restore.
- `GlobalExceptionMiddleware` chưa trả correlation id trong body lỗi. Header `X-Request-Id` đã có, nhưng thêm `traceId` vào `ApiResponse` lỗi sẽ giúp người dùng báo lỗi dễ hơn (file thuộc 01/03).
