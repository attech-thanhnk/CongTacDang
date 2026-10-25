using CongTacDang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CongTacDang.Infrastructure.Data.Configurations;

public sealed class CalibrationSessionConfiguration : IEntityTypeConfiguration<CalibrationSession>
{
    public void Configure(EntityTypeBuilder<CalibrationSession> entity)
    {
        entity.ToTable("calibration_sessions");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.PeriodId, e.Status });
        entity.HasIndex(e => e.DepartmentId);
        entity.HasIndex(e => e.PartyCellId);
        entity.Property(e => e.Status).HasConversion<int>();
        entity.Property(e => e.GroupName).HasMaxLength(300).IsRequired();
        entity.Property(e => e.Location).HasMaxLength(500);
        entity.Property(e => e.ChairName).HasMaxLength(200).IsRequired();
        entity.Property(e => e.SecretaryName).HasMaxLength(200).IsRequired();
        entity.Property(e => e.ScopeNote).HasMaxLength(4000);
        entity.Property(e => e.ExtraConclusion).HasMaxLength(4000);
        entity.Property(e => e.PreparedByName).HasMaxLength(200).IsRequired();
        entity.Property(e => e.ConcludedByName).HasMaxLength(200);
        entity.Property(e => e.ReopenReason).HasMaxLength(2000);
        entity.Property(e => e.VoidReason).HasMaxLength(2000);
        entity.Property(e => e.Version)
            .HasColumnName("xmin")
            .IsRowVersion();

        entity.HasOne(e => e.Period)
            .WithMany()
            .HasForeignKey(e => e.PeriodId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Department)
            .WithMany()
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(e => e.PartyCell)
            .WithMany()
            .HasForeignKey(e => e.PartyCellId)
            .OnDelete(DeleteBehavior.SetNull);
        // Các Id người (chủ trì, thư ký, người lập, người chốt) là tham chiếu mềm.

        entity.HasQueryFilter(e => !e.IsDeleted && !e.Period.IsDeleted);
    }
}

public sealed class CalibrationRecordConfiguration : IEntityTypeConfiguration<CalibrationRecord>
{
    public void Configure(EntityTypeBuilder<CalibrationRecord> entity)
    {
        entity.ToTable("calibration_records");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.SessionId);
        // Một hồ sơ chỉ thuộc tối đa một biên bản còn hiệu lực.
        entity.HasIndex(e => e.RecordId).IsUnique().HasFilter("\"IsActive\" = true");
        entity.Property(e => e.GradeBefore).HasConversion<int>();

        entity.HasOne(e => e.Session)
            .WithMany(s => s.Records)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Record)
            .WithMany()
            .HasForeignKey(e => e.RecordId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasQueryFilter(e => !e.Session.IsDeleted && !e.Record.IsDeleted);
    }
}

public sealed class CalibrationAttendeeConfiguration : IEntityTypeConfiguration<CalibrationAttendee>
{
    public void Configure(EntityTypeBuilder<CalibrationAttendee> entity)
    {
        entity.ToTable("calibration_attendees");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.SessionId, e.Order });
        entity.Property(e => e.FullName).HasMaxLength(200).IsRequired();
        entity.Property(e => e.Capacity).HasMaxLength(300);
        entity.Property(e => e.DissentNote).HasMaxLength(4000);

        entity.HasOne(e => e.Session)
            .WithMany(s => s.Attendees)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasQueryFilter(e => !e.Session.IsDeleted);
    }
}

public sealed class CalibrationSummaryRowConfiguration : IEntityTypeConfiguration<CalibrationSummaryRow>
{
    public void Configure(EntityTypeBuilder<CalibrationSummaryRow> entity)
    {
        entity.ToTable("calibration_summary_rows");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.SessionId, e.GradeCode }).IsUnique();
        entity.Property(e => e.GradeCode).HasMaxLength(50).IsRequired();
        entity.Property(e => e.NotableDifference).HasMaxLength(4000);
        entity.Property(e => e.PreliminaryConclusion).HasMaxLength(4000);

        entity.HasOne(e => e.Session)
            .WithMany(s => s.SummaryRows)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasQueryFilter(e => !e.Session.IsDeleted);
    }
}

public sealed class CalibrationCaseConfiguration : IEntityTypeConfiguration<CalibrationCase>
{
    public void Configure(EntityTypeBuilder<CalibrationCase> entity)
    {
        entity.ToTable("calibration_cases");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.SessionId, e.Order });
        entity.Property(e => e.GradeBefore).HasConversion<int>();
        entity.Property(e => e.Clarification).HasMaxLength(4000);
        entity.Property(e => e.Basis).HasMaxLength(4000);
        entity.Property(e => e.ResponsibleName).HasMaxLength(200);

        entity.HasOne(e => e.Session)
            .WithMany(s => s.Cases)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        // RecordId, ResponsibleMemberId là tham chiếu mềm.

        entity.HasQueryFilter(e => !e.Session.IsDeleted);
    }
}
