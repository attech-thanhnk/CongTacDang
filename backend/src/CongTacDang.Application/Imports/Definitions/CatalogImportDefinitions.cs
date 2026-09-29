using System.Globalization;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Organization;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Imports.Definitions;

/// <summary>Một dòng import danh mục đơn vị (đơn vị chính quyền hoặc tổ chức Đảng).</summary>
public sealed class CatalogImportRow
{
    /// <summary>Mã đã chuẩn hóa (chữ hoa).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả; null = để trống (khi cập nhật giữ nguyên).</summary>
    public string? Description { get; set; }

    /// <summary>Thứ tự hiển thị; null = để trống (tạo mới: 0, cập nhật: giữ nguyên).</summary>
    public int? SortOrder { get; set; }

    /// <summary>Trạng thái; null = để trống (tạo mới: hoạt động, cập nhật: giữ nguyên).</summary>
    public bool? IsActive { get; set; }

    /// <summary>Mã đơn vị cha (chữ hoa); null = để trống (tạo mới: gốc, cập nhật: giữ nguyên cha).</summary>
    public string? ParentCode { get; set; }

    /// <summary>Tên loại đơn vị; null = để trống (tạo mới: không loại, cập nhật: giữ nguyên).</summary>
    public string? UnitTypeName { get; set; }

    /// <summary>Id loại đơn vị đã phân giải khi kiểm tra.</summary>
    public Guid? UnitTypeId { get; set; }
}

/// <summary>
/// Phần chung của import danh mục đơn vị: theo mã — mã đã có → cập nhật, chưa có → tạo mới.
/// Mã thuộc bản ghi đã xóa mềm → lỗi dòng (mã vẫn bị giữ bởi ràng buộc duy nhất).
/// Cây đơn vị (task 14): cột "Mã đơn vị cha" tham chiếu đơn vị đã có hoặc đơn vị trong cùng tệp (thứ tự dòng tùy ý);
/// thiếu cha hoặc tạo vòng → lỗi dòng. Ghi xong tính lại đường dẫn cả cây và xóa cache quyền.
/// </summary>
public abstract class CatalogImportDefinition : IImportDefinition<CatalogImportRow>
{
    /// <summary>Giá trị cột trạng thái: hoạt động.</summary>
    public const string StatusActive = "Hoạt động";

    /// <summary>Giá trị cột trạng thái: ngừng hoạt động.</summary>
    public const string StatusInactive = "Ngừng hoạt động";

    /// <summary>Khóa cột.</summary>
    public const string CodeKey = "code", NameKey = "name", DescriptionKey = "description", SortOrderKey = "sortOrder", StatusKey = "status",
        ParentCodeKey = "parentCode", UnitTypeKey = "unitType";

    private readonly IImportLookup _lookup;
    private readonly IOrgImportLookup? _orgLookup;
    private readonly IAccessCacheInvalidator? _accessCache;

    /// <summary>Khởi tạo.</summary>
    protected CatalogImportDefinition(IImportLookup lookup, IOrganizationRepository organizations,
        IOrgImportLookup? orgLookup, IAccessCacheInvalidator? accessCache)
    {
        _lookup = lookup;
        _orgLookup = orgLookup;
        _accessCache = accessCache;
        Organizations = organizations;
        TemplateColumns = new[]
        {
            new ImportColumn(CodeKey, $"Mã {Label}", true,
                $"Mã duy nhất, không chứa khoảng trắng, tối đa {CatalogRules.MaxCodeLength} ký tự; không phân biệt hoa thường (lưu chữ hoa). "
                + $"Mã đã có → cập nhật {Label} đó; chưa có → tạo mới.", null, ExampleCode),
            new ImportColumn(NameKey, $"Tên {Label}", true, $"Tên đầy đủ, tối đa {CatalogRules.MaxNameLength} ký tự.", null, ExampleName),
            new ImportColumn(ParentCodeKey, "Mã đơn vị cha", false,
                $"Mã {Label} cấp trên trực tiếp — đã có trong danh mục hoặc có trong cùng tệp (thứ tự dòng tùy ý). "
                + "Để trống: tạo mới = đơn vị gốc, cập nhật = giữ nguyên cấp trên. Không được tạo vòng.", null, ExampleParentCode),
            new ImportColumn(UnitTypeKey, "Loại đơn vị", false,
                $"Tên loại trong danh mục loại đơn vị ({SideLabel}), ví dụ {ExampleUnitType}. Để trống: tạo mới = không loại, cập nhật = giữ nguyên.",
                null, ExampleUnitType),
            new ImportColumn(DescriptionKey, "Mô tả", false, "Chức năng, nhiệm vụ. Để trống khi cập nhật → giữ nguyên.", null, "…"),
            new ImportColumn(SortOrderKey, "Thứ tự hiển thị", false,
                "Số nguyên ≥ 0, nhỏ đứng trước (trong cùng cấp). Để trống: tạo mới = 0, cập nhật = giữ nguyên.", null, "1"),
            new ImportColumn(StatusKey, "Trạng thái", false,
                "Để trống: tạo mới = Hoạt động, cập nhật = giữ nguyên.", new[] { StatusActive, StatusInactive }, StatusActive)
        };
    }

