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

    /// <summary>Mã ngôn ngữ vi-VN đầu định dạng ô: bản in hiện dấu phẩy thập phân mà ô vẫn là số để tính.</summary>
    private const string VietnameseLocale = "[$-42A]";

    /// <summary>Chiều cao một dòng chữ cỡ 11 (pt).</summary>
    private const double LineHeight = 15;

    /// <summary>Định dạng số với <paramref name="decimals"/> chữ số thập phân, dấu phẩy kiểu Việt Nam.</summary>
    public static string NumberFormat(int decimals) =>
        VietnameseLocale + (decimals <= 0 ? "0" : "0." + new string('0', decimals));

    /// <summary>Điểm: số nguyên in "30", số lẻ một chữ số "29,5" (như bản Word); null → ô trống.</summary>
    public static void SetScore(IXLCell cell, double? value)
    {
        if (value is not { } number)
            return;
        cell.Value = number;
        cell.Style.NumberFormat.Format = NumberFormat(number % 1 == 0 ? 0 : 1);
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    /// <summary>Khổ A4, lề hẹp, vừa một trang theo chiều ngang (chiều dọc không giới hạn), lặp dòng tiêu đề bảng ở mỗi trang.</summary>
    public static void PrintSetup(IXLWorksheet ws, bool landscape, int? repeatFirstRow = null, int? repeatLastRow = null)
    {
        ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
        ws.PageSetup.PageOrientation = landscape ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
        ws.PageSetup.FitToPages(1, 0);
        ws.PageSetup.Margins.SetLeft(0.4).SetRight(0.4).SetTop(0.5).SetBottom(0.5).SetHeader(0.3).SetFooter(0.3);
        ws.PageSetup.CenterHorizontally = true;
        if (repeatFirstRow is { } first)
            ws.PageSetup.SetRowsToRepeatAtTop(first, repeatLastRow ?? first);
    }

    /// <summary>Trang tính chỉ để xem: vùng in là một ô trống nên khi in/chuyển PDF không ra trang nào; dữ liệu giữ nguyên.</summary>
    public static void ExcludeFromPrint(IXLWorksheet ws)
    {
        var lastColumn = ws.LastColumnUsed()?.ColumnNumber() ?? 1;
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        ws.PageSetup.PrintAreas.Clear();
        ws.PageSetup.PrintAreas.Add(lastRow + 2, lastColumn + 2, lastRow + 2, lastColumn + 2);
    }

    /// <summary>
    /// Chiều cao từng dòng theo chữ: Excel/LibreOffice không tự giãn ô gộp, còn chiều cao cố định thì cắt chữ xuống dòng.
    /// Ước số dòng theo độ dài chữ và bề rộng cột (ô gộp cộng các cột); gọi sau khi đã đặt độ rộng cột và ghi xong dữ liệu.
    /// </summary>
    public static void FitRowHeights(IXLWorksheet ws)
    {
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        var tall = new List<(IXLRange Range, double Height)>();
        for (var r = 1; r <= lastRow; r++)
        {
            double height = 0;
            foreach (var cell in ws.Row(r).CellsUsed())
            {
                var text = cell.GetFormattedString();
                if (string.IsNullOrWhiteSpace(text))
                    continue;
                var range = cell.IsMerged() ? cell.MergedRange() : null;
                if (range != null && range.FirstColumn().ColumnNumber() != cell.Address.ColumnNumber)
                    continue;
                double width = 0;
                var firstColumn = range?.FirstColumn().ColumnNumber() ?? cell.Address.ColumnNumber;
                var lastColumn = range?.LastColumn().ColumnNumber() ?? cell.Address.ColumnNumber;
                for (var c = firstColumn; c <= lastColumn; c++)
                    width += ws.Column(c).Width;
                var font = cell.Style.Font;
                var lines = cell.Style.Alignment.WrapText ? WrappedLines(text, width, font.FontSize, font.Bold) : 1;
                var needed = lines * LineHeight * font.FontSize / 11 + 2;
                // Ô gộp nhiều dòng (tiêu đề cột hai tầng) chia chiều cao cho các dòng của vùng gộp, xét sau.
                if (range != null && range.RowCount() > 1)
                    tall.Add((range, needed));
                else
                    height = Math.Max(height, needed);
            }
            if (height > LineHeight + 2 && height > ws.Row(r).Height)
                ws.Row(r).Height = height;
        }
        foreach (var (range, needed) in tall)
        {
            var first = range.FirstRow().RowNumber();
            var last = range.LastRow().RowNumber();
            double have = 0;
            for (var r = first; r <= last; r++)
                have += ws.Row(r).Height;
            if (needed > have)
                ws.Row(last).Height += needed - have;
        }
    }

    /// <summary>Số dòng khi ngắt theo từ; ước một ký tự ≈ một đơn vị độ rộng cột ở cỡ 11 (chữ đậm rộng hơn).</summary>
    private static int WrappedLines(string text, double width, double fontSize, bool bold)
    {
        var capacity = Math.Max(4, (width - 1) * 11 / Math.Max(6, fontSize) * (bold ? 0.88 : 1.0));
        var lines = 0;
        foreach (var paragraph in text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            var used = 0.0;
            var count = 1;
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                double length = word.Length;
                if (used > 0 && used + 1 + length > capacity)
                {
                    count++;
                    used = 0;
                }
                // Từ dài hơn một dòng bị ngắt giữa từ.
                while (length > capacity)
                {
                    count++;
                    length -= capacity;
                }
                used += (used > 0 ? 1 : 0) + length;
            }
            lines += count;
        }
        return Math.Max(1, lines);
    }

    /// <summary>Dòng 1 là số mẫu (phải); từ dòng 2 khối trái và khối phải; trả về dòng kế tiếp khối dài hơn.</summary>
    public static int WriteHeader(IXLWorksheet ws, int lastColumn, string formLabel, int leftColumns,
        IReadOnlyList<ExcelHeaderLine> left, IReadOnlyList<ExcelHeaderLine> right)
    {
        ws.Cell(1, lastColumn).Value = formLabel;
        ws.Cell(1, lastColumn).Style.Font.Bold = true;
        ws.Cell(1, lastColumn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        var rightStart = Math.Max(leftColumns + 2, lastColumn - 4);
        void Block(IReadOnlyList<ExcelHeaderLine> lines, int first, int last, bool oneLine)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                ws.Cell(2 + i, first).Value = oneLine ? lines[i].Text.Replace('\n', ' ') : lines[i].Text;
                var style = ws.Range(2 + i, first, 2 + i, last).Merge().Style
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetWrapText(!oneLine)
                    .Font.SetBold(lines[i].Bold).Font.SetItalic(lines[i].Italic);
                // Tên tổ chức in một dòng: không ngắt chữ, cột hẹp thì thu nhỏ cho vừa.
                if (oneLine)
                    style.Alignment.SetShrinkToFit(true);
            }
        }
        // Khối trái gộp hết các cột trước khối phải để tên tổ chức dài vẫn nằm trên một dòng.
        Block(left, 1, rightStart - 1, oneLine: true);
        Block(right, rightStart, lastColumn, oneLine: false);
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
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetWrapText(true)
            .Font.SetBold(bold).Font.SetFontSize(bold ? 13 : 11);
    }

    /// <summary>Dòng chữ gộp hết bề ngang, căn trái, tự xuống dòng (chiều cao đặt ở <see cref="FitRowHeights"/>).</summary>
    public static void TextRow(IXLWorksheet ws, int row, int lastColumn, string text, bool bold = false, bool italic = false)
    {
        ws.Cell(row, 1).Value = text;
        ws.Range(row, 1, row, lastColumn).Merge().Style
            .Alignment.SetWrapText(true).Alignment.SetVertical(XLAlignmentVerticalValues.Top)
            .Font.SetBold(bold).Font.SetItalic(italic);
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
        var format = NumberFormat(comma < 0 ? 0 : digits.Length - comma - 1);
        cell.Value = percent ? number / 100 : number;
        cell.Style.NumberFormat.Format = percent ? format + "%" : format;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    /// <summary>Đọc số theo cách in của <see cref="FormText"/>; không phải số → false.</summary>
    public static bool TryParse(string? value, out double number) =>
        double.TryParse(value?.Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, VietnameseNumbers, out number);
}
