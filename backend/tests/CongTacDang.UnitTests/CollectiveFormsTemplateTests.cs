using CongTacDang.Application.DTOs;
using CongTacDang.Application.Reports;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using CongTacDang.Infrastructure.Documents;
using CongTacDang.Infrastructure.Documents.Forms;
using CongTacDang.Infrastructure.Services;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Task 19 (T-84, T-85): template Mẫu 07, 08, 12, 16 dựng từ biểu mẫu gốc — khớp tag, sinh tài liệu hợp lệ, đúng số liệu.</summary>
public class CollectiveFormsTemplateTests
{
    private static readonly EvaluationPeriod Period = new() { Year = 2026, Quarter = EvaluationQuarter.Quy3, Name = "Quý III/2026" };
    private static readonly PartyHeader Header = PartyHeader.Of("Đảng bộ Công ty A", "Chi bộ Kỹ thuật");

    public static IEnumerable<object[]> Forms()
    {
        yield return new object[] { Mau07Data.TemplateFileName, Mau07Sample() };
        yield return new object[] { Mau08Data.TemplateFileName, Mau08Sample() };
        yield return new object[] { Mau12Data.TemplateFileName, Mau12Sample(withAttendees: true) };
        var (period, records) = DocumentTemplateTests.SampleData();
        yield return new object[] { Mau13Data.TemplateFileName, Wave8FormsTests.Mau13Sample(period, records) };
        yield return new object[] { Mau16Data.TemplateFileName, Mau16Sample() };
    }

    [Theory]
    [MemberData(nameof(Forms))]
    public void Template_TagsMatchFormDataClass(string templateFileName, object formData)
    {
        var template = new FileWordTemplateStore().Load(templateFileName);
        var templateTags = DocxTemplateEngine.GetTags(template).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var catalog = TemplateTagCatalog.Describe(formData.GetType());
        var common = OrganizationTemplateFields.Tags.Select(t => t.Tag);

        // Không thừa: mọi tag trong template đều khai báo trên lớp dữ liệu hoặc là tag thông tin đơn vị.
        Assert.Empty(templateTags.Except(catalog.Select(t => t.Tag).Concat(common), StringComparer.OrdinalIgnoreCase).OrderBy(t => t));
        // Không thiếu: trường/khối lặp đều có trong template; điều kiện chỉ cần một nhánh.
        var missing = catalog
            .Where(t => t.Kind is TemplateTagCatalog.FieldKind or TemplateTagCatalog.RepeatKind)
            .Select(t => t.Tag)
            .Where(t => !templateTags.Contains(t))
            .ToList();
        Assert.Empty(missing);
        foreach (var condition in catalog.Where(t => t.Kind == TemplateTagCatalog.IfKind).Select(t => t.ConditionName!))
            Assert.True(templateTags.Contains("if:" + condition) || templateTags.Contains("ifnot:" + condition), condition);
    }

