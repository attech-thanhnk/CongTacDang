using System.Globalization;
using ClosedXML.Excel;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Imports;

namespace CongTacDang.Infrastructure.Imports;

/// <summary>Đọc/ghi Excel cho chức năng import bằng ClosedXML.</summary>
public sealed class ClosedXmlImportWorkbook : IImportWorkbook
{
    private static readonly XLColor RequiredHeaderColor = XLColor.FromHtml("#FDE68A");
    private static readonly XLColor OptionalHeaderColor = XLColor.FromHtml("#E5E7EB");

    /// <inheritdoc />
    public byte[] CreateTemplate(IImportDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        using var workbook = new XLWorkbook();
        var data = workbook.AddWorksheet(ImportLimits.DataSheetName);
        var columns = definition.TemplateColumns;

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var col = i + 1;
            var cell = data.Cell(1, col);
            cell.Value = column.Header;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = column.Required ? RequiredHeaderColor : OptionalHeaderColor;
            cell.Style.Alignment.WrapText = true;
            cell.CreateComment().AddText((column.Required ? "Bắt buộc. " : "Không bắt buộc. ") + column.Description);

            var body = data.Range(2, col, ImportLimits.MaxRows + 1, col);
            body.Style.NumberFormat.Format = "@"; // giữ nguyên số 0 đầu (số thẻ Đảng, mã…)

            if (column.AllowedValues is { Count: > 0 } allowed)
            {
                var validation = body.CreateDataValidation();
                validation.List("\"" + string.Join(",", allowed) + "\"", true);
                validation.IgnoreBlanks = true;
                validation.ShowErrorMessage = true;
                validation.ErrorTitle = "Giá trị không hợp lệ";
                validation.ErrorMessage = $"Cột \"{column.Header}\" chỉ nhận: {string.Join(", ", allowed)}.";
            }

            data.Column(col).Width = Math.Clamp(column.Header.Length + 6, 14, 40);
        }

