# Báo cáo: Tích hợp Đợt 4 (task 08 ║ 09 ║ 10)

- **Branch:** `chore/wave4-integration` (tạo từ `main` @ `1db3b69`, đã `git merge --ff-only main` và kiểm tra HEAD = `main` trước khi sửa)
- **Commit cuối:** commit chứa báo cáo này (commit code cuối: `6b06055`)
- **Ngày:** 2026-09-28

## Kết quả theo việc

| # | Việc | Trạng thái | Tóm tắt | File chính |
|---|---|---|---|---|
| 1 | Build xanh | done | `ImportFrameworkTests.FakeAccounts` triển khai đủ thành viên mới của `IUserAccountService` (ném `NotSupportedException` — import không dùng). Không đổi kỳ vọng. | `tests/UnitTests/ImportFrameworkTests.cs` |
| 2 | Transaction import | done | `CreateAsync` của task 08 tự `SaveChanges` (không mở transaction riêng) → trong `ExecuteInTransactionAsync` vẫn rollback được, nhưng ghi từng dòng. Thêm `IUserAccountService.StageCreateAsync` (cùng kiểm tra/lỗi, **không lưu** — `IUserAccountRepository.Stage`); import cán bộ dùng nó, khung import lưu cả lô **một lần** trong transaction. Trùng tên với tài khoản đã "stage" chưa lưu cũng bị chặn (409). Đồng bộ kiểm tra tên đăng nhập/email ở bước xem trước với `AccountRules` (trước: xem trước chấp nhận `ab`, `a@b` rồi lúc ghi mới báo 400). | `Application/Services/(I)UserAccountService.cs`, `Common/Interfaces/IUserAccountRepository.cs`, `Infrastructure/Repositories/UserAccountRepository.cs`, `Imports/Definitions/UserImportDefinition.cs` |
| 3 | Hoàn thiện phân quyền ở file 08/10 | done | `/api/auth/me`, login, refresh, change-password trả `grants: [{code, scopeType, scopeId, scopeName}]` (`IRoleAssignmentService.GetGrantsAsync`), giữ `permissions`; `roles` = tên vai trò từ bản gán đang hiệu lực (`EffectivePermissions.RoleNames`, kể cả vai trò chưa có quyền). `UserService` dùng `IAuthorizationGuard` (hồ sơ người khác: `system.users.read` bao trùm Phòng/Chi bộ; không tồn tại → 404 chỉ với người có phạm vi Toàn công ty, còn lại 403 — không lộ tài khoản). `UserController /api/users/list` → `[RequirePermission(system.users.read)]` + lọc `GetScope` (T-61); `GET /api/users` đã lọc sẵn (task 08). `AuditController` đã `system.audit.read` cho mọi endpoint; `OrganizationController` đã dùng `[RequirePermission(catalog.manage)]` (merge đã có) — kiểm tra lại, không cần sửa. Chốt quản trị cuối: **xóa `AdministratorGuard`/`IAdministratorGuard`**, khóa/xóa tài khoản gọi `IRoleAssignmentService.EnsureAdministratorsRemainWithoutUserAsync` (bản gán Global, đang hiệu lực, tài khoản đang hoạt động — `AdministratorInvariant`); chốt "không tự khóa/xóa" chuyển vào `UserAccountService`. Thông báo 409 của chốt có thêm "quản trị viên cuối cùng". Repository: bỏ `.Include(Roles)` (`UserRepository.GetWithRolesAndPermissions*` → `GetWithOrganizationByIdAsync`; `RefreshTokenRepository` sau merge đã không còn include). | `Api/Controllers/AuthController.cs`, `UserController.cs`, `AdminRoleController.cs`, `Application/Services/AuthService.cs`, `UserService.cs`, `UserAccountService.cs`, `RoleAssignmentService.cs`, `DTOs/DtoModels.cs`, `Common/Security/EffectivePermissions.cs` |
| 4 | Gỡ di sản | done | Xóa `AppPermissions.cs`, `AppRoles.cs`, `LegacyPermissionMap.cs`, `ProfileAccessPolicyAdapter.cs` (`IAccessPolicy`, `AccessOperation`), nhánh mã cũ + `AuthenticatedOnly` trong `PermissionPolicyProvider`, `EffectivePermissions.LegacyRoleCodes/HasLegacyRole`, constructor `PermissionResolver(IUserRepository, PermissionCache)` (DI đăng ký thẳng `AddScoped<IPermissionResolver, PermissionResolver>`), `PartyMemberProfile.Roles`, `AppRole.Members`, ánh xạ `user_roles`, `IAttachmentAccessReader.GetUserIdsInRoleAsync`. Seeder: xóa `MigrateLegacyUserRolesAsync` (chuyển dữ liệu nay nằm trong migration). `LegacyPolicies` không còn trong mã sau merge. Test cũ chỉ sửa phần dựng (fake repository, `SecurityTests` dùng resolver giả thay constructor cũ); `AuthzContractTests` đổi 2 khẳng định mã cũ → "mã cũ không còn là policy"; xóa `DataSeederAuthorizationTests.LegacyUserRoles_MigratedToScopedAssignments_Once` (thay bằng `Wave4MigrationTests`). Build **0 warning** (hết mọi `[Obsolete]` tạm). | `Application/Common/Security/*`, `Api/Authorization/PermissionAuthorization.cs`, `Api/Extensions/AuthorizationExtensions.cs`, `Domain/Entities/{AppRole,PartyMemberProfile}.cs`, `Infrastructure/Data/Configurations/AppRoleConfiguration.cs`, `Infrastructure/Data/DataSeeder.cs`, `Infrastructure/Repositories/SpecificRepositories.cs` |
| 5 | Audit & cấu hình | done | `CongTacDangDbContext`: không ghi audit cho `LoginEvent` và cho cập nhật tài khoản mà giá trị **thực đổi** chỉ gồm `LastLoginAt`/`FailedLoginCount`/`LockoutEnd` (+ `UpdatedAt/By`) **khi không có người đăng nhập** (request đăng nhập ẩn danh) — so theo giá trị vì `GenericRepository.UpdateAsync` đánh dấu mọi cột; mở khóa của quản trị vẫn được audit. `SecurityStamp` vào danh sách che (đã có `PasswordHash`). Key `Security:Password:MinLength`, `Security:RateLimit:Auth:{PermitLimit,WindowSeconds}` trong `appsettings.json`, `docker/.env.example` (`Security__…`) và truyền vào container ở `docker-compose.yml`. `next.config.mjs`: `experimental.proxyTimeout = 300000` kèm chú thích. | `Infrastructure/Data/CongTacDangDbContext.cs`, `Api/appsettings.json`, `docker/.env.example`, `docker/docker-compose.yml`, `frontend/next.config.mjs` |
| 6 | Migration `Wave4` | done | Xem mục "Migration Wave4". | `Infrastructure/Data/Migrations/20260928160510_Wave4*.cs`, snapshot |
| 7 | Quản trị ban đầu (bổ sung) | done | `Seed:InitialAdmin:{Username,FullName,Password}` (mặc định rỗng). Seeder (chạy độc lập `SeedSampleData`, sau dữ liệu mẫu): nếu **chưa** có tài khoản hoạt động giữ `system.roles.manage` và `system.assignments.manage` (bản gán Global đang hiệu lực) và cấu hình đủ → tạo tài khoản `MustChangePassword = true`, gán vai trò bảo vệ có đủ 2 quyền quản trị (ưu tiên `QUAN_TRI_HE_THONG`) phạm vi Global. Đã có quản trị → Information, không tạo/không đổi mật khẩu. Thiếu cấu hình → Warning hướng dẫn biến `Seed__InitialAdmin__*`. Mật khẩu sai chính sách / tên sai mẫu / tên đã dùng (kể cả đã xóa) → Error, không tạo. Mật khẩu không ghi log (`InitialAdminOptions.ToString` không in mật khẩu). | `Infrastructure/Data/DataSeeder.cs`, `Api/Extensions/PersistenceExtensions.cs`, `appsettings.json`, `docker/.env.example`, `docker-compose.yml` |
| 8 | Xóa cache quyền sau commit (bổ sung) | done | `UnitOfWork` thêm `IAfterCommitActions` (hoãn tác vụ tới khi `ExecuteInTransactionAsync` commit/rollback; không có transaction → chạy ngay). `IAccessCacheInvalidator` nay là `DeferredAccessCacheInvalidator` (scoped, bọc `PermissionCache` singleton) → mọi chỗ gọi (`RoleAssignmentService`, `RoleService`, `UserAccountService`, `AuthService`) tự hoãn khi đang trong transaction. Test hạ tầng (`ApiFactory`, `RoleAssignmentApiTests`) xóa cache qua `PermissionCache` trực tiếp (không resolve dịch vụ scoped từ root). | `Application/Common/Interfaces/IAfterCommitActions.cs`, `Application/Common/Security/DeferredAccessCacheInvalidator.cs`, `Infrastructure/Persistence/UnitOfWork.cs`, `Api/Extensions/{Persistence,Authorization}Extensions.cs` |
| 9 | Đọc vai trò khi chỉ có quyền gán (bổ sung) | done | `GET /api/admin/roles`, `GET /api/admin/roles/{id}`, `GET /api/admin/permissions`: `[RequireAnyPermission(system.roles.manage, system.assignments.manage)]` + service `EnsureCanReadRoles` (Global); ghi vẫn chỉ `system.roles.manage`. | `Api/Controllers/AdminRoleController.cs`, `Application/Services/RoleService.cs` |
| 10 | Frontend `authService.ts` (bổ sung) | done | Chú thích `roles` theo mô hình mới; thêm kiểu `AccessGrant` và `grants?: AccessGrant[]` (`scopeType: "Global" \| "Department" \| "PartyCell"` — API trả chuỗi, `scopeId: string \| null`, `scopeName: string`). `/api/users/list` (trang chủ, trang `/users`) đã dùng mã mới + lọc phạm vi ở việc 3; sửa chú thích `userService.getUsers`. | `frontend/services/authService.ts`, `frontend/services/userService.ts` |

