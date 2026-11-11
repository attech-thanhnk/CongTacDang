using System;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Đề xuất của một bước như in trên biểu mẫu; <c>RecordedAt</c> null = bước chưa ghi nhận.</summary>
public readonly record struct StepProposal(EvaluationGrade Grade, DateTime? RecordedAt, string? Comment, Guid? ById, string? ByName)
{
    public static readonly StepProposal None = new(EvaluationGrade.ChuaXepLoai, null, null, null, null);
}

/// <summary>
/// Cột "tập thể lãnh đạo đề xuất" và "cấp trực tiếp sử dụng đề xuất" của biểu mẫu. Diện Trưởng đơn vị đề xuất:
/// đề xuất ghi ở bước 3a là của cấp trực tiếp sử dụng nên in ở cột cấp trực tiếp, cột tập thể để trống.
/// </summary>
public static class ProposalColumns
{
    /// <summary>Cấu hình kỳ lỗi định dạng hoặc chưa có kỳ → coi như diện thường.</summary>
    public static bool IsUnitLeaderProposal(EvaluationRecord record, PeriodSettings? settings) =>
        settings != null && settings.ResolveProfile(record.WorkflowProfileCode, record.ApprovalAuthority).IsUnitLeaderProposal();

    /// <summary>Cấu hình lấy từ <paramref name="period"/>, không có thì từ kỳ nạp kèm hồ sơ.</summary>
    public static bool IsUnitLeaderProposal(EvaluationRecord record, EvaluationPeriod? period = null) =>
        IsUnitLeaderProposal(record, SettingsOrNull(period ?? record.Period));

    public static StepProposal Collective(EvaluationRecord record, bool unitLeaderProposal) =>
        unitLeaderProposal ? StepProposal.None : CollectiveStep(record);

    public static StepProposal DirectSupervisor(EvaluationRecord record, bool unitLeaderProposal) =>
        unitLeaderProposal
            ? CollectiveStep(record)
            : new StepProposal(record.DirectorProposedGrade, record.DirectorReviewedAt, record.DirectorComment,
                record.DirectorReviewedById, record.DirectorReviewedByName);

    private static StepProposal CollectiveStep(EvaluationRecord record) =>
        new(record.CollectiveProposedGrade, record.CollectiveRecordedAt, record.CollectiveComment,
            record.CollectiveRecordedById, record.CollectiveRecordedByName);

    /// <summary>Cấu hình của kỳ; chưa có kỳ hoặc lỗi định dạng → null (biểu mẫu vẫn in được).</summary>
    public static PeriodSettings? SettingsOrNull(EvaluationPeriod? period)
    {
        if (period == null)
            return null;
        try
        {
            return period.GetSettings();
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
