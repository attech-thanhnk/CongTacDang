using CongTacDang.Infrastructure.Documents;
using CongTacDang.Infrastructure.Documents.Forms;
using CongTacDang.Infrastructure.Services;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Trình bày bản in chung cho mọi biểu mẫu: tiêu đề Đảng bộ, ô bảng hẹp, khối chữ ký, dòng có ngắt dòng.</summary>
public class DocxLayoutTests
{
    [Theory]
    [InlineData("ĐẢNG BỘ TỔNG CÔNG TY QUẢN LÝ BAY VIỆT NAM", "ĐẢNG BỘ TỔNG CÔNG TY\nQUẢN LÝ BAY VIỆT NAM")]
    [InlineData("ĐẢNG BỘ CÔNG TY TNHH KỸ THUẬT QUẢN LÝ BAY", "ĐẢNG BỘ CÔNG TY TNHH\nKỸ THUẬT QUẢN LÝ BAY")]
    [InlineData("CHI BỘ KỸ THUẬT", "CHI BỘ KỸ THUẬT")]
    [InlineData(null, null)]
    public void PartyHeaderLines_SplitsLongNamesIntoTwoBalancedLines(string? name, string? expected) =>
        Assert.Equal(expected, PartyHeader.Lines(name));

    [Fact]
    public void Render_NarrowCellsLeftAligned_SignatureKeptWithContent_LineBreaksNotJustified()
    {
        var (_, records) = DocumentTemplateTests.SampleData();
        var result = DocxTemplateEngine.Render(
            new FileWordTemplateStore().Load(Mau02Data.TemplateFileName), TemplateDataBinder.Bind(Mau02Data.From(records[0])));

        using var doc = WordprocessingDocument.Open(new MemoryStream(result.Content), false);
        var body = doc.MainDocumentPart!.Document!.Body!;

        // Ô tên sản phẩm (hẹp) không căn đều hai bên.
        var nameCell = body.Descendants<TableCell>().First(c => c.InnerText == records[0].Tasks.OrderBy(t => t.TaskOrder).First().TaskName);
        Assert.All(nameCell.Elements<Paragraph>(), p => Assert.Equal(JustificationValues.Left, p.ParagraphProperties?.Justification?.Val?.Value));

        // Bảng chữ ký không ngắt trang và đi liền đoạn nội dung ngay trước.
        var signature = body.Elements<Table>().Last(t => t.InnerText.Contains("ghi rõ họ tên"));
        Assert.All(signature.Elements<TableRow>(), r => Assert.NotNull(r.TableRowProperties?.GetFirstChild<CantSplit>()));
        var previous = signature.PreviousSibling<Paragraph>();
        Assert.NotNull(previous?.ParagraphProperties?.KeepNext);

        // Dòng kết thúc bằng ngắt dòng trong đoạn căn đều không bị giãn chữ.
        Assert.NotNull(doc.MainDocumentPart.DocumentSettingsPart?.Settings?.GetFirstChild<Compatibility>()?.GetFirstChild<DoNotExpandShiftReturn>());
    }
}