    /// <summary>Repository danh mục (dùng khi ghi).</summary>
    protected IOrganizationRepository Organizations { get; }

    /// <summary>Nhãn tiếng Việt dùng trong tiêu đề cột: "đơn vị" (chính quyền) / "tổ chức Đảng".</summary>
    protected abstract string Label { get; }

    /// <summary>Bên của loại đơn vị hợp lệ.</summary>
    protected abstract OrgSide Side { get; }

    /// <summary>Tên hiển thị của bên (trong hướng dẫn).</summary>
    protected string SideLabel => CatalogRules.SideName(Side);

    /// <summary>Mã ví dụ.</summary>
    protected abstract string ExampleCode { get; }

    /// <summary>Tên ví dụ.</summary>
    protected abstract string ExampleName { get; }

    /// <summary>Mã cha ví dụ.</summary>
    protected abstract string ExampleParentCode { get; }

    /// <summary>Loại đơn vị ví dụ.</summary>
    protected abstract string ExampleUnitType { get; }

    /// <inheritdoc />
    public abstract string Kind { get; }

    /// <inheritdoc />
    public abstract string DisplayName { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public IReadOnlyList<ImportColumn> TemplateColumns { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = new[] { PermissionCodes.CatalogManage };

    /// <summary>Danh mục hiện có (kể cả đã xóa mềm) để kiểm tra mã.</summary>
    protected abstract Task<IReadOnlyList<CatalogLookupEntry>> LoadExistingAsync(IImportLookup lookup, CancellationToken ct);

    /// <summary>Tạo mới / cập nhật trong DbContext (chưa lưu) — trả (tạo mới, cập nhật).</summary>
    protected abstract Task<(int Created, int Updated)> ApplyAsync(IReadOnlyList<CatalogImportRow> rows, CancellationToken ct);

    /// <inheritdoc />
    public CatalogImportRow ParseRow(ImportSourceRow source, ICollection<string> errors)
    {
        var row = new CatalogImportRow
        {
            Code = CatalogRules.NormalizeCode(source.Get(CodeKey)),
            Name = source.Get(NameKey),
            Description = source.GetOrNull(DescriptionKey),
            ParentCode = source.GetOrNull(ParentCodeKey) is { } parent ? CatalogRules.NormalizeCode(parent) : null,
            UnitTypeName = source.GetOrNull(UnitTypeKey)
        };

        if (row.Code.Length > 0 && CatalogRules.ValidateCode(row.Code, Label) is { } codeError)
            errors.Add(codeError);
        if (row.Name.Length > 0 && CatalogRules.ValidateName(row.Name, Label) is { } nameError)
            errors.Add(nameError);
        if (row.ParentCode != null && row.Code.Length > 0 && row.ParentCode == row.Code)
            errors.Add($"\"Mã đơn vị cha\" trùng với chính mã {Label} \"{row.Code}\". Một đơn vị không thể là cấp trên của chính nó.");

        if (source.GetOrNull(SortOrderKey) is { } sortText)
        {
            if (int.TryParse(sortText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sort) && sort >= 0)
                row.SortOrder = sort;
            else
                errors.Add($"\"Thứ tự hiển thị\" phải là số nguyên không âm (đang là \"{sortText}\").");
        }

        row.IsActive = source.GetOrNull(StatusKey) switch
        {
            null => null,
            StatusActive => true,
            _ => false
        };
        return row;
    }

    /// <inheritdoc />
    public async Task ValidateAsync(IReadOnlyList<ImportRow<CatalogImportRow>> rows, CancellationToken ct)
    {
        var existing = (await LoadExistingAsync(_lookup, ct))
            .GroupBy(e => e.Code.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.IsDeleted).First());
        var firstRowByCode = new Dictionary<string, int>();

        foreach (var row in rows)
        {
            var code = row.Data.Code;
            if (code.Length == 0)
                continue;

            if (firstRowByCode.TryGetValue(code, out var firstRow))
            {
                row.AddError($"Mã {Label} \"{code}\" trùng với dòng {firstRow} trong tệp. Mỗi mã chỉ được xuất hiện một lần.");
                continue;
            }
            firstRowByCode[code] = row.RowNumber;

            if (existing.TryGetValue(code, out var entry))
            {
                if (entry.IsDeleted)
                    row.AddError($"Mã {Label} \"{code}\" thuộc {Label} \"{entry.Name}\" đã bị xóa nên không dùng lại được. Hãy chọn mã khác.");
                else
                    row.Action = ImportRowAction.Update;
            }
            else
            {
                row.Action = ImportRowAction.Create;
            }
        }

        ValidateTree(rows, existing);
        await ResolveUnitTypesAsync(rows, ct);
    }

    /// <summary>Kiểm tra mã đơn vị cha (có trong danh mục hoặc trong tệp) và vòng lặp cha–con trên cây sau khi nhập.</summary>
    private void ValidateTree(IReadOnlyList<ImportRow<CatalogImportRow>> rows, IReadOnlyDictionary<string, CatalogLookupEntry> existing)
    {
        // Nút trong tệp (dòng hợp lệ về mã): Id thật nếu đã có, Id tạm nếu tạo mới.
        var fileRows = rows.Where(r => r.Data.Code.Length > 0 && !r.HasErrors)
            .GroupBy(r => r.Data.Code).ToDictionary(g => g.Key, g => g.First());
        var idByCode = existing.Values.Where(e => !e.IsDeleted).ToDictionary(e => e.Code.ToUpperInvariant(), e => e.Id);
        foreach (var code in fileRows.Keys.Where(c => !idByCode.ContainsKey(c)))
            idByCode[code] = Guid.NewGuid();

        var links = existing.Values.Where(e => !e.IsDeleted).ToDictionary(e => e.Id, e => e.ParentId);
        var rowById = new Dictionary<Guid, ImportRow<CatalogImportRow>>();
        foreach (var (code, row) in fileRows)
        {
            var id = idByCode[code];
            rowById[id] = row;
            var parentCode = row.Data.ParentCode;
            if (parentCode == null)
            {
                if (!links.ContainsKey(id))
                    links[id] = null; // tạo mới không có cha → gốc; cập nhật → giữ nguyên cha
                continue;
            }

            if (!idByCode.TryGetValue(parentCode, out var parentId))
            {
                var deleted = existing.TryGetValue(parentCode, out var entry) && entry.IsDeleted;
                row.AddError(deleted
                    ? $"Đơn vị cha mã \"{parentCode}\" đã bị xóa. Hãy chọn đơn vị cha khác."
                    : $"Không tìm thấy đơn vị cha mã \"{parentCode}\" trong danh mục hoặc trong tệp. Hãy kiểm tra mã hoặc thêm đơn vị cha vào tệp.");
                links[id] = links.GetValueOrDefault(id);
                continue;
            }
            links[id] = parentId;
        }

        var result = OrgTree.BuildPaths(links.Select(l => new OrgNodeLink(l.Key, l.Value)));
        foreach (var id in result.CycleNodes)
        {
            if (rowById.TryGetValue(id, out var row) && !row.HasErrors)
                row.AddError($"Quan hệ cấp trên – cấp dưới của mã \"{row.Data.Code}\" tạo thành vòng (đơn vị là cấp trên của chính nó qua các cấp). "
                    + "Hãy sửa cột \"Mã đơn vị cha\".");
        }
    }

    private async Task ResolveUnitTypesAsync(IReadOnlyList<ImportRow<CatalogImportRow>> rows, CancellationToken ct)
    {
        if (rows.All(r => r.Data.UnitTypeName == null))
            return;
        if (_orgLookup == null)
            throw new InvalidOperationException("Chưa cấu hình tra cứu loại đơn vị cho import.");

        var types = (await _orgLookup.GetUnitTypesAsync(ct)).Where(t => t.Side == Side)
            .GroupBy(t => t.Name.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());
        foreach (var row in rows.Where(r => r.Data.UnitTypeName != null))
        {
            var name = row.Data.UnitTypeName!;
            if (!types.TryGetValue(name.Trim().ToLowerInvariant(), out var type))
            {
                row.AddError($"Loại đơn vị \"{name}\" không có trong danh mục loại đơn vị ({SideLabel}). "
                    + "Hãy thêm loại ở màn hình Danh mục tổ chức → Loại đơn vị hoặc sửa tên.");
                continue;
            }
            if (!type.IsActive)
            {
                row.AddError($"Loại đơn vị \"{type.Name}\" đã ngừng dùng. Hãy chọn loại khác.");
                continue;
            }
            row.Data.UnitTypeId = type.Id;
        }
    }

    /// <inheritdoc />
    public async Task<ImportCommitResult> CommitAsync(IReadOnlyList<ImportRow<CatalogImportRow>> rows, CancellationToken ct)
    {
        var (created, updated) = await ApplyAsync(rows.Select(r => r.Data).ToList(), ct);
        // Cấu trúc cây có thể đổi → phạm vi gán vai trò bao trùm cây con phải tính lại (xóa sau commit).
        _accessCache?.InvalidateAll();
        return new ImportCommitResult(created, updated);
    }

    /// <summary>
    /// Ghi các dòng vào cây <typeparamref name="TUnit"/>: tạo/cập nhật theo mã, gán cha theo mã (kể cả đơn vị vừa tạo trong lô),
    /// gán loại, rồi tính lại đường dẫn của cả cây.
    /// </summary>
    protected (int Created, int Updated) ApplyToTree<TUnit>(
        IReadOnlyList<CatalogImportRow> rows,
        List<TUnit> allIncludingDeleted,
        Func<string, TUnit> create,
        Action<TUnit, CatalogImportRow, bool> set,
        Func<TUnit, bool> isDeleted,
        Action<TUnit, Guid?> setUnitType)
        where TUnit : class, IOrgUnit
    {
        var live = allIncludingDeleted.Where(u => !isDeleted(u)).ToList();
        var byCode = live.GroupBy(u => u.Code.ToUpperInvariant()).ToDictionary(g => g.Key, g => g.First());
        int created = 0, updated = 0;
        foreach (var row in rows)
        {
            var isNew = !byCode.TryGetValue(row.Code, out var entity);
            if (isNew)
            {
                entity = create(row.Code);
                byCode[row.Code] = entity;
                live.Add(entity);
                created++;
            }
            else
            {
                updated++;
            }

            set(entity!, row, isNew);
            if (row.UnitTypeId.HasValue)
                setUnitType(entity!, row.UnitTypeId);
        }

        foreach (var row in rows.Where(r => r.ParentCode != null))
            byCode[row.Code].ParentId = byCode[row.ParentCode!].Id;

        OrganizationService.RecomputePaths(live, Label);
        return (created, updated);
    }

    /// <summary>Gán giá trị dòng vào bản ghi (cập nhật: ô trống giữ nguyên).</summary>
    protected static void Apply(CatalogImportRow row, Action<string> setName, Action<string> setDescription,
        Action<int> setSortOrder, Action<bool> setActive, bool isNew)
    {
        setName(row.Name);
        if (row.Description != null || isNew) setDescription(row.Description ?? string.Empty);
        if (row.SortOrder.HasValue || isNew) setSortOrder(row.SortOrder ?? 0);
        if (row.IsActive.HasValue || isNew) setActive(row.IsActive ?? true);
    }
}

/// <summary>Import danh mục đơn vị chính quyền (<c>departments</c>).</summary>
public sealed class DepartmentImportDefinition : CatalogImportDefinition
{
    /// <summary>Khởi tạo.</summary>
    public DepartmentImportDefinition(IImportLookup lookup, IOrganizationRepository organizations,
        IOrgImportLookup? orgLookup = null, IAccessCacheInvalidator? accessCache = null)
        : base(lookup, organizations, orgLookup, accessCache) { }

