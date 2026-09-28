using System.Globalization;
using System.Text;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;

namespace CongTacDang.Application.Imports.Definitions;

/// <summary>Một dòng import gán vai trò.</summary>
public sealed class RoleAssignmentImportRow
{
    /// <summary>Tên đăng nhập như trong tệp.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Tên vai trò như trong tệp.</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>Loại phạm vi (null nếu giá trị sai).</summary>
    public ScopeType? ScopeType { get; set; }

    /// <summary>Mã Phòng/Chi bộ (chữ hoa), null khi Toàn công ty.</summary>
    public string? ScopeCode { get; set; }

    /// <summary>Hiệu lực từ (UTC, đầu ngày giờ Việt Nam); null = ngay khi nhập.</summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>Hiệu lực đến (UTC, không bao gồm = đầu ngày sau "Đến ngày"); null = không thời hạn.</summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; set; }

    /// <summary>Id tài khoản đã phân giải khi kiểm tra.</summary>
    public Guid UserId { get; set; }

    /// <summary>Id vai trò đã phân giải khi kiểm tra.</summary>
    public Guid RoleId { get; set; }

    /// <summary>Id Phòng/Chi bộ đã phân giải khi kiểm tra (null khi Toàn công ty).</summary>
    public Guid? ScopeId { get; set; }
}

/// <summary>
/// Import gán vai trò kèm phạm vi và thời hạn (<c>role-assignments</c>, task 13 — T-64).
/// Kiểm tra người, vai trò, phạm vi tồn tại; vai trò có quyền "không áp dụng phạm vi" chỉ gán Toàn công ty;
/// không tự gán cho người đang nhập; trùng bản gán đang/sắp hiệu lực (cùng người – vai trò – phạm vi, chồng lấn thời gian)
/// hoặc trùng trong tệp → lỗi dòng. Ghi qua <see cref="IRoleAssignmentService"/> (đủ chốt chặn) trong transaction của khung.
/// </summary>
public sealed class RoleAssignmentImportDefinition : IImportDefinition<RoleAssignmentImportRow>
{
    /// <summary>Khóa cột.</summary>
    public const string UsernameKey = "username", RoleNameKey = "roleName", ScopeTypeKey = "scopeType", ScopeCodeKey = "scopeCode",
        ValidFromKey = "validFrom", ValidToKey = "validTo", NoteKey = "note";

    /// <summary>Giá trị cột "Loại phạm vi".</summary>
    public const string ScopeGlobal = "Toàn công ty", ScopeDepartment = "Phòng", ScopePartyCell = "Chi bộ";

    /// <summary>Múi giờ dùng để hiểu "Từ ngày"/"Đến ngày" (Việt Nam, UTC+7, không có giờ mùa hè).</summary>
    public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private const int MaxNoteLength = 1000;

