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

**Làm sạch (2026-09-28)** — quyết định: không giữ dữ liệu/CSDL cũ nào. Gộp toàn bộ migration thành một `InitialCreate`; bỏ logic baseline CSDL cũ (T-25 → `wontfix`), khối DDL trong seeder (T-26), đoạn nâng cấp mật khẩu plain-text và file đính kèm giả được seed (T-51). CSDL `congtacdang_test` được tạo lại từ đầu.

**Task 05 (2026-09-28)** — merge vào `main`, build 0 lỗi 0 warning, 74/75 test (1 test PDF bỏ qua vì máy không có LibreOffice), `tsc` pass. Schema mới gộp vào `InitialCreate` (chưa có CSDL triển khai cần giữ). Chạy thật trên CSDL thử nghiệm: xuất Word Mẫu 01/02/10 hợp lệ, không còn `{{TAG}}`; PDF trả 503 khi thiếu LibreOffice; Excel 14 theo `periodId`; tải phiên bản mới giữ bản cũ, tải được từng phiên bản.
- T-07 `partial`: agent bị môi trường chặn khi xóa `MinioFileStorageService.cs`; còn file này, khối `Storage:Minio` trong `appsettings.json`, dịch vụ `minio`/`minio-init` trong compose, biến `MINIO_*` trong `.env.example` — chờ xác nhận xóa.
- T-38 `partial`: chưa chạy chuyển PDF thật (cần máy/Docker có LibreOffice).
- Bố cục Word thay đổi nhỏ so với bản cũ (số quý, dấu thập phân, các ô trước đây in giá trị bịa/sai nay để trống) — xem `reports/05-files-docs.md`.

**Đợt 3 (2026-09-28)** — task 07 merge vào `main` (`6b42ba1`), build 0 lỗi 0 warning, unit 95/96 (1 skip PDF), integration 13/13 trên PostgreSQL `192.168.22.159` (CSDL tạm `ctd_it_*`, đã dọn), `tsc` pass. Migration `Wave3` có chuyển dữ liệu `IsApprovedByAttech → ApprovalAuthority`, `SecurityStamp` ngẫu nhiên từng dòng; đã áp dụng thử toàn bộ migration trên CSDL trống ở 159, `has-pending-model-changes` sạch. Phân quyền đọc từ CSDL mỗi request (bỏ đọc claim), 26 policy cũ khớp trên mọi tổ hợp vai trò.
- T-55 `in-progress`: phần nền xong; resolver/guard thật, bỏ `AppRoles`/`AppPermissions` là task 09.
- Ghi nhận cho đợt sau: `/api/auth/login` giới hạn 10 lần/phút/IP — test tích hợp phải tái sử dụng client đã đăng nhập; username chưa chuẩn hóa hoa/thường (task 08); JSON trả tiếng Việt dạng `\uXXXX`.

**Đợt 4–5 + dọn sạch (2026-09-28)** — task 08, 09, 10, tích hợp Đợt 4, task 11, 12, 13 và đợt dọn sạch cuối đã merge vào `main`. Người điều phối kiểm lại trên bản gộp: build 0 lỗi 0 warning, unit 193/194 (1 skip PDF thiếu LibreOffice), integration 72/72 trên PostgreSQL `192.168.22.159` (0 skip), `tsc` + `npm run build` pass, grep không còn tên vai trò/mã quyền cũ/MinIO (trừ hằng số nội bộ vai trò quản trị mặc định trong `DataSeeder`). Migration gộp lại thành một `InitialCreate` (không giữ dữ liệu/CSDL cũ theo quyết định chủ dự án); `has-pending-model-changes` sạch.
- T-65 `partial`: giao diện quản trị mới chỉ kiểm bằng `tsc`/build, chưa thao tác trên trình duyệt.
- B-07 `partial`: đã điền thêm Mẫu 10 (nhận xét cấp trực tiếp sử dụng), Mẫu 13 (phiếu không hợp lệ, số có mặt); ô còn trống liệt kê trong `reports/12-evaluation-workflow.md`.
- T-27 vẫn `open`: chưa build image / chạy compose (máy không có Docker). T-38: chưa chạy PDF thật. Khi chạy thử app đã phát hiện và sửa T-69 (proxy Docker trỏ sai backend).
- Chạy thử app thật (backend + `next start` qua proxy, CSDL tạm trên 159, dữ liệu mẫu): 25/26 kịch bản API đạt ngay, kịch bản còn lại do script thiếu `periodId`, chạy lại đạt. Đã kiểm: bắt đổi mật khẩu phía máy chủ, `grants`, tạo tài khoản → đăng nhập, gán vai trò có hiệu lực ngay, chống tự khóa, khóa → 401 ngay, cán bộ thường bị chặn danh sách tài khoản, import, và work-queue của 8 tài khoản mẫu đúng vai (kể cả chặn tự duyệt hồ sơ của mình).
- Sau khi triển khai bản này mọi người phải đăng nhập lại (JWT mới có `sstamp`).

