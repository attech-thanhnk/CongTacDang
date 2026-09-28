# Danh sách lỗi & cải tiến (nguồn duy nhất)

Rà soát ngày 2026-09-28. Mỗi mục có mã cố định, **không đánh lại mã**; mục mới thêm vào cuối nhóm.

- **Mức độ:** 🔴 Nghiêm trọng · 🟠 Cao · 🟡 Trung bình · ⚪ Thấp
- **Trạng thái:** `open` · `in-progress` · `done` · `partial` · `deferred` · `wontfix`
- **Owner:** task file phụ trách (`tasks/0x-*.md`)

Agent **không sửa file này**. Người điều phối cập nhật trạng thái sau khi đọc báo cáo trong `reports/`.

**Đợt 1 (2026-09-28)** — merge `ddd45b5`. Trạng thái `done` dựa trên báo cáo agent + build/test/tsc trên bản gộp; chưa có review độc lập từng mã lỗi, chưa chạy với PostgreSQL/Docker thật. Khi tích hợp đã sửa thêm 3 lỗi: migration `InitialCreate` lệch model (tách `AuthHardening`), `version` bắt buộc làm mọi cập nhật trả 409, cookie phiên không nhìn thấy được từ middleware Next.js.
- T-07 `partial`: đã bỏ public-read và đưa MinIO vào profile `tools`; `MinioFileStorageService` vẫn cần xóa hoặc viết lại bằng SDK.
- T-15 `partial`: backend đã có `xmin` + `version`; còn thiếu phía frontend (T-24).

**Đợt 2 (2026-09-28)** — task 04 + 06 merge vào `main`, build 0 lỗi, 37/37 test, `tsc` pass, chạy thật trên CSDL thử nghiệm: tải biểu mẫu của người khác → 403, Excel theo vai trò, không xem profile người khác, có `X-Request-Id`, log ra file. Khi tích hợp đã sửa thêm T-49 (version luôn 0 trên danh sách) và T-50 (logger không dispose). Migration mới: `AttachmentUploadedBy`.
- T-24 `partial`: service tự gửi version + cảnh báo 409; nút "Tải lại" còn đặt lại toàn bộ form (`app/evaluations/page.tsx`).
- T-26 `open`: agent bị chặn khi xóa khối DDL; chờ quyết định (giữ tới khi làm T-25, hay xóa ngay).
- T-30 `partial`: Excel 14/15/16 mới giới hạn theo vai trò (Cán bộ, Bí thư Chi bộ → 403); lọc theo Chi bộ cần sửa `ReportService` (task 05).
- T-35: đã xóa `sign.ps1`; nếu từng chạy script, cần gỡ tay chứng chỉ `CN=CongTacDangLocal` khỏi `CurrentUser\Root` và `CurrentUser\My` (hướng dẫn trong `reports/04-authz.md`).
- T-39 `partial`: còn warning nullable trong `DocxTemplateEngine` (task 05 viết lại file này).

## T — Kỹ thuật

