# Báo cáo: Task 11 — Giao diện quản trị tài khoản, vai trò, phân quyền

- **Branch:** `feat/admin-ui` (fast-forward từ `473665f` trước khi sửa file đầu tiên)
- **Commit cuối:** commit chứa báo cáo này (commit code cuối: `3d29610`)
- **Ngày:** 2026-09-28

## Kết quả theo mã lỗi

| Mã | Trạng thái | Tóm tắt thay đổi | File chính |
|---|---|---|---|
| T-65 | partial (code xong; **chưa kiểm trên trình duyệt**, xem mục Luồng) | Thay trang `app/users` (1.230 dòng) bằng các trang tách riêng: `/admin/users` (danh sách phân trang phía máy chủ, tìm kiếm, lọc Phòng/Chi bộ/trạng thái, cột trạng thái + đăng nhập cuối, tạo tài khoản), `/admin/users/[id]` (thông tin + sửa/vô hiệu hóa/mở lại/mở khóa đăng nhập/đặt lại mật khẩu/xóa có hộp xác nhận; tab bản gán vai trò + lịch sử; tab "Người này làm được gì"), `/admin/roles` (danh sách, tạo, đổi tên/mô tả, xóa), `/admin/roles/[id]` (ma trận quyền theo phân hệ + người được gán). Hộp thoại mật khẩu tạm dùng chung cho tạo tài khoản và đặt lại mật khẩu (nút sao chép có phương án dự phòng khi chạy HTTP trong LAN — `navigator.clipboard` chỉ có ở ngữ cảnh an toàn). `app/users/page.tsx` → `redirect("/admin/users")`. | `app/admin/**`, `components/admin/**`, `app/users/page.tsx` |
| T-56 (giao diện) | done | Menu và nút theo mã quyền: `AppSidebar` chia nhóm chính + nhóm "Quản trị" (Tài khoản `system.users.read`, Vai trò `system.roles.manage`, Danh mục `catalog.manage`, Nhập dữ liệu `system.import`, Nhật ký `system.audit.read`); thêm "Việc cần xử lý" (`/work-queue`, có bất kỳ `evaluation.*` trừ `evaluation.read`) và "Kỳ đánh giá" (`/periods`, `period.manage`) — route do task 12 tạo. Bỏ mục "Cán bộ & Tổ chức". Thanh bên và đầu trang không còn hiện `user.roles[0]` thô: `sessionRoleLabel()` hiện tên vai trò đầu tiên (bỏ qua giá trị dạng mã `^[A-Z0-9_]+$` do máy chủ cũ trả), mặc định "Cán bộ". `AuthContext`: nhận `permissions` + `grants` (chuẩn hóa `scopeType` dạng chuỗi **hoặc** số 0/1/2); `hasPermission(code)` giữ chữ ký; thêm `hasPermissionIn(code, scopeType, scopeId?)` (bản cấp Toàn công ty bao trùm mọi phạm vi; không có `grants` → chỉ dựa `permissions`); không có `hasRole`. Nút quản lý tài khoản xét `system.users.manage` trong phạm vi Phòng/Chi bộ của chính tài khoản đó; trang vai trò và gán vai trò yêu cầu quyền ở phạm vi Toàn công ty. | `contexts/AuthContext.tsx`, `components/layout/AppSidebar.tsx`, `components/layout/AppHeader.tsx` |
| T-59 (giao diện) | done | Trang `/audit` có 2 tab: "Nhật ký thao tác" (trang cũ, thêm lọc `UserRoleAssignment`, `AdministrativeDepartment`) và "Nhật ký đăng nhập" (lọc tài khoản — ô tìm tài khoản, kết quả, từ ngày/đến hết ngày; phân trang phía máy chủ; `GET /api/audit/logins`). | `app/audit/page.tsx`, `components/admin/LoginEventsTab.tsx`, `services/auditService.ts` |
| T-55 (giao diện) | done | Gán vai trò từ trang người dùng (vai trò chọn từ danh sách) hoặc từ trang vai trò (người chọn qua ô tìm kiếm): loại phạm vi Toàn công ty/Phòng/Chi bộ, đối tượng phạm vi lấy từ danh mục (`/api/organizations/departments|branches`, chỉ mục đang hoạt động), hiệu lực từ ngày / đến hết ngày (gửi `validTo` = 0 giờ ngày hôm sau vì máy chủ coi là mốc không bao gồm), ghi chú. Vai trò chứa quyền "không áp dụng phạm vi" → khóa loại phạm vi về Toàn công ty kèm giải thích. Sửa thời hạn/ghi chú (ngày không đổi → gửi lại đúng mốc cũ), kết thúc ngay, xóa bản gán; lịch sử bản gán hết hạn. Tài khoản của chính mình: nút gán/sửa/thu hồi bị tắt kèm giải thích (máy chủ vẫn chặn 403). | `components/admin/AssignmentFormModal.tsx`, `AssignmentTable.tsx`, `UserPicker.tsx`, `services/roleService.ts` |