## Kết quả grep (việc 4)

`grep -rnE "AppRoles|AppPermissions|IAccessPolicy|LegacyRoleCodes|LegacyPermissionMap|IsInRole|HasRole\(" backend/src frontend --include=*.cs --include=*.ts --include=*.tsx` (bỏ `node_modules`, `bin`, `obj`, `.next`): **0 dòng** (kể cả `DataSeeder` — seeder đã dùng hằng số nội bộ `RoleCodes`).

## Migration Wave4

`20260928160510_Wave4` (sinh bằng `dotnet ef migrations add Wave4`, chỉnh tay):

**Up**
1. Khối `DO $$` kiểm tra trước, **`RAISE EXCEPTION`** với danh sách cụ thể, không tự sửa: tên đăng nhập trùng khi hạ chữ thường (kể cả tài khoản đã xóa); vai trò chưa xóa trùng tên (cho index unique mới `IX_roles_Name`); mô tả vai trò > 1000 ký tự (cột `Description` text → varchar(1000)).
2. `UPDATE party_member_profiles SET "Username" = lower("Username")` rồi `CREATE UNIQUE INDEX "IX_party_member_profiles_Username_lower" ON party_member_profiles (lower("Username"))` (index biểu thức — ngoài model EF, `has-pending-model-changes` không thấy; index cũ `IX_party_member_profiles_Username` giữ nguyên).
3. Phần EF sinh: xóa `administrative_departments.HeadId` (task 10: không nơi nào dùng); `roles.IsProtected` (false), `roles.Description` varchar(1000); `permissions.Module` (''), `permissions.SortOrder` (0); `party_member_profiles.LastLoginAt` (NULL); `party_cells.SortOrder`, `administrative_departments.SortOrder` (0); bảng `login_events`, `user_role_assignments` + index; `IX_roles_Name` unique lọc `IsDeleted = FALSE`. Cột NOT NULL mới đều có default cho dữ liệu cũ; `Module/SortOrder` được seeder điền khi khởi động; `IsProtected` của `QUAN_TRI_HE_THONG` do seeder đánh dấu (lần đánh dấu đầu bổ sung 2 quyền quản trị) — migration cố ý không đặt.
4. **Sau** khi tạo `user_role_assignments` và **trước** `DropTable("user_roles")`: `INSERT … SELECT gen_random_uuid(), user_id, role_id, 0 /*Global*/, NULL, now(), NULL, 'Chuyển từ gán vai trò cũ (user_roles) khi nâng cấp Wave4.', now(), FALSE FROM user_roles` — mọi dòng, giữ nguyên vai trò.

