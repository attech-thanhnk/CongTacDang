using System.Globalization;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Organization;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Imports.Definitions;

/// <summary>Một dòng import danh mục chức vụ.</summary>
public sealed class PositionImportRow
{
    /// <summary>Tên chức vụ.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Bên (null nếu sai giá trị — đã báo lỗi).</summary>
    public PositionSide? Side { get; set; }

    /// <summary>Mã thống kê đã chuẩn hóa; null = để trống (cập nhật: giữ nguyên).</summary>
    public string? StatCode { get; set; }

    /// <summary>Thẩm quyền mặc định; null = để trống (cập nhật: giữ nguyên).</summary>
    public ApprovalAuthority? DefaultApprovalAuthority { get; set; }

    /// <summary>Lãnh đạo, quản lý; null = để trống.</summary>
    public bool? IsLeadership { get; set; }

    /// <summary>Thứ tự hiển thị; null = để trống.</summary>
    public int? SortOrder { get; set; }

    /// <summary>Đang dùng; null = để trống.</summary>
    public bool? IsActive { get; set; }

    /// <summary>Id chức vụ đã có (cập nhật).</summary>
    public Guid? ExistingId { get; set; }
}

/// <summary>
/// Import danh mục chức vụ (<c>positions</c>): theo tên — tên đã có → cập nhật, chưa có → tạo mới.
/// Đổi thẩm quyền mặc định → tính lại thẩm quyền suy ra của cán bộ đang giữ chức vụ.
/// </summary>
public sealed class PositionImportDefinition : IImportDefinition<PositionImportRow>
{
    /// <summary>Khóa cột.</summary>
    public const string NameKey = "name", SideKey = "side", StatCodeKey = "statCode", AuthorityKey = "defaultApprovalAuthority",
        LeadershipKey = "isLeadership", SortOrderKey = "sortOrder", StatusKey = "status";

    /// <summary>Giá trị cột "Bên".</summary>
    public const string SideParty = "Đảng", SideAdministrative = "Chính quyền", SideMass = "Đoàn thể", SideOther = "Khác";

    /// <summary>Giá trị cột có/không và trạng thái.</summary>
    public const string Yes = "Có", No = "Không", StatusActive = "Đang dùng", StatusInactive = "Ngừng dùng";

    private readonly IOrgImportLookup _lookup;
    private readonly IPositionRepository _positions;
    private readonly IPositionService _service;

    /// <summary>Khởi tạo.</summary>
    public PositionImportDefinition(IOrgImportLookup lookup, IPositionRepository positions, IPositionService service)
    {
        _lookup = lookup;
        _positions = positions;
        _service = service;
    }

    /// <inheritdoc />
    public string Kind => "positions";

    /// <inheritdoc />
    public string DisplayName => "Danh mục chức vụ";

    /// <inheritdoc />
    public string Description => "Thêm mới hoặc cập nhật chức vụ theo tên (bên, mã thống kê Mẫu 15A/15B, thẩm quyền mặc định).";

    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = new[] { PermissionCodes.CatalogManage };

    /// <inheritdoc />
    public IReadOnlyList<ImportColumn> TemplateColumns { get; } = new[]
    {
        new ImportColumn(NameKey, "Tên chức vụ", true,
            $"Tối đa {PositionRules.MaxNameLength} ký tự; không phân biệt hoa thường. Tên đã có → cập nhật chức vụ đó.", null, "Trưởng phòng"),
        new ImportColumn(SideKey, "Bên", true, "Bên của chức vụ.", new[] { SideParty, SideAdministrative, SideMass, SideOther }, SideAdministrative),
        new ImportColumn(StatCodeKey, "Mã thống kê", false,
            "Mã chức danh Mẫu 15A/15B (M1…M26). Để trống khi cập nhật → giữ nguyên.", null, "M26"),
        new ImportColumn(AuthorityKey, "Thẩm quyền mặc định", false,
            "CoSo = Đảng ủy cơ sở quyết định xếp loại người giữ chức vụ; CapTren = cấp ủy cấp trên quyết định. Để trống khi cập nhật → giữ nguyên.",
            new[] { nameof(ApprovalAuthority.CoSo), nameof(ApprovalAuthority.CapTren) }, nameof(ApprovalAuthority.CoSo)),
        new ImportColumn(LeadershipKey, "Lãnh đạo, quản lý", false, "Chức vụ lãnh đạo, quản lý. Để trống: tạo mới = Không.", new[] { Yes, No }, Yes),
        new ImportColumn(SortOrderKey, "Thứ tự hiển thị", false, "Số nguyên ≥ 0, nhỏ đứng trước.", null, "10"),
        new ImportColumn(StatusKey, "Trạng thái", false, "Để trống: tạo mới = Đang dùng, cập nhật = giữ nguyên.",
            new[] { StatusActive, StatusInactive }, StatusActive)
    };

