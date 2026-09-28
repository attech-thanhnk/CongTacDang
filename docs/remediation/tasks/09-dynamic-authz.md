# Task 09 — Phân quyền động có phạm vi (Đợt 4)

- **Agent:** B · **Branch:** `feat/dynamic-authz` (tạo từ `main` **sau khi** task 07 đã merge)
- **Mã:** T-55, T-45, T-48, T-46, T-61, phần test phân quyền của T-42
- **Báo cáo:** `docs/remediation/reports/09-dynamic-authz.md`
- **Đọc trước:** `CLAUDE.md`, `RULES.md` (mục 8), **toàn bộ** `docs/thiet-ke/phan-quyen.md`.

## Mục tiêu
Hiện thực đầy đủ thiết kế phân quyền: vai trò trong CSDL, gán kèm phạm vi và thời hạn, guard duy nhất, chốt chặn, cấu hình mặc định. Sau task này **không còn tên vai trò trong logic** và **không còn mã quyền cũ**.

## Việc cần làm

### 1. Mô hình dữ liệu
- Entity `UserRoleAssignment` (mục 2 thiết kế) + cấu hình trong `Infrastructure/Data/Configurations/`. Index: `(UserId)`, `(RoleId)`, `(ScopeType, ScopeId)`.
- Bỏ quan hệ nhiều-nhiều `PartyMemberProfile.Roles` / `AppRole.Members` và bảng join. Dữ liệu cũ: seeder chuyển mỗi cặp user–role cũ thành bản gán `Global` (chỉ chạy khi bảng gán trống) — không có CSDL thật cần giữ, nhưng vẫn làm để CSDL thử nghiệm không mất phân quyền.
- `AppRole`: thêm `IsProtected`; `Code` không còn dùng trong logic (được giữ để seed tìm vai trò mặc định). `Permission`: thêm `Module`, `SortOrder`.

### 2. Resolver & guard thật
- `PermissionResolver` thay bản v0: nạp bản gán đang hiệu lực + quyền của vai trò; bỏ qua mã không có trong `PermissionCodes.All` (log cảnh báo 1 lần).
- `AuthorizationGuard` thay `LegacyAuthorizationGuard`: đúng luật mục 4 thiết kế (phạm vi, chủ hồ sơ, `ApprovalAuthority`, xung đột lợi ích).
- **Xóa** `IAccessPolicy`/`AccessPolicy`, `AppRoles`, `AppPermissions`, các policy composite trong `AuthorizationExtensions`. Mọi controller dùng `[RequirePermission(PermissionCodes.X)]`.

### 3. Áp dụng guard vào service hiện có (giữ nguyên luồng, chỉ thay kiểm tra quyền)
Theo bảng ánh xạ mã cũ→mới của thiết kế:

| Service / endpoint | Kiểm tra mới |
|---|---|
| `EvaluationService` đọc hồ sơ, lịch sử, danh sách theo kỳ/Chi bộ | `evaluation.read` + `GetScope` cho danh sách |
| `RegisterTasksAsync`, `SubmitSelfScoreAsync` | `evaluation.self` trên hồ sơ |
| `SubmitBranchReviewAsync`, `SubmitBranchMeetingAsync` | `evaluation.cell.confirm` trên **từng** hồ sơ |
| `SubmitAppraisalAsync` | `evaluation.appraise` trên **từng** hồ sơ (**T-48**) |
| `ApproveFinalGradeAsync` | `evaluation.decide` hoặc `evaluation.decide.external` theo `ApprovalAuthority` |
| Kỳ đánh giá (tạo, kích hoạt, đổi trạng thái) | `period.manage` |
| `CollectiveEvaluationService` hồ sơ tập thể | `collective.manage` (ghi) / `evaluation.read` hoặc `collective.manage` (đọc) theo phạm vi Chi bộ/Phòng |
| Biên bản hội nghị | `meeting.manage` / `meeting.read` theo phạm vi; **người không có phạm vi phù hợp không thấy biên bản nào (T-45)** |
| `AttachmentService` | quyền trên tệp = quyền trên hồ sơ gắn tệp (`evaluation.read` / `evaluation.self`); văn bản chung: đọc cho mọi người, ghi `attachment.general.manage` |
| `ReportAccessService` + `ExportReportController` (kể cả `/cadres`) | `report.export` + `GetScope` (**T-61**: danh sách cán bộ lọc theo phạm vi) |
| `EvaluationController`, `CollectiveEvaluationController`, `AttachmentController`, `ExportReportController`, `RoleController` | thay attribute |
Không thêm bước, không đổi trạng thái — việc đó của task 12.

