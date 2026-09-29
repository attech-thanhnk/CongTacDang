using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using CongTacDang.Infrastructure.Documents;
using CongTacDang.Infrastructure.Documents.Forms;
using CongTacDang.Infrastructure.Services;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>
/// Tích hợp đợt 8: Mẫu 13 dựng lại đúng biểu mẫu gốc (tách mục I/II theo thẩm quyền, cột "Chưa đánh giá, xếp loại", Tổ kiểm
/// phiếu, số phiếu), tham số cửa sổ cảnh báo kế hoạch Mẫu 17 của bộ tiêu chí.
/// </summary>
public class Wave8FormsTests
{
    /// <summary>Dữ liệu Mẫu 13 mẫu: hồ sơ đầu thuộc thẩm quyền BTVĐUTCT, hồ sơ sau thuộc Đảng ủy cơ sở.</summary>
    internal static Mau13Data Mau13Sample(
        EvaluationPeriod period, IReadOnlyList<EvaluationRecord> records, bool withCommittee = true, ApprovalAuthority? onlyAuthority = null)
    {
        var meeting = new EvaluationMeeting
        {
            Period = period,
            Stage = WorkflowStep.B4_DECISION,
            Location = "Phòng họp tầng 3",
            InvitedCount = 12,
            PresentCount = 11,
            AbsentCount = 1,
            ChairName = "Nguyễn Văn Chủ",
            SecretaryName = "Trần Thị Thư",
            StartedAt = new DateTime(2026, 9, 20, 1, 30, 0, DateTimeKind.Utc),
            EndedAt = new DateTime(2026, 9, 20, 4, 0, 0, DateTimeKind.Utc)
        };
        var details = new MeetingDetailsDto
        {
            WorkingRules = "Đảng ủy Công ty nhiệm kỳ 2025-2030",
            ChairTitle = "Bí thư Đảng ủy",
            SecretaryTitle = "Đảng ủy viên",
            BallotsIssued = 11,
            BallotsCollected = 11,
            BallotsValid = 10,
            BallotsInvalid = 1
        };
        details.Attendees.Add(new MeetingAttendeeDto { Name = "Hoàng Văn Ghi", Title = "Chuyên viên Văn phòng Đảng ủy" });
        if (withCommittee)
        {
            details.CountingCommittee.Add(new MeetingAttendeeDto { Name = "Lê Văn Trưởng", Title = "Đảng ủy viên" });
            details.CountingCommittee.Add(new MeetingAttendeeDto { Name = "Phạm Thị Viên", Title = "Đảng viên" });
        }
        var lines = records.Select((r, i) => new Mau13Line(
                new EvaluationMeetingVoteSummary { VotesExcellent = 3 + i, VotesGood = 6, VotesSatisfactory = 1, VotesNotRated = 1 - i, InvalidVotes = 1, Notes = i == 0 ? "Đề nghị cấp trên" : "" },
                r.Member?.FullName,
                "Trưởng phòng — Phòng Kỹ thuật",
                onlyAuthority ?? (i == 0 ? ApprovalAuthority.CapTren : ApprovalAuthority.CoSo)))
            .ToList();
        return Mau13Data.From(meeting, PartyHeader.Of("Đảng bộ Tổng công ty", "Đảng ủy Công ty A"), "Đảng ủy Công ty A", "Đảng ủy Công ty A", details, lines);
    }

    [Fact]
    public void Mau13_Template_TagsMatchDataClass_AndRendersValidDocument()
    {
        var (period, records) = DocumentTemplateTests.SampleData();
        var template = new FileWordTemplateStore().Load(Mau13Data.TemplateFileName);
        var data = TemplateDataBinder.Bind(Mau13Sample(period, records));

        var result = DocxTemplateEngine.Render(template, data);

        Assert.Empty(result.MissingTags);
        using var doc = WordprocessingDocument.Open(new MemoryStream(result.Content), false);
        var body = doc.MainDocumentPart!.Document!.Body!;
        Assert.DoesNotContain(body.Descendants<SdtElement>(), sdt => sdt.SdtProperties?.GetFirstChild<Tag>() != null);
        Assert.Empty(DocumentTemplateTests.Validate(result.Content));
    }