Service viết lại theo endpoint task 08/09/10, kiểu TypeScript chặt, không `any`: `services/userService.ts` (giữ `getUsers()`/`CadreItem` cho `app/page.tsx` — ngoài phạm vi task này, vẫn gọi `GET /api/users/list`), `services/roleService.ts`, `services/auditService.ts`.

**Partial T-65:** không chạy được ứng dụng để đi qua U1–U8 (xem "Luồng"). Không báo luồng nào là pass.

## Luồng

Máy này không có Docker/PostgreSQL cục bộ và không có chuỗi kết nối tới máy chủ 159 cho CSDL tạm `ctd_it_ui11` (biến `CONGTACDANG_TEST_PG` không đặt; không đọc `user-secrets`). Vì vậy **chưa chạy backend, chưa kiểm trên trình duyệt**; không tạo CSDL nào. Cột "Kiểm tra tĩnh" là đối chiếu code với DTO/controller backend hiện tại.

| # | Luồng | Trạng thái | Kiểm tra tĩnh |
|---|---|---|---|
| U1 | Tạo tài khoản → hộp thoại tên đăng nhập + mật khẩu tạm, nút sao chép, cảnh báo "chỉ hiển thị một lần" | không chạy | `POST /api/users` → `CreatedAccount {userId, username, temporaryPassword}` khớp `userService.create`; mật khẩu chỉ nằm trong state `TemporaryPasswordModal`, đóng là mất |
| U2 | Danh sách: tìm kiếm, lọc Phòng/Chi bộ/trạng thái, phân trang máy chủ; cột trạng thái, đăng nhập cuối | không chạy | query `page,pageSize,q,departmentId,partyCellId,isActive` → `PagedResult {items,page,pageSize,totalCount,totalPages}` khớp; trạng thái từ `isActive`/`isLockedOut`/`mustChangePassword`. Lọc "bị khóa tạm" không có tham số máy chủ → chỉ lọc Hoạt động/Vô hiệu |
| U3 | Sửa, khóa/mở, mở khóa đăng nhập, đặt lại mật khẩu, xóa — hộp xác nhận; 409 hiện đúng thông báo máy chủ | không chạy | các route `PUT /users/{id}`, `/activate`, `/deactivate`, `/unlock`, `/reset-password`, `DELETE` khớp; lỗi hiển thị `ApiError.message` (lấy `message` của `ApiResponse`), tiêu đề "Không thể thực hiện" cho 409 |
| U4 | Vai trò: danh sách, tạo, đổi tên/mô tả, xóa (409 → số bản gán); ma trận quyền theo module, mô tả, đánh dấu "không áp dụng phạm vi" | không chạy | `AdminRoleDto`, `PermissionModuleDto` khớp; hộp xác nhận xóa nêu `assignmentCount`, lỗi 409 hiện thông báo máy chủ; vai trò bảo vệ khóa 2 quyền quản trị trong ma trận |
| U5 | Gán vai trò từ trang người dùng / trang vai trò; phạm vi + đối tượng từ danh mục; thời hạn; ghi chú; kết thúc / xóa; lịch sử | không chạy | `CreateRoleAssignmentRequestDto` (`scopeType` chuỗi), `UpdateRoleAssignmentRequestDto`, `/end`, `DELETE`, `GET ?userId|roleId` (không `activeOn` → gồm lịch sử) khớp |
| U6 | "Người này làm được gì": quyền + phạm vi + vai trò nguồn | không chạy | `UserEffectivePermissionsDto` khớp; tài khoản vô hiệu → cảnh báo không có quyền |
| U7 | Nhật ký: tab thao tác + tab đăng nhập (lọc người, kết quả, thời gian) | không chạy | `GET /api/admin/audit`, `GET /api/audit/logins?userId,from,to,result,page,pageSize` khớp; `result` là tên enum (`Success`…), binder nhận chuỗi |
| U8 | Menu và nút theo quyền; không có chuỗi mã vai trò trong `frontend/` | partial | Menu/nút chỉ dùng mã quyền. Grep còn **1 dòng** — chú thích trong `services/authService.ts:11` (ngoài phạm vi file, xem "Cần phối hợp") |

