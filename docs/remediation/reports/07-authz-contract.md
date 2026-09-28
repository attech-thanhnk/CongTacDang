# Báo cáo: Task 07 — Khung hợp đồng phân quyền theo mã, test tích hợp

- **Branch:** `feat/authz-contract`
- **Commit cuối:** commit chứa báo cáo này (commit code cuối: `f240745`)
- **Ngày:** 2026-09-28

> Ghi chú môi trường: worktree ban đầu được tạo ở commit cũ `86cd795` (tổ tiên của `main`); đã fast-forward lên `76ee1b2` trước khi sửa file đầu tiên.

## Kết quả theo mã lỗi

| Mã | Trạng thái | Tóm tắt thay đổi | File chính |
|---|---|---|---|
| T-55 (phần nền) | done | Danh mục `PermissionCodes` (24 mã + metadata) và bảng ánh xạ mã cũ→mới; `IPermissionResolver` v0 (user↔role → grant Global) có cache 5 phút + `IAccessCacheInvalidator`; policy động tên = mã quyền (cũ và mới) + 7 policy composite/role cũ đánh giá từ resolver, **không còn đọc claim role/perm để phân quyền**; `[RequirePermission]`; 403 thiếu quyền trả `ApiResponse` nêu tên quyền hiển thị; `AuthService` (login/refresh) và `/api/auth/me` lấy quyền từ resolver; `IAuthorizationGuard` + `LegacyAuthorizationGuard` (adapter sang `IAccessPolicy`, chưa service nào dùng); `IUserAccountService` v0; `ApprovalAuthority` thay `IsApprovedByAttech`, `EvaluationRecord.ApprovalAuthority`, `PartyMemberProfile.SecurityStamp`; `ApplyConfigurationsFromAssembly`; seed mã quyền mới + gán mã quản trị mới cho `QUAN_TRI_HE_THONG` chỉ khi vừa tạo. | `Application/Common/Security/*`, `Api/Authorization/PermissionAuthorization.cs`, `Api/Extensions/AuthorizationExtensions.cs`, `Program.cs`, `AuthService.cs`, `RoleService.cs`, `UserService.cs`, `AuthController.cs`, `Application/Services/(I)UserAccountService.cs`, `DataSeeder.cs`, `CongTacDangDbContext.cs`, entity `PartyMemberProfile`, `EvaluationRecord` |
| T-63 (hạ tầng test) | done | Project `backend/tests/CongTacDang.IntegrationTests` (trong `.slnx`): `ApiFactory : WebApplicationFactory<Program>` tạo CSDL `ctd_it_<yyyyMMddHHmmss>_<guid8>`, `EnsureCreated`, seed tối thiểu qua `DataSeeder`, xóa CSDL khi xong, dọn CSDL `ctd_it_*` sót > 24 giờ; chốt an toàn tên CSDL; helper `LoginAsAsync`, `CreateUserWithPermissionsAsync`; skip sạch khi thiếu `CONGTACDANG_TEST_PG`; CI dùng service `postgres:16` riêng. | `backend/tests/CongTacDang.IntegrationTests/**`, `.github/workflows/ci.yml`, `Program.cs` (`public partial class Program`) |

## Chữ ký cuối cùng các interface (task Đợt 4 dựa vào)

Namespace `CongTacDang.Application.Common.Security` (trừ khi ghi khác).

### Mục 2 — nguồn quyền

