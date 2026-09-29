using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Imports;
using CongTacDang.Application.Imports.Definitions;
using CongTacDang.Application.Services;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>
/// Kiểm thử loại import <c>role-assignments</c> (task 13, T-64): phân giải người/vai trò/phạm vi, chốt tự gán,
/// quyền "không áp dụng phạm vi", trùng bản gán (CSDL và trong tệp), ngày tháng, ghi qua <see cref="IRoleAssignmentService"/>.
/// </summary>
public class RoleAssignmentImportTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 3, 0, 0, TimeSpan.Zero);

    private static readonly Guid Importer = Guid.NewGuid();
    private static readonly Guid UserA = Guid.NewGuid();
    private static readonly Guid UserB = Guid.NewGuid();
    private static readonly Guid UserDeleted = Guid.NewGuid();
    private static readonly Guid RoleEvaluatee = Guid.NewGuid();
    private static readonly Guid RoleCell = Guid.NewGuid();
    private static readonly Guid RoleAdmin = Guid.NewGuid();
    private static readonly Guid DeptKt = Guid.NewGuid();
    private static readonly Guid CellKt = Guid.NewGuid();
    private static readonly Guid CellClosed = Guid.NewGuid();

    [Fact]
    public void Definition_Kind_Permissions_AndColumns()
    {
        var (definition, _, _) = Create();
        Assert.Equal("role-assignments", definition.Kind);
        Assert.Equal(new[] { PermissionCodes.SystemAssignmentsManage }, definition.RequiredPermissions);
        Assert.Equal(
            new[] { "Tên đăng nhập", "Tên vai trò", "Loại phạm vi", "Mã đơn vị", "Từ ngày", "Đến ngày", "Ghi chú" },
            definition.TemplateColumns.Select(c => c.Header));
        var scope = definition.TemplateColumns.Single(c => c.Key == RoleAssignmentImportDefinition.ScopeTypeKey);
        Assert.Equal(new[] { "Toàn công ty", "Đơn vị chính quyền", "Tổ chức Đảng" }, scope.AllowedValues);
    }

    [Fact]
    public async Task ValidRows_AreCreate_DatesAreVietnamDays_AndCommitCallsAssignService()
    {
        var (definition, service, _) = Create();
        var analysis = await AnalyzeAsync(definition,
            Row(2, "CB.A", "người ĐƯỢC  đánh giá", "toàn công ty", "", "", "", ""),
            Row(3, "cb.a", "Chi ủy / Bí thư Chi bộ", "Tổ chức Đảng", "cb-kt", "01/10/2026", "31/12/2026", "QĐ 12"),
            Row(4, "cb.b", "Chi ủy / Bí thư Chi bộ", "Đơn vị chính quyền", "PH-KT", "2026-10-01", "", ""));

        Assert.All(analysis.Rows, r => Assert.True(r.Action == ImportRowAction.Create, string.Join(" ", r.Errors)));

        var processor = new ImportProcessor<RoleAssignmentImportRow>(definition);
        var result = await processor.CommitAsync(analysis, default);
        Assert.Equal(3, result.Created);
        Assert.Null(result.ResultFile);

        Assert.Equal(3, service.Calls.Count);
        var global = service.Calls[0];
        Assert.Equal((UserA, RoleEvaluatee, ScopeType.Global, (Guid?)null, (DateTime?)null, (DateTime?)null, (string?)null), global);

        var cell = service.Calls[1];
        Assert.Equal(UserA, cell.UserId);
        Assert.Equal(ScopeType.PartyCell, cell.ScopeType);
        Assert.Equal(CellKt, cell.ScopeId);
        // 01/10/2026 00:00 giờ Việt Nam = 30/09/2026 17:00 UTC; "đến ngày" tính cả ngày 31/12 → hết hạn 01/01/2027 00:00 (+7).
        Assert.Equal(new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc), cell.ValidFrom);
        Assert.Equal(new DateTime(2026, 12, 31, 17, 0, 0, DateTimeKind.Utc), cell.ValidTo);
        Assert.Equal("QĐ 12", cell.Note);

        Assert.Equal((UserB, RoleCell, ScopeType.Department, (Guid?)DeptKt), (service.Calls[2].UserId, service.Calls[2].RoleId, service.Calls[2].ScopeType, service.Calls[2].ScopeId));
    }

    [Fact]
    public async Task ReferencesAndGuardrails_AreRowErrors()
    {
        var (definition, _, _) = Create();
        var analysis = await AnalyzeAsync(definition,
            Row(2, "khong_co", "Người được đánh giá", "Toàn công ty", "", "", "", ""),
            Row(3, "cb.xoa", "Người được đánh giá", "Toàn công ty", "", "", "", ""),
            Row(4, "quantri", "Người được đánh giá", "Toàn công ty", "", "", "", ""),
            Row(5, "cb.a", "Vai trò không có", "Toàn công ty", "", "", "", ""),
            Row(6, "cb.a", "Quản trị hệ thống", "Tổ chức Đảng", "CB-KT", "", "", ""),
            Row(7, "cb.a", "Chi ủy / Bí thư Chi bộ", "Tổ chức Đảng", "CB-KHONG", "", "", ""),
            Row(8, "cb.a", "Chi ủy / Bí thư Chi bộ", "Tổ chức Đảng", "CB-DONG", "", "", ""),
            Row(9, "cb.a", "Chi ủy / Bí thư Chi bộ", "Đơn vị chính quyền", "CB-KT", "", "", ""),
            Row(10, "cb.a", "Người được đánh giá", "Toàn công ty", "PH-KT", "", "", ""),
            Row(11, "cb.a", "Chi ủy / Bí thư Chi bộ", "Tổ chức Đảng", "", "", "", ""),
            Row(12, "cb.a", "Người được đánh giá", "Tổ", "", "", "", ""),
            Row(13, "cb.a", "Người được đánh giá", "Toàn công ty", "", "32/13/2026", "", ""),
            Row(14, "cb.a", "Người được đánh giá", "Toàn công ty", "", "10/10/2026", "09/10/2026", ""),
            Row(15, "cb.a", "Người được đánh giá", "Toàn công ty", "", "", "27/09/2026", ""),
            Row(16, "cb.a", "Người được đánh giá", "Toàn công ty", "", "", "", new string('x', 1001)),
            Row(17, "", "", "", "", "", "", ""));

        string Errors(int rowNumber) => string.Join(" | ", analysis.Rows.Single(r => r.RowNumber == rowNumber).Errors);

        Assert.All(analysis.Rows, r => Assert.Equal(ImportRowAction.Error, r.Action));
        Assert.Contains("Không có tài khoản \"khong_co\"", Errors(2));
        Assert.Contains("đã bị xóa", Errors(3));
        Assert.Contains("tự gán vai trò cho chính mình", Errors(4));
        Assert.Contains("Không có vai trò tên \"Vai trò không có\"", Errors(5));
        Assert.Contains("chỉ gán được với Loại phạm vi \"Toàn công ty\"", Errors(6));
        Assert.Contains("Quản lý vai trò", Errors(6)); // nêu tên quyền, không nêu mã
        Assert.DoesNotContain("system.", Errors(6));
        Assert.Contains("Mã tổ chức Đảng \"CB-KHONG\" không có trong danh mục", Errors(7));
        Assert.Contains("đã ngừng hoạt động", Errors(8));
        Assert.Contains("Mã đơn vị chính quyền \"CB-KT\" không có trong danh mục", Errors(9));
        Assert.Contains("không kèm mã đơn vị", Errors(10));
        Assert.Contains("Thiếu \"Mã đơn vị\"", Errors(11));
        Assert.Contains("Chỉ nhận: Toàn công ty, Đơn vị chính quyền, Tổ chức Đảng", Errors(12));
        Assert.Contains("không phải ngày hợp lệ", Errors(13));
        Assert.Contains("phải bằng hoặc sau \"Từ ngày\"", Errors(14));
        Assert.Contains("đã qua", Errors(15));
        Assert.Contains("Ghi chú dài quá", Errors(16));
        Assert.Contains("Thiếu \"Tên đăng nhập\"", Errors(17));
        Assert.Contains("Thiếu \"Tên vai trò\"", Errors(17));
    }

    [Fact]
    public async Task DuplicateWithEffectiveAssignment_OrWithinFile_IsRowError_NonOverlappingIsAllowed()
    {
        var existing = new List<ExistingAssignmentEntry>
        {
            // Đang hiệu lực, không thời hạn.
            new(UserA, RoleEvaluatee, ScopeType.Global, null, Now.UtcDateTime.AddDays(-10), null),
            // Chi ủy Chi bộ KT của cb.b tới hết 31/10/2026 (giờ VN).
            new(UserB, RoleCell, ScopeType.PartyCell, CellKt,
                Now.UtcDateTime.AddDays(-10), RoleAssignmentImportDefinition.StartOfDayUtc(new DateOnly(2026, 11, 1)))
        };
        var (definition, _, _) = Create(existing);

        var analysis = await AnalyzeAsync(definition,
            Row(2, "cb.a", "Người được đánh giá", "Toàn công ty", "", "", "", ""),               // trùng bản gán đang hiệu lực
            Row(3, "cb.b", "Chi ủy / Bí thư Chi bộ", "Tổ chức Đảng", "CB-KT", "", "", ""),             // chồng lấn (hiện tại → không hạn)
            Row(4, "cb.b", "Chi ủy / Bí thư Chi bộ", "Tổ chức Đảng", "CB-KT", "01/11/2026", "", ""),   // nối tiếp sau khi hết hạn → hợp lệ
            Row(5, "cb.b", "Chi ủy / Bí thư Chi bộ", "Tổ chức Đảng", "CB-KT", "15/11/2026", "", ""),   // trùng dòng 4 trong tệp
            Row(6, "cb.b", "Chi ủy / Bí thư Chi bộ", "Đơn vị chính quyền", "PH-KT", "", "", ""),              // khác phạm vi → hợp lệ
            Row(7, "cb.a", "Chi ủy / Bí thư Chi bộ", "Tổ chức Đảng", "CB-KT", "", "", ""));            // khác vai trò → hợp lệ

        var byRow = analysis.Rows.ToDictionary(r => r.RowNumber);
        Assert.Contains("đã có vai trò \"Người được đánh giá\" ở phạm vi này", string.Join(" ", byRow[2].Errors));
        Assert.Contains("không thời hạn", string.Join(" ", byRow[2].Errors));
        Assert.Contains("đến hết 31/10/2026", string.Join(" ", byRow[3].Errors));
        Assert.Equal(ImportRowAction.Create, byRow[4].Action);
        Assert.Contains("Trùng với dòng 4", string.Join(" ", byRow[5].Errors));
        Assert.Equal(ImportRowAction.Create, byRow[6].Action);
        Assert.Equal(ImportRowAction.Create, byRow[7].Action);
    }

    [Fact]
    public async Task Commit_ServiceRejection_Propagates_SoFrameworkRollsBack()
    {
        var (definition, service, _) = Create();
        service.FailOnCall = 2;
        var analysis = await AnalyzeAsync(definition,
            Row(2, "cb.a", "Người được đánh giá", "Toàn công ty", "", "", "", ""),
            Row(3, "cb.b", "Người được đánh giá", "Toàn công ty", "", "", "", ""),
            Row(4, "cb.b", "Chi ủy / Bí thư Chi bộ", "Tổ chức Đảng", "CB-KT", "", "", ""));
        Assert.False(analysis.HasErrors);

        var processor = new ImportProcessor<RoleAssignmentImportRow>(definition);
        await Assert.ThrowsAsync<ValidationException>(() => processor.CommitAsync(analysis, default));
        Assert.Equal(2, service.Attempts); // dừng ở dòng lỗi, không gọi tiếp
    }

    // ===================== Hỗ trợ =====================

    private static (RoleAssignmentImportDefinition Definition, FakeAssignments Service, FakeLookup Lookup) Create(
        List<ExistingAssignmentEntry>? existing = null)
    {
        var lookup = new FakeLookup(existing ?? new List<ExistingAssignmentEntry>());
        var service = new FakeAssignments();
        var definition = new RoleAssignmentImportDefinition(lookup, new FakeCatalog(), service, new FakeCurrentUser(Importer), new FixedTime());
        return (definition, service, lookup);
    }

    private static Task<ImportAnalysis> AnalyzeAsync(RoleAssignmentImportDefinition definition, params ImportSourceRow[] rows)
        => new ImportProcessor<RoleAssignmentImportRow>(definition).AnalyzeAsync(rows, default);

    private static ImportSourceRow Row(int number, string username, string role, string scopeType, string scopeCode,
        string from, string to, string note)
        => new(number, new Dictionary<string, string>
        {
            [RoleAssignmentImportDefinition.UsernameKey] = username,
            [RoleAssignmentImportDefinition.RoleNameKey] = role,
            [RoleAssignmentImportDefinition.ScopeTypeKey] = scopeType,
            [RoleAssignmentImportDefinition.ScopeCodeKey] = scopeCode,
            [RoleAssignmentImportDefinition.ValidFromKey] = from,
            [RoleAssignmentImportDefinition.ValidToKey] = to,
            [RoleAssignmentImportDefinition.NoteKey] = note
        });

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeLookup : IRoleAssignmentImportLookup
    {
        private readonly List<ExistingAssignmentEntry> _existing;

        public FakeLookup(List<ExistingAssignmentEntry> existing) => _existing = existing;

        public Task<IReadOnlyList<AssignmentUserEntry>> FindUsersAsync(IReadOnlyCollection<string> usernames, CancellationToken ct)
        {
            var all = new[]
            {
                new AssignmentUserEntry(Importer, "quantri", "Quản trị", true, false),
                new AssignmentUserEntry(UserA, "cb.a", "Cán bộ A", true, false),
                new AssignmentUserEntry(UserB, "cb.b", "Cán bộ B", true, false),
                new AssignmentUserEntry(UserDeleted, "cb.xoa", "Cán bộ đã xóa", false, true)
            };
            return Task.FromResult<IReadOnlyList<AssignmentUserEntry>>(
                all.Where(u => usernames.Contains(u.Username.ToLowerInvariant())).ToList());
        }

        public Task<IReadOnlyList<AssignmentRoleEntry>> GetRolesAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AssignmentRoleEntry>>(new[]
            {
                new AssignmentRoleEntry(RoleEvaluatee, "Người được đánh giá", new[] { PermissionCodes.EvaluationSelf }),
                new AssignmentRoleEntry(RoleCell, "Chi ủy / Bí thư Chi bộ",
                    new[] { PermissionCodes.EvaluationRead, PermissionCodes.EvaluationCellConfirm, PermissionCodes.MeetingRead }),
                new AssignmentRoleEntry(RoleAdmin, "Quản trị hệ thống",
                    new[] { PermissionCodes.SystemRolesManage, PermissionCodes.SystemAssignmentsManage, PermissionCodes.CatalogManage })
            });

        public Task<IReadOnlyList<ExistingAssignmentEntry>> GetCurrentOrFutureAssignmentsAsync(
            IReadOnlyCollection<Guid> userIds, DateTime nowUtc, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ExistingAssignmentEntry>>(
                _existing.Where(e => userIds.Contains(e.UserId) && (e.ValidTo == null || e.ValidTo > nowUtc)).ToList());
    }

    private sealed class FakeCatalog : IImportLookup
    {
        public Task<IReadOnlyList<CatalogLookupEntry>> GetDepartmentsAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CatalogLookupEntry>>(new[] { new CatalogLookupEntry(DeptKt, "PH-KT", "Phòng Kỹ thuật", true, false) });

        public Task<IReadOnlyList<CatalogLookupEntry>> GetPartyCellsAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CatalogLookupEntry>>(new[]
            {
                new CatalogLookupEntry(CellKt, "CB-KT", "Chi bộ Kỹ thuật", true, false),
                new CatalogLookupEntry(CellClosed, "CB-DONG", "Chi bộ đã ngừng", false, false)
            });

        public Task<IReadOnlySet<string>> FindExistingUsernamesAsync(IReadOnlyCollection<string> usernames, CancellationToken ct)
            => throw new NotSupportedException();
    }

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public FakeCurrentUser(Guid id) => UserId = id;
        public Guid? UserId { get; }
        public string UserName => "quantri";
        public string? IpAddress => null;
        public string? UserAgent => null;
        public string? RequestPath => null;
    }

    private sealed class FakeAssignments : IRoleAssignmentService
    {
        public List<(Guid UserId, Guid RoleId, ScopeType ScopeType, Guid? ScopeId, DateTime? ValidFrom, DateTime? ValidTo, string? Note)> Calls { get; } = new();

        public int Attempts { get; private set; }

        public int? FailOnCall { get; set; }

        public Task<RoleAssignmentDto> AssignAsync(Guid userId, Guid roleId, ScopeType scopeType, Guid? scopeId,
            DateTime? validFrom, DateTime? validTo, string? note, CancellationToken ct = default)
        {
            Attempts++;
            if (FailOnCall == Attempts)
                throw new ValidationException("Người dùng đã có vai trò này ở phạm vi này.");
            Calls.Add((userId, roleId, scopeType, scopeId, validFrom, validTo, note));
            return Task.FromResult(new RoleAssignmentDto { Id = Guid.NewGuid(), UserId = userId, RoleId = roleId });
        }

        // Import chỉ dùng AssignAsync.
        public Task<RoleAssignmentDto> UpdateAsync(Guid assignmentId, DateTime? validFrom, DateTime? validTo, string? note, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RoleAssignmentDto> EndAsync(Guid assignmentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid assignmentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<RoleAssignmentDto>> QueryAsync(RoleAssignmentQuery query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UserEffectivePermissionsDto> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<AccessGrantDto>> GetGrantsAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task EnsureAdministratorsRemainWithoutUserAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
