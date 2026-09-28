# Báo cáo: Dọn sạch cuối (sau task 07–13)

- **Branch:** `chore/final-cleanup` (fast-forward tới `main` @ `b4c6948`, đã kiểm HEAD = `main` trước khi sửa)
- **Ngày:** 2026-09-28
- **Căn cứ:** chủ dự án quyết định không giữ dữ liệu, CSDL hay tương thích ngược của cách làm cũ (chưa có CSDL triển khai thật). Không thêm tính năng.

## 1. Gộp migration

| Việc | Kết quả |
|---|---|
| Xóa `InitialCreate` (cũ), `Wave3`, `Wave4`, snapshot | done — kèm toàn bộ SQL chuyển dữ liệu (`IsApprovedByAttech` → `ApprovalAuthority`, `user_roles` → `user_role_assignments`, hạ chữ thường username, khối `RAISE EXCEPTION`) |
| Sinh lại **một** `20260928170355_InitialCreate` từ model | done (`dotnet ef migrations add InitialCreate`) |
| Index unique `lower("Username")` | giữ, tạo bằng `migrationBuilder.Sql` trong `InitialCreate` (`IX_party_member_profiles_Username_lower`). Npgsql không khai báo được index biểu thức bằng `HasIndex` → nằm ngoài model, `has-pending-model-changes` sạch. Index `IX_party_member_profiles_Username` (unique, phân biệt hoa thường) vẫn trong model. |
| Thay đổi schema task 12 có trong model | có: `evaluation_periods.Settings/StatusReason/StatusChanged*` (không còn `IsActive`), cột B1–B5/09B của `evaluation_records`, `evaluation_record_histories.Step/Action/Reason/Score*/Grade*`, `evaluation_meetings.DepartmentId/Stage`, index `(PeriodId, Status)`, `(PeriodId, DepartmentId)` |
| Kiểm trên máy chủ 159 | tạo `ctd_it_mig_final` bằng `dotnet ef database update` (áp `InitialCreate`), `has-pending-model-changes`: *"No changes have been made to the model since the last migration."*, `migrations list` = 1 migration, đã `database drop --force`. |
| Test tích hợp dựng CSDL | `ApiFactory` và `DataSeederAuthorizationTests` chuyển từ `EnsureCreated` sang `MigrateAsync` → mọi test tích hợp chạy trên đúng migration triển khai (gồm index `lower(Username)`, có assert 23505 khi chèn `ADMIN` trùng `admin`). |
| Xóa test chỉ phục vụ migration cũ | `Wave4MigrationTests` (2 test) |
| `docs/database-migrations.md` | ghi migration đã gộp, index SQL, test dùng migration (không có mục Wave nào trước đó) |

## 2. Bỏ tương thích ngược & di sản

### Đã xóa (backend)

| Mục | Ghi chú |
|---|---|
| `GET /api/users/list`, `IUserService.GetCadresAsync`, `CadreDto` | trang chủ chuyển sang API mới |
| `GET /api/users/roles`, `IUserService.GetRolesAsync/GetUserByIdAsync/GetProfileAsync`, `RoleDto`, `PermissionDto` | không nơi nào dùng (vai trò: `/api/admin/roles`) |
| Bí danh `POST /api/users/create` | chỉ còn `POST /api/users` |
| `POST /api/admin/users/{id}/roles` (`SetGlobalRolesAsync`, `SetUserGlobalRolesRequestDto`) | "tương thích giao diện cũ"; gán qua `/api/admin/assignments` |
| `CreateUserDto`, `UpdateUserDto` | không dùng |
| `AdminTitle` (bí danh `PositionTitle`) và `IsActive` trong request tạo/sửa tài khoản | khóa/mở qua `activate`/`deactivate` |
| `EvaluationRecord.PartyCellProposedGrade`, `VotesExcellent/Good/Satisfactory/Unsatisfactory`, `TotalVoters` (+ DTO, mapping, dự phòng trong `ReportService`, `Mau13Data`, `EvaluationService.ProposedGrade`, `EffectiveGrade`) | kiểm phiếu chỉ còn ở `evaluation_meeting_vote_summaries` |
| `PartyCell.SecretaryId`, `DeputySecretaryId` | không nơi nào đọc/ghi |
| `Permission.Resource`, `Permission.Action` | chỉ seeder ghi, không ai đọc (trùng `Module`) |
| `TaskAttachment.FilePath`, `TaskAttachment.TaskId` | là bí danh nhưng EF map thành **cột trùng lặp** (`FilePath`, `TaskId`); index chuyển sang `RelatedId` |
| `AttachmentDto.Category` | frontend dùng `formCode` |
| `PermissionRequirement.Permission` ("tương thích task 07") | không dùng |
| Giá trị số enum phục vụ chuyển dữ liệu | `PeriodStatus` 0/1/2/3 (trước 0/10/11/12), `RecordStatus` 1…10 (trước 10…19), `CollectiveRecordStatus.Submitted = 1` (trước 3). API trả tên enum, không đổi hợp đồng. |
| `HasDefaultValue` chỉ để điền dữ liệu cũ khi thêm cột | `roles.IsProtected`, `permissions.Module/SortOrder` |
| `DataSeeder`: nhận diện vai trò cũ (`CAN_BO`, `BI_THU_CHI_BO`, `TO_THAM_DINH`, `BAN_THUONG_VU`, `DANG_UY_CO_SO`), đánh dấu bảo vệ vai trò quản trị cho CSDL cũ, dọn hồ sơ của `admin`, đồng bộ `Resource/Action` | vai trò mặc định chỉ tạo khi CSDL chưa có vai trò nào |
| Chú thích nhắc cơ chế cũ | `IsApprovedByAttech`, "claim perm", "tương thích giao diện cũ", "dữ liệu trước task 12", "baseline legacy schema", "Đợt 4"… trong code/test đã sửa |

