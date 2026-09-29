using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Infrastructure.Documents;
using CongTacDang.Infrastructure.Documents.Forms;
using CongTacDang.Infrastructure.Services;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Kiểm thử bộ điền template Word theo Content Control và 5 template biểu mẫu (T-37, T-38, T-42).</summary>
public class DocumentTemplateTests
{
    #region Bộ điền template

    [Fact]
    public void Field_ReplacesContent_AndKeepsFormattingOfFirstRun()
    {
        var template = BuildDocx(new Paragraph(
            new Run(new Text("Họ và tên: ") { Space = SpaceProcessingModeValues.Preserve }),
            SdtRunOf("FULL_NAME", BoldRun("«FULL_NAME»"))));

        var result = DocxTemplateEngine.Render(template, new TemplateData().Field("FULL_NAME", "Nguyễn Văn A"));

        Assert.Empty(result.MissingTags);
        using var doc = Open(result.Content);
        Assert.Equal("Họ và tên: Nguyễn Văn A", BodyText(doc));
        Assert.Empty(doc.MainDocumentPart!.Document!.Descendants<SdtElement>());
        var filledRun = doc.MainDocumentPart.Document.Descendants<Run>().Single(r => r.InnerText == "Nguyễn Văn A");
        Assert.NotNull(filledRun.RunProperties?.Bold);
    }

    [Fact]
    public void Field_SplitAcrossManyRunsByWord_IsStillReplacedWhole()
    {
        // Word thường tách chữ trong control thành nhiều run (sửa chính tả, đổi định dạng giữa chừng...).
        var template = BuildDocx(new Paragraph(
            SdtRunOf("POSITION",
                BoldRun("{{PO"),
                new Run(new Text("SIT")),
                new ProofError { Type = ProofingErrorValues.SpellStart },
                new Run(new RunProperties(new Italic()), new Text("ION}}")),
                new ProofError { Type = ProofingErrorValues.SpellEnd })));

        var result = DocxTemplateEngine.Render(template, new TemplateData().Field("POSITION", "Trưởng phòng"));

        using var doc = Open(result.Content);
        Assert.Equal("Trưởng phòng", BodyText(doc));
        Assert.DoesNotContain("{{", BodyText(doc));
        Assert.Single(doc.MainDocumentPart!.Document!.Descendants<Run>(), r => r.Descendants<Text>().Any());
    }

    [Fact]
    public void Field_Null_KeepsTemplateDefaultText()
    {
        var template = BuildDocx(new Paragraph(SdtRunOf("SUPERVISOR_NAME", new Run(new Text("................")))));

        var result = DocxTemplateEngine.Render(template, new TemplateData().Field("SUPERVISOR_NAME", null));

        Assert.Empty(result.MissingTags);
        using var doc = Open(result.Content);
        Assert.Equal("................", BodyText(doc));
    }

    [Fact]
    public void Field_Multiline_BecomesLineBreaks()
    {
        var template = BuildDocx(new Paragraph(SdtRunOf("COMMENT", new Run(new Text("x")))));

        var result = DocxTemplateEngine.Render(template, new TemplateData().Field("COMMENT", "Dòng 1\nDòng 2"));

        using var doc = Open(result.Content);
        var run = doc.MainDocumentPart!.Document!.Descendants<Run>().Single();
        Assert.Single(run.Elements<Break>());
        Assert.Equal(new[] { "Dòng 1", "Dòng 2" }, run.Elements<Text>().Select(t => t.Text));
    }

    [Fact]
    public void MissingData_IsReported_AndDefaultKept()
    {
        var template = BuildDocx(new Paragraph(SdtRunOf("UNKNOWN", new Run(new Text("...")))));

        var result = DocxTemplateEngine.Render(template, new TemplateData());

        Assert.Equal(new[] { "UNKNOWN" }, result.MissingTags);
        using var doc = Open(result.Content);
        Assert.Equal("...", BodyText(doc));
    }

    [Fact]
    public void RepeatRow_ClonesRowPerItem_AndResolvesParentFields()
    {
        var header = new TableRow(CellOf("STT"), CellOf("Tên"), CellOf("Kỳ"));
        var templateRow = new TableRow(
            CellOf(SdtRunOf("T_STT", new Run(new Text("«T_STT»")))),
            CellOf(SdtRunOf("T_NAME", new Run(new Text("«T_NAME»")))),
            CellOf(SdtRunOf("YEAR", new Run(new Text("«YEAR»")))));
        var total = new TableRow(CellOf("CỘNG"), CellOf(""), CellOf(""));
        var table = new Table(new TableProperties(), new TableGrid(), header, SdtRowOf("repeat:TASKS", templateRow), total);
        var template = BuildDocx(table);

        var data = new TemplateData()
            .Field("YEAR", "2026")
            .Collection("TASKS", new[]
            {
                new TemplateData().Field("T_STT", "1").Field("T_NAME", "Việc A"),
                new TemplateData().Field("T_STT", "2").Field("T_NAME", "Việc B"),
                new TemplateData().Field("T_STT", "3").Field("T_NAME", "Việc C")
            });

        var result = DocxTemplateEngine.Render(template, data);

        Assert.Empty(result.MissingTags);
        using var doc = Open(result.Content);
        var rows = doc.MainDocumentPart!.Document!.Descendants<TableRow>().Select(r => r.InnerText).ToList();
        Assert.Equal(new[] { "STTTênKỳ", "1Việc A2026", "2Việc B2026", "3Việc C2026", "CỘNG" }, rows);
        Assert.Empty(doc.MainDocumentPart.Document.Descendants<SdtElement>());
        Assert.Empty(Validate(result.Content));
    }

