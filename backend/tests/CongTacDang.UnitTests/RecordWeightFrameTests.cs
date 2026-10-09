using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>
/// Điều chỉnh tỷ lệ A-B-C-D của khung cho từng hồ sơ (cấp giao nhiệm vụ, ±biên độ của bộ tiêu chí, tổng 100%) và khung hiệu lực.
/// </summary>
public sealed class RecordWeightFrameTests
{
    private static CriteriaSetContent Set09A() => CriteriaSetDefaults.Build09A();

    private static EvaluationRecord Record(string frame = "K2") => new() { WeightFrameCode = frame };

    [Fact]
    public void DefaultSets_AdjustLimit_09AIs10_09BNone_AndOldJsonReadsNull()
    {
        Assert.Equal(10, Set09A().Parameters.FrameAdjustLimit);
        Assert.True(Set09A().Parameters.AllowsFrameAdjustment);
        Assert.Null(CriteriaSetDefaults.Build09B().Parameters.FrameAdjustLimit);

        // Nội dung lưu trước khi có tham số (không có khóa frameAdjustLimit) vẫn đọc được, mặc định không cho điều chỉnh.
        var json = Set09A().ToJson().Replace("\"frameAdjustLimit\":10,", string.Empty);
        Assert.DoesNotContain("frameAdjustLimit", json);
        var parsed = CriteriaSetContent.Parse(json);
        Assert.Null(parsed.Parameters.FrameAdjustLimit);
        Assert.False(parsed.Parameters.AllowsFrameAdjustment);
        Assert.Empty(parsed.Validate(CriteriaSetContent.Form09A));
        Assert.Equal(10, CriteriaSetContent.Parse(Set09A().ToJson()).Parameters.FrameAdjustLimit);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(51, true)]
    [InlineData(0, false)]
    [InlineData(10, false)]
    public void Validate_AdjustLimitRange(double limit, bool hasError)
    {
        var content = Set09A();
        content.Parameters.FrameAdjustLimit = limit;
        var errors = content.Validate(CriteriaSetContent.Form09A);
        Assert.Equal(hasError, errors.Any(e => e.Contains("Biên độ điều chỉnh")));
    }

    [Fact]
    public void ValidateAdjustment_LimitSumAndNegative()
    {
        var content = Set09A();
        var k2 = content.FindFrame("K2")!; // A 15 · B 50 · C 15 · D 20

        Assert.Null(RecordWeightFrames.ValidateAdjustment(content, k2, 25, 40, 15, 20)); // đúng ±10
        Assert.Null(RecordWeightFrames.ValidateAdjustment(content, k2, 15.005, 49.995, 15, 20));
        Assert.Contains("Tỷ lệ B (35%) lệch quá 10", RecordWeightFrames.ValidateAdjustment(content, k2, 15, 35, 30, 20));
        Assert.Contains("Tổng tỷ lệ A-B-C-D phải bằng 100% (đang là 105%)", RecordWeightFrames.ValidateAdjustment(content, k2, 20, 50, 15, 20));
        Assert.Contains("không được âm", RecordWeightFrames.ValidateAdjustment(content, content.FindFrame("K1")!, -1, 45, 30, 26));
        Assert.Contains("phải là số", RecordWeightFrames.ValidateAdjustment(content, k2, double.NaN, 50, 15, 20));
        Assert.Contains("chưa có khung", RecordWeightFrames.ValidateAdjustment(content, null, 25, 40, 15, 20));

        content.Parameters.FrameAdjustLimit = null;
        Assert.Contains("không cho điều chỉnh", RecordWeightFrames.ValidateAdjustment(content, k2, 25, 40, 15, 20));
        content.Parameters.FrameAdjustLimit = 0;
        Assert.Contains("không cho điều chỉnh", RecordWeightFrames.ValidateAdjustment(content, k2, 15, 50, 15, 20));
    }

    [Fact]
    public void Effective_UsesAdjustedWeights_ApplySameAsBaseClears()
    {
        var content = Set09A();
        var record = Record("k2");
        var k2 = content.FindFrame("K2")!;

        Assert.Same(k2, RecordWeightFrames.Effective(record, content));
        Assert.False(RecordWeightFrames.IsAdjusted(record));

        Assert.True(RecordWeightFrames.Apply(record, k2, 25, 40, 15, 20));
        Assert.True(RecordWeightFrames.IsAdjusted(record));
        var effective = RecordWeightFrames.Effective(record, content)!;
        Assert.Equal(("K2", 0.25, 0.40, 0.15, 0.20), (effective.Code, effective.A, effective.B, effective.C, effective.D));
        Assert.Equal(0.15, k2.A); // khung của bộ không bị sửa
        Assert.Equal("A 25% · B 40% · C 15% · D 20%", RecordWeightFrames.Describe(effective));
        Assert.False(RecordWeightFrames.Apply(record, k2, 25, 40, 15, 20)); // không đổi

        // Công thức chấm giữ nguyên, chỉ thay tỷ trọng: 30 × (0,25 + 0,40×0,95 + 0,15 + 0,20) = 29,4 (khung gốc: 29,3).
        var rounding = content.Parameters.Rounding.TaskScore;
        Assert.Equal(29.4, EvaluationScoring.ScoreTask(30, 1, 0.95, 1, 1, effective, rounding).Score);
        Assert.Equal(29.3, EvaluationScoring.ScoreTask(30, 1, 0.95, 1, 1, k2, rounding).Score);

        // Gửi lại đúng tỷ lệ khung gốc → bỏ điều chỉnh.
        Assert.True(RecordWeightFrames.Apply(record, k2, 15, 50, 15, 20));
        Assert.False(RecordWeightFrames.IsAdjusted(record));
        Assert.Same(k2, RecordWeightFrames.Effective(record, content));

        // Khung không có trong bộ → không có khung hiệu lực dù còn số liệu điều chỉnh.
        var orphan = Record("K9");
        orphan.FrameA = 0.25; orphan.FrameB = 0.40; orphan.FrameC = 0.15; orphan.FrameD = 0.20;
        Assert.Null(RecordWeightFrames.Effective(orphan, content));
        RecordWeightFrames.Clear(orphan);
        Assert.Null(orphan.FrameA);
    }
}