    /// <inheritdoc />
    public override string Kind => "departments";

    /// <inheritdoc />
    public override string DisplayName => "Danh mục đơn vị chính quyền";

    /// <inheritdoc />
    public override string Description => "Thêm mới hoặc cập nhật đơn vị chính quyền (Công ty, Phòng, Trung tâm…) theo mã, kèm đơn vị cha và loại đơn vị.";

    /// <inheritdoc />
    protected override string Label => "đơn vị";

    /// <inheritdoc />
    protected override OrgSide Side => OrgSide.Administrative;

    /// <inheritdoc />
    protected override string ExampleCode => "PH-KH";

    /// <inheritdoc />
    protected override string ExampleName => "Phòng Kế hoạch";

    /// <inheritdoc />
    protected override string ExampleParentCode => "ATTECH";

    /// <inheritdoc />
    protected override string ExampleUnitType => "Phòng";

    /// <inheritdoc />
    protected override Task<IReadOnlyList<CatalogLookupEntry>> LoadExistingAsync(IImportLookup lookup, CancellationToken ct)
        => lookup.GetDepartmentsAsync(ct);

    /// <inheritdoc />
    protected override async Task<(int Created, int Updated)> ApplyAsync(IReadOnlyList<CatalogImportRow> rows, CancellationToken ct)
    {
        var all = await Organizations.ListDepartmentsIncludingDeletedAsync();
        return ApplyToTree(rows, all,
            code =>
            {
                var entity = new AdministrativeDepartment { Code = code };
                Organizations.AddDepartment(entity);
                return entity;
            },
            (target, row, isNew) => Apply(row, v => target.Name = v, v => target.Description = v, v => target.SortOrder = v,
                v => target.IsActive = v, isNew),
            d => d.IsDeleted,
            (d, typeId) => d.UnitTypeId = typeId);
    }
}

/// <summary>Import danh mục tổ chức Đảng (<c>party-cells</c>).</summary>
public sealed class PartyCellImportDefinition : CatalogImportDefinition
{
    /// <summary>Khởi tạo.</summary>
    public PartyCellImportDefinition(IImportLookup lookup, IOrganizationRepository organizations,
        IOrgImportLookup? orgLookup = null, IAccessCacheInvalidator? accessCache = null)
        : base(lookup, organizations, orgLookup, accessCache) { }