    [Fact]
    public void RepeatRow_EmptyList_RemovesTemplateRow()
    {
        var table = new Table(new TableProperties(), new TableGrid(),
            new TableRow(CellOf("Tiêu đề")),
            SdtRowOf("repeat:ROWS", new TableRow(CellOf(SdtRunOf("NAME", new Run(new Text("x")))))));
        var template = BuildDocx(table);

        var result = DocxTemplateEngine.Render(template, new TemplateData().Collection("ROWS", Array.Empty<TemplateData>()));

        using var doc = Open(result.Content);
        Assert.Single(doc.MainDocumentPart!.Document!.Descendants<TableRow>());
    }

    [Theory]
    [InlineData(true, "Đạt vượt chuẩn")]
    [InlineData(false, "-")]
    public void ConditionalBlocks_ShowOnlyMatchingBranch(bool value, string expected)
    {
        var template = BuildDocx(new Paragraph(
            SdtRunOf("if:IS_EXCEED", new Run(new Text("Đạt vượt chuẩn"))),
            SdtRunOf("ifnot:IS_EXCEED", new Run(new Text("-")))));

        var result = DocxTemplateEngine.Render(template, new TemplateData().Condition("IS_EXCEED", value));

        using var doc = Open(result.Content);
        Assert.Equal(expected, BodyText(doc));
    }

    [Fact]
    public void ConditionalBlock_AtBlockLevel_RemovesParagraphs()
    {
        var template = BuildDocx(
            new Paragraph(new Run(new Text("Đầu"))),
            SdtBlockOf("if:HAS_NOTE", new Paragraph(new Run(new Text("Ghi chú")))),
            new Paragraph(new Run(new Text("Cuối"))));

        var hidden = DocxTemplateEngine.Render(template, new TemplateData().Condition("HAS_NOTE", false));
        var shown = DocxTemplateEngine.Render(template, new TemplateData().Condition("HAS_NOTE", true));

        using (var doc = Open(hidden.Content))
            Assert.Equal(new[] { "Đầu", "Cuối" }, Paragraphs(doc));
        using (var doc = Open(shown.Content))
            Assert.Equal(new[] { "Đầu", "Ghi chú", "Cuối" }, Paragraphs(doc));
    }

    [Fact]
    public void UntaggedContentControl_IsLeftUntouched()
    {
        var untagged = new SdtRun(new SdtProperties(new SdtId { Val = 1 }), new SdtContentRun(new Run(new Text("2"))));
        var template = BuildDocx(new Paragraph(untagged));

        var result = DocxTemplateEngine.Render(template, new TemplateData());

        using var doc = Open(result.Content);
        Assert.Single(doc.MainDocumentPart!.Document!.Descendants<SdtElement>());
    }

    [Fact]
    public void Binder_MapsAttributedProperties()
    {
        var data = TemplateDataBinder.Bind(new Mau02Data
        {
            FullName = "A",
            Tasks = new List<Mau02TaskRow> { new() { Name = "Việc", IsExceedStandard = true } }
        });

        Assert.Equal("A", data.Fields["FULL_NAME"]);
        Assert.Null(data.Fields["DEPARTMENT"]);
        var row = Assert.Single(data.Collections["TASKS"]);
        Assert.Equal("Việc", row.Fields["T_NAME"]);
        Assert.True(row.Conditions["T_IS_EXCEED"]);
    }

    #endregion

    #region 5 biểu mẫu hiện có

    public static IEnumerable<object[]> AllForms()
    {
        var (period, records) = SampleData();
        yield return new object[] { Mau01Data.TemplateFileName, Mau01Data.From(records[0]) };
        yield return new object[] { Mau02Data.TemplateFileName, Mau02Data.From(records[0], new Dictionary<Guid, string>()) };
        yield return new object[] { Mau10Data.TemplateFileName, Mau10Data.From(records[0]) };
        yield return new object[] { Mau11Data.TemplateFileName, Mau11Data.From(period, records, "Chi bộ Kỹ thuật") };
    }

