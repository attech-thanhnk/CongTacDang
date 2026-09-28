# Task 08 — Tài khoản & phiên làm việc (Đợt 4)

- **Agent:** A · **Branch:** `feat/accounts` (tạo từ `main` **sau khi** task 07 đã merge)
- **Mã:** T-56, T-57, T-58, T-59, T-60 (phần tài khoản), phần test tài khoản của T-42
- **Báo cáo:** `docs/remediation/reports/08-accounts-sessions.md`
- **Đọc trước:** `CLAUDE.md`, `RULES.md` (mục 8), `docs/thiet-ke/phan-quyen.md` (mục 1, 5), báo cáo task 07 (chữ ký interface).

## Luồng phải chạy đúng (mỗi luồng = ≥ 1 test tích hợp)

| # | Luồng | Kết quả mong đợi |
|---|---|---|
| L1 | Quản trị tạo tài khoản | Nhập **tên đăng nhập** + thông tin → API trả `username` + **mật khẩu tạm đúng 1 lần** → người đó đăng nhập được ngay |
| L2 | Đăng nhập lần đầu | Còn `MustChangePassword` → **mọi API trả 403 mã `PASSWORD_CHANGE_REQUIRED`**, trừ `GET /api/auth/me`, `POST /api/auth/change-password`, `POST /api/auth/logout`, `POST /api/auth/refresh-token` → đổi mật khẩu xong gọi API bình thường |
| L3 | Quên mật khẩu | Quản trị đặt lại → mật khẩu tạm 1 lần → **mọi phiên cũ bị thu hồi ngay** (access token cũ bị từ chối ở request kế tiếp) → quay về L2 |
| L4 | Khóa tài khoản | Quản trị khóa → request kế tiếp của người đó bị 401, refresh bị từ chối → mở khóa → đăng nhập lại được |
| L5 | Xóa tài khoản | Xóa mềm → như L4, không đăng nhập lại được; tên đăng nhập không tái sử dụng |
| L6 | Bị khóa do sai mật khẩu | 5 lần sai → khóa tạm; quản trị **mở khóa** được ngay (không cần đổi mật khẩu) |
| L7 | Tự đổi mật khẩu | Đổi xong → các phiên **khác** bị thu hồi, phiên hiện tại giữ |
| L8 | Nhật ký đăng nhập | Mọi lần đăng nhập thành công / sai mật khẩu / bị khóa / tài khoản vô hiệu / không tồn tại đều được ghi; xem được qua API |
| L9 | Chốt chặn | Không khóa/xóa chính mình; không khóa/xóa người cuối cùng còn quyền quản trị (mục 5 thiết kế) → 409 |

## Việc cần làm

### 1. Security stamp & kiểm tra phiên mỗi request (T-57)
- JWT **chỉ** chứa: `sub`, `username`, `name`, `sstamp`, `jti`, `exp`… — **bỏ** claim role/perm (task 07 đã chuyển phân quyền sang resolver).
- Trong `JwtBearerEvents.OnTokenValidated` (đặt ở `SecurityExtensions.cs`): nạp trạng thái tài khoản (cache bộ nhớ theo userId, xóa cache khi thay đổi) → từ chối (401) nếu tài khoản không tồn tại / đã xóa / `!IsActive` / `sstamp` khác `SecurityStamp` hiện tại.
- Đổi `SecurityStamp` + thu hồi refresh token khi: đặt lại mật khẩu, khóa, xóa. Khi tự đổi mật khẩu: đổi stamp, **cấp lại** cookie cho phiên hiện tại, thu hồi refresh token khác.
- Gọi `IAccessCacheInvalidator.InvalidateUser` ở các thời điểm trên.

### 2. Bắt buộc đổi mật khẩu phía máy chủ (T-58)
- Middleware (sau `UseAuthentication`) chặn theo danh sách cho phép ở L2; trả `ApiResponse.Fail` kèm `code = "PASSWORD_CHANGE_REQUIRED"` (thêm trường `Code` vào `ApiResponse` nếu chưa có — giữ tương thích).
- Frontend: `apiClient.ts` gặp mã này → chuyển tới `/change-password` (không refresh, không đăng xuất). `AuthGuard` giữ logic hiện có.