| Mã | Mức | Vấn đề | Vị trí chính | Owner | Trạng thái |
|---|---|---|---|---|---|
| T-01 | 🔴 | Path traversal: `formCode` từ người dùng đưa thẳng vào đường dẫn lưu file | `Application/Services/AttachmentService.cs`, `Infrastructure/Services/LocalFileStorageService.cs` | 01 | done |
| T-02 | 🔴 | Stored XSS: ContentType lấy từ client, không kiểm tra magic bytes, `/view` trả inline cùng origin, frontend `window.open(blobUrl)` | `AttachmentService.cs`, `Api/Controllers/AttachmentController.cs`, `frontend/components/attachments/DocumentViewerModal.tsx` | 01 | done |
| T-03 | 🔴 | Không có đổi/đặt lại mật khẩu; mọi tài khoản mặc định `123456` | `DataSeeder.cs`, `UserService.cs`, `AuthController.cs` | 01 | done |
| T-04 | 🔴 | Đăng nhập không có rate limit / khóa tài khoản; lệch thời gian phản hồi khi username không tồn tại | `AuthService.cs`, `Program.cs` | 01 | done |
| T-05 | 🔴 | Secret trong repo (JWT secret, mật khẩu DB, IP nội bộ, mật khẩu compose) | `Api/appsettings.json`, `docker/docker-compose.yml` | 03 | done |
| T-06 | 🟠 | Swagger luôn bật ở production (`IsDevelopment() \|\| true`) | `Program.cs` | 03 | done |
| T-07 | 🟠 | MinIO: bucket public-read, adapter gửi request không ký SigV4, không được đăng ký dùng | `docker-compose.yml`, `MinioFileStorageService.cs` | 03 | partial |
| T-08 | 🟠 | Thiếu quyền trả 401 thay vì 403 → apiClient refresh + retry → người dùng bị đăng xuất | `Api/Middlewares/GlobalExceptionMiddleware.cs`, các Application service, `frontend/services/apiClient.ts` | 01 | done |
| T-09 | 🟠 | Refresh token lưu nguyên văn; rotation không atomic; nhiều tab refresh cùng lúc bị coi là tái sử dụng → thu hồi toàn bộ phiên | `AuthService.cs`, `RefreshTokenRepository.cs`, `AuthController.cs` | 01 | done |
| T-10 | 🟠 | `InvalidOperationException` map thành 400 và lộ message nội bộ (EF/LINQ) | `GlobalExceptionMiddleware.cs` | 01 | done |
| T-11 | 🟡 | `UploadFile` tự catch mọi exception và trả `ex.Message` trong 500 | `AttachmentController.cs` | 01 | done |
| T-12 | 🟡 | `GetDownloadUrlAsync` trả `/api/attachments/download-by-key` — endpoint không tồn tại | `LocalFileStorageService.cs` | 01 | done |
| T-13 | 🔴 | Dùng `EnsureCreatedAsync`, không có EF Migrations; lỗi kết nối DB bị nuốt khi khởi động | `Program.cs` | 02 | done |
| T-14 | 🟠 | Mỗi thao tác repository tự `SaveChanges` — không có Unit of Work / transaction cho thao tác nhiều bước | `GenericRepository.cs`, `EvaluationService.SetActivePeriodAsync`, … | 02 | done |
| T-15 | 🟠 | Không có concurrency token — nhiều cấp cùng sửa hồ sơ thì người sau ghi đè | `CongTacDangDbContext.cs`, entities đánh giá | 02 | done |
| T-16 | 🟡 | Phiên bản gói lệch: net10.0 nhưng EF Core/Npgsql 9.x, JwtBearer preview, Swashbuckle 6.5 | `*.csproj` | 02 | done |
| T-17 | 🟡 | Upload đọc cả file vào `MemoryStream` + `ToArray()` (≈2× dung lượng file trong RAM) | `AttachmentService.cs` | 01 | done |
| T-18 | 🟡 | Chưa cấu hình ForwardedHeaders → IP trong audit/refresh token là IP của proxy Next.js | `Program.cs` | 03 | done |
| T-19 | 🟡 | Thiếu security headers (CSP, frame-ancestors, nosniff, Referrer-Policy); `/healthz` không kiểm tra DB | `Program.cs`, `frontend/next.config.mjs` | 03 (DB check: 02) | done |
| T-20 | 🟡 | Compose đặt `NEXT_PUBLIC_API_URL=http://localhost:5000/api` → trình duyệt bỏ qua proxy; biến NEXT_PUBLIC chỉ có hiệu lực lúc build | `docker/docker-compose.yml` | 03 | done |
| T-21 | 🟡 | Repo theo dõi file build/IDE/log/upload thật; chưa có test, chưa có CI | `.gitignore`, `backend/publish*`, `.vs/`, … | 03 (test project: 01) | done |
| T-22 | ⚪ | Frontend dùng 3 bộ UI (Bootstrap, Tailwind, PrimeReact) + 3 bộ icon; nhiều file > 1000 dòng | `frontend/` | — | deferred |
| T-23 | ⚪ | Truy vấn chỉ đọc thiếu `AsNoTracking`, thiếu index cho cột lọc thường dùng | `SpecificRepositories.cs`, `CongTacDangDbContext.cs` | 02 | done |
| T-24 | 🟠 | Frontend chưa gửi `version` khi cập nhật hồ sơ/kỳ đánh giá → kiểm tra concurrency (T-15) chưa có hiệu lực; backend tạm coi `version` là tùy chọn | `frontend/services/evaluationService.ts`, `components/evaluations/Step*.tsx` | 06 | partial |
| T-25 | 🟠 | Migration chưa chạy thử trên PostgreSQL thật (DB trống + DB cũ baseline); DB cũ baseline sẽ thiếu các index mới nằm trong `InitialCreate` (kể cả unique index kỳ đang hoạt động) | `Infrastructure/Data/Migrations`, `Api/Extensions/PersistenceExtensions.cs` | — | open |
| T-26 | 🟡 | `DataSeeder` còn khối DDL `ALTER TABLE … ADD COLUMN IF NOT EXISTS` tương thích schema cũ — trùng vai trò với migration | `Infrastructure/Data/DataSeeder.cs` | 06 | open |
| T-27 | 🟡 | Docker chưa được kiểm chứng: chưa `docker compose config`, chưa build image, chưa gọi `/healthz` (máy không có Docker) | `docker/`, `.github/workflows/ci.yml` | — | open |
| T-28 | 🟡 | Vận hành: JWT secret và mật khẩu DB cũ đã nằm trong lịch sử git → phải rotate trước khi triển khai | môi trường triển khai | — | open |
| T-29 | ⚪ | Chưa có `.gitattributes` — file LF/CRLF lẫn lộn (cảnh báo khi commit trên Windows) | gốc repo | 06 | done |
| T-30 | 🔴 | Xuất Word `/api/reports/docx/*` chỉ yêu cầu đăng nhập, không kiểm tra phạm vi → ai cũng tải được Mẫu 01/02/10 của người khác, Mẫu 11/13 của mọi Chi bộ; Excel 14/15/16 chỉ cần `reports.export` (CAN_BO có sẵn) và xuất toàn Đảng bộ | `Api/Controllers/ExportReportController.cs`, `Infrastructure/Services/ReportService.cs` | 04 | partial |
| T-31 | 🔴 | File đính kèm không có chủ sở hữu (`UploadedBy` là tên dạng chữ) và không kiểm tra phạm vi khi liệt kê/xem/tải/xóa → ai có `attachments.read` xem được mọi minh chứng | `TaskAttachment.cs`, `AttachmentService.cs`, `AttachmentController.cs` | 04 | done |
| T-32 | 🟠 | `GET /api/users/profile?username=` trả hồ sơ + roles/permissions của người khác; `GET /api/organizations/departments` không có policy; chưa có cơ chế kiểm tra quyền theo đối tượng dùng chung (mỗi service tự kiểm tra một kiểu) | `UserController.cs`, `OrganizationController.cs`, các Application service | 04 | done |
| T-33 | 🟠 | Seeder đồng bộ lại quyền của mọi role mỗi lần khởi động (khi `SeedSampleData=true`) → ghi đè thay đổi phân quyền làm qua giao diện | `Infrastructure/Data/DataSeeder.cs` | 04 | done |
| T-34 | 🟡 | Frontend dùng quyền `evaluations.branch_review` không tồn tại ở backend; hiển thị bước theo quyền không khớp backend | `frontend/app/evaluations/page.tsx`, `EvaluationStepNav.tsx` | 04 | done |
| T-35 | 🟠 | `backend/sign.ps1` tự tạo chứng chỉ và thêm vào kho **Trusted Root** của Windows để ký file build | `backend/sign.ps1` | 04 | done |
| T-36 | 🟠 | Mô hình file: mỗi nhiệm vụ chỉ 1 file, không có phiên bản (thay file = mất bản cũ), không gắn đối tượng tổng quát; adapter MinIO không dùng được (phần còn lại của T-07) | `TaskAttachment.cs`, `EvaluationTask.AttachmentId`, `MinioFileStorageService.cs` | 05 | open |
| T-37 | 🔴 | Bộ sinh Word thay `{{TAG}}` theo từng đoạn chữ (run) → hỏng khi Word tách run; file phôi sinh bằng code; nhiều chữ in cứng (giờ họp, khối, tên đơn vị) | `Infrastructure/Services/DocxTemplateEngine.cs` | 05 | open |
| T-38 | 🟠 | Không có xuất PDF phía máy chủ (Hướng dẫn yêu cầu nộp PDF kèm Word/Excel); 3 đường hiển thị biểu mẫu độc lập (Word, Excel, HTML in bằng `window.print`) có thể ra số khác nhau (ví dụ Mẫu 02 tính lại điểm theo trọng số Khung 2 cố định) | `DocxTemplateEngine.cs`, `ReportService.cs`, `frontend/components/evaluations/EvaluationPrintTemplate.tsx` | 05 | open |
| T-39 | ⚪ | Warning build: `KnownNetworks`/`IPNetwork` obsolete, nullable trong `DocxTemplateEngine` | `HostingExtensions.cs`, `DocxTemplateEngine.cs` | 06 | partial |
| T-40 | 🟡 | Log chỉ ra console: chưa có log ra file có xoay vòng, chưa có correlation id để lần theo một yêu cầu | `Api/Program.cs` | 06 | done |
| T-41 | 🟡 | Chưa có script sao lưu/khôi phục (PostgreSQL + thư mục upload) đã được thử nghiệm | `docker/`, `docs/deployment.md` | 06 | done |
| T-42 | 🟡 | Test mỏng (7 unit test, chỉ phần bảo mật); chưa có integration test cho API | `backend/tests/` | 04/05/06 | partial |

