using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Accounts;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Imports.Definitions;

/// <summary>Một dòng nhập bản gán vai trò.</summary>
public sealed class RoleAssignmentImportRow
{
    /// <summary>Tên đăng nhập (chuẩn hóa chữ thường).</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>Mã hoặc tên vai trò như trong file.</summary>
    public string RoleText { get; init; } = string.Empty;

    /// <summary>Loại phạm vi.</summary>
    public RoleScopeType ScopeType { get; init; }

    /// <summary>Mã đơn vị / tổ chức Đảng (chuẩn hóa chữ hoa).</summary>
    public string ScopeCode { get; init; } = string.Empty;

    /// <summary>Hiệu lực từ (UTC); null = ngay khi nhập.</summary>
    public DateTime? ValidFrom { get; init; }

    /// <summary>Hiệu lực đến (UTC, không bao gồm = 00:00 ngày sau "Đến ngày"); null = không thời hạn.</summary>
    public DateTime? ValidTo { get; init; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; init; }

    /// <summary>Id người được gán.</summary>
    public Guid UserId { get; set; }

    /// <summary>Id vai trò.</summary>
    public Guid RoleId { get; set; }

    /// <summary>Id đơn vị / tổ chức Đảng của phạm vi.</summary>
    public Guid? ScopeId { get; set; }
}

/// <summary>
/// Nhập bản gán vai trò (loại <c>role-assignments</c>): người, vai trò, phạm vi Toàn công ty / Đơn vị / Tổ chức Đảng, thời hạn.
/// Kiểm tra trước: người/vai trò/phạm vi tồn tại, vai trò có quyền chỉ áp dụng toàn công ty thì chỉ gán Toàn công ty, không tự gán
/// cho chính người đang nhập, trùng bản gán (trong file hoặc với bản gán đang/sắp hiệu lực). Ghi qua <see cref="IRoleAssignmentService"/>.
/// </summary>
public sealed class RoleAssignmentImportDefinition : ImportDefinition<RoleAssignmentImportRow>
{
    /// <summary>Khóa cột.</summary>
    public const string UsernameKey = "username", RoleKey = "role", ScopeTypeKey = "scopeType", ScopeCodeKey = "scopeCode",
        ValidFromKey = "validFrom", ValidToKey = "validTo", NoteKey = "note";

    /// <summary>Nhãn loại phạm vi trong file.</summary>
    public const string GlobalLabel = "Toàn công ty", DepartmentLabel = "Đơn vị", PartyCellLabel = "Tổ chức Đảng";

    private const int MaxNoteLength = 1000;

    private readonly IImportLookup _lookup;
    private readonly IRoleAssignmentService _assignments;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _time;

