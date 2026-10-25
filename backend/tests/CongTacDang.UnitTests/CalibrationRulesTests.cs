using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Quy tắc thuần của biên bản hiệu chuẩn (Mẫu 20) và giá trị sau hiệu chuẩn trên hồ sơ.</summary>
public sealed class CalibrationRulesTests
{
    private static EvaluationRecord Appraised(EvaluationGrade grade, double score) => new()
    {
        Status = RecordStatus.AwaitingDirectorReview, AppraisalProposedGrade = grade, AppraisalScore = score, AppraisedAt = DateTime.UtcNow
    };

    [Fact]
    public void Before_PrefersAppraisal_ThenCollective_ThenSelf()
    {
        var record = new EvaluationRecord
        {
            SelfScoredAt = DateTime.UtcNow, TotalSelfScore = 80, SelfProposedGrade = EvaluationGrade.HoanThanhTot,
            CollectiveProposedGrade = EvaluationGrade.HoanThanh
        };
        Assert.Equal((EvaluationGrade.HoanThanh, 80.0), CalibrationRules.Before(record));

        record.AppraisalProposedGrade = EvaluationGrade.HoanThanhXuatSac;
        record.AppraisalScore = 92;
        Assert.Equal((EvaluationGrade.HoanThanhXuatSac, 92.0), CalibrationRules.Before(record));

        var onlySelf = new EvaluationRecord { SelfScoredAt = DateTime.UtcNow, TotalSelfScore = 70, SelfProposedGrade = EvaluationGrade.HoanThanh };
        Assert.Equal((EvaluationGrade.HoanThanh, 70.0), CalibrationRules.Before(onlySelf));
        Assert.Equal((EvaluationGrade.ChuaXepLoai, (double?)null), CalibrationRules.Before(new EvaluationRecord()));
    }

    [Fact]
    public void Before_IgnoresCalibratedValues_SoReopeningGivesSameBaseline()
    {
        var record = Appraised(EvaluationGrade.HoanThanhTot, 85);
        record.CalibratedGrade = EvaluationGrade.HoanThanh;
        record.CalibratedScore = 70;

        Assert.Equal((EvaluationGrade.HoanThanhTot, 85.0), CalibrationRules.Before(record));
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData(EvaluationGrade.HoanThanhTot, 85.0, false)]
    [InlineData(EvaluationGrade.HoanThanh, null, true)]
    [InlineData(null, 80.0, true)]
    [InlineData(null, 85.0000001, false)]
    public void IsChanged_ComparesWithBefore(EvaluationGrade? gradeAfter, double? scoreAfter, bool expected)
    {
        var item = new CalibrationCase { GradeBefore = EvaluationGrade.HoanThanhTot, ScoreBefore = 85, GradeAfter = gradeAfter, ScoreAfter = scoreAfter };

        Assert.Equal(expected, CalibrationRules.IsChanged(item));
    }

    [Fact]
    public void Summarize_CountsBeforeAndAfterFromRecords_PercentRoundedToOneDecimal()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var records = new List<CalibrationRecord>
        {
            new() { RecordId = a, GradeBefore = EvaluationGrade.HoanThanhTot },
            new() { RecordId = b, GradeBefore = EvaluationGrade.HoanThanhTot },
            new() { RecordId = c, GradeBefore = EvaluationGrade.HoanThanh }
        };
        var cases = new List<CalibrationCase>
        {
            new() { RecordId = b, GradeBefore = EvaluationGrade.HoanThanhTot, GradeAfter = EvaluationGrade.HoanThanh }
        };

        var lines = CalibrationRules.Summarize(records, cases);

