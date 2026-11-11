#:package DocumentFormat.OpenXml@3.5.1
// Mẫu 05: chữ mặc định của ô "Cấp có thẩm quyền giao việc" (ASSIGNER) đổi từ chữ mẫu sang dòng chấm, để phiếu chưa duyệt in dòng trống.
// Cách chạy: dotnet run tools/templates/fix_mau05_assigner.cs -- <thư mục Templates/Word>. Chạy lại an toàn (đã sửa thì bỏ qua).
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

const string File = "Mau_05_PhieuNhiemVuDotXuat.docx";
const string Dots = "……………………………………………………………………………………………………";

using var doc = WordprocessingDocument.Open(Path.Combine(args[0], File), true);
var body = doc.MainDocumentPart!.Document!.Body!;
var sdt = body.Descendants<SdtRun>().Single(s => s.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value == "ASSIGNER");
var texts = sdt.Descendants<Text>().ToList();
if (texts.Count == 1 && texts[0].Text == Dots)
{
    Console.WriteLine(File + ": đã sửa — bỏ qua.");
    return;
}
texts[0].Text = Dots;
foreach (var extra in texts.Skip(1))
    extra.Parent!.Remove();
doc.MainDocumentPart.Document.Save();
Console.WriteLine(File + ": đã sửa.");
