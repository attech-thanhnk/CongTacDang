using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.Organization;

/// <summary>Chức vụ đang hiệu lực của một người — đủ để suy ra thẩm quyền, mã thống kê và diện quyết định gợi ý.</summary>
/// <param name="IsHead">Không tính vào trần Hoàn thành xuất sắc.</param>
/// <param name="DefaultWorkflowProfileCode">Mã diện quyết định gợi ý của chức vụ (null = theo thẩm quyền).</param>
public sealed record HeldPosition(string PositionName, string? StatCode, ApprovalAuthority? DefaultApprovalAuthority, bool IsHead = false,
    string? DefaultWorkflowProfileCode = null, bool IsPrimary = false);

/// <summary>Chức danh in của một chức vụ (có thể gộp "Quyền X" với "Phó X").</summary>
/// <param name="Acting">Bản ghi "Quyền X" không in vì người giữ cấp phó "Phó X" (<paramref name="Source"/>) cùng đơn vị; null nếu không có.</param>
public sealed record PrintedPosition(MemberPosition Source, PositionSide Side, string Title, MemberPosition? Acting = null);

/// <summary>Quy tắc suy ra từ chức vụ: thẩm quyền phê duyệt, mã thống kê, chức danh hiển thị.</summary>
public static class PositionRules
{
    public const int MaxNameLength = 200;

    private static readonly Regex StatCodePattern = new("^M([0-9]{1,3})$", RegexOptions.CultureInvariant);

    /// <summary>Thuộc diện đánh giá khi giữ chức vụ có mã thống kê hoặc là người đứng đầu; chuyên viên, nhân viên,
    /// đảng viên thường và tài khoản không có chức vụ thì không.</summary>
    public static bool IsEvaluable(IEnumerable<HeldPosition>? activePositions)
        => activePositions != null && activePositions.Any(p => !string.IsNullOrWhiteSpace(p.StatCode) || p.IsHead);

    /// <summary>Hạng chức vụ để sắp xếp từ cấp trên xuống (nhỏ hơn = cao hơn): theo mã thống kê M1–M26, người đứng đầu trước
    /// phó; chức vụ không có mã nhưng đứng đầu (Đài trưởng) sau Trưởng phòng; còn lại cuối.</summary>
    public static int SeniorityRank(IEnumerable<HeldPosition>? activePositions)
    {
        var best = 9999;
        foreach (var p in activePositions ?? Enumerable.Empty<HeldPosition>())
        {
            var number = p.StatCode is { Length: > 1 } code && int.TryParse(code.AsSpan(1), out var n) ? n : (int?)null;
            var rank = number.HasValue ? number.Value * 10 - (p.IsHead ? 5 : 0) : p.IsHead ? 258 : 9999;
            best = Math.Min(best, rank);
        }
        return best;
    }

    /// <summary>Lãnh đạo cấp Công ty (hạng tới Kiểm soát viên) xếp thành nhóm đầu.</summary>
    public const int CompanyLeadershipMaxRank = 150;

    /// <summary>Danh sách cán bộ từ chức danh cao xuống: lãnh đạo Công ty, rồi từng đơn vị theo thứ tự trong cây (Trưởng trước Phó), cuối cùng theo tên.</summary>
    public static List<T> OrderBySeniority<T>(IEnumerable<T> items, IReadOnlyDictionary<Guid, List<HeldPosition>> held,
        IReadOnlyDictionary<string, int> unitOrder, Func<T, Guid> memberId, Func<T, string?> departmentName, Func<T, string?> fullName)
        => items
            .Select(item => (Item: item, Rank: SeniorityRank(held.GetValueOrDefault(memberId(item)))))
            .OrderBy(x => x.Rank <= CompanyLeadershipMaxRank ? 0 : 1)
            .ThenBy(x => x.Rank <= CompanyLeadershipMaxRank ? -1 : unitOrder.GetValueOrDefault(departmentName(x.Item) ?? string.Empty, int.MaxValue))
            .ThenBy(x => x.Rank <= CompanyLeadershipMaxRank ? string.Empty : departmentName(x.Item) ?? string.Empty, StringComparer.CurrentCulture)
            .ThenBy(x => x.Rank)
            .ThenBy(x => fullName(x.Item) ?? string.Empty, StringComparer.CurrentCulture)
            .Select(x => x.Item)
            .ToList();