        Assert.Equal(4, lines.Count);
        var good = lines.Single(l => l.Grade == EvaluationGrade.HoanThanhTot);
        Assert.Equal((2, 66.7, 1, 33.3), (good.CountBefore, good.PercentBefore, good.CountAfter, good.PercentAfter));
        var done = lines.Single(l => l.Grade == EvaluationGrade.HoanThanh);
        Assert.Equal((1, 2), (done.CountBefore, done.CountAfter));
        Assert.Equal(0, lines.Single(l => l.Grade == EvaluationGrade.HoanThanhXuatSac).CountBefore);
        Assert.Null(CalibrationRules.Percent(0, 0));
    }

    [Fact]
    public void ConclusionProblems_ChangedCaseNeedsBasisAndResponsible_SingleRecordNeedsReason()
    {
        var recordId = Guid.NewGuid();
        var session = new CalibrationSession
        {
            GroupName = "Nhóm A", HeldAt = DateTime.UtcNow, ChairName = "Chủ trì", SecretaryName = "Thư ký"
        };
        session.Attendees.Add(new CalibrationAttendee { FullName = "Thành viên" });
        session.Records.Add(new CalibrationRecord { RecordId = recordId, GradeBefore = EvaluationGrade.HoanThanhTot });
        session.Cases.Add(new CalibrationCase
        {
            Order = 1, RecordId = recordId, GradeBefore = EvaluationGrade.HoanThanhTot, GradeAfter = EvaluationGrade.HoanThanh, Clarification = "Đã làm rõ"
        });

        var problems = CalibrationRules.ConclusionProblems(session);

        Assert.Contains(problems, p => p.Contains("căn cứ"));
        Assert.Contains(problems, p => p.Contains("người chịu trách nhiệm"));
        Assert.Contains(problems, p => p.Contains("một hồ sơ"));

        session.Cases.Single().Basis = "Biên bản kiểm tra";
        session.Cases.Single().ResponsibleName = "Chủ trì";
        session.ExtraConclusion = "Không có nhóm tương đồng";
        Assert.Empty(CalibrationRules.ConclusionProblems(session));

        // Trường hợp thảo luận mà giữ nguyên kết quả vẫn phải có nội dung làm rõ.
        session.Cases.Single().Clarification = " ";
        Assert.Single(CalibrationRules.ConclusionProblems(session));
    }

    [Fact]
    public void IsStale_OnlyStepOrResultChangesAfterConclusion()
    {
        var concludedAt = DateTime.UtcNow;
        EvaluationRecordHistory At(WorkflowAction action, int seconds) => new() { Action = action, CreatedAt = concludedAt.AddSeconds(seconds) };

        Assert.False(CalibrationRules.IsStale(concludedAt, new[] { At(WorkflowAction.Calibrate, 0), At(WorkflowAction.Complete, -5) }));
        Assert.False(CalibrationRules.IsStale(concludedAt, new[] { At(WorkflowAction.GrantUnlock, 5), At(WorkflowAction.TaskChange, 5) }));
        Assert.True(CalibrationRules.IsStale(concludedAt, new[] { At(WorkflowAction.Return, 5) }));
        Assert.True(CalibrationRules.IsStale(concludedAt, new[] { At(WorkflowAction.Complete, 5) }));
    }

    [Fact]
    public void CalibratedValues_BecomeEffectiveResult_UntilDecisionIsRecorded()
    {
        var record = Appraised(EvaluationGrade.HoanThanhTot, 85);
        record.SelfScoredAt = DateTime.UtcNow;
        record.TotalSelfScore = 90;
        Assert.Equal(85, record.EffectiveScore());
        Assert.Equal(EvaluationGrade.HoanThanhTot, record.EffectiveGrade());

        record.CalibratedGrade = EvaluationGrade.HoanThanh;
        record.CalibratedScore = 78;
        record.CalibratedAt = DateTime.UtcNow;
        Assert.Equal(78, record.ReviewedScore());
        Assert.Equal(EvaluationGrade.HoanThanh, record.ReviewedGrade());
        Assert.Equal(78, record.EffectiveScore());
        Assert.Equal(EvaluationGrade.HoanThanh, record.EffectiveGrade());

        record.FinalGrade = EvaluationGrade.HoanThanhTot;
        record.FinalScore = 84;
        Assert.Equal(84, record.EffectiveScore());
        Assert.Equal(EvaluationGrade.HoanThanhTot, record.EffectiveGrade());

        record.ClearCalibration();
        Assert.Null(record.CalibrationSessionId);
        Assert.Equal(85, record.ReviewedScore());
    }

    [Fact]
    public void EffectiveGrade_DirectorProposalLaterThanCalibration_Wins()
    {
        var record = Appraised(EvaluationGrade.HoanThanhTot, 85);
        record.CalibratedGrade = EvaluationGrade.HoanThanh;
        record.CalibratedAt = DateTime.UtcNow.AddMinutes(-10);
        record.DirectorProposedGrade = EvaluationGrade.HoanThanhTot;
        record.DirectorReviewedAt = DateTime.UtcNow;
        Assert.Equal(EvaluationGrade.HoanThanhTot, record.EffectiveGrade());

        // Hiệu chuẩn sau nhận xét của cấp trực tiếp sử dụng thì mức hiệu chuẩn là mới nhất.
        record.CalibratedAt = DateTime.UtcNow.AddMinutes(10);
        Assert.Equal(EvaluationGrade.HoanThanh, record.EffectiveGrade());
    }
}