    /// <inheritdoc />
    public override string Kind => "party-cells";

    /// <inheritdoc />
    public override string DisplayName => "Danh mục tổ chức Đảng";

    /// <inheritdoc />
    public override string Description => "Thêm mới hoặc cập nhật tổ chức Đảng (Đảng ủy, Đảng bộ bộ phận, Chi bộ…) theo mã, kèm tổ chức cấp trên và loại.";

    /// <inheritdoc />
    protected override string Label => "tổ chức Đảng";

    /// <inheritdoc />
    protected override OrgSide Side => OrgSide.Party;

    /// <inheritdoc />
    protected override string ExampleCode => "CB-KT";

    /// <inheritdoc />
    protected override string ExampleName => "Chi bộ Khối Kỹ thuật";

    /// <inheritdoc />
    protected override string ExampleParentCode => "DU-ATTECH";

    /// <inheritdoc />
    protected override string ExampleUnitType => "Chi bộ";

    /// <inheritdoc />
    protected override Task<IReadOnlyList<CatalogLookupEntry>> LoadExistingAsync(IImportLookup lookup, CancellationToken ct)
        => lookup.GetPartyCellsAsync(ct);

    /// <inheritdoc />
    protected override async Task<(int Created, int Updated)> ApplyAsync(IReadOnlyList<CatalogImportRow> rows, CancellationToken ct)
    {
        var all = await Organizations.ListPartyCellsIncludingDeletedAsync();
        return ApplyToTree(rows, all,
            code =>
            {
                var entity = new PartyCell { Code = code };
                Organizations.AddPartyCell(entity);
                return entity;
            },
            (target, row, isNew) => Apply(row, v => target.Name = v, v => target.Description = v, v => target.SortOrder = v,
                v => target.IsActive = v, isNew),
            c => c.IsDeleted,
            (c, typeId) => c.UnitTypeId = typeId);
    }
}
