# Task 07 — Khung hợp đồng: phân quyền theo mã, test tích hợp (Đợt 3)

- **Agent:** B · **Branch:** `feat/authz-contract`
- **Mã:** T-55 (phần nền), T-63 (phần hạ tầng test)
- **Báo cáo:** `docs/remediation/reports/07-authz-contract.md`
- **Đọc trước:** `CLAUDE.md`, `RULES.md` (mục 8), `docs/thiet-ke/phan-quyen.md`.

## Mục tiêu
Tạo **các interface, mã quyền và hạ tầng** mà task 08, 09, 10 (chạy song song ở Đợt 4) cùng dựa vào. **Hành vi phân quyền đối với người dùng cuối không đổi** sau task này. Task nhỏ, merge sớm — đừng làm việc của task 09.

## Việc cần làm

### 1. Danh mục mã quyền
- Tạo `Application/Common/Security/PermissionCodes.cs`: hằng số đúng như bảng mục 3 của thiết kế, kèm mảng `All` và metadata (`Name`, `Module`, `Description`, `AppliesScope`) dùng cho seed và giao diện.
- Giữ `AppPermissions.cs` (mã cũ) nguyên trạng — task 09 mới xóa.

### 2. Nguồn quyền mỗi request (bỏ phụ thuộc claim trong JWT)
- Interface (Application):
  ```csharp
  public interface IPermissionResolver {
      Task<EffectivePermissions> GetAsync(Guid userId, CancellationToken ct = default);
  }
  public sealed record PermissionGrant(string Code, ScopeType ScopeType, Guid? ScopeId, Guid SourceAssignmentId, string SourceRoleName);
  public sealed class EffectivePermissions { IReadOnlyList<PermissionGrant> Grants; bool Has(string code); /* ... */ }
  public enum ScopeType { Global = 0, Department = 1, PartyCell = 2 }
  public interface IAccessCacheInvalidator { void InvalidateUser(Guid userId); void InvalidateAll(); }
  ```
- Triển khai **v0** trên mô hình hiện tại (user ↔ role nhiều-nhiều): mọi quyền của role → `PermissionGrant` phạm vi `Global`, `SourceAssignmentId = Guid.Empty`. Có cache bộ nhớ theo userId (TTL 5 phút) + `IAccessCacheInvalidator`. `RoleService` gọi invalidate khi đổi quyền role / gán role.
- `Api/Extensions/AuthorizationExtensions.cs` (mới): chuyển toàn bộ khối `AddAuthorization` ra khỏi `Program.cs`; thêm `IAuthorizationPolicyProvider` + handler: policy tên = mã quyền (cũ **và** mới) → kiểm tra `IPermissionResolver.GetAsync(userId).Has(code)`. Các policy composite hiện có (`Policy_*`, `Require*`) giữ **kết quả như cũ** nhưng đánh giá từ resolver thay vì claim/role trong JWT.
- Thêm `[RequirePermission(string code)]` (attribute dựng trên policy) — task sau dùng.
- `AuthService` lấy danh sách quyền trả về cho login/refresh từ resolver (không tự tính từ `member.Roles`). `/api/auth/me` cũng vậy.
- **Chưa** bỏ claim role/perm khỏi JWT (task 08 bỏ). Không còn chỗ nào **đọc** claim `perm`/role để phân quyền.

### 3. Interface kiểm tra quyền theo đối tượng (chỉ khai báo + adapter)
- `Application/Common/Security/IAuthorizationGuard.cs` đúng mục 4 của thiết kế: `Can`, `Ensure`, `HasAny`, `GetScope`, kiểu `AccessTarget`, `ScopeFilter`.
- Triển khai v0 `LegacyAuthorizationGuard`: `HasAny` dùng resolver; `Can/Ensure` ánh xạ sang `IAccessPolicy` hiện có theo bảng ánh xạ mã cũ→mới (chỉ các mã có tương đương); `GetScope` trả Global nếu có quyền. Đăng ký DI. **Không** chuyển các service sang dùng guard (task 09 làm).

### 4. Interface tạo tài khoản (task 10 import dùng, task 08 triển khai lại)
```csharp
public interface IUserAccountService {
    Task<CreatedAccount> CreateAsync(CreateAccountCommand cmd, CancellationToken ct = default);
}
public sealed record CreateAccountCommand(string Username, string FullName, string? Email, string? PartyCardNumber,
    string? PositionTitle, Guid? DepartmentId, Guid? PartyCellId, ApprovalAuthority ApprovalAuthority);
public sealed record CreatedAccount(Guid UserId, string Username, string TemporaryPassword);
```
- Triển khai v0 tối thiểu trong `Application/Services/UserAccountService.cs`: kiểm tra username trống/trùng, băm mật khẩu tạm ngẫu nhiên, `MustChangePassword = true`, **trả mật khẩu tạm**. Không đổi `UserService.CreateUserAsync` (task 08 xử lý).

