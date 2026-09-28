# Báo cáo: Phân quyền theo đối tượng & bảo mật còn lại

- **Branch:** `fix/authz`
- **Commit cuối:** commit chứa báo cáo này (commit code cuối: `28c7740`)
- **Ngày:** 2026-09-28

> Ghi chú: worktree được tạo từ commit cũ `86cd795` (chưa có `docs/remediation`). Branch `fix/authz` được đặt lại về `main` (`1877805`) trước khi sửa file đầu tiên — chưa có thay đổi nào bị mất.

## Kết quả theo mã lỗi

| Mã | Trạng thái | Tóm tắt thay đổi | File chính |
|---|---|---|---|
| T-32 | done | Thêm `IAccessPolicy`/`AccessPolicy` (thao tác `Read`, `Update`, `Delete`, `Export`, thêm `BranchReview`, `Approve`) cho hồ sơ đánh giá, Chi bộ/kỳ, hồ sơ tập thể, biên bản, tệp đính kèm, hồ sơ người dùng. Chuyển `CanReadRecord`, `EnsureCanReviewBranch`, kiểm tra cấp phê duyệt, `IsElevated`/`CanAccessOrganization`… của `EvaluationService`, `CollectiveEvaluationService` sang policy mà không đổi kết quả. `GET /api/users/profile?username=` chỉ trả hồ sơ của chính mình, trừ Quản trị hệ thống có `users.read`. `GET /api/organizations/departments` yêu cầu `branches.read`. | `Application/Common/Security/AccessPolicy.cs`, `EvaluationService.cs`, `CollectiveEvaluationService.cs`, `UserService.cs`, `UserController.cs`, `OrganizationController.cs` |
| T-30 | partial | Mẫu 01/02/10: kiểm tra `Export` trên hồ sơ (= quyền xem). Mẫu 11/13: chỉ Chi bộ của mình với `branch_vote`, hoặc cấp cao (thẩm định/phê duyệt/quản trị); không truyền `branchId` thì Chi bộ mặc định xuất Chi bộ mình, cấp cao xuất toàn Đảng bộ. Excel 14/15/15A/15B/16: chỉ cấp cao. | `ExportReportController.cs`, `Application/Services/ReportAccessService.cs` |
| T-31 | done | Thêm `TaskAttachment.UploadedById` (FK tới `party_member_profiles`, `SET NULL`, dữ liệu cũ để null), ghi khi upload. Quyền trên tệp = quyền trên hồ sơ liên quan (qua `RecordId` hoặc `EvaluationTask.AttachmentId`); tệp không gắn hồ sơ: người tải lên + Quản trị hệ thống; văn bản `GENERAL` do quản trị tải lên: mọi người đã đăng nhập được xem. Áp dụng cho danh sách (lọc), xem chi tiết, xem/tải, xóa. | `TaskAttachment.cs`, `CongTacDangDbContext.cs`, `AttachmentService.cs`, `AttachmentController.cs`, `SpecificRepositories.cs` (interface + repo) |
| T-33 | done | Seeder chỉ tạo role/permission còn thiếu; permission mới không tự gán vào role, ghi log cảnh báo. Bỏ bước “đồng bộ quyền” chạy mỗi lần khởi động. Thêm cờ `Database:ResetRolePermissions` (mặc định `false`) để đặt lại quyền của các role hệ thống về ma trận mặc định. Ma trận mặc định gom về một chỗ (`DefaultRoles`). | `DataSeeder.cs`, `PersistenceExtensions.cs` (dòng gọi seeder) |
| T-34 | done | Bỏ `evaluations.branch_review`; Bước 3 chỉ dựa vào `evaluations.branch_vote`. Đã rà toàn bộ `frontend/`: mọi mã quyền còn lại đều có trong `AppPermissions.cs`. | `app/evaluations/page.tsx`, `components/evaluations/EvaluationStepNav.tsx`, `app/page.tsx` |
| T-35 | done | Xóa `backend/sign.ps1`. | `backend/sign.ps1` |
| T-42 (phần 04) | done | 22 unit test mới: so khớp luật mới với bản sao nguyên văn `CanReadRecord` cũ trên toàn bộ tổ hợp vai trò × Chi bộ × cờ `IsApprovedByAttech` (>1000 trường hợp, cho cả `Read` và `Export`); ma trận chủ hồ sơ / cùng Chi bộ / khác Chi bộ / cấp cao cho từng thao tác; phạm vi Chi bộ, hồ sơ tập thể, biên bản, hồ sơ người dùng; quyền trên tệp đính kèm. | `tests/.../AccessPolicyTests.cs`, `tests/.../AttachmentAccessPolicyTests.cs` |

