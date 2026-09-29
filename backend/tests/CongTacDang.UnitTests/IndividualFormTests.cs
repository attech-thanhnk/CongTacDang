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
/// Task 18 (T-82, T-83): cấu hình biểu mẫu cá nhân trong bộ tiêu chí, kiểm tra nội dung Mẫu 09C/9D/phần tự luận 09B, template
/// Mẫu 09A/09B/09C/9D dựng từ file biểu mẫu gốc (tag khớp lớp dữ liệu, xuất ra hợp lệ, có nội dung đã nhập).
/// </summary>
public class IndividualFormTests
{
    #region Bộ tiêu chí: danh sách biểu mẫu, mục 09C

    [Fact]
    public void Defaults_DeclareRequiredForms_And09CSection_FromOriginalForms()
    {
        var b = CriteriaSetDefaults.Build09B();
        var a = CriteriaSetDefaults.Build09A();

        Assert.Equal(new[] { "09B", "09C", "9D", "10" }, b.RequiredForms);
        Assert.Equal(new[] { "01", "02", "09A", "09C", "9D", "10" }, a.RequiredForms);

        var section = Assert.Single(b.SelfAssessmentSections);
        Assert.Equal("I", section.Code);
        Assert.Equal("I. Tự đánh giá kết quả thực hiện chức trách, nhiệm vụ được giao", section.Title);
        Assert.StartsWith("Trên cơ sở nhiệm vụ được giao", section.Guidance);
        Assert.StartsWith("(Lưu ý: Viết tóm tắt theo kết quả 6 trục.", section.Note);

        // Tiêu đề trục in trên Mẫu 09B lấy nguyên văn biểu mẫu gốc.
        Assert.Equal("TRỤC (1) – NHIỆM VỤ CHÍNH TRỊ, SẢN XUẤT KINH DOANH, CUNG CẤP DỊCH VỤ", b.Axes[0].FormTitle);
        Assert.StartsWith("- Đảm bảo cung cấp dịch vụ BĐHĐB", b.Axes[0].FormGuidance);
        Assert.All(b.Axes, axis => Assert.StartsWith($"TRỤC ({b.Axes.IndexOf(axis) + 1})", axis.FormTitle));

        Assert.Empty(b.Validate(CriteriaSetContent.Form09B));
        Assert.Empty(a.Validate(CriteriaSetContent.Form09A));
    }

    [Fact]
    public void Validate_RejectsInvalidFormConfiguration()
    {
        static List<string> Errors(Action<CriteriaSetContent> change)
        {
            var content = CriteriaSetDefaults.Build09B();
            change(content);
            return content.Normalize().Validate(CriteriaSetContent.Form09B);
        }

        Assert.Contains(Errors(c => c.RequiredForms.Add("07")), e => e.Contains("\"07\"", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.RequiredForms.Add("mẫu 09c")), e => e.Contains("nhiều lần", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.SelfAssessmentSections.Clear()), e => e.Contains("ít nhất một mục", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.SelfAssessmentSections[0].Code = "I 1"), e => e.Contains("Mã mục Mẫu 09C", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.SelfAssessmentSections[0].Title = " "), e => e.Contains("chưa có tiêu đề", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.SelfAssessmentSections[0].MaxLength = 50), e => e.Contains("từ 100 đến", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.SelfAssessmentSections.Add(new SelfAssessmentSection { Code = "i", Title = "Trùng" })), e => e.Contains("bị trùng", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.Axes[0].FormTitle = new string('x', 501)), e => e.Contains("Tiêu đề in trên Mẫu 09B", StringComparison.Ordinal));

        // Không áp dụng 09C thì không bắt buộc khai báo mục.
        Assert.Empty(Errors(c => { c.RequiredForms.Remove("09C"); c.SelfAssessmentSections.Clear(); }));
    }

    [Fact]
    public void ApplicableForms_FollowSelfScoreForm_AndRequiredForms()
    {
        var content = CriteriaSetDefaults.Build09A();
        var snapshot = new CriteriaSnapshot { SelfScoreForm = "09B", Content = content };

        // Mẫu tự chấm theo SelfScoreForm của bộ; 09A ghi trong RequiredForms bị bỏ qua.
        Assert.Equal(new[] { "01", "02", "09B", "09C", "9D", "10" }, snapshot.ApplicableForms());
        Assert.True(snapshot.AppliesForm("mau 09d"));
        Assert.False(snapshot.AppliesForm("09A"));

        content.RequiredForms = new List<string> { "09C" };
        Assert.Equal(new[] { "09B", "09C" }, snapshot.ApplicableForms());
        Assert.Equal("9D", RecordFormCodes.Normalize(" MAU_09D "));
        Assert.Equal("09C", RecordFormCodes.Normalize("Mẫu 09C"));
    }

