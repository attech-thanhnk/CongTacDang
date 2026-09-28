# Báo cáo: Task 08 — Tài khoản & phiên làm việc

- **Branch:** `feat/accounts`
- **Commit cuối:** commit chứa báo cáo này (commit code cuối: `c8c02e8`)
- **Ngày:** 2026-09-28

> Ghi chú môi trường: worktree được tạo ở commit cũ `86cd795`; đã `git merge --ff-only 8a64c85` (đầu `main`) trước khi sửa file đầu tiên.

## Kết quả theo mã lỗi

| Mã | Trạng thái | Tóm tắt thay đổi | File chính |
|---|---|---|---|
| T-56 | done | `IUserAccountService` triển khai đầy đủ (giữ nguyên chữ ký `CreateAsync`, thêm `SearchAsync`, `GetAsync`, `UpdateAsync`, `SetActiveAsync`, `UnlockAsync`, `ResetPasswordAsync`, `DeleteAsync`). Tên đăng nhập bắt buộc, chuẩn hóa chữ thường, `^[a-z0-9._-]{3,50}$`, duy nhất **kể cả tài khoản đã xóa** (không phân biệt hoa thường); email tùy chọn, kiểm tra định dạng; Phòng/Chi bộ phải tồn tại và đang hoạt động; mật khẩu tạm trả đúng 1 lần. `UserController` chuyển sang các endpoint mới (bảng dưới). Bỏ `UserService.CreateUserAsync/UpdateUserAsync/DeleteUserAsync/ResetPasswordAsync` (bản sinh `cb_xxxxxxxx`). | `Application/Services/(I)UserAccountService.cs`, `Application/Accounts/AccountRules.cs`, `Application/DTOs/AccountDtos.cs`, `Api/Controllers/UserController.cs`, `Infrastructure/Repositories/UserAccountRepository.cs` |
| T-57 | done | JWT chỉ còn `sub`, `unique_name`, `username`, `name`, `sstamp`, `jti`, `exp` (bỏ role/perm/party_role). `JwtBearerEvents.OnTokenValidated` (đặt qua `PostConfigure` trong `SecurityExtensions.AddAccountSessionValidation`) nạp trạng thái tài khoản (cache bộ nhớ theo userId, TTL 60 s, xóa ngay khi thay đổi) → 401 nếu không tồn tại / đã xóa / `!IsActive` / `sstamp` ≠ `SecurityStamp`. Đặt lại mật khẩu, khóa, xóa: đổi stamp + thu hồi mọi refresh token + `IAccessCacheInvalidator.InvalidateUser` + xóa cache trạng thái. Tự đổi mật khẩu: đổi stamp, cấp lại cookie `auth_token` cho phiên hiện tại, giữ refresh token hiện tại, thu hồi refresh token khác. Refresh bị từ chối nếu tài khoản bị khóa/xóa. 401 nay trả kèm `ApiResponse`. | `Api/Services/JwtService.cs`, `Application/Common/Interfaces/IJwtService.cs`, `Api/Extensions/SecurityExtensions.cs`, `Application/Accounts/AccountState.cs`, `Application/Services/AuthService.cs`, `Api/Controllers/AuthController.cs` |
| T-58 | done | `PasswordChangeRequiredMiddleware` (sau `UseAuthentication`, trước `UseAuthorization`): tài khoản còn `MustChangePassword` → mọi API trả 403 `{ success:false, code:"PASSWORD_CHANGE_REQUIRED", message }`, trừ `GET /api/auth/me`, `POST /api/auth/change-password`, `POST /api/auth/logout`, `POST /api/auth/refresh-token` (và `POST /api/auth/login`). Thêm `ApiResponse.Code` (bỏ khỏi JSON khi null — tương thích). Frontend: `apiClient.ts` gặp mã này → chuyển tới `/change-password` (không refresh, không đăng xuất); `AuthGuard` giữ nguyên. | `Api/Middlewares/PasswordChangeRequiredMiddleware.cs`, `Application/Common/Models/ApiResponse.cs`, `frontend/services/apiClient.ts`, `frontend/services/authService.ts`, `frontend/app/change-password/page.tsx` |
| T-59 (phần tài khoản) | done | Entity `LoginEvent` + `LoginEventConfiguration`; ghi mọi lần đăng nhập (`Success`, `InvalidPassword`, `UnknownUser` — kể cả nhánh không tồn tại, `LockedOut`, `Disabled`) kèm IP, User-Agent; `PartyMemberProfile.LastLoginAt`. `GET /api/audit/logins` (lọc `userId`, `from`, `to`, `result`, phân trang); `AuditController` chuyển sang `system.audit.read`. Mở khóa đăng nhập (`/unlock`); danh sách tài khoản phân trang + tìm kiếm; chốt chặn L9 (`IAdministratorGuard`): không tự khóa/xóa mình, không khóa/xóa người cuối cùng đang hoạt động có `system.roles.manage`/`system.assignments.manage` (phạm vi Global, tính qua `IPermissionResolver`) → 409. Chính sách mật khẩu cấu hình được. Khi đang khóa tạm thời, từ chối **trước** khi xét mật khẩu (trước đây mật khẩu đúng trả thông báo khác → lộ mật khẩu đúng trong thời gian khóa) và không gia hạn khóa. | `Application/Services/AuthService.cs`, `Application/Services/AuditService.cs`, `Api/Controllers/AuditController.cs`, `Application/Accounts/AdministratorGuard.cs`, `Domain/Entities/LoginEvent.cs`, `Infrastructure/Data/Configurations/LoginEventConfiguration.cs` |
| T-60 | done | `UserService.MapToProfileDto` không còn suy vai trò từ `PartyRole`; `roles`/`permissions` của hồ sơ (`/api/users/profile`, `/api/auth/me`) lấy từ `IPermissionResolver`. `/api/auth/me` tra theo Id (claim `sub`) thay vì username. | `Application/Services/UserService.cs`, `Api/Controllers/AuthController.cs` |
| T-42 (phần tài khoản) | done | 9 test tích hợp luồng tài khoản + 2 test nhật ký đăng nhập; 44 unit test mới. | `backend/tests/CongTacDang.IntegrationTests/{AccountFlowTests,LoginAuditTests,AccountTestFixture}.cs`, `backend/tests/CongTacDang.UnitTests/AccountUnitTests.cs` |