```csharp
public interface IPermissionResolver
{
    Task<EffectivePermissions> GetAsync(Guid userId, CancellationToken ct = default);
}

public enum ScopeType { Global = 0, Department = 1, PartyCell = 2 }

public sealed record PermissionGrant(string Code, ScopeType ScopeType, Guid? ScopeId, Guid SourceAssignmentId, string SourceRoleName);

public sealed class EffectivePermissions
{
    public EffectivePermissions(Guid userId, IEnumerable<PermissionGrant> grants, IEnumerable<string>? legacyRoleCodes = null);
    public static EffectivePermissions Empty(Guid userId);
    public Guid UserId { get; }
    public IReadOnlyList<PermissionGrant> Grants { get; }
    public IReadOnlyCollection<string> Codes { get; }              // mã có ở ≥ 1 phạm vi
    public IReadOnlyCollection<string> LegacyRoleCodes { get; }    // CHỈ cho policy composite cũ — task 09 bỏ
    public bool Has(string code);
    public bool HasAny(params string[] codes);
    public IEnumerable<PermissionGrant> GrantsFor(string code);
    public bool HasLegacyRole(string roleCode);                    // CHỈ cho policy composite cũ — task 09 bỏ
}

public interface IAccessCacheInvalidator
{
    void InvalidateUser(Guid userId);
    void InvalidateAll();
}
```

Triển khai: `PermissionCache : IAccessCacheInvalidator` (singleton, `PermissionCache(TimeProvider? timeProvider = null, TimeSpan? ttl = null)`, TTL mặc định 5 phút, có "thế hệ" chống ghi đè cache cũ sau khi xóa) và `PermissionResolver : IPermissionResolver` (scoped, `PermissionResolver(IUserRepository users, PermissionCache cache)`). Người dùng không tồn tại / đã xóa / `IsActive = false` → `EffectivePermissions.Empty`. Vai trò/quyền đã xóa mềm bị bỏ qua. v0: mọi grant `ScopeType.Global`, `ScopeId = null`, `SourceAssignmentId = Guid.Empty`, `SourceRoleName = AppRole.Name`.

Đăng ký DI: `Api/Extensions/AuthorizationExtensions.AddPermissionAuthorization()` (1 dòng trong `Program.cs`) — task 09 thay `PermissionResolver`/`LegacyAuthorizationGuard` ngay trong file này.

### Mục 2 — attribute/policy (namespace `CongTacDang.Api.Authorization`)

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission);   // Policy = permission
    public string Permission { get; }
}
public sealed class PermissionRequirement : IAuthorizationRequirement { public string Permission { get; } }
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider   // tên policy = mã trong PermissionCodes.All ∪ AppPermissions.All, hoặc tên policy composite cũ; tên khác → DefaultAuthorizationPolicyProvider
public sealed class PermissionAuthorizationHandler : IAuthorizationHandler     // Has(code) từ IPermissionResolver
public sealed class PermissionAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler // 403 + ApiResponse nêu tên quyền
public static class LegacyPolicies { IReadOnlyDictionary<string, LegacyPolicyRequirement> All; } // Policy_*, Require* — task 09 xóa
public static class ClaimsPrincipalIdentityExtensions { Guid? GetUserId(this ClaimsPrincipal user); } // chỉ đọc danh tính (sub/NameIdentifier)
```

### Mục 3 — kiểm tra quyền theo đối tượng

```csharp
public sealed record AccessTarget(
    Guid? OwnerId = null,
    Guid? DepartmentId = null,
    Guid? PartyCellId = null,
    ApprovalAuthority? ApprovalAuthority = null)
{
    public static AccessTarget None { get; }
    public static AccessTarget ForRecord(EvaluationRecord record);   // MemberId, DepartmentId, PartyCellId, ApprovalAuthority (ảnh chụp)
}

public sealed record ScopeFilter(
    bool IsGlobal,
    IReadOnlyList<Guid> DepartmentIds,
    IReadOnlyList<Guid> PartyCellIds,
    Guid? OwnerId = null)                      // OR-điều kiện: chủ hồ sơ luôn thấy
{
    public static ScopeFilter None { get; }
    public static ScopeFilter Global { get; }
    public static ScopeFilter OwnerOnly(Guid ownerId);
    public bool IsEmpty { get; }
}

