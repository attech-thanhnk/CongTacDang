using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>
/// Lưu nháp phiếu tự chấm: các hàm kiểm tra ở chế độ <c>partial</c> chỉ kiểm định dạng (mã, khoảng điểm, mức, độ dài), không bắt
/// buộc nhập đủ; chế độ mặc định (nộp phiếu) giữ nguyên yêu cầu đầy đủ.
/// </summary>
public class SelfScoreDraftValidationTests
{
    private static readonly CriteriaSetContent Criteria = CriteriaSetDefaults.Build09B();

    [Fact]
    public void GeneralScores_Partial_AllowsMissingItemsAndReasons_ButChecksFormat()
    {
        var partial = new Dictionary<string, GeneralItemScore>
        {
            ["1.1"] = new() { Score = 2 },
            ["2.1"] = new() { Score = 0 } // giảm điểm chưa nêu căn cứ
        };
        Assert.Null(EvaluationScoring.ValidateGeneralScores(Criteria, partial, partial: true));
        Assert.Null(EvaluationScoring.ValidateGeneralScores(Criteria, new Dictionary<string, GeneralItemScore>(), partial: true));
        Assert.NotNull(EvaluationScoring.ValidateGeneralScores(Criteria, partial));

        // Định dạng vẫn kiểm: mã lạ, vượt tối đa, "Đảm bảo/Không đảm bảo" nhận giá trị lẻ.
        Assert.Contains("\"9.9\"", EvaluationScoring.ValidateGeneralScores(Criteria,
            new Dictionary<string, GeneralItemScore> { ["9.9"] = new() { Score = 1 } }, partial: true));
        Assert.NotNull(EvaluationScoring.ValidateGeneralScores(Criteria,
            new Dictionary<string, GeneralItemScore> { ["1.1"] = new() { Score = 99 } }, partial: true));
        Assert.NotNull(EvaluationScoring.ValidateGeneralScores(Criteria,
            new Dictionary<string, GeneralItemScore> { ["1.1"] = new() { Score = 1 } }, partial: true));
    }

    [Fact]
    public void GeneralCriteriaScore_Partial_CountsMissingAsZero()
    {
        var partial = new Dictionary<string, GeneralItemScore> { ["1.1"] = new() { Score = 2 } };
        Assert.Equal(2, EvaluationScoring.GeneralCriteriaScore(Criteria, partial));
    }

    [Fact]
    public void AxisScores_Partial_AllowsMissingAxes_ButChecksRangeAndCode()
    {
        var one = new Dictionary<string, double> { ["T1"] = 12 };
        Assert.Null(EvaluationScoring.ValidateAxisScores(Criteria, one, partial: true));
        Assert.Null(EvaluationScoring.ValidateAxisScores(Criteria, new Dictionary<string, double>(), partial: true));
        Assert.Null(EvaluationScoring.ValidateAxisScores(Criteria, null, partial: true));
        Assert.Contains("Chưa chấm trục", EvaluationScoring.ValidateAxisScores(Criteria, one));

        Assert.NotNull(EvaluationScoring.ValidateAxisScores(Criteria, new Dictionary<string, double> { ["T1"] = 16 }, partial: true));
        Assert.NotNull(EvaluationScoring.ValidateAxisScores(Criteria, new Dictionary<string, double> { ["T1"] = -1 }, partial: true));
        Assert.NotNull(EvaluationScoring.ValidateAxisScores(Criteria, new Dictionary<string, double> { ["T9"] = 1 }, partial: true));
        Assert.Equal(12, EvaluationScoring.AxisTasksScore(Criteria, one));
    }

    [Fact]
    public void SelfAssessment_Partial_AllowsEmptyRequiredSection_ButChecksLengthAndCode()
    {
        var criteria = CriteriaSetDefaults.Build09B();
        criteria.SelfAssessmentSections[0].Required = true;
        var empty = new Dictionary<string, string?>();
        Assert.NotNull(RecordFormContent.ValidateSelfAssessment(criteria, empty));
        Assert.Null(RecordFormContent.ValidateSelfAssessment(criteria, empty, partial: true));

        var max = criteria.SelfAssessmentSections[0].MaxLength;
        Assert.NotNull(RecordFormContent.ValidateSelfAssessment(criteria,
            new Dictionary<string, string?> { ["I"] = new string('a', max + 1) }, partial: true));
        Assert.NotNull(RecordFormContent.ValidateSelfAssessment(criteria,
            new Dictionary<string, string?> { ["X"] = "nội dung" }, partial: true));
    }

    [Fact]
    public void AxisReports_Partial_SkipsBlankRows_ButChecksLevelsAndCodes()
    {
        var withBlank = new Dictionary<string, AxisReport?>
        {
            ["T1"] = new()
            {
                Tasks = new List<AxisTask>
                {
                    new() { Content = "Việc T1", Level = 1, Items = new List<AxisTaskItem> { new() { Content = " " } } },
                    new() { Content = "" }
                }
            }
        };
        Assert.NotNull(RecordFormContent.ValidateAxisReports(Criteria, withBlank));
        Assert.Null(RecordFormContent.ValidateAxisReports(Criteria, withBlank, partial: true));

        // Dòng trống bị bỏ khi ghi.
        var json = RecordFormContent.AxisReportsToJson(Criteria, withBlank);
        var saved = RecordFormContent.ParseAxisReports(json);
        var task = Assert.Single(saved["T1"].Tasks);
        Assert.Empty(task.Items);

        var badLevel = new Dictionary<string, AxisReport?>
        {
            ["T1"] = new() { Tasks = new List<AxisTask> { new() { Content = "Việc", Level = 99 } } }
        };
        Assert.NotNull(RecordFormContent.ValidateAxisReports(Criteria, badLevel, partial: true));
        Assert.NotNull(RecordFormContent.ValidateAxisReports(Criteria,
            new Dictionary<string, AxisReport?> { ["T9"] = new() }, partial: true));
    }
}