    [Fact]
    public void Content_RoundTripsJson_WithFormFields()
    {
        var content = CriteriaSetDefaults.Build09B();
        var parsed = CriteriaSetContent.Parse(content.ToJson());
        Assert.Equal(content.ToJson(), parsed.ToJson());
        Assert.Equal(content.RequiredForms, parsed.RequiredForms);
        Assert.Equal(content.SelfAssessmentSections[0].Note, parsed.SelfAssessmentSections[0].Note);
        Assert.Contains("\"requiredForms\":[\"09B\",\"09C\",\"9D\",\"10\"]", content.ToJson());
    }

    #endregion

    #region Nội dung 09C / 9D / 09B theo trục

    [Fact]
    public void SelfAssessment_Validation_AndJson()
    {
        var c = CriteriaSetDefaults.Build09B();
        Assert.Null(RecordFormContent.ValidateSelfAssessment(c, new Dictionary<string, string?> { ["i"] = "Nội dung" }));
        Assert.Null(RecordFormContent.ValidateSelfAssessment(c, new Dictionary<string, string?>()));
        Assert.Contains("\"II\" không có", RecordFormContent.ValidateSelfAssessment(c, new Dictionary<string, string?> { ["II"] = "x" }));
        Assert.Contains("vượt giới hạn 6000", RecordFormContent.ValidateSelfAssessment(c, new Dictionary<string, string?> { ["I"] = new string('a', 6001) }));

        c.SelfAssessmentSections[0].Required = true;
        Assert.Contains("hãy nhập nội dung", RecordFormContent.ValidateSelfAssessment(c, new Dictionary<string, string?> { ["I"] = "  " }));

        var json = RecordFormContent.SelfAssessmentToJson(c, new Dictionary<string, string?> { ["i"] = "  Dòng 1\r\nDòng 2  " });
        Assert.Equal("{\"I\":\"Dòng 1\\nDòng 2\"}", json);
        Assert.Equal("Dòng 1\nDòng 2", RecordFormContent.ParseSelfAssessment(json)["I"]);
    }

    [Fact]
    public void TaskResults_Validation_AndJson_OrderedByAxis()
    {
        var c = CriteriaSetDefaults.Build09B();
        var rows = new List<TaskResultRow?>
        {
            new() { AxisCode = "t3", Content = "Việc trục 3", Deadline = "30/9/2026" },
            new() { AxisCode = "T1", Content = " Việc trục 1a ", Status = "Hoàn thành", Product = "Báo cáo", Progress = "Đúng hạn", Note = " " },
            new() { AxisCode = "T1", Content = "Việc trục 1b" }
        };
        Assert.Null(RecordFormContent.ValidateTaskResults(c, rows));

        var saved = RecordFormContent.ParseTaskResults(RecordFormContent.TaskResultsToJson(c, rows));
        Assert.Equal(new[] { "Việc trục 1a", "Việc trục 1b", "Việc trục 3" }, saved.Select(r => r.Content));
        Assert.Equal(new[] { "T1", "T1", "T3" }, saved.Select(r => r.AxisCode));
        Assert.Null(saved[0].Note);
        Assert.Equal("Đúng hạn", saved[0].Progress);

        Assert.Contains("trục \"T9\"", RecordFormContent.ValidateTaskResults(c, new List<TaskResultRow?> { new() { AxisCode = "T9", Content = "x" } }));
        Assert.Contains("dòng 1: hãy nhập nội dung", RecordFormContent.ValidateTaskResults(c, new List<TaskResultRow?> { new() { AxisCode = "T1", Content = " " } }));
        Assert.Contains("\"Đánh giá tiến độ\"", RecordFormContent.ValidateTaskResults(c, new List<TaskResultRow?> { new() { AxisCode = "T1", Content = "x", Progress = new string('a', 501) } }));
        var many = Enumerable.Range(0, RecordFormContent.MaxTaskResultRows + 1).Select(_ => (TaskResultRow?)new TaskResultRow { AxisCode = "T1", Content = "x" }).ToList();
        Assert.Contains("tối đa", RecordFormContent.ValidateTaskResults(c, many));
    }

