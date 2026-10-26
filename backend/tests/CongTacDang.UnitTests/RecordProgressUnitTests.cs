using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Tiến trình hồ sơ: chủ thể từng bước và người/thời điểm hoàn thành bước đã xong.</summary>
public sealed class RecordProgressUnitTests
{
    private static readonly DateOnly Today = new(2026, 12, 1);

    private static EvaluationRecord Record(RecordStatus status, ApprovalAuthority authority = ApprovalAuthority.CoSo) => new()
    {
        Status = status,
        ApprovalAuthority = authority,
        Member = new PartyMemberProfile { FullName = "Nguyễn Văn A" },
        Department = new AdministrativeDepartment { Name = "Phòng Kỹ thuật" },
        PartyCell = new PartyCell { Name = "Chi bộ Kỹ thuật" }
    };

    private static WorkflowProfile Profile(string code) => PeriodSettings.Default().FindProfile(code)!;

    [Fact]
    public void Actor_InternalProfile_DescribesStepOwnerFromRecordData()
    {
        var record = Record(RecordStatus.AwaitingSelfScore);
        var profile = Profile("co-so");

        Assert.Equal("Nguyễn Văn A", EvaluationMapping.StepActor(WorkflowStep.B1_REGISTER, profile, record));
        Assert.Equal("Nguyễn Văn A", EvaluationMapping.StepActor(WorkflowStep.B2_SELF_SCORE, profile, record));
        Assert.Equal("Lãnh đạo Phòng Kỹ thuật", EvaluationMapping.StepActor(WorkflowStep.B1_APPROVE, profile, record));
        Assert.Equal("Chi bộ Kỹ thuật", EvaluationMapping.StepActor(WorkflowStep.B2_CELL_CONFIRM, profile, record));
        Assert.Equal("Tập thể lãnh đạo Phòng Kỹ thuật", EvaluationMapping.StepActor(WorkflowStep.B3A_COLLECTIVE, profile, record));
        Assert.Equal("Cơ quan thẩm định", EvaluationMapping.StepActor(WorkflowStep.B3B_APPRAISAL, profile, record));
        Assert.Equal("Cấp trực tiếp sử dụng", EvaluationMapping.StepActor(WorkflowStep.B3C_DIRECTOR, profile, record));
        Assert.Equal("Đảng ủy cơ sở", EvaluationMapping.StepActor(WorkflowStep.B4_DECISION, profile, record));
        Assert.Equal("Cơ quan công bố kết quả", EvaluationMapping.StepActor(WorkflowStep.B5_PUBLISH, profile, record));
    }

    [Fact]
    public void Actor_ExternalStep_IsUpperLevel_AndUnitReviewIsUnitLeaders()
    {
        var record = Record(RecordStatus.AwaitingAppraisal, ApprovalAuthority.CapTren);
        var upper = Profile("cap-tren");
        Assert.Equal("Cấp trên thực hiện", EvaluationMapping.StepActor(WorkflowStep.B3B_APPRAISAL, upper, record));
        Assert.Equal("Cấp trên thực hiện", EvaluationMapping.StepActor(WorkflowStep.B4_DECISION, upper, record));

        // Diện quyết định nội bộ nhưng cấp quyết định là cấp trên.
        Assert.Equal("Cấp trên có thẩm quyền quyết định", EvaluationMapping.StepActor(WorkflowStep.B4_DECISION, Profile("co-so"), record));
        // Ví dụ 3: B3c do lãnh đạo đơn vị thực hiện.
        Assert.Equal("Lãnh đạo Phòng Kỹ thuật", EvaluationMapping.StepActor(WorkflowStep.B3C_DIRECTOR, Profile("bi-thu-nhan-vien"), record));
    }

    [Fact]
    public void Actor_PartyCellNameWithoutPrefix_GetsPrefix_MissingDataFallsBack()
    {
        var record = Record(RecordStatus.AwaitingCellConfirm);
        record.PartyCell = new PartyCell { Name = "Kỹ thuật 2" };
        record.Department = null;
        var profile = Profile("co-so");
        Assert.Equal("Chi bộ Kỹ thuật 2", EvaluationMapping.StepActor(WorkflowStep.B2_CELL_CONFIRM, profile, record));
        Assert.Equal("Tập thể lãnh đạo đơn vị", EvaluationMapping.StepActor(WorkflowStep.B3A_COLLECTIVE, profile, record));

        record.PartyCell = null;
        Assert.Equal("Chi bộ", EvaluationMapping.StepActor(WorkflowStep.B2_CELL_CONFIRM, profile, record));
    }