public interface IAuthorizationGuard
{
    bool Can(string permission, AccessTarget target);
    void Ensure(string permission, AccessTarget target);   // ForbiddenException (403), thông báo nêu tên quyền hiển thị
    bool HasAny(string permission);
    ScopeFilter GetScope(string permission);
}
```

`LegacyAuthorizationGuard` (scoped; `ICurrentUserService`, `IPermissionResolver`, `IUserRepository`, `IAccessPolicy`):
- `HasAny(p)` = resolver có `p` **hoặc** có một mã cũ ánh xạ sang `p` (`LegacyPermissionMap`).
- `Can`: `evaluation.read` → `CanAccessRecord(Read)`; `evaluation.self` → `CanAccessRecord(Update)`; `evaluation.cell.confirm` → `BranchReview`; `evaluation.decide` / `.external` → `ApprovalAuthority == CoSo / CapTren` **và** `CanAccessRecord(Approve)`; `collective.manage` → `CanAccessCollective(Update)`; `meeting.read/manage` → `CanAccessMeeting(Read/Update)`; `report.export` → `CanAccessBranch(Export)`; `system.users.read/manage` (có `OwnerId`) → `CanAccessProfile(Read/Update)`; mã khác → `HasAny`.
- `GetScope`: có quyền → `Global` (với `evaluation.read` kèm `OwnerId` = mình); `evaluation.self` → `OwnerOnly`; không có `evaluation.read` → `OwnerOnly`; còn lại `None`.
- Interface đồng bộ theo thiết kế → guard gọi resolver/repository đồng bộ (sync-over-async), nạp một lần mỗi request.

### Mục 4 — tạo tài khoản (namespace `CongTacDang.Application.Services`)

```csharp
public interface IUserAccountService
{
    Task<CreatedAccount> CreateAsync(CreateAccountCommand cmd, CancellationToken ct = default);
}
public sealed record CreateAccountCommand(string Username, string FullName, string? Email, string? PartyCardNumber,
    string? PositionTitle, Guid? DepartmentId, Guid? PartyCellId, ApprovalAuthority ApprovalAuthority);
public sealed record CreatedAccount(Guid UserId, string Username, string TemporaryPassword);
```

`UserAccountService` v0 (scoped, đăng ký trong `Program.cs`): trim username; trống / > 100 ký tự / có khoảng trắng / thiếu họ tên → `ValidationException` (400); trùng username (không phân biệt hoa thường, trong bản ghi chưa xóa) → `ConflictException` (409); mật khẩu tạm 12 ký tự (luôn có chữ + số, `RandomNumberGenerator`), lưu BCrypt, `MustChangePassword = true`; không gán vai trò; `public static string GenerateTemporaryPassword()`. `UserService.CreateUserAsync` giữ nguyên.

### Danh mục mã quyền

`PermissionCodes` (hằng số + `IReadOnlyList<PermissionDefinition> Definitions`, `string[] All`, `IsDefined`, `Find`, `DisplayName`), `public sealed record PermissionDefinition(string Code, string Name, string Module, string Description, bool AppliesScope)`. `AppliesScope = false`: `system.roles.manage`, `system.assignments.manage`, `system.audit.read`, `system.import`, `catalog.manage`, `period.manage`, `attachment.general.manage`. `evaluation.self` đặt `AppliesScope = true` (phạm vi bị bỏ qua, luật chủ hồ sơ). `LegacyPermissionMap.OldToNew` theo bảng thiết kế; `attachments.upload/delete` chỉ ánh xạ sang `evaluation.self` (văn bản chung cấp riêng cho quản trị). `AppPermissions.cs` giữ nguyên.

## Cổng kiểm tra

| Bước | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build backend/CongTacDang.slnx` | pass | 0 warning, 0 error |
| `dotnet test` — unit | pass | 96 test: 95 pass, 1 skip (`PdfConversionTests.Mau01…` — thiếu LibreOffice, skip sẵn có), 0 fail |
| `dotnet test` — integration (có `CONGTACDANG_TEST_PG`, máy chủ 159) | pass | 13 test: 13 pass, 0 skip, 0 fail (5 test HTTP trên PostgreSQL + 8 test chốt an toàn tên CSDL) |
| `dotnet test` — integration (không có biến) | pass (skip) | 5 test HTTP **skip** kèm lý do, 8 test tên CSDL pass — skip không tính là pass |
| `npx tsc --noEmit` | pass | frontend không đổi |
| `npm run build` | pass | |
| grep `FindFirst("perm"\|HasClaim("perm"\|IsInRole` trong `backend/src` | sạch | JWT vẫn **ghi** claim role/perm (task 08 bỏ) nhưng không nơi nào đọc để phân quyền |
| CSDL tạm sau khi chạy | sạch | Kiểm tra `pg_database` sau các lần chạy: 0 CSDL `ctd_it_*` còn lại |