    /// <inheritdoc />
    public PositionImportRow ParseRow(ImportSourceRow source, ICollection<string> errors)
    {
        var row = new PositionImportRow
        {
            Name = source.Get(NameKey),
            Side = source.Get(SideKey) switch
            {
                SideParty => PositionSide.Party,
                SideAdministrative => PositionSide.Administrative,
                SideMass => PositionSide.MassOrganization,
                SideOther => PositionSide.Other,
                _ => null
            },
            IsLeadership = source.GetOrNull(LeadershipKey) switch { null => null, Yes => true, _ => false },
            IsActive = source.GetOrNull(StatusKey) switch { null => null, StatusActive => true, _ => false }
        };

        if (row.Name.Length > PositionRules.MaxNameLength)
            errors.Add($"Tên chức vụ dài quá {PositionRules.MaxNameLength} ký tự.");

        try
        {
            row.StatCode = PositionRules.NormalizeStatCode(source.GetOrNull(StatCodeKey));
        }
        catch (ArgumentException ex)
        {
            errors.Add(ex.Message);
        }

        if (source.GetOrNull(AuthorityKey) is { } authorityText
            && Enum.TryParse<ApprovalAuthority>(authorityText, ignoreCase: true, out var authority) && Enum.IsDefined(authority))
            row.DefaultApprovalAuthority = authority;

        if (source.GetOrNull(SortOrderKey) is { } sortText)
        {
            if (int.TryParse(sortText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sort) && sort >= 0)
                row.SortOrder = sort;
            else
                errors.Add($"\"Thứ tự hiển thị\" phải là số nguyên không âm (đang là \"{sortText}\").");
        }

        return row;
    }

    /// <inheritdoc />
    public async Task ValidateAsync(IReadOnlyList<ImportRow<PositionImportRow>> rows, CancellationToken ct)
    {
        var existing = (await _lookup.GetPositionsAsync(ct))
            .GroupBy(p => p.Name.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());
        var firstRow = new Dictionary<string, int>();
        foreach (var row in rows)
        {
            var key = row.Data.Name.Trim().ToLowerInvariant();
            if (key.Length == 0)
                continue;
            if (firstRow.TryGetValue(key, out var first))
            {
                row.AddError($"Chức vụ \"{row.Data.Name}\" trùng với dòng {first} trong tệp. Mỗi chức vụ chỉ được xuất hiện một lần.");
                continue;
            }
            firstRow[key] = row.RowNumber;

            if (existing.TryGetValue(key, out var entry))
            {
                row.Data.ExistingId = entry.Id;
                row.Action = ImportRowAction.Update;
            }
            else
            {
                row.Data.ExistingId = null;
                row.Action = ImportRowAction.Create;
            }
        }
    }