**Đợt 8 (2026-09-29)** — task 18 (09B/09C/9D), task 19 (07/08/12/14/15A/15B/16, trang Báo cáo), task 20 (công khai kết quả, kiến nghị, Mẫu 17, nhắc việc) và tích hợp đã merge (`a0c8a71`); bỏ trang `/forms` và `/attachments`. Người điều phối kiểm lại: build 0 lỗi 0 warning, unit 306/307 (1 skip PDF), integration 100/100 trên `192.168.22.159`, `tsc` pass; khôi phục 2 kiểm tra 404 báo cáo cũ mà agent tích hợp đã bỏ để khớp grep. Migration gộp lại `InitialCreate` mới → **CSDL cũ phải tạo lại**. Template mới dựng từ biểu mẫu gốc; Mẫu 13 làm lại đúng mẫu (mục I/II, cột "Chưa đánh giá"); Mẫu 08 thêm bản Excel.
- Cần nghiệp vụ xác nhận: 09A/09B/9D có phải nộp bản Excel (HD03 V.1) không; Mẫu 10 có áp dụng Q3/2026; 09C bắt buộc nhập, giới hạn ~6.000 ký tự ≈ 02 trang A4; cách đếm 15A/15B/16 khi kỳ chưa kết thúc; hạn và người xử lý kiến nghị; người lập/duyệt Mẫu 17, mức bắt buộc, cửa sổ 90 ngày; công khai điểm hay chỉ mức (xem `reports/wave8-integration.md`).
- Chưa làm (⚪): ngày sinh cán bộ trên 09A/09B/09C; tự điền "Căn cứ Hướng dẫn số …"; Mẫu 03–06, 18–20 (chờ nghiệp vụ có áp dụng không).

**Gỡ import (2026-09-29)** — theo quyết định chủ dự án, **bỏ hẳn chức năng nhập từ Excel** (trang `/imports`, API `/api/imports`, khung import, 7 loại import, quyền `system.import`, `proxyTimeout` 5 phút). Go-live khai báo qua giao diện: Danh mục → Tài khoản → Vai trò & phạm vi → Kỳ đánh giá (`docs/deployment.md`). Kịch bản go-live, cây đơn vị, W10, G4 viết lại dựng dữ liệu bằng API thường. Kiểm lại: build 0 warning, unit 244/245 (1 skip PDF), integration 88/88, `tsc` pass, grep sạch. T-64, T-73 (phần import): đóng theo quyết định bỏ chức năng. Code cũ còn trong lịch sử git nếu cần làm lại.

**Đợt 7 (2026-09-29)** — task 16 (bộ tiêu chí theo phiên bản), task 17 (cài đặt đơn vị, quản lý file mẫu Word) và tích hợp đã merge (`f62a116`). Người điều phối kiểm lại: build 0 lỗi 0 warning, unit 273/274 (1 skip PDF), integration 98/98 trên `192.168.22.159`, `tsc` pass, grep không còn điểm/trục/khung cứng, frontend không còn tên đơn vị cứng. Migration gộp lại `InitialCreate` mới → **CSDL cũ phải tạo lại**. Tiêu chí chung nay theo HD03 (3 nhóm, 17 tiêu chí con, Đảm bảo/Không đảm bảo/K/AD); ví dụ TC-1 ra đúng 67,6 / 97,6 như văn bản (trước ra 67,47 do làm tròn cứng).
- Cần nghiệp vụ xác nhận: 2 bộ tiêu chí mặc định (09B, 09A) và 8 câu hỏi trong `reports/16-criteria-sets.md`; nội dung cài đặt đơn vị mặc định.
- Còn mở (⚪): điểm thẩm định theo nhóm trên Mẫu 10; giải trình cho kết quả thẩm định cấp trên; làm tròn bước 0,5; đổi khung tỷ trọng sau khi đã tự chấm không tự tính lại; tên tệp tải xuống chưa lấy từ `Content-Disposition` (xem `reports/wave7-integration.md`).