    [Theory]
    [MemberData(nameof(Forms))]
    public void Template_RendersValidDocument_WithoutControls(string templateFileName, object formData)
    {
        var result = Render(templateFileName, formData);

        Assert.DoesNotContain(result.MissingTags, t => !t.StartsWith("ORG_", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(Validate(result.Content));
        using var doc = Open(result.Content);
        Assert.DoesNotContain(doc.MainDocumentPart!.Document!.Body!.Descendants<SdtElement>(), sdt => sdt.SdtProperties?.GetFirstChild<Tag>() != null);
    }

    [Fact]
    public void Mau07_FillsSectionsScoresAndHeader_KeepsOriginalHeadings()
    {
        var text = BodyText(Render(Mau07Data.TemplateFileName, Mau07Sample()).Content);

        Assert.Contains("ĐẢNG BỘ CÔNG TY A", text);
        Assert.Contains("CHI BỘ KỸ THUẬT", text);
        Assert.Contains("CỦA TẬP THỂ CHI ỦY CHI BỘ KỸ THUẬT", text);
        Assert.Contains("Quý III Năm 2026", text);
        Assert.Contains("Nội dung mục I.1", text);
        Assert.Contains("Nội dung mục I.4", text);
        Assert.Contains("Hạn chế: chậm báo cáo", text);
        Assert.Contains("1. Nhóm tiêu chí chung: 27,5/30", text);
        Assert.Contains("Tổng điểm: 90,5/100", text);
        // Chữ in sẵn của biểu mẫu gốc.
        Assert.Contains("A. NỘI DUNG TỰ ĐÁNH GIÁ", text);
        Assert.Contains("B. KẾT QUẢ CHẤM ĐIỂM CÁC TIÊU CHÍ ĐÁNH GIÁ", text);
        Assert.Contains("T/M ĐẢNG ỦY (CHI BỘ)", text);
    }

    [Fact]
    public void Mau08_Always13CategoryRows_TasksUnderTheirCategory()
    {
        var result = Render(Mau08Data.TemplateFileName, Mau08Sample());
        using var doc = Open(result.Content);
        var rows = doc.MainDocumentPart!.Document!.Body!.Descendants<TableRow>().Select(r => r.InnerText).ToList();

        foreach (var category in Hd03FormCatalog.Form08Categories)
            Assert.Single(rows, r => r.StartsWith(category.Code + category.Title, StringComparison.Ordinal));
        var first = rows.Single(r => r.StartsWith("1Thực hiện nhiệm vụ sản xuất", StringComparison.Ordinal));
        Assert.Contains("- Bảo dưỡng radar", first);
        Assert.Contains("Radar hoạt động 99,9%", first);
        // Nhóm có dòng "- Nhiệm vụ 1: …" trong biểu mẫu gốc mà chưa nhập → giữ chữ mẫu.
        Assert.Contains(rows, r => r.StartsWith("2Công tác xây dựng Đảng.", StringComparison.Ordinal) && r.Contains("Nhiệm vụ 1: …"));
        Assert.Contains("QUÝ III/NĂM 2026", BodyText(result.Content));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Mau12_FillsMeetingFields_ByStage(bool withAttendees)
    {
        var text = BodyText(Render(Mau12Data.TemplateFileName, Mau12Sample(withAttendees)).Content);

        Assert.Contains("Hội nghị Chi bộ Kỹ thuật về việc đánh giá, xếp loại chất lượng cán bộ quý III/2026", text);
        Assert.Contains("bỏ phiếu quyết định, phê duyệt mức xếp loại", text);
        Assert.Contains("Bắt đầu vào hồi 08h30, ngày 15/09/2026", text);
        Assert.Contains("Hội nghị kết thúc vào hồi 11h00", text);
        Assert.Contains("Phòng họp tầng 3", text);
        Assert.Contains("4. Chủ trì Hội nghị: Đồng chí Nguyễn Văn A - Bí thư Chi bộ.", text);
        Assert.Contains("Căn cứ Quy chế làm việc của Chi bộ Kỹ thuật nhiệm kỳ 2025–2030;", text);
        Assert.Contains("Hội nghị thống nhất kết quả", text);
        if (withAttendees)
            Assert.Contains("(1) Đồng chí Lê Văn C - Đảng viên, ghi biên bản.", text);
        else
            Assert.DoesNotContain("Lê Văn C", text);
    }

    [Fact]
    public void Mau16_PrintsCountsPercentAndManualSections()
    {
        var text = BodyText(Render(Mau16Data.TemplateFileName, Mau16Sample()).Content);

        Assert.Contains("Số 12-BC/CB", text);
        Assert.Contains("Kính gửi: Đảng ủy Công ty A", text);
        Assert.Contains("quý III năm 2026", text);
        Assert.Contains("Bí thư các chi bộ trực thuộc đảng ủy cơ sở.", text);
        Assert.Contains("50,0%", text);
        Assert.Contains("1. Đề nghị Đảng ủy Công ty A quyết định các trường hợp tại mục I.", text);
        // Mục không nhập tay giữ đoạn mẫu của biểu mẫu gốc.
        Assert.Contains("2. Kính đề nghị Đảng ủy cấp trên xem xét", text);
        Assert.Contains("Trần Văn Bí Thư", text);
    }

    [Fact]
    public void CollectiveFormText_FormatsQuarterTimeAndLines()
    {
        Assert.Equal("III", CollectiveFormText.RomanQuarter(EvaluationQuarter.Quy3));
        Assert.Equal("08h30", CollectiveFormText.Time(new DateTime(2026, 9, 15, 1, 30, 0, DateTimeKind.Utc)));
        Assert.Equal("16/09/2026", CollectiveFormText.Date(new DateTime(2026, 9, 15, 18, 0, 0, DateTimeKind.Utc)));
        Assert.Equal("A", CollectiveFormText.Lines(new[] { "A", " ", null }));
        Assert.Equal("- A\n- B", CollectiveFormText.Lines(new[] { "A", "B" }));
        Assert.Null(CollectiveFormText.Lines(new string?[] { null, "" }));
    }

    #region Dữ liệu mẫu

    private static Mau07Data Mau07Sample()
    {
        var record = new CollectiveEvaluationRecord
        {
            Period = Period, Form = CollectiveEvaluationForm.M07, SubjectName = "Chi ủy Chi bộ Kỹ thuật",
            Limitations = "Hạn chế: chậm báo cáo", Causes = "Nguyên nhân chủ quan", RemediationPlan = "Khắc phục trong quý IV",
            GeneralCriteriaScore = 27.5, TaskCriteriaScore = 63, TotalScore = 90.5
        };
        var sections = new Dictionary<string, string> { ["I.1"] = "Nội dung mục I.1", ["I.4"] = "Nội dung mục I.4" };
        return Mau07Data.From(record, Header, sections);
    }

    private static Mau08Data Mau08Sample()
    {
        var record = new CollectiveEvaluationRecord { Period = Period, Form = CollectiveEvaluationForm.M08, SubjectName = "Phòng Kỹ thuật" };
        record.Items.Add(new CollectiveEvaluationItem { ItemOrder = 1, Category = "1", TaskName = "Bảo dưỡng radar", PlanOrDirection = "Kế hoạch 2026", Result = "Radar hoạt động 99,9%" });
        record.Items.Add(new CollectiveEvaluationItem { ItemOrder = 2, Category = "8", TaskName = "Không để xảy ra sự cố", Result = "Đạt" });
        return Mau08Data.From(record, Header);
    }

    private static Mau12Data Mau12Sample(bool withAttendees)
    {
        var meeting = new EvaluationMeeting
        {
            Period = Period, FormCode = "M12", Stage = WorkflowStep.B4_DECISION, Location = "Phòng họp tầng 3",
            StartedAt = new DateTime(2026, 9, 15, 1, 30, 0, DateTimeKind.Utc), EndedAt = new DateTime(2026, 9, 15, 4, 0, 0, DateTimeKind.Utc),
            InvitedCount = 9, PresentCount = 8, AbsentCount = 1, ChairName = "Nguyễn Văn A", SecretaryName = "Trần Thị B",
            OutcomeContent = "Hội nghị thống nhất kết quả"
        };
        var details = new MeetingDetailsDto
        {
            WorkingRules = "Chi bộ Kỹ thuật nhiệm kỳ 2025–2030", ChairTitle = "Bí thư Chi bộ", SecretaryTitle = "Chi ủy viên",
            Attendees = withAttendees ? new List<MeetingAttendeeDto> { new() { Name = "Lê Văn C", Title = "Đảng viên, ghi biên bản" } } : new()
        };
        return Mau12Data.From(meeting, Header, "Chi bộ Kỹ thuật", "Chi bộ Kỹ thuật", details);
    }

    private static Mau16Data Mau16Sample()
    {
        var draft = new Form16DraftDto
        {
            Content = new Form16DraftContentDto
            {
                DocumentNumber = "Số 12-BC/CB", Recipient = "Đảng ủy Công ty A", MeetingDate = "15/9/2026",
                Proposal1 = "Đề nghị Đảng ủy Công ty A quyết định các trường hợp tại mục I.", SignerName = "Trần Văn Bí Thư"
            },
            BaseRows = new List<Form16SummaryRowDto>
            {
                new() { StatCode = "M22", Subject = Hd03FormCatalog.StatCodeSubjects["M22"], Total = 2, Excellent = 1, Good = 1, ExcellentPercent = 50 },
                new() { Subject = "Tổng cộng", Total = 2, Excellent = 1, Good = 1, ExcellentPercent = 50 }
            },
            SuperiorRows = new List<Form16SummaryRowDto> { new() { Subject = "Tổng cộng" } }
        };
        return Mau16Data.From(Period, Header, draft);
    }

    #endregion

    #region Hỗ trợ

    private static DocxRenderResult Render(string templateFileName, object formData)
    {
        var template = new FileWordTemplateStore().Load(templateFileName);
        var org = OrganizationTemplateFields.From("ĐẢNG BỘ A", "ĐẢNG BỘ CẤP TRÊN", "Công ty A", "Tổng công ty B", "A", "Hà Nội");
        return DocxTemplateEngine.Render(template, TemplateDataBinder.Bind(formData).WithShared(TemplateDataBinder.Bind(org)));
    }

    private static WordprocessingDocument Open(byte[] content) => WordprocessingDocument.Open(new MemoryStream(content), false);

    /// <summary>Toàn văn thân tài liệu, mỗi đoạn một dòng (ngắt dòng trong đoạn thành "\n").</summary>
    private static string BodyText(byte[] content)
    {
        using var doc = Open(content);
        return string.Join("\n", doc.MainDocumentPart!.Document!.Body!.Descendants<Paragraph>().Select(p =>
            string.Concat(p.Descendants().Select(e => e switch
            {
                Text t => t.Text,
                TabChar => "\t",
                Break => "\n",
                _ => string.Empty
            }))));
    }

    private static List<string> Validate(byte[] content)
    {
        using var doc = Open(content);
        return new OpenXmlValidator(FileFormatVersions.Office2019)
            .Validate(doc)
            .Select(e => $"{e.Description} @ {e.Path?.XPath}")
            .ToList();
    }

    #endregion
}
