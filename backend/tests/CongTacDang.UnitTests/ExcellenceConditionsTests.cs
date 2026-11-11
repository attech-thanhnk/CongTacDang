using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using CongTacDang.Infrastructure.Documents;
using CongTacDang.Infrastructure.Documents.Forms;
using CongTacDang.Infrastructure.Services;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Sáu điều kiện Hoàn thành xuất sắc — chỉ cảnh báo, in phiếu.</summary>
public sealed class ExcellenceConditionsTests
{
    private static ConditionTask Task(int order, bool exceed = false, int? level = 1, double? progress = 1, TaskKind kind = TaskKind.ThuongXuyen) =>
        new(order, kind, level, exceed, progress);

    private static ExcellenceConditionInput Input(params ConditionTask[] tasks) => new()
    {
        Tasks = tasks,
        MinExceedRatio = 0.30,
        SelfScored = true,
        SelfProposedGrade = EvaluationGrade.HoanThanhXuatSac,
        Appraised = true
    };

    private static string RenderText(string file, object data)
    {
        var result = DocxTemplateEngine.Render(new FileWordTemplateStore().Load(file), TemplateDataBinder.Bind(data));
        using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(new System.IO.MemoryStream(result.Content), false);
        return doc.MainDocumentPart!.Document!.Body!.InnerText;
    }

    private static ConditionResult Of(IEnumerable<ConditionResult> results, string code) => results.Single(r => r.Code == code);

    [Fact]
    public void Completion_AlwaysUnknown_ListsHints()
    {
        var report = ExcellenceConditions.Evaluate(Input(Task(1, progress: 0.85), Task(2, level: 3), Task(3, exceed: true)));
        var completion = Of(report.Computed, ExcellenceConditionCodes.Completion);
        Assert.Equal(ConditionState.Unknown, completion.State);
        Assert.Contains("SP1: tiến độ tự chấm 85%, dưới 90%", report.CompletionHints);
        Assert.Contains("SP2: thẩm định Mức 3", report.CompletionHints);
    }

    [Fact]
    public void Completion_NoProblems_HintsNone()
    {
        var report = ExcellenceConditions.Evaluate(Input(Task(1), Task(2)));
        Assert.Equal(new[] { "không có sản phẩm chậm tiến độ hay không đáp ứng" }, report.CompletionHints);
    }

    [Fact]
    public void Override_AppliesToFinalOnly()
    {
        var input = Input(Task(1, exceed: true), Task(2));
        var report = ExcellenceConditions.Evaluate(new ExcellenceConditionInput
        {
            Tasks = input.Tasks, MinExceedRatio = 0.3, Appraised = true,
            Overrides = new Dictionary<string, bool> { [ExcellenceConditionCodes.Completion] = false }
        });
        Assert.Equal(ConditionState.Unknown, Of(report.Computed, "I").State);
        Assert.Equal(ConditionState.NotMet, Of(report.Final, "I").State);
        Assert.Equal(ConditionState.NotMet, report.FinalOverall);
    }

    [Fact]
    public void AllMet_WhenEverythingConfirmed()
    {
        var report = ExcellenceConditions.Evaluate(new ExcellenceConditionInput
        {
            Tasks = new[] { Task(1, exceed: true), Task(2), Task(3) },
            MinExceedRatio = 0.3,
            Appraised = true,
            Overrides = new Dictionary<string, bool> { ["I"] = true }
        });
        Assert.Equal(ConditionState.NotApplicable, Of(report.Final, "II").State);
        Assert.Equal(ConditionState.Met, Of(report.Final, "IV").State);
        Assert.Equal(ConditionState.Met, report.FinalOverall);
    }

    [Fact]
    public void ExceedStandard_BelowThreshold_ReasonWithCounts()
    {
        var tasks = new[] { Task(1, exceed: true), Task(2, exceed: true) }.Concat(Enumerable.Range(3, 5).Select(i => Task(i))).ToArray();
        var report = ExcellenceConditions.Evaluate(Input(tasks));
        var exceed = Of(report.Computed, "III");
        Assert.Equal(ConditionState.NotMet, exceed.State);
        Assert.Equal("mới có 2/7 sản phẩm vượt chuẩn (28,6%), cần từ 30%", exceed.Reason);
        Assert.Equal(ConditionState.NotMet, report.SelfOverall);
    }

