using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Tỷ trọng 4 tiêu chí A-B-C-D (tỷ lệ 0..1, tổng = 1).</summary>
public sealed class CriteriaWeights
{
    /// <summary>Khởi tạo rỗng (dùng cho JSON).</summary>
    public CriteriaWeights()
    {
    }

    /// <summary>Khởi tạo với giá trị cho trước.</summary>
    public CriteriaWeights(double a, double b, double c, double d)
    {
        A = a;
        B = b;
        C = c;
        D = d;
    }

    /// <summary>Tiêu chí A — khối lượng.</summary>
    public double A { get; set; }

    /// <summary>Tiêu chí B — chất lượng.</summary>
    public double B { get; set; }

    /// <summary>Tiêu chí C — tiến độ.</summary>
    public double C { get; set; }

    /// <summary>Tiêu chí D — hiệu quả, sáng kiến.</summary>
    public double D { get; set; }
}

/// <summary>
/// Tham số nghiệp vụ của kỳ (<c>PeriodSettings.parameters</c>). <b>Giá trị mặc định = đúng hằng số code đang dùng trước task 12</b>
/// (nguồn gốc từng giá trị ghi trong báo cáo task 12); đổi tham số không đổi công thức.
/// </summary>
public sealed class EvaluationParameters
{
    /// <summary>Số sản phẩm/nhiệm vụ tối thiểu khi đăng ký (Mẫu 01). Gốc: EvaluationService.RegisterTasksAsync.</summary>
    public int MinTasks { get; set; } = 3;

    /// <summary>Số sản phẩm/nhiệm vụ tối đa khi đăng ký (Mẫu 01).</summary>
    public int MaxTasks { get; set; } = 7;

    /// <summary>Tổng trọng số bắt buộc của danh mục sản phẩm.</summary>
    public double TotalTaskWeight { get; set; } = 70.0;

    /// <summary>Sai số cho phép khi so tổng trọng số (sau khi làm tròn 2 chữ số).</summary>
    public double TaskWeightTolerance { get; set; } = 0.05;

    /// <summary>Điểm tối đa của mỗi tiêu chí chung T1..T6 (Mẫu 09).</summary>
    public double GeneralCriterionMaxScore { get; set; } = 5.0;

    /// <summary>Tỷ trọng A-B-C-D theo khung chức danh (khóa = tên <see cref="JobGroup"/>).</summary>
    public Dictionary<string, CriteriaWeights> JobGroupWeights { get; set; } = DefaultJobGroupWeights();

    /// <summary>Tỷ trọng dùng khi khung chức danh không có trong bảng.</summary>
    public CriteriaWeights FallbackWeights { get; set; } = new(0.25, 0.25, 0.25, 0.25);

    /// <summary>Điểm tối thiểu gợi ý mức Hoàn thành xuất sắc.</summary>
    public double ExcellentMinScore { get; set; } = 90.0;

    /// <summary>Điểm tối thiểu gợi ý mức Hoàn thành tốt.</summary>
    public double GoodMinScore { get; set; } = 70.0;

    /// <summary>Điểm tối thiểu gợi ý mức Hoàn thành.</summary>
    public double SatisfactoryMinScore { get; set; } = 50.0;

    /// <summary>Trần tỷ lệ Hoàn thành xuất sắc trên số Hoàn thành tốt trở lên (làm tròn xuống).</summary>
    public double ExcellentQuotaRatio { get; set; } = 0.20;

    /// <summary>Điểm tối đa nhóm tiêu chí chung của hồ sơ tập thể (Mẫu 06–08).</summary>
    public double CollectiveGeneralMaxScore { get; set; } = 30.0;

    /// <summary>Điểm tối đa nhóm kết quả thực hiện nhiệm vụ của hồ sơ tập thể.</summary>
    public double CollectiveTaskMaxScore { get; set; } = 70.0;

    /// <summary>
    /// Điểm tối đa 6 trục khi tự chấm theo Mẫu 09B (Q3/2026). <b>Mới</b> ở task 12 (code cũ chưa có 09B) — giá trị theo
    /// bản trích xuất HD03 mục 8.3, chờ nghiệp vụ xác nhận.
    /// </summary>
    public double[] AxisMaxScores { get; set; } = { 15.0, 10.0, 10.0, 15.0, 10.0, 10.0 };