### Đã xóa / đổi (frontend)

| Mục | Ghi chú |
|---|---|
| `app/page.tsx` | viết lại: kỳ (chọn), **Việc cần xử lý** (`GET /api/evaluations/work-queue?periodId`), **Hồ sơ của tôi** (`my-record` + `progress` 9 bước do máy chủ tính, `returnReason`), **Tiến độ trong phạm vi** (`GET /api/evaluations/records` khi có `evaluation.read`, đếm theo `status`/`statusDisplayName`), số liệu quản trị (`GET /api/users?pageSize=1` → `totalCount`, danh mục). Không còn tự suy tiến trình từ trường cũ, không còn link `/evaluations?step=`. |
| `userService.getUsers()`, `CadreItem` | xóa |
| `app/users` (trang chuyển hướng cũ), tiêu đề `/users` trong `AppHeader` | xóa; thêm tiêu đề cho các trang hiện có |
| `AuthContext` | `grants` bắt buộc (backend luôn trả — `AccessGrantDto.ScopeType` là **chuỗi** `Global/Department/PartyCell`, kiểm ở `/api/auth/me`, login, refresh); bỏ nhánh "máy chủ chưa trả grants", bỏ đọc `scopeType` dạng số, bỏ lọc nhãn vai trò dạng mã cũ |
| `authService.UserSession.grants` | bắt buộc |
| `EvaluationRecordDto` trường kiểm phiếu cũ, `auditService.EvaluationRecordHistoryDto`/`getRecordHistory` (trùng `evaluationService`), props thừa `allRecords/branchQuotas` của `EvaluationPdfModal`, `AttachmentItem.category` | xóa |

### Test đã sửa vì hành vi bị xóa

- `AccountFlowTests.L1`: route bí danh `/create` → kiểm `POST /api/users` thiếu tên đăng nhập → 400.
- `CrossModuleIntegrationTests` (đổi tên từ `Wave4IntegrationTests`): bỏ phần `/api/users/list`, giữ kiểm `GET /api/users` theo phạm vi (thêm assert người xem không nằm trong danh sách).
- `AuthzContractIntegrationTests.PermissionChange_…`: gán/kết thúc qua `/api/admin/assignments` thay endpoint tương thích.
- `AuthorizationMatrixTests`, `RoleAssignmentApiTests`: tìm vai trò quản trị bằng `ApiFactory.GetAdministratorRoleIdAsync()` (vai trò `IsProtected`) thay mã vai trò.
- `AuthzContractTests`, `AuthorizationGuardTests`, `RoleAssignmentApiTests`: ví dụ "mã không có trong danh mục" đổi từ mã cũ `users.read` sang `ma.khong.ton.tai`/`catalog.read`.
- `DocumentTemplateTests.SampleData`: dùng `CollectiveProposedGrade` thay trường Chi bộ cũ.
- `EvaluationWorkflowUnitTests`: oracle trạng thái theo giá trị enum mới.
- `Wave4InitialAdminTests` → `InitialAdminSeedTests` (chỉ đổi tên).