## Cổng kiểm tra

| Bước | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build backend/CongTacDang.slnx` | pass | 0 warning, 0 error |
| `dotnet test` — unit | pass | 140 test: 139 pass, 1 skip (`PdfConversionTests.Mau01…` — thiếu LibreOffice, skip sẵn có), 0 fail (44 test mới) |
| `dotnet test` — integration (có `CONGTACDANG_TEST_PG`, máy chủ 159) | pass | **24 test chạy: 24 pass, 0 skip, 0 fail** (13 test cũ + 11 test mới của task này) |
| `dotnet test` — integration (không có biến) | pass (skip) | 16 test HTTP **skip** kèm lý do, 8 test tên CSDL pass — skip không tính là pass |
| `npx tsc --noEmit` | pass | |
| `npm run build` | pass | |
| CSDL tạm sau khi chạy | không kiểm tra trực tiếp | Máy không có `psql`; `ApiFactory` xóa CSDL `ctd_it_*` bằng `DROP DATABASE … WITH (FORCE)` khi kết thúc và dọn CSDL sót > 24 giờ. Test task 08 tạo **thêm 1 CSDL tạm** (collection riêng). |

### Luồng/test

| Luồng | Trạng thái | Test |
|---|---|---|
| L1 Quản trị tạo tài khoản → username + mật khẩu tạm 1 lần → đăng nhập được ngay (không phân biệt hoa thường); trùng → 409; sai mẫu/email/Phòng không tồn tại → 400 | pass | `AccountFlowTests.L1_AdminCreatesAccount_UserCanLoginImmediately` |
| L2 Còn `MustChangePassword` → mọi API 403 `PASSWORD_CHANGE_REQUIRED` trừ danh sách cho phép; đổi xong gọi API bình thường | pass | `AccountFlowTests.L2_FirstLogin_MustChangePassword_BlocksApisUntilChanged` |
| L3 Đặt lại mật khẩu → phiên cũ bị thu hồi ngay (access + refresh) → mật khẩu tạm → quay về L2 | pass | `AccountFlowTests.L3_ResetPassword_RevokesSessionsImmediately_ThenFirstLoginFlow` |
| L4 Khóa → request kế tiếp 401, refresh bị từ chối, đăng nhập bị từ chối → mở khóa → đăng nhập lại được | pass | `AccountFlowTests.L4_Deactivate_RejectsNextRequestAndRefresh_ActivateAllowsLoginAgain` |
| L5 Xóa mềm → như L4, không đăng nhập lại được; tên đăng nhập không tái sử dụng (409) | pass | `AccountFlowTests.L5_Delete_RevokesSessions_BlocksLogin_UsernameNotReusable` |
| L6 5 lần sai → khóa tạm (mật khẩu đúng cũng bị từ chối); quản trị mở khóa → đăng nhập ngay, không phải đổi mật khẩu | pass | `AccountFlowTests.L6_FiveWrongPasswords_LockOut_AdminUnlockRestoresLogin` |
| L7 Tự đổi mật khẩu → phiên khác bị thu hồi, phiên hiện tại giữ (kể cả refresh) | pass | `AccountFlowTests.L7_ChangeOwnPassword_RevokesOtherSessions_KeepsCurrent` |
| L8 Ghi đủ Success / InvalidPassword / UnknownUser / Disabled / LockedOut, xem qua API, lọc theo user/kết quả/thời gian; `LastLoginAt` cập nhật; cần `system.audit.read` | pass | `LoginAuditTests.L8_EveryLoginOutcomeIsRecorded_AndQueryable`, `LoginAuditTests.AuditEndpoints_RequireAuditReadPermission` |
| L9 Không khóa/xóa chính mình; không khóa/xóa người cuối cùng còn quyền quản trị → 409 | pass | `AccountFlowTests.L9_CannotDisableSelf_OrLastAdministrator`; unit: `AccountUnitTests.AdminGuard_*` (6 test) |
| Danh sách phân trang/tìm kiếm, `pageSize` ≤ 200, cần quyền đọc/quản lý | pass | `AccountFlowTests.UsersList_IsPagedAndSearchable_RequiresReadPermission` |
| Unit: chuẩn hóa/validate username, email, chính sách mật khẩu, cache trạng thái, danh sách cho phép của middleware | pass | `AccountUnitTests.Username_*`, `Email_*`, `PasswordPolicy_*`, `AccountState*`, `PasswordGate_*`, `ApiResponse_OmitsCodeWhenNull` |

**Hạ tầng test:** test task 08 dùng collection riêng `accounts` (`AccountTestFixture`) với một `ApiFactory` **riêng** (CSDL tạm riêng) để L9 kiểm soát được ai đang giữ quyền quản trị, và một host dẫn xuất `WithWebHostBuilder` đặt `Security:RateLimit:Auth:PermitLimit=1000` (luồng khóa cần > 10 lần đăng nhập/phút). **`ApiFactory` không bị sửa**; giới hạn mặc định (10/phút/IP) không đổi.

**Test cũ:** toàn bộ pass **không sửa kỳ vọng**. Chỉ sửa để biên dịch: `SecurityTests.FakeJwtService.GenerateToken` theo chữ ký mới `GenerateToken(PartyMemberProfile)`. `AuthzContractTests` (dùng `new UserAccountService(users)`) không phải sửa — giữ constructor tối giản cho test.

## Endpoint mới / đổi (cho task 11)

Mọi phản hồi bọc `ApiResponse` `{ success, message, data, errors?, code?, timestamp }`. Lỗi: 400 dữ liệu sai, 403 thiếu quyền (thông báo nêu tên quyền), 404 không thấy, 409 xung đột/chốt chặn. `approvalAuthority` là số: `1` = Đảng ủy cơ sở (`CoSo`), `2` = cấp trên (`CapTren`). Thời gian là UTC ISO-8601.

| Method | Route | Quyền | Request | Response `data` |
|---|---|---|---|---|
| GET | `/api/users` | `system.users.read` (lọc theo phạm vi của guard) | query `page` (≥1, mặc định 1), `pageSize` (mặc định 20, tối đa 200 — lớn hơn bị cắt về 200), `q` (tên đăng nhập / họ tên / email / số thẻ Đảng, không phân biệt hoa thường), `departmentId`, `partyCellId`, `isActive` | `{ items: AccountListItem[], page, pageSize, totalCount, totalPages }` |
| GET | `/api/users/{id}` | `system.users.read` | — | `AccountListItem` (**đổi**: trước trả `CadreDto`) |
| POST | `/api/users` | `system.users.manage` | `{ username*, fullName*, email?, phoneNumber?, partyCardNumber?, positionTitle? (hoặc adminTitle), departmentId?, partyCellId?, approvalAuthority? (mặc định 1) }` | `{ userId, username, temporaryPassword }` — mật khẩu tạm **chỉ trả lần này** |
| POST | `/api/users/create` | `system.users.manage` | như `POST /api/users` (bí danh tạm thời; thiếu `username` → 400) | như trên |
| PUT | `/api/users/{id}` | `system.users.manage` | `{ fullName?, email?, phoneNumber?, partyCardNumber?, positionTitle?/adminTitle?, departmentId?, partyCellId?, approvalAuthority?, isActive? }` — null/không gửi = giữ nguyên; `""` = xóa (email/điện thoại/số thẻ); `Guid.Empty` = bỏ gán Phòng/Chi bộ; `isActive` (tương thích cũ) = gọi activate/deactivate với cùng chốt chặn. Không đổi được `username`. | `AccountListItem` |
| POST | `/api/users/{id}/activate` | `system.users.manage` | — | — (message) |
| POST | `/api/users/{id}/deactivate` | `system.users.manage` | — ; 409 nếu tự khóa mình / quản trị viên cuối cùng | — |
| POST | `/api/users/{id}/unlock` | `system.users.manage` | — (xóa đếm sai + khóa tạm, không đổi mật khẩu) | — |
| POST | `/api/users/{id}/reset-password` | `system.users.manage` (trước: `users.update`) | — | `{ userId, userName, temporaryPassword, mustChangePassword: true }` |
| DELETE | `/api/users/{id}` | `system.users.manage` (trước: `users.delete`) | — ; 409 như deactivate | — |
| GET | `/api/users/list` | `users.read` (mã cũ, giữ tạm cho giao diện cũ) | — | `CadreDto[]` (không đổi) |
| GET | `/api/audit/logins` | `system.audit.read` | query `userId`, `from`, `to` (UTC; `from > to` → 400), `result` (`Success`\|`InvalidPassword`\|`UnknownUser`\|`LockedOut`\|`Disabled`), `page`, `pageSize` (≤ 200) | `{ items: [{ id, userId, usernameAttempted, result, ipAddress, userAgent, createdAt }], page, pageSize, totalCount, totalPages }` — mới nhất trước |
| GET | `/api/admin/audit` | **`system.audit.read`** (trước: `roles.manage`) | không đổi | không đổi |
| POST | `/api/auth/change-password` | đã đăng nhập (kể cả khi còn mật khẩu tạm) | `{ currentPassword, newPassword }` | **đổi**: trả `LoginResponse` (`id, fullName, userName, roles, permissions, mustChangePassword:false, expiresAt`) và cấp lại cookie `auth_token`; sai mật khẩu hiện tại → **400** (trước: 401) |
| GET | `/api/auth/me` | đã đăng nhập | — | không đổi cấu trúc; `roles`/`permissions` từ resolver (không còn suy từ chức vụ Đảng) |

`AccountListItem`: `{ id, username, fullName, email, phoneNumber, partyCardNumber, isPartyMember, positionTitle, departmentId, departmentName, partyCellId, partyCellName, approvalAuthority, isActive, isLockedOut, lockoutEnd, failedLoginCount, mustChangePassword, lastLoginAt, createdAt }`.

403 do mật khẩu tạm: `{ success:false, code:"PASSWORD_CHANGE_REQUIRED", message:"Bạn đang dùng mật khẩu tạm. Hãy đổi mật khẩu trước khi tiếp tục sử dụng hệ thống." }`.

## Thay đổi schema (cần migration)

| Bảng | Thay đổi | Ghi chú |
|---|---|---|
| `login_events` (mới) | `Id uuid PK`, `UserId uuid NULL` (FK → `party_member_profiles.Id`, `ON DELETE SET NULL`), `UsernameAttempted varchar(100) NOT NULL`, `Result integer NOT NULL`, `IpAddress varchar(100) NULL`, `UserAgent varchar(500) NULL`, `CreatedAt timestamptz NOT NULL`; index `(UserId, CreatedAt)`, `(CreatedAt)` | Cấu hình ở `Infrastructure/Data/Configurations/LoginEventConfiguration.cs` (không có `DbSet`, dùng `Set<LoginEvent>()`). |
| `party_member_profiles` | thêm `LastLoginAt timestamptz NULL` | Không cần chuyển dữ liệu. |
| — (đề xuất, không bắt buộc) | Unique index hiện có trên `Username` phân biệt hoa thường. Code đã chuẩn hóa chữ thường khi tạo và so khớp `lower("Username")` khi đăng nhập/kiểm tra trùng. Trước khi triển khai nên chạy `SELECT lower("Username"), count(*) FROM party_member_profiles GROUP BY 1 HAVING count(*) > 1;` (phải rỗng) và cân nhắc thêm index `lower("Username")` (SQL tay trong migration) nếu bảng lớn. | EF không sinh được index biểu thức; hiện bảng nhỏ nên quét tuần tự chấp nhận được. |

## Key config mới
| Key | Mặc định | Ý nghĩa |
|---|---|---|
| `Security:Password:MinLength` | `8` (giá trị < 8 bị nâng lên 8) | Độ dài tối thiểu mật khẩu mới; luôn yêu cầu có chữ và số. |
| `Security:RateLimit:Auth:PermitLimit` | `10` | Số lần gọi `/api/auth/login`, `/api/auth/refresh-token` mỗi cửa sổ / IP. |
| `Security:RateLimit:Auth:WindowSeconds` | `60` | Độ dài cửa sổ giới hạn. |

## Thay đổi hành vi API / breaking change
- **Mọi người dùng phải đăng nhập lại một lần sau khi triển khai**: access token cũ không có claim `sstamp` bị từ chối (401) → frontend tự refresh → token mới có `sstamp`. Refresh token cũ vẫn dùng được.
- Khóa / xóa / đặt lại mật khẩu có hiệu lực **ngay request kế tiếp** (trước: tới 15 phút). Tự đổi mật khẩu đăng xuất các phiên khác.
- Tài khoản còn mật khẩu tạm bị chặn ở **backend** (403 `PASSWORD_CHANGE_REQUIRED`), không chỉ ở giao diện.
- Tên đăng nhập không phân biệt hoa thường khi đăng nhập. Tài khoản mới phải theo mẫu `^[a-z0-9._-]{3,50}$`; tài khoản cũ không theo mẫu vẫn đăng nhập được.
- `POST /api/auth/change-password`: sai mật khẩu hiện tại 401 → **400** (tránh giao diện hiểu nhầm là hết phiên và đăng xuất); trả thông tin phiên.
- 401 do chưa đăng nhập / phiên bị thu hồi nay có body `ApiResponse` (trước rỗng).
- `POST /api/users/create`: bắt buộc `username` → **giao diện `/users` hiện tại (gửi không có username) nhận 400** cho tới khi task 11 cập nhật. `GET /api/users/{id}` trả `AccountListItem` thay vì `CadreDto`.
- Quyền: `PUT/DELETE/reset-password` tài khoản dùng `system.users.manage` thay cho `users.update`/`users.delete`; `/api/admin/audit` dùng `system.audit.read` thay cho `roles.manage`. Vai trò `QUAN_TRI_HE_THONG` đã có các mã mới (seed task 07) nên quản trị không bị ảnh hưởng; vai trò tùy biến chỉ có mã cũ sẽ mất các thao tác này.
- Đang khóa tạm thời: mọi lần đăng nhập (kể cả mật khẩu đúng) trả thông báo khóa kèm số phút còn lại, không gia hạn khóa.
- JWT không còn claim `role`, `perm`, `party_role`.

## Cần phối hợp
- **Người điều phối:** sinh migration cho `login_events` + `LastLoginAt` (mục "Thay đổi schema"); bổ sung 3 key config vào `appsettings`/`.env.example`; cập nhật `CLAUDE.md` mục "Quy ước" (phân quyền không còn theo claim `perm`/role).
- **`CongTacDangDbContext` (không thuộc phạm vi):** `PrepareAuditEntries` ghi một `AuditLog` cho **mỗi** `LoginEvent` và mỗi lần cập nhật `PartyMemberProfile` khi đăng nhập (`LastLoginAt`, đếm sai) → bảng audit tăng gấp ~3 lần số lần đăng nhập. Đề xuất bỏ qua `LoginEvent` (và các thay đổi chỉ gồm `LastLoginAt`/`FailedLoginCount`/`LockoutEnd`) trong audit; thêm `SecurityStamp` vào `IsSensitiveProperty`.
- **Task 09:** `AuthService.IssueAsync` và `UserService.MapToProfileDtoAsync` còn dùng `EffectivePermissions.LegacyRoleCodes` để điền `roles` (nguồn duy nhất về vai trò từ resolver) — khi xóa `LegacyRoleCodes` hãy đổi hai chỗ này (ví dụ trả `[]` hoặc tên vai trò từ grants). `AdministratorGuard` dùng `GrantsFor(code)` + `ScopeType.Global` nên tương thích resolver có phạm vi/thời hạn; duyệt resolver lần lượt từng tài khoản đang hoạt động (chỉ khi người bị khóa/xóa đang giữ quyền quản trị) — nếu cần có thể thay bằng truy vấn repository. `UserAccountService` kiểm tra `system.users.manage`/`read` qua `IAuthorizationGuard` với `AccessTarget(DepartmentId, PartyCellId)` (không có `OwnerId`) và lọc danh sách bằng `GetScope(system.users.read)` → khi thay guard v0, phạm vi Phòng/Chi bộ có hiệu lực ngay. `/api/users/list` và `/api/users/profile?username=` còn dùng mã cũ / `IAccessPolicy`.
- **Task 10:** `IUserAccountService.CreateAsync` giữ nguyên chữ ký nhưng giờ: (1) chuẩn hóa username chữ thường, từ chối sai mẫu `^[a-z0-9._-]{3,50}$` (400); (2) trùng kể cả tài khoản đã xóa → 409; (3) Phòng/Chi bộ phải tồn tại và `IsActive` (400); (4) email sai định dạng → 400; (5) khi có người dùng đăng nhập, người gọi phải có `system.users.manage` bao trùm Phòng/Chi bộ của tài khoản (403) — đúng thiết kế "`system.import` phải kèm quyền quản lý loại dữ liệu được nhập". Tác vụ không có người dùng (hệ thống) bỏ qua kiểm tra quyền. `CreateAccountCommand` có thêm thuộc tính `init` tùy chọn `PhoneNumber`.
- **Task 11:** dùng bảng endpoint ở trên; cập nhật `frontend/services/userService.ts` + `app/users/**` (form tạo có ô tên đăng nhập, hiển thị mật khẩu tạm 1 lần, nút khóa/mở/mở khóa/đặt lại/xóa, danh sách phân trang, trang nhật ký đăng nhập); xử lý 409 chốt chặn; menu "Nhật ký" theo `system.audit.read`.
- **DataSeeder (không sửa):** không cần seed mới. Lưu ý tài khoản `admin` seed mật khẩu `123456` + `MustChangePassword=true` — nay bị chặn ở backend cho tới khi đổi mật khẩu (đúng ý đồ).
- **Test tích hợp của task khác:** nếu cần nhiều lần đăng nhập, có thể dùng cách của `AccountTestFixture` (`WithWebHostBuilder` + `Security:RateLimit:Auth:PermitLimit`) thay vì sửa `ApiFactory`.

## Phát hiện thêm
- `LoginRequestDto`, `ChangePasswordRequestDto` (và các DTO khác) dùng `string` không-null → thiếu trường thì MVC trả lỗi "The X field is required." bằng tiếng Anh trước khi vào service (RULES 8.5). Mức ⚪. Đã tránh cho `CreateAccountRequestDto`.
- `AccessPolicy.CanAccessProfile` (dùng cho `/api/users/profile?username=`) còn kiểm tra mã cũ `users.read` + vai trò quản trị — người chỉ có `system.users.read` không xem được hồ sơ người khác qua endpoint này. Mức 🟡 (task 09).
- `AccountStateCache`/`PermissionCache` là cache trong bộ nhớ một instance; chạy nhiều instance API cần cache phân tán hoặc TTL rất ngắn (hiện 60 s cho trạng thái tài khoản). Mức ⚪ (đã ghi trong thiết kế mục 7).
- Giao diện `/change-password` chạy trong layout đầy đủ; sidebar/header có thể gọi API và nhận 403 `PASSWORD_CHANGE_REQUIRED` (bị bỏ qua, không chuyển trang lặp) — task 11 có thể ẩn layout khi `mustChangePassword`. Mức ⚪.