### 5. Trường dữ liệu dùng chung
- Enum `Domain/Enums/ApprovalAuthority` (`CoSo = 1`, `CapTren = 2`). Thay `PartyMemberProfile.IsApprovedByAttech` bằng `ApprovalAuthority` (sửa mọi chỗ dùng; `AccessPolicy` đổi điều kiện tương ứng, kết quả không đổi). Thêm `EvaluationRecord.ApprovalAuthority`, gán khi tạo hồ sơ.
- Thêm `PartyMemberProfile.SecurityStamp` (string, khởi tạo `Guid.NewGuid().ToString("N")`). Chưa dùng.
- `CongTacDangDbContext.OnModelCreating`: thêm `modelBuilder.ApplyConfigurationsFromAssembly(typeof(CongTacDangDbContext).Assembly)` để các task sau đặt cấu hình entity mới trong `Infrastructure/Data/Configurations/*.cs` mà **không sửa DbContext**.

### 6. Seed mã quyền mới
- `DataSeeder`: tạo bản ghi cho mọi `PermissionCodes.All` còn thiếu (không xóa mã cũ). Gán cho vai trò quản trị (`QUAN_TRI_HE_THONG`) các mã: `system.*`, `catalog.manage`, `attachment.general.manage` — **chỉ khi vừa tạo mới permission** (không ghi đè cấu hình, giữ nguyên tinh thần T-33). Mục đích: endpoint mới của task 08/10 dùng mã mới chạy được ngay.

### 7. Hạ tầng test tích hợp
- Project `backend/tests/CongTacDang.IntegrationTests` (thêm vào `.slnx`), gói: `Microsoft.AspNetCore.Mvc.Testing`, `Xunit.SkippableFact` (xUnit hiện là 2.9.3), `Npgsql` (đã có qua Infrastructure).
- `ApiFactory : WebApplicationFactory<Program>`: đọc chuỗi kết nối từ biến môi trường `CONGTACDANG_TEST_PG` (máy chủ `192.168.22.159`, CSDL quản trị `postgres`, tài khoản có `CREATEDB`). Mỗi lần chạy tạo CSDL `ctd_it_<yyyyMMddHHmmss>_<guid8>`, `EnsureCreated`, seed tối thiểu, xóa CSDL khi xong. Tắt `AutoMigrate`, `SeedSampleData`; cấu hình JWT secret test.
- An toàn trên máy chủ dùng chung: fixture **từ chối chạy** nếu tên CSDL đích không bắt đầu bằng `ctd_it_`; chỉ `DROP DATABASE` tên `ctd_it_*`; khi khởi động, dọn các CSDL `ctd_it_*` sót lại quá 24 giờ (do lần chạy trước bị ngắt). Không bao giờ kết nối tới `congtacdang_test`.
- CI (GitHub Actions) dùng service `postgres:16` riêng, không kết nối tới `192.168.22.159`.
- Helper: `LoginAsAsync(username, password)` trả `HttpClient` giữ cookie; `CreateUserWithPermissionsAsync(params string[] codes)` (dùng API/DbContext) để test dựng người dùng theo quyền.
- Thiếu biến môi trường → test **skip** (không fail), in lý do.
- Test mẫu: login thành công/sai mật khẩu; một endpoint yêu cầu quyền trả 403 khi thiếu quyền, 200 khi có.
- `.github/workflows/ci.yml`: thêm service `postgres:16`, đặt `CONGTACDANG_TEST_PG`, chạy `dotnet test` cả 2 project.
- Nếu `Program` chưa `public partial class Program {}` → thêm cuối `Program.cs`.

## Phạm vi file (Đợt 3 chỉ có task này — được sửa mọi file cần cho các mục trên)
Không sửa logic nghiệp vụ, giao diện, luồng đánh giá.

## Tiêu chí hoàn thành
- Toàn bộ unit test cũ pass **không sửa kỳ vọng** (chứng minh hành vi phân quyền không đổi).
- `grep -rn "FindFirst(\"perm\"\|HasClaim(\"perm\"\|IsInRole" backend/src` → không còn chỗ dùng để phân quyền.
- Test tích hợp chạy được khi có `CONGTACDANG_TEST_PG`; skip sạch khi không có.
- Báo cáo liệt kê chữ ký cuối cùng của mọi interface ở mục 2–4 (task Đợt 4 dựa vào đó).