    [Theory]
    [InlineData(10, 10, ConditionState.Met)]
    [InlineData(10, 8, ConditionState.NotMet)]
    public void UnitCompletion_FromMau06(int assigned, int onTime, ConditionState expected)
    {
        var report = ExcellenceConditions.Evaluate(new ExcellenceConditionInput
        {
            Tasks = new[] { Task(1) }, IsLeader = true, UnitReport = new UnitTaskReport(assigned, onTime)
        });
        Assert.Equal(expected, Of(report.Computed, "II").State);
    }

    [Fact]
    public void UnitCompletion_LeaderWithoutMau06_Unknown_NonLeader_NotApplicable()
    {
        Assert.Equal(ConditionState.Unknown, Of(ExcellenceConditions.Evaluate(new ExcellenceConditionInput { IsLeader = true }).Computed, "II").State);
        Assert.Equal(ConditionState.NotApplicable, Of(ExcellenceConditions.Evaluate(new ExcellenceConditionInput()).Computed, "II").State);
    }

    [Fact]
    public void Remediation_ByPreviousPlanMilestones()
    {
        PreviousPlanState Plan(params PlanMilestoneState[] m) => new(true, m);
        ConditionState State(PreviousPlanState? plan) =>
            Of(ExcellenceConditions.Evaluate(new ExcellenceConditionInput { PreviousPlan = plan }).Computed, "IV").State;

        Assert.Equal(ConditionState.Met, State(null));
        Assert.Equal(ConditionState.Unknown, State(new PreviousPlanState(false, Array.Empty<PlanMilestoneState>())));
        Assert.Equal(ConditionState.Met, State(Plan(new("M30", ImprovementPlanContent.ResultAchieved, true), new("M60", null, false))));
        Assert.Equal(ConditionState.NotMet, State(Plan(new PlanMilestoneState("M30", ImprovementPlanContent.ResultNotAchieved, true))));
        Assert.Equal(ConditionState.NotMet, State(Plan(new PlanMilestoneState("M30", null, true))));
    }

    [Fact]
    public void NoCap_FromAppraisalCap()
    {
        var capped = ExcellenceConditions.Evaluate(new ExcellenceConditionInput
        {
            Appraised = true, GradeCap = EvaluationGrade.HoanThanh, CapReason = "Sự cố an toàn"
        });
        var v = Of(capped.Computed, "V");
        Assert.Equal(ConditionState.NotMet, v.State);
        Assert.Contains("Hoàn thành nhiệm vụ: Sự cố an toàn", v.Reason);
        Assert.Equal(ConditionState.Unknown, Of(ExcellenceConditions.Evaluate(new ExcellenceConditionInput()).Computed, "V").State);
        Assert.Equal(ConditionState.Met, Of(ExcellenceConditions.Evaluate(new ExcellenceConditionInput { SelfDeclaresNoCap = true }).Self, "V").State);
    }

    [Fact]
    public void FocusLevel2_NotMet()
    {
        var report = ExcellenceConditions.Evaluate(Input(Task(1, kind: TaskKind.TrongTam, level: 2), Task(2, exceed: true)));
        var vi = Of(report.Computed, "VI");
        Assert.Equal(ConditionState.NotMet, vi.State);
        Assert.Equal("nhiệm vụ trọng tâm SP1 Mức 2", vi.Reason);
    }

    [Fact]
    public void ExceedsCap_ComparesRanks()
    {
        Assert.True(ExcellenceConditions.ExceedsCap(EvaluationGrade.HoanThanhTot, EvaluationGrade.HoanThanh));
        Assert.False(ExcellenceConditions.ExceedsCap(EvaluationGrade.HoanThanh, EvaluationGrade.HoanThanh));
        Assert.False(ExcellenceConditions.ExceedsCap(EvaluationGrade.HoanThanhXuatSac, null));
    }