### T-30 — phần còn lại (partial)
- Excel 14/15/15A/15B/16 chưa lọc được theo Chi bộ vì việc lọc dữ liệu nằm trong `ReportService.cs` (thuộc task 05 theo RULES 7.2). Quyết định tạm: **chỉ cấp cao** (quyền `evaluations.appraise`, `evaluations.approve` hoặc vai trò Quản trị hệ thống) được xuất các báo cáo toàn Đảng bộ; **Bí thư Chi bộ và Cán bộ** (có `reports.export` theo mặc định) nhận 403. Phần “Bí thư Chi bộ xuất Chi bộ mình” cần thêm tham số phạm vi Chi bộ vào `IReportService.ExportForm14/15/15A/15B/16ReportAsync` — xem “Cần phối hợp”.
- `/api/reports/cadres` (danh sách cán bộ) **giữ nguyên** `reports.export`: dữ liệu tương đương `/api/users/list` (đang cho mọi người có `users.read`). Việc giới hạn danh sách cán bộ là luật nghiệp vụ chưa chốt (B-02).
- Mẫu 11/13 khi người có `evaluations.approve` với vai trò Đảng ủy cơ sở xuất theo Chi bộ: `ReportService` không lọc hồ sơ theo `IsApprovedByAttech` như `CanReadRecord`. Giữ “như hiện tại” (cấp cao xem mọi Chi bộ, giống `GetRecordsByBranchAsync`).

### Lựa chọn cơ chế (T-32)
Chọn **`IAccessPolicy` ở tầng Application** thay vì `IAuthorizationService` + `AuthorizationHandler` của ASP.NET Core, vì:
- Các kiểm tra phạm vi đang nằm trong Application service và dựa trên role/permission nạp từ CSDL (`GetWithRolesAndPermissionsByIdAsync`), không dựa trên claim trong JWT — thay đổi phân quyền có hiệu lực ngay, không chờ token hết hạn.
- Application không phụ thuộc ASP.NET Core; policy là lớp thuần, đồng bộ, không I/O → unit test trực tiếp, không cần `HttpContext`.
- Mọi luật “ai được thao tác trên đối tượng nào” nằm trong một file (`AccessPolicy.cs`). Khi chốt B-02 chỉ cần sửa lớp này (hoặc đăng ký một triển khai khác của `IAccessPolicy`).

### Luật đang áp dụng (không đặt luật mới)
- **Hồ sơ đánh giá – Read/Export:** nguyên văn `CanReadRecord` cũ.
- **Update:** chủ hồ sơ (giữ kiểm tra cũ của bước tự chấm). **BranchReview:** `branch_vote` + cùng Chi bộ (giữ `EnsureCanReviewBranch`). **Approve:** `evaluations.approve` + đúng vai trò theo `IsApprovedByAttech` (giữ luật cũ). **Delete:** không ai (hệ thống chưa có chức năng xóa hồ sơ).
- **Tệp đính kèm:** Read/Export theo quyền xem hồ sơ liên quan; Delete/Update theo quyền cập nhật hồ sơ liên quan (chủ hồ sơ), ngoài ra người tải lên và Quản trị hệ thống luôn được phép.
- Liên kết tệp qua nhiệm vụ (`EvaluationTask.AttachmentId`, người dùng tự khai báo khi đăng ký/tự chấm) chỉ được tính khi tệp do chính chủ hồ sơ tải lên hoặc là dữ liệu cũ chưa có `UploadedById` — chặn việc gắn Id tệp của người khác vào nhiệm vụ của mình để đọc.
- “Cấp quản trị” trong T-31/T-32 được hiểu là **vai trò `QUAN_TRI_HE_THONG`** (vì `users.read` mặc định có ở mọi Cán bộ nên không đủ để phân biệt).

### Gỡ chứng chỉ `CN=CongTacDangLocal` (T-35) — không tự chạy
`sign.ps1` đã tạo chứng chỉ trong `Cert:\CurrentUser\My` và thêm bản sao vào kho **Trusted Root** của người dùng hiện tại (`Cert:\CurrentUser\Root`). Trên máy nào đã từng chạy script, người quản trị máy tự chạy trong PowerShell (tài khoản đã chạy script):
```powershell
# Kiểm tra trước
Get-ChildItem Cert:\CurrentUser\Root, Cert:\CurrentUser\My | Where-Object Subject -eq 'CN=CongTacDangLocal'
# Gỡ (Windows sẽ hỏi xác nhận khi xóa khỏi Root)
Get-ChildItem Cert:\CurrentUser\Root | Where-Object Subject -eq 'CN=CongTacDangLocal' | Remove-Item
Get-ChildItem Cert:\CurrentUser\My   | Where-Object Subject -eq 'CN=CongTacDangLocal' | Remove-Item
```
Hoặc dùng `certmgr.msc` → *Trusted Root Certification Authorities* và *Personal* → xóa chứng chỉ “CongTacDangLocal”. Các file `.dll/.exe` đã ký trong `backend/publish` không cần ký lại.

