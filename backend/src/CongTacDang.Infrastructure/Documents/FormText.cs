using System;
using System.Globalization;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Infrastructure.Documents;

public static class FormText
{
    /// <summary>Dấu phẩy thập phân như chữ in sẵn "70,0" trong template.</summary>
    private static readonly NumberFormatInfo VietnameseNumbers = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        PercentDecimalSeparator = ",",
        PercentGroupSeparator = "."
    };

    public static string Number(double value, int decimals = 1) =>
        value.ToString("F" + decimals, VietnameseNumbers);

    /// <summary>Null → giữ chữ mặc định của template.</summary>
    public static string? Number(double? value, int decimals = 1) =>
        value.HasValue ? Number(value.Value, decimals) : null;

    /// <summary>Tỷ lệ 0..1 → "90%".</summary>
    public static string Percent(double ratio) =>
        Math.Round(ratio * 100).ToString("F0", VietnameseNumbers) + "%";


    public static string Year(int year) => year.ToString(CultureInfo.InvariantCulture);

    public static string Date(DateTime date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>Null → giữ chữ mặc định của template.</summary>
    public static string? OrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>Khi template đặt chỗ điền liền sau nhãn ("Quý", "Chi bộ:").</summary>
    public static string? Spaced(string? value) => OrNull(value) is { } text ? " " + text : null;

    public static string Grade(EvaluationGrade grade) => grade switch
    {
        EvaluationGrade.HoanThanhXuatSac => "Hoàn thành xuất sắc nhiệm vụ",
        EvaluationGrade.HoanThanhTot => "Hoàn thành tốt nhiệm vụ",
        EvaluationGrade.HoanThanh => "Hoàn thành nhiệm vụ",
        EvaluationGrade.KhongHoanThanh => "Không hoàn thành nhiệm vụ",
        _ => "Chưa xếp loại"
    };
}
