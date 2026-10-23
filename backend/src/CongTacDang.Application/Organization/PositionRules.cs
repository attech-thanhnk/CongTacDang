using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Organization;

/// <summary>Chức vụ đang hiệu lực của một người — dữ liệu tối thiểu để suy ra thẩm quyền và mã thống kê.</summary>
/// <param name="PositionName">Tên chức vụ.</param>
/// <param name="StatCode">Mã chức danh thống kê (M1…M26) của chức vụ.</param>
/// <param name="DefaultApprovalAuthority">Thẩm quyền mặc định của chức vụ.</param>
/// <param name="IsHead">Chức vụ người đứng đầu (không tính vào trần tỷ lệ Hoàn thành xuất sắc).</param>
public sealed record HeldPosition(string PositionName, string? StatCode, ApprovalAuthority? DefaultApprovalAuthority, bool IsHead = false);

/// <summary>
/// Quy tắc suy ra từ chức vụ (HD03):
/// <list type="bullet">
/// <item>Thẩm quyền phê duyệt: <c>CapTren</c> khi ít nhất một chức vụ đang hiệu lực có thẩm quyền mặc định <c>CapTren</c>,
/// ngược lại <c>CoSo</c> (tr.6: "giữ 2 chức danh … thì cấp ủy cấp trên quyết định cuối cùng").</item>
/// <item>Mã thống kê của người (Mẫu 15A/15B): mã nhỏ nhất trong các chức vụ đang hiệu lực
/// (tr.74: "ưu tiên thống kê theo nhóm chức danh có thứ tự đứng trước").</item>
/// </list>
/// </summary>
public static class PositionRules
{
    /// <summary>Độ dài tối đa tên chức vụ.</summary>
    public const int MaxNameLength = 200;

    private static readonly Regex StatCodePattern = new("^M([0-9]{1,3})$", RegexOptions.CultureInvariant);

    /// <summary>Thẩm quyền suy ra từ các chức vụ đang hiệu lực.</summary>
    public static ApprovalAuthority DeriveApprovalAuthority(IEnumerable<HeldPosition> activePositions)
        => activePositions.Any(p => p.DefaultApprovalAuthority == ApprovalAuthority.CapTren)
            ? ApprovalAuthority.CapTren
            : ApprovalAuthority.CoSo;

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

    /// <summary>
    /// Chuẩn hóa mã thống kê (cắt khoảng trắng, chữ hoa); rỗng → null. Sai dạng → <see cref="ArgumentException"/>
    /// với thông báo tiếng Việt.
    /// </summary>
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
    /// Chức danh hiển thị của một chức vụ đang giữ: <c>isActing</c> → "Quyền " + tên chức vụ (giữ nguyên chữ đầu của tên,
    /// ví dụ "Quyền Trưởng trung tâm"); ngược lại là tên chức vụ. Mọi nơi ghép tên chức vụ thành chức danh của người đều dùng hàm này.
    /// </summary>
    public static string DisplayName(string positionName, bool isActing)
    {
        var name = positionName?.Trim() ?? string.Empty;
        return isActing && name.Length > 0 ? ActingPrefix + name : name;
    }

    /// <summary>Chức danh hiển thị của bản ghi chức vụ (rỗng nếu chưa nạp chức vụ).</summary>
    public static string DisplayName(MemberPosition memberPosition)
        => DisplayName(memberPosition.Position?.Name ?? string.Empty, memberPosition.IsActing);

    /// <summary>
    /// Dữ liệu suy ra của bản ghi chức vụ: chức vụ "Quyền" có mã thống kê, thẩm quyền như chính chức vụ đó; tên là chức danh hiển thị.
    /// </summary>
    public static HeldPosition Held(MemberPosition memberPosition)
        => new(DisplayName(memberPosition), memberPosition.Position?.StatCode, memberPosition.Position?.DefaultApprovalAuthority,
            memberPosition.Position?.IsHead ?? false);

    /// <summary>
    /// Tách tiền tố "Quyền " / "Q. " (không phân biệt hoa thường) khỏi chức vụ ghi dạng chữ (nhập Excel):
    /// trả về true và phần tên chức vụ còn lại khi có tiền tố; ngược lại false và chuỗi đã cắt khoảng trắng.
    /// </summary>
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

    /// <summary>Tên hiển thị của bên chức vụ.</summary>
    public static string SideName(PositionSide side) => side switch
    {
        PositionSide.Party => "Đảng",
        PositionSide.Administrative => "Chính quyền",
        PositionSide.MassOrganization => "Đoàn thể",
        _ => "Khác"
    };

    /// <summary>Tên hiển thị của thẩm quyền.</summary>
    public static string AuthorityName(ApprovalAuthority authority) => authority == ApprovalAuthority.CapTren
        ? "Cấp ủy cấp trên quyết định"
        : "Đảng ủy cơ sở quyết định";
}
