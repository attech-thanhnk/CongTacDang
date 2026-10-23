using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>Đăng ký sẵn danh mục Mẫu 01 cho các test không xét bước đăng ký, duyệt sản phẩm.</summary>
internal static class RegistrationTestData
{
    /// <summary>
    /// Ghi thẳng ba sản phẩm 30/20/20 đã duyệt cho hồ sơ đang chờ đăng ký, chuyển hồ sơ sang bước tự chấm.
    /// Trả <c>taskScores</c> cùng tỷ lệ A-B-C-D: điểm nhiệm vụ = 70 × <paramref name="ratio"/>.
    /// </summary>
    public static async Task<object[]> RegisterAsync(ApiFactory factory, Guid recordId, double ratio = 1.0)
    {
        var ids = new List<Guid>();
        await factory.WithDbAsync(async db =>
        {
            var record = await db.EvaluationRecords.SingleAsync(r => r.Id == recordId);
            Assert.Equal(RecordStatus.AwaitingRegistration, record.Status);
            var deadline = DateTime.UtcNow.AddDays(30);
            foreach (var (order, weight, kind) in new[] { (1, 30.0, TaskKind.TrongTam), (2, 20.0, TaskKind.TrongTam), (3, 20.0, TaskKind.ThuongXuyen) })
            {
                var task = new EvaluationTask
                {
                    RecordId = recordId, TaskOrder = order, TaskName = $"Sản phẩm {order}", TargetOutput = $"Kết quả {order}", Weight = weight,
                    Deadline = deadline, AxisCode = "T1", Kind = kind, Role = TaskRole.ChuTri, ExceedCriteria = "Vượt tiến độ",
                    EvidenceSource = "Biên bản nghiệm thu", Confirmer = "Giám đốc"
                };
                db.EvaluationTasks.Add(task);
                ids.Add(task.Id);
            }
            record.TasksApprovedAt = DateTime.UtcNow;
            record.TasksApprovedByName = "Kiểm thử";
            record.Status = RecordStatus.AwaitingSelfScore;
            await db.SaveChangesAsync();
        });
        return ids
            .Select(id => (object)new
            {
                taskId = id, criteriaA_Ratio = ratio, criteriaB_Ratio = ratio, criteriaC_Ratio = ratio, criteriaD_Ratio = ratio, isExceedStandard = false
            })
            .ToArray();
    }

    /// <summary>Đánh dấu mọi sản phẩm vượt chuẩn (bỏ qua yêu cầu minh chứng) để mức theo điểm đạt Xuất sắc khi đủ điểm.</summary>
    public static Task MarkExceedAsync(ApiFactory factory, Guid recordId) => factory.WithDbAsync(async db =>
    {
        foreach (var task in await db.EvaluationTasks.Where(t => t.RecordId == recordId).ToListAsync())
            task.IsExceedStandard = true;
        await db.SaveChangesAsync();
    });
}
