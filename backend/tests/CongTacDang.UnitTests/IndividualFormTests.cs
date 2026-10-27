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

/// <summary>Biểu mẫu cá nhân trong bộ tiêu chí: kiểm tra nội dung 09C/9D và template 09A/09C/9D.</summary>
public class IndividualFormTests
{
    #region Bộ tiêu chí: danh sách biểu mẫu, mục 09C

    [Fact]
    public void Defaults_DeclareRequiredForms_And09CSection_FromOriginalForms()
    {
        var a = CriteriaSetDefaults.Build();
        Assert.Equal(new[] { "01", "02", "03", "09A", "09C", "9D", "10", "11", "17" }, a.RequiredForms);

        var section = Assert.Single(a.SelfAssessmentSections);
        Assert.Equal("I", section.Code);
        Assert.Equal("I. Tự đánh giá kết quả thực hiện chức trách, nhiệm vụ được giao", section.Title);
        Assert.StartsWith("Trên cơ sở nhiệm vụ được giao", section.Guidance);
        Assert.StartsWith("(Lưu ý: Viết tóm tắt theo kết quả 6 trục.", section.Note);

        Assert.Empty(a.Validate());
    }

    [Fact]
    public void Validate_RejectsInvalidFormConfiguration()
    {
        static List<string> Errors(Action<CriteriaSetContent> change)
        {
            var content = CriteriaSetDefaults.Build();
            change(content);
            return content.Normalize().Validate();
        }

        Assert.Contains(Errors(c => c.RequiredForms.Add("07")), e => e.Contains("\"07\"", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.RequiredForms.Add("mẫu 09c")), e => e.Contains("nhiều lần", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.SelfAssessmentSections.Clear()), e => e.Contains("ít nhất một mục", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.SelfAssessmentSections[0].Code = "I 1"), e => e.Contains("Mã mục Mẫu 09C", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.SelfAssessmentSections[0].Title = " "), e => e.Contains("chưa có tiêu đề", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.SelfAssessmentSections[0].MaxLength = 50), e => e.Contains("từ 100 đến", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.SelfAssessmentSections.Add(new SelfAssessmentSection { Code = "i", Title = "Trùng" })), e => e.Contains("bị trùng", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.RequiredForms.Add("09B")), e => e.Contains("\"09B\"", StringComparison.Ordinal));

        // Không áp dụng 09C thì không bắt buộc khai báo mục.
        Assert.Empty(Errors(c => { c.RequiredForms.Remove("09C"); c.SelfAssessmentSections.Clear(); }));
    }

    [Fact]
    public void ApplicableForms_Always09A_PlusRequiredForms()
    {
        var content = CriteriaSetDefaults.Build();
        var snapshot = new CriteriaSnapshot { Content = content };

        Assert.Equal(RecordFormCodes.All, snapshot.ApplicableForms());
        Assert.True(snapshot.AppliesForm("mau 09d"));

        // Mẫu 09A luôn áp dụng dù bộ không ghi trong danh sách.
        content.RequiredForms = new List<string> { "09C" };
        Assert.Equal(new[] { "09A", "09C" }, snapshot.ApplicableForms());
        Assert.Equal("9D", RecordFormCodes.Normalize(" MAU_09D "));
        Assert.Equal("09C", RecordFormCodes.Normalize("Mẫu 09C"));
    }

    [Fact]
    public void Content_RoundTripsJson_WithFormFields()
    {
        var content = CriteriaSetDefaults.Build();
        var parsed = CriteriaSetContent.Parse(content.ToJson());
        Assert.Equal(content.ToJson(), parsed.ToJson());
        Assert.Equal(content.RequiredForms, parsed.RequiredForms);
        Assert.Equal(content.SelfAssessmentSections[0].Note, parsed.SelfAssessmentSections[0].Note);
        Assert.Contains("\"requiredForms\":[\"01\",\"02\",\"03\",\"09A\",\"09C\",\"9D\",\"10\",\"11\",\"17\"]", content.ToJson());
    }

    #endregion

    #region Nội dung 09C / 9D theo trục