### Luồng/test

| Luồng | Trạng thái | Test |
|---|---|---|
| Policy mới giữ nguyên kết quả policy claim cũ (26 policy × 64 tổ hợp vai trò mặc định + 19 vai trò 1 quyền; principal chỉ có danh tính) | pass | `AuthzContractTests.NewPolicies_MatchOldClaimPolicies_ForEveryRoleCombination` |
| Claim perm/role giả trong token bị bỏ qua; mã mới qua policy; ẩn danh bị từ chối | pass | `NewPermissionCodePolicy_UsesResolver_AndRejectsAnonymous` |
| Provider nhận mã cũ + mới, tên lạ → fallback | pass | `PolicyProvider_KnowsOldAndNewCodes_FallsBackForUnknownNames`, `RequirePermissionAttribute_UsesCodeAsPolicyName` |
| Danh mục mã khớp thiết kế, ánh xạ đầy đủ | pass | `PermissionCodes_MatchDesignCatalog` |
| Resolver: grant Global, bỏ vai trò đã xóa, người vô hiệu → rỗng | pass | `Resolver_MapsRolesToGlobalGrants_AndSkipsInactiveOrDeleted` |
| Cache: theo user, xóa theo user/toàn bộ, hết hạn 5 phút, chống ghi đè sau khi xóa | pass | `Resolver_CachesPerUser_UntilInvalidatedOrExpired`, `Cache_DoesNotStoreResultLoadedBeforeInvalidation` |
| `RoleService` xóa cache khi đổi quyền vai trò / gán vai trò | pass | `RoleService_InvalidatesCache_OnPermissionAndAssignmentChanges` |
| Guard v0 | pass | `Guard_HasAny_UsesLegacyMapping`, `Guard_Can_MatchesAccessPolicy_ForRecords`, `Guard_Decide_RespectsApprovalAuthority`, `Guard_GetScope_GlobalWhenGranted_OwnerAlwaysForRead`, `Guard_Anonymous_HasNothing` |
| Tạo tài khoản v0 | pass | `UserAccountService_CreatesAccountWithTemporaryPassword`, `UserAccountService_RejectsInvalidInput` (4), `UserAccountService_RejectsDuplicateUsername_CaseInsensitive`, `TemporaryPasswords_AreRandom` |
| Login đúng mật khẩu → `/api/auth/me` trả quyền từ CSDL | pass | `AuthzContractIntegrationTests.Login_WithCorrectPassword_ReturnsPermissionsFromDatabase` |
| Login sai mật khẩu → 401 | pass | `Login_WithWrongPassword_Returns401` |
| Endpoint yêu cầu quyền: 403 khi thiếu (thông báo nêu tên quyền, không nêu mã), 200 khi có | pass | `PermissionEndpoint_Returns403WithoutPermission_200WithPermission` |
| Đổi gán vai trò có hiệu lực ở request kế tiếp, không cần đăng nhập lại (cả cấp và thu hồi) | pass | `PermissionChange_TakesEffectOnNextRequest_WithoutRelogin` |
| Ẩn danh → 401 | pass | `Anonymous_Returns401` |

