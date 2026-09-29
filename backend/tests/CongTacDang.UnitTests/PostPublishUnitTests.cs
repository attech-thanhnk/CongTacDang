using System.Text.Json;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
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

/// <summary>
/// Task 20 — sau công bố: mã quyền mới và luật guard, tham số bộ tiêu chí (công khai điểm, mức bắt buộc lập kế hoạch),
/// nội dung Mẫu 17, file mẫu Mẫu 17 dựng từ biểu mẫu gốc, nhắc việc (sắp tới hạn/quá hạn).
/// </summary>
public sealed class PostPublishUnitTests
{
    private static readonly Guid DeptA = Guid.NewGuid();
    private static readonly Guid CellA = Guid.NewGuid();

    private static EffectivePermissions Perms(Guid userId, params (string Code, ScopeType Type, Guid? Id)[] grants) =>
        new(userId, grants.Select(g => new PermissionGrant(g.Code, g.Type, g.Id, Guid.NewGuid(), "Test")));

    #region Quyền

    [Fact]
    public void NewPermissionCodes_AreDefined_ScopedAndInEvaluationModule()
    {
        foreach (var code in new[]
                 {
                     PermissionCodes.EvaluationResultsView, PermissionCodes.EvaluationAppealSubmit,
                     PermissionCodes.EvaluationAppealResolve, PermissionCodes.EvaluationImprovementManage
                 })
        {
            var definition = PermissionCodes.Find(code);
            Assert.NotNull(definition);
            Assert.True(definition!.AppliesScope, code);
            Assert.Equal("evaluation", definition.Module);
        }
    }

    [Fact]
    public void AppealSubmit_OnlyOnOwnRecord_ScopeIgnored()
    {
        var userId = Guid.NewGuid();
        var permissions = Perms(userId, (PermissionCodes.EvaluationAppealSubmit, ScopeType.Global, null));

        Assert.True(AuthorizationGuard.Evaluate(permissions, PermissionCodes.EvaluationAppealSubmit, new AccessTarget(userId, DeptA, CellA)));
        Assert.False(AuthorizationGuard.Evaluate(permissions, PermissionCodes.EvaluationAppealSubmit, new AccessTarget(Guid.NewGuid(), DeptA, CellA)));
        Assert.Equal(ScopeFilter.OwnerOnly(userId).OwnerId, AuthorizationGuard.BuildScope(permissions, PermissionCodes.EvaluationAppealSubmit).OwnerId);
        // Không có quyền → không gửi được cả trên hồ sơ của mình.
        Assert.False(AuthorizationGuard.Evaluate(Perms(userId), PermissionCodes.EvaluationAppealSubmit, new AccessTarget(userId, DeptA, CellA)));
    }

    [Fact]
    public void AppealResolve_AndImprovementManage_AreConflictOfInterest_OnOwnRecord()
    {
        var userId = Guid.NewGuid();
        foreach (var code in new[] { PermissionCodes.EvaluationAppealResolve, PermissionCodes.EvaluationImprovementManage })
        {
            Assert.Contains(code, AuthorizationGuard.ConflictOfInterestCodes);
            var permissions = Perms(userId, (code, ScopeType.Department, DeptA));
            Assert.False(AuthorizationGuard.Evaluate(permissions, code, new AccessTarget(userId, DeptA, CellA)), code);
            Assert.True(AuthorizationGuard.Evaluate(permissions, code, new AccessTarget(Guid.NewGuid(), DeptA, CellA)), code);
            Assert.False(AuthorizationGuard.Evaluate(permissions, code, new AccessTarget(Guid.NewGuid(), Guid.NewGuid(), CellA)), code);
        }
    }

    [Fact]
    public void ResultsView_ScopeFollowsAssignment()
    {
        var userId = Guid.NewGuid();
        var scoped = AuthorizationGuard.BuildScope(Perms(userId, (PermissionCodes.EvaluationResultsView, ScopeType.PartyCell, CellA)), PermissionCodes.EvaluationResultsView);
        Assert.False(scoped.IsGlobal);
        Assert.Null(scoped.OwnerId);
        var global = AuthorizationGuard.BuildScope(Perms(userId, (PermissionCodes.EvaluationResultsView, ScopeType.Global, null)), PermissionCodes.EvaluationResultsView);
        Assert.True(global.IsGlobal);
    }

    #endregion

    #region Tham số bộ tiêu chí

    [Fact]
    public void CriteriaParameters_Defaults_OnlyGradePublished_PlanRequiredForCAndD()
    {
        var content = CriteriaSetContent.Parse("{\"parameters\":{\"minTasks\":3}}");

        Assert.False(content.Parameters.PublishScores);
        Assert.Equal(new[] { EvaluationGrade.HoanThanh, EvaluationGrade.KhongHoanThanh }, content.Parameters.ImprovementPlanRequiredGrades);
    }