    /// <summary><c>CapTren</c> nếu có chức vụ đang hiệu lực mặc định <c>CapTren</c> (giữ nhiều chức danh), ngược lại <c>CoSo</c>.</summary>
    public static ApprovalAuthority DeriveApprovalAuthority(IEnumerable<HeldPosition> activePositions)
        => activePositions.Any(p => p.DefaultApprovalAuthority == ApprovalAuthority.CapTren)
            ? ApprovalAuthority.CapTren
            : ApprovalAuthority.CoSo;

    /// <summary>
    /// Diện quyết định gợi ý theo chức vụ, null nếu không có. Chỉ xét chức vụ có mã diện quyết định mà kỳ có
    /// (<paramref name="profileExists"/>) và thẩm quyền mặc định trống hoặc trùng thẩm quyền của người (diện quyết định phải khớp cấp
    /// quyết định). Nhiều chức vụ: mã thống kê thứ tự nhỏ nhất (chức danh cao nhất), rồi chức vụ chính, rồi tên.
    /// </summary>
    public static HeldPosition? SuggestWorkflowProfile(IEnumerable<HeldPosition>? activePositions, ApprovalAuthority authority,
        Func<string, bool> profileExists)
    {
        ArgumentNullException.ThrowIfNull(profileExists);
        return (activePositions ?? Enumerable.Empty<HeldPosition>())
            .Where(p => !string.IsNullOrWhiteSpace(p.DefaultWorkflowProfileCode) && profileExists(p.DefaultWorkflowProfileCode!))
            .Where(p => p.DefaultApprovalAuthority == null || p.DefaultApprovalAuthority == authority)
            .OrderBy(p => StatCodeOrder(p.StatCode) ?? int.MaxValue)
            .ThenByDescending(p => p.IsPrimary)
            .ThenBy(p => p.PositionName, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>Diện quyết định mặc định khi thêm người vào kỳ: theo chức vụ nếu có gợi ý hợp lệ, ngược lại theo cấp quyết định.</summary>
    public static WorkflowProfile DefaultProfile(PeriodSettings settings, ApprovalAuthority authority, IEnumerable<HeldPosition>? activePositions)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var suggested = SuggestWorkflowProfile(activePositions, authority, code => settings.FindProfile(code) != null);
        return settings.ResolveDefaultProfile(authority, suggested?.DefaultWorkflowProfileCode);
    }

    /// <summary>Thẩm quyền áp dụng: giá trị đặt tay nếu có, ngược lại suy ra.</summary>
    public static ApprovalAuthority EffectiveApprovalAuthority(ApprovalAuthority? manualOverride, IEnumerable<HeldPosition> activePositions)
        => manualOverride ?? DeriveApprovalAuthority(activePositions);

    /// <summary>Mã thống kê của người: mã có thứ tự nhỏ nhất trong các chức vụ đang hiệu lực; null nếu không có.</summary>
    public static string? PersonStatCode(IEnumerable<HeldPosition> activePositions)
        => activePositions
            .Select(p => p.StatCode)
            .Where(code => StatCodeOrder(code).HasValue)
            .OrderBy(code => StatCodeOrder(code))
            .FirstOrDefault();

    /// <summary>Thứ tự của mã thống kê (<c>M7</c> → 7); null nếu không đúng dạng.</summary>
    public static int? StatCodeOrder(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        var match = StatCodePattern.Match(code.Trim().ToUpperInvariant());
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>Chuẩn hóa mã thống kê (chữ hoa); rỗng → null; sai dạng → <see cref="ArgumentException"/>.</summary>
    public static string? NormalizeStatCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        var normalized = code.Trim().ToUpperInvariant();
        if (StatCodeOrder(normalized) is not { } order || order < 1)
            throw new ArgumentException(
                $"Mã chức danh thống kê \"{code.Trim()}\" không hợp lệ. Hãy nhập dạng M1…M26 theo Mẫu 15A/15B hoặc để trống.");
        return "M" + order.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Tiền tố chức danh của người giữ chức vụ dạng "Quyền" (được giao phụ trách).</summary>
    public const string ActingPrefix = "Quyền ";

    /// <summary>
    /// Chức danh hiển thị: <c>isActing</c> → "Quyền " + tên chức vụ. Mọi nơi ghép chức danh của người đều phải dùng hàm này.
    /// </summary>
    public static string DisplayName(string positionName, bool isActing)
    {
        var name = positionName?.Trim() ?? string.Empty;
        return isActing && name.Length > 0 ? ActingPrefix + name : name;
    }

    /// <summary>Chức danh hiển thị của bản ghi chức vụ (rỗng nếu chưa nạp chức vụ).</summary>
    public static string DisplayName(MemberPosition memberPosition)
        => DisplayName(memberPosition.Position?.Name ?? string.Empty, memberPosition.IsActing);

    /// <summary>Chỗ thay tên đơn vị trong mẫu chức danh khi in (<see cref="Position.FormTitle"/>).</summary>
    public const string UnitPlaceholder = "{don_vi}";

    /// <summary>Biểu mẫu không in tiền tố "Quyền " (màn hình quản trị vẫn hiện); đổi cách in chỉ cần sửa ở đây.</summary>
    public const bool PrintActingPrefix = false;

    private static readonly Regex Spaces = new(@"\s+", RegexOptions.CultureInvariant);

    /// <summary>
    /// Chức danh in trên biểu mẫu: mẫu chức danh (trống → tên chức vụ) với <see cref="UnitPlaceholder"/> thay bằng tên đơn vị;
    /// đơn vị trống thì bỏ chỗ thay và khoảng trắng thừa.
    /// </summary>
    public static string FormTitle(string positionName, string? formTitle, string? unitName)
        => FillUnit(string.IsNullOrWhiteSpace(formTitle) ? positionName : formTitle, unitName);

    /// <summary>Chức danh in của bản ghi chức vụ (cần nạp Position, PartyCell, Department).</summary>
    public static string FormTitle(MemberPosition memberPosition)
        => DisplayName(FormTitle(memberPosition.Position?.Name ?? string.Empty, memberPosition.Position?.FormTitle, UnitName(memberPosition)),
            memberPosition.IsActing && PrintActingPrefix);

    /// <summary>Tên tổ chức Đảng hoặc đơn vị chính quyền gắn với bản ghi chức vụ.</summary>
    public static string? UnitName(MemberPosition memberPosition)
        => new[] { memberPosition.PartyCell?.Name, memberPosition.Department?.Name }.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))?.Trim();

