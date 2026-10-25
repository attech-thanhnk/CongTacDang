using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Evaluation;
using CongTacDang.Infrastructure.Documents;
using CongTacDang.Infrastructure.Documents.Forms;
using CongTacDang.Infrastructure.Services;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Mẫu 04 (điều chỉnh, chuyển tiếp, loại trừ) và Mẫu 05 (đột xuất): tag khớp lớp dữ liệu, dữ liệu điền đúng chỗ.</summary>
public class TaskChangeFormsTests
{
    private static readonly FileWordTemplateStore Bundled = new();

    private static (TaskChangeFormSource Source, EvaluationRecord Record) Source(TaskChangeType type, TaskChangeStatus status = TaskChangeStatus.Approved)
    {
        var (_, records) = DocumentTemplateTests.SampleData();
        var record = records[0];
        var tasks = record.Tasks.OrderBy(t => t.TaskOrder).ToList();
        var before = tasks.ToDictionary(t => t.Id, t => t.Weight);
        var request = new TaskChangeRequest
        {
            RecordId = record.Id,
            Record = record,
            Type = type,
            Status = status,
            Reason = "Thiết bị chờ nhà thầu bàn giao",
            RequestedByName = "Nguyễn Văn A",
            RequestedAt = new DateTime(2026, 8, 20, 3, 0, 0, DateTimeKind.Utc),
            DecidedByName = "Lê Văn Duyệt",
            WeightsBefore = TaskChangeRules.WeightsToJson(before)
        };
        switch (type)
        {
            case TaskChangeType.Adjust:
                request.TaskId = tasks[0].Id;
                request.TaskNameSnapshot = tasks[0].TaskName;
                request.Proposal = TaskChangeRules.ProposalToJson(new TaskChangeProposal { TargetOutput = "Radar hoạt động 98%", Deadline = new DateTime(2026, 10, 15) });
                request.Weights = TaskChangeRules.WeightsToJson(new Dictionary<Guid, double> { [tasks[0].Id] = 25, [tasks[1].Id] = 30, [tasks[2].Id] = 15 });
                break;
            case TaskChangeType.Exclude:
            case TaskChangeType.Carryover:
                request.TaskId = tasks[2].Id;
                request.TaskNameSnapshot = tasks[2].TaskName;
                request.Weights = TaskChangeRules.WeightsToJson(new Dictionary<Guid, double> { [tasks[0].Id] = 40, [tasks[1].Id] = 30 });
                break;
            default:
                request.TaskNameSnapshot = "Khắc phục sự cố mất điện";
                request.Proposal = TaskChangeRules.ProposalToJson(new TaskChangeProposal
                {
                    TaskName = "Khắc phục sự cố mất điện", TargetOutput = "Khôi phục nguồn trong 24 giờ", Weight = 10, Deadline = new DateTime(2026, 9, 5)
                });
                request.Weights = TaskChangeRules.WeightsToJson(new Dictionary<Guid, double> { [tasks[0].Id] = 25, [tasks[1].Id] = 20, [tasks[2].Id] = 15 });
                break;
        }
        return (new TaskChangeFormSource(record, request, "Trưởng phòng Kỹ thuật"), record);
    }

    public static IEnumerable<object[]> Forms()
    {
        foreach (var type in new[] { TaskChangeType.Adjust, TaskChangeType.Exclude, TaskChangeType.Carryover })
            yield return new object[] { Mau04Data.TemplateFileName, type };
        yield return new object[] { Mau05Data.TemplateFileName, TaskChangeType.Urgent };
    }

    private static object DataOf(string templateFileName, TaskChangeFormSource source) =>
        templateFileName == Mau05Data.TemplateFileName ? Mau05Data.From(source) : Mau04Data.From(source);

    private static byte[] Render(string templateFileName, TaskChangeFormSource source)
    {
        var org = OrganizationTemplateFields.From("ĐẢNG BỘ A", "ĐẢNG BỘ CẤP TRÊN", "Công ty A", "Tổng công ty B", "A", "Hà Nội");
        var data = TemplateDataBinder.Bind(DataOf(templateFileName, source)).WithShared(TemplateDataBinder.Bind(org));
        var result = DocxTemplateEngine.Render(Bundled.Load(templateFileName), data);
        Assert.Empty(result.MissingTags);
        return result.Content;
    }