**Đợt 6 (2026-09-29)** — task 14 (tổ chức động), task 15 (luồng theo nhóm đối tượng) và tích hợp đã merge (`07106a7`). Người điều phối kiểm lại: build 0 lỗi 0 warning, unit 228/229 (1 skip PDF), integration 89/89 trên `192.168.22.159`, `tsc` pass, grep không còn enum chức vụ / mã quyền cũ. Migration gộp lại một `InitialCreate` mới → **CSDL cũ phải tạo lại**. Đổi tiêu đề cột import sang "Mã/Tên đơn vị", "Mã/Tên tổ chức Đảng", "Mã đơn vị công tác" (file mẫu cũ không dùng được). Loại đơn vị khai báo trên giao diện (chưa có import).
- Cần nghiệp vụ xác nhận thêm: danh mục chức vụ mặc định và mã M1–M26 (Chủ tịch/Giám đốc có xếp M14, Kế toán trưởng, Chi ủy viên), 3 hồ sơ luồng mặc định và 6 câu hỏi trong `reports/15-workflow-profiles.md`.

## T — Kỹ thuật

| Mã | Mức | Vấn đề | Vị trí chính | Owner | Trạng thái |
|---|---|---|---|---|---|
| T-01 | 🔴 | Path traversal: `formCode` từ người dùng đưa thẳng vào đường dẫn lưu file | `Application/Services/AttachmentService.cs`, `Infrastructure/Services/LocalFileStorageService.cs` | 01 | done |
| T-02 | 🔴 | Stored XSS: ContentType lấy từ client, không kiểm tra magic bytes, `/view` trả inline cùng origin, frontend `window.open(blobUrl)` | `AttachmentService.cs`, `Api/Controllers/AttachmentController.cs`, `frontend/components/attachments/DocumentViewerModal.tsx` | 01 | done |
| T-03 | 🔴 | Không có đổi/đặt lại mật khẩu; mọi tài khoản mặc định `123456` | `DataSeeder.cs`, `UserService.cs`, `AuthController.cs` | 01 | done |
| T-04 | 🔴 | Đăng nhập không có rate limit / khóa tài khoản; lệch thời gian phản hồi khi username không tồn tại | `AuthService.cs`, `Program.cs` | 01 | done |
| T-05 | 🔴 | Secret trong repo (JWT secret, mật khẩu DB, IP nội bộ, mật khẩu compose) | `Api/appsettings.json`, `docker/docker-compose.yml` | 03 | done |
| T-06 | 🟠 | Swagger luôn bật ở production (`IsDevelopment() \|\| true`) | `Program.cs` | 03 | done |
| T-07 | 🟠 | MinIO: bucket public-read, adapter gửi request không ký SigV4, không được đăng ký dùng | `docker-compose.yml`, `MinioFileStorageService.cs` | 03 | done |
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
| T-24 | 🟠 | Frontend chưa gửi `version` khi cập nhật hồ sơ/kỳ đánh giá → kiểm tra concurrency (T-15) chưa có hiệu lực; backend tạm coi `version` là tùy chọn | `frontend/services/evaluationService.ts`, `components/evaluations/Step*.tsx` | 06 → 12 | done |
| T-25 | 🟠 | Migration chưa chạy thử trên PostgreSQL thật (DB trống + DB cũ baseline); DB cũ baseline sẽ thiếu các index mới nằm trong `InitialCreate` (kể cả unique index kỳ đang hoạt động) | `Infrastructure/Data/Migrations`, `Api/Extensions/PersistenceExtensions.cs` | — | wontfix |
| T-26 | 🟡 | `DataSeeder` còn khối DDL `ALTER TABLE … ADD COLUMN IF NOT EXISTS` tương thích schema cũ — trùng vai trò với migration | `Infrastructure/Data/DataSeeder.cs` | 06 | done |
| T-27 | 🟡 | Docker chưa được kiểm chứng: chưa `docker compose config`, chưa build image, chưa gọi `/healthz` (máy không có Docker) | `docker/`, `.github/workflows/ci.yml` | — | open |
| T-28 | 🟡 | Vận hành: JWT secret và mật khẩu DB cũ đã nằm trong lịch sử git → phải rotate trước khi triển khai | môi trường triển khai | — | open |
| T-29 | ⚪ | Chưa có `.gitattributes` — file LF/CRLF lẫn lộn (cảnh báo khi commit trên Windows) | gốc repo | 06 | done |
| T-30 | 🔴 | Xuất Word `/api/reports/docx/*` chỉ yêu cầu đăng nhập, không kiểm tra phạm vi → ai cũng tải được Mẫu 01/02/10 của người khác, Mẫu 11/13 của mọi Chi bộ; Excel 14/15/16 chỉ cần `reports.export` (CAN_BO có sẵn) và xuất toàn Đảng bộ | `Api/Controllers/ExportReportController.cs`, `Infrastructure/Services/ReportService.cs` | 04 | done |
| T-31 | 🔴 | File đính kèm không có chủ sở hữu (`UploadedBy` là tên dạng chữ) và không kiểm tra phạm vi khi liệt kê/xem/tải/xóa → ai có `attachments.read` xem được mọi minh chứng | `TaskAttachment.cs`, `AttachmentService.cs`, `AttachmentController.cs` | 04 | done |
| T-32 | 🟠 | `GET /api/users/profile?username=` trả hồ sơ + roles/permissions của người khác; `GET /api/organizations/departments` không có policy; chưa có cơ chế kiểm tra quyền theo đối tượng dùng chung (mỗi service tự kiểm tra một kiểu) | `UserController.cs`, `OrganizationController.cs`, các Application service | 04 | done |
| T-33 | 🟠 | Seeder đồng bộ lại quyền của mọi role mỗi lần khởi động (khi `SeedSampleData=true`) → ghi đè thay đổi phân quyền làm qua giao diện | `Infrastructure/Data/DataSeeder.cs` | 04 | done |
| T-34 | 🟡 | Frontend dùng quyền `evaluations.branch_review` không tồn tại ở backend; hiển thị bước theo quyền không khớp backend | `frontend/app/evaluations/page.tsx`, `EvaluationStepNav.tsx` | 04 | done |
| T-35 | 🟠 | `backend/sign.ps1` tự tạo chứng chỉ và thêm vào kho **Trusted Root** của Windows để ký file build | `backend/sign.ps1` | 04 | done |
| T-36 | 🟠 | Mô hình file: mỗi nhiệm vụ chỉ 1 file, không có phiên bản (thay file = mất bản cũ), không gắn đối tượng tổng quát; adapter MinIO không dùng được (phần còn lại của T-07) | `TaskAttachment.cs`, `EvaluationTask.AttachmentId`, `MinioFileStorageService.cs` | 05 | done |
| T-37 | 🔴 | Bộ sinh Word thay `{{TAG}}` theo từng đoạn chữ (run) → hỏng khi Word tách run; file phôi sinh bằng code; nhiều chữ in cứng (giờ họp, khối, tên đơn vị) | `Infrastructure/Services/DocxTemplateEngine.cs` | 05 | done |
| T-38 | 🟠 | Không có xuất PDF phía máy chủ (Hướng dẫn yêu cầu nộp PDF kèm Word/Excel); 3 đường hiển thị biểu mẫu độc lập (Word, Excel, HTML in bằng `window.print`) có thể ra số khác nhau (ví dụ Mẫu 02 tính lại điểm theo trọng số Khung 2 cố định) | `DocxTemplateEngine.cs`, `ReportService.cs`, `frontend/components/evaluations/EvaluationPrintTemplate.tsx` | 05 | partial |
| T-39 | ⚪ | Warning build: `KnownNetworks`/`IPNetwork` obsolete, nullable trong `DocxTemplateEngine` | `HostingExtensions.cs`, `DocxTemplateEngine.cs` | 06 | done |
| T-40 | 🟡 | Log chỉ ra console: chưa có log ra file có xoay vòng, chưa có correlation id để lần theo một yêu cầu | `Api/Program.cs` | 06 | done |
| T-41 | 🟡 | Chưa có script sao lưu/khôi phục (PostgreSQL + thư mục upload) đã được thử nghiệm | `docker/`, `docs/deployment.md` | 06 | done |
| T-42 | 🟡 | Test mỏng (7 unit test, chỉ phần bảo mật); chưa có integration test cho API | `backend/tests/` | 04/05/06 | partial |