    private static string FillUnit(string? template, string? unitName)
        => Spaces.Replace((template ?? string.Empty).Replace(UnitPlaceholder, unitName?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase), " ").Trim();

    /// <summary>
    /// Chức danh in của một người theo thứ tự đầu vào. Giữ "Quyền X" và cấp phó "Phó X" ở cùng đơn vị thì bỏ "Quyền X",
    /// chỉ in chức danh cấp phó (bản ghi "Quyền X" gắn vào <see cref="PrintedPosition.Acting"/>).
    /// </summary>
    public static List<PrintedPosition> FormTitles(IEnumerable<MemberPosition> positions)
    {
        var list = (positions ?? Enumerable.Empty<MemberPosition>()).Where(mp => mp.Position != null).ToList();
        var actingOf = new Dictionary<MemberPosition, MemberPosition>();
        foreach (var acting in list.Where(mp => mp.IsActing))
        {
            var deputy = list.FirstOrDefault(d => d != acting && !d.IsActing && !actingOf.ContainsKey(d)
                && IsDeputyOf(d.Position!.Name, acting.Position!.Name)
                && d.PartyCellId == acting.PartyCellId && d.DepartmentId == acting.DepartmentId);
            if (deputy != null)
                actingOf[deputy] = acting;
        }

        return list
            .Where(mp => !actingOf.ContainsValue(mp))
            .Select(mp => new PrintedPosition(mp, mp.Position!.Side, FormTitle(mp), actingOf.GetValueOrDefault(mp)))
            .ToList();
    }