    [Fact]
    public void Mau13_KeepsOriginalFormText_AndSplitsRowsByAuthority()
    {
        var (period, records) = DocumentTemplateTests.SampleData();
        var template = new FileWordTemplateStore().Load(Mau13Data.TemplateFileName);

        var result = DocxTemplateEngine.Render(template, TemplateDataBinder.Bind(Mau13Sample(period, records)));

        using var doc = WordprocessingDocument.Open(new MemoryStream(result.Content), false);
        var body = doc.MainDocumentPart!.Document!.Body!;
        var text = body.InnerText;
        // Chữ in sẵn của biểu mẫu gốc.
        Assert.Contains("BIÊN BẢN KIỂM PHIẾU", text);
        Assert.Contains("II. KẾT QUẢ KIỂM PHIẾU", text);
        Assert.Contains("Chưa đánh giá, xếp loại", text);
        Assert.Contains("DANH SÁCH CÁN BỘ THUỘC THẨM QUYỀN QUYẾT ĐỊNH, PHÊ DUYỆT MỨC XẾP LOẠI CỦA BTVĐUTCT", text);
        Assert.Contains("T/M TỔ KIỂM PHIẾU", text);
        // Dữ liệu biên bản.
        Assert.Contains("quý III/2026", text);
        Assert.Contains("Hội nghị Đảng ủy Công ty A", text);
        Assert.Contains("Lê Văn Trưởng", text);
        Assert.Contains("Tổ trưởng", text);
        Assert.Contains("Phạm Thị Viên", text);
        Assert.Contains("Thành viên", text);
        Assert.Contains("Đảng ủy viên: Tổ trưởng", text);
        Assert.Contains("Hoàng Văn Ghi", text);

        // Bảng kết quả: tiêu đề mục I, dòng của hồ sơ thẩm quyền cấp trên, tiêu đề mục II, dòng của hồ sơ đảng ủy cơ sở.
        var table = body.Descendants<Table>().Single(t => t.InnerText.Contains("Số phiếu bầu"));
        var rows = table.Elements<TableRow>().Select(r => r.Elements<TableCell>().Select(c => c.InnerText).ToList()).ToList();
        Assert.Equal(7, rows.Count);
        Assert.Equal("I", rows[3][0]);
        Assert.Equal(new[] { "1", "Nguyễn Văn A", "Trưởng phòng — Phòng Kỹ thuật", "3", "6", "1", "0", "1", "Đề nghị cấp trên" }, rows[4]);
        Assert.Equal("II", rows[5][0]);
        Assert.Equal(new[] { "1", "Trần Thị B", "Trưởng phòng — Phòng Kỹ thuật", "4", "6", "1", "0", "0", "" }, rows[6]);

        // Số phiếu phát ra / thu về / hợp lệ / không hợp lệ.
        var paragraphs = body.Descendants<Paragraph>().Select(p => p.InnerText).ToList();
        Assert.Contains(paragraphs, p => p.StartsWith("- Tổng số phiếu phát ra:") && p.Contains("11 Phiếu"));
        Assert.Contains(paragraphs, p => p.StartsWith("+ Số phiếu hợp lệ:") && p.Contains("10 Phiếu"));
        Assert.Contains(paragraphs, p => p.StartsWith("+ Số phiếu không hợp lệ:") && p.Contains("1 Phiếu"));
    }

    [Fact]
    public void Mau13_WithoutCommitteeOrRowsOfOneAuthority_KeepsOriginalBlankLines()
    {
        var (period, records) = DocumentTemplateTests.SampleData();
        var template = new FileWordTemplateStore().Load(Mau13Data.TemplateFileName);

        var result = DocxTemplateEngine.Render(template, TemplateDataBinder.Bind(Mau13Sample(period, records.Take(1).ToList(), withCommittee: false, onlyAuthority: ApprovalAuthority.CoSo)));

        using var doc = WordprocessingDocument.Open(new MemoryStream(result.Content), false);
        var body = doc.MainDocumentPart!.Document!.Body!;
        var paragraphs = body.Descendants<Paragraph>().Select(p => p.InnerText).ToList();
        Assert.Contains(paragraphs, p => p.StartsWith("(1) Đồng chí …") && p.EndsWith("Tổ trưởng."));
        Assert.Contains(paragraphs, p => p.StartsWith("(2) Đồng chí …") && p.EndsWith("Thành viên."));
        var table = body.Descendants<Table>().Single(t => t.InnerText.Contains("Số phiếu bầu"));
        var rows = table.Elements<TableRow>().ToList();
        // Mục I không có cán bộ → giữ một dòng trống như biểu mẫu; mục II có một cán bộ.
        Assert.Equal(7, rows.Count);
        Assert.All(rows[4].Elements<TableCell>(), c => Assert.Equal(string.Empty, c.InnerText));
        Assert.Equal("Nguyễn Văn A", rows[6].Elements<TableCell>().ElementAt(1).InnerText);
    }

    [Fact]
    public void CriteriaParameters_ImprovementPlanAlertDays_DefaultAndValidation()
    {
        var content = CriteriaSetDefaults.Build09B();
        Assert.Equal(90, content.Parameters.ImprovementPlanAlertDays);
        Assert.Empty(content.Validate(CriteriaSetContent.Form09B));

        content.Parameters.ImprovementPlanAlertDays = 0;
        Assert.Contains(content.Validate(CriteriaSetContent.Form09B), e => e.Contains("kế hoạch 30-60-90"));
        content.Parameters.ImprovementPlanAlertDays = 3651;
        Assert.Contains(content.Validate(CriteriaSetContent.Form09B), e => e.Contains("kế hoạch 30-60-90"));

        // JSON cũ không có khóa → mặc định 90.
        var parsed = CriteriaSetContent.Parse("{\"parameters\":{}}");
        Assert.Equal(90, parsed.Parameters.ImprovementPlanAlertDays);
    }

    [Fact]
    public void DefaultCriteriaSets_RequiredForms_MatchHd03Q3Column()
    {
        // HD03 mục 7–8: Q3/2026 dùng 09B, 09C, 9D (Mẫu 10 giữ theo quyết định task 18 — chờ nghiệp vụ xác nhận); từ 2027 đủ mẫu.
        Assert.Equal(new[] { "09B", "09C", "9D", "10" }, CriteriaSetDefaults.Build09B().RequiredForms);
        Assert.Equal(new[] { "01", "02", "09A", "09C", "9D", "10" }, CriteriaSetDefaults.Build09A().RequiredForms);
    }
}