    [Theory]
    [MemberData(nameof(AllForms))]
    public void Template_TagsMatchFormDataClass(string templateFileName, object formData)
    {
        var template = new FileWordTemplateStore().Load(templateFileName);
        var templateTags = DocxTemplateEngine.GetTags(template).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dataTags = TagsOf(TemplateDataBinder.Bind(formData));
        // Task 17: tag thông tin đơn vị (ORG_*) dùng chung, tùy chọn — không khai báo trên lớp dữ liệu của mẫu.
        var commonTags = OrganizationTemplateFields.Tags.Select(t => t.Tag);

        // Không thiếu (template có Tag mà lớp dữ liệu không khai báo) và không thừa (khai báo nhưng template không dùng).
        Assert.Empty(templateTags.Except(dataTags.Concat(commonTags), StringComparer.OrdinalIgnoreCase).OrderBy(t => t));
        Assert.Empty(dataTags.Except(templateTags, StringComparer.OrdinalIgnoreCase).OrderBy(t => t));
    }

    [Theory]
    [MemberData(nameof(AllForms))]
    public void Template_RendersValidDocument_WithoutPlaceholders(string templateFileName, object formData)
    {
        var template = new FileWordTemplateStore().Load(templateFileName);
        var org = OrganizationTemplateFields.From("ĐẢNG BỘ A", "ĐẢNG BỘ CẤP TRÊN", "Công ty A", "Tổng công ty B", "A", "Hà Nội");

        var result = DocxTemplateEngine.Render(template, TemplateDataBinder.Bind(formData).WithShared(TemplateDataBinder.Bind(org)));

        Assert.Empty(result.MissingTags);
        Assert.Empty(Validate(result.Content));
        using var doc = Open(result.Content);
        var body = doc.MainDocumentPart!.Document!.Body!;
        Assert.DoesNotContain("{{", body.InnerText);
        Assert.DoesNotContain("«", body.InnerText);
        Assert.DoesNotContain(body.Descendants<SdtElement>(), sdt => sdt.SdtProperties?.GetFirstChild<Tag>() != null);
    }

    [Fact]
    public void Mau01_RendersOneRowPerTask()
    {
        var (_, records) = SampleData();
        var template = new FileWordTemplateStore().Load(Mau01Data.TemplateFileName);

        var result = DocxTemplateEngine.Render(template, TemplateDataBinder.Bind(Mau01Data.From(records[0])));

        using var doc = Open(result.Content);
        var text = doc.MainDocumentPart!.Document!.Body!.InnerText;
        Assert.Contains("Nguyễn Văn A", text);
        Assert.Contains("Quý 3", text);
        foreach (var task in records[0].Tasks)
            Assert.Contains(task.TaskName, text);
    }

    [Fact]
    public void Mau02_UsesStoredTaskScore_NotRecomputedWeights()
    {
        // Hồ sơ Khung 1 (tỷ trọng khác Khung 2): điểm hiển thị phải đúng SelfScore đã lưu.
        var (_, records) = SampleData();
        var record = records[0];
        record.WeightFrameCode = "K1";
        var task = record.Tasks.First();
        task.Weight = 20;
        task.CriteriaA_Ratio = 0.5;
        task.CriteriaB_Ratio = 1;
        task.CriteriaC_Ratio = 1;
        task.CriteriaD_Ratio = 1;
        task.SelfScore = 17.5;

        var data = Mau02Data.From(record);
        var row = data.Tasks.First();

        Assert.Equal("17,50", row.Score);
        Assert.Equal("87,5%", row.ResultPercent);
        Assert.Equal("50%", row.CriteriaA);
    }

    [Fact]
    public void Mau10_UsesStoredScores()
    {
        var (_, records) = SampleData();
        var data = Mau10Data.From(records[0]);

        Assert.Equal("28,5", data.GeneralSelfScore);
        Assert.Equal("65,4", data.TasksSelfScore);
        Assert.Equal("93,9", data.TotalSelfScore);
        Assert.Equal("92,0", data.TotalAppraisalScore);
        Assert.Equal("-1,9", data.TotalDiff);
        Assert.Null(data.GeneralAppraisalScore);
    }

    [Fact]
    public void TemplateStore_MissingTemplate_ThrowsClearError()
    {
        var store = new FileWordTemplateStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var ex = Assert.Throws<FileNotFoundException>(() => store.Load("Mau_99.docx"));
        Assert.Contains("Mau_99.docx", ex.Message);
        Assert.Throws<ArgumentException>(() => store.Load("../secret.docx"));
    }

    #endregion

    #region Hỗ trợ

