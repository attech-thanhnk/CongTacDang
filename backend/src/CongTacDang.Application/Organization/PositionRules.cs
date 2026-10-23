using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.Organization;

/// <summary>Chức vụ đang hiệu lực của một người — đủ để suy ra thẩm quyền, mã thống kê và hồ sơ luồng gợi ý.</summary>
/// <param name="IsHead">Không tính vào trần Hoàn thành xuất sắc.</param>
/// <param name="DefaultWorkflowProfileCode">Mã hồ sơ luồng gợi ý của chức vụ (null = theo thẩm quyền).</param>
public sealed record HeldPosition(string PositionName, string? StatCode, ApprovalAuthority? DefaultApprovalAuthority, bool IsHead = false,
    string? DefaultWorkflowProfileCode = null, bool IsPrimary = false);

/// <summary>Quy tắc suy ra từ chức vụ: thẩm quyền phê duyệt, mã thống kê, chức danh hiển thị.</summary>
public static class PositionRules
{
    public const int MaxNameLength = 200;

    private static readonly Regex StatCodePattern = new("^M([0-9]{1,3})$", RegexOptions.CultureInvariant);

    /// <summary><c>CapTren</c> nếu có chức vụ đang hiệu lực mặc định <c>CapTren</c> (giữ nhiều chức danh), ngược lại <c>CoSo</c>.</summary>
    public static ApprovalAuthority DeriveApprovalAuthority(IEnumerable<HeldPosition> activePositions)
        => activePositions.Any(p => p.DefaultApprovalAuthority == ApprovalAuthority.CapTren)
            ? ApprovalAuthority.CapTren
            : ApprovalAuthority.CoSo;

    /// <summary>
    /// Hồ sơ luồng gợi ý theo chức vụ, null nếu không có. Chỉ xét chức vụ có mã hồ sơ luồng mà kỳ có
    /// (<paramref name="profileExists"/>) và thẩm quyền mặc định trống hoặc trùng thẩm quyền của người (hồ sơ luồng phải khớp cấp
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

    /// <summary>Hồ sơ luồng mặc định khi thêm người vào kỳ: theo chức vụ nếu có gợi ý hợp lệ, ngược lại theo cấp quyết định.</summary>
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