    /// <summary>Khởi tạo.</summary>
    public RoleAssignmentImportDefinition(IImportLookup lookup, IRoleAssignmentService assignments, ICurrentUserService currentUser,
        TimeProvider? time = null)
    {
        _lookup = lookup;
        _assignments = assignments;
        _currentUser = currentUser;
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public override string Kind => "role-assignments";

    /// <inheritdoc />
    public override string Name => "Gán vai trò";

    /// <inheritdoc />
    public override string Description => "Gán vai trò cho tài khoản đã có, kèm phạm vi và thời hạn. Mỗi dòng tạo một bản gán mới.";

    /// <inheritdoc />
    public override int Order => 40;

    /// <inheritdoc />
    public override IReadOnlyList<string> RequiredPermissions { get; } = new[] { PermissionCodes.SystemAssignmentsManage };

    /// <inheritdoc />
    public override IReadOnlyList<ImportColumn> Columns { get; } = new ImportColumn[]
    {
        new(UsernameKey, "Tên đăng nhập", true, "Tên đăng nhập của tài khoản đã có.", Example: "nguyen.van.a"),
        new(RoleKey, "Vai trò", true, "Tên hoặc mã vai trò.", Example: "Lãnh đạo Phòng"),
        new(ScopeTypeKey, "Loại phạm vi", true, "Toàn công ty, Đơn vị hoặc Tổ chức Đảng. Phạm vi gồm cả các đơn vị cấp dưới.",
            new[] { GlobalLabel, DepartmentLabel, PartyCellLabel }, Example: DepartmentLabel),
        new(ScopeCodeKey, "Mã đơn vị/tổ chức Đảng", false, "Bắt buộc khi phạm vi là Đơn vị hoặc Tổ chức Đảng; để trống khi Toàn công ty.", Example: "PH-KT"),
        new(ValidFromKey, "Từ ngày", false, "Dạng dd/MM/yyyy. Để trống: hiệu lực ngay.", Example: "01/10/2026"),
        new(ValidToKey, "Đến ngày", false, "Dạng dd/MM/yyyy, còn hiệu lực hết ngày này. Để trống: không thời hạn.", Example: "31/12/2026"),
        new(NoteKey, "Ghi chú", false, "Số quyết định… (tùy chọn).")
    };

    /// <inheritdoc />
    public override async Task<IReadOnlyList<ImportColumn>> GetTemplateColumnsAsync(CancellationToken ct = default)
    {
        var roles = (await _lookup.GetRolesAsync(ct)).Select(r => r.Name).ToList();
        return Columns.Select(c => c.Key == RoleKey ? c.WithAllowedValues(roles) : c).ToList();
    }

    /// <summary>Đọc loại phạm vi (không phân biệt hoa thường, không dấu; nhận cả "Global/Department/PartyCell").</summary>
    public static RoleScopeType? ParseScopeType(string value) => ImportValues.Fold(value) switch
    {
        "toan cong ty" or "global" => RoleScopeType.Global,
        "don vi" or "don vi chinh quyen" or "department" => RoleScopeType.Department,
        "to chuc dang" or "partycell" => RoleScopeType.PartyCell,
        _ => null
    };

    /// <inheritdoc />
    protected override RoleAssignmentImportRow? ParseRow(ImportRawRow raw, ImportRowResult result)
    {
        var usernameText = raw.Get(UsernameKey);
        var roleText = raw.Get(RoleKey).Trim();
        var scopeText = raw.Get(ScopeTypeKey);
        var scopeType = ParseScopeType(scopeText);
        if (scopeText.Length > 0 && scopeType == null)
            result.AddError($"Loại phạm vi \"{scopeText}\" không hợp lệ: chọn {GlobalLabel}, {DepartmentLabel} hoặc {PartyCellLabel}.");

        var scopeCode = CatalogRules.NormalizeCode(raw.Get(ScopeCodeKey));
        if (scopeType == RoleScopeType.Global && scopeCode.Length > 0)
            result.AddError("Phạm vi Toàn công ty không kèm mã đơn vị/tổ chức Đảng. Hãy để trống cột mã.");
        if (scopeType is RoleScopeType.Department or RoleScopeType.PartyCell && scopeCode.Length == 0)
            result.AddError($"Phạm vi {(scopeType == RoleScopeType.Department ? DepartmentLabel : PartyCellLabel)} cần mã đơn vị/tổ chức Đảng.");

        var from = ImportValues.ParseDate(raw.Get(ValidFromKey), out var fromOk);
        if (!fromOk)
            result.AddError($"Từ ngày \"{raw.Get(ValidFromKey)}\" không đúng dạng dd/MM/yyyy.");
        var to = ImportValues.ParseDate(raw.Get(ValidToKey), out var toOk);
        if (!toOk)
            result.AddError($"Đến ngày \"{raw.Get(ValidToKey)}\" không đúng dạng dd/MM/yyyy.");
        if (from.HasValue && to.HasValue && to.Value < from.Value)
            result.AddError("Đến ngày phải không sớm hơn Từ ngày.");

        var note = raw.Get(NoteKey).Trim();
        if (note.Length > MaxNoteLength)
            result.AddError($"Ghi chú tối đa {MaxNoteLength} ký tự.");

        string? username = null;
        if (usernameText.Length > 0)
        {
            try
            {
                username = AccountRules.NormalizeAndValidateUsername(usernameText);
            }
            catch (ValidationException ex)
            {
                result.AddError(ex.Message);
            }
        }

        if (username == null || roleText.Length == 0 || scopeType == null)
            return null;
        return new RoleAssignmentImportRow
        {
            Username = username,
            RoleText = roleText,
            ScopeType = scopeType.Value,
            ScopeCode = scopeCode,
            ValidFrom = from.HasValue ? ImportValues.StartOfDayUtc(from.Value) : null,
            ValidTo = to.HasValue ? ImportValues.StartOfDayUtc(to.Value.AddDays(1)) : null,
            Note = note.Length == 0 ? null : note
        };
    }

    /// <inheritdoc />
    protected override async Task ValidateAsync(IReadOnlyList<ParsedImportRow<RoleAssignmentImportRow>> rows, CancellationToken ct)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var users = (await _lookup.FindUsersAsync(rows.Select(r => r.Data.Username).Distinct().ToList(), now, ct))
            .Where(u => !u.IsDeleted)
            .ToDictionary(u => u.Username, StringComparer.OrdinalIgnoreCase);
        var roles = await _lookup.GetRolesAsync(ct);
        var departments = ByCode(await _lookup.GetDepartmentsAsync(ct));
        var cells = ByCode(await _lookup.GetPartyCellsAsync(ct));
        var existing = await _lookup.GetCurrentOrFutureAssignmentsAsync(users.Values.Select(u => u.Id).ToList(), now, ct);
        var accepted = new List<(ParsedImportRow<RoleAssignmentImportRow> Row, DateTime From)>();

        foreach (var row in rows)
        {
            var data = row.Data;
            row.Result.Action = ImportRowAction.Create;

            if (!users.TryGetValue(data.Username, out var user))
                row.Error($"Không có tài khoản \"{data.Username}\" (hoặc tài khoản đã bị xóa).");
            else if (_currentUser.UserId == user.Id)
                row.Error("Không tự gán vai trò cho chính mình. Hãy nhờ một quản trị viên khác.");
            else
                data.UserId = user.Id;

            var role = roles.FirstOrDefault(r => string.Equals(r.Code, data.RoleText, StringComparison.OrdinalIgnoreCase))
                ?? roles.FirstOrDefault(r => ImportValues.SameText(r.Name, data.RoleText))
                ?? roles.FirstOrDefault(r => ImportValues.Fold(r.Name) == ImportValues.Fold(data.RoleText));
            if (role == null)
            {
                row.Error($"Không có vai trò \"{data.RoleText}\".");
            }
            else
            {
                data.RoleId = role.Id;
                if (data.ScopeType != RoleScopeType.Global)
                {
                    var globalOnly = role.PermissionCodes.Select(PermissionCodes.Find).Where(d => d is { AppliesScope: false }).Select(d => d!.Name).ToList();
                    if (globalOnly.Count > 0)
                        row.Error($"Vai trò \"{role.Name}\" có quyền chỉ áp dụng Toàn công ty ({string.Join(", ", globalOnly)}), chỉ gán được phạm vi Toàn công ty.");
                }
            }

            if (data.ScopeType != RoleScopeType.Global && data.ScopeCode.Length > 0)
            {
                var (units, label) = data.ScopeType == RoleScopeType.Department ? (departments, "đơn vị") : (cells, "tổ chức Đảng");
                if (!units.TryGetValue(data.ScopeCode, out var unit))
                    row.Error($"Mã {label} \"{data.ScopeCode}\" không có trong danh mục.");
                else if (!unit.IsActive)
                    row.Error($"Phạm vi \"{unit.Name}\" ({unit.Code}) đã ngừng hoạt động.");
                else
                    data.ScopeId = unit.Id;
            }

            var from = data.ValidFrom ?? now;
            if (data.ValidTo.HasValue && data.ValidTo.Value <= from)
                row.Error("Đến ngày đã qua hoặc trước Từ ngày — bản gán sẽ không có hiệu lực.");

            if (!row.IsValid)
                continue;

            var overlap = existing.FirstOrDefault(a => a.UserId == data.UserId && a.RoleId == data.RoleId && a.ScopeType == data.ScopeType
                && a.ScopeId == data.ScopeId && Overlaps(a.ValidFrom, a.ValidTo, from, data.ValidTo));
            if (overlap != null)
            {
                row.Error($"Đã có bản gán cùng người, vai trò, phạm vi còn hiệu lực trùng thời gian (từ {ToLocalDate(overlap.ValidFrom)}"
                    + (overlap.ValidTo.HasValue ? $" đến hết {ToLocalDate(overlap.ValidTo.Value.AddDays(-1))}" : string.Empty) + ").");
                continue;
            }

            var duplicate = accepted.FirstOrDefault(a => a.Row.Data.UserId == data.UserId && a.Row.Data.RoleId == data.RoleId
                && a.Row.Data.ScopeType == data.ScopeType && a.Row.Data.ScopeId == data.ScopeId
                && Overlaps(a.From, a.Row.Data.ValidTo, from, data.ValidTo));
            if (duplicate.Row != null)
            {
                row.Error($"Trùng bản gán ở dòng {duplicate.Row.RowNumber} (cùng người, vai trò, phạm vi, thời gian chồng lấn).");
                continue;
            }
            accepted.Add((row, from));
        }
    }

    /// <inheritdoc />
    protected override async Task<ImportCommitOutcome> CommitRowsAsync(IReadOnlyList<ParsedImportRow<RoleAssignmentImportRow>> rows, CancellationToken ct)
    {
        var outcome = new ImportCommitOutcome();
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            var data = row.Data;
            await RunRowAsync(row.RowNumber, () => _assignments.AssignAsync(
                data.UserId, data.RoleId, (ScopeType)(int)data.ScopeType, data.ScopeId, data.ValidFrom, data.ValidTo, data.Note, ct));
            outcome.Created++;
        }
        return outcome;
    }

    private static bool Overlaps(DateTime aFrom, DateTime? aTo, DateTime bFrom, DateTime? bTo)
        => (aTo == null || bFrom < aTo.Value) && (bTo == null || aFrom < bTo.Value);

    private static string ToLocalDate(DateTime utc) => utc.Add(ImportValues.VietnamOffset).ToString("dd/MM/yyyy");

    private static Dictionary<string, OrgUnitRef> ByCode(IReadOnlyList<OrgUnitRef> units)
        => units.Where(u => !u.IsDeleted)
            .GroupBy(u => u.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
}