**Test cũ:** toàn bộ pass **không sửa kỳ vọng**. Chỉ sửa phần dựng dữ liệu cho biên dịch được: `AccessPolicyTests` (gán `ApprovalAuthority` thay `IsApprovedByAttech` trong `RecordOf` và bản sao luật cũ), `SecurityTests` (truyền thêm `PermissionResolver` vào constructor `AuthService`).

## Thay đổi schema (cần migration)

Người điều phối sinh migration (`Wave3`) — **cần SQL chuyển dữ liệu tay**, EF không tự suy ra:

| Bảng | Thay đổi | Ghi chú chuyển dữ liệu |
|---|---|---|
| `party_member_profiles` | thêm `ApprovalAuthority integer NOT NULL`; **xóa** `IsApprovedByAttech boolean` | Trước khi xóa cột cũ: `UPDATE party_member_profiles SET "ApprovalAuthority" = CASE WHEN "IsApprovedByAttech" THEN 1 ELSE 2 END;` (EF sẽ sinh DropColumn + AddColumn default 0 → phải chèn UPDATE giữa AddColumn và DropColumn, hoặc AddColumn default 1). |
| `party_member_profiles` | thêm `SecurityStamp varchar(64) NOT NULL` | Giá trị khác nhau từng dòng: `UPDATE party_member_profiles SET "SecurityStamp" = replace(gen_random_uuid()::text, '-', '');` (không để chuỗi rỗng). |
| `evaluation_records` | thêm `ApprovalAuthority integer NOT NULL` | Chụp từ hồ sơ cán bộ: `UPDATE evaluation_records r SET "ApprovalAuthority" = m."ApprovalAuthority" FROM party_member_profiles m WHERE m."Id" = r."MemberId";` |
| — | `ApplyConfigurationsFromAssembly` | Hiện chưa có lớp `IEntityTypeConfiguration` nào → không đổi model. |

Dữ liệu seed (không phải schema): 24 bản ghi `permissions` mới (Resource = Module, Action = phần sau module) và gán `system.*`, `catalog.manage`, `attachment.general.manage` cho vai trò `QUAN_TRI_HE_THONG` (chỉ khi các permission này vừa được tạo).

## Key config mới
| Key | Mặc định | Ý nghĩa |
|---|---|---|
| (biến môi trường) `CONGTACDANG_TEST_PG` | không đặt → test tích hợp skip | Chuỗi kết nối PostgreSQL tới CSDL quản trị (tài khoản có `CREATEDB`) cho test tích hợp. Không ghi vào file nào trong repo. |

Không có key cấu hình ứng dụng mới (TTL cache 5 phút là hằng số `PermissionCache.DefaultTtl`).

## Thay đổi hành vi API / breaking change
- **Kết quả phân quyền không đổi** với dữ liệu hiện có (chứng minh bằng test so khớp trên mọi tổ hợp vai trò). Khác biệt duy nhất là thời điểm hiệu lực: đổi quyền vai trò / gán vai trò có hiệu lực **ngay request kế tiếp** thay vì chờ access token hết hạn (≤ 15 phút); tài khoản bị vô hiệu hóa/xóa mất quyền ngay (trước đây token cũ còn dùng được tới khi hết hạn). Endpoint chỉ `[Authorize]` (không policy) vẫn cho qua như cũ — chặn hoàn toàn là việc của task 08.
- Claim `perm`/role trong JWT **vẫn được ghi** (giá trị giờ lấy từ resolver) nhưng không còn được đọc để phân quyền.
- 403 do thiếu quyền ở policy: trước trả body rỗng, nay trả `ApiResponse` `{ success: false, message: "Bạn không có quyền \"<tên quyền>\" để thực hiện thao tác này. Hãy liên hệ quản trị hệ thống nếu cần được cấp quyền." }`. Status code không đổi.
- `permissions` trong response login/refresh/`/api/auth/me` = `EffectivePermissions.Codes`: với vai trò quản trị có thêm các mã mới (`system.*`, `catalog.manage`, `attachment.general.manage`). Frontend chỉ kiểm tra mã cũ nên không ảnh hưởng. `roles` giữ nguyên (`/me` vẫn có fallback theo `PartyRole` — T-60, task 08).