### 3. Vòng đời tài khoản (T-56, T-59)
- Triển khai đầy đủ `IUserAccountService` (task 07 đã khai báo) và mở rộng: `CreateAsync`, `UpdateAsync`, `SetActiveAsync(bool)`, `UnlockAsync`, `ResetPasswordAsync`, `DeleteAsync`.
- Tên đăng nhập: bắt buộc, chuẩn hóa chữ thường, `^[a-z0-9._-]{3,50}$`, **duy nhất kể cả tài khoản đã xóa** (giữ unique index hiện có). Email tùy chọn, đúng định dạng nếu có.
- `UserController`: `POST /api/users` (thay `/create`, giữ route cũ trả 410 hoặc alias — ghi trong báo cáo), `PUT /api/users/{id}`, `POST /api/users/{id}/activate|deactivate|unlock|reset-password`, `DELETE /api/users/{id}`. Policy: `system.users.manage`; đọc: `system.users.read`.
- `GET /api/users` có **phân trang + tìm kiếm** (`page`, `pageSize ≤ 200`, `q`, `departmentId`, `partyCellId`, `isActive`), trả `username`, trạng thái khóa, lần đăng nhập cuối. Giữ `/list` tạm thời cho frontend cũ.
- Bỏ đoạn suy vai trò từ `PartyRole` trong `UserService.MapToProfileDto` (T-60): hồ sơ trả đúng quyền thật từ resolver.
- Chốt chặn L9: để kiểm tra "người cuối cùng còn quyền quản trị", dùng `IPermissionResolver` duyệt người đang hoạt động có `system.roles.manage`/`system.assignments.manage` (hoặc truy vấn tương đương đặt trong repository).
- Chính sách mật khẩu cấu hình được: `Security:Password:MinLength` (mặc định 8), yêu cầu chữ + số giữ nguyên.

### 4. Nhật ký đăng nhập (T-59)
- Entity `LoginEvent` (`Id`, `UserId?`, `UsernameAttempted`, `Result` enum `Success|InvalidPassword|UnknownUser|LockedOut|Disabled`, `IpAddress`, `UserAgent`, `CreatedAt`), cấu hình trong `Infrastructure/Data/Configurations/LoginEventConfiguration.cs`, index `(UserId, CreatedAt)`, `(CreatedAt)`.
- Ghi trong `AuthService.LoginAsync` (không làm lộ thời gian phản hồi: ghi cả nhánh không tồn tại).
- `GET /api/audit/logins?userId=&from=&to=&result=&page=` — policy `system.audit.read`. Chuyển `AuditController` hiện có sang `system.audit.read`.
- `PartyMemberProfile.LastLoginAt` (cập nhật khi đăng nhập thành công).

### 5. Tránh xung đột với task 09 (chạy song song)
- **Không** dùng `PartyMemberProfile.Roles`, `AppRole`, `AppRoles`, `AppPermissions` trong code mới — task 09 xóa các thứ này. Mọi thông tin quyền lấy qua `IPermissionResolver`; mã quyền dùng `PermissionCodes`.
- Chỗ code hiện có của mình đang nạp `Roles` (ví dụ `GetWithRolesAndPermissionsAsync` trong đăng nhập) → bỏ phần nạp vai trò nếu không còn cần sau task 07.

## Phạm vi file (xem bảng RULES mục 8.2)
Chủ sở hữu: `AuthService`, `AuthController`, `JwtService`, `SecurityExtensions`, `UserAccountService`, `UserService`, `UserController`, `AuditController`, `AuditService`, `RefreshTokenRepository`, phần `UserRepository` trong `SpecificRepositories.cs`, entity `PartyMemberProfile` (trường tài khoản), `RefreshToken`, `LoginEvent`; frontend `services/apiClient.ts`, `services/authService.ts`, `components/layout/AuthGuard.tsx`, `app/change-password/**`, `app/login/**`.
**Không** sửa: `DataSeeder` (ghi nhu cầu seed vào "Cần phối hợp"), `frontend/app/users/**` (task 11 làm giao diện), mọi file vai trò/quyền.

## Test
- Tích hợp: L1–L9 (mỗi luồng một test, file `AccountFlowTests.cs`, `LoginAuditTests.cs`).
- Unit: chuẩn hóa/validate username, chốt chặn L9, middleware đổi mật khẩu (danh sách cho phép).

## Tiêu chí hoàn thành
- L1–L9 pass với PostgreSQL thật (ghi rõ nếu không chạy được).
- Báo cáo: bảng endpoint mới/đổi (method, route, quyền, request/response) để task 11 làm giao diện.
