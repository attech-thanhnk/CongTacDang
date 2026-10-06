using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Imports;

namespace CongTacDang.Infrastructure.Imports;

/// <summary>
/// Đọc/ghi file Excel của chức năng nhập dữ liệu bằng ClosedXML. File mẫu: sheet "Dữ liệu" (tiêu đề cố định, cột bắt buộc có dấu *,
/// danh sách chọn cho cột có giá trị hợp lệ, định dạng chữ để giữ số 0 đầu) và sheet "Hướng dẫn".
/// </summary>
public sealed class ExcelImportWorkbook : IImportWorkbook
{
    private const string ListSheetName = "Danh mục";
    private const int TemplateRows = ImportLimits.MaxRows + 1;
    private static readonly XLColor RequiredFill = XLColor.FromHtml("#FDE9D9");
    private static readonly XLColor HeaderFill = XLColor.FromHtml("#E7EEF7");

    /// <inheritdoc />
    public IReadOnlyList<ImportRawRow> Read(Stream content, IReadOnlyList<ImportColumn> columns)
    {
        ArgumentNullException.ThrowIfNull(content);
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(content);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new ValidationException("Không đọc được file. Hãy kiểm tra file là Excel .xlsx (không đặt mật khẩu) và tải lên lại.");
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(w => string.Equals(w.Name.Trim(), ImportLimits.DataSheetName, StringComparison.OrdinalIgnoreCase))
                ?? workbook.Worksheets.FirstOrDefault(w => w.Visibility == XLWorksheetVisibility.Visible)
                ?? throw new ValidationException("File không có sheet dữ liệu.");

            var map = ReadHeader(sheet, columns);
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            var rows = new List<ImportRawRow>();
            for (var r = 2; r <= lastRow; r++)
            {
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                var any = false;
                foreach (var (key, col) in map)
                {
                    var text = CellText(sheet.Cell(r, col));
                    values[key] = text;
                    any |= text.Length > 0;
                }
                if (!any)
                    continue;
                if (rows.Count >= ImportLimits.MaxRows)
                    throw new ValidationException($"File có quá {ImportLimits.MaxRows} dòng dữ liệu. Hãy chia thành nhiều file.");
                rows.Add(new ImportRawRow(r, values));
            }
            return rows;
        }
    }

    /// <summary>Đối chiếu dòng tiêu đề với các cột: bỏ dấu * và khoảng trắng thừa, không phân biệt hoa thường/dấu.</summary>
    private static Dictionary<string, int> ReadHeader(IXLWorksheet sheet, IReadOnlyList<ImportColumn> columns)
    {
        var lastCol = sheet.Row(1).LastCellUsed()?.Address.ColumnNumber ?? 0;
        var headers = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var c = 1; c <= lastCol; c++)
        {
            var key = NormalizeHeader(CellText(sheet.Cell(1, c)));
            if (key.Length > 0)
                headers.TryAdd(key, c);
        }

        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        var missing = new List<string>();
        foreach (var column in columns)
        {
            if (headers.TryGetValue(NormalizeHeader(column.Header), out var index))
                map[column.Key] = index;
            else if (column.Required)
                missing.Add(column.Header);
        }

        if (map.Count == 0)
            throw new ValidationException("Dòng 1 của sheet dữ liệu không có tiêu đề cột đúng mẫu. Hãy dùng file mẫu tải về từ trang này.");
        if (missing.Count > 0)
            throw new ValidationException($"File thiếu cột bắt buộc: {string.Join(", ", missing)}. Hãy dùng file mẫu tải về từ trang này.");
        return map;
    }

    private static string NormalizeHeader(string header) => ImportValues.Fold(header.Replace("*", string.Empty));

    /// <summary>Giá trị ô dạng chữ: ngày → dd/MM/yyyy, số → không có phần thập phân thừa, Có/Không cho ô logic; cắt khoảng trắng.</summary>
    public static string CellText(IXLCell cell)
    {
        var value = cell.HasFormula ? cell.CachedValue : cell.Value;
        var text = value.Type switch
        {
            XLDataType.Blank => string.Empty,
            XLDataType.Text => value.GetText(),
            XLDataType.Number when IsDateFormat(cell) => DateTime.FromOADate(value.GetNumber()).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            XLDataType.Number => value.GetNumber().ToString("0.##########", CultureInfo.InvariantCulture),
            XLDataType.Boolean => value.GetBoolean() ? ImportValues.Yes : ImportValues.No,
            XLDataType.DateTime => value.GetDateTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            XLDataType.TimeSpan => value.GetTimeSpan().ToString(),
            _ => string.Empty
        };
        return text.Trim();
    }

    /// <summary>Ô số có định dạng ngày (mã định dạng dựng sẵn 14–22, 45–47 hoặc chuỗi định dạng có d/m/y, không phải giờ thuần).</summary>
    private static bool IsDateFormat(IXLCell cell)
    {
        var format = cell.Style.NumberFormat;
        if (format.NumberFormatId is >= 14 and <= 22 or >= 45 and <= 47)
            return true;
        var text = (format.Format ?? string.Empty).ToLowerInvariant();
        if (text.Length == 0 || text == "@" || text == "general")
            return false;
        // Bỏ phần trong ngoặc kép / ngoặc vuông (chữ, màu) trước khi tìm ký hiệu ngày.
        var stripped = System.Text.RegularExpressions.Regex.Replace(text, "\"[^\"]*\"|\\[[^\\]]*\\]", string.Empty);
        return stripped.Contains('d') || stripped.Contains('y');
    }

    /// <inheritdoc />
    public byte[] BuildTemplate(string title, string description, IReadOnlyList<ImportColumn> columns)
    {
        using var workbook = new XLWorkbook();
        var data = workbook.Worksheets.Add(ImportLimits.DataSheetName);
        var guide = workbook.Worksheets.Add(ImportLimits.GuideSheetName);
        IXLWorksheet? lists = null;

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var col = i + 1;
            var header = data.Cell(1, col);
            header.SetValue(column.Required ? column.Header + " *" : column.Header);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = column.Required ? RequiredFill : HeaderFill;
            header.Style.Alignment.WrapText = true;
            header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            if (!string.IsNullOrEmpty(column.Description))
                header.CreateComment().AddText(column.Description);

            var body = data.Range(2, col, TemplateRows, col);
            body.Style.NumberFormat.Format = "@"; // chữ: giữ số 0 đầu, ngày nhập dạng dd/MM/yyyy

            if (column.AllowedValues is { Count: > 0 } values && !column.MultipleValues)
            {
                lists ??= workbook.Worksheets.Add(ListSheetName);
                for (var v = 0; v < values.Count; v++)
                    lists.Cell(v + 1, col).SetValue(values[v]);
                var source = lists.Range(1, col, values.Count, col);
                var validation = body.CreateDataValidation();
                validation.List(source, true);
                validation.IgnoreBlanks = true;
                validation.ErrorStyle = XLErrorStyle.Stop;
                validation.ErrorTitle = column.Header;
                validation.ErrorMessage = "Hãy chọn giá trị trong danh sách.";
            }
            data.Column(col).Width = Math.Clamp(column.Header.Length + 6, 14, 40);
        }
        data.Row(1).Height = 32;
        data.SheetView.FreezeRows(1);
        if (lists != null)
            lists.Visibility = XLWorksheetVisibility.Hidden;

        BuildGuide(guide, title, description, columns);
        data.SetTabActive();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void BuildGuide(IXLWorksheet guide, string title, string description, IReadOnlyList<ImportColumn> columns)
    {
        guide.Cell(1, 1).SetValue($"Nhập dữ liệu: {title}");
        guide.Cell(1, 1).Style.Font.Bold = true;
        guide.Cell(1, 1).Style.Font.FontSize = 14;
        guide.Cell(2, 1).SetValue(description);

        var notes = new[]
        {
            $"Nhập dữ liệu ở sheet \"{ImportLimits.DataSheetName}\" từ dòng 2; không đổi tên, thứ tự dòng tiêu đề. Cột có dấu * là bắt buộc.",
            $"Tối đa {ImportLimits.MaxRows} dòng, file .xlsx không quá {ImportLimits.MaxFileBytes / 1024 / 1024} MB. Dòng trống được bỏ qua.",
            "Ngày nhập dạng dd/MM/yyyy (ví dụ 05/09/2026).",
            "Sau khi tải lên, hệ thống hiện xem trước từng dòng; còn dòng lỗi thì chưa nhập được dòng nào — hãy sửa file rồi tải lên lại."
        };
        for (var i = 0; i < notes.Length; i++)
            guide.Cell(4 + i, 1).SetValue("• " + notes[i]);

        var start = 5 + notes.Length;
        var headers = new[] { "Cột", "Bắt buộc", "Cách nhập", "Ví dụ", "Giá trị hợp lệ" };
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = guide.Cell(start, c + 1);
            cell.SetValue(headers[c]);
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = HeaderFill;
        }

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var row = start + 1 + i;
            SetText(guide.Cell(row, 1), column.Header);
            SetText(guide.Cell(row, 2), column.Required ? ImportValues.Yes : ImportValues.No);
            SetText(guide.Cell(row, 3), column.Description);
            SetText(guide.Cell(row, 4), column.Example ?? string.Empty);
            SetText(guide.Cell(row, 5), column.AllowedValues is { Count: > 0 } values ? string.Join("; ", values) : string.Empty);
        }

        guide.Column(1).Width = 28;
        guide.Column(2).Width = 10;
        guide.Column(3).Width = 70;
        guide.Column(4).Width = 26;
        guide.Column(5).Width = 60;
        guide.Range(start + 1, 1, start + columns.Count, 5).Style.Alignment.WrapText = true;
        guide.Range(start + 1, 1, start + columns.Count, 5).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
    }

    /// <inheritdoc />
    public byte[] BuildTable(string sheetName, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        for (var c = 0; c < headers.Count; c++)
        {
            var cell = sheet.Cell(1, c + 1);
            SetText(cell, headers[c]);
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = HeaderFill;
        }

        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Count; c++)
                SetText(sheet.Cell(r, c + 1), row[c]);
            r++;
        }
        sheet.Columns().AdjustToContents(8.0, 60.0);
        sheet.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Ghi chữ vào ô; chống chèn công thức: giá trị bắt đầu bằng = + - @ (hoặc tab, xuống dòng) được đánh dấu "chữ thuần"
    /// (tiền tố ' trong Excel) để không bị hiểu là công thức khi mở/chỉnh sửa.
    /// </summary>
    public static void SetText(IXLCell cell, string? value)
    {
        var text = value ?? string.Empty;
        cell.SetValue(text);
        if (IsFormulaLike(text))
            cell.Style.IncludeQuotePrefix = true;
    }

    /// <summary>Giá trị có thể bị Excel hiểu là công thức.</summary>
    public static bool IsFormulaLike(string value)
        => value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r';
}