## Cần phối hợp
- **Người điều phối:** sinh migration kèm 3 câu SQL chuyển dữ liệu ở mục "Thay đổi schema" (đặc biệt `IsApprovedByAttech` → `ApprovalAuthority`, nếu không mọi cán bộ trình cấp trên sẽ bị đổi thành giá trị 0/không hợp lệ).
- **Task 08:** bỏ claim role/perm khỏi JWT (`JwtService`, `IJwtService.GenerateToken`; `AuthService` đang truyền `effective.LegacyRoleCodes`/`Codes`); triển khai lại `IUserAccountService` (kiểm tra Phòng/Chi bộ tồn tại — v0 chưa kiểm tra, FK sai sẽ lỗi 500; trùng username với tài khoản **đã xóa mềm** hiện rơi vào unique index → 409 thông báo chung); gọi `IAccessCacheInvalidator.InvalidateUser` khi khóa/xóa/đổi/đặt lại mật khẩu (v0 đã gọi ở `UserService.UpdateUserAsync`/`DeleteUserAsync`); dùng `SecurityStamp`.
- **Task 09:** thay `PermissionResolver` (bản gán có phạm vi/thời hạn, bỏ mã không còn trong `PermissionCodes`) và `LegacyAuthorizationGuard` trong `AuthorizationExtensions`; xóa `LegacyPolicies`, `EffectivePermissions.LegacyRoleCodes/HasLegacyRole`, `LegacyPermissionMap`, `AppPermissions`, `AppRoles` khi chuyển controller sang `[RequirePermission(PermissionCodes.X)]`. Lưu ý `LegacyAuthorizationGuard.GetScope` v0 **thô** (có quyền → Global, kể cả `evaluation.read` của CAN_BO vốn chỉ xem hồ sơ mình theo `AccessPolicy`) — không dùng v0 để lọc danh sách. `IAuthorizationGuard` đồng bộ nên bản thật nên nạp quyền trước (vd. qua policy/middleware) để tránh sync-over-async.
- **Task 08/09/10 viết test tích hợp:** dùng `[Collection(ApiCollection.Name)]` + `[SkippableFact]` + `Skip.If(_factory.SkipReason != null, _factory.SkipReason)`; mỗi task một file test riêng. Endpoint `/api/auth/login` bị giới hạn 10 lần/phút/IP và TestServer dùng chung một phân vùng ("unknown") — toàn bộ test tích hợp hiện dùng 6 lần đăng nhập; khi thêm test cần tái sử dụng `HttpClient` đã đăng nhập hoặc cho phép cấu hình giới hạn (task sở hữu `SecurityExtensions`).
- `CLAUDE.md` mục "Quy ước" còn ghi "policy theo claim `perm` + role" — nên cập nhật sau khi merge (không sửa trong task này).

## Phát hiện thêm
- `DataSeeder` trước đây kiểm tra permission còn thiếu **có áp** query filter xóa mềm → nếu một permission bị xóa mềm, lần khởi động sau sẽ chèn trùng `Code` và vỡ unique index. Đã đổi sang `IgnoreQueryFilters()` trong phần seed permission (thuộc phạm vi task 07). Mức 🟡.
- Unique index `party_member_profiles.Username` bao gồm cả bản ghi đã xóa mềm và phân biệt hoa thường; đăng nhập so khớp chính xác hoa/thường. Đề xuất task 08 thống nhất chuẩn hóa username (lower-case) và chính sách tái sử dụng username đã xóa. Mức 🟡.
- Các thông báo lỗi JSON (`GlobalExceptionMiddleware`, MVC) mã hóa ký tự tiếng Việt thành `\uXXXX` — hợp lệ JSON nhưng khó đọc log/curl. Mức ⚪.
