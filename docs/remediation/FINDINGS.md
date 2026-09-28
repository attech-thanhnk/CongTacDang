# Danh sách lỗi & cải tiến (nguồn duy nhất)

Rà soát ngày 2026-09-28. Mỗi mục có mã cố định, **không đánh lại mã**; mục mới thêm vào cuối nhóm.

- **Mức độ:** 🔴 Nghiêm trọng · 🟠 Cao · 🟡 Trung bình · ⚪ Thấp
- **Trạng thái:** `open` · `in-progress` · `done` · `partial` · `deferred` · `wontfix`
- **Owner:** task file phụ trách (`tasks/0x-*.md`)

Agent **không sửa file này**. Người điều phối cập nhật trạng thái sau khi đọc báo cáo trong `reports/`.

## T — Kỹ thuật

| Mã | Mức | Vấn đề | Vị trí chính | Owner | Trạng thái |
|---|---|---|---|---|---|
| T-01 | 🔴 | Path traversal: `formCode` từ người dùng đưa thẳng vào đường dẫn lưu file | `Application/Services/AttachmentService.cs`, `Infrastructure/Services/LocalFileStorageService.cs` | 01 | open |
| T-02 | 🔴 | Stored XSS: ContentType lấy từ client, không kiểm tra magic bytes, `/view` trả inline cùng origin, frontend `window.open(blobUrl)` | `AttachmentService.cs`, `Api/Controllers/AttachmentController.cs`, `frontend/components/attachments/DocumentViewerModal.tsx` | 01 | open |
| T-03 | 🔴 | Không có đổi/đặt lại mật khẩu; mọi tài khoản mặc định `123456` | `DataSeeder.cs`, `UserService.cs`, `AuthController.cs` | 01 | open |
| T-04 | 🔴 | Đăng nhập không có rate limit / khóa tài khoản; lệch thời gian phản hồi khi username không tồn tại | `AuthService.cs`, `Program.cs` | 01 | open |
| T-05 | 🔴 | Secret trong repo (JWT secret, mật khẩu DB, IP nội bộ, mật khẩu compose) | `Api/appsettings.json`, `docker/docker-compose.yml` | 03 | open |
| T-06 | 🟠 | Swagger luôn bật ở production (`IsDevelopment() \|\| true`) | `Program.cs` | 03 | open |
| T-07 | 🟠 | MinIO: bucket public-read, adapter gửi request không ký SigV4, không được đăng ký dùng | `docker-compose.yml`, `MinioFileStorageService.cs` | 03 | open |
| T-08 | 🟠 | Thiếu quyền trả 401 thay vì 403 → apiClient refresh + retry → người dùng bị đăng xuất | `Api/Middlewares/GlobalExceptionMiddleware.cs`, các Application service, `frontend/services/apiClient.ts` | 01 | open |
| T-09 | 🟠 | Refresh token lưu nguyên văn; rotation không atomic; nhiều tab refresh cùng lúc bị coi là tái sử dụng → thu hồi toàn bộ phiên | `AuthService.cs`, `RefreshTokenRepository.cs`, `AuthController.cs` | 01 | open |
| T-10 | 🟠 | `InvalidOperationException` map thành 400 và lộ message nội bộ (EF/LINQ) | `GlobalExceptionMiddleware.cs` | 01 | open |
| T-11 | 🟡 | `UploadFile` tự catch mọi exception và trả `ex.Message` trong 500 | `AttachmentController.cs` | 01 | open |
| T-12 | 🟡 | `GetDownloadUrlAsync` trả `/api/attachments/download-by-key` — endpoint không tồn tại | `LocalFileStorageService.cs` | 01 | open |
| T-13 | 🔴 | Dùng `EnsureCreatedAsync`, không có EF Migrations; lỗi kết nối DB bị nuốt khi khởi động | `Program.cs` | 02 | open |
| T-14 | 🟠 | Mỗi thao tác repository tự `SaveChanges` — không có Unit of Work / transaction cho thao tác nhiều bước | `GenericRepository.cs`, `EvaluationService.SetActivePeriodAsync`, … | 02 | open |
| T-15 | 🟠 | Không có concurrency token — nhiều cấp cùng sửa hồ sơ thì người sau ghi đè | `CongTacDangDbContext.cs`, entities đánh giá | 02 | open |
| T-16 | 🟡 | Phiên bản gói lệch: net10.0 nhưng EF Core/Npgsql 9.x, JwtBearer preview, Swashbuckle 6.5 | `*.csproj` | 02 | open |
| T-17 | 🟡 | Upload đọc cả file vào `MemoryStream` + `ToArray()` (≈2× dung lượng file trong RAM) | `AttachmentService.cs` | 01 | open |
| T-18 | 🟡 | Chưa cấu hình ForwardedHeaders → IP trong audit/refresh token là IP của proxy Next.js | `Program.cs` | 03 | open |
| T-19 | 🟡 | Thiếu security headers (CSP, frame-ancestors, nosniff, Referrer-Policy); `/healthz` không kiểm tra DB | `Program.cs`, `frontend/next.config.mjs` | 03 (DB check: 02) | open |
| T-20 | 🟡 | Compose đặt `NEXT_PUBLIC_API_URL=http://localhost:5000/api` → trình duyệt bỏ qua proxy; biến NEXT_PUBLIC chỉ có hiệu lực lúc build | `docker/docker-compose.yml` | 03 | open |
| T-21 | 🟡 | Repo theo dõi file build/IDE/log/upload thật; chưa có test, chưa có CI | `.gitignore`, `backend/publish*`, `.vs/`, … | 03 (test project: 01) | open |
| T-22 | ⚪ | Frontend dùng 3 bộ UI (Bootstrap, Tailwind, PrimeReact) + 3 bộ icon; nhiều file > 1000 dòng | `frontend/` | — | deferred |
| T-23 | ⚪ | Truy vấn chỉ đọc thiếu `AsNoTracking`, thiếu index cho cột lọc thường dùng | `SpecificRepositories.cs`, `CongTacDangDbContext.cs` | 02 | open |

## B — Nghiệp vụ (để xử lý sau)

| Mã | Mức | Vấn đề | Vị trí chính | Owner | Trạng thái |
|---|---|---|---|---|---|
| B-01 | 🟠 | `UpdatePeriodStatusAsync` nhận bất kỳ trạng thái nào (lùi bước được); chưa có state machine cho kỳ/hồ sơ | `EvaluationService.cs` | — | deferred |
| B-02 | 🟠 | File đính kèm không giới hạn phạm vi: ai có `AttachmentsRead` cũng xem được minh chứng của mọi cán bộ | `AttachmentService.cs`, `AttachmentController.cs` | — | deferred |
| B-03 | 🟠 | Bỏ phiếu kín "không lưu User ID" cần kiểm chứng — audit log tự ghi `ActorId` cho mọi entity thêm mới | `CongTacDangDbContext.PrepareAuditEntries`, `CollectiveEvaluationService.cs` | — | deferred |
| B-04 | 🟡 | Quy tắc 70đ, ≥30% minh chứng vượt chuẩn, trần 20% Xuất sắc, chênh lệch ≥5đ cần đối chiếu Hướng dẫn 03 và có unit test | `EvaluationService.cs`, `CollectiveEvaluationService.cs` | — | deferred |
| B-05 | ⚪ | Chưa có tài liệu hướng dẫn sử dụng (HDSD cũ mô tả sai route đã bị xóa); cần viết lại theo route thực tế | `docs/` | — | deferred |