    [Fact]
    public void AxisNotes_Validation_AndJson()
    {
        var c = CriteriaSetDefaults.Build09B();
        var notes = new Dictionary<string, AxisNote?>
        {
            ["t1"] = new() { Target = " Mục tiêu 1 ", Result = "Kết quả 1" },
            ["T2"] = new() { Note = "  " }
        };
        Assert.Null(RecordFormContent.ValidateAxisNotes(c, notes));
        var json = RecordFormContent.AxisNotesToJson(c, notes);
        var parsed = RecordFormContent.ParseAxisNotes(json);
        Assert.Equal(new[] { "T1" }, parsed.Keys);
        Assert.Equal("Mục tiêu 1", parsed["T1"].Target);
        Assert.Null(RecordFormContent.AxisNotesToJson(c, new Dictionary<string, AxisNote?> { ["T1"] = new() }));
        Assert.Contains("\"T8\"", RecordFormContent.ValidateAxisNotes(c, new Dictionary<string, AxisNote?> { ["T8"] = new() }));
        Assert.Contains("4000", RecordFormContent.ValidateAxisNotes(c, new Dictionary<string, AxisNote?> { ["T1"] = new() { Result = new string('a', 4001) } }));
    }

    #endregion

    #region Template 09A/09B/09C/9D

    public static IEnumerable<object[]> Forms()
    {
        var source = Source();
        yield return new object[] { Mau09AData.TemplateFileName, Mau09AData.From(source) };
        yield return new object[] { Mau09BData.TemplateFileName, Mau09BData.From(source) };
        yield return new object[] { Mau09CData.TemplateFileName, Mau09CData.From(source) };
        yield return new object[] { Mau9DData.TemplateFileName, Mau9DData.From(source) };
    }

    [Theory]
    [MemberData(nameof(Forms))]
    public void Template_TagsMatchFormDataClass(string templateFileName, object formData)
    {
        var template = new FileWordTemplateStore().Load(templateFileName);
        var templateTags = DocxTemplateEngine.GetTags(template).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dataTags = TemplateTagCatalog.Describe(formData.GetType()).Select(t => t.Tag).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var commonTags = OrganizationTemplateFields.Tags.Select(t => t.Tag);

        Assert.Empty(templateTags.Except(dataTags.Concat(commonTags), StringComparer.OrdinalIgnoreCase).OrderBy(t => t));
        Assert.Empty(dataTags.Except(templateTags, StringComparer.OrdinalIgnoreCase).OrderBy(t => t));
        Assert.NotNull(WordFormCatalog.FindByFileName(templateFileName));
    }

    [Theory]
    [MemberData(nameof(Forms))]
    public void Template_RendersValidDocument_WithoutLeftoverTags(string templateFileName, object formData)
    {
        var text = Render(templateFileName, formData, out var content);

        Assert.Empty(DocumentTemplateTests.Validate(content));
        Assert.DoesNotContain("«", text);
        using var doc = WordprocessingDocument.Open(new MemoryStream(content), false);
        Assert.DoesNotContain(doc.MainDocumentPart!.Document!.Body!.Descendants<SdtElement>(), sdt => sdt.SdtProperties?.GetFirstChild<Tag>() != null);
        // Tên Đảng bộ, Chi bộ, kỳ từ dữ liệu (không còn dòng chấm tiêu đề).
        Assert.Contains("ĐẢNG BỘ THỬ NGHIỆM", text);
        Assert.Contains("CHI BỘ KỸ THUẬT", text);
        Assert.Contains("Nguyễn Văn A", text);
    }