    [Fact]
    public void CriteriaParameters_RequiredGrades_SerializeAsCodes_AndRoundTrip()
    {
        var content = new CriteriaSetContent();
        content.Parameters.PublishScores = true;
        content.Parameters.ImprovementPlanRequiredGrades = new List<EvaluationGrade> { EvaluationGrade.KhongHoanThanh };

        var json = content.ToJson();
        Assert.Contains("\"improvementPlanRequiredGrades\":[\"KhongHoanThanh\"]", json);
        Assert.Contains("\"publishScores\":true", json);

        var parsed = CriteriaSetContent.Parse(json);
        Assert.True(parsed.Parameters.PublishScores);
        Assert.Equal(new[] { EvaluationGrade.KhongHoanThanh }, parsed.Parameters.ImprovementPlanRequiredGrades);

        // API (tùy chọn JSON mặc định của ASP.NET) dùng cùng converter trên thuộc tính.
        var viaWeb = JsonSerializer.Deserialize<CriteriaSetContent>(
            "{\"parameters\":{\"improvementPlanRequiredGrades\":[\"HoanThanh\",4]}}", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(new[] { EvaluationGrade.HoanThanh, EvaluationGrade.KhongHoanThanh }, viaWeb.Parameters.ImprovementPlanRequiredGrades);
        Assert.Throws<FormatException>(() => CriteriaSetContent.Parse("{\"parameters\":{\"improvementPlanRequiredGrades\":[\"Sai\"]}}"));
    }

    [Fact]
    public void CriteriaParameters_EmptyRequiredGrades_Allowed_UnrankedGradeRejected()
    {
        var content = CriteriaSetContent.Parse("{\"parameters\":{\"improvementPlanRequiredGrades\":[]}}");
        Assert.Empty(content.Parameters.ImprovementPlanRequiredGrades);

        content.Parameters.ImprovementPlanRequiredGrades = new List<EvaluationGrade> { EvaluationGrade.ChuaXepLoai };
        Assert.Contains(content.Validate(CriteriaSetContent.Form09B), e => e.Contains("kế hoạch 30-60-90"));
    }

    #endregion

    #region Nội dung Mẫu 17

    [Fact]
    public void PlanContent_NormalizesThreeMilestones_AndReportsMissingForApproval()
    {
        var content = ImprovementPlanContent.Parse("{\"supporterName\":\"  \",\"stages\":{\"M30\":{\"limitation\":\" Chậm \",\"result\":\"Sai\"},\"X\":{}}}");

        Assert.Equal(new[] { "M30", "M60", "M90" }, content.Stages.Keys.ToArray());
        Assert.Equal("Chậm", content.Stage("M30").Limitation);
        Assert.Null(content.Stage("M30").Result);
        Assert.Null(content.SupporterName);

        var missing = content.MissingForApproval();
        Assert.Contains(missing, m => m.Contains("người trực tiếp hỗ trợ"));
        Assert.Contains(missing, m => m.StartsWith("Mốc 30 ngày") && m.Contains("mục tiêu") && !m.Contains("hạn chế"));
        Assert.Equal(4, missing.Count);
    }

    [Fact]
    public void PlanContent_ResultNames_FollowForm17()
    {
        Assert.Equal("Đạt yêu cầu", ImprovementPlanContent.ResultName("M30", ImprovementPlanContent.ResultAchieved));
        Assert.Equal("Chưa chuyển biến", ImprovementPlanContent.ResultName("M60", ImprovementPlanContent.ResultNotAchieved));
        Assert.Equal("Đạt (Đóng kế hoạch)", ImprovementPlanContent.ResultName("M90", ImprovementPlanContent.ResultAchieved));
        Assert.Equal("Không đạt (Xem xét nhân sự)", ImprovementPlanContent.ResultName("M90", ImprovementPlanContent.ResultNotAchieved));
        Assert.Equal("Chưa đánh giá", ImprovementPlanContent.ResultName("M90", null));
    }

    #endregion

    #region File mẫu Mẫu 17

    private static (ImprovementPlan Plan, EvaluationRecord Record) SamplePlan()
    {
        var content = new ImprovementPlanContent { SupporterName = "Trần Văn Hỗ Trợ", SupporterTitle = "Phó Trưởng phòng" };
        foreach (var code in new[] { "M30", "M60", "M90" })
        {
            var stage = content.Stage(code);
            stage.Limitation = $"Hạn chế {code}";
            stage.Target = $"Mục tiêu {code}";
            stage.Measures = $"Biện pháp {code}";
            stage.Coordination = $"Phối hợp {code}";
        }
        content.Stage("M30").Result = ImprovementPlanContent.ResultAchieved;
        content.Stage("M60").Result = ImprovementPlanContent.ResultNotAchieved;
        var record = new EvaluationRecord
        {
            FinalGrade = EvaluationGrade.HoanThanh,
            Member = new PartyMemberProfile { FullName = "Nguyễn Văn Khắc Phục", PositionTitle = "Trưởng phòng" },
            Department = new AdministrativeDepartment { Name = "Phòng Kỹ thuật" }
        };
        var plan = new ImprovementPlan
        {
            Content = content.Normalize().ToJson(),
            ApprovedByName = "Lê Thủ Trưởng",
            AcknowledgedAt = DateTime.UtcNow
        };
        return (plan, record);
    }

    [Fact]
    public void Mau17Template_TagsMatchDataClass_AndPassUploadValidation()
    {
        var template = new FileWordTemplateStore().Load(Mau17Data.TemplateFileName);
        var templateTags = DocxTemplateEngine.GetTags(template).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dataTags = TemplateTagCatalog.Describe(typeof(Mau17Data)).Select(t => t.Tag).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orgTags = OrganizationTemplateFields.Tags.Select(t => t.Tag);

        Assert.Empty(templateTags.Except(dataTags.Concat(orgTags), StringComparer.OrdinalIgnoreCase));
        Assert.Empty(dataTags.Except(templateTags, StringComparer.OrdinalIgnoreCase));
        Assert.Contains("ORG_LOCATION", templateTags);
        Assert.Contains("ORG_COMPANY_NAME_UPPER", templateTags);

        var form = WordFormCatalog.Find("MAU_17")!;
        var check = WordTemplateValidator.Check(form, template);
        Assert.True(check.IsValid, string.Join("; ", check.Errors));
        Assert.Empty(check.Warnings);
    }

    [Fact]
    public void Mau17Template_KeepsOriginalFormText()
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(new FileWordTemplateStore().Load(Mau17Data.TemplateFileName)), false);
        var text = doc.MainDocumentPart!.Document!.Body!.InnerText;

