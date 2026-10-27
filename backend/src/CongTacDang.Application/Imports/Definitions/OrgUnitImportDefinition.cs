using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Organization;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Imports.Definitions;

/// <summary>Một dòng nhập đơn vị chính quyền / tổ chức Đảng.</summary>
public sealed class OrgUnitImportRow
{
    /// <summary>Mã (đã chuẩn hóa chữ hoa).</summary>
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>Tên viết tắt; null = để trống / giữ nguyên.</summary>
    public string? ShortName { get; init; }

    /// <summary>Mã cấp trên (đã chuẩn hóa); rỗng = gốc (tạo mới) hoặc giữ nguyên (cập nhật).</summary>
    public string ParentCode { get; init; } = string.Empty;

    /// <summary>Tên loại đơn vị như trong file.</summary>
    public string UnitTypeName { get; init; } = string.Empty;

    /// <summary>Thứ tự hiển thị; null = mặc định / giữ nguyên.</summary>
    public int? SortOrder { get; init; }

    /// <summary>Mô tả; null = trống / giữ nguyên.</summary>
    public string? Description { get; init; }

    /// <summary>Id đơn vị đã có (cập nhật).</summary>
    public Guid? ExistingId { get; set; }

    public Guid? UnitTypeId { get; set; }
}

/// <summary>
/// Nhập cây tổ chức theo mã (đã có → cập nhật, chưa có → tạo mới), ghi qua <see cref="IOrganizationService"/>.
/// Cấp trên trống: tạo mới → đơn vị gốc; cập nhật → giữ nguyên.
/// </summary>
public abstract class OrgUnitImportDefinition : ImportDefinition<OrgUnitImportRow>
{
    public const string CodeKey = "code";

    public const string NameKey = "name";

    public const string ShortNameKey = "shortName";

    public const string ParentKey = "parentCode";

    public const string UnitTypeKey = "unitType";

    public const string SortOrderKey = "sortOrder";

    public const string DescriptionKey = "description";

    private readonly IImportLookup _lookup;

    protected OrgUnitImportDefinition(IImportLookup lookup) => _lookup = lookup;

    protected abstract OrgSide Side { get; }

    /// <summary>Nhãn (ví dụ "đơn vị", "tổ chức Đảng").</summary>
    protected abstract string Label { get; }

    /// <summary>Danh sách đơn vị hiện có (kể cả đã xóa).</summary>
    protected abstract Task<IReadOnlyList<OrgUnitRef>> LoadUnitsAsync(IImportLookup lookup, CancellationToken ct);

    protected abstract Task<Guid> CreateUnitAsync(SaveCatalogItemDto input);

    protected abstract Task UpdateUnitAsync(Guid id, SaveCatalogItemDto input);

    public override IReadOnlyList<string> RequiredPermissions { get; } = new[] { PermissionCodes.CatalogManage };

    public override async Task<IReadOnlyList<ImportColumn>> GetTemplateColumnsAsync(CancellationToken ct = default)
    {
        var types = (await _lookup.GetUnitTypesAsync(ct)).Where(t => t.Side == Side && t.IsActive).Select(t => t.Name).ToList();
        return Columns.Select(c => c.Key == UnitTypeKey ? c.WithAllowedValues(types) : c).ToList();
    }

    protected override OrgUnitImportRow? ParseRow(ImportRawRow raw, ImportRowResult result)
    {
        var code = CatalogRules.NormalizeCode(raw.Get(CodeKey));
        var name = raw.Get(NameKey).Trim();
        if (code.Length > 0 && CatalogRules.ValidateCode(code, Label) is { } codeError)
            result.AddError(codeError);
        if (name.Length > 0 && CatalogRules.ValidateName(name, Label) is { } nameError)
            result.AddError(nameError);

        var shortName = CatalogRules.NormalizeShortName(raw.Get(ShortNameKey));
        if (CatalogRules.ValidateShortName(shortName, Label) is { } shortNameError)
            result.AddError(shortNameError);

        var parent = CatalogRules.NormalizeCode(raw.Get(ParentKey));
        var sortOrder = ImportValues.ParseInt(raw.Get(SortOrderKey), out var sortOk);
        if (!sortOk)
            result.AddError($"Thứ tự \"{raw.Get(SortOrderKey)}\" không phải số nguyên.");

        if (code.Length == 0)
            return null;
        var description = raw.Get(DescriptionKey).Trim();
        return new OrgUnitImportRow
        {
            Code = code,
            Name = name,
            ShortName = shortName,
            ParentCode = parent,
            UnitTypeName = raw.Get(UnitTypeKey).Trim(),
            SortOrder = sortOrder,
            Description = description.Length == 0 ? null : description
        };
    }

