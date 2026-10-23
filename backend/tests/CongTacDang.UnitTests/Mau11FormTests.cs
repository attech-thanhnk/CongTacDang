using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Infrastructure.Documents;
using CongTacDang.Infrastructure.Documents.Forms;
using CongTacDang.Infrastructure.Services;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Mẫu 11: cột (5)–(7) là mức đề xuất ở bước tập thể, thẩm định, cấp trực tiếp; chia mục I/II, mục rỗng không in.</summary>
public class Mau11FormTests
{
    private static readonly DateTime At = new(2026, 9, 20, 2, 0, 0, DateTimeKind.Utc);

    private static EvaluationRecord Record(string name, ApprovalAuthority authority, bool proposals)
    {
        var record = new EvaluationRecord
        {
            Member = new PartyMemberProfile { FullName = name, PositionTitle = "Trưởng phòng" },
            Department = new AdministrativeDepartment { Name = "Phòng Kỹ thuật" },
            ApprovalAuthority = authority,
            GeneralCriteriaScore = 30,
            TasksScore = 57.5,
            SelfProposedGrade = EvaluationGrade.HoanThanhTot,
            SelfScoredAt = At
        };
        if (proposals)
        {
            record.CollectiveProposedGrade = EvaluationGrade.HoanThanhTot;
            record.CollectiveRecordedAt = At;
            record.AppraisalProposedGrade = EvaluationGrade.HoanThanhXuatSac;
            record.AppraisedAt = At;
            record.DirectorProposedGrade = EvaluationGrade.HoanThanh;
            record.DirectorReviewedAt = At;
        }
        return record;
    }

    private static EvaluationPeriod Period() => new() { Year = 2026, Quarter = EvaluationQuarter.Quy3 };

    private static List<List<string>> RenderRows(Mau11Data data)
    {
        var template = new FileWordTemplateStore().Load(Mau11Data.TemplateFileName);
        var org = OrganizationTemplateFields.From("ĐẢNG BỘ CÔNG TY A", "ĐẢNG BỘ TỔNG CÔNG TY B", "Công ty A", "Tổng công ty B", "A", "Hà Nội");
        var result = DocxTemplateEngine.Render(template, TemplateDataBinder.Bind(data).WithShared(TemplateDataBinder.Bind(org)));
        Assert.Empty(result.MissingTags);
        Assert.Empty(DocumentTemplateTests.Validate(result.Content));

        using var doc = WordprocessingDocument.Open(new MemoryStream(result.Content), false);
        var body = doc.MainDocumentPart!.Document!.Body!;
        Assert.DoesNotContain("«", body.InnerText);
        var table = body.Descendants<Table>().Single(t => t.InnerText.Contains("Mức tự xếp loại đề xuất"));
        return table.Elements<TableRow>().Select(r => r.Elements<TableCell>().Select(c => c.InnerText).ToList()).ToList();
    }

    [Fact]
    public void Mau11_FillsColumns5To7_AndSplitsByAuthority()
    {
        var lines = new List<Mau11Line>
        {
            new(Record("Nguyễn Văn A", ApprovalAuthority.CapTren, proposals: true), "Bí thư Đảng ủy, Giám đốc — Công ty A"),
            new(Record("Trần Thị B", ApprovalAuthority.CoSo, proposals: true), "Bí thư Chi bộ, Trưởng phòng — Phòng Kỹ thuật"),
            new(Record("Lê Văn C", ApprovalAuthority.CoSo, proposals: false), null)
        };

        var data = Mau11Data.From(Period(), lines, "ĐẢNG BỘ CÔNG TY A");

        Assert.Equal("ĐẢNG ỦY CÔNG TY A", data.BaseAuthority);
        Assert.Equal("ĐẢNG BỘ CÔNG TY A", data.PartyCell);
        var rows = RenderRows(data);
        // 3 dòng tiêu đề + mục I (tiêu đề + 1) + mục II (tiêu đề + 2).
        Assert.Equal(8, rows.Count);
        Assert.Equal("I", rows[3][0]);
        Assert.Equal(
            new[] { "1", "Nguyễn Văn A", "Bí thư Đảng ủy, Giám đốc — Công ty A", "30", "57,5", "Hoàn thành tốt nhiệm vụ",
                "Hoàn thành tốt nhiệm vụ", "Hoàn thành xuất sắc nhiệm vụ", "Hoàn thành nhiệm vụ", "", "", "", "", "" },
            rows[4]);
        Assert.Equal("II", rows[5][0]);
        Assert.Equal("DANH SÁCH CÁN BỘ THUỘC THẨM QUYỀN QUYẾT ĐỊNH, PHÊ DUYỆT MỨC XẾP LOẠI CỦA ĐẢNG ỦY CÔNG TY A", rows[5][1]);
        Assert.Equal("1", rows[6][0]);
        Assert.Equal("Trần Thị B", rows[6][1]);
        Assert.Equal("Hoàn thành xuất sắc nhiệm vụ", rows[6][7]);
        // Hồ sơ chưa qua các bước đề xuất (phiếu in cho hội nghị tập thể lãnh đạo): cột (5)–(7) trống; chức vụ mặc định.
        Assert.Equal(
            new[] { "2", "Lê Văn C", "Trưởng phòng — Phòng Kỹ thuật", "30", "57,5", "Hoàn thành tốt nhiệm vụ", "", "", "", "", "", "", "", "" },
            rows[7]);
    }