    /// <inheritdoc />
    public async Task<ImportCommitResult> CommitAsync(IReadOnlyList<ImportRow<PositionImportRow>> rows, CancellationToken ct)
    {
        int created = 0, updated = 0;
        foreach (var d in rows.Select(r => r.Data))
        {
            Position? entity = d.ExistingId.HasValue ? await _positions.FindPositionAsync(d.ExistingId.Value, ct) : null;
            var isNew = entity == null;
            if (isNew)
            {
                entity = new Position { Name = d.Name.Trim() };
                _positions.AddPosition(entity);
                created++;
            }
            else
            {
                updated++;
            }

            var target = entity!;
            target.Name = d.Name.Trim();
            if (d.Side.HasValue) target.Side = d.Side.Value;
            if (d.StatCode != null || isNew) target.StatCode = d.StatCode;
            if (d.DefaultApprovalAuthority.HasValue || isNew) target.DefaultApprovalAuthority = d.DefaultApprovalAuthority;
            if (d.IsLeadership.HasValue || isNew) target.IsLeadership = d.IsLeadership ?? false;
            if (d.SortOrder.HasValue || isNew) target.SortOrder = d.SortOrder ?? 0;
            if (d.IsActive.HasValue || isNew) target.IsActive = d.IsActive ?? true;
        }

        // Lưu trong transaction của khung rồi tính lại thẩm quyền suy ra (thẩm quyền mặc định có thể đã đổi).
        await _positions.SaveChangesAsync(ct);
        await _service.RecomputeAllApprovalAuthoritiesAsync(ct);
        return new ImportCommitResult(created, updated);
    }
}

/// <summary>Một dòng import chức vụ của cán bộ.</summary>
public sealed class MemberPositionImportRow
{
    /// <summary>Tên đăng nhập.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Tên chức vụ.</summary>
    public string PositionName { get; set; } = string.Empty;

    /// <summary>Mã đơn vị (chữ hoa) hoặc null.</summary>
    public string? UnitCode { get; set; }

    /// <summary>Chức vụ chính (true), kiêm nhiệm (false), để trống (null — tự chọn).</summary>
    public bool? IsPrimary { get; set; }

    /// <summary>Hiệu lực từ (UTC, đầu ngày giờ Việt Nam).</summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>Hiệu lực đến (UTC, không bao gồm = đầu ngày sau "Đến ngày").</summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; set; }

    /// <summary>Cán bộ đã phân giải.</summary>
    public Guid UserId { get; set; }

    /// <summary>Chức vụ đã phân giải.</summary>
    public Guid PositionId { get; set; }

    /// <summary>Tổ chức Đảng đã phân giải.</summary>
    public Guid? PartyCellId { get; set; }

    /// <summary>Đơn vị chính quyền đã phân giải.</summary>
    public Guid? DepartmentId { get; set; }
}

/// <summary>
/// Import chức vụ của cán bộ (<c>member-positions</c>): mỗi dòng thêm một chức vụ (chính hoặc kiêm nhiệm) có thời hạn cho một cán bộ.
/// Ghi qua <see cref="IPositionService"/> (kiểm tra quyền quản lý tài khoản trong phạm vi, tính lại thẩm quyền suy ra).
/// </summary>
public sealed class MemberPositionImportDefinition : IImportDefinition<MemberPositionImportRow>
{
    /// <summary>Khóa cột.</summary>
    public const string UsernameKey = "username", PositionKey = "position", UnitKey = "unitCode", KindKey = "kind",
        ValidFromKey = "validFrom", ValidToKey = "validTo", NoteKey = "note";

    /// <summary>Giá trị cột "Chính/kiêm nhiệm".</summary>
    public const string KindPrimary = "Chính", KindConcurrent = "Kiêm nhiệm";