    [Fact]
    public void SelfRemark_Texts()
    {
        var ok = new[] { Task(1, exceed: true), Task(2) };
        var low = new[] { Task(1), Task(2), Task(3), Task(4) };
        Assert.Null(ExcellenceConditions.SelfRemark(false, EvaluationGrade.HoanThanhXuatSac, ok, 0.3, true));
        Assert.Equal("Đáp ứng điều kiện mức Hoàn thành xuất sắc nhiệm vụ; không thuộc trường hợp bị khống chế",
            ExcellenceConditions.SelfRemark(true, EvaluationGrade.HoanThanhXuatSac, ok, 0.3, true));
        Assert.Equal("Chưa đủ điều kiện Hoàn thành xuất sắc nhiệm vụ: mới có 0/4 sản phẩm vượt chuẩn (0%), cần từ 30%",
            ExcellenceConditions.SelfRemark(true, EvaluationGrade.HoanThanhXuatSac, low, 0.3, null));
        Assert.Equal("Đáp ứng điều kiện mức Hoàn thành tốt nhiệm vụ", ExcellenceConditions.SelfRemark(true, EvaluationGrade.HoanThanhTot, low, 0.3, false));
    }

    [Fact]
    public void Overrides_Json_RoundTrip_DropsUnknownCodes()
    {
        var json = ExcellenceConditionCodes.OverridesToJson(new Dictionary<string, bool> { ["I"] = true, ["X"] = false });
        Assert.Equal(new Dictionary<string, bool> { ["I"] = true }, ExcellenceConditionCodes.ParseOverrides(json));
        Assert.Null(ExcellenceConditionCodes.OverridesToJson(new Dictionary<string, bool>()));
        Assert.Empty(ExcellenceConditionCodes.ParseOverrides("not json"));
    }

    [Fact]
    public void Mau10_PrintsConditionsAndCap()
    {
        var record = DocumentTemplateTests.SampleData().Records[0];
        record.AppraisedAt ??= DateTime.UtcNow;
        record.SelfScoredAt ??= DateTime.UtcNow;
        record.SelfProposedGrade = EvaluationGrade.HoanThanhXuatSac;
        record.SelfDeclaresNoCap = true;
        record.AppraisalGradeCap = EvaluationGrade.HoanThanhTot;
        record.AppraisalCapReason = "Vi phạm quy trình";
        var report = ExcellenceConditions.Evaluate(new ExcellenceConditionInput
        {
            Tasks = ExcellenceConditions.TasksOf(record.Tasks), MinExceedRatio = 0.3, SelfScored = true,
            SelfProposedGrade = record.SelfProposedGrade, SelfDeclaresNoCap = true, Appraised = true,
            GradeCap = record.AppraisalGradeCap, CapReason = record.AppraisalCapReason
        });
        var data = Mau10Data.From(record, null, report);

        Assert.Equal("Không vi phạm", data.SelfCap);
        Assert.Equal("Khống chế tối đa mức Hoàn thành tốt nhiệm vụ: Vi phạm quy trình", data.AppraisalCap);
        Assert.StartsWith("☐ Đạt  ☒ Không", data.AppraisalExcellent);
        Assert.Contains("(v) khống chế tối đa mức", data.AppraisalExcellent);

        var text = RenderText(Mau10Data.TemplateFileName, data);
        Assert.Contains("Khống chế tối đa mức Hoàn thành tốt nhiệm vụ: Vi phạm quy trình", text);
        Assert.Contains("(v) khống chế tối đa mức", text);

        record.SelfProposedGrade = EvaluationGrade.HoanThanhTot;
        Assert.Equal(Mau10Data.NotProposed, Mau10Data.From(record, null, ExcellenceConditions.Evaluate(new ExcellenceConditionInput
        {
            SelfScored = true, SelfProposedGrade = EvaluationGrade.HoanThanhTot
        })).SelfExcellent);
    }

    [Fact]
    public void Mau10_NotAppraised_KeepsDefaults()
    {
        var record = DocumentTemplateTests.SampleData().Records[0];
        record.AppraisedAt = null;
        record.SelfDeclaresNoCap = null;
        var data = Mau10Data.From(record, null, ExcellenceConditions.Evaluate(new ExcellenceConditionInput()));
        Assert.Null(data.AppraisalExcellent);
        Assert.Null(data.AppraisalCap);
        Assert.Null(data.SelfCap);
        var text = RenderText(Mau10Data.TemplateFileName, data);
        Assert.DoesNotContain("Không vi phạm", text);
    }
}