    internal static (EvaluationPeriod Period, List<EvaluationRecord> Records) SampleData()
    {
        var period = new EvaluationPeriod { Year = 2026, Quarter = EvaluationQuarter.Quy3, Name = "Quý III/2026" };
        var cell = new PartyCell { Name = "Chi bộ Kỹ thuật" };
        var dept = new AdministrativeDepartment { Name = "Phòng Kỹ thuật" };

        EvaluationRecord Make(string name, double tasksScore, double appraisal)
        {
            var member = new PartyMemberProfile { FullName = name, PositionTitle = "Trưởng phòng", PartyCell = cell, Department = dept };
            var record = new EvaluationRecord
            {
                Period = period, Member = member, PartyCell = cell, Department = dept,
                GeneralCriteriaScore = 28.5, TasksScore = tasksScore, TotalSelfScore = 28.5 + tasksScore,
                SelfProposedGrade = EvaluationGrade.HoanThanhTot, CollectiveProposedGrade = EvaluationGrade.HoanThanhTot,
                AppraisalScore = appraisal, AppraisalComment = "Nhất trí", AppraisalProposedGrade = EvaluationGrade.HoanThanhTot
            };
            record.Tasks.Add(new EvaluationTask { TaskOrder = 1, TaskName = "Bảo dưỡng hệ thống radar", TargetOutput = "Radar hoạt động 99%", Weight = 30, Deadline = new DateTime(2026, 9, 30), CriteriaB_Ratio = 0.9, CriteriaD_Ratio = 0.8, SelfScore = 27.3, IsExceedStandard = true });
            record.Tasks.Add(new EvaluationTask { TaskOrder = 2, TaskName = "Lập kế hoạch kiểm tra định kỳ", Weight = 25, Deadline = new DateTime(2026, 8, 15), CriteriaC_Ratio = 0.9, SelfScore = 24.6 });
            record.Tasks.Add(new EvaluationTask { TaskOrder = 3, TaskName = "Đào tạo nhân viên mới", TargetOutput = "5 người", Weight = 15, Deadline = new DateTime(2026, 9, 1), CriteriaA_Ratio = 0.8, CriteriaB_Ratio = 0.9, SelfScore = 13.5 });
            return record;
        }

        return (period, new List<EvaluationRecord> { Make("Nguyễn Văn A", 65.4, 92.0), Make("Trần Thị B", 60.0, 88.5) });
    }

    private static HashSet<string> TagsOf(TemplateData data)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in data.Fields.Keys)
            tags.Add(field);
        foreach (var condition in data.Conditions.Keys)
        {
            tags.Add(DocxTemplateEngine.IfPrefix + condition);
            tags.Add(DocxTemplateEngine.IfNotPrefix + condition);
        }
        foreach (var (name, items) in data.Collections)
        {
            tags.Add(DocxTemplateEngine.RepeatPrefix + name);
            // Tag của dòng lặp khai báo trên lớp phần tử; bind một phần tử rỗng để lấy đủ tên.
            foreach (var item in items.Take(1))
                tags.UnionWith(TagsOf(item));
        }
        return tags;
    }

    internal static byte[] BuildDocx(params OpenXmlElement[] bodyElements)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body(bodyElements));
            main.Document.Save();
        }
        return ms.ToArray();
    }

    private static Run BoldRun(string text) =>
        new(new RunProperties(new Bold()), new Text(text) { Space = SpaceProcessingModeValues.Preserve });

    private static int _id = 1000;

    private static SdtProperties PropsOf(string tag) =>
        new(new SdtAlias { Val = tag }, new Tag { Val = tag }, new SdtId { Val = Interlocked.Increment(ref _id) });

    private static SdtRun SdtRunOf(string tag, params OpenXmlElement[] runs) =>
        new(PropsOf(tag), new SdtContentRun(runs));

    private static SdtBlock SdtBlockOf(string tag, params OpenXmlElement[] blocks) =>
        new(PropsOf(tag), new SdtContentBlock(blocks));

    private static SdtRow SdtRowOf(string tag, params TableRow[] rows) =>
        new(PropsOf(tag), new SdtContentRow(rows));

    private static TableCell CellOf(string text) => new(new Paragraph(new Run(new Text(text))));

    private static TableCell CellOf(OpenXmlElement inline) => new(new Paragraph(inline));

    private static WordprocessingDocument Open(byte[] content) => WordprocessingDocument.Open(new MemoryStream(content), false);

    private static string BodyText(WordprocessingDocument doc) => doc.MainDocumentPart!.Document!.Body!.InnerText;

    private static List<string> Paragraphs(WordprocessingDocument doc) =>
        doc.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().Select(p => p.InnerText).ToList();

    internal static List<string> Validate(byte[] content)
    {
        using var doc = Open(content);
        return new OpenXmlValidator(FileFormatVersions.Office2019)
            .Validate(doc)
            .Select(e => $"{e.Description} @ {e.Path?.XPath}")
            .ToList();
    }

    #endregion
}
