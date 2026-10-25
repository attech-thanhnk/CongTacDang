using CongTacDang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CongTacDang.Infrastructure.Data.Configurations;

public sealed class ControlCheckConfiguration : IEntityTypeConfiguration<ControlCheck>
{
    public void Configure(EntityTypeBuilder<ControlCheck> entity)
    {
        entity.ToTable("control_checks");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.PeriodId, e.Status });
        entity.HasIndex(e => e.DepartmentId);
        entity.HasIndex(e => e.PartyCellId);
        entity.Property(e => e.Status).HasConversion<int>();
        entity.Property(e => e.Conclusion).HasConversion<int?>();
        entity.Property(e => e.GroupName).HasMaxLength(500).IsRequired();
        entity.Property(e => e.NeedsWorkContent).HasMaxLength(4000);
        entity.Property(e => e.PerformedByName).HasMaxLength(200).IsRequired();
        entity.Property(e => e.ConfirmedByName).HasMaxLength(200);
        entity.Property(e => e.ReturnReason).HasMaxLength(2000);
        entity.Property(e => e.Version)
            .HasColumnName("xmin")
            .IsRowVersion();

        entity.HasOne(e => e.Period)
            .WithMany()
            .HasForeignKey(e => e.PeriodId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasMany(e => e.Items)
            .WithOne(i => i.Check)
            .HasForeignKey(i => i.CheckId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasMany(e => e.Records)
            .WithOne(r => r.Check)
            .HasForeignKey(r => r.CheckId)
            .OnDelete(DeleteBehavior.Cascade);
        // Đơn vị là tham chiếu mềm (không FK) vì phiếu có thể thuộc đơn vị chính quyền hoặc tổ chức Đảng.

        entity.HasQueryFilter(e => !e.IsDeleted);
    }
}

public sealed class ControlCheckItemConfiguration : IEntityTypeConfiguration<ControlCheckItem>
{
    public void Configure(EntityTypeBuilder<ControlCheckItem> entity)
    {
        entity.ToTable("control_check_items");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.CheckId, e.ItemCode }).IsUnique();
        entity.Property(e => e.ItemCode).HasMaxLength(8).IsRequired();
        entity.Property(e => e.ItemText).HasMaxLength(1000).IsRequired();
        entity.Property(e => e.Result).HasConversion<int?>();
        entity.Property(e => e.Evidence).HasMaxLength(4000);
        entity.HasQueryFilter(e => !e.Check.IsDeleted);
    }
}

public sealed class ControlCheckRecordConfiguration : IEntityTypeConfiguration<ControlCheckRecord>
{
    public void Configure(EntityTypeBuilder<ControlCheckRecord> entity)
    {
        entity.ToTable("control_check_records");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.CheckId, e.RecordId }).IsUnique();
        // Mỗi hồ sơ chỉ thuộc một phiếu còn hiệu lực.
        entity.HasIndex(e => e.RecordId).IsUnique().HasFilter("\"IsCurrent\"");
        entity.Property(e => e.GradeAtCheck).HasConversion<int>();
        entity.HasOne(e => e.Record)
            .WithMany()
            .HasForeignKey(e => e.RecordId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasQueryFilter(e => !e.Check.IsDeleted && !e.Record.IsDeleted);
    }
}
