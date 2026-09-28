using System.Globalization;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Imports.Definitions;

/// <summary>Một dòng import danh mục (Phòng/đơn vị hoặc Chi bộ).</summary>
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
}

/// <summary>
/// Phần chung của import danh mục: theo mã — mã đã có → cập nhật, chưa có → tạo mới.
/// Mã thuộc bản ghi đã xóa mềm → lỗi dòng (mã vẫn bị giữ bởi ràng buộc duy nhất).
/// </summary>
public abstract class CatalogImportDefinition : IImportDefinition<CatalogImportRow>
{
    /// <summary>Giá trị cột trạng thái: hoạt động.</summary>
    public const string StatusActive = "Hoạt động";

    /// <summary>Giá trị cột trạng thái: ngừng hoạt động.</summary>
    public const string StatusInactive = "Ngừng hoạt động";

    /// <summary>Khóa cột.</summary>
    public const string CodeKey = "code", NameKey = "name", DescriptionKey = "description", SortOrderKey = "sortOrder", StatusKey = "status";

    private readonly IImportLookup _lookup;

    /// <summary>Khởi tạo.</summary>
    protected CatalogImportDefinition(IImportLookup lookup, IOrganizationRepository organizations)
    {
        _lookup = lookup;
        Organizations = organizations;
        TemplateColumns = new[]
        {
            new ImportColumn(CodeKey, $"Mã {Label}", true,
                $"Mã duy nhất, không chứa khoảng trắng, tối đa {CatalogRules.MaxCodeLength} ký tự; không phân biệt hoa thường (lưu chữ hoa). "
                + $"Mã đã có → cập nhật {Label} đó; chưa có → tạo mới.", null, ExampleCode),
            new ImportColumn(NameKey, $"Tên {Label}", true, $"Tên đầy đủ, tối đa {CatalogRules.MaxNameLength} ký tự.", null, ExampleName),
            new ImportColumn(DescriptionKey, "Mô tả", false, "Chức năng, nhiệm vụ. Để trống khi cập nhật → giữ nguyên.", null, "…"),
            new ImportColumn(SortOrderKey, "Thứ tự hiển thị", false,
                "Số nguyên ≥ 0, nhỏ đứng trước. Để trống: tạo mới = 0, cập nhật = giữ nguyên.", null, "1"),
            new ImportColumn(StatusKey, "Trạng thái", false,
                "Để trống: tạo mới = Hoạt động, cập nhật = giữ nguyên.", new[] { StatusActive, StatusInactive }, StatusActive)
        };
    }

    /// <summary>Repository danh mục (dùng khi ghi).</summary>
    protected IOrganizationRepository Organizations { get; }

    /// <summary>Nhãn tiếng Việt: "Phòng" / "Chi bộ".</summary>
    protected abstract string Label { get; }

    /// <summary>Mã ví dụ.</summary>
    protected abstract string ExampleCode { get; }