    private const int MaxNoteLength = 1000;
    private static readonly string[] DateFormats = { "d/M/yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "d-M-yyyy", "d.M.yyyy" };

    private readonly IOrgImportLookup _lookup;
    private readonly IImportLookup _catalog;
    private readonly IPositionService _service;
    private readonly IAuthorizationGuard _guard;

    /// <summary>Khởi tạo.</summary>
    public MemberPositionImportDefinition(IOrgImportLookup lookup, IImportLookup catalog, IPositionService service, IAuthorizationGuard guard)
    {
        _lookup = lookup;
        _catalog = catalog;
        _service = service;
        _guard = guard;
    }

    /// <inheritdoc />
    public string Kind => "member-positions";

    /// <inheritdoc />
    public string DisplayName => "Chức vụ của cán bộ";

    /// <inheritdoc />
    public string Description => "Thêm chức vụ (chính hoặc kiêm nhiệm, có thời hạn) cho cán bộ đã có tài khoản; "
        + "thẩm quyền phê duyệt được suy ra lại theo chức vụ.";

    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = new[] { PermissionCodes.SystemUsersManage };

    /// <inheritdoc />
    public IReadOnlyList<ImportColumn> TemplateColumns { get; } = new[]
    {
        new ImportColumn(UsernameKey, "Tên đăng nhập", true, "Tài khoản cán bộ đã có (nhập cán bộ trước).", null, "nguyenvana"),
        new ImportColumn(PositionKey, "Chức vụ", true, "Tên chức vụ trong danh mục chức vụ (đang dùng).", null, "Trưởng phòng"),
        new ImportColumn(UnitKey, "Mã đơn vị", false,
            "Nơi giữ chức vụ: mã tổ chức Đảng với chức vụ Đảng, mã đơn vị chính quyền với chức vụ chính quyền "
            + "(chức vụ đoàn thể/khác: tìm đơn vị chính quyền trước, rồi tổ chức Đảng).", null, "PH-KH"),
        new ImportColumn(KindKey, "Chính/kiêm nhiệm", false,
            "Chính = chức vụ chính (chức danh hiển thị mặc định); để trống → là chức vụ chính nếu cán bộ chưa có chức vụ chính.",
            new[] { KindPrimary, KindConcurrent }, KindPrimary),
        new ImportColumn(ValidFromKey, "Từ ngày", false, "dd/mm/yyyy; để trống = hôm nay.", null, "01/07/2026"),
        new ImportColumn(ValidToKey, "Đến ngày", false, "dd/mm/yyyy (hết ngày này thì thôi giữ chức vụ); để trống = không thời hạn.", null, ""),
        new ImportColumn(NoteKey, "Ghi chú", false, $"Số quyết định bổ nhiệm… Tối đa {MaxNoteLength} ký tự.", null, "QĐ 123/QĐ-ATTECH")
    };

    /// <inheritdoc />
    public MemberPositionImportRow ParseRow(ImportSourceRow source, ICollection<string> errors)
    {
        var row = new MemberPositionImportRow
        {
            Username = source.Get(UsernameKey),
            PositionName = source.Get(PositionKey),
            UnitCode = source.GetOrNull(UnitKey) is { } unit ? CatalogRules.NormalizeCode(unit) : null,
            IsPrimary = source.GetOrNull(KindKey) switch { null => null, KindPrimary => true, _ => false },
            Note = source.GetOrNull(NoteKey)
        };

        var from = ParseDate(source.GetOrNull(ValidFromKey), "Từ ngày", errors);
        var to = ParseDate(source.GetOrNull(ValidToKey), "Đến ngày", errors);
        if (from.HasValue)
            row.ValidFrom = RoleAssignmentImportDefinition.StartOfDayUtc(from.Value);
        if (to.HasValue)
            row.ValidTo = RoleAssignmentImportDefinition.StartOfDayUtc(to.Value.AddDays(1));
        if (from.HasValue && to.HasValue && to.Value < from.Value)
            errors.Add("\"Đến ngày\" phải bằng hoặc sau \"Từ ngày\". Hãy sửa thời hạn.");
        if (row.Note != null && row.Note.Length > MaxNoteLength)
            errors.Add($"Ghi chú dài quá {MaxNoteLength} ký tự. Hãy rút ngắn.");
        return row;
    }

    /// <inheritdoc />
    public async Task ValidateAsync(IReadOnlyList<ImportRow<MemberPositionImportRow>> rows, CancellationToken ct)
    {
        var usernames = rows.Select(r => r.Data.Username.ToLowerInvariant()).Where(u => u.Length > 0).Distinct().ToList();
        var members = (await _lookup.FindMembersAsync(usernames, ct))
            .GroupBy(m => m.Username.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.IsDeleted).First(), StringComparer.Ordinal);
        var positions = (await _lookup.GetPositionsAsync(ct))
            .GroupBy(p => p.Name.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var departments = ToCatalog(await _catalog.GetDepartmentsAsync(ct));
        var cells = ToCatalog(await _catalog.GetPartyCellsAsync(ct));

        foreach (var row in rows)
        {
            var d = row.Data;
            row.Action = ImportRowAction.Create;

            if (d.Username.Length > 0)
            {
                if (!members.TryGetValue(d.Username.ToLowerInvariant(), out var member) || member.IsDeleted)
                    row.AddError($"Không có tài khoản \"{d.Username}\"{(member?.IsDeleted == true ? " (tài khoản đã bị xóa)" : string.Empty)}. "
                        + "Hãy kiểm tra tên đăng nhập hoặc nhập cán bộ trước.");
                else if (!_guard.Can(PermissionCodes.SystemUsersManage, new AccessTarget(DepartmentId: member.DepartmentId, PartyCellId: member.PartyCellId)))
                    row.AddError($"Bạn không có quyền \"{PermissionCodes.DisplayName(PermissionCodes.SystemUsersManage)}\" đối với cán bộ "
                        + $"\"{member.FullName}\" (ngoài phạm vi được gán).");
                else
                    d.UserId = member.Id;
            }

            PositionLookupEntry? position = null;
            if (d.PositionName.Length > 0)
            {
                if (!positions.TryGetValue(d.PositionName.Trim().ToLowerInvariant(), out position))
                    row.AddError($"Chức vụ \"{d.PositionName}\" không có trong danh mục chức vụ. Hãy thêm chức vụ vào danh mục trước hoặc sửa tên.");
                else if (!position.IsActive)
                    row.AddError($"Chức vụ \"{position.Name}\" đã ngừng dùng. Hãy chọn chức vụ khác.");
                else
                    d.PositionId = position.Id;
            }

            if (d.UnitCode != null && position != null)
                ResolveUnit(row, position, departments, cells);
        }
    }

    /// <inheritdoc />
    public async Task<ImportCommitResult> CommitAsync(IReadOnlyList<ImportRow<MemberPositionImportRow>> rows, CancellationToken ct)
    {
        foreach (var d in rows.Select(r => r.Data))
        {
            await _service.AddMemberPositionAsync(d.UserId, new SaveMemberPositionDto
            {
                PositionId = d.PositionId,
                PartyCellId = d.PartyCellId,
                DepartmentId = d.DepartmentId,
                IsPrimary = d.IsPrimary,
                ValidFrom = d.ValidFrom,
                ValidTo = d.ValidTo,
                Note = d.Note
            }, ct);
        }

        return new ImportCommitResult(rows.Count, 0);
    }

    private static void ResolveUnit(ImportRow<MemberPositionImportRow> row, PositionLookupEntry position,
        IReadOnlyDictionary<string, CatalogLookupEntry> departments, IReadOnlyDictionary<string, CatalogLookupEntry> cells)
    {
        var d = row.Data;
        var code = d.UnitCode!;
        CatalogLookupEntry? entry;
        bool isCell;
        switch (position.Side)
        {
            case PositionSide.Party:
                isCell = true;
                cells.TryGetValue(code, out entry);
                break;
            case PositionSide.Administrative:
                isCell = false;
                departments.TryGetValue(code, out entry);
                break;
            default:
                isCell = !departments.TryGetValue(code, out entry);
                if (isCell)
                    cells.TryGetValue(code, out entry);
                break;
        }

        var label = isCell ? "tổ chức Đảng" : "đơn vị chính quyền";
        if (entry == null || entry.IsDeleted)
        {
            row.AddError($"Mã {label} \"{code}\" không có trong danh mục (chức vụ \"{position.Name}\" giữ tại {label}). Hãy kiểm tra lại mã.");
            return;
        }
        if (!entry.IsActive)
        {
            row.AddError($"{(isCell ? "Tổ chức Đảng" : "Đơn vị")} \"{entry.Name}\" (mã {code}) đã ngừng hoạt động. Hãy chọn đơn vị khác.");
            return;
        }

        if (isCell)
            d.PartyCellId = entry.Id;
        else
            d.DepartmentId = entry.Id;
    }

    private static Dictionary<string, CatalogLookupEntry> ToCatalog(IReadOnlyList<CatalogLookupEntry> entries)
        => entries.GroupBy(e => e.Code.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.IsDeleted).First(), StringComparer.Ordinal);

    private static DateOnly? ParseDate(string? text, string header, ICollection<string> errors)
    {
        if (text == null)
            return null;
        if (DateOnly.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        errors.Add($"\"{header}\" = \"{text}\" không phải ngày hợp lệ. Hãy nhập dạng dd/mm/yyyy, ví dụ 01/10/2026.");
        return null;
    }
}