### Giữ lại (và lý do)

| Mục | Lý do |
|---|---|
| `AppRole.Code` + hằng số nội bộ `DataSeeder.RoleCodes` (gồm `QUAN_TRI_HE_THONG`) | **Ngoại lệ duy nhất của grep.** Seeder cần mã ổn định để nhận ra vai trò mặc định khi `Database:ResetRolePermissions` (tên vai trò quản trị sửa được), khi gán dữ liệu mẫu và khi chọn vai trò quản trị ban đầu. Không dùng để phân quyền. Vai trò tạo qua API có mã sinh tự động `ROLE_<guid>`. |
| Mã quyền `system.users.read`, `system.roles.manage` | là mã **mới** theo `docs/thiet-ke/phan-quyen.md`; khớp mẫu grep `users\.(read…)`/`roles\.manage` vì chứa chuỗi con (xem mục 5). |
| `TaskAttachment`: `RecordId`, `RelatedId`, suy đối tượng khi `OwnerType` null, liên kết minh chứng qua `EvaluationTask.AttachmentId` | Nằm trong đường kiểm quyền tệp (`AttachmentRecordLink`, `EffectiveOwner*`); `RecordId` vẫn được ghi và dùng để xác định hồ sơ của tệp. Gỡ phần dự phòng `OwnerType = null` cần sửa lại mô hình sở hữu tệp + test quyền — không chắc an toàn trong phạm vi dọn dẹp. |
| `IEvaluationRepository` song song `IEvaluationWorkflowRepository` | cả hai đang được dùng (`ReportService`, `CollectiveEvaluationService`); gộp là tái cấu trúc, không phải xóa code chết. |
| `UserProfileDto` (`AdminTitle`, `PartyBranchName`, `AdminDeptName`) của `GET /api/users/profile` | endpoint còn dùng (test tài khoản, tra hồ sơ theo phạm vi); đổi tên trường là đổi hợp đồng API, không phải di sản. |
| `PeriodSettings.Parse("")` → mẫu đầy đủ | phòng thủ khi cột rỗng; bỏ chú thích "kỳ trước task 12". |
| Chú thích dẫn nguồn "task NN"/mã lỗi | truy vết thiết kế, không mô tả cơ chế cũ. |
| `HasDefaultValue` của `MustChangePassword`, `FailedLoginCount`, `VersionNumber` | giá trị mặc định hợp lệ của mô hình hiện tại. |

## 3. Dữ liệu mẫu (`Database:SeedSampleData=true`)

- Chỉ tạo khi CSDL **chưa có** tài khoản, Phòng, Chi bộ, kỳ nào (kể cả đã xóa mềm) → không bao giờ tạo lại/gán lại.
- 4 Phòng (`BGD`, `PH-KT`, `PH-KH`, `PH-TCCB`), 2 Chi bộ (`CB-KT`, `CB-VP`), 9 tài khoản (`admin`, `giamdoc`, `vanphong`, `thamdinh`, `truongphong.kt`, `bithu.kt`, `thuky.kt`, `canbo.kt1`, `canbo.kt2`), tất cả `MustChangePassword = true`, bản gán vai trò mặc định có phạm vi (Toàn công ty / Phòng Kỹ thuật / Chi bộ Khối Kỹ thuật) — bảng chi tiết trong `docs/deployment.md`.
- Kỳ "Đánh giá, xếp loại cán bộ Quý III/2026", mẫu "Quý III/2026 — chuyển tiếp" (09B), **Đang mở**; 6 hồ sơ ở 6 trạng thái khác nhau (Chờ tự chấm, Chờ Chi bộ xác nhận, Chờ ghi nhận đề xuất tập thể, Chờ thẩm định, Chờ cấp trực tiếp sử dụng, Đã công bố — cấp trên). Hồ sơ dựng bằng `RecordStateMachine` theo cấu hình kỳ: ảnh chụp Phòng/Chi bộ/khung/cấp quyết định, dữ liệu hợp lệ của từng bước đã qua, lịch sử `Create` + `Complete` từng bước.
- **Mật khẩu mẫu (đã chọn cả hai cách, ưu tiên cấu hình):**
  - `Seed:SamplePassword` (biến `Seed__SamplePassword`) có giá trị → dùng làm mật khẩu tạm chung; phải đạt chính sách mật khẩu, sai → **không tạo dữ liệu mẫu**, log Error. Không ghi mật khẩu ra log.
  - Rỗng (mặc định) → sinh ngẫu nhiên 12 ký tự (`UserAccountService.GenerateTemporaryPassword`), ghi **một lần** vào log mức **Warning** ngay khi tạo; khởi động lại không ghi lại.
  - Không còn `123456`.