    private static readonly string[] DateFormats = { "d/M/yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "d-M-yyyy", "d.M.yyyy" };

    private readonly IRoleAssignmentImportLookup _lookup;
    private readonly IImportLookup _catalog;
    private readonly IRoleAssignmentService _assignments;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _time;

    /// <summary>Khởi tạo.</summary>
    public RoleAssignmentImportDefinition(
        IRoleAssignmentImportLookup lookup,
        IImportLookup catalog,
        IRoleAssignmentService assignments,
        ICurrentUserService currentUser,
        TimeProvider? time = null)
    {
        _lookup = lookup;
        _catalog = catalog;
        _assignments = assignments;
        _currentUser = currentUser;
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public string Kind => "role-assignments";

    /// <inheritdoc />
    public string DisplayName => "Gán vai trò";

    /// <inheritdoc />
    public string Description => "Gán vai trò cho tài khoản đã có, kèm phạm vi (Toàn công ty / Phòng / Chi bộ) và thời hạn. "
        + "Mỗi dòng là một bản gán mới; không sửa hay thu hồi bản gán đã có (làm việc đó ở màn hình Gán vai trò).";

    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = new[] { PermissionCodes.SystemAssignmentsManage };

    /// <inheritdoc />
    public IReadOnlyList<ImportColumn> TemplateColumns { get; } = new[]
    {
        new ImportColumn(UsernameKey, "Tên đăng nhập", true,
            "Tên đăng nhập của tài khoản đã có (không phân biệt hoa thường). Không nhập tài khoản của chính người đang nhập dữ liệu.",
            null, "nguyenvana"),
        new ImportColumn(RoleNameKey, "Tên vai trò", true,
            "Tên vai trò đúng như trong màn hình Quản lý vai trò (không phân biệt hoa thường).", null, "Chi ủy / Bí thư Chi bộ"),
        new ImportColumn(ScopeTypeKey, "Loại phạm vi", true,
            "Toàn công ty: có hiệu lực với mọi đơn vị. Phòng / Chi bộ: chỉ trong đơn vị ghi ở cột Mã Phòng/Chi bộ. "
            + "Vai trò có quyền quản trị hệ thống, nhập dữ liệu, quản lý kỳ đánh giá… chỉ gán được Toàn công ty.",
            new[] { ScopeGlobal, ScopeDepartment, ScopePartyCell }, ScopePartyCell),
        new ImportColumn(ScopeCodeKey, "Mã Phòng/Chi bộ", false,
            "Bắt buộc khi Loại phạm vi là Phòng hoặc Chi bộ (mã đang hoạt động trong danh mục); để trống khi Toàn công ty.",
            null, "CB-KT"),
        new ImportColumn(ValidFromKey, "Từ ngày", false,
            "Ngày bắt đầu hiệu lực, dạng dd/mm/yyyy. Để trống = có hiệu lực ngay khi nhập.", null, "01/10/2026"),
        new ImportColumn(ValidToKey, "Đến ngày", false,
            "Ngày cuối cùng còn hiệu lực (tính cả ngày này), dạng dd/mm/yyyy. Để trống = không thời hạn.", null, "31/12/2026"),
        new ImportColumn(NoteKey, "Ghi chú", false, $"Căn cứ, số quyết định… Tối đa {MaxNoteLength} ký tự.", null, "Theo Quyết định số 12-QĐ/ĐU")
    };

    /// <inheritdoc />
    public RoleAssignmentImportRow ParseRow(ImportSourceRow source, ICollection<string> errors)
    {
        var row = new RoleAssignmentImportRow
        {
            Username = source.Get(UsernameKey),
            RoleName = source.Get(RoleNameKey),
            ScopeCode = source.GetOrNull(ScopeCodeKey) is { } code ? CatalogRules.NormalizeCode(code) : null,
            Note = source.GetOrNull(NoteKey)
        };

        var scopeText = source.Get(ScopeTypeKey);
        row.ScopeType = scopeText switch
        {
            ScopeGlobal => ScopeType.Global,
            ScopeDepartment => ScopeType.Department,
            ScopePartyCell => ScopeType.PartyCell,
            _ => null // Trống hoặc sai: khung đã báo lỗi cột.
        };

        if (row.ScopeType == ScopeType.Global && row.ScopeCode != null)
            errors.Add($"Phạm vi \"{ScopeGlobal}\" không kèm mã Phòng/Chi bộ. Hãy để trống cột \"Mã Phòng/Chi bộ\" hoặc đổi Loại phạm vi.");
        if (row.ScopeType is (ScopeType.Department or ScopeType.PartyCell) && row.ScopeCode == null)
            errors.Add($"Thiếu \"Mã Phòng/Chi bộ\" cho phạm vi \"{scopeText}\". Hãy nhập mã {scopeText} trong danh mục.");

        var fromDate = ParseDate(source.GetOrNull(ValidFromKey), "Từ ngày", errors);
        var toDate = ParseDate(source.GetOrNull(ValidToKey), "Đến ngày", errors);
        if (fromDate.HasValue)
            row.ValidFrom = StartOfDayUtc(fromDate.Value);
        if (toDate.HasValue)
            row.ValidTo = StartOfDayUtc(toDate.Value.AddDays(1));

        if (fromDate.HasValue && toDate.HasValue && toDate.Value < fromDate.Value)
            errors.Add("\"Đến ngày\" phải bằng hoặc sau \"Từ ngày\". Hãy sửa thời hạn.");
        else if (row.ValidTo.HasValue && row.ValidTo.Value <= _time.GetUtcNow().UtcDateTime)
            errors.Add("\"Đến ngày\" đã qua nên bản gán sẽ không bao giờ có hiệu lực. Hãy sửa ngày hoặc bỏ dòng này.");

        if (row.Note != null && row.Note.Length > MaxNoteLength)
            errors.Add($"Ghi chú dài quá {MaxNoteLength} ký tự. Hãy rút ngắn.");

        return row;
    }

    /// <inheritdoc />
    public async Task ValidateAsync(IReadOnlyList<ImportRow<RoleAssignmentImportRow>> rows, CancellationToken ct)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var usernames = rows.Select(r => r.Data.Username.ToLowerInvariant()).Where(u => u.Length > 0).Distinct().ToList();
        var users = (await _lookup.FindUsersAsync(usernames, ct))
            .GroupBy(u => u.Username.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.OrderBy(u => u.IsDeleted).First(), StringComparer.Ordinal);
        var roles = (await _lookup.GetRolesAsync(ct))
            .GroupBy(r => NormalizeName(r.Name))
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var departments = ToCatalog(await _catalog.GetDepartmentsAsync(ct));
        var partyCells = ToCatalog(await _catalog.GetPartyCellsAsync(ct));

        // Bước 1: phân giải người, vai trò, phạm vi.
        foreach (var row in rows)
        {
            ResolveUser(row, users);
            ResolveRole(row, roles);
            ResolveScope(row, departments, partyCells);
            row.Action = ImportRowAction.Create;
        }

        // Bước 2: trùng với bản gán đang/sắp hiệu lực trong CSDL và trùng trong tệp.
        var userIds = rows.Where(r => r.Data.UserId != Guid.Empty).Select(r => r.Data.UserId).Distinct().ToList();
        var existing = userIds.Count == 0
            ? new List<ExistingAssignmentEntry>()
            : (await _lookup.GetCurrentOrFutureAssignmentsAsync(userIds, now, ct)).ToList();

        var accepted = new List<ImportRow<RoleAssignmentImportRow>>();
        foreach (var row in rows)
        {
            var data = row.Data;
            if (data.UserId == Guid.Empty || data.RoleId == Guid.Empty || data.ScopeType == null
                || (data.ScopeType != ScopeType.Global && data.ScopeId == null))
                continue;

            var from = data.ValidFrom ?? now;
            var clash = existing.FirstOrDefault(e => e.UserId == data.UserId && e.RoleId == data.RoleId
                && e.ScopeType == data.ScopeType && e.ScopeId == data.ScopeId && Overlaps(e.ValidFrom, e.ValidTo, from, data.ValidTo));
            if (clash != null)
            {
                row.AddError($"Tài khoản \"{data.Username}\" đã có vai trò \"{data.RoleName}\" ở phạm vi này "
                    + $"(bản gán từ {ToLocalDate(clash.ValidFrom)}{(clash.ValidTo.HasValue ? $" đến hết {ToLocalDate(clash.ValidTo.Value.AddTicks(-1))}" : ", không thời hạn")}). "
                    + "Hãy bỏ dòng này hoặc sửa thời hạn bản gán hiện có ở màn hình Gán vai trò.");
                continue;
            }

            var twin = accepted.FirstOrDefault(a => a.Data.UserId == data.UserId && a.Data.RoleId == data.RoleId
                && a.Data.ScopeType == data.ScopeType && a.Data.ScopeId == data.ScopeId
                && Overlaps(a.Data.ValidFrom ?? now, a.Data.ValidTo, from, data.ValidTo));
            if (twin != null)
            {
                row.AddError($"Trùng với dòng {twin.RowNumber} (cùng tài khoản, vai trò, phạm vi và thời hạn chồng lấn). "
                    + "Mỗi bản gán chỉ ghi một lần.");
                continue;
            }

            if (!row.HasErrors)
                accepted.Add(row);
        }
    }

    /// <inheritdoc />
    public async Task<ImportCommitResult> CommitAsync(IReadOnlyList<ImportRow<RoleAssignmentImportRow>> rows, CancellationToken ct)
    {
        foreach (var row in rows)
        {
            var d = row.Data;
            await _assignments.AssignAsync(d.UserId, d.RoleId, d.ScopeType!.Value, d.ScopeId, d.ValidFrom, d.ValidTo, d.Note, ct);
        }

        return new ImportCommitResult(rows.Count, 0);
    }

    #region Phân giải

    private void ResolveUser(ImportRow<RoleAssignmentImportRow> row, IReadOnlyDictionary<string, AssignmentUserEntry> users)
    {
        var data = row.Data;
        if (data.Username.Length == 0)
            return;
        if (!users.TryGetValue(data.Username.ToLowerInvariant(), out var user) || user.IsDeleted)
        {
            row.AddError($"Không có tài khoản \"{data.Username}\"{(user?.IsDeleted == true ? " (tài khoản đã bị xóa)" : string.Empty)}. "
                + "Hãy kiểm tra tên đăng nhập hoặc nhập cán bộ trước khi gán vai trò.");
            return;
        }

        if (_currentUser.UserId.HasValue && _currentUser.UserId.Value == user.Id)
        {
            row.AddError("Không thể tự gán vai trò cho chính mình (tài khoản đang nhập dữ liệu). "
                + "Hãy bỏ dòng này và nhờ một quản trị viên khác gán.");
            return;
        }

        data.UserId = user.Id;
    }

    private static void ResolveRole(ImportRow<RoleAssignmentImportRow> row, IReadOnlyDictionary<string, AssignmentRoleEntry> roles)
    {
        var data = row.Data;
        if (data.RoleName.Length == 0)
            return;
        if (!roles.TryGetValue(NormalizeName(data.RoleName), out var role))
        {
            row.AddError($"Không có vai trò tên \"{data.RoleName}\". Hãy chép đúng tên vai trò từ màn hình Quản lý vai trò.");
            return;
        }

        data.RoleName = role.Name;
        if (data.ScopeType is ScopeType.Department or ScopeType.PartyCell)
        {
            var globalOnly = role.PermissionCodes
                .Select(PermissionCodes.Find)
                .Where(d => d != null && !d.AppliesScope)
                .Select(d => d!.Name)
                .ToList();
            if (globalOnly.Count > 0)
            {
                row.AddError($"Vai trò \"{role.Name}\" có quyền chỉ áp dụng cho Toàn công ty ({string.Join(", ", globalOnly)}), "
                    + $"nên chỉ gán được với Loại phạm vi \"{ScopeGlobal}\". Hãy đổi Loại phạm vi hoặc chọn vai trò khác.");
                return;
            }
        }

        data.RoleId = role.Id;
    }

    private static void ResolveScope(
        ImportRow<RoleAssignmentImportRow> row,
        IReadOnlyDictionary<string, CatalogLookupEntry> departments,
        IReadOnlyDictionary<string, CatalogLookupEntry> partyCells)
    {
        var data = row.Data;
        if (data.ScopeType is not (ScopeType.Department or ScopeType.PartyCell) || data.ScopeCode == null)
            return;

        var (catalog, label) = data.ScopeType == ScopeType.Department ? (departments, "Phòng") : (partyCells, "Chi bộ");
        if (!catalog.TryGetValue(data.ScopeCode, out var entry) || entry.IsDeleted)
        {
            row.AddError($"Mã {label} \"{data.ScopeCode}\" không có trong danh mục. Hãy kiểm tra lại mã hoặc Loại phạm vi.");
            return;
        }
        if (!entry.IsActive)
        {
            row.AddError($"{label} \"{entry.Name}\" (mã {data.ScopeCode}) đã ngừng hoạt động nên không gán phạm vi được. Hãy chọn {label} khác.");
            return;
        }

        data.ScopeId = entry.Id;
    }

    #endregion

    #region Tiện ích

    private static DateOnly? ParseDate(string? text, string header, ICollection<string> errors)
    {
        if (text == null)
            return null;
        if (DateOnly.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        errors.Add($"\"{header}\" = \"{text}\" không phải ngày hợp lệ. Hãy nhập dạng dd/mm/yyyy, ví dụ 01/10/2026.");
        return null;
    }

    /// <summary>Đầu ngày <paramref name="date"/> theo giờ Việt Nam, đổi ra UTC.</summary>
    public static DateTime StartOfDayUtc(DateOnly date)
        => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), VietnamOffset).UtcDateTime;

    private static string ToLocalDate(DateTime utc)
        => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(VietnamOffset).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static bool Overlaps(DateTime aFrom, DateTime? aTo, DateTime bFrom, DateTime? bTo)
        => (aTo == null || bFrom < aTo.Value) && (bTo == null || aFrom < bTo.Value);

    /// <summary>Chuẩn hóa tên để so khớp: NFC, gộp khoảng trắng, chữ thường.</summary>
    private static string NormalizeName(string name)
        => string.Join(' ', name.Normalize(NormalizationForm.FormC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();

    private static Dictionary<string, CatalogLookupEntry> ToCatalog(IReadOnlyList<CatalogLookupEntry> entries)
        => entries.GroupBy(e => e.Code.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.IsDeleted).First(), StringComparer.Ordinal);

    #endregion
}
