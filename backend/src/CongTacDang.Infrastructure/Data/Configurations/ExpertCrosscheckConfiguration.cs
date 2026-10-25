using CongTacDang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CongTacDang.Infrastructure.Data.Configurations;

public sealed class ExpertCrosscheckConfiguration : IEntityTypeConfiguration<ExpertCrosscheck>
{
    public void Configure(EntityTypeBuilder<ExpertCrosscheck> entity)
    {
        entity.ToTable("expert_crosschecks");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.RecordId, e.Status });
        entity.HasIndex(e => new { e.ExpertDepartmentId, e.Status });
        entity.HasIndex(e => e.PeriodId);
        entity.Property(e => e.Status).HasConversion<int>();
        entity.Property(e => e.ExpertAgencyName).HasMaxLength(500);
        entity.Property(e => e.Fields).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb").IsRequired();
        entity.Property(e => e.AddresseeName).HasMaxLength(500).IsRequired();
        entity.Property(e => e.RequestReason).HasMaxLength(4000).IsRequired();
        entity.Property(e => e.RequestedByName).HasMaxLength(200).IsRequired();
        entity.Property(e => e.SubmittedByName).HasMaxLength(200);
        entity.Property(e => e.ConfirmedByName).HasMaxLength(200);
        entity.Property(e => e.CancelledByName).HasMaxLength(200);
        entity.Property(e => e.ReturnReason).HasMaxLength(2000);
        entity.Property(e => e.CancelReason).HasMaxLength(2000);
        entity.Property(e => e.Version)
            .HasColumnName("xmin")
            .IsRowVersion();

        entity.HasOne(e => e.Record)
            .WithMany()
            .HasForeignKey(e => e.RecordId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.ExpertDepartment)
            .WithMany()
            .HasForeignKey(e => e.ExpertDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasMany(e => e.Items)
            .WithOne(i => i.Crosscheck)
            .HasForeignKey(i => i.CrosscheckId)
            .OnDelete(DeleteBehavior.Cascade);
        // Id người là tham chiếu mềm (không FK).

        entity.HasQueryFilter(e => !e.IsDeleted && !e.Record.IsDeleted);
    }
}

public sealed class ExpertCrosscheckItemConfiguration : IEntityTypeConfiguration<ExpertCrosscheckItem>
{
    public void Configure(EntityTypeBuilder<ExpertCrosscheckItem> entity)
    {
        entity.ToTable("expert_crosscheck_items");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.CrosscheckId, e.Order });
        entity.Property(e => e.ItemName).HasMaxLength(500).IsRequired();
        entity.Property(e => e.SelfData).HasMaxLength(4000).IsRequired();
        entity.Property(e => e.SourceData).HasMaxLength(4000).IsRequired();
        entity.Property(e => e.Result).HasConversion<int?>();
        entity.Property(e => e.AdjustmentType).HasConversion<int>();
        entity.Property(e => e.BasisText).HasMaxLength(4000).IsRequired();
        entity.Property(e => e.BasisDocNumber).HasMaxLength(200).IsRequired();
        entity.Property(e => e.BasisAgency).HasMaxLength(500).IsRequired();

        // Cùng bộ lọc xóa mềm của phiếu.
        entity.HasQueryFilter(e => !e.Crosscheck.IsDeleted && !e.Crosscheck.Record.IsDeleted);
    }
}