## Cổng kiểm tra

| Bước | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build` | pass | 0 error, 8 warning — trùng baseline trước khi sửa (5× CS8602 `DocxTemplateEngine`, 3× ASPDEPR005 `HostingExtensions`, thuộc T-39). Không thêm warning mới. |
| `dotnet test` | pass | 29/29 (7 test cũ + 22 test mới). |
| `npx tsc --noEmit` | pass | Chạy `npm ci` trước. Worktree mới không có `frontend/next-env.d.ts` (file sinh tự động, bị `.gitignore`) nên lần đầu báo lỗi `<style jsx>` ở `EvaluationPdfModal.tsx`; tạo `next-env.d.ts` chuẩn (không commit) thì pass. |
| Chạy app / HTTP thật | không chạy | Theo RULES 7.3 không chạy app vào CSDL dùng chung; hành vi API chỉ được kiểm bằng unit test ở tầng policy. |
| Test seeder (T-33) | không có | Test project không có provider EF in-memory/SQLite (thêm package là ngoài phạm vi). Cần kiểm thử tay trên CSDL thử nghiệm (xem “Cần phối hợp”). |

## Thay đổi schema (cần migration)
- Bảng `task_attachments`: thêm cột `UploadedById uuid NULL`, FK → `party_member_profiles("Id")` `ON DELETE SET NULL`, index `IX_task_attachments_UploadedById`. Dữ liệu cũ để null (không backfill theo tên `UploadedBy` vì tên không duy nhất).

## Key config mới
| Key | Mặc định | Ý nghĩa |
|---|---|---|
| `Database:ResetRolePermissions` | `false` | `true` → khi khởi động, đặt lại quyền của 6 vai trò hệ thống về ma trận mặc định (ghi đè thay đổi làm qua giao diện). Chỉ bật một lần khi cần, sau đó tắt. |

## Thay đổi hành vi API / breaking change
- `GET /api/users/profile?username=<người khác>` → **403** trừ Quản trị hệ thống có `users.read` (username không tồn tại: 403 với người không có quyền, 404 với quản trị). Không truyền `username` hoặc truyền chính mình → như cũ. Hồ sơ lấy theo Id trong JWT (`NameIdentifier`), không còn theo claim `Name`.
- `GET /api/organizations/departments` → cần `branches.read` (mặc định: Cán bộ, Bí thư, Tổ thẩm định, BTV, Quản trị có; **Đảng ủy cơ sở không có** — tài khoản chỉ có vai trò này sẽ nhận 403).
- `GET /api/reports/docx/mau-01|02|10/{recordId}` → 403 nếu không được xem hồ sơ.
- `GET /api/reports/docx/mau-11|13` → 403 nếu `branchId` không phải Chi bộ mình (trừ cấp cao); Chi bộ không truyền `branchId` sẽ nhận file của Chi bộ mình thay vì toàn Đảng bộ; người không có `branch_vote` và không phải cấp cao → 403.
- `GET /api/reports/form-14|15|15a|15b|16` → 403 với Cán bộ và Bí thư Chi bộ (trước đây mọi người có `reports.export`). Frontend trang báo cáo/biểu mẫu nên ẩn các nút này với người không phải cấp cao.
- `GET /api/attachments/list` → chỉ trả tệp người dùng được xem (trước: toàn bộ). Văn bản chỉ đạo cũ (seed) có `UploadedById = null`, không gắn hồ sơ → **chỉ Quản trị hệ thống thấy** cho tới khi được tải lại bởi quản trị với mã `GENERAL` hoặc được cập nhật `UploadedById`.
- `GET /api/attachments/{id}`, `/{id}/view`, `/{id}/download`, `DELETE /api/attachments/{id}` → 403 khi không có quyền trên tệp. Xóa minh chứng của cán bộ khác: Tổ thẩm định/BTV (có `attachments.delete`) **không còn xóa được** tệp không phải của mình (trừ Quản trị hệ thống).
- `IAttachmentService` đổi chữ ký: các phương thức đọc/xóa nhận thêm `requesterId`; `UploadAttachmentAsync` thêm tham số tùy chọn `uploadedById`.

## Cần phối hợp
- **Người điều phối:** sinh migration cho `task_attachments.UploadedById` (mục Thay đổi schema). Lưu ý khối DDL `ExecuteSqlRaw` trong `DataSeeder` (thuộc 06) hiện đang bổ sung cột kiểu `ADD COLUMN IF NOT EXISTS`; nếu CSDL cũ không qua migration thì cần thêm cột này tương tự — việc của 06/người điều phối.
- **Task 05 (`ReportService.cs`):** thêm tham số phạm vi Chi bộ (`Guid? partyCellId`) cho `ExportForm14/15/15A/15B/16ReportAsync` để Bí thư Chi bộ xuất báo cáo Chi bộ mình; khi đó `ExportReportController` gọi `IReportAccessService.ResolveBranchExportScopeAsync` thay cho `EnsureCanExportOrganizationReportAsync`. Task 05 tiếp quản `AttachmentService`/`AttachmentController`/`TaskAttachment`: giữ các lời gọi `IAccessPolicy.CanAccessAttachment` khi đổi mô hình file (nhiều file, phiên bản).
- **Task 03/người điều phối:** thêm `Database:ResetRolePermissions=false` vào `appsettings*.json` và `.env.example`.
- **Task 06 / `Program.cs`:** đã thêm 2 dòng đăng ký DI (`IAccessPolicy`, `IReportAccessService`) cạnh các dòng đăng ký service sẵn có; `PersistenceExtensions.cs` thêm 1 dòng đăng ký `IAttachmentAccessReader` và đổi lời gọi `DataSeeder.SeedAsync`.
- **Test file của task 01 (`SecurityTests.cs`)** dùng constructor `AttachmentService(repo, storage)`. Constructor này được giữ lại chỉ cho kiểm tra hợp lệ khi upload; mọi thao tác cần kiểm tra quyền sẽ ném `InvalidOperationException` nếu dùng nó. Không sửa file test của task 01.
- **Kiểm thử tay trên CSDL thử nghiệm sau merge:** (1) khởi động 2 lần với `SeedSampleData=true`, đổi quyền một role qua giao diện, khởi động lại → quyền giữ nguyên; (2) bật `ResetRolePermissions=true` → quyền về mặc định; (3) upload minh chứng ở Bước 1/2 rồi đăng nhập Bí thư cùng Chi bộ, Bí thư khác Chi bộ, Tổ thẩm định để xem/tải.
- **Frontend:** trang tệp đính kèm, trang báo cáo cần xử lý 403 thân thiện (ẩn nút Excel toàn Đảng bộ với người không phải cấp cao) — ngoài phạm vi mã quyền của T-34.

## Phát hiện thêm
- **(đề xuất T-43, 🟡)** `CollectiveEvaluationService.GetMeetingsAsync`: người có `branch_vote` nhưng **chưa được gán Chi bộ** (`PartyCellId = null`) sẽ nhận **toàn bộ** biên bản của kỳ (repository bỏ lọc khi `partyCellId` null). Đã giữ nguyên hành vi theo yêu cầu “không đổi kết quả”; nên lọc thêm bằng `IAccessPolicy.CanAccessMeeting` sau khi chốt.
- **(đề xuất T-44, 🟡)** `DataSeeder` (khi `SeedSampleData=true`) vẫn sửa **vai trò của người dùng** mỗi lần khởi động: gỡ `QUAN_TRI_HE_THONG` khỏi `bithu_attech`, thêm `DANG_UY_CO_SO` cho `bithu_attech`, thêm `QUAN_TRI_HE_THONG` cho `admin`, gán role theo `PartyRole` cho user chưa có role. Không thuộc “quyền của role” (T-33) nên chưa sửa; có thể ghi đè gán vai trò làm qua giao diện.
- **(đề xuất T-45, 🟡)** `SubmitAppraisalAsync` không kiểm tra phạm vi đối tượng (chỉ policy `evaluations.appraise` ở endpoint) và `RegisterTasksAsync`/`SubmitSelfScoreAsync` nhận `AttachmentId` tùy ý mà không kiểm tra tệp tồn tại/thuộc người dùng. Rủi ro đọc trộm tệp đã được chặn ở policy (xem luật liên kết qua nhiệm vụ), nhưng dữ liệu nhiệm vụ vẫn có thể trỏ tới tệp của người khác.
- **(⚪)** `AuthController.Me` vẫn gọi `IUserService.GetProfileAsync(username)` theo claim tên — không lộ dữ liệu người khác nhưng nên chuyển sang `GetProfileForRequesterAsync` theo Id (file thuộc task 01, đợt 1).
- **(⚪)** Hệ thống chưa có luật chung cho “cấp quản trị” (vai trò vs quyền `roles.manage`); hiện dùng vai trò `QUAN_TRI_HE_THONG`. Nên chốt cùng B-02.
