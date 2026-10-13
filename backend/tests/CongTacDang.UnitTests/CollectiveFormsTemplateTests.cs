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

/// <summary>Task 19 (T-84, T-85): template Mẫu 07, 08, 11, 12, 13, 16 dựng từ biểu mẫu gốc — khớp tag, sinh tài liệu hợp lệ, đúng số liệu.</summary>
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
        yield return new object[] { Mau11Data.TemplateFileName, Mau11Data.From(period, records.Select(r => new Mau11Line(r, null)).ToList(), "Chi bộ Kỹ thuật") };
        yield return new object[] { Mau16Data.TemplateFileName, Mau16Sample() };
        yield return new object[] { MauQdData.TemplateFileName, MauQdSample() };
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
        // Dòng địa danh – ngày: địa danh từ cài đặt đơn vị, ngày lập = lần cập nhật hồ sơ (giờ Việt Nam).
        Assert.Contains("Hà Nội, ngày 21 tháng 09 năm 2026", text);
    }

    [Fact]
    public void Mau07_08_NeverSaved_UsesCreatedDate()
    {
        var record = new CollectiveEvaluationRecord
        {
            Period = Period, Form = CollectiveEvaluationForm.M08, CreatedAt = new DateTime(2026, 9, 1, 3, 0, 0, DateTimeKind.Utc)
        };
        Assert.Equal("ngày 01 tháng 09 năm 2026", Mau08Data.From(record, Header).DateLine);
        Assert.Equal("ngày 01 tháng 09 năm 2026", Mau07Data.From(record, Header, new Dictionary<string, string>()).DateLine);
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
        Assert.Contains("Hà Nội, ngày 21 tháng 09 năm 2026", BodyText(result.Content));
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
        // Chữ ký chủ trì: chức danh theo chức vụ chủ trì đã lưu; hội nghị cấp ủy (B4) giữ ghi chú đóng dấu.
        Assert.Contains("CHỦ TRÌ HỘI NGHỊ\nBÍ THƯ CHI BỘ\n(ký, ghi rõ họ tên, đóng dấu)", text);
    }

    [Fact]
    public void Mau12_ProposalMeeting_ChairSignsByStoredTitle_ArchiveNameInSentenceCase()
    {
        var org = new OrganizationSettingsDto
        {
            PartyCommitteeName = "ĐẢNG BỘ CÔNG TY TNHH KỸ THUẬT QUẢN LÝ BAY", SuperiorPartyName = "ĐẢNG BỘ TỔNG CÔNG TY QUẢN LÝ BAY VIỆT NAM",
            CompanyName = "Công ty TNHH Kỹ thuật Quản lý bay", ParentCompanyName = "Tổng công ty Quản lý bay Việt Nam"
        };
        var partyName = CollectiveFormText.PartyCommitteeInSentence(org);
        var meeting = new EvaluationMeeting
        {
            Period = Period, FormCode = "M12", Stage = WorkflowStep.B3A_COLLECTIVE, Location = "Phòng họp",
            StartedAt = new DateTime(2026, 9, 15, 1, 30, 0, DateTimeKind.Utc), ChairName = "Phạm Văn Trưởng", SecretaryName = "Trần Thị B"
        };
        var details = new MeetingDetailsDto { ChairTitle = "Trưởng phòng Kỹ thuật", SecretaryTitle = "Chuyên viên" };
        var header = PartyHeader.Of(org.SuperiorPartyName, org.PartyCommitteeName, partyName);

        var text = BodyText(Render(Mau12Data.TemplateFileName, Mau12Data.From(meeting, header, "Phòng Kỹ thuật", partyName, details)).Content);

        Assert.Contains("CHỦ TRÌ HỘI NGHỊ\nTRƯỞNG PHÒNG KỸ THUẬT\n(ký, ghi rõ họ tên)", text);
        Assert.DoesNotContain("BÍ THƯ", text);
        Assert.DoesNotContain("đóng dấu", text);
        // Giữa câu dùng tên thường; chỉ dòng tiêu đề in hoa.
        Assert.Contains("01 bản lưu tại Đảng bộ Công ty TNHH Kỹ thuật Quản lý bay và 01 bản lưu hồ sơ", text);
        // Tên dài ngắt thành hai dòng cân nhau ở cột tiêu đề trái.
        Assert.Contains("ĐẢNG BỘ CÔNG TY TNHH\nKỸ THUẬT QUẢN LÝ BAY", text);
        Assert.Contains("Hội nghị tập thể lãnh đạo, quản lý Phòng Kỹ thuật về việc", text);

        // Chưa ghi chức vụ chủ trì: hội nghị tập thể lãnh đạo không in "BÍ THƯ".
        var noTitle = Mau12Data.From(meeting, header, "Phòng Kỹ thuật", partyName, new MeetingDetailsDto());
        Assert.Equal(string.Empty, noTitle.ChairSignTitle);
    }

    [Fact]
    public void Mau16_WholeParty_OrganizerInSentenceCase_DateFromDraft()
    {
        var org = new OrganizationSettingsDto { PartyCommitteeName = "ĐẢNG BỘ CÔNG TY A", SuperiorPartyName = "ĐẢNG BỘ TỔNG CÔNG TY B", CompanyName = "Công ty A" };
        var header = PartyHeader.Of(org.SuperiorPartyName, org.PartyCommitteeName, CollectiveFormText.PartyCommitteeInSentence(org));
        var draft = new Form16DraftDto
        {
            UpdatedAt = new DateTime(2026, 9, 25, 20, 0, 0, DateTimeKind.Utc),
            Content = new Form16DraftContentDto { MeetingDate = "15/9/2026" },
            BaseRows = new List<Form16SummaryRowDto> { new() { Subject = "Tổng cộng" } },
            SuperiorRows = new List<Form16SummaryRowDto> { new() { Subject = "Tổng cộng" } }
        };

        var text = BodyText(Render(Mau16Data.TemplateFileName, Mau16Data.From(Period, header, draft)).Content);

        Assert.Contains("Ngày 15/9/2026, Đảng bộ Công ty A đã tổ chức Hội nghị", text);
        Assert.DoesNotContain("ĐẢNG BỘ CÔNG TY A đã", text);
        Assert.Contains("Hà Nội, ngày 26 tháng 09 năm 2026", text);
        Assert.Contains("ĐẢNG BỘ CÔNG TY A", text);
    }

    [Fact]
    public void CollectiveFormText_NameInSentence_CommitteeAndDateLine()
    {
        Assert.Equal("Đảng bộ Công ty TNHH Kỹ thuật Quản lý bay",
            CollectiveFormText.NameInSentence("ĐẢNG BỘ CÔNG TY TNHH KỸ THUẬT QUẢN LÝ BAY", "Công ty TNHH Kỹ thuật Quản lý bay", "Tổng công ty Quản lý bay Việt Nam"));
        // Tên riêng dài hơn được ưu tiên (tên đơn vị cấp trên chứa tên ngắn hơn).
        Assert.Equal("Đảng bộ Tổng công ty Quản lý bay Việt Nam",
            CollectiveFormText.NameInSentence("ĐẢNG BỘ TỔNG CÔNG TY QUẢN LÝ BAY VIỆT NAM", "Công ty Quản lý bay Việt Nam", "Tổng công ty Quản lý bay Việt Nam"));
        Assert.Equal("Chi bộ Kỹ thuật", CollectiveFormText.NameInSentence("Chi bộ Kỹ thuật", "Công ty A"));
        Assert.Equal("ĐẢNG BỘ X", CollectiveFormText.NameInSentence("ĐẢNG BỘ X", "Công ty A"));
        Assert.Null(CollectiveFormText.NameInSentence(" "));

        Assert.Equal("ĐẢNG ỦY CÔNG TY A", CollectiveFormText.CommitteeUpper("Đảng bộ Công ty A"));
        Assert.Equal("CHI BỘ KỸ THUẬT", CollectiveFormText.CommitteeUpper("Chi bộ Kỹ thuật"));
        Assert.Equal("ĐẢNG ỦY CÔNG TY A", CollectiveFormText.CommitteeUpper("Đảng ủy Công ty A"));
        Assert.Null(CollectiveFormText.CommitteeUpper(null));

        Assert.Equal("ngày 16 tháng 09 năm 2026", CollectiveFormText.DateLine(new DateTime(2026, 9, 15, 18, 0, 0, DateTimeKind.Utc)));
        Assert.Null(CollectiveFormText.DateLine(null));
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
    public void MauQd_FillsDecisionAndAppendixFromRecords()
    {
        var result = Render(MauQdData.TemplateFileName, MauQdSample());
        var text = BodyText(result.Content);

        // Trang Quyết định: tiêu đề, căn cứ, cấp ủy ra quyết định, nơi nhận, chữ ký — tên tổ chức lấy từ dữ liệu.
        Assert.Contains("ĐẢNG BỘ TỔNG CÔNG TY B", text);
        Assert.Contains("ĐẢNG BỘ CÔNG TY A", text);
        Assert.Contains("Số 15-QĐ/ĐU", text);
        Assert.Contains("Hà Nội, ngày 20 tháng 09 năm 2026", text);
        Assert.Contains("Xếp loại cán bộ Quý III năm 2026 đối với các trường hợp thuộc thẩm quyền quyết định của Đảng ủy Công ty A", text);
        Assert.Contains("của Thường vụ Đảng ủy Tổng công ty B về việc đánh giá, xếp loại cán bộ Quý III năm 2026;", text);
        Assert.Contains("Quy chế hoạt động của Ban Chấp hành Đảng bộ Công ty A, nhiệm kỳ 2025-2030;", text);
        Assert.Contains("Hội nghị Đảng ủy Công ty A ngày 18/09/2026", text);
        Assert.Contains("thuộc diện Ban Chấp hành Đảng bộ Công ty A đánh giá, xếp loại.", text);
        Assert.Contains("ĐẢNG ỦY CÔNG TY A QUYẾT ĐỊNH", text);
        Assert.Contains("Điều 1: Xếp loại cán bộ Quý III năm 2026", text);
        Assert.Contains("- BTV Đảng ủy Tổng công ty B (để b/c),", text);
        Assert.Contains("T/M ĐẢNG ỦY", text);
        Assert.Contains("Phạm Văn Chủ Trì", text);

        // Phụ lục: tiêu đề, dòng tham chiếu Quyết định, cột 8 theo tên tổ chức, dòng cán bộ đủ mức từng bước.
        Assert.Contains("KẾT QUẢ ĐÁNH GIÁ, XẾP LOẠI CÁN BỘ QUÝ III/2026 THUỘC THẨM QUYỀN QUYẾT ĐỊNH CỦA ĐẢNG ỦY CÔNG TY A", text);
        Assert.Contains("(kèm theo Quyết định số 15-QĐ/ĐU ngày 20/09/2026)", text);
        Assert.Contains("Ban Chấp hành Đảng bộ Công ty A đánh giá, xếp loại", text);
        using var doc = Open(result.Content);
        var rows = doc.MainDocumentPart!.Document!.Body!.Elements<Table>().Last().Elements<TableRow>().Skip(2)
            .Select(r => r.Elements<TableCell>().Select(c => c.InnerText).ToList())
            .ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "1", "Nguyễn Văn Một", "Trưởng phòng — Phòng Kỹ thuật", "Hoàn thành tốt nhiệm vụ", "Hoàn thành tốt nhiệm vụ",
            "Hoàn thành xuất sắc nhiệm vụ", "Hoàn thành tốt nhiệm vụ", "Hoàn thành tốt nhiệm vụ", "" }, rows[0]);
        // Bước không thực hiện (chưa có mức) để trống, không in "Chưa xếp loại".
        Assert.Equal(new[] { "2", "Trần Thị Hai", "Phó Bí thư Chi bộ", "Hoàn thành nhiệm vụ", "Hoàn thành nhiệm vụ", "", "",
            "Hoàn thành nhiệm vụ", "" }, rows[1]);
    }

    [Fact]
    public void MauQd_NoDocumentOrMeeting_KeepsDottedPlaceholders_ChiBoSigns()
    {
        var header = PartyHeader.Of("Đảng bộ Công ty A", "Chi bộ Kỹ thuật");
        var data = MauQdData.From(Period, header, "Đảng bộ Công ty A", new DecisionDocumentInfo(null, null, null, null),
            new[] { new MauQdLine("Lê Văn Ba", null, EvaluationGrade.HoanThanhTot, EvaluationGrade.ChuaXepLoai, EvaluationGrade.ChuaXepLoai,
                EvaluationGrade.ChuaXepLoai, EvaluationGrade.HoanThanhTot) });

        var text = BodyText(Render(MauQdData.TemplateFileName, data).Content);

        Assert.Contains("Số …..-QĐ/……", text);
        Assert.Contains("Hà Nội, ngày …… tháng …… năm ……", text);
        Assert.Contains("Hội nghị Chi bộ Kỹ thuật ngày …../…../…….", text);
        Assert.Contains("(kèm theo Quyết định số …..-QĐ/…… ngày …… tháng …… năm ……)", text);
        Assert.Contains("CHI BỘ KỸ THUẬT QUYẾT ĐỊNH", text);
        Assert.Contains("T/M CHI BỘ", text);
        Assert.Contains("- BTV Đảng ủy Công ty A (để b/c),", text);
        Assert.Null(data.SignerName);
    }

    [Fact]
    public void CollectiveFormText_CommitteeExecutiveTermAndSigner()
    {
        Assert.Equal("Đảng ủy Công ty A", CollectiveFormText.CommitteeInSentence("Đảng bộ Công ty A"));
        Assert.Equal("ĐẢNG ỦY TCT B", CollectiveFormText.CommitteeInSentence("ĐẢNG BỘ TCT B"));
        Assert.Equal("Chi bộ Kỹ thuật", CollectiveFormText.CommitteeInSentence("Chi bộ Kỹ thuật"));
        Assert.Null(CollectiveFormText.CommitteeInSentence(" "));
        Assert.Equal("Ban Chấp hành Đảng bộ Công ty A", CollectiveFormText.ExecutiveInSentence("Đảng bộ Công ty A"));
        Assert.Equal("Chi bộ Kỹ thuật", CollectiveFormText.ExecutiveInSentence("Chi bộ Kỹ thuật"));
        Assert.Equal("T/M ĐẢNG ỦY", CollectiveFormText.SignOnBehalf("ĐẢNG ỦY CÔNG TY A"));
        Assert.Equal("T/M CHI BỘ", CollectiveFormText.SignOnBehalf("CHI BỘ KỸ THUẬT"));
        Assert.Null(CollectiveFormText.SignOnBehalf("ĐẢNG BỘ X"));
        Assert.Equal("2025-2030", CollectiveFormText.PartyTerm(2025));
        Assert.Equal("2025-2030", CollectiveFormText.PartyTerm(2029));
        Assert.Null(CollectiveFormText.PartyTerm(2030));
        Assert.Equal("2030-2035", CollectiveFormText.PartyTerm(2031));
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
            GeneralCriteriaScore = 27.5, TaskCriteriaScore = 63, TotalScore = 90.5,
            UpdatedAt = new DateTime(2026, 9, 21, 2, 0, 0, DateTimeKind.Utc)
        };
        var sections = new Dictionary<string, string> { ["I.1"] = "Nội dung mục I.1", ["I.4"] = "Nội dung mục I.4" };
        return Mau07Data.From(record, Header, sections);
    }

    private static Mau08Data Mau08Sample()
    {
        var record = new CollectiveEvaluationRecord
        {
            Period = Period, Form = CollectiveEvaluationForm.M08, SubjectName = "Phòng Kỹ thuật",
            UpdatedAt = new DateTime(2026, 9, 21, 2, 0, 0, DateTimeKind.Utc)
        };
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

    private static MauQdData MauQdSample()
    {
        var header = PartyHeader.Of("ĐẢNG BỘ TỔNG CÔNG TY B", "ĐẢNG BỘ CÔNG TY A", "Đảng bộ Công ty A");
        var info = new DecisionDocumentInfo("Số 15-QĐ/ĐU", new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 18, 1, 30, 0, DateTimeKind.Utc), "Phạm Văn Chủ Trì");
        var lines = new[]
        {
            new MauQdLine("Nguyễn Văn Một", "Trưởng phòng — Phòng Kỹ thuật", EvaluationGrade.HoanThanhTot, EvaluationGrade.HoanThanhTot,
                EvaluationGrade.HoanThanhXuatSac, EvaluationGrade.HoanThanhTot, EvaluationGrade.HoanThanhTot),
            new MauQdLine("Trần Thị Hai", "Phó Bí thư Chi bộ", EvaluationGrade.HoanThanh, EvaluationGrade.HoanThanh,
                EvaluationGrade.ChuaXepLoai, EvaluationGrade.ChuaXepLoai, EvaluationGrade.HoanThanh)
        };
        return MauQdData.From(Period, header, "Đảng bộ Tổng công ty B", info, lines);
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
