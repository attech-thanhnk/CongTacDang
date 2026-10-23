using CongTacDang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CongTacDang.Infrastructure.Data.Configurations;

public sealed class TaskChangeRequestConfiguration : IEntityTypeConfiguration<TaskChangeRequest>
{
    public void Configure(EntityTypeBuilder<TaskChangeRequest> entity)
    {
        entity.ToTable("task_change_requests");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.RecordId, e.Status });
        entity.HasIndex(e => e.Status);
        entity.Property(e => e.Type).HasConversion<int>();
        entity.Property(e => e.Status).HasConversion<int>();
        entity.Property(e => e.TaskNameSnapshot).HasMaxLength(500);
        entity.Property(e => e.Proposal).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();
        entity.Property(e => e.Weights).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();
        entity.Property(e => e.WeightsBefore).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();
        entity.Property(e => e.Reason).HasMaxLength(4000).IsRequired();
        entity.Property(e => e.RequestedByName).HasMaxLength(200).IsRequired();
        entity.Property(e => e.DecidedByName).HasMaxLength(200);
        entity.Property(e => e.DecisionComment).HasMaxLength(4000);
        entity.Property(e => e.Version)
            .HasColumnName("xmin")
            .IsRowVersion();

        entity.HasOne(e => e.Record)
            .WithMany()
            .HasForeignKey(e => e.RecordId)
            .OnDelete(DeleteBehavior.Cascade);
        // Các Id người/việc là tham chiếu mềm (không FK).

        // Áp cùng bộ lọc xóa mềm của hồ sơ.
        entity.HasQueryFilter(e => !e.IsDeleted && !e.Record.IsDeleted);
    }
}
