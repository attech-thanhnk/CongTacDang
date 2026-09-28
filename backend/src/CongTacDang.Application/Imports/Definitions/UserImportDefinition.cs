using CongTacDang.Application.Accounts;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Imports.Definitions;

/// <summary>Một dòng import cán bộ.</summary>
public sealed class UserImportRow
{
    /// <summary>Tên đăng nhập (đã cắt khoảng trắng).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Họ và tên.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Email.</summary>
    public string? Email { get; set; }

    /// <summary>Số thẻ Đảng (trống = không phải Đảng viên).</summary>
    public string? PartyCardNumber { get; set; }

    /// <summary>Chức danh.</summary>
    public string? PositionTitle { get; set; }

    /// <summary>Mã Phòng (chữ hoa).</summary>
    public string? DepartmentCode { get; set; }

    /// <summary>Mã Chi bộ (chữ hoa).</summary>
    public string? PartyCellCode { get; set; }

    /// <summary>Thẩm quyền phê duyệt.</summary>
    public ApprovalAuthority ApprovalAuthority { get; set; } = ApprovalAuthority.CoSo;

    /// <summary>Id Phòng đã phân giải khi kiểm tra.</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>Id Chi bộ đã phân giải khi kiểm tra.</summary>
    public Guid? PartyCellId { get; set; }
}

/// <summary>
/// Import cán bộ + tạo tài khoản (<c>users</c>). Tài khoản tạo qua <see cref="IUserAccountService"/> (mật khẩu tạm, buộc đổi);
/// tham chiếu Phòng/Chi bộ bằng mã (tự kiểm tra, không dựa vào service). Tên đăng nhập đã có → lỗi dòng
/// (nhập lại không sửa tài khoản cũ). Kết thúc → tệp "tên đăng nhập + mật khẩu tạm" tải một lần.
/// </summary>
public sealed class UserImportDefinition : IImportDefinition<UserImportRow>
{
    /// <summary>Khóa cột.</summary>
    public const string UsernameKey = "username", FullNameKey = "fullName", EmailKey = "email", PartyCardKey = "partyCardNumber",
        PositionKey = "positionTitle", DepartmentKey = "departmentCode", PartyCellKey = "partyCellCode", ApprovalKey = "approvalAuthority";

    private const int MaxFullNameLength = 200;
    private const int MaxEmailLength = 200;

    private readonly IImportLookup _lookup;
    private readonly IUserAccountService _accounts;
    private readonly TimeProvider _time;