**Down** (cố gắng đảo ngược): dựng lại `user_roles` + chép các bản gán Global chưa xóa đang hiệu lực (`DISTINCT`); bản gán Phòng/Chi bộ/thời hạn không biểu diễn được → bỏ. Xóa index `lower(Username)` (cách viết hoa cũ không khôi phục được). Xóa `login_events`, `user_role_assignments`, cột mới; khôi phục `HeadId` (rỗng), `Description` text.

**Kiểm chứng trên CSDL thật (192.168.22.159, chỉ `ctd_it_*`)**
- CLI: tạo `ctd_it_mig_wave4` (script F# + Npgsql trong thư mục tạm, chuỗi kết nối lấy từ biến môi trường), `dotnet ef database update Wave3` → chèn dữ liệu trạng thái Wave3: 3 vai trò cũ (`CAN_BO`, `BI_THU_CHI_BO`, `QUAN_TRI_HE_THONG`), 4 tài khoản (`Admin`, `NguyenVanA` có chữ hoa, `bithu`, `daxoa` đã xóa mềm), 5 dòng `user_roles` → `dotnet ef database update` (Wave4). Kết quả: lịch sử có đủ 3 migration; `user_roles` đã xóa; `user_role_assignments` đúng **5** dòng (admin–QUAN_TRI, bithu–BI_THU, bithu–CAN_BO, daxoa–CAN_BO, nguyenvana–CAN_BO), tất cả `ScopeType = 0`, `ScopeId` NULL, `ValidFrom` = thời điểm nâng cấp, `ValidTo` NULL; tên đăng nhập `admin`, `nguyenvana` (chữ thường); có `IX_party_member_profiles_Username_lower`; `SortOrder` NOT NULL default 0; `IsProtected` false (seeder xử lý). `dotnet ef migrations has-pending-model-changes`: **"No changes have been made to the model since the last migration."** Đã xóa `ctd_it_mig_wave4`.
- Test tự động `Wave4MigrationTests` (CSDL `ctd_it_*` tạo/xóa trong test): (a) Wave3 → chèn dữ liệu cũ → Wave4: 3 bản gán Global đúng cặp, `user_roles` bị xóa, username hạ chữ thường, chèn `NGUYEN.VAN.A` bị `IX_party_member_profiles_Username_lower` chặn (23505); chạy seeder → admin cũ có `system.assignments.manage` Global và tên vai trò trong `RoleNames`; Down về Wave3 → `user_roles` có lại 3 dòng, `user_role_assignments` không còn. (b) Hai tài khoản `TranVanB`/`tranvanb` (một đã xóa) → migration dừng với `RAISE EXCEPTION` "tên đăng nhập trùng … tranvanb", dữ liệu giữ nguyên, CSDL vẫn ở Wave3.

## Cổng kiểm tra

| Bước | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build backend/CongTacDang.slnx --no-incremental` | pass | **0 warning, 0 error** |
| `dotnet test` — unit | pass | 169: **168 pass, 1 skip** (`PdfConversionTests.Mau01…` — thiếu LibreOffice, skip sẵn có), 0 fail |
| `dotnet test` — integration (`CONGTACDANG_TEST_PG`, máy 159) | pass | **57 chạy, 57 pass, 0 skip, 0 fail** — chạy toàn bộ 3 lần liên tiếp đều xanh |
| `npx tsc --noEmit` | pass | |
| `npm run build` | pass | Next.js nhận `experimental.proxyTimeout`; cảnh báo lint `exhaustive-deps`/`no-img-element` có từ trước |
| `docker compose -f docker/docker-compose.yml config` | **không chạy** | máy không có Docker |
| `has-pending-model-changes` | pass | sạch |

Test mới/đổi và luồng:

| Luồng | Trạng thái | Test |
|---|---|---|
| Stage nhiều tài khoản trong transaction, lỗi ở bước ghi (unique) → 0 tài khoản; trùng tên với tài khoản đã stage → 409 | pass | `Wave4IntegrationTests.StagedAccounts_AreNotWritten_WhenSaveFailsInsideTransaction` |
| Import 2 cán bộ, dòng 2 bị 403 lúc ghi (quản lý tài khoản chỉ phạm vi Phòng A) → không tài khoản nào được ghi | pass | `Wave4IntegrationTests.ImportUsers_RowFailingAtCommit_WritesNoAccount` |
| Login + `/me` trả `grants` (mã, loại, Id, tên phạm vi) và `roles` = tên vai trò hiệu lực (kể cả vai trò rỗng) | pass | `Wave4IntegrationTests.LoginAndMe_ReturnGrantsWithScope_AndRoleNamesFromEffectiveAssignments` |
| T-61 `/api/users/list`, `GET /api/users`, `GET /api/users/{id}`, `/api/users/profile?username=` theo phạm vi `system.users.read` | pass | `Wave4IntegrationTests.UserLists_AreFilteredBySystemUsersReadScope` |
| Đăng nhập (sai + đúng) không sinh audit; thao tác quản trị có audit, không lộ `SecurityStamp`/`PasswordHash` | pass | `Wave4IntegrationTests.Login_IsNotDuplicatedInAuditLog_AndSecurityFieldsAreMasked` |
| Xóa cache trong transaction chỉ sau commit (request chen giữa không "đóng băng" quyền cũ) — đã xác nhận test **fail** khi đăng ký lại invalidator tức thời | pass | `Wave4IntegrationTests.PermissionCacheInvalidation_InsideTransaction_HappensAfterCommit` |
| Người chỉ có `system.assignments.manage` đọc được vai trò/danh mục quyền, không tạo/sửa/xóa vai trò; người không có cả hai → 403 | pass | `Wave4IntegrationTests.AssignmentsManager_CanReadRolesAndCatalog_ButCannotChangeRoles` |
| Quản trị ban đầu: CSDL trống + cấu hình → đăng nhập, buộc đổi mật khẩu, có 2 quyền quản trị Global; khởi động lại không đổi mật khẩu/không tạo thêm; đã có quản trị → không tạo tài khoản khác | pass | `Wave4InitialAdminTests.EmptyDatabase_WithConfig_CreatesAdminOnce_MustChangePassword_WithAdminPermissions` |
| Quản trị ban đầu: mật khẩu sai chính sách → không tạo | pass | `Wave4InitialAdminTests.EmptyDatabase_WithPasswordViolatingPolicy_DoesNotCreateAdmin` |
| Migration Wave3 → Wave4 → Down; trùng username → RAISE EXCEPTION | pass | `Wave4MigrationTests.*` (2) |
| Chốt L9 hợp nhất (tự khóa/xóa; quản trị cuối) | pass | `AccountFlowTests.L9_CannotDisableSelf_OrLastAdministrator` (không đổi kỳ vọng); unit `AccountUnitTests.AdminGuard_*` (5, viết lại trên `AdministratorInvariant` + `UserAccountService`) |

Test cũ đã sửa (ngoài phần dựng dữ liệu): `AccountUnitTests.AdminGuard_*` viết lại vì lớp `AdministratorGuard` bị xóa (cùng 5 tình huống; tình huống "bỏ qua bản gán không Global / tài khoản không hoạt động" nay nằm trong truy vấn repository, phủ bởi L9 tích hợp); `ImportFrameworkTests.Users_*` đổi tên đăng nhập mẫu 2 ký tự (`a1`, `u1`…) thành ≥ 3 ký tự vì bước xem trước nay áp quy tắc `AccountRules` — các khẳng định giữ nguyên; `AuthzContractTests` (mã cũ không còn là policy); xóa `DataSeederAuthorizationTests.LegacyUserRoles_MigratedToScopedAssignments_Once`.

## Thay đổi schema
Đã có trong migration `Wave4` (mục trên). Không còn thay đổi model chờ migration.

## Key config mới

| Key | Mặc định | Ý nghĩa |
|---|---|---|
| `Security:Password:MinLength` | `8` | Độ dài tối thiểu mật khẩu (task 08) — nay có trong `appsettings.json`, `.env.example`, compose |
| `Security:RateLimit:Auth:PermitLimit` | `10` | Số lần login/refresh mỗi cửa sổ/IP (task 08) |
| `Security:RateLimit:Auth:WindowSeconds` | `60` | Cửa sổ giới hạn (task 08) |
| `Seed:InitialAdmin:Username` / `FullName` / `Password` | rỗng | Tài khoản quản trị ban đầu khi chưa có quản trị (việc 7) |

## Thay đổi hành vi API / breaking change
- Login / refresh / change-password / `/api/auth/me`: thêm `grants`; `roles` luôn là **tên** vai trò từ bản gán đang hiệu lực (`/me` trước đây trả mã/quan hệ cũ).
- `/api/users/list`: quyền `system.users.read` (mã cũ `users.read` không còn), chỉ trả cán bộ thuộc Phòng/Chi bộ trong phạm vi (Toàn công ty → tất cả).
- `/api/users/profile?username=`: xem hồ sơ người khác cần `system.users.read` bao trùm Phòng/Chi bộ của người đó; thông báo 403 mới nêu tên quyền.
- Mã quyền cũ (`users.read`, `branches.*`, `roles.manage`…) **không còn là policy** — nếu còn controller nào khai báo mã cũ sẽ rơi vào provider mặc định (lỗi cấu hình khi khởi động request). Hiện không còn chỗ nào.
- 409 khi khóa/xóa quản trị viên cuối: thông báo chung của chốt "luôn còn quản trị" (cũng dùng cho thu hồi bản gán / gỡ quyền vai trò).
- `GET /api/admin/roles`, `/roles/{id}`, `/permissions`: người có `system.assignments.manage` đọc được.
- Nhập cán bộ: dòng có tên đăng nhập < 3 ký tự hoặc ký tự ngoài `[a-z0-9._-]`, email không hợp lệ theo `AccountRules` → lỗi ngay ở bước xem trước (trước: qua xem trước, lỗi 400 lúc ghi).
- Dữ liệu cũ sau migration: mọi `user_roles` thành bản gán **Global của chính vai trò cũ**; vai trò cũ chỉ có mã quyền cũ nên **không cấp quyền gì** (trừ `QUAN_TRI_HE_THONG` được seeder bảo vệ + bổ sung quyền quản trị). Khác với đoạn chuyển trong seeder của task 09 (ánh xạ `CAN_BO` → "Người được đánh giá", `BI_THU_CHI_BO` → "Chi ủy" phạm vi Chi bộ…) — theo chỉ đạo, đoạn đó đã bị xóa.

## Cần phối hợp
- **Người điều phối — sau nâng cấp CSDL thử nghiệm:** quản trị rà lại bản gán (cán bộ cũ `CAN_BO`, `BI_THU_CHI_BO`… cần gán lại vai trò mặc định mới, ví dụ qua `/api/admin/assignments` hoặc import vai trò của task 13).
- **`CLAUDE.md` (không sửa theo chỉ đạo):** mục "Quy ước" còn "policy theo claim `perm` (`AppPermissions`) + role (`AppRoles`)" — nay là `PermissionCodes` + `[RequirePermission]` + `IAuthorizationGuard`, bản gán có phạm vi; mục "Cấu trúc" thiếu `app/catalog`, `app/imports`, `Application/Imports`, `Infrastructure/Imports`.
- **`docs/deployment.md`:** bổ sung hướng dẫn `Seed__InitialAdmin__*` (tạo quản trị ban đầu, đổi mật khẩu ngay, xóa khỏi `.env`) và ghi chú cache quyền/trạng thái trong bộ nhớ một instance.
- **Task 11:** dùng `grants` (kèm `scopeName`) cho hiển thị phạm vi; `AppSidebar`/`AppHeader` hiển thị `roles[0]` nay là tên vai trò ở cả login lẫn `/me`.
- **Task 13:** import bản gán gọi `IRoleAssignmentService.AssignAsync` trong transaction — cache nay tự xóa sau commit; `AssignAsync` vẫn `SaveChanges` từng lần (đúng trong transaction, rollback được).
- `docker compose config` chưa kiểm được (máy không có Docker) — cần chạy trên máy có Docker.

## Phát hiện thêm
- 🟡 Đồng hồ máy chủ CSDL (159) nhanh hơn máy chạy app ~2 giây. Bản gán do migration tạo có `ValidFrom = now()` phía máy chủ → vài giây đầu sau nâng cấp resolver phía app coi là "chưa hiệu lực" (tự hết khi qua mốc — cache hết hạn đúng mốc). Triển khai Docker cùng máy không bị; nếu tách máy cần đồng bộ NTP.
- 🟡 `AccountStateCache` (trạng thái tài khoản, TTL 60 s) cũng được xóa trước commit khi thao tác nằm trong transaction — hiện chưa có luồng khóa/xóa tài khoản nào chạy trong transaction bao ngoài nên chưa ảnh hưởng; nếu thêm (ví dụ import khóa tài khoản) nên dùng `IAfterCommitActions` như cache quyền.
- ⚪ Nhập cán bộ: xem trước chưa kiểm tra phạm vi `system.users.manage` của người nhập trên Phòng/Chi bộ từng dòng — dòng ngoài phạm vi chỉ bị chặn (403, rollback cả lô) ở bước ghi. Có thể thêm vào `ValidateAsync` để báo lỗi dòng sớm.
- ⚪ `GenericRepository.UpdateAsync` dùng `DbSet.Update` đánh dấu mọi cột là đã sửa → audit "Update" ghi toàn bộ cột (không chỉ cột đổi). Ngoài phạm vi.
- ⚪ Máy chủ dùng chung đôi lúc không xóa được CSDL tạm ngay (một lần chạy toàn bộ test để sót `ctd_it_20260928162150_*`, đã xóa tay); `Wave4MigrationTests` nay thử lại khi xóa. Còn sót `ctd_it_20260928160412_96e5d357` (nhiều khả năng từ một lần chạy test lúc 16:04 của phiên này, không chắc chắn chủ sở hữu nên không xóa tay) — `ApiFactory` tự dọn CSDL `ctd_it_*` quá 24 giờ.
