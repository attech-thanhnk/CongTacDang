# Báo cáo: Task 09 — Phân quyền động có phạm vi

- **Branch:** `feat/dynamic-authz`
- **Commit cuối:** commit chứa báo cáo này (commit code cuối: `450a077`)
- **Ngày:** 2026-09-28

> Ghi chú môi trường: worktree tạo ở commit cũ; đã `git merge --ff-only 8a64c85` (đầu `main`) và đổi tên branch trước khi sửa file đầu tiên.

## Kết quả theo mã lỗi

| Mã | Trạng thái | Tóm tắt thay đổi | File chính |
|---|---|---|---|
| T-55 | partial | Bảng `user_role_assignments` (gán vai trò kèm phạm vi Global/Phòng/Chi bộ + thời hạn); `PermissionResolver` thật (bản gán đang hiệu lực, bỏ mã lạ + log 1 lần, quyền "không áp dụng phạm vi" chỉ tính khi gán Global, cache hết hạn đúng mốc ValidFrom/ValidTo); `AuthorizationGuard` thật (phạm vi, chủ hồ sơ, ApprovalAuthority, xung đột lợi ích); xóa `AccessPolicy`/`LegacyAuthorizationGuard`/policy composite; `[RequirePermission]` + `[RequireAnyPermission]`; API quản trị vai trò/danh mục quyền/gán vai trò/tra cứu quyền hiệu lực kèm 7 chốt chặn; `IRoleAssignmentService` (task 13); frontend bỏ `hasRole` và ngoại lệ theo tên vai trò. **Còn lại** (ngoài phạm vi file, xem "Cần phối hợp"): `AppPermissions`/`AppRoles` còn dùng ở `UserController`, `OrganizationController`, `AuditController`, `UserService` → giữ, đánh dấu `[Obsolete]`; quan hệ user↔role cũ (`user_roles`, `PartyMemberProfile.Roles`) còn được `UserService`/`UserRepository`/`RefreshTokenRepository` đọc → giữ ánh xạ, không còn dùng để phân quyền; trường `grants` của `/api/auth/me`/login nằm trong `AuthController`/`AuthService` (task 08) → đã cung cấp `IRoleAssignmentService.GetGrantsAsync`. | `Application/Common/Security/*`, `Api/Authorization/PermissionAuthorization.cs`, `Api/Extensions/AuthorizationExtensions.cs`, `Application/Services/Role*Service.cs`, `Api/Controllers/Admin*Controller.cs`, `Domain/Entities/UserRoleAssignment.cs`, `Infrastructure/Data/Configurations/*`, `Infrastructure/Repositories/Role*Repository.cs` |
| T-48 | done | `SubmitAppraisalAsync` gọi `Ensure(evaluation.appraise, hồ sơ)` — phạm vi + không thẩm định hồ sơ của mình. Mọi bước ghi khác cũng kiểm tra trên **từng** hồ sơ (`branch-review`, từng hồ sơ trong `branch-meeting-review`, `approve-final` theo `ApprovalAuthority`). | `EvaluationService.cs` |
| T-45 | done | Biên bản lọc theo phạm vi `meeting.read` ∪ `meeting.manage`; không có phạm vi phù hợp → danh sách rỗng; lọc Chi bộ khác qua tham số không lộ biên bản. | `CollectiveEvaluationService.cs` |
| T-46 | done | Seeder: tạo vai trò mặc định mục 6 chỉ khi CSDL chưa có vai trò nào ngoài vai trò cũ; không ghi đè quyền vai trò đã có; gán mẫu **chỉ** cho tài khoản mẫu chưa từng có bản gán (kể cả đã xóa/hết hạn); chuyển cặp user–role cũ sang bản gán (chỉ khi bảng gán trống). | `DataSeeder.cs` |
| T-61 | done (phần `/reports`) | `/api/reports/cadres` chỉ xuất cán bộ trong phạm vi `report.export` (Toàn công ty/Phòng/Chi bộ); báo cáo theo Chi bộ kiểm tra `branchId` trong phạm vi. Phần `/api/users/list` là của task 08 (xem "Cần phối hợp"). | `ReportAccessService.cs`, `ExportReportController.cs`, `ReportService.ExportCadresReportAsync` |
| T-42 (phần phân quyền) | done | Unit `AuthorizationGuardTests` (36 test case), tích hợp `AuthorizationMatrixTests` (12), `RoleAssignmentApiTests` (4), `DataSeederAuthorizationTests` (2). | `backend/tests/**` |

**Vì sao T-55 `partial`:** RULES 8.2 + chỉ đạo điều phối — không sửa file của task 08/10. Mọi phần còn lại đã có sẵn điểm nối (interface/adapter) và danh sách dòng cụ thể ở mục "Cần phối hợp".

## Cổng kiểm tra