| T-43 | 🟡 | `frontend/next-env.d.ts` nằm trong `.gitignore` → checkout mới / CI chạy `tsc --noEmit` trước `next build` báo lỗi `<style jsx>` | `.gitignore`, `frontend/` | — | open |
| T-44 | 🟡 | Khôi phục bản sao lưu của phiên bản code cũ trong khi app bật `AutoMigrate` → migrate tự chạy lên schema mới ngay khi khởi động; cần quy trình khôi phục kèm đúng phiên bản code | `docker/restore.sh`, `docs/deployment.md` | — | open |
| T-45 | 🟡 | Người có `branch_vote` nhưng chưa gán Chi bộ xem được mọi biên bản họp trong kỳ | `Application/Common/Security/AccessPolicy.cs`, `CollectiveEvaluationService.cs` | 09 | done |
| T-46 | 🟡 | Seeder vẫn gán lại role cho người dùng mỗi lần khởi động khi `SeedSampleData=true` | `Infrastructure/Data/DataSeeder.cs` | 09 | done |
| T-47 | 🟡 | `EvaluationTask.AttachmentId` gửi từ client không được kiểm tra (file có tồn tại, có thuộc quyền người gửi) | `EvaluationService.RegisterTasksAsync` | 05 | done |
| T-48 | 🟠 | `SubmitAppraisalAsync` không kiểm tra quyền trên từng hồ sơ (chỉ dựa vào policy của controller) | `EvaluationService.cs` | 09 | done |
| T-49 | 🔴 | `xmin` khai báo dạng shadow property → truy vấn `AsNoTracking` (danh sách hồ sơ, kỳ đang hoạt động) trả `version: 0` → bước 3–5 luôn nhận 409 khi frontend gửi version. Phát hiện khi chạy thật lúc tích hợp đợt 2 | `CongTacDangDbContext.cs`, `UnitOfWork.cs` | — | done |
| T-50 | 🟡 | File logger đăng ký bằng instance nên không được dispose khi tắt ứng dụng → mất các dòng log cuối. Phát hiện nhờ test khi tích hợp | `Api/Logging/RollingFileLoggerProvider.cs` | — | done |
| T-51 | ⚪ | Tài liệu hướng dẫn seed sẵn (không có người tải lên, không gắn hồ sơ) sau T-31 chỉ quản trị hệ thống xem được — cần quyết định cách phân loại "văn bản chung" | `DataSeeder.cs`, `AccessPolicy.cs` | — | done |
| T-52 | 🟡 | `TaskAttachment.TaskId` và `FilePath` là alias nhưng EF ánh xạ thành cột trùng lặp | `TaskAttachment.cs`, `CongTacDangDbContext.cs` | — | done |
| T-53 | ⚪ | `MapToRecordDto` hiển thị tên file của phiên bản đầu tiên thay vì phiên bản hiện hành | `EvaluationService.cs` | 12 | done |
| T-54 | ⚪ | Chưa có giao diện xem lịch sử phiên bản file (API đã có) | `frontend/components/attachments/` | — | open |
| T-55 | 🔴 | Phân quyền gắn tên vai trò cứng trong code (`AppRoles`, `IsInRole`), không có phạm vi khi gán, không tạo/xóa được vai trò; quyền nằm trong JWT → đổi quyền chậm tới 15 phút. Vai trò không khớp tác nhân HD03 (Chi bộ làm việc của tập thể lãnh đạo Phòng; thiếu lãnh đạo trực tiếp, cấp trực tiếp sử dụng; BTV ĐUTCT là cấp ngoài) | `AppRoles.cs`, `AccessPolicy.cs`, `Program.cs`, `DataSeeder.cs` | 07, 09 | done |
| T-56 | 🟠 | Tạo tài khoản sinh username ngẫu nhiên `cb_xxxxxxxx`, mật khẩu tạm bị bỏ đi, không gán quyền → tài khoản mới không dùng được | `UserService.CreateUserAsync` | 08 | done |
| T-57 | 🟠 | Khóa / xóa / đặt lại mật khẩu không vô hiệu access token đang dùng (còn hiệu lực ≤ 15 phút) | `AuthService`, `JwtService`, `Program.cs` | 08 | done |
| T-58 | 🟠 | `MustChangePassword` chỉ chặn ở frontend; backend vẫn cho gọi mọi API bằng mật khẩu tạm | `AuthGuard.tsx`, backend không có | 08 | done |
| T-59 | 🟡 | Không có nhật ký đăng nhập, không mở khóa đăng nhập, danh sách người dùng không phân trang; không chặn tự khóa/xóa mình và quản trị viên cuối cùng | `UserService`, `UserController`, `AuthService` | 08, 09 | done |
| T-60 | 🟡 | Hồ sơ người dùng trả vai trò suy từ `PartyRole` khi chưa gán vai trò → giao diện hiện chức năng mà backend từ chối | `UserService.MapToProfileDto` | 08 | done |
| T-61 | 🔴 | Vai trò Cán bộ có `users.read` + `reports.export` → mọi cán bộ gọi `/api/users/list` và `/api/reports/cadres` lấy danh sách toàn bộ cán bộ kèm số thẻ Đảng | `DataSeeder.cs:21-25`, `UserController`, `ExportReportController.ExportCadres` | 09 (08: `/users`) | done |
| T-62 | 🟡 | Phòng ban chỉ có API đọc; xóa danh mục không kiểm tra còn cán bộ/hồ sơ | `OrganizationController`, `OrganizationService` | 10 | done |
| T-63 | 🟡 | Không có test tích hợp API trên PostgreSQL thật (một phần T-42) | `backend/tests/` | 07 | done |
| T-64 | 🟠 | Không có chức năng import dữ liệu (Phòng, Chi bộ, cán bộ, gán vai trò, người được đánh giá) → go-live phải nhập tay từng người | — | 10, 13, 12 | done |
| T-65 | 🟡 | `app/users/page.tsx` > 1.000 dòng gộp tài khoản/Chi bộ/vai trò; không có giao diện gán phạm vi, tra cứu quyền, nhật ký đăng nhập | `frontend/app/users/page.tsx` | 11 | partial |
| T-66 | 🟠 | Luồng đánh giá lệch HD03: thiếu duyệt danh mục (B1), Chi bộ xác nhận tách khỏi đề xuất tập thể lãnh đạo Phòng (B3a), cấp trực tiếp sử dụng (B3c), ghi nhận quyết định cấp trên, công bố/khóa (B5), trả lại/mở lại có lý do; bước không cấu hình theo kỳ | `EvaluationService`, `DomainEnums.cs`, `components/evaluations/*` | 12 | done |
| T-67 | 🟡 | Hằng số nghiệp vụ (số sản phẩm, tổng trọng số, ngưỡng, trần %, chênh lệch) rải rác trong code, không cấu hình theo kỳ | `EvaluationService`, `CollectiveEvaluationService` | 12 | done |
| T-68 | 🟡 | Chưa có kịch bản go-live kiểm thử đầu-cuối (CSDL trống → import → đăng nhập → phân quyền → đánh giá) | `backend/tests/` | 13 | done |
| T-69 | 🔴 | Proxy `/api/*` của Next.js được ghi cố định lúc build, nhưng `Dockerfile.frontend` build không có `BACKEND_INTERNAL_URL` (biến chỉ đặt lúc chạy container) → frontend trong Docker gọi `localhost:5000` của chính nó, mọi API lỗi 500. Đã sửa: build arg trong Dockerfile + compose, bỏ biến runtime vô tác dụng, chú thích trong `next.config.mjs` | `docker/Dockerfile.frontend`, `docker/docker-compose.yml`, `frontend/next.config.mjs` | — | done |
| T-71 | 🟠 | Phòng và Chi bộ là danh sách phẳng — không khai báo được Đảng bộ bộ phận, đơn vị nhiều cấp; phạm vi gán vai trò không bao trùm đơn vị con | `PartyCell`, `AdministrativeDepartment`, `AuthorizationGuard` | 14 | done |
| T-72 | 🟠 | Chức vụ Đảng/chính quyền là enum cứng (thiếu UV UBKT, Trưởng trung tâm, Kế toán trưởng…), không có mã chức danh M1–M26 cho Mẫu 15A/15B, không ghi được kiêm nhiệm | `DomainEnums.cs`, `PartyMemberProfile`, `ReportService` | 14 | done |
| T-73 | 🟡 | Import và giao diện danh mục chưa hỗ trợ cây đơn vị, loại đơn vị, danh mục chức vụ, chức vụ của cán bộ | `Application/Imports`, `app/catalog` | 14 | done |
| T-74 | 🔴 | Một luồng cho cả kỳ: hồ sơ diện BTV Đảng ủy Tổng công ty (Ban Giám đốc) đi luồng nội bộ và kẹt ở bước cấp trực tiếp sử dụng; không có bước do cấp trên thực hiện; không có biến thể theo đối tượng (HD03 Phụ lục III ví dụ 2, 3) | `EvaluationWorkflowService`, `PeriodSettings` | 15 | done |
| T-75 | 🟠 | Không kiểm tra trước được hồ sơ nào sẽ kẹt vì không ai đủ quyền thực hiện bước | `PeriodService` | 15 | done |
| T-76 | 🟡 | Giao diện kỳ chưa cấu hình được luồng theo nhóm, người thực hiện từng bước | `app/periods` | 15 | done |
| T-77 | 🔴 | Tiêu chí chung chấm thành 6 ô × 5 điểm (`GeneralScoreT1..T6`) — lệch HD03 (3 nhóm 18/4/8, 17 tiêu chí con, "đảm bảo/không đảm bảo", K/AD); trục, khung tỷ trọng là enum; nội dung chấm điểm không khai báo được theo văn bản từng thời kỳ | `EvaluationRecord`, `DomainEnums.cs`, `EvaluationScoring` | 16 | done |
| T-78 | 🟠 | Tham số tính điểm của kỳ chỉ xem, không sửa được; làm tròn, trần Xuất sắc, ngưỡng giải trình chênh lệch không cấu hình được (B-08, B-09) | `PeriodSettings`, `EvaluationParameters` | 16 | done |
| T-79 | 🟡 | Form tự chấm/thẩm định dựng cứng 6 ô tiêu chí, 6 trục | `components/evaluations/RecordActionPanel.tsx` | 16 | done |
| T-80 | 🟡 | Tên đơn vị ghi cứng trong code xuất biểu mẫu và tên file ("ĐẢNG BỘ CÔNG TY TNHH KỸ THUẬT QUẢN LÝ BAY", "ATTECH") | `ReportService`, `Documents/**` | 17 | done |
| T-81 | 🟡 | Không thay được file mẫu Word qua giao diện, không có phiên bản, không kiểm tra tag khi thay | `WordTemplateStore`, `Templates/Word` | 17 | done |
| T-82 | 🔴 | Hồ sơ không có chỗ nhập Mẫu 09C (bản tự đánh giá tự luận) và 9D (phụ lục kết quả theo trục) — bắt buộc Quý III/2026 | `EvaluationRecord`, form tự chấm | 18 | done |
| T-83 | 🔴 | Không xuất được Mẫu 09B, 09C, 9D (bắt buộc nộp Quý III/2026); hồ sơ chỉ xuất 01/02/10 (Q3/2026 chưa áp dụng) | `Documents`, trang hồ sơ | 18 | done |
| T-84 | 🔴 | Không xuất được Mẫu 07, 08 (tập thể) và 12 (biên bản hội nghị) — bắt buộc Quý III/2026 | `CollectiveEvaluationService`, `Documents` | 19 | done |
| T-85 | 🟠 | Trang Báo cáo lệch HD03: tên Mẫu 14 sai, "Mẫu 15" là thống kê cơ cấu tổ chức (không có trong HD03), "Mẫu 16" là Excel trong khi HD03 là báo cáo Word | `ReportService`, `app/reports` | 19 | done |
| T-86 | 🟠 | Không có công khai kết quả sau công bố (Bước 5 HD03) | — | 20 | done |
| T-87 | 🟠 | Không có kiến nghị/giải trình sau công bố (PL II mục III.2) — chỉ có mở lại hồ sơ | — | 20 | done |
| T-88 | 🟠 | Không có kế hoạch hỗ trợ, khắc phục 30-60-90 ngày (Mẫu 17, bắt buộc với mức C/D) | — | 20 | done |
| T-89 | 🟡 | Không có nhắc việc/sắp tới hạn trong ứng dụng | — | 20 | done |
| T-70 | ⚪ | EF cảnh báo 10622 khi khởi động: `CollectiveEvaluationRecord`, `EvaluationMeeting` có query filter xóa mềm nhưng là đầu bắt buộc của quan hệ với bảng con (item, vote summary) — cần filter tương ứng cho bảng con hoặc quan hệ tùy chọn | `CongTacDangDbContext`, `Data/Configurations/` | — | open |