    [Fact]
    public void Mau11_OnlyBaseAuthority_OmitsSectionI()
    {
        var unscored = Record("Phạm Văn D", ApprovalAuthority.CoSo, proposals: false);
        unscored.SelfScoredAt = null;

        var data = Mau11Data.From(Period(), new List<Mau11Line> { new(unscored, "Phó trưởng phòng — Phòng Kỹ thuật") }, "Chi bộ Kỹ thuật");

        Assert.False(data.ShowSuperior);
        Assert.True(data.ShowBase);
        var rows = RenderRows(data);
        Assert.Equal(5, rows.Count);
        Assert.Equal("II", rows[3][0]);
        Assert.EndsWith("CỦA CHI BỘ KỸ THUẬT", rows[3][1]);
        // Chưa tự chấm → cột (4a)–(4c) trống.
        Assert.Equal(new[] { "1", "Phạm Văn D", "Phó trưởng phòng — Phòng Kỹ thuật", "", "", "", "", "", "", "", "", "", "", "" }, rows[4]);
    }

    [Fact]
    public void Mau11_NoRecords_PrintsBothSectionsWithBlankLines()
    {
        var data = Mau11Data.From(Period(), new List<Mau11Line>(), null);

        var rows = RenderRows(data);
        Assert.Equal(7, rows.Count);
        Assert.Equal("I", rows[3][0]);
        Assert.All(rows[4], c => Assert.Equal(string.Empty, c));
        Assert.Equal("II", rows[5][0]);
        Assert.Contains("ĐẢNG ỦY/CHI ỦY CƠ SỞ…", rows[5][1]);
        Assert.All(rows[6], c => Assert.Equal(string.Empty, c));
    }

    [Fact]
    public void Mau13_OnlySuperiorAuthority_OmitsSectionII_KeepsVoteCounts()
    {
        var (period, records) = DocumentTemplateTests.SampleData();
        var template = new FileWordTemplateStore().Load(Mau13Data.TemplateFileName);
        var data = Wave8FormsTests.Mau13Sample(period, records, onlyAuthority: ApprovalAuthority.CapTren);

        Assert.True(data.ShowSuperior);
        Assert.False(data.ShowBase);
        var result = DocxTemplateEngine.Render(template, TemplateDataBinder.Bind(data));

        using var doc = WordprocessingDocument.Open(new MemoryStream(result.Content), false);
        var table = doc.MainDocumentPart!.Document!.Body!.Descendants<Table>().Single(t => t.InnerText.Contains("Số phiếu bầu"));
        var rows = table.Elements<TableRow>().Select(r => r.Elements<TableCell>().Select(c => c.InnerText).ToList()).ToList();
        Assert.Equal(6, rows.Count);
        Assert.Equal("I", rows[3][0]);
        Assert.Equal(new[] { "1", "Nguyễn Văn A", "Trưởng phòng — Phòng Kỹ thuật", "3", "6", "1", "0", "1", "Đề nghị cấp trên" }, rows[4]);
        Assert.Equal(new[] { "2", "Trần Thị B", "Trưởng phòng — Phòng Kỹ thuật", "4", "6", "1", "0", "0", "" }, rows[5]);
        Assert.DoesNotContain("PHÊ DUYỆT MỨC XẾP LOẠI CỦA ĐẢNG ỦY", table.InnerText);
    }

    [Fact]
    public void Mau13_NoLines_PrintsBothSectionsWithBlankLines()
    {
        var (period, _) = DocumentTemplateTests.SampleData();
        var data = Wave8FormsTests.Mau13Sample(period, new List<EvaluationRecord>());

        Assert.True(data.ShowSuperior);
        Assert.True(data.ShowBase);
        Assert.False(data.HasSuperiorRows);
        Assert.False(data.HasBaseRows);
    }
}