### 4. API quản trị vai trò & gán vai trò
- Vai trò: `GET/POST /api/admin/roles`, `PUT /api/admin/roles/{id}` (tên, mô tả), `PUT /api/admin/roles/{id}/permissions`, `DELETE /api/admin/roles/{id}` (xóa mềm; vai trò đang được gán → 409 kèm số bản gán). Quyền: `system.roles.manage`.
- Danh mục quyền: `GET /api/admin/permissions` (nhóm theo module, có mô tả, `appliesScope`).
- Gán: `GET /api/admin/assignments?userId=&roleId=&scopeType=&scopeId=&activeOn=`, `POST /api/admin/assignments`, `PUT /api/admin/assignments/{id}` (thời hạn, ghi chú), `POST /api/admin/assignments/{id}/end` (đặt `ValidTo = now`), `DELETE` (xóa mềm). Quyền: `system.assignments.manage`.
- Tra cứu: `GET /api/admin/users/{id}/effective-permissions` → danh sách quyền + phạm vi (kèm tên Phòng/Chi bộ) + vai trò/bản gán nguồn. Quyền: `system.assignments.manage` hoặc `system.users.read`.
- `GET /api/auth/me` và phản hồi login: giữ `permissions: string[]` (mã, loại trùng) và thêm `grants: [{code, scopeType, scopeId, scopeName}]`.
- Interface `IRoleAssignmentService` (Application) dùng được từ task 13 (import): `AssignAsync(userId, roleId, scopeType, scopeId, validFrom, validTo, note)`, trả lỗi nghiệp vụ dạng `ValidationException` có thông báo tiếng Việt.
- Chốt chặn đúng mục 5 thiết kế — mỗi chốt một test.

### 5. Seeder (T-46)
- Tạo vai trò mặc định mục 6 thiết kế **chỉ khi chưa có vai trò nào** (ngoài vai trò do task 07 tạo). Không bao giờ ghi đè quyền của vai trò đã tồn tại; không gán lại vai trò cho người đã có bản gán.
- Dữ liệu mẫu: gán vai trò cho tài khoản mẫu kèm phạm vi hợp lý (Bí thư Chi bộ mẫu → PartyCell của mình…).

### 6. Frontend (tối thiểu, chỉ để không vỡ)
- Thay mọi mã quyền cũ bằng mã mới trong `frontend/` (sidebar, `app/evaluations/page.tsx`, `EvaluationStepNav.tsx`, `Step*.tsx`, các trang khác). Bỏ `hasRole(...)` → dùng `hasPermission`. Không thiết kế lại giao diện (task 11, 12).

## Phạm vi file (RULES mục 8.2)
Chủ sở hữu: `Application/Common/Security/**`, `AuthorizationExtensions.cs`, `RoleService`, `RoleController` (→ đổi thành `AdminRoleController`/`AdminAssignmentController` tùy chọn), `ReportAccessService`, entity `AppRole`, `Permission`, `UserRoleAssignment`, `DataSeeder.cs`, phần `RoleRepository` (tách ra `Repositories/RoleRepository.cs`), **chỉ các dòng kiểm tra quyền** trong `EvaluationService`, `CollectiveEvaluationService`, `AttachmentService`, `ExportReportController`, `EvaluationController`, `CollectiveEvaluationController`, `AttachmentController`; frontend: chỉ chuỗi mã quyền / `hasRole`.
**Không** sửa: `OrganizationController`/`OrganizationService` (task 10), `UserController`/`UserService`/`AuthService` (task 08) — cần đổi gì ở đó → "Cần phối hợp".

## Test
- Unit (`AuthorizationGuardTests.cs`): ma trận phạm vi × mã × chủ hồ sơ × `ApprovalAuthority` × hiệu lực thời gian; xung đột lợi ích.
- Tích hợp (`AuthorizationMatrixTests.cs`): với **mỗi endpoint** trong bảng mục 3, tạo người dùng theo từng vai trò mặc định + phạm vi, kiểm tra 200/403 và **dữ liệu trả về đúng phạm vi** (danh sách không chứa hồ sơ ngoài phạm vi). T-45, T-48, T-61 có test riêng.
- Tích hợp chốt chặn: tự gán, xóa vai trò bảo vệ, thu hồi quản trị cuối.
- Test "thay đổi có hiệu lực ngay": gỡ quyền → request kế tiếp 403 (không chờ token hết hạn).

## Tiêu chí hoàn thành
- `grep -rnE "AppRoles|AppPermissions|IsInRole|HasRole\(|QUAN_TRI_HE_THONG|BAN_THUONG_VU|BI_THU_CHI_BO|TO_THAM_DINH|DANG_UY_CO_SO" backend/src frontend --include=*.cs --include=*.ts --include=*.tsx` → chỉ còn trong `DataSeeder` (tìm vai trò mặc định theo `Code`).
- Báo cáo: danh sách endpoint admin (method, route, quyền, request/response) cho task 11; bảng endpoint → quyền → phạm vi sau khi đổi.