- Tài khoản quản trị ban đầu (`Seed:InitialAdmin:*`) giữ nguyên hành vi; có dữ liệu mẫu thì `admin` mẫu đã là quản trị nên bỏ qua.

## 4. Test

| Luồng | Trạng thái | Test |
|---|---|---|
| Migration gộp + index `lower(Username)` chặn trùng hoa thường | pass | `DataSeederAuthorizationTests.SampleData_SeededOnce_NeverReassigned_RolePermissionsNotOverwritten` |
| Dữ liệu mẫu: 9 vai trò, admin chỉ có quyền kỹ thuật, bản gán có phạm vi, mật khẩu cấu hình + bắt buộc đổi, kỳ Open 09B, 6 hồ sơ 6 trạng thái; seed lần 2 không tạo lại/không gán lại/không ghi đè quyền | pass | như trên |
| `Seed:SamplePassword` sai chính sách (`123456`) → không tạo dữ liệu mẫu | pass | `DataSeederAuthorizationTests.SampleData_InvalidConfiguredPassword_CreatesNoSampleData` |
| CSDL trống + `SeedSampleData=true` → khởi động → `bithu.kt` đăng nhập, bị buộc đổi mật khẩu, đổi → `work-queue` đúng 1 nhóm `B2_CELL_CONFIRM` (hồ sơ `canbo.kt2`, có hành động `ConfirmByCell`); `canbo.kt1` thấy hồ sơ của mình ở `B2_SELF_SCORE`; `thamdinh` thấy `B3B_APPRAISAL`; `admin` 0 việc; kỳ hiện hành Open/09B | pass | `SampleDataSeedTests.EmptyDatabase_SeedSampleData_LoginShowsWorkQueueByRoleAndScope` |
| Không cấu hình mật khẩu → mật khẩu ngẫu nhiên đúng 1 Warning, đăng nhập được bằng mật khẩu đó, `123456` bị 401; khởi động lại không log lại | pass | `SampleDataSeedTests.EmptyDatabase_SeedSampleData_WithoutConfiguredPassword_GeneratesRandomPasswordLoggedOnce` |
| Go-live: quản trị ban đầu qua **cấu hình host** `Seed:InitialAdmin` (không ghi thẳng CSDL) → import Phòng/Chi bộ/cán bộ/gán vai trò → quyền + `grants` theo tệp → gỡ bản gán → 403 → **Cơ quan thẩm định tạo kỳ, tắt B3a/B3c khi dự thảo, thêm người được đánh giá, mở kỳ**; quản trị 403 trên hồ sơ → quản trị gán "Văn phòng Đảng ủy" qua API → hồ sơ tự chấm 09B → Chi bộ xác nhận → thẩm định → quyết định → **Published**; lịch sử đủ 5 bước | pass | `GoLiveScenarioTests.GoLive_FromEmptyDatabase_ImportCatalogCadresAssignments_ThenPermissionsFollowAssignments` |
| `/api/auth/me.grants` (hết skip) | pass | `RoleAssignmentImportIntegrationTests.GoLive_Step3_AuthMe_ReturnsGrantsWithScope` |

## 5. Cổng kiểm tra