    [Fact]
    public void Mau09B_ShowsGroupsAxesScoresAndNotes()
    {
        var source = Source();
        var text = Render(Mau09BData.TemplateFileName, Mau09BData.From(source), out var content);

        Assert.Contains("Mẫu số 09B", text);
        Assert.Contains("TRỤC (1) – NHIỆM VỤ CHÍNH TRỊ", text);
        Assert.Contains("TRỤC (6) – THỰC HIỆN NHIỆM VỤ QUỐC PHÒNG", text);
        Assert.Contains("Mục tiêu trục 1 (test)", text);
        Assert.Contains("Kết quả trục 1 (test)", text);
        Assert.Contains("Hoàn thành tốt nhiệm vụ", text);
        Assert.Contains("Bí thư Chi bộ; Chủ tịch Công đoàn; Trưởng phòng", text);

        using var doc = WordprocessingDocument.Open(new MemoryStream(content), false);
        var tables = doc.MainDocumentPart!.Document!.Body!.Elements<Table>().ToList();
        // Nhóm I: 1 dòng tiêu đề + 3 nhóm + 17 tiêu chí con; nhóm II: 1 + 6 trục.
        Assert.Equal(1 + 3 + 17, tables[1].Elements<TableRow>().Count());
        Assert.Equal(1 + 6, tables[2].Elements<TableRow>().Count());
        var item21 = tables[1].Elements<TableRow>().Single(r => r.Elements<TableCell>().First().InnerText == "2.1");
        var cells = item21.Elements<TableCell>().Select(c => c.InnerText).ToList();
        Assert.Equal(("", "X", "1", "0", "Căn cứ giảm điểm"), (cells[2], cells[3], cells[4], cells[5], cells[6]));
        var axis1 = tables[2].Elements<TableRow>().ElementAt(1).Elements<TableCell>().Select(c => c.InnerText).ToList();
        Assert.Equal(("15", "14"), (axis1[3], axis1[4]));
        var summary = tables[3].Elements<TableRow>().Select(r => r.Elements<TableCell>().ElementAt(2).InnerText).ToList();
        Assert.Equal(new[] { "29,0", "64,0", "93,0", "Hoàn thành tốt nhiệm vụ" }, summary.Skip(2));
    }

    [Fact]
    public void Mau09C_ShowsSectionContent_ScoresAndPositions()
    {
        var text = Render(Mau09CData.TemplateFileName, Mau09CData.From(Source()), out _);

        Assert.Contains("BẢN TỰ ĐÁNH GIÁ, XẾP LOẠI CỦA CÁ NHÂN", text);
        Assert.Contains("I. Tự đánh giá kết quả thực hiện chức trách, nhiệm vụ được giao", text);
        Assert.Contains("Tự đánh giá mục I (test)", text);
        // Mẫu gốc dùng khoảng trắng không ngắt (U+00A0) sau dấu hai chấm.
        Assert.Matches(@"Chức vụ Đảng:\s+Bí thư Chi bộ", text);
        Assert.Matches(@"Chức vụ chính quyền:\s+Trưởng phòng", text);
        Assert.Matches(@"Chức vụ đoàn thể:\s+Chủ tịch Công đoàn", text);
        Assert.Contains("29,0/30", text);
        Assert.Contains("93,0/100", text);
        Assert.Contains("Hà Nội, ngày", text);
    }

    [Fact]
    public void Mau9D_GroupsRowsUnderEachAxis()
    {
        Render(Mau9DData.TemplateFileName, Mau9DData.From(Source()), out var content);

        using var doc = WordprocessingDocument.Open(new MemoryStream(content), false);
        var rows = doc.MainDocumentPart!.Document!.Body!.Elements<Table>().Single().Elements<TableRow>()
            .Select(r => r.Elements<TableCell>().Select(c => c.InnerText).ToList()).ToList();
        // 2 dòng tiêu đề + 6 dòng trục + 3 dòng nhiệm vụ.
        Assert.Equal(2 + 6 + 3, rows.Count);
        Assert.Equal("Trục 1", rows[2][0]);
        Assert.Equal(new[] { "1", "Việc T1 thứ nhất", "30/9/2026", "Đã xong", "Báo cáo", "Đúng hạn", "" }, rows[3]);
        Assert.Equal(new[] { "2", "Việc T1 thứ hai" }, rows[4].Take(2));
        Assert.Equal("Trục 2", rows[5][0]);
        Assert.Equal("Trục 3", rows[6][0]);
        Assert.Equal(new[] { "1", "Việc T3" }, rows[7].Take(2));
        Assert.Equal("Trục 6", rows[10][0]);
    }

    [Fact]
    public void Mau09A_ShowsTasksExceedAndGradeCheckbox()
    {
        var source = Source();
        var text = Render(Mau09AData.TemplateFileName, Mau09AData.From(source), out _);

        Assert.Contains("1 / 2 sản phẩm (đạt tỷ lệ: 50%)", text);
        Assert.Contains("☒ Hoàn thành tốt", text);
        Assert.Contains("☐ Hoàn thành xuất sắc", text);
    }

