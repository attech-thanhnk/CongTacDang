using System;
using System.Collections.Generic;
using System.Globalization;
using ClosedXML.Excel;
using CongTacDang.Infrastructure.Documents.Forms;

namespace CongTacDang.Infrastructure.Documents;

/// <summary>Một dòng của khối tiêu đề trái/phải trên bảng tính biểu mẫu.</summary>
public sealed record ExcelHeaderLine(string Text, bool Bold = false, bool Italic = false);

/// <summary>Bố cục dùng chung của biểu mẫu lập trên Excel: tiêu đề hai bên, dòng tiêu đề, bảng, dòng ký.</summary>
public static class ExcelFormLayout
{
    /// <summary>Số đọc theo cách in của <see cref="FormText"/> (dấu phẩy thập phân).</summary>
    private static readonly NumberFormatInfo VietnameseNumbers = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

    /// <summary>Dòng 1 là số mẫu (phải); từ dòng 2 khối trái và khối phải; trả về dòng kế tiếp khối dài hơn.</summary>
    public static int WriteHeader(IXLWorksheet ws, int lastColumn, string formLabel, int leftColumns,
        IReadOnlyList<ExcelHeaderLine> left, IReadOnlyList<ExcelHeaderLine> right)
    {
        ws.Cell(1, lastColumn).Value = formLabel;
        ws.Cell(1, lastColumn).Style.Font.Bold = true;
        ws.Cell(1, lastColumn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        var rightStart = Math.Max(leftColumns + 2, lastColumn - 4);
        void Block(IReadOnlyList<ExcelHeaderLine> lines, int first, int last)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                ws.Cell(2 + i, first).Value = lines[i].Text;
                ws.Range(2 + i, first, 2 + i, last).Merge().Style
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Font.SetBold(lines[i].Bold).Font.SetItalic(lines[i].Italic);
            }
        }
        Block(left, 1, leftColumns);
        Block(right, rightStart, lastColumn);
        return 2 + Math.Max(left.Count, right.Count);
    }

    /// <summary>
    /// Tiêu đề trái theo tổ chức Đảng, phải "ĐẢNG CỘNG SẢN VIỆT NAM" và ngày lập; <paramref name="dateLine"/> ("ngày … tháng … năm …")
    /// như bản Word của mẫu, null → ngày hiện tại.
    /// </summary>
    public static void WriteFormHeader(IXLWorksheet ws, int lastColumn, string formLabel, PartyHeader header, string location, int leftColumns,
        string? dateLine = null) =>
        WriteHeader(ws, lastColumn, formLabel, leftColumns,
            new[] { new ExcelHeaderLine(header.Parent ?? "ĐẢNG BỘ …"), new ExcelHeaderLine(header.Organization ?? "ĐẢNG ỦY (CHI BỘ) …", Bold: true) },
            new[]
            {
                new ExcelHeaderLine("ĐẢNG CỘNG SẢN VIỆT NAM", Bold: true),
                new ExcelHeaderLine(dateLine != null ? $"{location}, {dateLine}" : TodayLine(location), Italic: true)
            });

    /// <summary>"Địa danh, ngày … tháng … năm …" theo ngày hiện tại.</summary>
    public static string TodayLine(string location) =>
        $"{location}, ngày {DateTime.Now:dd} tháng {DateTime.Now:MM} năm {DateTime.Now:yyyy}";

    public static void TitleRow(IXLWorksheet ws, int row, int lastColumn, string text, bool bold)
    {
        ws.Cell(row, 1).Value = text;
        ws.Range(row, 1, row, lastColumn).Merge().Style
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Font.SetBold(bold).Font.SetFontSize(bold ? 13 : 11);
    }

    /// <summary>Dòng chữ gộp hết bề ngang, căn trái, tự xuống dòng.</summary>
    public static void TextRow(IXLWorksheet ws, int row, int lastColumn, string text, bool bold = false, bool italic = false)
    {
        ws.Cell(row, 1).Value = text;
        ws.Range(row, 1, row, lastColumn).Merge().Style
            .Alignment.SetWrapText(true).Alignment.SetVertical(XLAlignmentVerticalValues.Top)
            .Font.SetBold(bold).Font.SetItalic(italic);
        // Ô gộp không tự giãn chiều cao theo chữ nên ước số dòng theo bề rộng các cột (đặt độ rộng cột trước khi gọi).
        double width = 0;
        for (var c = 1; c <= lastColumn; c++)
            width += ws.Column(c).Width;
        var lines = Math.Ceiling(text.Length * 1.1 / Math.Max(10, width));
        ws.Row(row).Height = 15 * Math.Max(1, lines);
    }

    /// <summary>Hai cặp nhãn – giá trị trên một dòng (nửa trái, nửa phải) như dòng thông tin cán bộ của biểu mẫu Word.</summary>
    public static void InfoRow(IXLWorksheet ws, int row, int lastColumn, string left, string? right = null)
    {
        var half = right == null ? lastColumn : Math.Max(1, lastColumn / 2);
        ws.Cell(row, 1).Value = left;
        ws.Range(row, 1, row, half).Merge().Style.Alignment.SetWrapText(true);
        if (right == null)
            return;
        ws.Cell(row, half + 1).Value = right;
        ws.Range(row, half + 1, row, lastColumn).Merge().Style.Alignment.SetWrapText(true);
    }

    public static void StyleHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Alignment.WrapText = true;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.LastRow().Style.Font.Bold = false;
        range.LastRow().Style.Font.Italic = true;
    }

    /// <summary>Dòng tiêu đề bảng một hàng (không có hàng đánh số cột).</summary>
    public static void StyleHeaderRow(IXLRange range)
    {
        StyleHeader(range);
        range.Style.Font.Bold = true;
        range.Style.Font.Italic = false;
    }

    /// <summary>Kẻ khung, tự xuống dòng, căn trên cho vùng dữ liệu của bảng.</summary>
    public static void StyleBody(IXLRange range)
    {
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Alignment.WrapText = true;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        range.Worksheet.Rows(range.FirstRow().RowNumber(), range.LastRow().RowNumber()).AdjustToContents();
    }

    public static void SignatureRow(IXLWorksheet ws, int row, int lastColumn, string left, string leftNote, string right, string rightNote) =>
        Signatures(ws, row, lastColumn, (left, leftNote), (right, rightNote));

    /// <summary>Các khối ký chia đều bề ngang: dòng chức danh đậm, dòng ghi chú nghiêng.</summary>
    public static void Signatures(IXLWorksheet ws, int row, int lastColumn, params (string Title, string Note)[] blocks)
    {
        for (var i = 0; i < blocks.Length; i++)
        {
            var first = 1 + i * lastColumn / blocks.Length;
            var last = (i + 1) * lastColumn / blocks.Length;
            ws.Cell(row, first).Value = blocks[i].Title;
            ws.Cell(row + 1, first).Value = blocks[i].Note;
            ws.Range(row, first, row, last).Merge().Style.Font.SetBold(true).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetWrapText(true);
            ws.Range(row + 1, first, row + 1, last).Merge().Style.Font.SetItalic(true).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetWrapText(true);
        }
    }

    /// <summary>Ô chữ; null giữ ô trống.</summary>
    public static void SetText(IXLCell cell, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            cell.Value = value;
    }

    /// <summary>Số in bằng <see cref="FormText"/> ("26,5", "90%") ghi thành ô số giữ đúng số chữ số; chữ khác ghi nguyên văn.</summary>
    public static void SetNumber(IXLCell cell, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        var text = value.Trim();
        var percent = text.EndsWith('%');
        var digits = percent ? text[..^1].Trim() : text;
        if (!TryParse(digits, out var number))
        {
            cell.Value = value;
            return;
        }
        var comma = digits.IndexOf(',', StringComparison.Ordinal);
        var decimals = comma < 0 ? 0 : digits.Length - comma - 1;
        var format = decimals == 0 ? "0" : "0." + new string('0', decimals);
        cell.Value = percent ? number / 100 : number;
        cell.Style.NumberFormat.Format = percent ? format + "%" : format;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    /// <summary>Đọc số theo cách in của <see cref="FormText"/>; không phải số → false.</summary>
    public static bool TryParse(string? value, out double number) =>
        double.TryParse(value?.Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, VietnameseNumbers, out number);
}