## Cổng kiểm tra

| Bước | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build` | không chạy | task chỉ sửa frontend |
| `dotnet test` | không chạy | task chỉ sửa frontend |
| `npx tsc --noEmit` | pass | |
| `npm run build` | pass | 17 route; cảnh báo ESLint `react-hooks/exhaustive-deps` có sẵn (audit `loadLogs`, evaluations, attachments) |
| Grep mã vai trò (task file) | **1 kết quả** | `services/authService.ts:11` — chú thích `(CAN_BO, BI_THU_CHI_BO, ...)`; không có trong file thuộc phạm vi task 11 |
| Chạy app U1–U8 | không chạy | không có CSDL tạm (xem "Luồng") |

## Thay đổi schema (cần migration)
- Không có.

## Key config mới
Không có.

## Thay đổi hành vi API / breaking change
- Không đổi API. Frontend:
  - `/users` chuyển hướng sang `/admin/users`; menu "Cán bộ & Tổ chức" bỏ, thay bằng nhóm "Quản trị".
  - `userService` bỏ `getProfile`, `createUser`, `updateUser`, `deleteUser`, `getRoles` (API cũ `/users/create`, `/users/roles` không còn được frontend gọi); `roleService` bỏ lớp chuyển đổi tạm của task 09 (`getAdminRoles`, `assignUserRoles` → `POST /admin/users/{id}/roles` không còn được gọi).
  - `AuthContext.user` có kiểu `AuthUser` (= `UserSession` + `grants: PermissionGrant[] | null`); thêm `hasPermissionIn`, hàm `sessionRoleLabel`.

## Cần phối hợp
- **`frontend/services/authService.ts:11`** (ngoài phạm vi file task 11): sửa chú thích `/** Danh sách vai trò hệ thống (CAN_BO, BI_THU_CHI_BO, ...) */` thành `/** Tên vai trò từ các bản gán đang hiệu lực (chỉ để hiển thị) */` để grep U8 rỗng; nên thêm `grants?: { code: string; scopeType: string | number; scopeId: string | null; scopeName: string }[]` vào `UserSession` (hiện `AuthContext` tự đọc trường này nên không bắt buộc).
- **Kiểm tra thực tế U1–U8:** cần người điều phối chạy (backend 51xx + CSDL tạm, frontend 31xx) hoặc cấp chuỗi kết nối cho CSDL tạm `ctd_it_*` qua biến môi trường.
- **Người chỉ có `system.assignments.manage`** (không có `system.roles.manage`) không lấy được danh sách vai trò/danh mục quyền (`GET /api/admin/roles`, `/api/admin/permissions` yêu cầu `system.roles.manage`) nên không chọn được vai trò khi gán (giao diện hiện thông báo lỗi máy chủ, vẫn xem/kết thúc/xóa bản gán được). Đề xuất task 09: cho `GET /api/admin/roles` (chỉ đọc) và `GET /api/admin/permissions` chấp nhận thêm `system.assignments.manage`.
- `/api/auth/me` hiện trả `roles` = mã vai trò cũ (`LegacyRoleCodes`) và chưa có `grants`; frontend chạy được cả hai trường hợp (agent tích hợp đang đổi).
- `app/page.tsx` (Tổng quan, ngoài phạm vi) còn gọi `GET /api/users/list` (policy mã cũ `users.read`) — cần chuyển sang `GET /api/users` khi bỏ endpoint cũ.

## Phát hiện thêm
- Không có tham số lọc "đang bị khóa tạm" ở `GET /api/users` — nếu nghiệp vụ cần, thêm `isLockedOut` (task 08).
