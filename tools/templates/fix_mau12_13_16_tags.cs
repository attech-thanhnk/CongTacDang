#:package DocumentFormat.OpenXml@3.5.1
// Mẫu 12, 13: mục 3.1 dùng VOTE_PURPOSE (bỏ cụm "hoặc …" của mẫu gốc), "(có lý do)" thành ABSENT_NOTE.
// Mẫu 16: dòng "Các Ủy viên BCH/Chi ủy viên…" thành MEMBERS_RECEIVER, "T/M ĐẢNG ỦY (CHI BỘ)" thành SIGN_ON_BEHALF.
// Cách chạy: dotnet run tools/templates/fix_mau12_13_16_tags.cs -- <thư mục Templates/Word>. Chạy lại an toàn (đã sửa thì bỏ qua).
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

var dir = args[0];
var nextId = 931000;

foreach (var file in new[] { "Mau_12_BienBanHoiNghi.docx", "Mau_13_BienBanKiemPhieu.docx" })
{
    Edit(file, body =>
    {
        Wrap(body, "3.1. Thành phần được triệu tập", "đề xuất mức xếp loại (hoặc bỏ phiếu quyết định, phê duyệt mức xếp loại)", "VOTE_PURPOSE");
        Wrap(body, "Số vắng mặt", " (có lý do)", "ABSENT_NOTE");
    }, "ABSENT_NOTE");
}

Edit("Mau_16_BaoCaoKetQuaDanhGia.docx", body =>
{
    Wrap(body, "Các Ủy viên BCH/Chi ủy viên", "Các Ủy viên BCH/Chi ủy viên…", "MEMBERS_RECEIVER");
    Wrap(body, "T/M ĐẢNG ỦY (CHI BỘ)", "T/M ĐẢNG ỦY (CHI BỘ)", "SIGN_ON_BEHALF");
}, "SIGN_ON_BEHALF");

Edit("Mau_16_BaoCaoKetQuaDanhGia.docx", body =>
{
    // Mục III.1 chưa nhập: tên cấp ủy cơ sở điền từ dữ liệu, bỏ dòng ghi chú hướng dẫn của mẫu gốc.
    Wrap(body, "1. Kính đề nghị Đảng ủy cơ sở", "Đảng ủy cơ sở…", "BASE_COMMITTEE");
    body.Descendants<Paragraph>().Single(p => p.InnerText.StartsWith("(Ghi chú: Tùy theo thực tế", StringComparison.Ordinal)).Remove();
    // Chưa có nơi nhận thì in dòng chấm thay cho chữ hướng dẫn.
    foreach (var sdt in body.Descendants<SdtRun>().Where(s => s.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value == "RECIPIENT"))
    {
        var texts = sdt.Descendants<Text>().ToList();
        texts[0].Text = "…………………………";
        foreach (var extra in texts.Skip(1))
            extra.Text = string.Empty;
    }
}, "BASE_COMMITTEE");

// Mẫu 12, 13: chỗ chức vụ chưa ghi in dòng chấm, không in chữ hướng dẫn "(ghi rõ chức vụ Đảng, chính quyền)".
foreach (var file in new[] { "Mau_12_BienBanHoiNghi.docx", "Mau_13_BienBanKiemPhieu.docx" })
{
    using var doc = WordprocessingDocument.Open(Path.Combine(dir, file), true);
    var texts = doc.MainDocumentPart!.Document!.Body!.Descendants<Text>()
        .Where(t => t.Text.Contains("(ghi rõ chức vụ Đảng, chính quyền)", StringComparison.Ordinal)).ToList();
    foreach (var t in texts)
        t.Text = t.Text.Replace("… (ghi rõ chức vụ Đảng, chính quyền)", "……………………", StringComparison.Ordinal)
            .Replace(" (ghi rõ chức vụ Đảng, chính quyền)", "………………", StringComparison.Ordinal);
    doc.MainDocumentPart.Document.Save();
    Console.WriteLine(file + ": bỏ chữ hướng dẫn chức vụ ở " + texts.Count + " chỗ.");
}

void Edit(string file, Action<Body> change, string marker)
{
    using var doc = WordprocessingDocument.Open(Path.Combine(dir, file), true);
    var body = doc.MainDocumentPart!.Document!.Body!;
    if (body.Descendants<SdtElement>().Any(s => s.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value == marker))
    {
        Console.WriteLine(file + ": đã sửa — bỏ qua.");
        return;
    }
    change(body);
    doc.MainDocumentPart.Document.Save();
    Console.WriteLine(file + ": đã sửa.");
}

// Tách run chứa đoạn chữ ra ba phần, bọc phần giữa trong control có Tag (giữ định dạng run gốc).
void Wrap(Body body, string paragraphText, string target, string tag)
{
    var paragraph = body.Descendants<Paragraph>().Single(p => p.InnerText.Contains(paragraphText, StringComparison.Ordinal)
        && p.Ancestors<SdtElement>().All(s => s.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value?.StartsWith("repeat:") != true));
    var run = paragraph.Descendants<Run>().Single(r => r.InnerText.Contains(target, StringComparison.Ordinal));
    var text = run.GetFirstChild<Text>()!;
    if (run.Elements<Text>().Count() != 1)
        throw new InvalidOperationException(tag + ": run có nhiều đoạn chữ.");
    var index = text.Text.IndexOf(target, StringComparison.Ordinal);
    var before = text.Text[..index];
    var after = text.Text[(index + target.Length)..];

    Run Copy(string value)
    {
        var copy = (Run)run.CloneNode(true);
        copy.GetFirstChild<Text>()!.Text = value;
        copy.GetFirstChild<Text>()!.Space = SpaceProcessingModeValues.Preserve;
        return copy;
    }

    var sdt = new SdtRun(
        new SdtProperties(new SdtAlias { Val = tag }, new Tag { Val = tag }, new SdtId { Val = nextId++ }),
        new SdtContentRun(Copy(target)));
    if (before.Length > 0)
        run.InsertBeforeSelf(Copy(before));
    run.InsertBeforeSelf(sdt);
    if (after.Length > 0)
        run.InsertBeforeSelf(Copy(after));
    run.Remove();
}