    private static string BodyText(byte[] content)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(content), false);
        return doc.MainDocumentPart!.Document!.Body!.InnerText;
    }

    [Theory]
    [InlineData("MAU_04")]
    [InlineData("MAU_05")]
    public void Template_TagsMatchDataClass(string code)
    {
        var form = WordFormCatalog.Find(code)!;
        var templateTags = DocxTemplateEngine.GetTags(Bundled.Load(form.TemplateFileName)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dataTags = form.FormTags.Select(t => t.Tag).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var commonTags = OrganizationTemplateFields.Tags.Select(t => t.Tag);

        Assert.Empty(templateTags.Except(dataTags.Concat(commonTags), StringComparer.OrdinalIgnoreCase).OrderBy(t => t));
        Assert.Empty(dataTags.Except(templateTags, StringComparer.OrdinalIgnoreCase).OrderBy(t => t));
    }

    [Theory]
    [MemberData(nameof(Forms))]
    public void Template_RendersValidDocument_WithoutPlaceholders(string templateFileName, TaskChangeType type)
    {
        var content = Render(templateFileName, Source(type).Source);

        Assert.Empty(DocumentTemplateTests.Validate(content));
        using var doc = WordprocessingDocument.Open(new MemoryStream(content), false);
        var body = doc.MainDocumentPart!.Document!.Body!;
        Assert.DoesNotContain("{{", body.InnerText);
        Assert.DoesNotContain("«", body.InnerText);
        Assert.DoesNotContain(body.Descendants<SdtElement>(), sdt => sdt.SdtProperties?.GetFirstChild<Tag>() != null);
    }

    [Fact]
    public void Mau04_Adjust_FillsFieldsAndChecksBoxes()
    {
        var (source, _) = Source(TaskChangeType.Adjust);

        var text = BodyText(Render(Mau04Data.TemplateFileName, source));

        Assert.Contains("PHIẾU ĐỀ NGHỊ ĐIỀU CHỈNH, CHUYỂN TIẾP, LOẠI TRỪ", text);
        Assert.Contains("CÔNG TY A", text);
        Assert.Contains("Hà Nội, ngày 20 tháng 08 năm 2026", text);
        Assert.Contains("Quý III / Năm 2026", text);
        Assert.Contains("Nguyễn Văn A", text);
        Assert.Contains("Trưởng phòng Kỹ thuật", text);
        Assert.Contains("Bảo dưỡng hệ thống radar", text);
        Assert.Contains("☒ Điều chỉnh tiến độ/chỉ tiêu", text);
        Assert.Contains("☒ Hiệu chỉnh kỹ thuật trọng số", text);
        Assert.Contains("☐ Chuyển tiếp sang quý sau", text);
        Assert.Contains("☐ Loại trừ trách nhiệm", text);
        Assert.Contains("Mục tiêu: Radar hoạt động 98%", text);
        Assert.Contains("Thời hạn: 15/10/2026", text);
        Assert.Contains("Trọng số: 25 điểm", text);
        Assert.Contains("Bảo dưỡng hệ thống radar: 30 → 25 điểm", text);
        Assert.Contains("Lập kế hoạch kiểm tra định kỳ: 25 → 30 điểm", text);
        Assert.Contains("Tổng trọng số nhóm nhiệm vụ sau điều chỉnh: 70 điểm.", text);
        Assert.Contains("Thiết bị chờ nhà thầu bàn giao", text);
        Assert.Contains("Lê Văn Duyệt", text);
        // Điều chỉnh đã duyệt đã ghi đè nội dung việc → không in lại mục tiêu cũ như thể còn đúng.
        Assert.DoesNotContain("Mục tiêu: Radar hoạt động 99%", text);
    }

    [Fact]
    public void Mau04_PendingAdjust_ShowsCurrentContentAsBefore_AndNoApprover()
    {
        var (source, _) = Source(TaskChangeType.Adjust, TaskChangeStatus.Pending);

        var data = Mau04Data.From(source);

        Assert.Contains("Mục tiêu: Radar hoạt động 99%", data.ContentBefore);
        Assert.Contains("Trọng số: 30 điểm", data.ContentBefore);
        Assert.Null(data.ApproverSign);
        Assert.Equal("Nguyễn Văn A", data.RequesterSign);
    }

    [Theory]
    [InlineData(TaskChangeType.Exclude, "☒ Loại trừ trách nhiệm", "Loại trừ, trọng số 0")]
    [InlineData(TaskChangeType.Carryover, "☒ Chuyển tiếp sang quý sau", "Chuyển sang quý sau")]
    public void Mau04_ExcludeAndCarryover_CheckTheirBox(TaskChangeType type, string box, string after)
    {
        var (source, _) = Source(type);

        var text = BodyText(Render(Mau04Data.TemplateFileName, source));

        Assert.Contains(box, text);
        Assert.Contains(after, text);
        Assert.Contains("Đào tạo nhân viên mới", text);
        Assert.Contains("Đào tạo nhân viên mới: 15 → 0 điểm", text);
        Assert.Contains("Tổng trọng số nhóm nhiệm vụ sau điều chỉnh: 70 điểm.", text);
    }

    [Fact]
    public void Mau05_FillsUrgentTask_AndRebalanceBox()
    {
        var (source, _) = Source(TaskChangeType.Urgent);

        var text = BodyText(Render(Mau05Data.TemplateFileName, source));

        Assert.Contains("PHIẾU GHI NHẬN NHIỆM VỤ BỔ SUNG, KHẨN CẤP, ĐỘT XUẤT TRONG KỲ", text);
        Assert.Contains("Quý III / Năm 2026", text);
        Assert.Contains("Nguyễn Văn A", text);
        Assert.Contains("Thiết bị chờ nhà thầu bàn giao", text);
        Assert.Contains("Ngày 20 tháng 08 năm 2026", text);
        Assert.Contains("Khắc phục sự cố mất điện", text);
        Assert.Contains("Khôi phục nguồn trong 24 giờ", text);
        Assert.Contains("Ngày 05 tháng 09 năm 2026", text);
        Assert.Contains("☒ Điều chỉnh giảm trọng số các nhiệm vụ khác để gán trọng số mới: 10 điểm", text);
        Assert.Contains("☐ Thay thế nhiệm vụ số", text);
    }

    [Fact]
    public void Mau05_NoReducedWeight_LeavesRebalanceBoxUnchecked()
    {
        var (source, _) = Source(TaskChangeType.Urgent);
        source.Request.WeightsBefore = source.Request.Weights;

        var data = Mau05Data.From(source);

        Assert.Null(data.BoxRebalance);
        Assert.Null(data.NewWeight);
    }
}
