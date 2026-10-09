using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>
/// Đề nghị điều chỉnh việc trong quý (Mẫu 04, 05): kiểm tra nội dung, tổng trọng số đúng 70 và số việc sau điều chỉnh (chỉ đếm việc còn
/// hiệu lực), áp đề nghị vào danh mục (điều chỉnh, loại trừ, chuyển tiếp, việc đột xuất).
/// </summary>
public sealed class TaskChangeRulesTests
{
    private static readonly CriteriaSetContent Criteria = CriteriaSetDefaults.Build09A();
    private static readonly DateTime EndDate = new(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    private static List<EvaluationTask> Tasks(params double[] weights)
    {
        var recordId = Guid.NewGuid();
        return weights.Select((w, i) => new EvaluationTask
        {
            RecordId = recordId, TaskOrder = i + 1, TaskName = $"Việc {i + 1}", TargetOutput = "Chuẩn", Weight = w, SelfScore = w,
            AxisCode = "T1", Kind = TaskKind.ThuongXuyen, Role = TaskRole.ChuTri
        }).ToList();
    }

    private static TaskChangeRequest Request(TaskChangeType type, Guid? taskId, TaskChangeProposal proposal, Dictionary<Guid, double> weights,
        Guid recordId) => new()
    {
        RecordId = recordId, Type = type, TaskId = taskId,
        Proposal = TaskChangeRules.ProposalToJson(proposal), Weights = TaskChangeRules.WeightsToJson(weights)
    };

    [Fact]
    public void Adjust_WeightsMustStillTotal70()
    {
        var tasks = Tasks(30, 20, 20);
        var weights = new Dictionary<Guid, double> { [tasks[0].Id] = 25 };
        var error = TaskChangeRules.Validate(TaskChangeType.Adjust, tasks[0].Id, new TaskChangeProposal(), weights, tasks, Criteria);
        Assert.NotNull(error);
        Assert.Contains("70", error);

        weights[tasks[1].Id] = 25;
        Assert.Null(TaskChangeRules.Validate(TaskChangeType.Adjust, tasks[0].Id, new TaskChangeProposal(), weights, tasks, Criteria));
    }

    [Fact]
    public void Adjust_WithoutAnyChange_IsRejected()
    {
        var tasks = Tasks(30, 20, 20);
        var error = TaskChangeRules.Validate(TaskChangeType.Adjust, tasks[0].Id,
            new TaskChangeProposal { TaskName = "Việc 1" }, new Dictionary<Guid, double>(), tasks, Criteria);
        Assert.Contains("chưa có nội dung mới", error);
    }

    [Fact]
    public void Adjust_Apply_UpdatesFieldsAndWeights()
    {
        var tasks = Tasks(30, 20, 20);
        var request = Request(TaskChangeType.Adjust, tasks[0].Id,
            new TaskChangeProposal { TaskName = "Việc 1 mới", TargetOutput = "Chuẩn mới", AxisCode = "t2", Deadline = new DateTime(2026, 11, 15) },
            new Dictionary<Guid, double> { [tasks[0].Id] = 35, [tasks[2].Id] = 15 }, tasks[0].RecordId);

        var (error, created) = TaskChangeRules.Apply(request, tasks, Criteria, EndDate, Criteria.FindFrame("K2"));

        Assert.Null(error);
        Assert.Null(created);
        Assert.Equal("Việc 1 mới", tasks[0].TaskName);
        Assert.Equal("Chuẩn mới", tasks[0].TargetOutput);
        Assert.Equal("T2", tasks[0].AxisCode);
        Assert.Equal(new DateTime(2026, 11, 15), tasks[0].Deadline.Date);
        Assert.Equal(new[] { 35.0, 20, 15 }, tasks.Select(t => t.Weight));
        Assert.Equal(70.0, tasks.Sum(t => t.Weight), 6);
        // Điểm tạm tính lại theo trọng số mới (tỷ lệ 100% → bằng trọng số).
        Assert.Equal(35.0, tasks[0].SelfScore, 6);
    }

    [Fact]
    public void Exclude_KeepsTaskWithZeroWeight_AndCountsOnlyActiveTasks()
    {
        var tasks = Tasks(30, 20, 10, 10);
        var weights = new Dictionary<Guid, double> { [tasks[0].Id] = 35, [tasks[1].Id] = 20, [tasks[2].Id] = 15 };
        var request = Request(TaskChangeType.Exclude, tasks[3].Id, new TaskChangeProposal(), weights, tasks[0].RecordId);

        var (error, _) = TaskChangeRules.Apply(request, tasks, Criteria, EndDate, null);

        Assert.Null(error);
        Assert.Equal(TaskChangeMark.Excluded, tasks[3].ChangeStatus);
        Assert.Equal(0, tasks[3].Weight);
        Assert.Equal(0, tasks[3].SelfScore);
        Assert.False(TaskChangeRules.IsActive(tasks[3]));
        Assert.Equal(70.0, tasks.Where(TaskChangeRules.IsActive).Sum(t => t.Weight), 6);

        // Việc đã loại trừ không đề nghị thay đổi được nữa và không được có trong bảng trọng số.
        Assert.Contains("đã được loại trừ", TaskChangeRules.Validate(TaskChangeType.Adjust, tasks[3].Id, new TaskChangeProposal { TaskName = "x" },
            new Dictionary<Guid, double>(), tasks, Criteria));
        Assert.Contains("không còn hiệu lực", TaskChangeRules.Validate(TaskChangeType.Adjust, tasks[0].Id, new TaskChangeProposal { TaskName = "x" },
            new Dictionary<Guid, double> { [tasks[3].Id] = 5 }, tasks, Criteria));
    }

    [Fact]
    public void Exclude_BelowMinimumTaskCount_IsRejected()
    {
        var tasks = Tasks(30, 20, 20);
        var weights = new Dictionary<Guid, double> { [tasks[0].Id] = 40, [tasks[1].Id] = 30 };
        var error = TaskChangeRules.Validate(TaskChangeType.Carryover, tasks[2].Id, new TaskChangeProposal(), weights, tasks, Criteria);
        Assert.Contains("Số lượng nhiệm vụ", error);
    }

    [Fact]
    public void Carryover_MarksCarriedOver()
    {
        var tasks = Tasks(30, 20, 10, 10);
        var weights = new Dictionary<Guid, double> { [tasks[2].Id] = 20 };
        var request = Request(TaskChangeType.Carryover, tasks[3].Id, new TaskChangeProposal(), weights, tasks[0].RecordId);
        var (error, _) = TaskChangeRules.Apply(request, tasks, Criteria, EndDate, null);
        Assert.Null(error);
        Assert.Equal(TaskChangeMark.CarriedOver, tasks[3].ChangeStatus);
        Assert.Equal(20, tasks[2].Weight);
    }

    [Fact]
    public void Urgent_AddsDotXuatTask_WithinTotal70()
    {
        var tasks = Tasks(30, 20, 20);
        var proposal = new TaskChangeProposal { TaskName = "Việc đột xuất", TargetOutput = "Xong trước 30/11", AxisCode = "T1", Weight = 10 };

        // Cộng thêm mà không giảm việc cũ → vượt 70.
        Assert.Contains("70", TaskChangeRules.Validate(TaskChangeType.Urgent, null, proposal, new Dictionary<Guid, double>(), tasks, Criteria));
        // Thiếu nội dung bắt buộc.
        Assert.Contains("chưa nhập", TaskChangeRules.Validate(TaskChangeType.Urgent, null, new TaskChangeProposal { TaskName = "x" },
            new Dictionary<Guid, double>(), tasks, Criteria));

        var weights = new Dictionary<Guid, double> { [tasks[1].Id] = 15, [tasks[2].Id] = 15 };
        var request = Request(TaskChangeType.Urgent, null, proposal, weights, tasks[0].RecordId);
        var (error, created) = TaskChangeRules.Apply(request, tasks, Criteria, EndDate, null);

        Assert.Null(error);
        Assert.NotNull(created);
        Assert.Equal(TaskKind.DotXuat, created!.Kind);
        Assert.Equal(4, created.TaskOrder);
        Assert.Equal(10, created.Weight);
        Assert.Equal(EndDate, created.Deadline);
        Assert.Equal(70.0, tasks.Sum(t => t.Weight) + created.Weight, 6);
    }

    [Fact]
    public void Urgent_AboveMaxTaskCount_IsRejected()
    {
        var tasks = Tasks(10, 10, 10, 10, 10, 10, 10);
        var weights = tasks.ToDictionary(t => t.Id, _ => 9.0);
        var error = TaskChangeRules.Validate(TaskChangeType.Urgent, null,
            new TaskChangeProposal { TaskName = "x", TargetOutput = "y", AxisCode = "T1", Weight = 7 }, weights, tasks, Criteria);
        Assert.Contains("Số lượng nhiệm vụ", error);
    }

    [Fact]
    public void Apply_WithInvalidRequest_ChangesNothing()
    {
        var tasks = Tasks(30, 20, 20);
        var request = Request(TaskChangeType.Exclude, tasks[2].Id, new TaskChangeProposal(), new Dictionary<Guid, double>(), tasks[0].RecordId);
        var (error, _) = TaskChangeRules.Apply(request, tasks, Criteria, EndDate, null);
        Assert.NotNull(error);
        Assert.Null(tasks[2].ChangeStatus);
        Assert.Equal(new[] { 30.0, 20, 20 }, tasks.Select(t => t.Weight));
    }

    [Fact]
    public void ZeroWeightForActiveTask_IsRejected()
    {
        var tasks = Tasks(30, 20, 10, 10);
        var weights = new Dictionary<Guid, double> { [tasks[0].Id] = 40, [tasks[3].Id] = 0 };
        var error = TaskChangeRules.Validate(TaskChangeType.Adjust, tasks[0].Id, new TaskChangeProposal(), weights, tasks, Criteria);
        Assert.Contains("phải lớn hơn 0", error);
    }

    [Fact]
    public void WeightsJson_RoundTrips()
    {
        var id = Guid.NewGuid();
        var json = TaskChangeRules.WeightsToJson(new Dictionary<Guid, double> { [id] = 12.5 });
        Assert.Equal(12.5, TaskChangeRules.WeightsFromJson(json)[id]);
        Assert.Empty(TaskChangeRules.WeightsFromJson("không phải json"));
    }
}
