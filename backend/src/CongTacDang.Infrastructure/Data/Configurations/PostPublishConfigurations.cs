using CongTacDang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CongTacDang.Infrastructure.Data.Configurations;

/// <summary>Kiến nghị sau công bố (task 20 — T-87): nhiều kiến nghị mỗi hồ sơ, xmin.</summary>
public sealed class EvaluationAppealConfiguration : IEntityTypeConfiguration<EvaluationAppeal>
{
    public void Configure(EntityTypeBuilder<EvaluationAppeal> entity)
    {
        entity.ToTable("evaluation_appeals");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.RecordId, e.Status });
        entity.HasIndex(e => e.Status);
        entity.Property(e => e.SubmittedByName).HasMaxLength(200).IsRequired();
        entity.Property(e => e.Content).HasMaxLength(4000).IsRequired();
        // Mảng PostgreSQL (text[] / uuid[]).
        entity.Property(e => e.ConcernedSteps).IsRequired();
        entity.Property(e => e.ConflictedUserIds).IsRequired();
        entity.Property(e => e.ReviewStartedByName).HasMaxLength(200);
        entity.Property(e => e.ResolvedByName).HasMaxLength(200);
        entity.Property(e => e.Response).HasMaxLength(4000);
        entity.Ignore(e => e.IsOpen);
        entity.Property(e => e.Version)
            .HasColumnName("xmin")
            .IsRowVersion();

        entity.HasOne(e => e.Record)
            .WithMany()
            .HasForeignKey(e => e.RecordId)
            .OnDelete(DeleteBehavior.Cascade);
        // SubmittedById / ResolvedById…: tham chiếu mềm (không FK) như người thực hiện các bước trên hồ sơ.
        entity.HasIndex(e => e.SubmittedById);

        // Hồ sơ đánh giá có bộ lọc xóa mềm → áp cùng điều kiện cho bảng con.
        entity.HasQueryFilter(e => !e.Record.IsDeleted);
    }
}

/// <summary>Kế hoạch 30-60-90 ngày — Mẫu 17 (task 20 — T-88): mỗi hồ sơ tối đa một kế hoạch, nội dung jsonb, xmin.</summary>
public sealed class ImprovementPlanConfiguration : IEntityTypeConfiguration<ImprovementPlan>
{
    public void Configure(EntityTypeBuilder<ImprovementPlan> entity)
    {
        entity.ToTable("improvement_plans");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.RecordId).IsUnique();
        entity.HasIndex(e => e.Status);
        entity.Property(e => e.Content).HasColumnType("jsonb").IsRequired();
        entity.Property(e => e.PreparedByName).HasMaxLength(200);
        entity.Property(e => e.ApprovedByName).HasMaxLength(200);
        entity.Property(e => e.AcknowledgementComment).HasMaxLength(2000);
        entity.Property(e => e.Version)
            .HasColumnName("xmin")
            .IsRowVersion();

        entity.HasOne(e => e.Record)
            .WithMany()
            .HasForeignKey(e => e.RecordId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasQueryFilter(e => !e.Record.IsDeleted);
    }
}
