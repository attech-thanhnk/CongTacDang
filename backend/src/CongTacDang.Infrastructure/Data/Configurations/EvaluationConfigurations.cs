using CongTacDang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CongTacDang.Infrastructure.Data.Configurations;

/// <summary>Kỳ đánh giá (task 12: thêm cấu hình jsonb, bỏ cờ IsActive — kỳ hiện hành = kỳ Open/Locked mới nhất).</summary>
public sealed class EvaluationPeriodConfiguration : IEntityTypeConfiguration<EvaluationPeriod>
{
    public void Configure(EntityTypeBuilder<EvaluationPeriod> entity)
    {
        entity.ToTable("evaluation_periods");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
        entity.Property(e => e.Settings).HasColumnType("jsonb").IsRequired();
        entity.Property(e => e.StatusReason).HasMaxLength(1000);
        entity.HasIndex(e => new { e.Year, e.Quarter });
        entity.HasIndex(e => e.Status);
        entity.Property(e => e.Version)
            .HasColumnName("xmin")
            .IsRowVersion();

        entity.HasMany(p => p.Records)
            .WithOne(r => r.Period)
            .HasForeignKey(r => r.PeriodId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>Hồ sơ đánh giá cá nhân.</summary>
public sealed class EvaluationRecordConfiguration : IEntityTypeConfiguration<EvaluationRecord>
{
    public void Configure(EntityTypeBuilder<EvaluationRecord> entity)
    {
        entity.ToTable("evaluation_records");
        entity.HasKey(e => e.Id);

        // 1 Cán bộ chỉ có 1 hồ sơ trong 1 kỳ đánh giá (kể cả bản ghi đã xóa mềm — thêm lại thì khôi phục).
        entity.HasIndex(e => new { e.PeriodId, e.MemberId }).IsUnique();
        entity.HasIndex(e => e.MemberId);
        entity.HasIndex(e => e.PartyCellId);
        entity.HasIndex(e => e.DepartmentId);
        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => new { e.PeriodId, e.Status });
        entity.Property(e => e.Version)
            .HasColumnName("xmin")
            .IsRowVersion();

        entity.Property(e => e.ReturnReason).HasMaxLength(2000);
        entity.Property(e => e.SelfScoreForm).HasMaxLength(10);
        entity.Property(e => e.TasksApprovedByName).HasMaxLength(200);
        entity.Property(e => e.TasksApprovalComment).HasMaxLength(4000);
        entity.Property(e => e.CellConfirmedByName).HasMaxLength(200);
        entity.Property(e => e.CollectiveComment).HasMaxLength(4000);
        entity.Property(e => e.CollectiveRecordedByName).HasMaxLength(200);
        entity.Property(e => e.AppraisedByName).HasMaxLength(200);
        entity.Property(e => e.DirectorComment).HasMaxLength(4000);
        entity.Property(e => e.DirectorReviewedByName).HasMaxLength(200);
        entity.Property(e => e.DecisionDocumentNumber).HasMaxLength(100);
        entity.Property(e => e.DecisionAuthorityName).HasMaxLength(300);
        entity.Property(e => e.DecisionRecordedByName).HasMaxLength(200);
        entity.Property(e => e.PublishedByName).HasMaxLength(200);

        entity.HasOne(r => r.Member)
            .WithMany()
            .HasForeignKey(r => r.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(r => r.PartyCell)
            .WithMany()
            .HasForeignKey(r => r.PartyCellId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(r => r.Department)
            .WithMany()
            .HasForeignKey(r => r.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasMany(r => r.Tasks)
            .WithOne(t => t.Record)
            .HasForeignKey(t => t.RecordId)
            .OnDelete(DeleteBehavior.Cascade);

        // CollectiveMeetingId / DecisionMeetingId: tham chiếu mềm tới evaluation_meetings (không FK, tránh vòng khóa ngoại
        // records ↔ meetings qua vote summaries).

        entity.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>Công việc / sản phẩm của hồ sơ.</summary>
public sealed class EvaluationTaskConfiguration : IEntityTypeConfiguration<EvaluationTask>
{
    public void Configure(EntityTypeBuilder<EvaluationTask> entity)
    {
        entity.ToTable("evaluation_tasks");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.RecordId, e.TaskOrder });
        entity.HasIndex(e => e.AttachmentId);
        entity.Property(e => e.TaskName).HasMaxLength(500).IsRequired();
        entity.Property(e => e.Version)
            .HasColumnName("xmin")
            .IsRowVersion();

        entity.HasOne(t => t.Attachment)
            .WithMany()
            .HasForeignKey(t => t.AttachmentId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>Lịch sử chuyển trạng thái hồ sơ (task 12: thêm Step, Action, Reason, ảnh chụp điểm/mức).</summary>
public sealed class EvaluationRecordHistoryConfiguration : IEntityTypeConfiguration<EvaluationRecordHistory>
{
    public void Configure(EntityTypeBuilder<EvaluationRecordHistory> entity)
    {
        entity.ToTable("evaluation_record_histories");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.RecordId, e.CreatedAt });
        entity.Property(e => e.Reason).HasMaxLength(2000);
        entity.HasOne(e => e.Record)
            .WithMany()
            .HasForeignKey(e => e.RecordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Hồ sơ tự đánh giá tập thể (Mẫu 06–08).</summary>
public sealed class CollectiveEvaluationRecordConfiguration : IEntityTypeConfiguration<CollectiveEvaluationRecord>
{
    public void Configure(EntityTypeBuilder<CollectiveEvaluationRecord> entity)
    {
        entity.ToTable("collective_evaluation_records");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.PeriodId, e.Form, e.PartyCellId, e.DepartmentId }).IsUnique();
        entity.HasIndex(e => new { e.PeriodId, e.Status });
        entity.HasIndex(e => e.PartyCellId);
        entity.HasIndex(e => e.DepartmentId);
        entity.Property(e => e.SubjectName).HasMaxLength(300).IsRequired();
        entity.Property(e => e.Form).HasConversion<int>();
        entity.Property(e => e.Version)
            .HasColumnName("xmin")
            .IsRowVersion();
        entity.HasOne(e => e.Period)
            .WithMany(p => p.CollectiveRecords)
            .HasForeignKey(e => e.PeriodId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.PartyCell)
            .WithMany()
            .HasForeignKey(e => e.PartyCellId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(e => e.Department)
            .WithMany()
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(e => e.Head)
            .WithMany()
            .HasForeignKey(e => e.HeadId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasMany(e => e.Items)
            .WithOne(i => i.CollectiveRecord)
            .HasForeignKey(i => i.CollectiveRecordId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>Dòng nội dung của hồ sơ tập thể.</summary>
public sealed class CollectiveEvaluationItemConfiguration : IEntityTypeConfiguration<CollectiveEvaluationItem>
{
    public void Configure(EntityTypeBuilder<CollectiveEvaluationItem> entity)
    {
        entity.ToTable("collective_evaluation_items");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Category).HasMaxLength(200);
        entity.Property(e => e.TaskName).HasMaxLength(500);
    }
}

/// <summary>Biên bản hội nghị / kiểm phiếu (task 12: thêm DepartmentId, Stage).</summary>
public sealed class EvaluationMeetingConfiguration : IEntityTypeConfiguration<EvaluationMeeting>
{
    public void Configure(EntityTypeBuilder<EvaluationMeeting> entity)
    {
        entity.ToTable("evaluation_meetings");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.PeriodId, e.PartyCellId, e.FormCode });
        entity.HasIndex(e => new { e.PeriodId, e.DepartmentId });
        entity.Property(e => e.Version)
            .HasColumnName("xmin")
            .IsRowVersion();
        entity.Property(e => e.FormCode).HasMaxLength(10).IsRequired();
        entity.Property(e => e.MeetingType).HasMaxLength(200);
        entity.Property(e => e.Location).HasMaxLength(300);
        entity.HasOne(e => e.Period)
            .WithMany(p => p.Meetings)
            .HasForeignKey(e => e.PeriodId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.PartyCell)
            .WithMany()
            .HasForeignKey(e => e.PartyCellId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(e => e.Department)
            .WithMany()
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasMany(e => e.VoteSummaries)
            .WithOne(v => v.Meeting)
            .HasForeignKey(v => v.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>Kết quả kiểm phiếu tổng hợp theo hồ sơ (không có thông tin người bỏ phiếu).</summary>
public sealed class EvaluationMeetingVoteSummaryConfiguration : IEntityTypeConfiguration<EvaluationMeetingVoteSummary>
{
    public void Configure(EntityTypeBuilder<EvaluationMeetingVoteSummary> entity)
    {
        entity.ToTable("evaluation_meeting_vote_summaries");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.MeetingId, e.RecordId }).IsUnique();
        entity.Ignore(e => e.TotalBallots);
        entity.HasOne(e => e.Record)
            .WithMany()
            .HasForeignKey(e => e.RecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
