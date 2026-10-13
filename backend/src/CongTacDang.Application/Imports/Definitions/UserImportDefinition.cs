using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Accounts;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Imports.Definitions;

/// <summary>Một dòng nhập cán bộ + tài khoản.</summary>
public sealed class UserImportRow
{
    /// <summary>Tên đăng nhập (đã chuẩn hóa chữ thường).</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>Họ và tên.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Ngày sinh dạng yyyy-MM-dd; null = trống.</summary>
    public string? DateOfBirth { get; init; }

    /// <summary>Email (đã kiểm tra); rỗng = trống.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Điện thoại.</summary>
    public string Phone { get; init; } = string.Empty;

    /// <summary>Mã đơn vị chính quyền (chuẩn hóa chữ hoa).</summary>
    public string DepartmentCode { get; init; } = string.Empty;

    /// <summary>Mã tổ chức Đảng (chuẩn hóa chữ hoa).</summary>
    public string PartyCellCode { get; init; } = string.Empty;

    /// <summary>Đảng viên: true/false; null = suy theo số thẻ đảng.</summary>
    public bool? IsPartyMember { get; init; }

    /// <summary>Số thẻ đảng.</summary>
    public string PartyCardNumber { get; init; } = string.Empty;

    /// <summary>Chức vụ (tên hoặc mã thống kê) theo thứ tự trong ô; chức vụ đầu là chức vụ chính khi tạo mới.</summary>
    public IReadOnlyList<string> PositionTokens { get; init; } = Array.Empty<string>();

    /// <summary>Mã khung tỷ trọng (đã chuẩn hóa); null = trống.</summary>
    public string? WeightFrameCode { get; init; }

    /// <summary>Kiểu trình bày biểu mẫu như ghi trong ô; rỗng = trống.</summary>
    public string FormLayoutText { get; init; } = string.Empty;

    /// <summary>Kiểu trình bày đặt riêng đã đối chiếu.</summary>
    public Guid? FormLayoutId { get; set; }

    /// <summary>Ô ghi "Theo chức vụ": bỏ kiểu đặt riêng (cán bộ dùng kiểu của chức vụ).</summary>
    public bool ClearFormLayout { get; set; }

    /// <summary>Tài khoản đã có (cập nhật).</summary>
    public UserRef? Existing { get; set; }

    /// <summary>Id đơn vị chính quyền đã đối chiếu.</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>Id tổ chức Đảng đã đối chiếu.</summary>
    public Guid? PartyCellId { get; set; }

    /// <summary>Chức vụ đã đối chiếu (bỏ trùng).</summary>
    public List<PositionRef> Positions { get; } = new();
}

/// <summary>
/// Nhập cán bộ + tài khoản (loại <c>users</c>). Tên đăng nhập chưa có → tạo tài khoản qua <see cref="IUserAccountService"/>
/// (mật khẩu tạm sinh tự động, bắt đổi ở lần đăng nhập đầu; xuất file mật khẩu một lần). Tên đăng nhập đã có → cập nhật
/// thông tin, <b>không đổi mật khẩu</b>; ô trống giữ nguyên giá trị cũ; chức vụ chỉ thêm chức vụ chưa giữ (không kết thúc chức vụ đang giữ).
/// Quyền <c>system.users.manage</c> kiểm tra trên đơn vị/tổ chức Đảng của từng tài khoản.
/// </summary>
public sealed partial class UserImportDefinition : ImportDefinition<UserImportRow>
{
    /// <summary>Khóa cột.</summary>
    public const string UsernameKey = "username", FullNameKey = "fullName", DateOfBirthKey = "dateOfBirth", EmailKey = "email",
        PhoneKey = "phone", DepartmentKey = "departmentCode", PartyCellKey = "partyCellCode", PartyMemberKey = "isPartyMember",
        PartyCardKey = "partyCardNumber", PositionsKey = "positions", WeightFrameKey = "weightFrame", FormLayoutKey = "formLayout";

    /// <summary>Giá trị cột kiểu trình bày: bỏ kiểu đặt riêng, dùng kiểu theo chức vụ.</summary>
    public const string ByPositionLayout = "Theo chức vụ";

    private const int MaxFullNameLength = 200;
    private const int MaxPhoneLength = 30;
    private const int MaxPartyCardLength = 50;

    private readonly IImportLookup _lookup;
    private readonly IAuthorizationGuard _guard;
    private readonly IUserAccountService _accounts;
    private readonly IPositionService _positions;
    private readonly IFormLayoutService _formLayouts;
    private readonly TimeProvider _time;