    [Fact]
    public void SelfAssessment_Validation_AndJson()
    {
        var c = CriteriaSetDefaults.Build();
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
    public void AxisReports_Validation_AndJson_OrderedByAxis()
    {
        var c = CriteriaSetDefaults.Build();
        var reports = new Dictionary<string, AxisReport?>
        {
            ["t3"] = new() { Tasks = { new() { Content = "Việc trục 3" } }, Deadline = "30/9/2026" },
            ["T1"] = new()
            {
                Tasks =
                {
                    new() { Content = " Việc trục 1a ", Level = 1 },
                    new() { Content = "Việc trục 1b", Items = { new() { Content = " Ý con ", Level = 2 } } }
                },
                Status = "- Việc 1a\n- Việc 1b", Product = "Báo cáo", Progress = "Đúng hạn", Note = " "
            },
            ["T2"] = new() { Note = "  " }
        };
        Assert.Null(RecordFormContent.ValidateAxisReports(c, reports));

        var json = RecordFormContent.AxisReportsToJson(c, reports);
        var saved = RecordFormContent.ParseAxisReports(json);
        // Theo thứ tự trục của bộ, khóa theo mã trục của bộ; trục không có nội dung bị bỏ.
        Assert.Equal(new[] { "T1", "T3" }, saved.Keys);
        Assert.Equal(new[] { "Việc trục 1a", "Việc trục 1b" }, saved["T1"].Tasks.Select(t => t.Content));
        Assert.Equal("Ý con", saved["T1"].Tasks[1].Items.Single().Content);
        Assert.Equal(2, saved["T1"].Tasks[1].Items.Single().Level);
        Assert.Null(saved["T1"].Note);
        Assert.Equal("Đúng hạn", saved["T1"].Progress);
        Assert.Equal("30/9/2026", saved["T3"].Deadline);

        // Kỳ chỉ áp dụng 9D: không lưu nhiệm vụ; chỉ áp dụng 09C: không lưu phần tóm tắt.
        var summaryOnly = RecordFormContent.ParseAxisReports(RecordFormContent.AxisReportsToJson(c, reports, includeTasks: false));
        Assert.All(summaryOnly.Values, r => Assert.Empty(r.Tasks));
        var tasksOnly = RecordFormContent.ParseAxisReports(RecordFormContent.AxisReportsToJson(c, reports, includeSummary: false));
        Assert.Null(tasksOnly["T1"].Status);
        Assert.Null(tasksOnly["T3"].Deadline);

        static string? Error(AxisReport report, string axis = "T1") =>
            RecordFormContent.ValidateAxisReports(CriteriaSetDefaults.Build(), new Dictionary<string, AxisReport?> { [axis] = report });
        Assert.Contains("Trục \"T9\"", Error(new AxisReport(), "T9"));
        Assert.Contains("Trục 1, nhiệm vụ 1: hãy nhập nội dung", Error(new AxisReport { Tasks = { new() { Content = " " } } }));
        Assert.Contains("Trục 1, nhiệm vụ 1, ý 1: hãy nhập nội dung", Error(new AxisReport { Tasks = { new() { Content = "x", Items = { new() { Content = "" } } } } }));
        Assert.Contains("\"Đánh giá tiến độ\"", Error(new AxisReport { Progress = new string('a', 501) }));
        Assert.Contains("tối đa", Error(new AxisReport { Tasks = Enumerable.Range(0, RecordFormContent.MaxTasksPerAxis + 1).Select(_ => new AxisTask { Content = "x" }).ToList() }));
        Assert.Contains("tối đa", Error(new AxisReport { Tasks = { new() { Content = "x", Items = Enumerable.Range(0, RecordFormContent.MaxItemsPerTask + 1).Select(_ => new AxisTaskItem { Content = "y" }).ToList() } } }));
        Assert.Contains("mức tự đánh giá", Error(new AxisReport { Tasks = { new() { Content = "x", Level = 4 } } }));
        Assert.Contains("ý 1: mức tự đánh giá", Error(new AxisReport { Tasks = { new() { Content = "x", Items = { new() { Content = "y", Level = 4 } } } } }));
        Assert.Null(Error(new AxisReport { Tasks = { new() { Content = "x", Level = 3 } } }));
    }

    [Fact]
    public void AxisTaskBlocks_FollowStandardWording_AndCountTowards09CLimit()
    {
        var c = CriteriaSetDefaults.Build();
        var reports = RecordFormContent.ParseAxisReports(RecordFormContent.AxisReportsToJson(c, new Dictionary<string, AxisReport?>
        {
            ["T1"] = new()
            {
                Tasks =
                {
                    new() { Content = "Nhóm nhiệm vụ KHCN. 02 đề tài chậm tiến độ", Level = 2 },
                    new() { Content = "Lãnh đạo SXKD.", Level = 1 }
                }
            },
            ["T2"] = new() { Status = "Chỉ có phần tóm tắt 9D" },
            ["T3"] = new()
            {
                Tasks = { new() { Content = "Thực hiện Kế hoạch 824", Items = { new() { Content = "Phối hợp sửa biểu mẫu", Level = 1 }, new() { Content = "Đề tài chậm tiến độ" } } } }
            }
        }));

        var blocks = RecordFormContent.BuildAxisTaskBlocks(c, reports);
        // Trục không có nhiệm vụ không in; số thứ tự trục giữ theo bộ tiêu chí.
        Assert.Equal(new[] { $"1. {c.Axes[0].Name} (trục 1)", $"3. {c.Axes[2].Name} (trục 3)" }, blocks.Select(b => b.Title));
        Assert.Equal(new[]
        {
            "- Nhiệm vụ 1: Nhóm nhiệm vụ KHCN. 02 đề tài chậm tiến độ. Tự đánh giá mức 2.",
            "- Nhiệm vụ 2: Lãnh đạo SXKD. Tự đánh giá mức 1."
        }, blocks[0].Lines.Select(l => l.FullText));
        Assert.Equal(new[]
        {
            "- Nhiệm vụ 1: Thực hiện Kế hoạch 824.",
            "+ Phối hợp sửa biểu mẫu. Tự đánh giá mức 1.",
            "+ Đề tài chậm tiến độ."
        }, blocks[1].Lines.Select(l => l.FullText));
        // Câu nối mức tách riêng (in nghiêng); không chọn mức thì không có.
        Assert.Equal(new AxisTaskLine("- Nhiệm vụ 1: Nhóm nhiệm vụ KHCN. 02 đề tài chậm tiến độ.", "Tự đánh giá mức 2."), blocks[0].Lines[0]);
        Assert.Null(blocks[1].Lines[0].Level);
        Assert.Equal(blocks.Sum(b => b.Title.Length + 1 + b.Lines.Sum(l => l.FullText.Length + 1)), RecordFormContent.AxisTaskTextLength(c, reports));

        // Giới hạn ký tự của mục I tính cả phần nhiệm vụ theo trục.
        var taskLength = RecordFormContent.AxisTaskTextLength(c, reports);
        Assert.True(taskLength > 0);
        var limit = c.SelfAssessmentSections[0].MaxLength;
        Assert.Null(RecordFormContent.ValidateSelfAssessment(c, new Dictionary<string, string?> { ["I"] = new string('a', limit - taskLength) }, reports));
        Assert.Contains("nhận xét chung và nhiệm vụ theo trục", RecordFormContent.ValidateSelfAssessment(c, new Dictionary<string, string?> { ["I"] = new string('a', limit - taskLength + 1) }, reports));

        // Mục bắt buộc: có nhiệm vụ theo trục là đã có nội dung.
        c.SelfAssessmentSections[0].Required = true;
        Assert.Null(RecordFormContent.ValidateSelfAssessment(c, new Dictionary<string, string?>(), reports));
        Assert.Contains("hãy nhập nội dung", RecordFormContent.ValidateSelfAssessment(c, new Dictionary<string, string?>(), new Dictionary<string, AxisReport>()));
    }

    [Fact]
    public void TaskLevels_AndWording_AreConfiguredInCriteriaSet()
    {
        var c = CriteriaSetDefaults.Build();
        Assert.Equal(new[] { 1, 2, 3 }, c.TaskLevels.Select(l => l.Code));
        Assert.Empty(c.Validate());

        static Dictionary<string, AxisReport?> One(int? level) =>
            new() { ["T1"] = new AxisReport { Tasks = { new() { Content = "Làm A", Level = level } } } };

        // Bộ khác khai báo mức và câu chữ riêng → máy chủ và Mẫu 09C theo bộ, không theo hằng số trong code.
        c.TaskLevels = new List<TaskLevel> { new() { Code = 1, Label = "Tốt" }, new() { Code = 5, Label = "Kém" } };
        c.TaskLineLabel = "Việc {n} -";
        c.TaskLevelPhrase = "(mức {level})";
        Assert.Empty(c.Validate());
        Assert.Null(RecordFormContent.ValidateAxisReports(c, One(5)));
        Assert.Contains("chỉ nhận 1, 5", RecordFormContent.ValidateAxisReports(c, One(3)));

        var source = Source();
        source.Criteria.TaskLevels = c.TaskLevels;
        source.Criteria.TaskLineLabel = c.TaskLineLabel;
        source.Criteria.TaskLevelPhrase = c.TaskLevelPhrase;
        source.Record.AxisReports = RecordFormContent.AxisReportsToJson(c, One(5));
        var section = Assert.Single(Mau09CData.From(source).Sections);
        var line = section.Axes.Single().Lines.Single();
        Assert.Equal(("Việc 1 - Làm A.", " (mức 5)"), (line.Text, line.Level));

        // Không khai báo mức → không nhận mức.
        c.TaskLevels = new List<TaskLevel>();
        c.TaskLevelPhrase = null;
        Assert.Contains("chưa khai báo mức", RecordFormContent.ValidateAxisReports(c, One(1)));

        // Cấu hình sai bị từ chối.
        var bad = CriteriaSetDefaults.Build();
        bad.TaskLevels[1].Code = 1;
        bad.TaskLevelPhrase = "không có tham số";
        var errors = bad.Validate();
        Assert.Contains(errors, e => e.Contains("bị trùng"));
        Assert.Contains(errors, e => e.Contains("{level}"));
    }

    #endregion

    #region Template 09A/09C/9D

    public static IEnumerable<object[]> Forms()
    {
        var source = Source();
        yield return new object[] { Mau09AData.TemplateFileName, Mau09AData.From(source) };
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
        // Khối điều kiện chỉ cần một nhánh (if: hoặc ifnot:).
        static string Condition(string tag) => tag.StartsWith(DocxTemplateEngine.IfNotPrefix, StringComparison.OrdinalIgnoreCase)
            ? tag[DocxTemplateEngine.IfNotPrefix.Length..]
            : tag.StartsWith(DocxTemplateEngine.IfPrefix, StringComparison.OrdinalIgnoreCase) ? tag[DocxTemplateEngine.IfPrefix.Length..] : tag;
        var templateConditions = templateTags.Select(Condition).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Empty(dataTags.Except(templateTags, StringComparer.OrdinalIgnoreCase)
            .Where(t => Condition(t) == t || !templateConditions.Contains(Condition(t))).OrderBy(t => t));
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
        // Dòng địa danh – ngày: ngày nộp phiếu tự chấm (11/9/2026); bản chuẩn Mẫu 9D không có dòng này.
        if (templateFileName == Mau9DData.TemplateFileName)
            Assert.DoesNotContain("Hà Nội, ngày", text);
        else
            Assert.Contains("Hà Nội, ngày 11 tháng 09 năm 2026", text);
        // Chỗ điền đặt liền sau nhãn: không dính chữ ("Quý3", "NĂM2026").
        Assert.DoesNotMatch(@"(?i)(Quý|Năm)\d", text);
    }

    [Fact]
    public void NotYetSelfScored_KeepsDateDots()
    {
        var source = Source();
        source.Record.SelfScoredAt = null;
        Assert.Null(Mau09AData.From(source).DateLine);
        Assert.Null(Mau09CData.From(source).DateLine);
    }

    [Fact]
    public void Mau09A_FillsDateOfBirth_AndKeepsDefaultWhenMissing()
    {
        var source = Source();
        source.Record.Member!.DateOfBirth = new DateOnly(1973, 10, 21);
        var text = Render(Mau09AData.TemplateFileName, Mau09AData.From(source), out _);
        Assert.Contains("Ngày sinh: 21/10/1973", text);

        source.Record.Member.DateOfBirth = null;
        Assert.Null(Mau09AData.From(source).DateOfBirth);
        Assert.Contains("Ngày sinh: ……/……/…………", Render(Mau09AData.TemplateFileName, Mau09AData.From(source), out _));
    }

    [Fact]
    public void Mau09A_ShowsGroupNotes_AndOriginalItemWording()
    {
        var source = Source();
        source.Record.GeneralGroupNotes = RecordFormContent.GeneralGroupNotesToJson(source.Criteria,
            new Dictionary<string, string?> { ["1"] = "Nhận xét nhóm 1 (test)" });
        var text = Render(Mau09AData.TemplateFileName, Mau09AData.From(source), out _);

        Assert.Contains("Nhận xét nhóm 1 (test)", text);
        Assert.Contains("Tuyệt đối trung thành với Đảng, với Tổ quốc và Nhân dân", text);
        Assert.Contains("Căn cứ giảm điểm", text);
    }

    [Fact]
    public void GeneralGroupNotes_Validation_AndJson()
    {
        var c = CriteriaSetDefaults.Build();
        var json = RecordFormContent.GeneralGroupNotesToJson(c, new Dictionary<string, string?> { [" 1 "] = "  Nhận xét  ", ["2"] = " " });
        Assert.Equal(new[] { "1" }, RecordFormContent.ParseGeneralGroupNotes(json).Keys);
        Assert.Equal("Nhận xét", RecordFormContent.ParseGeneralGroupNotes(json)["1"]);
        Assert.Null(RecordFormContent.GeneralGroupNotesToJson(c, new Dictionary<string, string?> { ["1"] = "" }));
        Assert.Null(RecordFormContent.ValidateGeneralGroupNotes(c, new Dictionary<string, string?> { ["3"] = "x" }));
        Assert.Contains("\"9\"", RecordFormContent.ValidateGeneralGroupNotes(c, new Dictionary<string, string?> { ["9"] = "x" }));
        Assert.Contains("2000", RecordFormContent.ValidateGeneralGroupNotes(c, new Dictionary<string, string?> { ["1"] = new string('a', 2001) }));
    }

    [Fact]
    public void DefaultCriteria_ItemFormTexts_AreOriginalWording()
    {
        var c = CriteriaSetDefaults.Build();
        var items = c.AllItems.Select(x => x.Item).ToList();
        Assert.Equal(17, items.Count);
        Assert.All(items, i => Assert.False(string.IsNullOrWhiteSpace(i.FormText)));
        Assert.StartsWith("Tuyệt đối trung thành với Đảng", items.Single(i => i.Code == "1.1").FormText);
        Assert.Equal("Tự soi, tự sửa trên tinh thần trách nhiệm chính trị cao, không né tránh, không đổ lỗi.", items.Single(i => i.Code == "3.4").FormText);
        Assert.Empty(c.Validate());
        var bad = CriteriaSetDefaults.Build();
        bad.GeneralGroups[0].Items[0].FormText = new string('a', 4001);
        Assert.Contains(bad.Validate(), e => e.Contains("in trên phiếu"));
        // Lưu/đọc bộ tiêu chí giữ nguyên nội dung in.
        var parsed = CriteriaSetContent.Parse(CriteriaSetDefaults.Build().ToJson());
        Assert.StartsWith("Tuyệt đối trung thành", parsed.GeneralGroups[0].Items[0].FormText);
    }

    [Fact]
    public void Mau09C_ShowsSectionContent_ScoresAndPositions()
    {
        var text = Render(Mau09CData.TemplateFileName, Mau09CData.From(Source()), out _);

        Assert.Contains("BẢN TỰ ĐÁNH GIÁ, XẾP LOẠI CỦA CÁ NHÂN", text);
        Assert.Contains("I. Tự đánh giá kết quả thực hiện chức trách, nhiệm vụ được giao", text);
        Assert.Matches(@"Họ và tên:\s+NGUYỄN VĂN A\s*Ngày sinh:\s*21/10/1973", text);
        // Mẫu gốc dùng khoảng trắng không ngắt (U+00A0) sau dấu hai chấm.
        Assert.Matches(@"Chức vụ Đảng:\s+Bí thư Chi bộ", text);
        Assert.Matches(@"Chức vụ chính quyền:\s+Trưởng phòng", text);
        Assert.Matches(@"Chức vụ đoàn thể:\s+Chủ tịch Công đoàn", text);
        // Điểm số nguyên in gọn như bản đã nộp.
        Assert.Contains("29/30", text);
        Assert.Contains("64/70", text);
        Assert.Contains("93/100", text);
        Assert.Contains("Hà Nội, ngày", text);
        Assert.Contains("Quý III Năm 2026", text);
        Assert.Matches(@"Đơn vị công tác:\s+Phòng Kỹ thuật, Công ty A\.", text);

        // Mục I: nhận xét chung thay cho câu dẫn, rồi từng trục (tiêu đề in đậm, mỗi nhiệm vụ một đoạn) — trục không có nhiệm vụ không in.
        var axes = CriteriaSetDefaults.Build().Axes;
        var paragraphs = text.Split('\n').ToList();
        var expected = new[]
        {
            "I. Tự đánh giá kết quả thực hiện chức trách, nhiệm vụ được giao",
            "Tự đánh giá mục I (test)",
            $"1. {axes[0].Name} (trục 1)",
            "- Nhiệm vụ 1: Việc T1 thứ nhất.",
            "- Nhiệm vụ 2: Việc T1 thứ hai. Tự đánh giá mức 2.",
            "+ Ý con của việc thứ hai. Tự đánh giá mức 1.",
            $"3. {axes[2].Name} (trục 3)",
            "- Nhiệm vụ 1: Việc T3.",
            "II. Tự đề xuất xếp loại mức chất lượng"
        };
        var start = paragraphs.IndexOf(expected[0]);
        Assert.True(start >= 0);
        Assert.Equal(expected, paragraphs.Skip(start).Take(expected.Length));
        Assert.DoesNotContain("Trên cơ sở nhiệm vụ được giao", text);
        Assert.DoesNotContain("………", text);
        Assert.DoesNotContain(paragraphs, p => p.StartsWith($"2. {axes[1].Name}", StringComparison.Ordinal));
        // Ký tên: họ tên chữ thường dưới "(Ký, ghi rõ họ tên)".
        var sign = paragraphs.FindIndex(p => p.Contains("NHÂN TỰ ĐÁNH GIÁ", StringComparison.Ordinal));
        Assert.Equal("Nguyễn Văn A", paragraphs.Skip(sign + 1).First(p => p.Trim().Length > 0));
        // Lưu ý hướng dẫn viết (chữ đỏ của biểu mẫu gốc) không in — như bản tự đánh giá đã nộp; lưu ý cuối biểu mẫu vẫn giữ.
        Assert.DoesNotContain("Viết tóm tắt theo kết quả 6 trục", text);
        Assert.Contains("Nội dung trình bày trong phạm vi không quá 02 trang A4", text);
    }

    [Fact]
    public void Mau09C_KeepsDots_OnlyWhenNothingEntered()
    {
        // Chỉ có nhiệm vụ theo trục: câu dẫn rồi tới trục, không còn dòng chấm.
        var source = Source();
        source.Record.SelfAssessment = "{}";
        var section = Assert.Single(Mau09CData.From(source).Sections);
        Assert.False(section.ShowDots);
        var text = Render(Mau09CData.TemplateFileName, Mau09CData.From(source), out _);
        var paragraphs = text.Split('\n').ToList();
        var intro = paragraphs.FindIndex(p => p.StartsWith("Trên cơ sở nhiệm vụ được giao", StringComparison.Ordinal));
        Assert.StartsWith("1. ", paragraphs[intro + 1]);

        // Chưa nhập gì: giữ dòng chấm của biểu mẫu để viết tay.
        source.Record.AxisReports = "{}";
        section = Assert.Single(Mau09CData.From(source).Sections);
        Assert.True(section.ShowDots);
        Assert.Empty(section.Axes);
        text = Render(Mau09CData.TemplateFileName, Mau09CData.From(source), out _);
        paragraphs = text.Split('\n').ToList();
        intro = paragraphs.FindIndex(p => p.StartsWith("Trên cơ sở nhiệm vụ được giao", StringComparison.Ordinal));
        Assert.StartsWith("………", paragraphs[intro + 1]);

        // Chưa nhập ngày sinh, chưa tự chấm: giữ dòng chấm; điểm lẻ in theo làm tròn của bộ tiêu chí.
        source.Record.Member!.DateOfBirth = null;
        Assert.Null(Mau09CData.From(source).DateOfBirth);
        source.Record.GeneralCriteriaScore = 29.5;
        Assert.Equal("29,5", Mau09CData.From(source).GeneralScore);
    }

    [Fact]
    public void Mau09C_LevelPhraseItalic_TitleBlack()
    {
        Render(Mau09CData.TemplateFileName, Mau09CData.From(Source()), out var content);
        using var doc = WordprocessingDocument.Open(new MemoryStream(content), false);
        var body = doc.MainDocumentPart!.Document!.Body!;
        var line = body.Descendants<Paragraph>().First(p => p.InnerText.StartsWith("- Nhiệm vụ 2: Việc T1 thứ hai.", StringComparison.Ordinal));
        var runs = line.Descendants<Run>().Where(r => r.InnerText.Length > 0).ToList();
        Assert.Equal(" Tự đánh giá mức 2.", runs[^1].InnerText);
        Assert.NotNull(runs[^1].RunProperties?.Italic);
        Assert.Null(runs[0].RunProperties?.Italic);
        // Tiêu đề trục in đậm, không thụt đầu dòng.
        var axisTitle = body.Descendants<Paragraph>().First(p => p.InnerText.EndsWith("(trục 1)", StringComparison.Ordinal));
        Assert.NotNull(axisTitle.Descendants<Run>().First().RunProperties?.Bold);
        Assert.Null(axisTitle.ParagraphProperties?.Indentation);
        // Tiêu đề và dòng quý màu đen như bản đã nộp.
        foreach (var title in body.Descendants<Paragraph>().Where(p => p.InnerText.StartsWith("BẢN TỰ ĐÁNH GIÁ", StringComparison.Ordinal) || p.InnerText.StartsWith("Quý ", StringComparison.Ordinal)))
            Assert.Empty(title.Descendants<Color>());
    }

    [Fact]
    public void Mau9D_OneRowPerAxis_AsOriginalForm()
    {
        var text = Render(Mau9DData.TemplateFileName, Mau9DData.From(Source()), out var content);
        var axes = CriteriaSetDefaults.Build().Axes;

        // Phần đầu như bản chuẩn: "Mẫu số 09D", quý số La Mã, chức vụ 3 dòng (Đảng / chính quyền / đoàn thể), đơn vị kèm tên công ty.
        Assert.Contains("Mẫu số 09D", text);
        Assert.Contains("TRONG QUÝ III NĂM 2026", text);
        Assert.Contains("Chức vụ: Chức vụ Đảng: Bí thư Chi bộ", text);
        Assert.Contains("Chức vụ chính quyền: Trưởng phòng", text);
        Assert.Contains("Chức vụ đoàn thể: Chủ tịch Công đoàn", text);
        Assert.Contains("Đơn vị công tác: Phòng Kỹ thuật, Công ty A", text);
        Assert.DoesNotContain("*", text);

        using var doc = WordprocessingDocument.Open(new MemoryStream(content), false);
        var tables = doc.MainDocumentPart!.Document!.Body!.Elements<Table>().ToList();
        // Bảng tiêu đề (Đảng bộ, Chi bộ | ĐẢNG CỘNG SẢN VIỆT NAM) và bảng kết quả.
        Assert.Equal(2, tables.Count);
        var header = tables[0].Elements<TableRow>().Single().Elements<TableCell>().Select(c => c.Elements<Paragraph>().Select(p => p.InnerText).ToList()).ToList();
        Assert.Equal(new[] { "ĐẢNG BỘ THỬ NGHIỆM", "CHI BỘ KỸ THUẬT" }, header[0].Take(2));
        Assert.Equal("ĐẢNG CỘNG SẢN VIỆT NAM", header[1][0]);
        var rows = tables[1].Elements<TableRow>()
            .Select(r => r.Elements<TableCell>().Select(c => c.InnerText).ToList()).ToList();
        // 2 dòng tiêu đề + đúng 6 dòng trục như biểu mẫu gốc (không có dòng nhiệm vụ riêng).
        Assert.Equal(2 + 6, rows.Count);
        Assert.Equal(new[] { "Trục 1", axes[0].Name, "30/9/2026", "Đã xong", "Báo cáo", "Đúng hạn", "" }, rows[2]);
        Assert.Equal(new[] { "Trục 2", axes[1].Name, "", "", "", "", "" }, rows[3]);
        Assert.Equal("Trục 6", rows[7][0]);
    }

    [Fact]
    public void CellLine_DropsLeadingChiBoFromCatalogName_AsSubmittedForm()
    {
        // Không lặp chữ "Chi bộ" của nhãn; tiêu đề trái giữ tên đầy đủ.
        var source = Source();
        var d = Render(Mau9DData.TemplateFileName, Mau9DData.From(source), out _);
        Assert.Contains("Chi bộ, Đảng bộ đang sinh hoạt: Kỹ thuật", d);
        Assert.Contains("CHI BỘ KỸ THUẬT", d);

        // Tên không bắt đầu bằng "Chi bộ" (Đảng bộ bộ phận…) giữ nguyên; chỉ có chữ "Chi bộ" cũng giữ nguyên.
        source.Record.PartyCell = new PartyCell { Name = "Đảng bộ bộ phận Khối Kỹ thuật" };
        Assert.Equal("Đảng bộ bộ phận Khối Kỹ thuật", Mau9DData.From(source).CellName);
        source.Record.PartyCell = new PartyCell { Name = "Chi bộ" };
        Assert.Equal("Chi bộ", Mau9DData.From(source).CellName);
    }

    [Fact]
    public void Mau9D_NoPositionsInCatalog_UsesProfileTitleForAdministrativeLine()
    {
        var source = Source() with { Positions = MemberPositionNames.Empty };
        var data = Mau9DData.From(source);
        Assert.Equal((null, "Trưởng phòng", null), (data.PartyPosition, data.AdministrativePosition, data.MassPosition));

        // Chức vụ trống giữ dòng chấm của template.
        var text = Render(Mau9DData.TemplateFileName, data, out _);
        Assert.Contains("Chức vụ: Chức vụ Đảng: …", text);
        Assert.Contains("Chức vụ chính quyền: Trưởng phòng", text);
        Assert.Contains("Chức vụ đoàn thể: …", text);
    }

    [Fact]
    public void CompanyLeader_WorkUnitLine_ShowsOnlyCompanyName()
    {
        // Chủ tịch, Giám đốc (bản đã nộp quý III/2026): "Đơn vị công tác: Công ty TNHH Kỹ thuật Quản lý bay" — hồ sơ thuộc đơn vị gốc.
        var source = Source();
        source.Record.Department = new AdministrativeDepartment { Name = "Công ty A" };

        Assert.Matches(@"Đơn vị công tác:\s+Công ty A\.\n", Render(Mau09CData.TemplateFileName, Mau09CData.From(source), out _));
        var text = Render(Mau9DData.TemplateFileName, Mau9DData.From(source), out _);
        Assert.Contains("Đơn vị công tác: Công ty A\n", text);
        Assert.DoesNotContain("Công ty A, Công ty A", text);

        // Hồ sơ chưa gắn đơn vị: giữ dòng chấm trước tên công ty để viết tay.
        source.Record.Department = null;
        Assert.True(Mau9DData.From(source).HasDepartment);
    }

    [Fact]
    public void FormLayout_Resolve_OverrideThenPositionsBySortOrderThenDefault()
    {
        var secretary = new FormLayout { Name = "Bí thư Đảng ủy", SortOrder = 1 };
        var leader = new FormLayout { Name = "Lãnh đạo Công ty", SortOrder = 2 };
        var staff = new FormLayout { Name = "Cán bộ phòng ban", SortOrder = 3, IsDefault = true };

        // Chủ tịch kiêm Bí thư Đảng ủy: hai chức vụ gắn hai kiểu → kiểu có thứ tự nhỏ hơn.
        Assert.Same(secretary, FormLayout.Resolve(null, new[] { leader, secretary }, staff));
        // Chức vụ không gắn kiểu → kiểu mặc định; đặt riêng thắng tất cả.
        Assert.Same(staff, FormLayout.Resolve(null, new FormLayout?[] { null }, staff));
        Assert.Same(leader, FormLayout.Resolve(leader, new[] { secretary }, staff));
        Assert.Null(FormLayout.Resolve(null, Array.Empty<FormLayout>(), null));
    }

    [Fact]
    public void Mau9D_LeaderLayout_DateLineAndOneLinePositions()
    {
        // Như Mẫu 9D của Giám đốc: dòng "Hà Nội, ngày …" dưới quốc hiệu, chức vụ một dòng.
        var layout = new FormLayout { Name = "Lãnh đạo Công ty", SingleLinePositions = true, ShowDateLineOn9D = true };
        var text = Render(Mau9DData.TemplateFileName, Mau9DData.From(Source() with { Layout = layout }), out _);

        Assert.Contains("ĐẢNG CỘNG SẢN VIỆT NAM\n\nHà Nội, ngày 11 tháng 09 năm 2026", text);
        Assert.Contains("Chức vụ (Đảng, Đoàn thể, Chính quyền): Bí thư Chi bộ, Chủ tịch Công đoàn, Trưởng phòng", text);
        Assert.DoesNotContain("Chức vụ chính quyền:", text);
    }

    [Fact]
    public void Mau09C_EmptyPositionText_FromLayout()
    {
        // Bản 9C của lãnh đạo ghi "Chức vụ đoàn thể: không"; chưa có kiểu thì giữ dòng chấm.
        var positions = MemberPositionNames.From(new[] { (PositionSide.Party, "Bí thư Đảng ủy"), (PositionSide.Administrative, "Chủ tịch Công ty") });
        var layout = new FormLayout { Name = "Lãnh đạo Công ty", EmptyPositionText = "không" };

        var data = Mau09CData.From(Source() with { Positions = positions, Layout = layout });
        Assert.Equal(("Bí thư Đảng ủy", "Chủ tịch Công ty", "không"), (data.PartyPosition, data.AdministrativePosition, data.MassPosition));
        Assert.Null(Mau09CData.From(Source() with { Positions = positions }).MassPosition);
    }

    [Fact]
    public void Mau09A_ShowsTasksExceedAndGradeCheckbox()
    {
        var source = Source();
        var text = Render(Mau09AData.TemplateFileName, Mau09AData.From(source), out _);

        Assert.Contains("Quý III Năm 2026", text);
        Assert.Contains("1 / 2 sản phẩm (đạt tỷ lệ: 50%)", text);
        Assert.Contains("☒ Hoàn thành tốt", text);
        Assert.Contains("☐ Hoàn thành xuất sắc", text);
    }


    [Fact]
    public void RealRecord_Q3_2026_ReproducesStandard09CAnd9D()
    {
        // Số liệu Mẫu 9C/9D thật quý III/2026: chung 30/30, nhiệm vụ 53, tổng 83.
        var criteria = CriteriaSetDefaults.Build();
        var general = criteria.AllItems.ToDictionary(x => x.Item.Code, x => new GeneralItemScore { Score = x.Item.MaxScore });
        Assert.Null(EvaluationScoring.ValidateGeneralScores(criteria, general));
        var generalTotal = EvaluationScoring.GeneralCriteriaScore(criteria, general);
        const double tasksTotal = 53;
        Assert.Equal(30, generalTotal);
        Assert.Equal(83, EvaluationScoring.TotalScore(generalTotal, tasksTotal, criteria.Parameters.Rounding.Total));

        var source = Source();
        var record = source.Record;
        record.Member!.FullName = "Nguyễn Văn B";
        record.GeneralScores = EvaluationScoring.GeneralScoresToJson(criteria, general);
        record.GeneralCriteriaScore = generalTotal;
        record.TasksScore = tasksTotal;
        record.TotalSelfScore = 83;
        record.SelfProposedGrade = EvaluationGrade.HoanThanh;
        // Một phần nội dung Mẫu 9C/9D thật: nhận xét chung, nhiệm vụ kèm mức, nhiệm vụ có ý con "+", tóm tắt trục trên Mẫu 9D.
        record.SelfAssessment = RecordFormContent.SelfAssessmentToJson(criteria, new Dictionary<string, string?>
        {
            ["I"] = "Bản thân luôn bám sát nghị quyết, kế hoạch công tác của Đảng ủy và của Công ty, hoàn thành các nhiệm vụ được giao. Cụ thể như sau:"
        });
        record.AxisReports = RecordFormContent.AxisReportsToJson(criteria, new Dictionary<string, AxisReport?>
        {
            ["T1"] = new()
            {
                Tasks =
                {
                    new() { Content = "Nhóm nhiệm vụ KHCN đang trong giai đoạn lập Hồ sơ thiết kế. Tuy vậy có 02 đề tài đang chậm tiến độ cần phải làm thủ tục điều chỉnh tiến độ", Level = 2 },
                    new() { Content = "Lãnh đạo thực hiện SXKD. Đảm bảo các hệ thống CNTT của Công ty và khách hàng hoạt động ổn định, an toàn và liên tục", Level = 1 }
                },
                Status = "- Nhóm nhiệm vụ KHCN: 02 đề tài phải điều chỉnh tiến độ\n- Lãnh đạo thực hiện SXKD: Các hệ thống CNTT được giao hoạt động ổn định",
                Product = "Cơ bản hoàn thành",
                Progress = "Chậm tiến độ 02 đề tài KHCN"
            },
            ["T3"] = new()
            {
                Tasks =
                {
                    new()
                    {
                        Content = "Thực hiện Kế hoạch tại quyết định 824/QĐ-KTQLB về phát triển KHCN, ĐMST và CĐS tại Công ty và Phòng",
                        Items =
                        {
                            new() { Content = "Phối hợp với Trung tâm BĐKT trong quá trình sửa, bổ sung các biểu mẫu trong hệ thống SIRMS", Level = 1 },
                            new() { Content = "Đối với các NV KHCN: công việc phải đề xuất KH điều chỉnh tiến độ", Level = 2 }
                        }
                    }
                },
                Product = "Cơ bản hoàn thành",
                Progress = "Chậm tiến độ 02 đề tài KHCN"
            }
        });

        var text = Render(Mau09CData.TemplateFileName, Mau09CData.From(source), out _);
        Assert.Contains("NGUYỄN VĂN B", text, StringComparison.Ordinal);
        var paragraphs = text.Split('\n').ToList();
        var start = paragraphs.FindIndex(p => p.StartsWith("Bản thân luôn bám sát", StringComparison.Ordinal));
        Assert.Equal(new[]
        {
            $"1. {criteria.Axes[0].Name} (trục 1)",
            "- Nhiệm vụ 1: Nhóm nhiệm vụ KHCN đang trong giai đoạn lập Hồ sơ thiết kế. Tuy vậy có 02 đề tài đang chậm tiến độ cần phải làm thủ tục điều chỉnh tiến độ. Tự đánh giá mức 2.",
            "- Nhiệm vụ 2: Lãnh đạo thực hiện SXKD. Đảm bảo các hệ thống CNTT của Công ty và khách hàng hoạt động ổn định, an toàn và liên tục. Tự đánh giá mức 1.",
            $"3. {criteria.Axes[2].Name} (trục 3)",
            "- Nhiệm vụ 1: Thực hiện Kế hoạch tại quyết định 824/QĐ-KTQLB về phát triển KHCN, ĐMST và CĐS tại Công ty và Phòng.",
            "+ Phối hợp với Trung tâm BĐKT trong quá trình sửa, bổ sung các biểu mẫu trong hệ thống SIRMS. Tự đánh giá mức 1.",
            "+ Đối với các NV KHCN: công việc phải đề xuất KH điều chỉnh tiến độ. Tự đánh giá mức 2."
        }, paragraphs.Skip(start + 1).Take(7));
        Assert.Contains("30/30", text);
        Assert.Contains("53/70", text);
        Assert.Contains("83/100", text);

        // Mẫu 9D: dòng Trục 1 như bản thật (tình hình nhiều dòng "- …" trong một ô).
        using var doc = WordprocessingDocument.Open(new MemoryStream(Render(Mau9DData.TemplateFileName, Mau9DData.From(source))), false);
        var axis1 = doc.MainDocumentPart!.Document!.Body!.Elements<Table>().Last().Elements<TableRow>().ElementAt(2).Elements<TableCell>().ToList();
        Assert.Equal(2, axis1[3].Descendants<Break>().Count() + 1);
        Assert.StartsWith("- Nhóm nhiệm vụ KHCN", axis1[3].InnerText);
        Assert.Equal(("Cơ bản hoàn thành", "Chậm tiến độ 02 đề tài KHCN"), (axis1[4].InnerText, axis1[5].InnerText));
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

    private static byte[] Render(string templateFileName, object formData)
    {
        Render(templateFileName, formData, out var content);
        return content;
    }

    private static string RenderData(string templateFileName, TemplateData data, OrganizationTemplateFields org)
    {
        var template = new FileWordTemplateStore().Load(templateFileName);
        var result = DocxTemplateEngine.Render(template, data.WithShared(TemplateDataBinder.Bind(org)));
        Assert.Empty(result.MissingTags);
        Assert.Empty(DocumentTemplateTests.Validate(result.Content));
        using var doc = WordprocessingDocument.Open(new MemoryStream(result.Content), false);
        return string.Join("\n", doc.MainDocumentPart!.Document!.Body!.Descendants<Paragraph>().Select(p => p.InnerText));
    }

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
        var criteria = CriteriaSetDefaults.Build();
        var period = new EvaluationPeriod { Year = 2026, Quarter = EvaluationQuarter.Quy3, Name = "Quý III/2026" };
        var cell = new PartyCell { Name = "Chi bộ Kỹ thuật" };
        var dept = new AdministrativeDepartment { Name = "Phòng Kỹ thuật", ParentId = Guid.NewGuid() };
        var member = new PartyMemberProfile
        {
            FullName = "Nguyễn Văn A", DateOfBirth = new DateOnly(1973, 10, 21), PositionTitle = "Trưởng phòng", PartyCell = cell, Department = dept
        };
        var general = criteria.AllItems.ToDictionary(
            x => x.Item.Code,
            x => x.Item.Code == "2.1" ? new GeneralItemScore { Score = 0, Reason = "Căn cứ giảm điểm" } : new GeneralItemScore { Score = x.Item.MaxScore });
        var record = new EvaluationRecord
        {
            Period = period, Member = member, PartyCell = cell, Department = dept,
            GeneralScores = EvaluationScoring.GeneralScoresToJson(criteria, general),
            GeneralCriteriaScore = 29, TasksScore = 64, TotalSelfScore = 93,
            SelfProposedGrade = EvaluationGrade.HoanThanhTot,
            SelfScoredAt = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc),
            SelfAssessment = RecordFormContent.SelfAssessmentToJson(criteria, new Dictionary<string, string?> { ["I"] = "Tự đánh giá mục I (test)" }),
            AxisReports = RecordFormContent.AxisReportsToJson(criteria, new Dictionary<string, AxisReport?>
            {
                ["T1"] = new()
                {
                    Tasks =
                    {
                        new() { Content = "Việc T1 thứ nhất" },
                        new() { Content = "Việc T1 thứ hai", Level = 2, Items = { new() { Content = "Ý con của việc thứ hai", Level = 1 } } }
                    },
                    Deadline = "30/9/2026", Status = "Đã xong", Product = "Báo cáo", Progress = "Đúng hạn"
                },
                ["T3"] = new() { Tasks = { new() { Content = "Việc T3" } } }
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
