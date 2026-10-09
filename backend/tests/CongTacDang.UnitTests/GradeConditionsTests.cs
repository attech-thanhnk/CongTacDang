using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Điều kiện xếp loại theo mức thẩm định từng nhiệm vụ — chỉ cảnh báo.</summary>
public sealed class GradeConditionsTests
{
    private static GradeConditionTask Focus(int? level, bool exceed = false) => new(TaskKind.TrongTam, level, exceed);
    private static GradeConditionTask Regular(int? level, bool exceed = false) => new(TaskKind.ThuongXuyen, level, exceed);

    [Fact]
    public void Excellent_AllFocusLevel1_EnoughExceed_NoWarning()
    {
        var tasks = new[] { Focus(1, true), Focus(1), Regular(2) };
        Assert.Empty(GradeConditions.Check(tasks, EvaluationGrade.HoanThanhXuatSac, 0.30));
    }

    [Fact]
    public void Excellent_FocusLevel2_Warns()
    {
        var tasks = new[] { Focus(2, true), Focus(1, true), Regular(1) };
        var warning = Assert.Single(GradeConditions.Check(tasks, EvaluationGrade.HoanThanhXuatSac, 0.30));
        Assert.Contains("Mức 2", warning);
        Assert.Contains("Hoàn thành xuất sắc", warning);
    }

    [Fact]
    public void Excellent_ExceedRatioBelowThreshold_Warns()
    {
        // 1/4 = 25% < 30%.
        var tasks = new[] { Focus(1, true), Focus(1), Regular(1), Regular(1) };
        var warning = Assert.Single(GradeConditions.Check(tasks, EvaluationGrade.HoanThanhXuatSac, 0.30));
        Assert.Contains("1/4", warning);
        Assert.Contains("30%", warning);
    }

    [Fact]
    public void Excellent_ExceedRatioAtThreshold_NoWarning()
    {
        // 3/10 = 30% đúng ngưỡng.
        var tasks = Enumerable.Range(0, 10).Select(i => Regular(1, i < 3)).ToArray();
        Assert.Empty(GradeConditions.Check(tasks, EvaluationGrade.HoanThanhXuatSac, 0.30));
    }

    [Fact]
    public void Excellent_NoThresholdInCriteria_SkipsExceedCheck()
    {
        var tasks = new[] { Focus(1), Regular(1) };
        Assert.Empty(GradeConditions.Check(tasks, EvaluationGrade.HoanThanhXuatSac, null));
    }

    [Theory]
    [InlineData(EvaluationGrade.HoanThanhXuatSac)]
    [InlineData(EvaluationGrade.HoanThanhTot)]
    [InlineData(EvaluationGrade.HoanThanh)]
    public void FocusLevel3_OnlyNotCompleted(EvaluationGrade grade)
    {
        var tasks = new[] { Focus(3, true), Focus(1, true), Regular(1, true) };
        var warnings = GradeConditions.Check(tasks, grade, 0.30);
        Assert.Contains(warnings, w => w.Contains("Mức 3") && w.Contains("Không hoàn thành nhiệm vụ"));
    }

    [Fact]
    public void FocusLevel3_NotCompleted_NoWarning()
    {
        var tasks = new[] { Focus(3), Regular(1) };
        Assert.Empty(GradeConditions.Check(tasks, EvaluationGrade.KhongHoanThanh, 0.30));
    }

    [Theory]
    [InlineData(EvaluationGrade.HoanThanhTot)]
    [InlineData(EvaluationGrade.HoanThanh)]
    public void GoodOrCompleted_FocusLevel2_NoWarning(EvaluationGrade grade)
    {
        var tasks = new[] { Focus(2), Regular(3) };
        Assert.Empty(GradeConditions.Check(tasks, grade, 0.30));
    }

    [Fact]
    public void RegularLevel3_NotFocus_NoWarning()
    {
        var tasks = new[] { Focus(1, true), Regular(3, true) };
        Assert.Empty(GradeConditions.Check(tasks, EvaluationGrade.HoanThanhXuatSac, 0.30));
    }

    [Fact]
    public void LegacyTaskWithoutKind_TreatedAsNotFocus()
    {
        var tasks = new[] { new GradeConditionTask(null, 3, true), Focus(1, true) };
        Assert.Empty(GradeConditions.Check(tasks, EvaluationGrade.HoanThanhXuatSac, 0.30));
    }

    [Fact]
    public void NoTasksOrNoGrade_NoWarning()
    {
        Assert.Empty(GradeConditions.Check(Array.Empty<GradeConditionTask>(), EvaluationGrade.HoanThanhXuatSac, 0.30));
        Assert.Empty(GradeConditions.Check(new[] { Focus(3) }, EvaluationGrade.ChuaXepLoai, 0.30));
    }

    [Fact]
    public void LevelNotYetSet_Ignored()
    {
        var tasks = new[] { Focus(null, true), Focus(null, true) };
        Assert.Empty(GradeConditions.Check(tasks, EvaluationGrade.HoanThanhXuatSac, 0.30));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void IsValidLevel(int level, bool expected) => Assert.Equal(expected, GradeConditions.IsValidLevel(level));

    [Fact]
    public void ExcellentMinExceedRatio_ReadsCriteriaParameter()
    {
        var content = CriteriaSetDefaults.Build09A();
        Assert.Equal(0.30, GradeConditions.ExcellentMinExceedRatio(content));
        Assert.Null(GradeConditions.ExcellentMinExceedRatio(null));
    }
}