    /// <summary>"Phó " + tên chức vụ kia, không phân biệt hoa thường.</summary>
    public static bool IsDeputyOf(string deputyName, string positionName)
        => !string.IsNullOrWhiteSpace(positionName)
            && string.Equals(Spaces.Replace(deputyName?.Trim() ?? string.Empty, " "), "Phó " + Spaces.Replace(positionName.Trim(), " "),
                StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Chức danh in một ô (Mẫu 01/02/10/17, ô ký): chức danh in của chức vụ chính; chức danh trên hồ sơ cán bộ đã sửa tay khác chức vụ chính
    /// hoặc không có chức vụ chính thì giữ chức danh trên hồ sơ.
    /// </summary>
    public static string? PrimaryFormTitle(IEnumerable<MemberPosition>? positions, string? profileTitle)
    {
        var printed = FormTitles(positions ?? Enumerable.Empty<MemberPosition>());
        var primary = printed.FirstOrDefault(p => p.Source.IsPrimary) ?? printed.FirstOrDefault(p => p.Acting?.IsPrimary == true);
        var title = string.IsNullOrWhiteSpace(profileTitle) ? null : profileTitle.Trim();
        if (primary == null)
            return title;
        var primarySource = primary.Source.IsPrimary ? primary.Source : primary.Acting!;
        // Chức danh hồ sơ đồng bộ theo chức vụ chính; khác đi là do người quản lý sửa tay.
        if (title != null && !string.Equals(title, DisplayName(primarySource), StringComparison.OrdinalIgnoreCase))
            return title;
        return primary.Title;
    }

    /// <summary>Chức danh in của mọi chức vụ, theo bên (Đảng, Chính quyền, Đoàn thể, Khác) rồi thứ tự danh mục, ngăn cách bằng dấu phẩy.</summary>
    public static string JoinFormTitles(IEnumerable<MemberPosition> positions)
        => string.Join(", ", FormTitles((positions ?? Enumerable.Empty<MemberPosition>()).Where(p => p.Position != null)
                .OrderBy(p => (int)p.Position!.Side).ThenBy(p => p.Position!.SortOrder))
            .Select(p => p.Title).Where(t => t.Length > 0).Distinct());

    /// <summary>Chức vụ "Quyền" có mã thống kê, thẩm quyền như chính chức vụ; tên là chức danh hiển thị.</summary>
    public static HeldPosition Held(MemberPosition memberPosition)
        => new(DisplayName(memberPosition), memberPosition.Position?.StatCode, memberPosition.Position?.DefaultApprovalAuthority,
            memberPosition.Position?.IsHead ?? false, memberPosition.Position?.DefaultWorkflowProfileCode, memberPosition.IsPrimary);

    /// <summary>Tách tiền tố "Quyền " / "Q. " (không phân biệt hoa thường) khỏi chức vụ nhập dạng chữ.</summary>
    public static bool TryStripActingPrefix(string text, out string positionName)
    {
        var value = (text ?? string.Empty).Normalize(System.Text.NormalizationForm.FormC).Trim();
        foreach (var prefix in new[] { "Quyền ", "Q." })
        {
            if (value.Length > prefix.Length && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var rest = value[prefix.Length..].Trim();
                if (rest.Length > 0)
                {
                    positionName = rest;
                    return true;
                }
            }
        }
        positionName = value;
        return false;
    }

    public static string SideName(PositionSide side) => side switch
    {
        PositionSide.Party => "Đảng",
        PositionSide.Administrative => "Chính quyền",
        PositionSide.MassOrganization => "Đoàn thể",
        _ => "Khác"
    };

    public static string AuthorityName(ApprovalAuthority authority) => authority == ApprovalAuthority.CapTren
        ? "Cấp ủy cấp trên quyết định"
        : "Đảng ủy cơ sở quyết định";
}