    protected override async Task ValidateAsync(IReadOnlyList<ParsedImportRow<OrgUnitImportRow>> rows, CancellationToken ct)
    {
        var units = await LoadUnitsAsync(_lookup, ct);
        var types = (await _lookup.GetUnitTypesAsync(ct)).Where(t => t.Side == Side).ToList();
        var byCode = units.GroupBy(u => u.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(u => u.IsDeleted).First(), StringComparer.OrdinalIgnoreCase);
        var fileCodes = rows.Select(r => r.Data.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        MarkDuplicates(rows, r => r.Code, $"Mã {Label}");

        var existingRoot = units.Where(u => !u.IsDeleted && u.ParentId == null).OrderBy(u => u.Code, StringComparer.Ordinal).FirstOrDefault();
        int? newRootRow = null;

        foreach (var row in rows)
        {
            var data = row.Data;
            byCode.TryGetValue(data.Code, out var existing);
            if (existing is { IsDeleted: true })
            {
                row.Error($"Mã {Label} \"{data.Code}\" đã dùng cho {Label} đã xóa \"{existing.Name}\". Hãy dùng mã khác.");
            }
            else if (existing != null)
            {
                data.ExistingId = existing.Id;
                row.Result.Action = ImportRowAction.Update;
            }
            else
            {
                row.Result.Action = ImportRowAction.Create;
            }

            // Loại đơn vị.
            if (data.UnitTypeName.Length > 0)
            {
                var type = types.FirstOrDefault(t => ImportValues.SameText(t.Name, data.UnitTypeName))
                    ?? types.FirstOrDefault(t => ImportValues.Fold(t.Name) == ImportValues.Fold(data.UnitTypeName));
                if (type == null)
                {
                    row.Error($"Loại \"{data.UnitTypeName}\" không có trong danh mục loại đơn vị bên {CatalogRules.SideName(Side)}. "
                        + $"Hãy chọn một trong: {string.Join(", ", types.Where(t => t.IsActive).Select(t => t.Name))}.");
                }
                else if (!type.IsActive && existing?.UnitTypeId != type.Id)
                {
                    row.Error($"Loại \"{type.Name}\" đã ngừng dùng. Hãy chọn loại khác.");
                }
                else
                {
                    data.UnitTypeId = type.Id;
                }
            }

            // Cấp trên.
            if (data.ParentCode.Length == 0)
            {
                if (existing == null)
                {
                    if (existingRoot != null)
                        row.Error($"Hãy nhập mã cấp trên: cây {Label} đã có gốc \"{existingRoot.Name}\" ({existingRoot.Code}), mỗi cây chỉ có một gốc.");
                    else if (newRootRow.HasValue)
                        row.Error($"Hãy nhập mã cấp trên: dòng {newRootRow} đã là {Label} gốc, mỗi cây chỉ có một gốc.");
                    else
                        newRootRow = row.RowNumber;
                }
            }
            else if (string.Equals(data.ParentCode, data.Code, StringComparison.OrdinalIgnoreCase))
            {
                row.Error($"Mã cấp trên không được trùng mã của chính {Label}.");
            }
            else if (!fileCodes.Contains(data.ParentCode))
            {
                if (!byCode.TryGetValue(data.ParentCode, out var parent))
                    row.Error($"Mã cấp trên \"{data.ParentCode}\" không có trong danh mục và không có trong file.");
                else if (parent.IsDeleted)
                    row.Error($"Cấp trên \"{parent.Name}\" ({parent.Code}) đã bị xóa. Hãy chọn cấp trên khác.");
            }
        }

        CheckCycles(rows, units);
    }

    /// <summary>Dựng cây sau khi nhập (dữ liệu hiện có + thay đổi trong file) để phát hiện quan hệ cấp trên tạo vòng.</summary>
    private void CheckCycles(IReadOnlyList<ParsedImportRow<OrgUnitImportRow>> rows, IReadOnlyList<OrgUnitRef> units)
    {
        var ids = units.Where(u => !u.IsDeleted).ToDictionary(u => u.Code, u => u.Id, StringComparer.OrdinalIgnoreCase);
        var parents = units.Where(u => !u.IsDeleted).ToDictionary(u => u.Id, u => u.ParentId);
        var rowById = new Dictionary<Guid, ParsedImportRow<OrgUnitImportRow>>();
        foreach (var row in rows)
        {
            if (row.Data.ExistingId is { } existingId)
            {
                rowById.TryAdd(existingId, row);
                continue;
            }
            if (ids.ContainsKey(row.Data.Code))
                continue; // mã đã xóa hoặc trùng — đã báo lỗi
            var tempId = Guid.NewGuid();
            ids[row.Data.Code] = tempId;
            parents[tempId] = null;
            rowById[tempId] = row;
        }

        foreach (var (id, row) in rowById)
        {
            if (row.Data.ParentCode.Length > 0 && ids.TryGetValue(row.Data.ParentCode, out var parentId))
                parents[id] = parentId;
        }

        var result = OrgTree.BuildPaths(parents.Select(p => new OrgNodeLink(p.Key, p.Value)));
        foreach (var id in result.CycleNodes)
        {
            if (rowById.TryGetValue(id, out var row))
                row.Error($"Quan hệ cấp trên tạo thành vòng (cấp trên \"{row.Data.ParentCode}\" đang là {Label} cấp dưới của \"{row.Data.Code}\").");
        }
    }

    protected override async Task<ImportCommitOutcome> CommitRowsAsync(IReadOnlyList<ParsedImportRow<OrgUnitImportRow>> rows, CancellationToken ct)
    {
        var outcome = new ImportCommitOutcome();
        var units = await LoadUnitsAsync(_lookup, ct);
        var ids = units.Where(u => !u.IsDeleted).ToDictionary(u => u.Code, u => u.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var row in OrderParentsFirst(rows))
        {
            ct.ThrowIfCancellationRequested();
            var data = row.Data;
            Guid? parentId = data.ParentCode.Length == 0 ? null : ids[data.ParentCode];
            var input = new SaveCatalogItemDto
            {
                Name = data.Name,
                ShortName = data.ShortName,
                Description = data.Description,
                SortOrder = data.SortOrder,
                UnitTypeId = data.UnitTypeId,
                ParentId = parentId
            };

            if (data.ExistingId is { } existingId)
            {
                await RunRowAsync(row.RowNumber, () => UpdateUnitAsync(existingId, input));
                outcome.Updated++;
            }
            else
            {
                input.Code = data.Code;
                ids[data.Code] = await RunRowAsync(row.RowNumber, () => CreateUnitAsync(input));
                outcome.Created++;
            }
        }
        return outcome;
    }

    /// <summary>Sắp các dòng để cấp trên có trong file được ghi trước cấp dưới.</summary>
    public static List<ParsedImportRow<OrgUnitImportRow>> OrderParentsFirst(IReadOnlyList<ParsedImportRow<OrgUnitImportRow>> rows)
    {
        var byCode = rows.GroupBy(r => r.Data.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var ordered = new List<ParsedImportRow<OrgUnitImportRow>>(rows.Count);
        var visited = new HashSet<ParsedImportRow<OrgUnitImportRow>>();

        void Visit(ParsedImportRow<OrgUnitImportRow> row)
        {
            if (!visited.Add(row))
                return;
            if (row.Data.ParentCode.Length > 0 && byCode.TryGetValue(row.Data.ParentCode, out var parent))
                Visit(parent);
            ordered.Add(row);
        }

        foreach (var row in rows)
            Visit(row);
        return ordered;
    }
}

/// <summary>Nhập đơn vị chính quyền (loại <c>departments</c>).</summary>
public sealed class DepartmentImportDefinition : OrgUnitImportDefinition
{
    private readonly IOrganizationService _organization;

    public DepartmentImportDefinition(IImportLookup lookup, IOrganizationService organization) : base(lookup)
        => _organization = organization;

    public override string Kind => "departments";

    public override string Name => "Đơn vị chính quyền";

    public override string Description => "Cây đơn vị chính quyền theo mã: mã đã có thì cập nhật, chưa có thì tạo mới.";

    public override int Order => 10;

    protected override OrgSide Side => OrgSide.Administrative;

    protected override string Label => CatalogRules.AdministrativeLabel;

    public override IReadOnlyList<ImportColumn> Columns { get; } = new ImportColumn[]
    {
        new(CodeKey, "Mã đơn vị", true, "Mã duy nhất, không có khoảng trắng (ví dụ PH-KT). Mã đã có thì dòng này cập nhật đơn vị đó.", Example: "PH-KT"),
        new(NameKey, "Tên đơn vị", true, "Tên đầy đủ.", Example: "Phòng Kỹ thuật"),
        new(ShortNameKey, "Tên viết tắt", false, "Tối đa 30 ký tự, dùng đặt tên tệp xuất. Để trống: tạo mới thì không có, cập nhật thì giữ nguyên.", Example: "KT"),
        new(ParentKey, "Mã đơn vị cấp trên", false,
            "Mã đơn vị cấp trên (đã có hoặc có trong file). Để trống: tạo đơn vị gốc (khi chưa có gốc) hoặc giữ nguyên cấp trên khi cập nhật.",
            Example: "CTY"),
        new(UnitTypeKey, "Loại đơn vị", true, "Chọn trong danh mục loại đơn vị bên chính quyền.", Example: "Phòng"),
        new(SortOrderKey, "Thứ tự", false, "Số nguyên, nhỏ đứng trước.", Example: "1"),
        new(DescriptionKey, "Mô tả", false, "Chức năng, nhiệm vụ (tùy chọn).")
    };

    protected override Task<IReadOnlyList<OrgUnitRef>> LoadUnitsAsync(IImportLookup lookup, CancellationToken ct) => lookup.GetDepartmentsAsync(ct);

    protected override async Task<Guid> CreateUnitAsync(SaveCatalogItemDto input) => (await _organization.CreateDepartmentAsync(input)).Id;

    protected override Task UpdateUnitAsync(Guid id, SaveCatalogItemDto input) => _organization.UpdateDepartmentAsync(id, input);
}

/// <summary>Nhập tổ chức Đảng (loại <c>party-cells</c>).</summary>
public sealed class PartyCellImportDefinition : OrgUnitImportDefinition
{
    private readonly IOrganizationService _organization;

    public PartyCellImportDefinition(IImportLookup lookup, IOrganizationService organization) : base(lookup)
        => _organization = organization;

    public override string Kind => "party-cells";

    public override string Name => "Tổ chức Đảng";

    public override string Description => "Cây tổ chức Đảng theo mã: mã đã có thì cập nhật, chưa có thì tạo mới.";

    public override int Order => 20;

    protected override OrgSide Side => OrgSide.Party;

    protected override string Label => CatalogRules.PartyLabel;

    public override IReadOnlyList<ImportColumn> Columns { get; } = new ImportColumn[]
    {
        new(CodeKey, "Mã tổ chức Đảng", true, "Mã duy nhất, không có khoảng trắng (ví dụ CB-KT). Mã đã có thì dòng này cập nhật tổ chức đó.", Example: "CB-KT"),
        new(NameKey, "Tên tổ chức Đảng", true, "Tên đầy đủ.", Example: "Chi bộ Kỹ thuật"),
        new(ShortNameKey, "Tên viết tắt", false, "Tối đa 30 ký tự, dùng đặt tên tệp xuất. Để trống: tạo mới thì không có, cập nhật thì giữ nguyên.", Example: "CB-KT"),
        new(ParentKey, "Mã tổ chức cấp trên", false,
            "Mã tổ chức Đảng cấp trên (đã có hoặc có trong file). Để trống: tạo tổ chức gốc (khi chưa có gốc) hoặc giữ nguyên cấp trên khi cập nhật.",
            Example: "DU"),
        new(UnitTypeKey, "Loại tổ chức", true, "Chọn trong danh mục loại đơn vị bên Đảng.", Example: "Chi bộ"),
        new(SortOrderKey, "Thứ tự", false, "Số nguyên, nhỏ đứng trước.", Example: "1"),
        new(DescriptionKey, "Mô tả", false, "Ghi chú (tùy chọn).")
    };

    protected override Task<IReadOnlyList<OrgUnitRef>> LoadUnitsAsync(IImportLookup lookup, CancellationToken ct) => lookup.GetPartyCellsAsync(ct);

    protected override async Task<Guid> CreateUnitAsync(SaveCatalogItemDto input) => (await _organization.CreateBranchAsync(input)).Id;

    protected override Task UpdateUnitAsync(Guid id, SaveCatalogItemDto input) => _organization.UpdateBranchAsync(id, input);
}