        data.SheetView.FreezeRows(1);
        AddGuideSheet(workbook, definition);
        data.SetTabActive();
        return Save(workbook);
    }

    /// <inheritdoc />
    public IReadOnlyList<ImportSourceRow> ReadRows(Stream content, IImportDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(definition);

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(content);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new ValidationException("Không đọc được tệp. Hãy kiểm tra tệp là Excel .xlsx hợp lệ (không đặt mật khẩu) và dùng file mẫu của hệ thống.");
        }

        using (workbook)
        {
            if (!workbook.Worksheets.TryGetWorksheet(ImportLimits.DataSheetName, out var sheet))
                sheet = workbook.Worksheets.FirstOrDefault()
                    ?? throw new ValidationException("Tệp không có sheet dữ liệu nào. Hãy dùng file mẫu của hệ thống.");

            var columnMap = MapHeaders(sheet, definition);
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            var rows = new List<ImportSourceRow>();

            for (var r = 2; r <= lastRow; r++)
            {
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                var hasValue = false;
                foreach (var (key, col) in columnMap)
                {
                    var value = ReadCell(sheet.Cell(r, col));
                    values[key] = value;
                    hasValue |= value.Length > 0;
                }

                // Bỏ dòng trống (kể cả dòng trống cuối tệp).
                if (!hasValue)
                    continue;

                if (rows.Count >= ImportLimits.MaxRows)
                    throw new ValidationException(
                        $"Tệp có nhiều hơn {ImportLimits.MaxRows:N0} dòng dữ liệu. Hãy tách thành nhiều tệp, mỗi tệp tối đa {ImportLimits.MaxRows:N0} dòng.");

                foreach (var column in definition.TemplateColumns)
                    values.TryAdd(column.Key, string.Empty);
                rows.Add(new ImportSourceRow(r, values));
            }

            return rows;
        }
    }

    /// <inheritdoc />
    public byte[] CreateResultFile(ImportResultTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(string.IsNullOrWhiteSpace(table.SheetName) ? "Kết quả" : table.SheetName);
        var row = 1;

        if (!string.IsNullOrWhiteSpace(table.Title))
        {
            var title = sheet.Cell(row, 1);
            SetText(title, table.Title);
            title.Style.Font.Bold = true;
            title.Style.Font.FontColor = XLColor.DarkRed;
            row += 2;
        }

        for (var c = 0; c < table.Headers.Count; c++)
        {
            var cell = sheet.Cell(row, c + 1);
            SetText(cell, table.Headers[c]);
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = OptionalHeaderColor;
        }

        foreach (var values in table.Rows)
        {
            row++;
            for (var c = 0; c < values.Count; c++)
                SetText(sheet.Cell(row, c + 1), values[c]);
        }

        for (var c = 1; c <= table.Headers.Count; c++)
            sheet.Column(c).Width = 24;
        return Save(workbook);
    }

    /// <summary>
    /// Chống formula injection: giá trị bắt đầu bằng <c>= + - @</c>, tab hoặc CR được thêm tiền tố <c>'</c>
    /// để Excel/LibreOffice không hiểu là công thức. Khi ghi vào ô, ClosedXML lưu tiền tố này theo cách chuẩn của Excel:
    /// ô kiểu văn bản có cờ <c>quotePrefix</c> (giống người dùng gõ <c>'</c> trong Excel) — hiển thị đúng giá trị gốc.
    /// </summary>
    public static string SanitizeCellText(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;
    }

    private static void SetText(IXLCell cell, string? value)
    {
        cell.Style.NumberFormat.Format = "@";
        cell.Value = SanitizeCellText(value);
    }

    private static List<(string Key, int Column)> MapHeaders(IXLWorksheet sheet, IImportDefinition definition)
    {
        var lastColumn = sheet.Row(1).LastCellUsed()?.Address.ColumnNumber ?? 0;
        var byHeader = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var c = 1; c <= lastColumn; c++)
        {
            var header = NormalizeHeader(ReadCell(sheet.Cell(1, c)));
            if (header.Length == 0)
                continue;
            if (!byHeader.TryAdd(header, c))
                throw new ValidationException($"Cột \"{header}\" xuất hiện hai lần ở dòng tiêu đề. Hãy xóa cột trùng.");
        }

        var map = new List<(string, int)>();
        var missing = new List<string>();
        foreach (var column in definition.TemplateColumns)
        {
            if (byHeader.TryGetValue(NormalizeHeader(column.Header), out var col))
                map.Add((column.Key, col));
            else if (column.Required)
                missing.Add(column.Header);
        }

        if (missing.Count > 0)
            throw new ValidationException(
                $"Tệp thiếu cột bắt buộc: {string.Join(", ", missing)}. Hãy tải file mẫu mới nhất và giữ nguyên dòng tiêu đề.");
        return map;
    }

    private static string NormalizeHeader(string header) => header.Trim().TrimEnd('*').Trim();

    private static string ReadCell(IXLCell cell)
    {
        var value = cell.HasFormula ? cell.CachedValue : cell.Value;
        var text = value.Type switch
        {
            XLDataType.Blank => string.Empty,
            XLDataType.Text => value.GetText(),
            XLDataType.Number => value.GetNumber().ToString(CultureInfo.InvariantCulture),
            XLDataType.Boolean => value.GetBoolean() ? "TRUE" : "FALSE",
            XLDataType.DateTime => value.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            XLDataType.TimeSpan => value.GetTimeSpan().ToString(),
            _ => string.Empty
        };
        return text.Trim();
    }

    private static void AddGuideSheet(XLWorkbook workbook, IImportDefinition definition)
    {
        var guide = workbook.AddWorksheet(ImportLimits.GuideSheetName);
        guide.Cell(1, 1).Value = $"Hướng dẫn nhập: {definition.DisplayName}";
        guide.Cell(1, 1).Style.Font.Bold = true;
        guide.Cell(1, 1).Style.Font.FontSize = 14;
        guide.Cell(2, 1).Value = definition.Description;

        var notes = new[]
        {
            $"1. Điền dữ liệu vào sheet \"{ImportLimits.DataSheetName}\" từ dòng 2; giữ nguyên dòng tiêu đề (cột nền vàng là bắt buộc).",
            $"2. Chỉ nhận tệp .xlsx tối đa 5 MB và {ImportLimits.MaxRows:N0} dòng dữ liệu; dòng trống được bỏ qua, khoảng trắng hai đầu được cắt.",
            "3. Tải tệp lên → hệ thống hiển thị kết quả kiểm tra từng dòng. Có bất kỳ dòng lỗi nào thì không được xác nhận (không nhập một phần).",
            "4. Xác nhận → toàn bộ dữ liệu được ghi trong một lần; phiên xem trước hết hạn sau 30 phút."
        };
        for (var i = 0; i < notes.Length; i++)
            guide.Cell(4 + i, 1).Value = notes[i];

        var headerRow = 5 + notes.Length;
        string[] headers = { "Cột", "Bắt buộc", "Mô tả", "Giá trị hợp lệ", "Ví dụ" };
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = guide.Cell(headerRow, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = OptionalHeaderColor;
        }

        var row = headerRow;
        foreach (var column in definition.TemplateColumns)
        {
            row++;
            guide.Cell(row, 1).Value = column.Header;
            guide.Cell(row, 2).Value = column.Required ? "Có" : "Không";
            guide.Cell(row, 3).Value = column.Description;
            guide.Cell(row, 4).Value = column.AllowedValues is { Count: > 0 } ? string.Join(" | ", column.AllowedValues) : string.Empty;
            SetText(guide.Cell(row, 5), column.Example);
        }

        guide.Column(1).Width = 26;
        guide.Column(2).Width = 10;
        guide.Column(3).Width = 80;
        guide.Column(4).Width = 28;
        guide.Column(5).Width = 24;
        guide.Column(3).Style.Alignment.WrapText = true;
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
