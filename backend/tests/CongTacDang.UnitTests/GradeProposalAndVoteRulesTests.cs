using System;
using System.Collections.Generic;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Organization;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Mức đề xuất so với mức theo điểm, mức theo kết quả kiểm phiếu, hồ sơ luồng gợi ý theo chức vụ, người lập kế hoạch.</summary>
public sealed class GradeProposalAndVoteRulesTests
{
    private static CriteriaSetContent Content09B(bool explanationOnGradeChange = true)
    {
        var content = CriteriaSetDefaults.Build09B();
        content.Parameters.ExplanationOnGradeChange = explanationOnGradeChange;
        return content;
    }

    #region Mức đề xuất so với mức theo điểm

    [Fact]
    public void Proposal_ExcellentAt87_IsHigherThanScoreGrade_RequiresExplanation()
    {
        var content = Content09B();
        // Lỗi cũ: thẩm định 30 + 57 = 87 (dưới ngưỡng 90) đề xuất Xuất sắc vẫn được nhận, không cảnh báo.
        Assert.Equal(1, EvaluationScoring.CompareWithScoreGrade(content, EvaluationGrade.HoanThanhXuatSac, 87));
        Assert.True(EvaluationScoring.ProposalRequiresExplanation(content, EvaluationGrade.HoanThanhXuatSac, 87));
        Assert.Equal(0, EvaluationScoring.CompareWithScoreGrade(content, EvaluationGrade.HoanThanhTot, 87));
        Assert.False(EvaluationScoring.ProposalRequiresExplanation(content, EvaluationGrade.HoanThanhTot, 87));
    }

    [Fact]
    public void Proposal_Lower_IsNegative_RequiresExplanation_FlagOff_OnlyWarns()
    {
        Assert.Equal(-1, EvaluationScoring.CompareWithScoreGrade(Content09B(), EvaluationGrade.HoanThanhTot, 93));
        Assert.True(EvaluationScoring.ProposalRequiresExplanation(Content09B(), EvaluationGrade.HoanThanhTot, 93));
        Assert.Equal(-2, EvaluationScoring.CompareWithScoreGrade(Content09B(), EvaluationGrade.HoanThanh, 93));
        Assert.False(EvaluationScoring.ProposalRequiresExplanation(Content09B(explanationOnGradeChange: false), EvaluationGrade.HoanThanhXuatSac, 60));
        Assert.Equal(0, EvaluationScoring.CompareWithScoreGrade(Content09B(), EvaluationGrade.ChuaXepLoai, 93));
    }

    [Fact]
    public void Proposal_ScoreGrade_UsesExceedRatioOfCriteria()
    {
        // 09A: Xuất sắc cần tỷ lệ vượt chuẩn của bộ; đủ điểm nhưng thiếu tỷ lệ thì mức theo điểm là Tốt.
        var content = CriteriaSetDefaults.Build09A();
        Assert.Equal(1, EvaluationScoring.CompareWithScoreGrade(content, EvaluationGrade.HoanThanhXuatSac, 95, exceedStandardRatio: 0.1));
        Assert.Equal(0, EvaluationScoring.CompareWithScoreGrade(content, EvaluationGrade.HoanThanhXuatSac, 95, exceedStandardRatio: 0.5));
    }

    [Fact]
    public void EnsureExplained_SelfExcellentAt86_WithoutReason_Throws_WithReason_Passes()
    {
        var content = Content09B();
        var ex = Assert.Throws<ValidationException>(() => GradeProposalRules.EnsureExplained(content, WorkflowStep.B2_SELF_SCORE,
            EvaluationGrade.HoanThanhXuatSac, 86, null, "  ", "\"Lý do đề xuất mức\""));
        Assert.Contains("cao hơn mức theo điểm", ex.Message);
        Assert.Contains("86 điểm", ex.Message);
        GradeProposalRules.EnsureExplained(content, WorkflowStep.B2_SELF_SCORE, EvaluationGrade.HoanThanhXuatSac, 86, null, "Có thành tích nổi trội", "x");
        GradeProposalRules.EnsureExplained(content, WorkflowStep.B2_SELF_SCORE, EvaluationGrade.HoanThanhTot, 86, null, null, "x");
    }