| T-43 | 🟡 | `frontend/next-env.d.ts` nằm trong `.gitignore` → checkout mới / CI chạy `tsc --noEmit` trước `next build` báo lỗi `<style jsx>` | `.gitignore`, `frontend/` | — | open |
| T-44 | 🟡 | Khôi phục bản sao lưu của phiên bản code cũ trong khi app bật `AutoMigrate` → migrate tự chạy lên schema mới ngay khi khởi động; cần quy trình khôi phục kèm đúng phiên bản code | `docker/restore.sh`, `docs/deployment.md` | — | open |
| T-45 | 🟡 | Người có `branch_vote` nhưng chưa gán Chi bộ xem được mọi biên bản họp trong kỳ | `Application/Common/Security/AccessPolicy.cs`, `CollectiveEvaluationService.cs` | — | open |
| T-46 | 🟡 | Seeder vẫn gán lại role cho người dùng mỗi lần khởi động khi `SeedSampleData=true` | `Infrastructure/Data/DataSeeder.cs` | — | open |
| T-47 | 🟡 | `EvaluationTask.AttachmentId` gửi từ client không được kiểm tra (file có tồn tại, có thuộc quyền người gửi) | `EvaluationService.RegisterTasksAsync` | 05 | open |
| T-48 | 🟠 | `SubmitAppraisalAsync` không kiểm tra quyền trên từng hồ sơ (chỉ dựa vào policy của controller) | `EvaluationService.cs` | — | open |
| T-49 | 🔴 | `xmin` khai báo dạng shadow property → truy vấn `AsNoTracking` (danh sách hồ sơ, kỳ đang hoạt động) trả `version: 0` → bước 3–5 luôn nhận 409 khi frontend gửi version. Phát hiện khi chạy thật lúc tích hợp đợt 2 | `CongTacDangDbContext.cs`, `UnitOfWork.cs` | — | done |
| T-50 | 🟡 | File logger đăng ký bằng instance nên không được dispose khi tắt ứng dụng → mất các dòng log cuối. Phát hiện nhờ test khi tích hợp | `Api/Logging/RollingFileLoggerProvider.cs` | — | done |
| T-51 | ⚪ | Tài liệu hướng dẫn seed sẵn (không có người tải lên, không gắn hồ sơ) sau T-31 chỉ quản trị hệ thống xem được — cần quyết định cách phân loại "văn bản chung" | `DataSeeder.cs`, `AccessPolicy.cs` | — | open |

