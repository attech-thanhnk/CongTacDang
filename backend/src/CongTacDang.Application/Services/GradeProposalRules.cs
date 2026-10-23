using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.Services;

/// <summary>Mức đề xuất của một bước khác mức theo điểm của bộ tiêu chí.</summary>
/// <param name="Direction">Dương = cao hơn, âm = thấp hơn (số bậc).</param>
public sealed record GradeDeviation(WorkflowStep Step, EvaluationGrade Proposed, EvaluationGrade ScoreGrade, double Score, int Direction,
    bool HasExplanation, string Message);

/// <summary>
/// Đối chiếu mức đề xuất ở từng bước với mức theo điểm (ngưỡng và tỷ lệ vượt chuẩn của bộ tiêu chí). Làm đổi mức (nâng hay hạ) phải
/// nêu căn cứ khi bộ bật <see cref="CriteriaParameters.ExplanationOnGradeChange"/>; bộ tắt thì chỉ cảnh báo.
/// </summary>
public static class GradeProposalRules
{
    /// <summary>Chấm theo nhiệm vụ (09A): tỷ lệ nhiệm vụ còn hiệu lực đánh dấu vượt chuẩn; chấm theo trục → null.</summary>
    public static double? ExceedRatio(EvaluationRecord record, CriteriaSnapshot criteria)
    {
        if (criteria.UsesAxisScoring || record.Tasks == null)
            return null;
        var tasks = record.Tasks.Where(t => !t.IsDeleted && TaskChangeRules.IsActive(t)).ToList();
        return EvaluationScoring.ExceedStandardRatio(tasks.Count(t => t.IsExceedStandard), tasks.Count);
    }

    /// <summary>Chưa nhập căn cứ khi bắt buộc → <see cref="ValidationException"/> nêu mức, điểm và ô cần nhập.</summary>
    public static void EnsureExplained(CriteriaSetContent criteria, WorkflowStep step, EvaluationGrade proposed, double score,
        double? exceedRatio, string? explanation, string fieldName)
    {
        if (!string.IsNullOrWhiteSpace(explanation) || !EvaluationScoring.ProposalRequiresExplanation(criteria, proposed, score, exceedRatio))
            return;
        var direction = EvaluationScoring.CompareWithScoreGrade(criteria, proposed, score, exceedRatio);
        var scoreGrade = EvaluationScoring.SuggestGrade(criteria, score, exceedRatio);
        throw new ValidationException($"{Message(step, proposed, scoreGrade, score, direction)}: hãy nhập {fieldName} "
            + (direction > 0 ? "nêu lý do đề xuất mức cao hơn." : "nêu căn cứ xếp mức thấp hơn."));
    }

    /// <summary>Đã có điểm để đối chiếu (đã nộp phiếu tự chấm hoặc có điểm thẩm định); chưa có thì không so mức.</summary>
    public static bool HasScore(EvaluationRecord r) => r.SelfScoredAt.HasValue || r.AppraisalScore.HasValue;

    /// <summary>Các bước đã có mức đề xuất mà khác mức theo điểm, theo thứ tự bước.</summary>
    public static List<GradeDeviation> Deviations(EvaluationRecord r, CriteriaSnapshot? criteria)
    {
        var result = new List<GradeDeviation>();
        if (criteria == null || !HasScore(r))
            return result;
        var ratio = ExceedRatio(r, criteria);
        var latestScore = r.AppraisalScore ?? r.TotalSelfScore;
        var steps = new (WorkflowStep Step, EvaluationGrade Grade, double Score, string? Explanation)[]
        {
            (WorkflowStep.B2_SELF_SCORE, r.SelfScoredAt.HasValue ? r.SelfProposedGrade : EvaluationGrade.ChuaXepLoai, r.TotalSelfScore, r.SelfGradeExplanation),
            (WorkflowStep.B3A_COLLECTIVE, r.CollectiveProposedGrade, r.TotalSelfScore, r.CollectiveComment),
            (WorkflowStep.B3B_APPRAISAL, r.AppraisalProposedGrade, latestScore, r.AppraisalExplanation),
            (WorkflowStep.B3C_DIRECTOR, r.DirectorProposedGrade, latestScore, r.DirectorComment),
            (WorkflowStep.B4_DECISION, r.FinalGrade, r.FinalScore, r.DecisionExplanation)
        };
        foreach (var (step, grade, score, explanation) in steps)
        {
            var direction = EvaluationScoring.CompareWithScoreGrade(criteria.Content, grade, score, ratio);
            if (direction == 0)
                continue;
            var scoreGrade = EvaluationScoring.SuggestGrade(criteria.Content, score, ratio);
            result.Add(new GradeDeviation(step, grade, scoreGrade, score, direction, !string.IsNullOrWhiteSpace(explanation),
                Message(step, grade, scoreGrade, score, direction)));
        }
        return result;
    }

    /// <summary>Bước cho mức hiệu lực (<see cref="EvaluationRecord.EffectiveGrade"/>); chưa có mức → null.</summary>
    public static WorkflowStep? EffectiveStep(EvaluationRecord r)
    {
        if (r.FinalGrade != EvaluationGrade.ChuaXepLoai)
            return WorkflowStep.B4_DECISION;
        if (r.DirectorProposedGrade != EvaluationGrade.ChuaXepLoai)
            return WorkflowStep.B3C_DIRECTOR;
        if (r.AppraisalProposedGrade != EvaluationGrade.ChuaXepLoai)
            return WorkflowStep.B3B_APPRAISAL;
        if (r.CollectiveProposedGrade != EvaluationGrade.ChuaXepLoai)
            return WorkflowStep.B3A_COLLECTIVE;
        return r.SelfScoredAt.HasValue && r.SelfProposedGrade != EvaluationGrade.ChuaXepLoai ? WorkflowStep.B2_SELF_SCORE : null;
    }

    public static string Label(WorkflowStep step) => step switch
    {
        WorkflowStep.B2_SELF_SCORE => "Mức tự đề xuất",
        WorkflowStep.B3A_COLLECTIVE => "Mức tập thể lãnh đạo đề xuất",
        WorkflowStep.B3B_APPRAISAL => "Mức thẩm định đề xuất",
        WorkflowStep.B3C_DIRECTOR => "Mức cấp trực tiếp sử dụng đề xuất",
        WorkflowStep.B4_DECISION => "Mức được quyết định",
        _ => "Mức đề xuất"
    };

    private static string Message(WorkflowStep step, EvaluationGrade proposed, EvaluationGrade scoreGrade, double score, int direction) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{Label(step)} \"{CriteriaSetContent.GradeName(proposed)}\" {(direction > 0 ? "cao hơn" : "thấp hơn")} mức theo điểm "
            + $"({score:0.##} điểm → \"{CriteriaSetContent.GradeName(scoreGrade)}\")");
}
