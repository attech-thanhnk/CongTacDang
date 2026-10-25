using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Mã lĩnh vực chuyên môn trên phiếu đối soát.</summary>
public static class CrosscheckFields
{
    public const string SafetyQuality = "SAFETY_QUALITY";
    public const string CnsTechnical = "CNS_TECHNICAL";
    public const string Atm = "ATM";
    public const string PlanningProject = "PLANNING_PROJECT";
    public const string Finance = "FINANCE";
    public const string LegalInspectionAudit = "LEGAL_INSPECTION_AUDIT";

    public static readonly IReadOnlyList<string> All = new[]
    {
        SafetyQuality, CnsTechnical, Atm, PlanningProject, Finance, LegalInspectionAudit
    };

    public static string DisplayName(string code) => code switch
    {
        SafetyQuality => "An toàn - chất lượng",
        CnsTechnical => "Kỹ thuật CNS",
        Atm => "Quản lý không lưu",
        PlanningProject => "Kế hoạch - đầu tư, dự án",
        Finance => "Tài chính",
        LegalInspectionAudit => "Pháp chế, thanh tra, kiểm toán",
        _ => code
    };

    public static bool IsKnown(string? code) => code != null && All.Contains(code);
}

/// <summary>Quy tắc thuần của phiếu đối soát chuyên ngành (Mẫu 03).</summary>
public static class CrosscheckRules
{
    public const int MaxNameLength = 500;
    public const int MaxTextLength = 4000;
    public const int MaxReasonLength = 2000;
    public const int MaxItems = 50;

    private static readonly NumberFormatInfo Vietnamese = new() { NumberDecimalSeparator = "," };

    /// <summary>Chưa xác nhận và chưa hủy: chặn hoàn thành thẩm định.</summary>
    public static bool IsOpen(CrosscheckStatus status) =>
        status is CrosscheckStatus.Requested or CrosscheckStatus.InProgress or CrosscheckStatus.Submitted;

    public static string StatusName(CrosscheckStatus status) => status switch
    {
        CrosscheckStatus.Requested => "Chờ lập phiếu",
        CrosscheckStatus.InProgress => "Đang lập",
        CrosscheckStatus.Submitted => "Chờ xác nhận",
        CrosscheckStatus.Confirmed => "Đã xác nhận",
        CrosscheckStatus.Cancelled => "Đã hủy",
        _ => status.ToString()
    };

    public static string ResultName(CrosscheckResult result) => result switch
    {
        CrosscheckResult.Pass => "Đạt chuẩn",
        CrosscheckResult.Exceed => "Vượt chuẩn",
        CrosscheckResult.Defect => "Có khiếm khuyết",
        _ => result.ToString()
    };

    public static string AdjustmentName(CrosscheckAdjustment type) => type switch
    {
        CrosscheckAdjustment.None => "Không đề xuất",
        CrosscheckAdjustment.Decrease => "Giảm điểm",
        CrosscheckAdjustment.RecognizeExceed => "Ghi nhận vượt chuẩn",
        _ => type.ToString()
    };

    public static List<string> FieldsFromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    public static string FieldsToJson(IEnumerable<string> fields) => JsonSerializer.Serialize(fields.ToList());

    /// <summary>Hai phiếu trùng lĩnh vực khi có ít nhất một mã chung.</summary>
    public static bool FieldsOverlap(string? a, string? b) => FieldsFromJson(a).Intersect(FieldsFromJson(b)).Any();

    /// <summary>
    /// Một dòng đã sẵn sàng để gửi: không áp dụng thì bỏ qua; ngược lại phải có kết luận; khiếm khuyết hoặc có đề xuất điểm
    /// phải nêu căn cứ; giảm điểm phải có số điểm dương. Trả null nếu hợp lệ.
    /// </summary>
    public static string? ValidateItemForSubmit(ExpertCrosscheckItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var label = string.IsNullOrWhiteSpace(item.ItemName) ? $"dòng {item.Order}" : $"\"{item.ItemName}\"";
        if (item.NotApplicable)
            return null;
        if (item.Result is null)
            return $"Hãy chọn kết luận đối soát (đạt chuẩn, vượt chuẩn hoặc có khiếm khuyết) cho {label}.";
        if (string.IsNullOrWhiteSpace(item.SourceData))
            return $"Hãy nhập kết quả đối soát với dữ liệu gốc cho {label}.";
        if (item.AdjustmentType == CrosscheckAdjustment.Decrease && item.AdjustmentPoints is not > 0)
            return $"Hãy nhập số điểm đề xuất giảm cho {label}.";
        if (item.AdjustmentType != CrosscheckAdjustment.Decrease && item.AdjustmentPoints.HasValue)
            return $"Chỉ nhập số điểm khi đề xuất giảm điểm ({label}).";
        if ((item.Result == CrosscheckResult.Defect || item.AdjustmentType != CrosscheckAdjustment.None) && !HasBasis(item))
            return $"Hãy nêu căn cứ (nội dung hoặc số văn bản) cho {label}: dòng có khiếm khuyết hoặc có đề xuất điểm phải có căn cứ.";
        return null;
    }

    public static bool HasBasis(ExpertCrosscheckItem item) =>
        !string.IsNullOrWhiteSpace(item.BasisText) || !string.IsNullOrWhiteSpace(item.BasisDocNumber);

    /// <summary>Chuỗi trên cột "Đề xuất tăng/giảm điểm": "0", "−1,5đ" hoặc "+ ghi nhận vượt chuẩn"; dòng không áp dụng trả "K/AD".</summary>
    public static string FormatAdjustment(ExpertCrosscheckItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.NotApplicable)
            return "K/AD";
        return item.AdjustmentType switch
        {
            CrosscheckAdjustment.Decrease => "−" + (item.AdjustmentPoints ?? 0).ToString("0.##", Vietnamese) + "đ",
            CrosscheckAdjustment.RecognizeExceed => "+ ghi nhận vượt chuẩn",
            _ => "0"
        };
    }
}