    [Fact]
    public void Progress_DoneSteps_CarryCompletion_OthersDoNot()
    {
        var record = Record(RecordStatus.AwaitingCollective);
        var selfAt = new DateTime(2026, 11, 20, 2, 30, 0, DateTimeKind.Utc);
        var cellAt = new DateTime(2026, 11, 22, 8, 0, 0, DateTimeKind.Utc);
        record.SelfScoredAt = selfAt;
        record.CellConfirmedAt = cellAt;
        record.CellConfirmedByName = "Bí thư Chi bộ";
        // Dữ liệu cũ của bước chưa tới (sau khi mở lại) không được hiện như đã xong.
        record.AppraisedAt = selfAt;
        record.AppraisedByName = "Người cũ";

        var progress = EvaluationMapping.Progress(record, Profile("co-so"), Today);
        var self = progress.Single(p => p.Step == "B2_SELF_SCORE");
        var cell = progress.Single(p => p.Step == "B2_CELL_CONFIRM");
        var collective = progress.Single(p => p.Step == "B3A_COLLECTIVE");
        var appraisal = progress.Single(p => p.Step == "B3B_APPRAISAL");

        Assert.Equal(("done", selfAt, "Nguyễn Văn A"), (self.State, self.DoneAt, self.DoneByName));
        Assert.Equal(("done", cellAt, "Bí thư Chi bộ"), (cell.State, cell.DoneAt, cell.DoneByName));
        Assert.Equal("current", collective.State);
        Assert.Equal("Tập thể lãnh đạo Phòng Kỹ thuật", collective.Actor);
        Assert.Null(collective.DoneAt);
        Assert.Equal("pending", appraisal.State);
        Assert.Null(appraisal.DoneAt);
        Assert.Null(appraisal.DoneByName);
        Assert.All(progress.Where(p => p.Enabled), p => Assert.False(string.IsNullOrEmpty(p.Actor)));
    }

    [Fact]
    public void Progress_ExternalDoneStep_UsesRecordedResult()
    {
        var record = Record(RecordStatus.AwaitingDirectorReview, ApprovalAuthority.CapTren);
        var recordedAt = new DateTime(2026, 11, 25, 3, 0, 0, DateTimeKind.Utc);
        record.AppraisedByName = "Tên chép từ kết quả";
        record.ExternalResults.Add(new EvaluationExternalResult
        {
            Step = WorkflowStep.B3B_APPRAISAL,
            AuthorityName = "Ban Tổ chức cấp trên",
            RecordedByName = "Văn phòng",
            RecordedAt = recordedAt
        });

        var appraisal = EvaluationMapping.Progress(record, Profile("cap-tren"), Today).Single(p => p.Step == "B3B_APPRAISAL");
        Assert.Equal("done", appraisal.State);
        Assert.Equal("External", appraisal.Mode);
        Assert.Equal("Cấp trên thực hiện", appraisal.Actor);
        Assert.Equal(recordedAt, appraisal.DoneAt);
        Assert.Equal("Ban Tổ chức cấp trên", appraisal.DoneByName);
    }

    [Fact]
    public void Progress_Published_AllActiveStepsDone_PublishCompletion()
    {
        var record = Record(RecordStatus.Published);
        var publishedAt = new DateTime(2026, 12, 20, 1, 0, 0, DateTimeKind.Utc);
        record.PublishedAt = publishedAt;
        record.PublishedByName = "Văn phòng Đảng ủy";

        var progress = EvaluationMapping.Progress(record, Profile("co-so"), Today);
        Assert.All(progress, p => Assert.Equal("done", p.State));
        var publish = progress.Single(p => p.Step == "B5_PUBLISH");
        Assert.Equal((publishedAt, "Văn phòng Đảng ủy"), (publish.DoneAt, publish.DoneByName));
        // Bước đăng ký không lưu thời điểm riêng.
        Assert.Null(progress.Single(p => p.Step == "B1_REGISTER").DoneAt);
    }

    [Fact]
    public void DeadlineDistance_OverdueDueTodayFutureAndNoDeadline()
    {
        Assert.Equal((3, (int?)null), EvaluationMapping.DeadlineDistance(Today.AddDays(-3), Today));
        Assert.Equal((1, (int?)null), EvaluationMapping.DeadlineDistance(Today.AddDays(-1), Today));
        Assert.Equal(((int?)null, 0), EvaluationMapping.DeadlineDistance(Today, Today));
        Assert.Equal(((int?)null, 5), EvaluationMapping.DeadlineDistance(Today.AddDays(5), Today));
        Assert.Equal(((int?)null, (int?)null), EvaluationMapping.DeadlineDistance(null, Today));
    }

    [Fact]
    public void Today_UsesVietnamTime()
    {
        // 18:00 UTC ngày 30/11 là 01:00 ngày 01/12 giờ Việt Nam.
        Assert.Equal(new DateOnly(2026, 12, 1), EvaluationMapping.Today(new DateTime(2026, 11, 30, 18, 0, 0, DateTimeKind.Utc)));
    }
}