        foreach (var printed in new[]
                 {
                     "Mẫu số 17", "KẾ HOẠCH HỖ TRỢ, KHẮC PHỤC VÀ PHÁT TRIỂN 30-60-90 NGÀY",
                     "Bắt buộc áp dụng đối với cán bộ xếp loại Hoàn thành nhiệm vụ - Mức C hoặc Không hoàn thành nhiệm vụ - Mức D",
                     "Họ và tên cán bộ:", "Đơn vị công tác:", "Mức xếp loại quý vừa qua:", "Người trực tiếp hỗ trợ, giám sát:",
                     "Giai đoạn khắc phục", "Hạn chế cần khắc phục", "Mục tiêu/sản phẩm", "Biện pháp & đào tạo hỗ trợ", "Phối hợp/giám sát",
                     "Kết quả sau mỗi mốc", "Mốc 30 ngày (Khắc phục cấp bách)", "Mốc 60 ngày (Cải thiện hiệu suất)", "Mốc 90 ngày (Đánh giá chuyển biến)",
                     "Đạt yêu cầu", "Chưa chuyển biến", "Đạt (Đóng kế hoạch)", "Không đạt (Xem xét nhân sự)",
                     "THỦ TRƯỞNG ĐƠN VỊ", "NGƯỜI TRỰC TIẾP HỖ TRỢ, THEO DÕI", "CÁ NHÂN CAM KẾT KHẮC PHỤC"
                 })
        {
            Assert.Contains(printed, text);
        }
        // Chỉ phần Mẫu 17: không còn mẫu khác, không còn chữ "Ví dụ" hướng dẫn trong ô điền.
        Assert.DoesNotContain("Mẫu số 16", text);
        Assert.DoesNotContain("Mẫu số 18", text);
        Assert.DoesNotContain("Ví dụ:", text);
    }

    [Fact]
    public void Mau17_RendersPlanAndRecord_ValidOpenXml()
    {
        var (plan, record) = SamplePlan();
        var template = new FileWordTemplateStore().Load(Mau17Data.TemplateFileName);
        var org = OrganizationTemplateFields.From("ĐẢNG BỘ A", "ĐẢNG BỘ CẤP TRÊN", "Công ty A", "Tổng công ty B", "A", "Hà Nội");

        var result = DocxTemplateEngine.Render(template, TemplateDataBinder.Bind(Mau17Data.From(plan, record)).WithShared(TemplateDataBinder.Bind(org)));

        using var doc = WordprocessingDocument.Open(new MemoryStream(result.Content), false);
        Assert.Empty(new OpenXmlValidator(FileFormatVersions.Office2019).Validate(doc));
        var body = doc.MainDocumentPart!.Document!.Body!;
        var text = body.InnerText;
        foreach (var expected in new[]
                 {
                     "Nguyễn Văn Khắc Phục", "Trưởng phòng", "Phòng Kỹ thuật", "Hoàn thành nhiệm vụ - Mức C", "Trần Văn Hỗ Trợ",
                     "Phó Trưởng phòng", "Hạn chế M30", "Mục tiêu M60", "Biện pháp M90", "Phối hợp M90", "Lê Thủ Trưởng",
                     "TỔNG CÔNG TY B", "CÔNG TY A", "Hà Nội"
                 })
        {
            Assert.Contains(expected, text);
        }
        // M30 đạt, M60 chưa chuyển biến, M90 chưa đánh giá: đúng 2 ô được đánh dấu.
        Assert.Equal(2, text.Count(c => c == '☒'));
        Assert.Equal(4, text.Count(c => c == '☐'));
        Assert.DoesNotContain(body.Descendants<SdtElement>(), sdt => sdt.SdtProperties?.GetFirstChild<Tag>() != null);
    }

    #endregion

    #region Nhắc việc

    [Fact]
    public async Task NotificationSummary_CountsOverdueDueSoonAndPostPublishGroups()
    {
        var today = EvaluationMapping.Today(DateTime.UtcNow);
        WorkQueueItemDto Item(DateOnly? deadline, bool overdue) => new()
        {
            RecordId = Guid.NewGuid(), FullName = "Cán bộ", PeriodName = "Kỳ", Deadline = deadline, Overdue = overdue
        };
        var queue = new WorkQueueDto
        {
            Groups =
            {
                new WorkQueueGroupDto
                {
                    Step = "B3B_APPRAISAL", StepName = "Thẩm định",
                    Items = { Item(today.AddDays(-1), true), Item(today, false), Item(today.AddDays(2), false), Item(today.AddDays(3), false), Item(null, false) }
                },
                new WorkQueueGroupDto { Step = PostPublishWorkQueue.AppealsGroup, StepName = "Kiến nghị chờ xử lý", Count = 1, Items = { Item(null, false) } },
                new WorkQueueGroupDto { Step = PostPublishWorkQueue.ImprovementPlansGroup, StepName = "Kế hoạch", Count = 2, Items = { Item(null, false), Item(null, false) } }
            }
        };
        queue.Groups[0].Count = 5;
        queue.Total = 8;

        var summary = await new NotificationService(new FakeWorkflow(queue), new NotificationOptions { DueSoonDays = 2 }).GetSummaryAsync();

        Assert.Equal(8, summary.Total);
        Assert.Equal(5, summary.PendingSteps);
        Assert.Equal(1, summary.Overdue);
        Assert.Equal(2, summary.DueSoon);
        Assert.Equal(1, summary.Appeals);
        Assert.Equal(2, summary.ImprovementPlans);
        Assert.Equal(0, summary.PlansToAcknowledge);
        Assert.Equal("overdue", summary.Items[0].Kind);
        Assert.Equal(new[] { "dueSoon", "dueSoon" }, summary.Items.Skip(1).Take(2).Select(i => i.Kind));
        Assert.Equal("appeal", summary.Items[3].Kind);
        Assert.All(summary.Items, i => Assert.StartsWith("/evaluations/", i.Link));
    }

    private sealed class FakeWorkflow : IEvaluationWorkflowService
    {
        private readonly WorkQueueDto _queue;

        public FakeWorkflow(WorkQueueDto queue) => _queue = queue;

        public Task<WorkQueueDto> GetWorkQueueAsync(Guid? periodId, CancellationToken ct = default) => Task.FromResult(_queue);

        public Task<EvaluationRecordDto> SubmitTasksAsync(Guid recordId, SubmitTasksRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EvaluationRecordDto> ApproveTasksAsync(Guid recordId, CommentRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EvaluationRecordDto> ReturnTasksAsync(Guid recordId, ReturnRecordRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EvaluationRecordDto> SubmitSelfScoreAsync(Guid recordId, SubmitSelfScoreRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EvaluationRecordDto> ConfirmByCellAsync(Guid recordId, CommentRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EvaluationRecordDto> ReturnByCellAsync(Guid recordId, ReturnRecordRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EvaluationRecordDto> RecordCollectiveProposalAsync(Guid recordId, CollectiveProposalRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EvaluationRecordDto> AppraiseAsync(Guid recordId, AppraisalRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EvaluationRecordDto> ReturnByAppraiserAsync(Guid recordId, ReturnRecordRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EvaluationRecordDto> DirectorReviewAsync(Guid recordId, DirectorReviewRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EvaluationRecordDto> RecordDecisionAsync(Guid recordId, DecisionRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EvaluationRecordDto> PublishAsync(Guid recordId, WorkflowRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EvaluationRecordDto> ReopenAsync(Guid recordId, ReopenRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EvaluationRecordDto> RecordExternalResultAsync(Guid recordId, string step, ExternalResultRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RecordActionsDto> GetActionsAsync(Guid recordId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    #endregion
}