    [Fact]
    public void NotYetSelfScored_KeepsTemplateDots_ForScores()
    {
        var source = Source();
        source.Record.SelfScoredAt = null;
        var data = Mau09CData.From(source);
        Assert.Null(data.GeneralScore);
        Assert.Null(data.TotalScore);
    }

    #endregion

    #region Hỗ trợ

    private static string Render(string templateFileName, object formData, out byte[] content)
    {
        var template = new FileWordTemplateStore().Load(templateFileName);
        var org = OrganizationTemplateFields.From("ĐẢNG BỘ THỬ NGHIỆM", "ĐẢNG BỘ CẤP TRÊN", "Công ty A", "Tổng công ty B", "A", "Hà Nội");
        var result = DocxTemplateEngine.Render(template, TemplateDataBinder.Bind(formData).WithShared(TemplateDataBinder.Bind(org)));
        Assert.Empty(result.MissingTags);
        content = result.Content;
        using var doc = WordprocessingDocument.Open(new MemoryStream(content), false);
        return string.Join("\n", doc.MainDocumentPart!.Document!.Body!.Descendants<Paragraph>().Select(p => p.InnerText));
    }

    private static IndividualFormSource Source()
    {
        var criteria = CriteriaSetDefaults.Build09B();
        var period = new EvaluationPeriod { Year = 2026, Quarter = EvaluationQuarter.Quy3, Name = "Quý III/2026" };
        var cell = new PartyCell { Name = "Chi bộ Kỹ thuật" };
        var dept = new AdministrativeDepartment { Name = "Phòng Kỹ thuật" };
        var member = new PartyMemberProfile { FullName = "Nguyễn Văn A", PositionTitle = "Trưởng phòng", PartyCell = cell, Department = dept };
        var general = criteria.AllItems.ToDictionary(
            x => x.Item.Code,
            x => x.Item.Code == "2.1" ? new GeneralItemScore { Score = 0, Reason = "Căn cứ giảm điểm" } : new GeneralItemScore { Score = x.Item.MaxScore });
        var record = new EvaluationRecord
        {
            Period = period, Member = member, PartyCell = cell, Department = dept,
            GeneralScores = EvaluationScoring.GeneralScoresToJson(criteria, general),
            GeneralCriteriaScore = 29, TasksScore = 64, TotalSelfScore = 93,
            AxisScores = EvaluationScoring.AxisScoresToJson(criteria, new Dictionary<string, double> { ["T1"] = 14, ["T2"] = 9, ["T3"] = 9, ["T4"] = 14, ["T5"] = 9, ["T6"] = 9 }),
            SelfProposedGrade = EvaluationGrade.HoanThanhTot,
            SelfScoredAt = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc),
            SelfAssessment = RecordFormContent.SelfAssessmentToJson(criteria, new Dictionary<string, string?> { ["I"] = "Tự đánh giá mục I (test)" }),
            TaskResults = RecordFormContent.TaskResultsToJson(criteria, new List<TaskResultRow?>
            {
                new() { AxisCode = "T1", Content = "Việc T1 thứ nhất", Deadline = "30/9/2026", Status = "Đã xong", Product = "Báo cáo", Progress = "Đúng hạn" },
                new() { AxisCode = "T3", Content = "Việc T3" },
                new() { AxisCode = "T1", Content = "Việc T1 thứ hai" }
            }),
            AxisNotes = RecordFormContent.AxisNotesToJson(criteria, new Dictionary<string, AxisNote?>
            {
                ["T1"] = new() { Target = "Mục tiêu trục 1 (test)", Result = "Kết quả trục 1 (test)" }
            })
        };
        record.Tasks.Add(new EvaluationTask { TaskOrder = 1, TaskName = "Sản phẩm 1", Weight = 40, SelfScore = 38, IsExceedStandard = true });
        record.Tasks.Add(new EvaluationTask { TaskOrder = 2, TaskName = "Sản phẩm 2", Weight = 30, SelfScore = 26 });

        var positions = MemberPositionNames.From(new[]
        {
            (PositionSide.Party, "Bí thư Chi bộ"),
            (PositionSide.Administrative, "Trưởng phòng"),
            (PositionSide.MassOrganization, "Chủ tịch Công đoàn")
        });
        return new IndividualFormSource(record, criteria, positions);
    }

    #endregion
}