| Bước | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build backend/CongTacDang.slnx` | pass | 0 lỗi; **24 warning CS0618** (đều do `[Obsolete]` có chủ đích, chỉ trong file ngoài phạm vi: `UserController`, `OrganizationController`, `AuditController`, `UserService`, `AuthService`, `SecurityTests`) |
| `dotnet test` — unit | pass | 102 test: **101 pass, 1 skip** (`PdfConversionTests.Mau01…` — thiếu LibreOffice, skip sẵn có), 0 fail |
| `dotnet test` — integration, có `CONGTACDANG_TEST_PG` (máy chủ 159) | pass | 31 test: **31 chạy, 31 pass, 0 skip**, 0 fail |
| `dotnet test` — integration, không có biến | pass (skip) | 8 pass (tên CSDL), **23 skip** — skip không tính là pass |
| `npx tsc --noEmit` | pass | |
| `npm run build` | pass | |
| grep tiêu chí hoàn thành | còn sót **đúng các file ngoài phạm vi** | xem mục "Kết quả grep" |
| CSDL tạm | chỉ `ctd_it_*` | `ApiFactory` xóa CSDL khi xong; `DataSeederAuthorizationTests` tạo/xóa CSDL `ctd_it_*` riêng trong `finally`. Không có `psql` trên máy nên chưa đối chiếu `pg_database` sau chạy. |

### Luồng/test theo task file

| Luồng | Trạng thái | Test |
|---|---|---|
| Resolver: chỉ bản gán đang hiệu lực (ValidTo không bao gồm), bỏ bản gán/vai trò đã xóa, bản gán tương lai | pass | `AuthorizationGuardTests.Resolver_UsesOnlyEffectiveAssignments_ValidToExclusive` |
| Resolver: bỏ mã không có trong `PermissionCodes`, quyền "không áp dụng phạm vi" gán theo Phòng bị bỏ | pass | `Resolver_SkipsUnknownCodes_AndGlobalOnlyCodesOnScopedAssignments` |
| Tài khoản vô hiệu / không tồn tại → rỗng, kể cả quyền chủ hồ sơ | pass | `Resolver_InactiveOrMissingUser_IsEmptyAndInactive` |
| Cache: hết hạn đúng mốc bản gán, xóa theo người/toàn bộ, TTL 5 phút | pass | `Resolver_Cache_ExpiresAtAssignmentBoundary_AndOnInvalidation` |
| Ma trận phạm vi (Global/Phòng/Chi bộ × mã có phạm vi × đối tượng) | pass | `Guard_ScopeMatrix_ForScopedCodes` (8 trường hợp × 15 mã), `Guard_GlobalOnlyCodes_IgnoreScopedGrants` |
| Chủ hồ sơ: `evaluation.self` chỉ trên hồ sơ mình; `evaluation.read` luôn đúng với chủ | pass | `Guard_EvaluationSelf_OnlyOwner_ScopeIgnored`, `Guard_EvaluationRead_OwnerAlways_OthersByScope` |
| `ApprovalAuthority` × decide/decide.external | pass | `Guard_Decide_FollowsApprovalAuthority` (2 trường hợp) |
| Xung đột lợi ích (9 mã × CoSo/CapTren) | pass | `Guard_ConflictOfInterest_NoApprovalOnOwnRecord` |
| Thông báo 403 nêu tên quyền, không nêu mã; ẩn danh | pass | `Guard_Instance_UsesCurrentUser_EnsureMessageUsesDisplayName`, `Guard_Anonymous_HasNothing` |
| Chốt: tự gán; cần `system.assignments.manage` Global; vai trò có quyền chỉ-Global gán theo Phòng; phạm vi sai; thời hạn sai; trùng | pass | `Assign_ToSelf_IsForbidden`, `Assign_RequiresGlobalAssignmentsManage`, `Assign_GlobalOnlyRole_WithScope_IsRejected`, `Assign_InvalidScope_IsRejected` (3), `Assign_Validates_DatesAndDuplicates_AndInvalidatesCache` |
| Chốt: luôn còn quản trị (kết thúc/xóa bản gán, khóa/xóa người, gỡ quyền vai trò) | pass | `End_And_Delete_LastAdministrator_AreRejected`, `UpdateRolePermissions_RemovingLastAdminCodes_IsConflict`, `AdministratorInvariant_RequiresBothAdminCodes` |
| Chốt: vai trò bảo vệ, không sửa quyền vai trò mình đang được gán, mã lạ, thêm quyền chỉ-Global cho vai trò đang gán theo Phòng, xóa vai trò đang gán | pass | `UpdateRolePermissions_Guards`, `DeleteRole_ProtectedOrAssigned_IsConflict_OtherwiseSoftDeleted`, `CreateRole_DuplicateName_IsConflict_AndCodeIsGenerated` |
| Tra cứu "người X làm được gì" | pass | `EffectivePermissions_ListsScopeNamesAndSources`, `PermissionCatalog_GroupsByModule_WithAppliesScope` |
| **Tích hợp** — kỳ đánh giá (đọc: mọi người; ghi: `period.manage`) | pass | `AuthorizationMatrixTests.Periods_ReadableByEveryLoggedInUser_ManageNeedsPeriodManage` |
| Tích hợp — xem hồ sơ/lịch sử theo phạm vi, chủ hồ sơ, quản trị không xem | pass | `RecordById_And_History_FollowScope_OwnerAlways_AdminNever` (10 cặp) |
| Tích hợp — danh sách hồ sơ **chỉ chứa hồ sơ trong phạm vi** | pass | `RecordLists_ReturnOnlyRecordsInScope` |
| Tích hợp — bước cá nhân (đăng ký, tự chấm) | pass | `SelfSteps_OnlyOwnerWithEvaluationSelf` |
| Tích hợp — Chi bộ xác nhận từng hồ sơ + xung đột lợi ích + biên bản kiểm phiếu rollback | pass | `CellConfirm_PerRecordScope_AndConflictOfInterest` |
| Tích hợp — **T-48** thẩm định từng hồ sơ | pass | `T48_Appraisal_ChecksEachRecord` |
| Tích hợp — quyết định theo `ApprovalAuthority` | pass | `ApproveFinal_FollowsApprovalAuthority` |
| Tích hợp — hồ sơ tập thể theo Chi bộ/Phòng | pass | `CollectiveRecords_FilteredByCellOrDepartmentScope` |
| Tích hợp — **T-45** biên bản | pass | `T45_Meetings_OnlyWithinMeetingScope` |
| Tích hợp — tệp theo quyền hồ sơ, văn bản chung | pass | `Attachments_FollowRecordPermissions_GeneralDocsPublic` |
| Tích hợp — **T-61** danh sách cán bộ lọc theo phạm vi (đọc file Excel) | pass | `T61_CadreExport_FilteredByReportScope` |
| Tích hợp — báo cáo/biểu mẫu theo Chi bộ, biểu mẫu hồ sơ cá nhân | pass | `BranchReports_ResolveScope` |
| Tích hợp — vòng đời vai trò & bản gán qua API (409 kèm số bản gán…) | pass | `RoleAssignmentApiTests.RoleAndAssignmentLifecycle_ThroughApi` |
| Tích hợp chốt chặn — tự gán, xóa vai trò bảo vệ, tự sửa quyền vai trò mình | pass | `Guardrail_CannotAssignSelf_NorDeleteProtectedRole` |
| Tích hợp chốt chặn — thu hồi quản trị cuối | pass | `Guardrail_CannotRevokeLastAdministrator` (tạm kết thúc bản gán quản trị khác trong CSDL test rồi khôi phục ở `finally`) |
| "Thay đổi có hiệu lực ngay": gỡ quyền → request kế tiếp 403 (cùng access token) | pass | `RevokingPermission_TakesEffectOnNextRequest`; `AuthzContractIntegrationTests.PermissionChange_TakesEffectOnNextRequest_WithoutRelogin` |
| Seeder T-46: gán mẫu 1 lần, không gán lại bản gán đã thu hồi, không ghi đè quyền đã sửa | pass | `DataSeederAuthorizationTests.SampleData_SeededOnce_NeverReassigned_RolePermissionsNotOverwritten` |
| Seeder: chuyển `user_roles` cũ → bản gán có phạm vi, chạy lại không nhân đôi | pass | `DataSeederAuthorizationTests.LegacyUserRoles_MigratedToScopedAssignments_Once` |

**Test cũ đã sửa** (task file yêu cầu xóa `AccessPolicy`/`LegacyAuthorizationGuard`/mã cũ):
- Xóa `AccessPolicyTests.cs`, `AttachmentAccessPolicyTests.cs` (kiểm lớp đã xóa); luật tương đương chuyển sang `AuthorizationGuardTests` và `AuthorizationMatrixTests.Attachments_*`.
- `AuthzContractTests.cs` (task 07, do tôi viết): bỏ test so khớp policy cũ theo claim/vai trò, guard v0, resolver v0, `RoleService` v0; giữ danh mục mã, cache, policy theo mã, `UserAccountService`; thêm test `any:` và policy chuyển tiếp mã cũ.
- `AttachmentVersioningTests.cs` (task 05): chỉ sửa **phần dựng dữ liệu** (fixture dùng `IPermissionResolver` giả với bản gán: cán bộ = `evaluation.self`, Bí thư = `evaluation.read` phạm vi Chi bộ) — kỳ vọng giữ nguyên, toàn bộ pass.
- `AuthzContractIntegrationTests.cs` (task 07): đổi mã cũ → mã mới; endpoint tương thích nhận `roleIds` thay `roleCodes`.
- `SecurityTests.cs` **không sửa**: `PermissionResolver` giữ constructor cũ `(IUserRepository, PermissionCache)` (đánh dấu `[Obsolete]`, không tính bản gán) để code/test task 08 viết song song vẫn biên dịch.

## Bảng endpoint → quyền → phạm vi (sau task 09)

"Đăng nhập" = `[Authorize]`, service kiểm tra trên đối tượng. Policy `A | B` = `[RequireAnyPermission(A, B)]`.

| Endpoint | Policy controller | Kiểm tra trong service / phạm vi dữ liệu |
|---|---|---|
| `GET /api/evaluations/periods`, `/periods/active` | Đăng nhập | — |
| `POST /api/evaluations/periods`, `PUT /periods/{id}/activate`, `PUT /periods/{id}/status` | `period.manage` | `Ensure(period.manage)` — chỉ bản gán Global |
| `GET /api/evaluations/my-record` | Đăng nhập | hồ sơ của chính mình |
| `GET /api/evaluations/records/{id}`, `/records/{id}/history` | Đăng nhập | `Ensure(evaluation.read, hồ sơ)`: chủ hồ sơ luôn; khác theo Phòng/Chi bộ ảnh chụp trên hồ sơ |
| `GET /api/evaluations/records?periodId` | `evaluation.read` | lọc `GetScope(evaluation.read)` (+ hồ sơ của mình) |
| `GET /api/evaluations/branch-records?periodId&branchId` | `evaluation.read` | như trên; `branchId` ngoài phạm vi → chỉ còn hồ sơ của mình (không lộ) |
| `GET /api/evaluations/branch-quotas` | `evaluation.read` | số liệu tính trên hồ sơ trong phạm vi |
| `POST /api/evaluations/tasks/register`, `/self-score` | `evaluation.self` | chỉ hồ sơ của mình |
| `POST /api/evaluations/branch-review` | `evaluation.cell.confirm` | trên hồ sơ; không phải hồ sơ của mình |
| `POST /api/evaluations/branch-meeting-review` | `evaluation.cell.confirm` | trên **từng** hồ sơ trong biên bản (lỗi 1 hồ sơ → rollback toàn bộ) |
| `POST /api/evaluations/appraisal` | `evaluation.appraise` | trên hồ sơ; không phải hồ sơ của mình (T-48) |
| `POST /api/evaluations/approve-final` | `evaluation.decide` \| `evaluation.decide.external` | `decide` nếu hồ sơ `CoSo`, `decide.external` nếu `CapTren`; phạm vi; không phải hồ sơ của mình |
| `GET /api/evaluations/collective-records`, `/{id}` | `evaluation.read` \| `collective.manage` | Phòng/Chi bộ của hồ sơ tập thể (không áp luật chủ hồ sơ) |
| `POST /api/evaluations/collective-records` | `collective.manage` | Phòng/Chi bộ trong request |
| `GET /api/evaluations/meetings`, `/{id}` | `meeting.read` \| `meeting.manage` | Chi bộ của biên bản; không có phạm vi phù hợp → rỗng (T-45) |
| `POST /api/evaluations/meetings` | `meeting.manage` | Chi bộ trong request |
| `GET /api/attachments/list`, `GET /api/attachments?ownerType&ownerId`, `GET /{id}`, `/{id}/versions`, tải về | Đăng nhập | tệp gắn hồ sơ: `evaluation.read` trên hồ sơ; văn bản `GENERAL` do người có `attachment.general.manage` tải: mọi người; tệp chưa gắn hồ sơ: chỉ người tải |
| `POST /api/attachments/upload` | Đăng nhập | gắn hồ sơ: `evaluation.self` (chủ hồ sơ); không gắn: `evaluation.self` hoặc `attachment.general.manage` |
| `POST /api/attachments/{id}/versions`, `DELETE /api/attachments/{id}` | Đăng nhập | gắn hồ sơ: `evaluation.self` trên hồ sơ; văn bản chung công khai: `attachment.general.manage`; chưa gắn: người tải |
| `GET /api/reports/cadres` | `report.export` | chỉ cán bộ trong phạm vi `report.export` (T-61) |
| `GET /api/reports/form-14`, `form-15`, `form-15a`, `form-15b`, `form-16` | `report.export` | `branchId` phải trong phạm vi; không truyền: Global → toàn Đảng bộ, đúng 1 Chi bộ → Chi bộ đó, nhiều Chi bộ → 400, chỉ có phạm vi Phòng → 403 |
| `GET /api/reports/docx/mau-01|02|10/{recordId}` | Đăng nhập | `Ensure(evaluation.read, hồ sơ)` |
| `GET /api/reports/docx/mau-11`, `mau-13` | `meeting.read` \| `report.export` | như form-14 trên hợp phạm vi 2 quyền |
| `/api/admin/roles*`, `GET /api/admin/permissions` | `system.roles.manage` | service `Ensure(system.roles.manage)` Global |
| `/api/admin/assignments*`, `POST /api/admin/users/{id}/roles` | `system.assignments.manage` | service `Ensure(system.assignments.manage)` Global + chốt chặn |
| `GET /api/admin/users/{id}/effective-permissions` | `system.assignments.manage` \| `system.users.read` | `assignments.manage` Global, hoặc `users.read` trong phạm vi Phòng/Chi bộ của tài khoản |
| `/api/users/*` (task 08 — **chưa đổi attribute**) | mã cũ, đánh giá qua ánh xạ: `users.read` → `system.users.read`; `users.create/update/delete` → `system.users.manage` | (task 08) |
| `/api/organizations/*` (task 10 — **chưa đổi**) | `branches.read` → chỉ cần đăng nhập; `branches.create/update/delete` → `catalog.manage` | (task 10) |
| `GET /api/audit/*` (task 08 — **chưa đổi**) | `roles.manage` → một trong `system.roles.manage`/`system.assignments.manage`/`system.audit.read` | nên đổi sang `system.audit.read` |

## API quản trị cho task 11

Mọi phản hồi bọc `ApiResponse<T>` (`{ success, message, data, errors, timestamp }`), JSON camelCase. Thời gian ISO 8601; giá trị không có múi giờ được hiểu là **UTC**. `scopeType` là chuỗi `Global` | `Department` | `PartyCell`.

| Method | Route | Quyền | Request | Response `data` / lỗi |
|---|---|---|---|---|
| GET | `/api/admin/roles` | `system.roles.manage` | — | `AdminRoleDto[]` = `{ id, name, description, isProtected, isSystem, permissionCodes: string[], assignmentCount }` (`assignmentCount` = bản gán đang/sắp hiệu lực) |
| GET | `/api/admin/roles/{id}` | `system.roles.manage` | — | `AdminRoleDto`; 404 |
| POST | `/api/admin/roles` | `system.roles.manage` | `{ name, description?, permissionCodes?: string[] }` | `AdminRoleDto`; 400 tên trống/>100 ký tự/mã lạ; 409 trùng tên (không phân biệt hoa thường) |
| PUT | `/api/admin/roles/{id}` | `system.roles.manage` | `{ name, description? }` | `AdminRoleDto`; 400; 404; 409 trùng tên |
| PUT | `/api/admin/roles/{id}/permissions` | `system.roles.manage` | `{ permissionCodes: string[] }` (đặt lại toàn bộ) | `AdminRoleDto`; 400 mã lạ / thêm quyền "không áp dụng phạm vi" cho vai trò đang được gán theo Phòng/Chi bộ; 403 vai trò mình đang được gán; 404; 409 vai trò bảo vệ thiếu 2 quyền quản trị / mất quản trị cuối |
| DELETE | `/api/admin/roles/{id}` | `system.roles.manage` | — | xóa mềm; 404; 409 vai trò bảo vệ / "đang có N bản gán…" |
| GET | `/api/admin/permissions` | `system.roles.manage` | — | `PermissionModuleDto[]` = `{ module, moduleName, permissions: [{ code, name, module, description, appliesScope, sortOrder }] }` |
| GET | `/api/admin/assignments?userId=&roleId=&scopeType=&scopeId=&activeOn=` | `system.assignments.manage` | query (tất cả tùy chọn; không có `activeOn` → gồm cả bản gán đã hết hạn = lịch sử) | `RoleAssignmentDto[]` = `{ id, userId, username, fullName, roleId, roleName, scopeType, scopeId, scopeName, validFrom, validTo, note, status: Active \| Future \| Expired }`; 400 `scopeType` sai |
| POST | `/api/admin/assignments` | `system.assignments.manage` | `{ userId, roleId, scopeType?: "Global", scopeId?, validFrom?, validTo?, note? }` | `RoleAssignmentDto`; 400 (người/vai trò/Phòng/Chi bộ không tồn tại hoặc ngừng hoạt động, Global kèm `scopeId`, thiếu `scopeId`, vai trò có quyền chỉ-Global gán theo phạm vi, `validTo ≤ validFrom`, trùng bản gán chồng lấn thời gian, ghi chú > 1000); 403 tự gán cho mình |
| PUT | `/api/admin/assignments/{id}` | `system.assignments.manage` | `{ validFrom?, validTo?, note? }` (`validFrom` trống = giữ; `validTo` trống = không thời hạn) | `RoleAssignmentDto`; 400; 403 bản gán của mình; 404; 409 mất quản trị cuối |
| POST | `/api/admin/assignments/{id}/end` | `system.assignments.manage` | — | `RoleAssignmentDto` (`validTo = now`); 400 đã hết hạn; 403; 404; 409 |
| DELETE | `/api/admin/assignments/{id}` | `system.assignments.manage` | — | xóa mềm; 403; 404; 409 |
| GET | `/api/admin/users/{id}/effective-permissions` | `system.assignments.manage` \| `system.users.read` | — | `{ userId, isActive, permissions: [{ code, name, module, sources: [{ scopeType, scopeId, scopeName, roleName, assignmentId }] }], assignments: RoleAssignmentDto[] (đang hiệu lực) }`; 403; 404 |
| POST | `/api/admin/users/{id}/roles` | `system.assignments.manage` | `{ roleIds: string[] }` | **Tương thích trang `/users` cũ**: đặt tập vai trò phạm vi Global (kết thúc bản gán Global không còn trong danh sách, tạo bản gán mới); trả `RoleAssignmentDto[]` đang hiệu lực. Task 11 nên dùng `/api/admin/assignments`. |

Đã bỏ: `POST /api/admin/users/{id}/roles` với `{ roleCodes }` (thay bằng `roleIds`). `GET /api/admin/roles` và `/api/admin/permissions` **đổi hình dạng** phản hồi (trang `/users` cũ dùng qua lớp chuyển đổi trong `frontend/services/roleService.ts`).

`/api/auth/me` + login: **chưa** có `grants` (file của task 08) — dữ liệu sẵn qua `IRoleAssignmentService.GetGrantsAsync(userId)` → `AccessGrantDto[] = { code, scopeType, scopeId, scopeName }` (loại trùng, theo thứ tự danh mục).

## Chữ ký `IRoleAssignmentService` cho task 13

Namespace `CongTacDang.Application.Services`; đăng ký scoped trong `AuthorizationExtensions`. `ScopeType` là `CongTacDang.Application.Common.Security.ScopeType { Global = 0, Department = 1, PartyCell = 2 }`.

```csharp
public sealed record RoleAssignmentQuery(Guid? UserId = null, Guid? RoleId = null, ScopeType? ScopeType = null,
    Guid? ScopeId = null, DateTime? ActiveOn = null);

public interface IRoleAssignmentService
{
    Task<RoleAssignmentDto> AssignAsync(Guid userId, Guid roleId, ScopeType scopeType, Guid? scopeId,
        DateTime? validFrom, DateTime? validTo, string? note, CancellationToken ct = default);
    Task<RoleAssignmentDto> UpdateAsync(Guid assignmentId, DateTime? validFrom, DateTime? validTo, string? note, CancellationToken ct = default);
    Task<RoleAssignmentDto> EndAsync(Guid assignmentId, CancellationToken ct = default);
    Task DeleteAsync(Guid assignmentId, CancellationToken ct = default);
    Task<List<RoleAssignmentDto>> QueryAsync(RoleAssignmentQuery query, CancellationToken ct = default);
    Task<UserEffectivePermissionsDto> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct = default);
    Task<List<AccessGrantDto>> GetGrantsAsync(Guid userId, CancellationToken ct = default);
    Task EnsureAdministratorsRemainWithoutUserAsync(Guid userId, CancellationToken ct = default);
    Task<List<RoleAssignmentDto>> SetGlobalRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken ct = default);
}
```

Hành vi `AssignAsync` (task 13 dùng khi import): yêu cầu người gọi có `system.assignments.manage` **phạm vi Global** (người đăng nhập hiện tại, qua `IAuthorizationGuard`); `validFrom` null = ngay bây giờ; thời gian không múi giờ = UTC; mỗi lần gọi `SaveChanges` một lần → gọi trong `IUnitOfWork.ExecuteInTransactionAsync` để cả lô trong 1 transaction; lỗi nghiệp vụ:
- `ValidationException` (400, tiếng Việt): tài khoản không tồn tại/đã xóa; vai trò không tồn tại/đã xóa; loại phạm vi sai; Global kèm Phòng/Chi bộ; thiếu Phòng/Chi bộ; Phòng/Chi bộ không tồn tại hoặc ngừng hoạt động; vai trò có quyền chỉ-Global gán theo Phòng/Chi bộ; `validTo ≤ validFrom`; **trùng bản gán** cùng người–vai trò–phạm vi chồng lấn thời gian; ghi chú > 1000 ký tự.
- `ForbiddenException` (403): tự gán cho chính mình; người gọi thiếu quyền.
- Tìm vai trò theo **tên** (cột import "tên vai trò") → dùng `IRoleRepository.GetAllRolesWithPermissionsAsync()` rồi so khớp tên (không phân biệt hoa thường); tìm Phòng/Chi bộ theo mã qua repository của task 10.

## Kết quả grep

`grep -rnE "AppRoles|AppPermissions|IsInRole|HasRole\(|QUAN_TRI_HE_THONG|BAN_THUONG_VU|BI_THU_CHI_BO|TO_THAM_DINH|DANG_UY_CO_SO" backend/src frontend --include=*.cs --include=*.ts --include=*.tsx` (bỏ `node_modules`, `Migrations/`):

| File | Số dòng | Lý do |
|---|---|---|
| `Infrastructure/Data/DataSeeder.cs` | 7 | **được phép** — tìm vai trò mặc định/vai trò cũ theo `Code` |
| `Application/Common/Security/AppPermissions.cs`, `AppRoles.cs`, `LegacyPermissionMap.cs` | (định nghĩa) | giữ, `[Obsolete]` — còn chỗ dùng ngoài phạm vi |
| `Api/Controllers/UserController.cs` | 6 | task 08 — `[Authorize(Policy = AppPermissions.Users*)]` |
| `Api/Controllers/OrganizationController.cs` | 6 | task 10 — `[Authorize(Policy = AppPermissions.Branches*)]` |
| `Api/Controllers/AuditController.cs` | 1 | task 08 — `AppPermissions.RolesManage` |
| `Application/Services/UserService.cs` | 3 | task 08 — `roles.Add(AppRoles.CAN_BO/BAN_THUONG_VU/BI_THU_CHI_BO)` trong `MapToProfileDto` (T-60) |
| `frontend/services/authService.ts` | 1 | task 08 — chú thích `(CAN_BO, BI_THU_CHI_BO, ...)` |

`frontend/app`, `components`, `contexts`: **không còn** `hasRole`, mã vai trò, mã quyền cũ.

## Thay đổi schema (cần migration `Wave4`)

| Bảng | Thay đổi | Ghi chú |
|---|---|---|
| `user_role_assignments` (mới) | `Id uuid PK`, `UserId uuid NOT NULL` FK → `party_member_profiles` (Restrict), `RoleId uuid NOT NULL` FK → `roles` (Restrict), `ScopeType integer NOT NULL`, `ScopeId uuid NULL`, `ValidFrom timestamptz NOT NULL`, `ValidTo timestamptz NULL`, `Note varchar(1000) NULL`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `IsDeleted boolean`, `DeletedAt`, `DeletedBy`; index `(UserId)`, `(RoleId)`, `(ScopeType, ScopeId)` | Cấu hình `Infrastructure/Data/Configurations/UserRoleAssignmentConfiguration.cs` |
| `roles` | thêm `IsProtected boolean NOT NULL DEFAULT false`; `Description` → `varchar(1000)`; index unique có lọc `IX_roles_Name` trên `Name` `WHERE "IsDeleted" = FALSE` | Tên vai trò hiện có không trùng (6 vai trò cũ) |
| `permissions` | thêm `Module varchar(50) NOT NULL DEFAULT ''`, `SortOrder integer NOT NULL DEFAULT 0` | Seeder điền theo `PermissionCodes` khi khởi động |
| `user_roles` | **giữ nguyên** (chưa xóa) | Xóa sau khi task 08 bỏ chỗ đọc — xem "Cần phối hợp" |
| — | Cấu hình `AppRole`/`Permission` chuyển từ `OnModelCreating` sang `Configurations/AppRoleConfiguration.cs`, `PermissionConfiguration.cs` (không đổi bảng) | |

**Chuyển dữ liệu user–role cũ → bản gán:** không cần SQL trong migration — `DataSeeder` chạy sau `MigrateAsync` khi khởi động: nếu `user_role_assignments` trống và bảng `user_roles` tồn tại thì chuyển mỗi cặp (đọc SQL thô, vẫn chạy sau khi gỡ quan hệ khỏi model), ánh xạ: `CAN_BO` → "Người được đánh giá" Global; `BI_THU_CHI_BO` → "Chi ủy / Bí thư Chi bộ" phạm vi **Chi bộ của người đó** (chưa thuộc Chi bộ → bỏ qua, log cảnh báo); `TO_THAM_DINH` → "Cơ quan thẩm định" Global; `BAN_THUONG_VU`, `DANG_UY_CO_SO` → "Văn phòng Đảng ủy" Global; `QUAN_TRI_HE_THONG` → chính nó (được đánh dấu bảo vệ) Global; vai trò tự tạo khác → chính vai trò đó Global. Vai trò mặc định mới được tạo khi CSDL chỉ có 6 vai trò cũ. Vai trò cũ (trừ quản trị) giữ nguyên nhưng mã quyền cũ không còn hiệu lực — quản trị có thể xóa khi không còn bản gán.

Nếu người điều phối muốn chuyển ngay trong migration (không chờ seeder), SQL tương đương cho phần Global (chạy **sau** khi seeder đã tạo vai trò mặc định — nên để seeder làm):
```sql
INSERT INTO user_role_assignments ("Id","UserId","RoleId","ScopeType","ScopeId","ValidFrom","CreatedAt","IsDeleted","Note")
SELECT gen_random_uuid(), ur.user_id, ur.role_id, 0, NULL, now(), now(), false, 'Chuyển từ user_roles'
FROM user_roles ur WHERE NOT EXISTS (SELECT 1 FROM user_role_assignments);
```

## Key config mới
Không có. (Cache 5 phút vẫn là hằng số `PermissionCache.DefaultTtl`.) Test: `ApiFactory.LoginAsAsync(username, password, distinctClientIp: true)` gắn header `X-Test-Client-Ip` (chỉ có tác dụng trong host test qua `IStartupFilter`) để mỗi client một phân vùng giới hạn đăng nhập; mặc định không đổi.

## Thay đổi hành vi API / breaking change
- **Phân quyền theo bản gán**: người dùng chỉ có quyền từ bản gán vai trò đang hiệu lực. CSDL cũ: seeder chuyển tự động lần khởi động đầu (xem trên). Mã quyền cũ trong CSDL không còn hiệu lực.
- **Quản trị kỹ thuật không xem nội dung đánh giá** (thiết kế mục 1.6): vai trò "Quản trị hệ thống" mặc định không có `evaluation.*` → `records`, `branch-records`, tệp minh chứng của người khác… trả 403/rỗng. Trang tổng quan/đánh giá của frontend ở "chế độ quản trị" hiện bảng trống.
- Policy composite cũ (`Policy_*`, `Require*`) bị xóa; controller dùng mã mới (bảng trên). `GET periods`, `my-record`, `records/{id}` chỉ cần đăng nhập (service kiểm tra).
- Tải tệp: cần `evaluation.self` (hoặc `attachment.general.manage`); tệp mã `GENERAL` chỉ công khai khi người tải có `attachment.general.manage` (giữ ngữ nghĩa cũ "do quản trị tải").
- Báo cáo theo Chi bộ không truyền `branchId`: phạm vi nhiều Chi bộ → 400 "Hãy chọn Chi bộ"; chỉ phạm vi Phòng → 403.
- Thông báo 403 mới (tên quyền hiển thị; xung đột lợi ích; sai cấp thẩm quyền).
- Login/refresh: trường `roles` giờ là **tên** các vai trò đang hiệu lực (trước là mã). `/api/auth/me` vẫn lấy `roles` từ `UserService` (quan hệ cũ + suy từ `PartyRole`) — không dùng cho phân quyền, task 08 xử lý (T-60).
- API quản trị: xem mục "API quản trị cho task 11".

## Cần phối hợp
**Task 08** (file của task 08, không sửa):
1. `AuthController.Me` và `AuthService` (login/refresh): thêm `grants` = `IRoleAssignmentService.GetGrantsAsync(userId)`; bỏ `effective.LegacyRoleCodes` (đã `[Obsolete]`, giờ là tên vai trò) khi bỏ claim role/perm khỏi JWT.
2. `UserService`: bỏ `IAccessPolicy` (adapter tạm `ProfileAccessPolicyAdapter` → `system.users.read/manage` theo phạm vi) và thay bằng `IAuthorizationGuard`; bỏ `AppRoles`/`member.Roles` trong `MapToProfileDto` (T-60); `GetRolesAsync` vẫn dùng được `IRoleRepository.GetAllRolesWithPermissionsAsync()`.
3. `UserController` → `[RequirePermission(PermissionCodes.SystemUsersRead/SystemUsersManage)]`; `/api/users/list` lọc theo `GetScope(system.users.read)` (phần T-61 của task 08). `AuditController` → `system.audit.read` (hiện mã cũ `roles.manage` được ánh xạ sang **một trong** roles/assignments/audit).
4. `UserRepository.GetWithRolesAndPermissions*` (`SpecificRepositories.cs` dòng ~56–75) và `RefreshTokenRepository.GetByTokenWithUserAsync` (dòng ~28): bỏ `.Include(Roles)`.
5. Khóa/xóa tài khoản (L9): gọi `IRoleAssignmentService.EnsureAdministratorsRemainWithoutUserAsync(userId)` → 409 nếu là quản trị cuối; `IAccessCacheInvalidator.InvalidateUser`.
6. `frontend/services/authService.ts` dòng 11: chú thích còn tên mã vai trò cũ.

**Task 10:** `OrganizationController` → đọc: `[Authorize]`; ghi: `[RequirePermission(PermissionCodes.CatalogManage)]` (hiện đi qua ánh xạ mã cũ, kết quả tương đương).

**Người điều phối (sau khi 08, 10 merge):**
1. Migration `Wave4` theo mục "Thay đổi schema".
2. Khi không còn chỗ đọc `Roles`: xóa khối `user_roles` trong `AppRoleConfiguration.cs`, `AppRole.Members`, `PartyMemberProfile.Roles`, `IUserRepository.GetWithRolesAndPermissions*` (nếu task 08 chưa bỏ), `IAttachmentAccessReader.GetUserIdsInRoleAsync` + `AttachmentRepository.GetUserIdsInRoleAsync` (không còn dùng), rồi migration xóa bảng `user_roles` (seeder tự bỏ qua khi bảng không còn).
3. Xóa `AppPermissions.cs`, `AppRoles.cs`, `LegacyPermissionMap.cs` + nhánh mã cũ trong `PermissionPolicyProvider.CreateRequirement`, `EffectivePermissions.LegacyRoleCodes/HasLegacyRole`, `ProfileAccessPolicyAdapter.cs` (`IAccessPolicy`, `AccessOperation`), constructor `PermissionResolver(IUserRepository, PermissionCache)` và dòng đăng ký `IAccessPolicy` trong `AuthorizationExtensions`.
4. `CLAUDE.md` mục "Quy ước": phân quyền theo mã quyền (`PermissionCodes`) + bản gán có phạm vi; kiểm tra đối tượng qua `IAuthorizationGuard`.
5. `docs/deployment.md`: ghi chú cache quyền trong bộ nhớ một instance (thiết kế mục 7) — không thuộc phạm vi file của task này.

**Task 11:** dùng bảng API trên; `AuthContext.hasPermission` đã chỉ so mã (không còn ngoại lệ quản trị), `hasRole` đã xóa; `roleService.ts` hiện là lớp chuyển đổi tạm cho trang `/users` cũ; hiển thị `user.roles[0]` trong `AppSidebar`/`AppHeader` giờ là tên vai trò (login) hoặc mã cũ (`/me`) — nên đổi sang `grants`.

**Task 12:** `EvaluationMeeting` chỉ có `PartyCellId` nên vai trò "Thư ký tập thể lãnh đạo" gán theo **Phòng** không thấy/lập được biên bản nào (đúng luật phạm vi, nhưng thiếu dữ liệu Phòng trên biên bản). Các mã `evaluation.tasks.approve`, `evaluation.collective.record`, `evaluation.director.review`, `evaluation.publish`, `evaluation.reopen` đã có luật trong guard (phạm vi + xung đột lợi ích) nhưng chưa có endpoint.

## Phát hiện thêm
- 🟡 `EvaluationMeeting` thiếu `DepartmentId` (xem task 12 ở trên) — đề xuất mã mới.
- 🟡 Seeder chuyển `DANG_UY_CO_SO`/`BAN_THUONG_VU` cũ sang "Văn phòng Đảng ủy" (có cả `decide` và `decide.external`, `publish`, `reopen`) — rộng hơn quyền cũ; chỉ ảnh hưởng CSDL thử nghiệm, quản trị cần rà lại bản gán sau nâng cấp.
- ⚪ Tệp cũ chưa gắn hồ sơ và không có người tải lên: trước chỉ quản trị xem được, nay không ai xem được (quản trị không còn quyền đặc biệt trên tệp).
- ⚪ Các service vẫn nhận `requesterId` từ controller nhưng guard đánh giá theo người đăng nhập (`ICurrentUserService`) — luôn trùng nhau ở API hiện tại; `AttachmentService` đánh giá theo `requesterId` truyền vào (dùng cùng luật `AuthorizationGuard.Evaluate`).
- ⚪ `ReportService.cs` dòng ~432 còn chú thích nhắc `IAccessPolicy`.