    /// <summary>Khởi tạo.</summary>
    public UserImportDefinition(IImportLookup lookup, IUserAccountService accounts, TimeProvider? time = null)
    {
        _lookup = lookup;
        _accounts = accounts;
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public string Kind => "users";

    /// <inheritdoc />
    public string DisplayName => "Cán bộ và tài khoản";

    /// <inheritdoc />
    public string Description => "Tạo hồ sơ cán bộ kèm tài khoản đăng nhập (mật khẩu tạm, bắt buộc đổi ở lần đăng nhập đầu). "
        + "Sau khi nhập, tải một lần tệp danh sách tên đăng nhập và mật khẩu tạm.";

    /// <inheritdoc />
    public IReadOnlyList<string> RequiredPermissions { get; } = new[] { PermissionCodes.SystemUsersManage };

    /// <inheritdoc />
    public IReadOnlyList<ImportColumn> TemplateColumns { get; } = new[]
    {
        new ImportColumn(UsernameKey, "Tên đăng nhập", true,
            $"Từ {AccountRules.UsernameMinLength} đến {AccountRules.UsernameMaxLength} ký tự: chữ cái không dấu, chữ số và \". _ -\", "
            + "không khoảng trắng (lưu chữ thường), không trùng tài khoản đã có (không phân biệt hoa thường). "
            + "Tên đã có → dòng bị báo lỗi, tài khoản cũ không bị thay đổi.", null, "nguyenvana"),
        new ImportColumn(FullNameKey, "Họ và tên", true, $"Tối đa {MaxFullNameLength} ký tự.", null, "Nguyễn Văn A"),
        new ImportColumn(EmailKey, "Email", false, "Địa chỉ thư điện tử (có @).", null, "nguyenvana@attech.com.vn"),
        new ImportColumn(PartyCardKey, "Số thẻ Đảng", false, "Để trống nếu không phải Đảng viên.", null, "12345678"),
        new ImportColumn(PositionKey, "Chức danh", false, "Chức danh hiển thị trên biểu mẫu; để trống → \"Cán bộ\".", null, "Trưởng phòng"),
        new ImportColumn(DepartmentKey, "Mã Phòng", false, "Mã Phòng/đơn vị đang hoạt động trong danh mục.", null, "PH-KH"),
        new ImportColumn(PartyCellKey, "Mã Chi bộ", false, "Mã Chi bộ đang hoạt động trong danh mục.", null, "CB-VP"),
        new ImportColumn(ApprovalKey, "Thẩm quyền phê duyệt", true,
            "CoSo = Đảng ủy cơ sở quyết định xếp loại; CapTren = cấp trên quyết định.",
            new[] { nameof(ApprovalAuthority.CoSo), nameof(ApprovalAuthority.CapTren) }, nameof(ApprovalAuthority.CoSo))
    };

    /// <inheritdoc />
    public UserImportRow ParseRow(ImportSourceRow source, ICollection<string> errors)
    {
        var row = new UserImportRow
        {
            Username = source.Get(UsernameKey),
            FullName = source.Get(FullNameKey),
            Email = source.GetOrNull(EmailKey),
            PartyCardNumber = source.GetOrNull(PartyCardKey),
            PositionTitle = source.GetOrNull(PositionKey),
            DepartmentCode = source.GetOrNull(DepartmentKey) is { } dept ? CatalogRules.NormalizeCode(dept) : null,
            PartyCellCode = source.GetOrNull(PartyCellKey) is { } cell ? CatalogRules.NormalizeCode(cell) : null
        };

        // Cùng quy tắc với IUserAccountService (AccountRules) để dòng qua được bước xem trước không bị từ chối lúc ghi.
        if (row.Username.Any(char.IsWhiteSpace))
            errors.Add("Tên đăng nhập không được chứa khoảng trắng. Hãy bỏ khoảng trắng.");
        else if (row.Username.Length > 0)
        {
            try
            {
                AccountRules.NormalizeAndValidateUsername(row.Username);
            }
            catch (ValidationException ex)
            {
                errors.Add(ex.Message);
            }
        }
        if (row.FullName.Length > MaxFullNameLength)
            errors.Add($"Họ và tên dài quá {MaxFullNameLength} ký tự.");
        if (row.Email != null && (row.Email.Length > MaxEmailLength || !AccountRules.IsValidEmail(row.Email)))
            errors.Add($"Email \"{row.Email}\" không hợp lệ. Hãy nhập dạng ten@donvi.vn hoặc để trống.");

        if (Enum.TryParse<ApprovalAuthority>(source.Get(ApprovalKey), ignoreCase: true, out var authority)
            && Enum.IsDefined(authority))
            row.ApprovalAuthority = authority;

        return row;
    }

    /// <inheritdoc />
    public async Task ValidateAsync(IReadOnlyList<ImportRow<UserImportRow>> rows, CancellationToken ct)
    {
        var departments = ToLookup(await _lookup.GetDepartmentsAsync(ct));
        var partyCells = ToLookup(await _lookup.GetPartyCellsAsync(ct));
        var usernames = rows.Select(r => r.Data.Username.ToLowerInvariant()).Where(u => u.Length > 0).Distinct().ToList();
        var existing = await _lookup.FindExistingUsernamesAsync(usernames, ct);
        var firstRowByUsername = new Dictionary<string, int>();

        foreach (var row in rows)
        {
            var data = row.Data;
            var key = data.Username.ToLowerInvariant();
            if (key.Length > 0)
            {
                if (firstRowByUsername.TryGetValue(key, out var firstRow))
                    row.AddError($"Tên đăng nhập \"{data.Username}\" trùng với dòng {firstRow} trong tệp. Mỗi tên chỉ được xuất hiện một lần.");
                else
                    firstRowByUsername[key] = row.RowNumber;

                if (existing.Contains(key))
                    row.AddError($"Tên đăng nhập \"{data.Username}\" đã có tài khoản (kể cả tài khoản đã xóa). "
                        + "Nhập dữ liệu không sửa tài khoản đã có — hãy bỏ dòng này hoặc sửa thông tin trong màn hình quản lý tài khoản.");
            }

            data.DepartmentId = Resolve(data.DepartmentCode, departments, "Phòng", row);
            data.PartyCellId = Resolve(data.PartyCellCode, partyCells, "Chi bộ", row);
            row.Action = ImportRowAction.Create;
        }
    }

    /// <inheritdoc />
    public async Task<ImportCommitResult> CommitAsync(IReadOnlyList<ImportRow<UserImportRow>> rows, CancellationToken ct)
    {
        var accounts = new List<IReadOnlyList<string?>>(rows.Count);
        var index = 0;
        foreach (var row in rows)
        {
            var d = row.Data;
            // Chỉ đưa vào đơn vị công việc; khung nhập lưu cả lô một lần trong transaction → một dòng lỗi thì không tài khoản nào được ghi.
            var created = await _accounts.StageCreateAsync(new CreateAccountCommand(
                d.Username, d.FullName, d.Email, d.PartyCardNumber, d.PositionTitle, d.DepartmentId, d.PartyCellId, d.ApprovalAuthority), ct);
            accounts.Add(new string?[] { (++index).ToString(), created.Username, d.FullName, created.TemporaryPassword });
        }

        var now = _time.GetLocalNow();
        var table = new ImportResultTable(
            $"tai-khoan-moi-{now:yyyyMMdd-HHmm}.xlsx",
            "Tài khoản",
            "Danh sách tài khoản mới và mật khẩu tạm — giao riêng cho từng người, người dùng phải đổi mật khẩu ở lần đăng nhập đầu. Hủy tệp sau khi giao.",
            new[] { "STT", "Tên đăng nhập", "Họ và tên", "Mật khẩu tạm" },
            accounts);
        return new ImportCommitResult(rows.Count, 0, table);
    }

    private static Dictionary<string, CatalogLookupEntry> ToLookup(IReadOnlyList<CatalogLookupEntry> entries)
        => entries.GroupBy(e => e.Code.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.IsDeleted).First());

    private static Guid? Resolve(string? code, Dictionary<string, CatalogLookupEntry> catalog, string label, ImportRow<UserImportRow> row)
    {
        if (code == null)
            return null;
        if (!catalog.TryGetValue(code, out var entry) || entry.IsDeleted)
        {
            row.AddError($"Mã {label} \"{code}\" không có trong danh mục. Hãy kiểm tra lại mã hoặc thêm {label} vào danh mục trước.");
            return null;
        }
        if (!entry.IsActive)
        {
            row.AddError($"{label} \"{entry.Name}\" (mã {code}) đã ngừng hoạt động. Hãy chọn {label} khác.");
            return null;
        }
        return entry.Id;
    }
}
