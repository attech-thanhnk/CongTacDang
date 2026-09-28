using System;
using System.Globalization;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Infrastructure.Documents;

/// <summary>Định dạng giá trị đã lưu thành chữ trên biểu mẫu (dùng chung cho mọi lớp dữ liệu mẫu).</summary>
public static class FormText
{
    /// <summary>Số theo cách viết tiếng Việt: dấu phẩy thập phân (giống chữ in sẵn "70,0" trong template).</summary>
    private static readonly NumberFormatInfo VietnameseNumbers = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        PercentDecimalSeparator = ",",
        PercentGroupSeparator = "."
    };

    /// <summary>Định dạng số với số chữ số thập phân cố định.</summary>
    public static string Number(double value, int decimals = 1) =>
        value.ToString("F" + decimals, VietnameseNumbers);

    /// <summary>Định dạng số, null nếu không có giá trị (giữ chữ mặc định của template).</summary>
    public static string? Number(double? value, int decimals = 1) =>
        value.HasValue ? Number(value.Value, decimals) : null;

    /// <summary>Tỷ lệ 0..1 thành phần trăm làm tròn, ví dụ 0.9 → "90%".</summary>
    public static string Percent(double ratio) =>
        Math.Round(ratio * 100).ToString("F0", VietnameseNumbers) + "%";

    /// <summary>Số quý (1-4).</summary>
    public static string Quarter(EvaluationQuarter quarter) => ((int)quarter).ToString(CultureInfo.InvariantCulture);

    /// <summary>Năm.</summary>
    public static string Year(int year) => year.ToString(CultureInfo.InvariantCulture);

    /// <summary>Ngày dạng dd/MM/yyyy.</summary>
    public static string Date(DateTime date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>Chuỗi rỗng/khoảng trắng thành null (giữ chữ mặc định của template).</summary>
    public static string? OrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>Tên mức xếp loại.</summary>
    public static string Grade(EvaluationGrade grade) => grade switch
    {
        EvaluationGrade.HoanThanhXuatSac => "Hoàn thành xuất sắc nhiệm vụ",
        EvaluationGrade.HoanThanhTot => "Hoàn thành tốt nhiệm vụ",
        EvaluationGrade.HoanThanh => "Hoàn thành nhiệm vụ",
        EvaluationGrade.KhongHoanThanh => "Không hoàn thành nhiệm vụ",
        _ => "Chưa xếp loại"
    };
}