    /// <summary>Khởi tạo.</summary>
    public UserImportDefinition(IImportLookup lookup, IAuthorizationGuard guard, IUserAccountService accounts, IPositionService positions,
        IFormLayoutService formLayouts, TimeProvider? time = null)
    {
        _lookup = lookup;
        _guard = guard;
        _accounts = accounts;
        _positions = positions;
        _formLayouts = formLayouts;
        _time = time ?? TimeProvider.System;
    }

    [GeneratedRegex("^M[0-9]{1,3}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StatCodePattern();

    /// <inheritdoc />
    public override string Kind => "users";

    /// <inheritdoc />
    public override string Name => "Cán bộ và tài khoản";

    /// <inheritdoc />
    public override string Description =>
        "Tên đăng nhập chưa có thì tạo tài khoản (mật khẩu tạm, bắt đổi khi đăng nhập lần đầu); đã có thì cập nhật thông tin, không đổi mật khẩu.";

    /// <inheritdoc />
    public override int Order => 30;

    /// <inheritdoc />
    public override IReadOnlyList<string> RequiredPermissions { get; } = new[] { PermissionCodes.SystemUsersManage };

    /// <inheritdoc />
    public override IReadOnlyList<ImportColumn> Columns { get; } = new ImportColumn[]
    {
        new(UsernameKey, "Tên đăng nhập", true, "Chữ không dấu, số và . _ - (3–50 ký tự). Đã có thì dòng này cập nhật tài khoản đó.", Example: "nguyen.van.a"),
        new(FullNameKey, "Họ và tên", true, "Họ và tên đầy đủ.", Example: "Nguyễn Văn A"),
        new(DateOfBirthKey, "Ngày sinh", false, "Dạng dd/MM/yyyy.", Example: "15/08/1980"),
        new(EmailKey, "Email", false, "Thư điện tử.", Example: "a.nv@donvi.vn"),
        new(PhoneKey, "Điện thoại", false, "Số điện thoại (để dạng chữ để giữ số 0 đầu).", Example: "0912345678"),
        new(DepartmentKey, "Mã đơn vị", false, "Mã đơn vị chính quyền đang công tác (phải có trong danh mục, đang hoạt động).", Example: "PH-KT"),
        new(PartyCellKey, "Mã tổ chức Đảng", false, "Mã tổ chức Đảng nơi sinh hoạt (phải có trong danh mục, đang hoạt động).", Example: "CB-KT"),
        new(PartyMemberKey, "Đảng viên", false, "Có hoặc Không. Để trống: có mã tổ chức Đảng hoặc số thẻ đảng thì là Đảng viên.", ImportValues.YesNo, Example: "Có"),
        new(PartyCardKey, "Số thẻ đảng", false, "Số thẻ đảng viên.", Example: "01234567"),
        new(PositionsKey, "Chức vụ", false,
            "Tên chức vụ hoặc mã thống kê (M1…M26) trong danh mục; nhiều chức vụ phân tách bằng dấu ;. Chức vụ đầu tiên là chức vụ chính.",
            MultipleValues: true, Example: "Trưởng phòng; Bí thư chi bộ"),
        new(WeightFrameKey, "Khung tỷ trọng mặc định", false, "Mã khung tỷ trọng A-B-C-D theo bộ tiêu chí (tùy chọn).", Example: "K2"),
        new(FormLayoutKey, "Kiểu trình bày biểu mẫu", false,
            $"Chỉ ghi khi cán bộ cần kiểu khác kiểu của chức vụ. Để trống: theo chức vụ (tài khoản đã có thì giữ nguyên); ghi \"{ByPositionLayout}\" để bỏ kiểu đã đặt riêng.")
    };

    /// <inheritdoc />
    public override async Task<IReadOnlyList<ImportColumn>> GetTemplateColumnsAsync(CancellationToken ct = default)
    {
        var positions = (await _lookup.GetPositionsAsync(ct)).Where(p => p.IsActive)
            .Select(p => string.IsNullOrEmpty(p.StatCode) ? p.Name : $"{p.Name} ({p.StatCode})")
            .ToList();
        var layouts = (await _lookup.GetFormLayoutsAsync(ct)).Select(l => l.Name).Prepend(ByPositionLayout).ToList();
        return Columns.Select(c => c.Key switch
        {
            PositionsKey => c.WithAllowedValues(positions),
            FormLayoutKey => c.WithAllowedValues(layouts),
            _ => c
        }).ToList();
    }

    /// <inheritdoc />
    protected override UserImportRow? ParseRow(ImportRawRow raw, ImportRowResult result)
    {
        var usernameText = raw.Get(UsernameKey);
        if (usernameText.Length == 0)
            return null;

        var username = Try(result, () => AccountRules.NormalizeAndValidateUsername(usernameText));
        var fullName = raw.Get(FullNameKey).Trim();
        if (fullName.Length > MaxFullNameLength)
            result.AddError($"Họ và tên dài quá {MaxFullNameLength} ký tự.");

        string? dateOfBirth = null;
        var dob = ImportValues.ParseDate(raw.Get(DateOfBirthKey), out var dobOk);
        if (!dobOk)
        {
            result.AddError($"Ngày sinh \"{raw.Get(DateOfBirthKey)}\" không đúng dạng dd/MM/yyyy.");
        }
        else if (dob.HasValue)
        {
            dateOfBirth = dob.Value.ToString("yyyy-MM-dd");
            Try(result, () => AccountRules.ParseOptionalDateOfBirth(dateOfBirth, Today()));
        }

        var email = Try(result, () => AccountRules.NormalizeOptionalEmail(raw.Get(EmailKey))) ?? string.Empty;
        var phone = raw.Get(PhoneKey).Trim();
        if (phone.Length > MaxPhoneLength)
            result.AddError($"Điện thoại dài quá {MaxPhoneLength} ký tự.");

        var card = raw.Get(PartyCardKey).Trim();
        if (card.Length > MaxPartyCardLength)
            result.AddError($"Số thẻ đảng dài quá {MaxPartyCardLength} ký tự.");
        var isMember = ImportValues.ParseYesNo(raw.Get(PartyMemberKey), out var memberOk);
        if (!memberOk)
            result.AddError($"Đảng viên \"{raw.Get(PartyMemberKey)}\" không hợp lệ: chỉ nhận Có hoặc Không.");
        else if (isMember == true && card.Length == 0 && raw.Get(PartyCellKey).Trim().Length == 0)
            result.AddError("Đảng viên phải có mã tổ chức Đảng nơi sinh hoạt hoặc số thẻ đảng.");
        else if (isMember == false && card.Length > 0)
            result.AddError("Không phải đảng viên thì để trống số thẻ đảng.");

        var weightFrame = Try(result, () => UserAccountService.NormalizeWeightFrameCode(raw.Get(WeightFrameKey)));

        if (username == null)
            return null;
        return new UserImportRow
        {
            Username = username,
            FullName = fullName,
            DateOfBirth = dateOfBirth,
            Email = email,
            Phone = phone,
            DepartmentCode = CatalogRules.NormalizeCode(raw.Get(DepartmentKey)),
            PartyCellCode = CatalogRules.NormalizeCode(raw.Get(PartyCellKey)),
            IsPartyMember = isMember,
            PartyCardNumber = card,
            PositionTokens = ImportValues.SplitList(raw.Get(PositionsKey)),
            WeightFrameCode = weightFrame,
            FormLayoutText = raw.Get(FormLayoutKey).Trim()
        };
    }

    /// <inheritdoc />
    protected override async Task ValidateAsync(IReadOnlyList<ParsedImportRow<UserImportRow>> rows, CancellationToken ct)
    {
        MarkDuplicates(rows, r => r.Username, "Tên đăng nhập");

        var now = _time.GetUtcNow().UtcDateTime;
        var users = (await _lookup.FindUsersAsync(rows.Select(r => r.Data.Username).Distinct().ToList(), now, ct))
            .GroupBy(u => u.Username, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(u => u.IsDeleted).First(), StringComparer.OrdinalIgnoreCase);
        var departments = ActiveByCode(await _lookup.GetDepartmentsAsync(ct));
        var cells = ActiveByCode(await _lookup.GetPartyCellsAsync(ct));
        var positions = await _lookup.GetPositionsAsync(ct);
        var layouts = rows.Any(r => r.Data.FormLayoutText.Length > 0) ? await _lookup.GetFormLayoutsAsync(ct) : Array.Empty<FormLayoutRef>();

        foreach (var row in rows)
        {
            var data = row.Data;
            users.TryGetValue(data.Username, out var existing);
            if (existing is { IsDeleted: true })
            {
                row.Error($"Tên đăng nhập \"{data.Username}\" thuộc tài khoản đã xóa, không dùng lại được. Hãy chọn tên đăng nhập khác.");
            }
            else if (existing != null)
            {
                data.Existing = existing;
                row.Result.Action = ImportRowAction.Update;
                row.Result.Notes.Add("Tài khoản đã có: cập nhật thông tin, giữ nguyên mật khẩu.");
            }
            else
            {
                row.Result.Action = ImportRowAction.Create;
            }

            data.DepartmentId = ResolveUnit(row, data.DepartmentCode, departments, "Mã đơn vị", "đơn vị")
                ?? (data.DepartmentCode.Length == 0 ? existing?.DepartmentId : null);
            data.PartyCellId = ResolveUnit(row, data.PartyCellCode, cells, "Mã tổ chức Đảng", "tổ chức Đảng")
                ?? (data.PartyCellCode.Length == 0 ? existing?.PartyCellId : null);

            foreach (var token in data.PositionTokens)
            {
                var position = ResolvePosition(row, token, positions, existing);
                if (position != null && data.Positions.All(p => p.Id != position.Id))
                    data.Positions.Add(position);
            }
            ResolveFormLayout(row, layouts);

            // Quyền quản lý tài khoản trên đơn vị/tổ chức Đảng của tài khoản (nơi hiện tại và nơi đến).
            if (existing is { IsDeleted: false }
                && !_guard.Can(PermissionCodes.SystemUsersManage, new AccessTarget(DepartmentId: existing.DepartmentId, PartyCellId: existing.PartyCellId)))
            {
                row.Error("Bạn không có quyền quản lý tài khoản này (ngoài phạm vi được gán).");
            }
            else if (!_guard.Can(PermissionCodes.SystemUsersManage, new AccessTarget(DepartmentId: data.DepartmentId, PartyCellId: data.PartyCellId)))
            {
                row.Error("Bạn không có quyền quản lý tài khoản ở đơn vị/tổ chức Đảng đã nhập (ngoài phạm vi được gán).");
            }
        }
    }

    /// <inheritdoc />
    protected override async Task<ImportCommitOutcome> CommitRowsAsync(IReadOnlyList<ParsedImportRow<UserImportRow>> rows, CancellationToken ct)
    {
        var outcome = new ImportCommitOutcome();
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            var data = row.Data;
            if (data.Existing is { } existing)
            {
                await RunRowAsync(row.RowNumber, async () =>
                {
                    await _accounts.UpdateAsync(existing.Id, BuildUpdate(data), ct);
                    var held = existing.HeldPositionIds.ToHashSet();
                    foreach (var position in data.Positions.Where(p => !held.Contains(p.Id)))
                        await _positions.AddMemberPositionAsync(existing.Id, PositionInput(position, data, isPrimary: null), ct);
                    if (data.FormLayoutId.HasValue || data.ClearFormLayout)
                        await _formLayouts.SetMemberLayoutAsync(existing.Id, new SetMemberFormLayoutDto { LayoutId = data.FormLayoutId }, ct);
                });
                outcome.Updated++;
            }
            else
            {
                var created = await RunRowAsync(row.RowNumber, async () =>
                {
                    var account = await _accounts.CreateAsync(new CreateAccountCommand(
                        data.Username, data.FullName, data.Email, NullIfEmpty(data.PartyCardNumber), null, data.DepartmentId, data.PartyCellId)
                    {
                        PhoneNumber = data.Phone,
                        WeightFrameCode = data.WeightFrameCode,
                        DateOfBirth = data.DateOfBirth
                    }, ct);
                    for (var i = 0; i < data.Positions.Count; i++)
                        await _positions.AddMemberPositionAsync(account.UserId, PositionInput(data.Positions[i], data, isPrimary: i == 0), ct);
                    if (data.FormLayoutId.HasValue)
                        await _formLayouts.SetMemberLayoutAsync(account.UserId, new SetMemberFormLayoutDto { LayoutId = data.FormLayoutId }, ct);
                    return account;
                });
                outcome.Credentials.Add(new ImportCredential(created.Username, data.FullName, created.TemporaryPassword));
                outcome.Created++;
            }
        }
        return outcome;
    }

    /// <summary>Lệnh cập nhật: ô trống giữ nguyên; "Đảng viên = Không" xóa số thẻ đảng.</summary>
    public static UpdateAccountCommand BuildUpdate(UserImportRow data)
    {
        string? card = data.IsPartyMember == false ? string.Empty : NullIfEmpty(data.PartyCardNumber);
        return new UpdateAccountCommand(
            FullName: data.FullName,
            Email: NullIfEmpty(data.Email),
            PhoneNumber: NullIfEmpty(data.Phone),
            PartyCardNumber: card,
            PositionTitle: null,
            DepartmentId: data.DepartmentCode.Length == 0 ? null : data.DepartmentId,
            PartyCellId: data.PartyCellCode.Length == 0 ? null : data.PartyCellId)
        {
            WeightFrameCode = data.WeightFrameCode,
            DateOfBirth = data.DateOfBirth
        };
    }

    /// <summary>Chức vụ Đảng giữ tại tổ chức Đảng của cán bộ, chức vụ chính quyền giữ tại đơn vị; chức vụ khác không gắn đơn vị.</summary>
    private static SaveMemberPositionDto PositionInput(PositionRef position, UserImportRow data, bool? isPrimary) => new()
    {
        PositionId = position.Id,
        PartyCellId = position.Side == PositionSide.Party ? data.PartyCellId : null,
        DepartmentId = position.Side == PositionSide.Administrative ? data.DepartmentId : null,
        IsPrimary = isPrimary
    };

    private static Guid? ResolveUnit(ParsedImportRow<UserImportRow> row, string code, IReadOnlyDictionary<string, OrgUnitRef> units,
        string header, string label)
    {
        if (code.Length == 0)
            return null;
        if (!units.TryGetValue(code, out var unit))
        {
            row.Error($"{header} \"{code}\" không có trong danh mục {label}.");
            return null;
        }
        if (!unit.IsActive)
        {
            row.Error($"{char.ToUpperInvariant(label[0])}{label[1..]} \"{unit.Name}\" ({unit.Code}) đã ngừng hoạt động.");
            return null;
        }
        return unit.Id;
    }

    /// <summary>Đối chiếu ô kiểu trình bày: tên kiểu trong danh mục hoặc "Theo chức vụ".</summary>
    private static void ResolveFormLayout(ParsedImportRow<UserImportRow> row, IReadOnlyList<FormLayoutRef> layouts)
    {
        var text = row.Data.FormLayoutText;
        if (text.Length == 0)
            return;
        if (ImportValues.Fold(text) == ImportValues.Fold(ByPositionLayout))
        {
            row.Data.ClearFormLayout = true;
            return;
        }
        var match = layouts.FirstOrDefault(l => ImportValues.SameText(l.Name, text))
            ?? layouts.FirstOrDefault(l => ImportValues.Fold(l.Name) == ImportValues.Fold(text));
        if (match == null)
            row.Error($"Kiểu trình bày \"{text}\" không có trong danh mục kiểu trình bày biểu mẫu.");
        else
            row.Data.FormLayoutId = match.Id;
    }

    private static PositionRef? ResolvePosition(ParsedImportRow<UserImportRow> row, string token, IReadOnlyList<PositionRef> positions, UserRef? existing)
    {
        // Cho phép dạng "Tên (Mx)" như danh sách trong file mẫu.
        var name = token;
        var statCode = StatCodePattern().IsMatch(token) ? token : null;
        var open = token.LastIndexOf('(');
        if (statCode == null && open > 0 && token.EndsWith(')'))
        {
            var inner = token[(open + 1)..^1].Trim();
            if (StatCodePattern().IsMatch(inner))
                name = token[..open].Trim();
        }

        PositionRef? match;
        if (statCode != null)
        {
            var byCode = positions.Where(p => string.Equals(p.StatCode, statCode, StringComparison.OrdinalIgnoreCase)).ToList();
            if (byCode.Count == 0)
            {
                row.Error($"Không có chức vụ mang mã thống kê \"{statCode.ToUpperInvariant()}\" trong danh mục chức vụ.");
                return null;
            }
            if (byCode.Count > 1)
            {
                row.Error($"Mã thống kê \"{statCode.ToUpperInvariant()}\" ứng với nhiều chức vụ ({string.Join(", ", byCode.Select(p => p.Name))}). Hãy ghi tên chức vụ.");
                return null;
            }
            match = byCode[0];
        }
        else
        {
            match = positions.FirstOrDefault(p => ImportValues.SameText(p.Name, name))
                ?? positions.FirstOrDefault(p => ImportValues.Fold(p.Name) == ImportValues.Fold(name));
            if (match == null)
            {
                row.Error($"Chức vụ \"{token}\" không có trong danh mục chức vụ.");
                return null;
            }
        }

        if (!match.IsActive && existing?.HeldPositionIds.Contains(match.Id) != true)
        {
            row.Error($"Chức vụ \"{match.Name}\" đã ngừng dùng.");
            return null;
        }
        return match;
    }

    private static Dictionary<string, OrgUnitRef> ActiveByCode(IReadOnlyList<OrgUnitRef> units)
        => units.Where(u => !u.IsDeleted)
            .GroupBy(u => u.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    private static T? Try<T>(ImportRowResult result, Func<T> action)
    {
        try
        {
            return action();
        }
        catch (ValidationException ex)
        {
            result.AddError(ex.Message);
            return default;
        }
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow.Add(ImportValues.VietnamOffset));
}