    [Fact]
    public void Deviations_ListEachStep_GradeWarningStepIsEffective()
    {
        var record = new EvaluationRecord
        {
            TotalSelfScore = 86, SelfScoredAt = DateTime.UtcNow, SelfProposedGrade = EvaluationGrade.HoanThanhXuatSac,
            CollectiveProposedGrade = EvaluationGrade.HoanThanhTot, AppraisalScore = 87, AppraisalProposedGrade = EvaluationGrade.HoanThanhXuatSac
        };
        var criteria = new CriteriaSnapshot { Content = Content09B(), SelfScoreForm = CriteriaSetContent.Form09B };
        var deviations = GradeProposalRules.Deviations(record, criteria);
        Assert.Equal(new[] { WorkflowStep.B2_SELF_SCORE, WorkflowStep.B3B_APPRAISAL }, deviations.ConvertAll(d => d.Step));
        Assert.All(deviations, d => Assert.Equal(EvaluationGrade.HoanThanhTot, d.ScoreGrade));
        Assert.Equal(WorkflowStep.B3B_APPRAISAL, GradeProposalRules.EffectiveStep(record));
    }

    #endregion

    #region Kiểm phiếu

    [Fact]
    public void Votes_1Excellent4Good_Of5Present_PassesGood_RejectsExcellent()
    {
        var tally = new VoteTally(1, 4, 0, 0, 0, 0);
        Assert.Equal(EvaluationGrade.HoanThanhTot, CollectiveVoteRules.PassedGrade(tally, 5));
        Assert.Null(CollectiveVoteRules.Validate(EvaluationGrade.HoanThanhTot, tally, 5));
        var error = CollectiveVoteRules.Validate(EvaluationGrade.HoanThanhXuatSac, tally, 5);
        Assert.NotNull(error);
        Assert.Contains("Xuất sắc 1, Tốt 4", error);
        Assert.Contains("(5/5 phiếu)", error);
        Assert.Contains("(1/5 phiếu)", error);
    }

    [Fact]
    public void Votes_Cumulative_FromTop_And_NoMajority()
    {
        // 3 Xuất sắc + 2 Tốt / 7 có mặt: Xuất sắc 3/7 chưa quá nửa, cộng dồn tới Tốt 5/7 → Tốt.
        Assert.Equal(EvaluationGrade.HoanThanhTot, CollectiveVoteRules.PassedGrade(new VoteTally(3, 2, 0, 0, 0, 0), 7));
        // Quá nửa phiếu "Chưa đánh giá" / không hợp lệ → không mức nào đạt.
        var spread = new VoteTally(1, 1, 0, 0, 2, 1);
        Assert.Null(CollectiveVoteRules.PassedGrade(spread, 5));
        Assert.Contains("Không mức nào đạt quá nửa", CollectiveVoteRules.Validate(EvaluationGrade.HoanThanhTot, spread, 5));
        // Đúng một nửa không phải quá nửa.
        Assert.Equal(EvaluationGrade.HoanThanhTot, CollectiveVoteRules.PassedGrade(new VoteTally(2, 1, 1, 0, 0, 0), 4));
        // Chưa có phiếu → không kiểm.
        Assert.Null(CollectiveVoteRules.Validate(EvaluationGrade.HoanThanhXuatSac, new VoteTally(0, 0, 0, 0, 0, 0), 5));
        // Chưa ghi số có mặt → tính trên tổng phiếu.
        Assert.Equal(EvaluationGrade.HoanThanhXuatSac, CollectiveVoteRules.PassedGrade(new VoteTally(3, 2, 0, 0, 0, 0), 0));
    }

    #endregion

    #region Hồ sơ luồng gợi ý theo chức vụ

    private static HeldPosition Held(string name, string? stat, ApprovalAuthority? authority, string? profile = null, bool primary = false) =>
        new(name, stat, authority, false, profile, primary);