| Bước | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build backend/CongTacDang.slnx --no-incremental` | pass | **0 Warning(s), 0 Error(s)** |
| `dotnet test` — unit | pass | 194: **193 pass, 1 skip** (`PdfConversionTests.Mau01…` — máy không có LibreOffice, skip sẵn có), 0 fail |
| `dotnet test` — integration (`CONGTACDANG_TEST_PG`, máy 159) | pass | **72 chạy, 72 pass, 0 skip**, 0 fail |
| `dotnet ef migrations has-pending-model-changes` | pass | sạch (trên `ctd_it_mig_final`, đã xóa) |
| `npx tsc --noEmit` | pass | |
| `npm run build` | pass | `next lint` các file sửa: không cảnh báo |
| `docker compose -f docker/docker-compose.yml config` | **không chạy** | máy không có Docker |
| CSDL | chỉ `ctd_it_*` | không đụng `congtacdang_test`; `ctd_it_mig_final` đã xóa |

### Grep

Lệnh theo đề bài (ripgrep, `backend/src`, `backend/tests`, `frontend/{app,components,contexts,services}`) còn khớp **chỉ** hai loại:
1. Mã quyền **mới** `system.users.read`, `system.roles.manage` (PermissionCodes, AppSidebar, chú thích…) — mẫu `users\.(read…)`, `roles\.manage` khớp chuỗi con của mã mới; không đổi mã quyền vì là danh mục đã chốt trong thiết kế.
2. `backend/src/CongTacDang.Infrastructure/Data/DataSeeder.cs:71: public const string Administrator = "QUAN_TRI_HE_THONG";` — ngoại lệ cho phép (mục 2 "Giữ lại").

Grep loại trừ tiền tố `system.` (mã cũ thật sự) — kết quả:
```
$ grep -rnPi --include=*.cs --include=*.ts --include=*.tsx --exclude-dir=node_modules --exclude-dir=bin --exclude-dir=obj \
  "AppRoles|AppPermissions|IAccessPolicy|Legacy(RoleCodes|PermissionMap|Policies)|IsInRole|HasRole\(|hasRole|IsApprovedByAttech|branch_vote|evaluations\.(register|self_score|appraise|approve|read)|(?<!system\.)users\.(read|create|update|delete)|(?<!system\.)roles\.manage|reports\.export|QUAN_TRI_HE_THONG|BAN_THUONG_VU|BI_THU_CHI_BO|TO_THAM_DINH|DANG_UY_CO_SO|CAN_BO|minio" \
  backend/src backend/tests frontend/app frontend/components frontend/contexts frontend/services
backend/src/CongTacDang.Infrastructure/Data/DataSeeder.cs:71:        public const string Administrator = "QUAN_TRI_HE_THONG";
```
`[Obsolete]`: 0 chỗ.

## 6. Tài liệu

- `docs/deployment.md`: thêm mục **Biến cấu hình tài khoản, bảo mật và dữ liệu khởi tạo** (`Seed__InitialAdmin__*`, `Seed__SamplePassword`, `Security__*`, `Database__*`; cache quyền một instance), **Dữ liệu mẫu**, **Go-live (CSDL thật)** theo checklist báo cáo 13 (quản trị ban đầu qua cấu hình, thứ tự import, gán vai trò, giao tài khoản, kiểm tra, **mở kỳ**); sửa câu cũ về health check CSDL.
- `docs/database-migrations.md`: migration gộp, index SQL, test dùng migration.
- `docs/thiet-ke/phan-quyen.md`: bỏ bảng ánh xạ mã cũ → mới và chú thích `IsApprovedByAttech`; cập nhật quy tắc dữ liệu mẫu.
- `appsettings.json`: thêm `Seed:SamplePassword` rỗng.

## Cần phối hợp

- **`CLAUDE.md`** (không sửa theo chỉ đạo): mục "Quy ước" còn "policy theo claim `perm` (`AppPermissions`) + role (`AppRoles`)" — nay là `PermissionCodes` + `[RequirePermission]` + `IAuthorizationGuard`, bản gán có phạm vi; mục "Cấu trúc" còn `components/evaluations/Step1..Step5`, thiếu `app/work-queue`, `app/periods`, `app/catalog`, `app/imports`, `app/admin`, `Domain/Evaluation`, `Application/Imports`.
- **`docker-compose.yml`** không truyền `Database__AutoMigrate`, `Database__SeedSampleData`, `Seed__SamplePassword` vào container (chủ ý cho production). Go-live dùng `dotnet ef database update` trước khi khởi động (đã ghi trong deployment). Nếu muốn tự migrate trong Compose cần thêm biến — ngoài phạm vi (không thêm tính năng).
- `docker compose config` cần chạy trên máy có Docker.

## Phát hiện thêm

- ⚪ Mô hình sở hữu tệp (`TaskAttachment`) còn ba đường xác định đối tượng (`OwnerType/OwnerId`, `RecordId/RelatedId`, `EvaluationTask.AttachmentId`). Với CSDL mới, `OwnerType` luôn có giá trị → có thể đơn giản hóa về một đường, kèm rà lại test quyền tệp.
- ⚪ `IEvaluationRepository` và `IEvaluationWorkflowRepository` trùng một phần chức năng — có thể gộp.
- ⚪ Trang `/evaluations` chưa đọc `?periodId=` (trang tổng quan không còn truyền tham số này).
