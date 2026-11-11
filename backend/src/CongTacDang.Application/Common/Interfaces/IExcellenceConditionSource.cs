using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>Kết quả xét điều kiện Xuất sắc kèm dấu hiệu phiếu đối soát an toàn - chất lượng có khiếm khuyết.</summary>
public sealed record ExcellenceConditionContext(ExcellenceConditionReport Report, bool HasSafetyDefect);

/// <summary>Nạp dữ liệu ngoài hồ sơ (Mẫu 06, kế hoạch Mẫu 17 kỳ trước, chức vụ, Mẫu 03) rồi xét điều kiện.</summary>
public interface IExcellenceConditionSource
{
    /// <summary>Hồ sơ phải nạp sẵn <c>Period</c> và <c>Tasks</c>.</summary>
    Task<ExcellenceConditionContext> LoadAsync(EvaluationRecord record, CancellationToken ct = default);
}