    [Fact]
    public void DefaultProfile_DeputyDirector_UsesPositionProfile_NotUpperByAuthority()
    {
        var settings = PeriodSettings.Default(CriteriaSetContent.Form09B);
        var held = new[] { Held("Phó Giám đốc", "M14", ApprovalAuthority.CapTren, PeriodSettings.ProfileDeputyDirector, primary: true) };
        // Lỗi cũ: chỉ theo thẩm quyền → cap-tren.
        Assert.Equal(PeriodSettings.ProfileUpper, settings.ResolveProfile(null, ApprovalAuthority.CapTren).Code);
        Assert.Equal(PeriodSettings.ProfileDeputyDirector, PositionRules.DefaultProfile(settings, ApprovalAuthority.CapTren, held).Code);
        // Không có chức vụ gắn hồ sơ luồng → theo thẩm quyền như cũ.
        Assert.Equal(PeriodSettings.ProfileUpper,
            PositionRules.DefaultProfile(settings, ApprovalAuthority.CapTren, new[] { Held("Giám đốc", "M14", ApprovalAuthority.CapTren) }).Code);
        Assert.Equal(PeriodSettings.ProfileBase, PositionRules.DefaultProfile(settings, ApprovalAuthority.CoSo, null).Code);
    }

    [Fact]
    public void DefaultProfile_IgnoresMissingProfile_AndAuthorityMismatch()
    {
        var settings = PeriodSettings.Default(CriteriaSetContent.Form09B);
        settings.Profiles.RemoveAll(p => p.Code == PeriodSettings.ProfileDeputyDirector);
        var held = new[] { Held("Phó Giám đốc", "M14", ApprovalAuthority.CapTren, PeriodSettings.ProfileDeputyDirector) };
        Assert.Equal(PeriodSettings.ProfileUpper, PositionRules.DefaultProfile(settings, ApprovalAuthority.CapTren, held).Code);

        // Thẩm quyền đặt tay CoSo khác thẩm quyền chức vụ (CapTren) → không dùng gợi ý của chức vụ.
        var full = PeriodSettings.Default(CriteriaSetContent.Form09B);
        Assert.Equal(PeriodSettings.ProfileBase, PositionRules.DefaultProfile(full, ApprovalAuthority.CoSo, held).Code);
    }

    [Fact]
    public void SuggestProfile_ManyPositions_HighestStatCodeThenPrimary()
    {
        var settings = PeriodSettings.Default(CriteriaSetContent.Form09B);
        bool Exists(string code) => settings.FindProfile(code) != null;
        var held = new List<HeldPosition>
        {
            Held("Trưởng phòng", "M26", ApprovalAuthority.CoSo, PeriodSettings.ProfileCellSecretaryStaff, primary: true),
            Held("Phó Giám đốc", "M14", null, PeriodSettings.ProfileDeputyDirector),
            Held("Chức vụ không mã", null, null, PeriodSettings.ProfileBase)
        };
        Assert.Equal("Phó Giám đốc", PositionRules.SuggestWorkflowProfile(held, ApprovalAuthority.CoSo, Exists)!.PositionName);

        var sameRank = new[]
        {
            Held("B", "M14", null, PeriodSettings.ProfileUpper),
            Held("A", "M14", null, PeriodSettings.ProfileDeputyDirector, primary: true)
        };
        Assert.Equal("A", PositionRules.SuggestWorkflowProfile(sameRank, ApprovalAuthority.CapTren, Exists)!.PositionName);
        Assert.Null(PositionRules.SuggestWorkflowProfile(new[] { Held("X", "M1", null) }, ApprovalAuthority.CapTren, Exists));
    }

    #endregion

    [Fact]
    public void ImprovementPlan_Preparer_IsDetected_LegacyPlanWithoutPreparer_NotBlocked()
    {
        var userId = Guid.NewGuid();
        Assert.True(ImprovementPlanService.IsPreparer(new ImprovementPlan { PreparedById = userId }, userId));
        Assert.False(ImprovementPlanService.IsPreparer(new ImprovementPlan { PreparedById = Guid.NewGuid() }, userId));
        Assert.False(ImprovementPlanService.IsPreparer(new ImprovementPlan { PreparedById = null }, userId));
    }
}
