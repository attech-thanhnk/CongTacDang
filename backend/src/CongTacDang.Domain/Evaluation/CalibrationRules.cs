using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Một dòng tổng hợp theo mức của biên bản; số liệu tính từ hồ sơ.</summary>
public sealed record CalibrationSummaryLine(
    EvaluationGrade Grade, int CountBefore, double? PercentBefore, int CountAfter, double? PercentAfter);

/// <summary>Quy tắc thuần của biên bản hiệu chuẩn (Mẫu 20).</summary>
public static class CalibrationRules
{
    public const int MaxTextLength = 4000;

    public const int MaxNameLength = 200;

    public const int MaxAttendees = 100;

    /// <summary>Các mức có dòng trong bảng tổng hợp, theo thứ tự in.</summary>
    public static readonly IReadOnlyList<EvaluationGrade> SummaryGrades = new[]
    {
        EvaluationGrade.HoanThanhXuatSac, EvaluationGrade.HoanThanhTot, EvaluationGrade.HoanThanh, EvaluationGrade.KhongHoanThanh
    };

    /// <summary>Hồ sơ đã thẩm định và chưa ghi nhận quyết định.</summary>
    public static bool IsEligibleStatus(RecordStatus status) =>
        status is RecordStatus.AwaitingDirectorReview or RecordStatus.AwaitingDecision;

    /// <summary>Đã có kết quả thẩm định (điểm hoặc mức), kể cả do cấp trên ghi nhận.</summary>
    public static bool HasAppraisal(EvaluationRecord record) =>
        record.AppraisalScore.HasValue || record.AppraisalProposedGrade != EvaluationGrade.ChuaXepLoai;

    /// <summary>
    /// Mốc trước hiệu chuẩn: kết quả thẩm định (hoặc cấp trên ghi nhận), không có thì đề xuất tập thể, rồi tự chấm.
    /// Không xét giá trị sau hiệu chuẩn nên mở lại biên bản vẫn ra cùng mốc.
    /// </summary>
    public static (EvaluationGrade Grade, double? Score) Before(EvaluationRecord record)
    {
        var grade = record.AppraisalProposedGrade != EvaluationGrade.ChuaXepLoai ? record.AppraisalProposedGrade
            : record.CollectiveProposedGrade != EvaluationGrade.ChuaXepLoai ? record.CollectiveProposedGrade
            : record.SelfScoredAt.HasValue ? record.SelfProposedGrade
            : EvaluationGrade.ChuaXepLoai;
        var score = record.AppraisalScore ?? (record.SelfScoredAt.HasValue ? record.TotalSelfScore : (double?)null);
        return (grade, score);
    }

    public static bool SameScore(double? a, double? b) =>
        a.HasValue == b.HasValue && (!a.HasValue || Math.Abs(a.Value - b!.Value) < CriteriaSetContent.Epsilon);

    /// <summary>Điểm hoặc mức sau hiệu chuẩn khác mốc trước.</summary>
    public static bool IsChanged(CalibrationCase c) =>
        (c.GradeAfter.HasValue && c.GradeAfter != c.GradeBefore)
        || (c.ScoreAfter.HasValue && !SameScore(c.ScoreAfter, c.ScoreBefore));

    /// <summary>Mức sau hiệu chuẩn của trường hợp; không đổi → mức trước.</summary>
    public static EvaluationGrade GradeAfterOf(CalibrationCase c) => c.GradeAfter ?? c.GradeBefore;

    public static double? ScoreAfterOf(CalibrationCase c) => c.ScoreAfter ?? c.ScoreBefore;

    /// <summary>Phần trăm làm tròn một chữ số thập phân; tổng 0 → null.</summary>
    public static double? Percent(int count, int total) =>
        total > 0 ? Math.Round(count * 100.0 / total, 1, MidpointRounding.AwayFromZero) : null;

    /// <summary>Số lượng và tỷ lệ theo mức, trước và sau hiệu chuẩn; chỉ là số liệu, không so với chỉ tiêu nào.</summary>
    public static List<CalibrationSummaryLine> Summarize(IReadOnlyCollection<CalibrationRecord> records, IReadOnlyCollection<CalibrationCase> cases)
    {
        var afterByRecord = cases.GroupBy(c => c.RecordId).ToDictionary(g => g.Key, g => GradeAfterOf(g.First()));
        var total = records.Count;
        var lines = new List<CalibrationSummaryLine>();
        foreach (var grade in SummaryGrades)
        {
            var before = records.Count(r => r.GradeBefore == grade);
            var after = records.Count(r => (afterByRecord.TryGetValue(r.RecordId, out var g) ? g : r.GradeBefore) == grade);
            lines.Add(new CalibrationSummaryLine(grade, before, Percent(before, total), after, Percent(after, total)));
        }
        return lines;
    }

    /// <summary>Các lỗi khiến chưa chốt được biên bản (rỗng = đủ điều kiện về nội dung).</summary>
    public static List<string> ConclusionProblems(CalibrationSession session)
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(session.GroupName))
            problems.Add("Chưa nhập tên nhóm chức danh/công việc tương đồng.");
        if (session.HeldAt is null)
            problems.Add("Chưa nhập thời gian họp.");
        if (string.IsNullOrWhiteSpace(session.ChairName))
            problems.Add("Chưa nhập chủ trì.");
        if (string.IsNullOrWhiteSpace(session.SecretaryName))
            problems.Add("Chưa nhập thư ký.");
        if (session.Attendees.Count == 0)
            problems.Add("Chưa nhập thành phần tham dự.");
        if (session.Records.Count == 0)
            problems.Add("Biên bản chưa có hồ sơ nào.");
        else if (session.Records.Count == 1 && string.IsNullOrWhiteSpace(session.ExtraConclusion))
            problems.Add("Biên bản chỉ có một hồ sơ: hãy ghi lý do không có nhóm tương đồng ở phần ghi chú kết luận.");

        foreach (var c in session.Cases.OrderBy(c => c.Order))
        {
            if (string.IsNullOrWhiteSpace(c.Clarification))
                problems.Add($"Trường hợp {c.Order}: chưa nhập nội dung làm rõ.");
            if (IsChanged(c) && string.IsNullOrWhiteSpace(c.Basis))
                problems.Add($"Trường hợp {c.Order}: điều chỉnh kết quả phải nêu căn cứ.");
            if (IsChanged(c) && string.IsNullOrWhiteSpace(c.ResponsibleName))
                problems.Add($"Trường hợp {c.Order}: điều chỉnh kết quả phải ghi người chịu trách nhiệm.");
        }
        return problems;
    }

    /// <summary>
    /// Phiếu/biên bản đã chốt hết hiệu lực khi hồ sơ có thay đổi bước, điểm hoặc mức sau lúc chốt;
    /// việc áp dụng của chính biên bản được ghi cùng thời điểm chốt nên không tính.
    /// </summary>
    public static bool IsStale(DateTime concludedAt, IEnumerable<EvaluationRecordHistory> recordHistory) =>
        recordHistory.Any(h => h.CreatedAt > concludedAt
            && h.Action is WorkflowAction.Complete or WorkflowAction.Return or WorkflowAction.Reopen
                or WorkflowAction.RecordExternal or WorkflowAction.Calibrate);
}