    /// <summary>Bảng tỷ trọng mặc định theo khung chức danh.</summary>
    public static Dictionary<string, CriteriaWeights> DefaultJobGroupWeights() => new()
    {
        [nameof(JobGroup.Khung1_QuanLyDangDoanThe)] = new CriteriaWeights(0.25, 0.35, 0.20, 0.20),
        [nameof(JobGroup.Khung2_AnToanKyThuat)] = new CriteriaWeights(0.15, 0.50, 0.15, 0.20),
        [nameof(JobGroup.Khung3_DuAnDauTu)] = new CriteriaWeights(0.20, 0.30, 0.35, 0.15),
        [nameof(JobGroup.Khung4_KhcnChuyenDoiSo)] = new CriteriaWeights(0.15, 0.30, 0.20, 0.35)
    };

    /// <summary>Tỷ trọng của khung chức danh.</summary>
    public CriteriaWeights WeightsFor(JobGroup group) =>
        JobGroupWeights != null && JobGroupWeights.TryGetValue(group.ToString(), out var weights) && weights != null
            ? weights
            : FallbackWeights ?? new CriteriaWeights(0.25, 0.25, 0.25, 0.25);

    /// <summary>Kiểm tra tham số hợp lệ; trả danh sách lỗi (rỗng = hợp lệ).</summary>
    public List<string> Validate()
    {
        var errors = new List<string>();
        if (MinTasks < 1)
            errors.Add("Số sản phẩm tối thiểu phải từ 1 trở lên.");
        if (MaxTasks < MinTasks)
            errors.Add("Số sản phẩm tối đa phải lớn hơn hoặc bằng số tối thiểu.");
        if (TotalTaskWeight <= 0)
            errors.Add("Tổng trọng số sản phẩm phải lớn hơn 0.");
        if (TaskWeightTolerance < 0)
            errors.Add("Sai số tổng trọng số không được âm.");
        if (GeneralCriterionMaxScore <= 0)
            errors.Add("Điểm tối đa mỗi tiêu chí chung phải lớn hơn 0.");
        if (!(ExcellentMinScore > GoodMinScore && GoodMinScore > SatisfactoryMinScore && SatisfactoryMinScore >= 0))
            errors.Add("Ngưỡng điểm phải giảm dần: Xuất sắc > Tốt > Hoàn thành ≥ 0.");
        if (ExcellentQuotaRatio < 0 || ExcellentQuotaRatio > 1)
            errors.Add("Trần tỷ lệ Hoàn thành xuất sắc phải trong khoảng 0–1.");
        if (CollectiveGeneralMaxScore <= 0 || CollectiveTaskMaxScore <= 0)
            errors.Add("Điểm tối đa của hồ sơ tập thể phải lớn hơn 0.");

        foreach (var group in Enum.GetValues<JobGroup>())
        {
            if (JobGroupWeights == null || !JobGroupWeights.TryGetValue(group.ToString(), out var w) || w == null)
            {
                errors.Add($"Thiếu tỷ trọng A-B-C-D của khung chức danh {group}.");
                continue;
            }
            if (new[] { w.A, w.B, w.C, w.D }.Any(x => x < 0) || Math.Abs(w.A + w.B + w.C + w.D - 1.0) > 1e-6)
                errors.Add($"Tỷ trọng A-B-C-D của khung {group} phải không âm và có tổng bằng 1.");
        }

        if (JobGroupWeights != null)
        {
            foreach (var key in JobGroupWeights.Keys.Where(k => !Enum.TryParse<JobGroup>(k, out _)))
                errors.Add($"Khung chức danh \"{key}\" không tồn tại.");
        }

        if (AxisMaxScores == null || AxisMaxScores.Length != 6 || AxisMaxScores.Any(x => x <= 0))
            errors.Add("Điểm tối đa 6 trục (Mẫu 09B) phải gồm đúng 6 giá trị lớn hơn 0.");

        return errors;
    }
}