## B — Nghiệp vụ (để xử lý sau)

| Mã | Mức | Vấn đề | Vị trí chính | Owner | Trạng thái |
|---|---|---|---|---|---|
| B-01 | 🟠 | `UpdatePeriodStatusAsync` nhận bất kỳ trạng thái nào (lùi bước được); chưa có state machine cho kỳ/hồ sơ | `EvaluationService.cs` | — | deferred |
| B-02 | 🟠 | File đính kèm không giới hạn phạm vi: ai có `AttachmentsRead` cũng xem được minh chứng của mọi cán bộ | `AttachmentService.cs`, `AttachmentController.cs` | — | deferred (cơ chế kiểm tra phạm vi: T-31) |
| B-03 | 🟠 | Bỏ phiếu kín "không lưu User ID" cần kiểm chứng — audit log tự ghi `ActorId` cho mọi entity thêm mới | `CongTacDangDbContext.PrepareAuditEntries`, `CollectiveEvaluationService.cs` | — | deferred |
| B-04 | 🟡 | Quy tắc 70đ, ≥30% minh chứng vượt chuẩn, trần 20% Xuất sắc, chênh lệch ≥5đ cần đối chiếu Hướng dẫn 03 và có unit test | `EvaluationService.cs`, `CollectiveEvaluationService.cs` | — | deferred |
| B-05 | ⚪ | Chưa có tài liệu hướng dẫn sử dụng (HDSD cũ mô tả sai route đã bị xóa); cần viết lại theo route thực tế | `docs/` | — | deferred |