## B — Nghiệp vụ (để xử lý sau)

| Mã | Mức | Vấn đề | Vị trí chính | Owner | Trạng thái |
|---|---|---|---|---|---|
| B-01 | 🟠 | `UpdatePeriodStatusAsync` nhận bất kỳ trạng thái nào (lùi bước được); chưa có state machine cho kỳ/hồ sơ | `EvaluationService.cs` | 12 | done |
| B-02 | 🟠 | File đính kèm không giới hạn phạm vi: ai có `AttachmentsRead` cũng xem được minh chứng của mọi cán bộ | `AttachmentService.cs`, `AttachmentController.cs` | — | deferred (cơ chế kiểm tra phạm vi: T-31) |
| B-03 | 🟠 | Bỏ phiếu kín "không lưu User ID" cần kiểm chứng — audit log tự ghi `ActorId` cho mọi entity thêm mới | `CongTacDangDbContext.PrepareAuditEntries`, `CollectiveEvaluationService.cs` | 12 | done |
| B-04 | 🟡 | Quy tắc 70đ, ≥30% minh chứng vượt chuẩn, trần 20% Xuất sắc, chênh lệch ≥5đ cần đối chiếu Hướng dẫn 03 và có unit test | `EvaluationService.cs`, `CollectiveEvaluationService.cs` | — | deferred |
| B-05 | ⚪ | Chưa có tài liệu hướng dẫn sử dụng (HDSD cũ mô tả sai route đã bị xóa); cần viết lại theo route thực tế | `docs/` | — | deferred |
| B-06 | 🟡 | Cán bộ không xóa được file của chính mình (role `CAN_BO` không có `attachments.delete`); cơ chế theo đối tượng đã cho phép chủ file — cần quyết định ai được xóa | `DataSeeder.cs` (quyền mặc định) | — | deferred |
| B-07 | 🟡 | Biểu mẫu có ô để trống vì chưa có dữ liệu lưu: Mẫu 01 mã SP/trục/vai trò, Mẫu 10 điểm thẩm định theo nhóm, Mẫu 13 số phiếu không hợp lệ | `Infrastructure/Documents/`, template Word | 12 | partial |
| B-08 | 🟠 | Cần nghiệp vụ xác nhận công thức (code giữ nguyên, tham số hóa trong `EvaluationParameters`): ví dụ TC-1 văn bản ra 67,6 điểm, code ra ≈ 67,47 (cách làm tròn); mẫu số và làm tròn của trần tỷ lệ Xuất sắc (TC-3); điểm tối đa từng trục 09B | `Domain/Evaluation/EvaluationParameters.cs`, `EvaluationScoring` | — | open |
| B-09 | 🟡 | Chưa bắt giải trình khi chênh lệch tự chấm – thẩm định ≥ 5 điểm; mở lại hồ sơ trong kỳ đã đóng tự đưa kỳ về "Khóa dữ liệu" — cần nghiệp vụ xác nhận | `EvaluationWorkflowService` | — | open |
| B-10 | 🟡 | Cần TCCB-LĐ xác nhận cấu hình mặc định: vai trò/phạm vi (`docs/thiet-ke/phan-quyen.md` mục 6) và 5 câu hỏi luồng (`docs/thiet-ke/luong-danh-gia.md` mục 6); quy ước ngày import gán vai trò (giờ Việt Nam, "Đến ngày" tính trọn ngày) | `docs/thiet-ke/` | — | open |