    /// <summary>Tên ví dụ.</summary>
    protected abstract string ExampleName { get; }

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
            Description = source.GetOrNull(DescriptionKey)
        };

        if (row.Code.Length > 0 && CatalogRules.ValidateCode(row.Code, Label) is { } codeError)
            errors.Add(codeError);
        if (row.Name.Length > 0 && CatalogRules.ValidateName(row.Name, Label) is { } nameError)
            errors.Add(nameError);

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
            .ToDictionary(g => g.Key, g => g.First());
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
    }

    /// <inheritdoc />
    public async Task<ImportCommitResult> CommitAsync(IReadOnlyList<ImportRow<CatalogImportRow>> rows, CancellationToken ct)
    {
        var (created, updated) = await ApplyAsync(rows.Select(r => r.Data).ToList(), ct);
        return new ImportCommitResult(created, updated);
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

/// <summary>Import danh mục Phòng/đơn vị (<c>departments</c>).</summary>
public sealed class DepartmentImportDefinition : CatalogImportDefinition
{
    /// <summary>Khởi tạo.</summary>
    public DepartmentImportDefinition(IImportLookup lookup, IOrganizationRepository organizations) : base(lookup, organizations) { }

    /// <inheritdoc />
    public override string Kind => "departments";

    /// <inheritdoc />
    public override string DisplayName => "Danh mục Phòng/đơn vị";

    /// <inheritdoc />
    public override string Description => "Thêm mới hoặc cập nhật Phòng/đơn vị chuyên môn theo mã.";

    /// <inheritdoc />
    protected override string Label => "Phòng";

    /// <inheritdoc />
    protected override string ExampleCode => "PH-KH";

    /// <inheritdoc />
    protected override string ExampleName => "Phòng Kế hoạch";

    /// <inheritdoc />
    protected override Task<IReadOnlyList<CatalogLookupEntry>> LoadExistingAsync(IImportLookup lookup, CancellationToken ct)
        => lookup.GetDepartmentsAsync(ct);

    /// <inheritdoc />
    protected override async Task<(int Created, int Updated)> ApplyAsync(IReadOnlyList<CatalogImportRow> rows, CancellationToken ct)
    {
        var existing = (await Organizations.ListDepartmentsIncludingDeletedAsync())
            .Where(d => !d.IsDeleted)
            .GroupBy(d => d.Code.ToUpperInvariant()).ToDictionary(g => g.Key, g => g.First());
        int created = 0, updated = 0;
        foreach (var row in rows)
        {
            var isNew = !existing.TryGetValue(row.Code, out var entity);
            if (isNew)
            {
                entity = new AdministrativeDepartment { Code = row.Code };
                Organizations.AddDepartment(entity);
                created++;
            }
            else
            {
                updated++;
            }

            var target = entity!;
            Apply(row, v => target.Name = v, v => target.Description = v, v => target.SortOrder = v, v => target.IsActive = v, isNew);
        }

        return (created, updated);
    }
}

/// <summary>Import danh mục Chi bộ (<c>party-cells</c>).</summary>
public sealed class PartyCellImportDefinition : CatalogImportDefinition
{
    /// <summary>Khởi tạo.</summary>
    public PartyCellImportDefinition(IImportLookup lookup, IOrganizationRepository organizations) : base(lookup, organizations) { }

    /// <inheritdoc />
    public override string Kind => "party-cells";

    /// <inheritdoc />
    public override string DisplayName => "Danh mục Chi bộ";

    /// <inheritdoc />
    public override string Description => "Thêm mới hoặc cập nhật Chi bộ theo mã.";

    /// <inheritdoc />
    protected override string Label => "Chi bộ";

    /// <inheritdoc />
    protected override string ExampleCode => "CB-KT";

    /// <inheritdoc />
    protected override string ExampleName => "Chi bộ Khối Kỹ thuật";

    /// <inheritdoc />
    protected override Task<IReadOnlyList<CatalogLookupEntry>> LoadExistingAsync(IImportLookup lookup, CancellationToken ct)
        => lookup.GetPartyCellsAsync(ct);

    /// <inheritdoc />
    protected override async Task<(int Created, int Updated)> ApplyAsync(IReadOnlyList<CatalogImportRow> rows, CancellationToken ct)
    {
        var existing = (await Organizations.ListPartyCellsIncludingDeletedAsync())
            .Where(c => !c.IsDeleted)
            .GroupBy(c => c.Code.ToUpperInvariant()).ToDictionary(g => g.Key, g => g.First());
        int created = 0, updated = 0;
        foreach (var row in rows)
        {
            var isNew = !existing.TryGetValue(row.Code, out var entity);
            if (isNew)
            {
                entity = new PartyCell { Code = row.Code };
                Organizations.AddPartyCell(entity);
                created++;
            }
            else
            {
                updated++;
            }

            var target = entity!;
            Apply(row, v => target.Name = v, v => target.Description = v, v => target.SortOrder = v, v => target.IsActive = v, isNew);
        }

        return (created, updated);
    }
}
