using CongTacDang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CongTacDang.Infrastructure.Data.Configurations;

/// <summary>Bản nháp phần nhập tay của báo cáo tổng hợp (task 19): một bản cho mỗi kỳ + tổ chức Đảng + biểu mẫu.</summary>
public sealed class ReportDraftConfiguration : IEntityTypeConfiguration<ReportDraft>
{
    public void Configure(EntityTypeBuilder<ReportDraft> entity)
    {
        entity.ToTable("report_drafts");
        entity.HasKey(e => e.Id);
        // Toàn Đảng bộ (PartyCellId null) cũng chỉ có một bản nháp cho mỗi kỳ và biểu mẫu.
        entity.HasIndex(e => new { e.PeriodId, e.PartyCellId, e.FormCode }).IsUnique().AreNullsDistinct(false);
        entity.Property(e => e.FormCode).HasMaxLength(10).IsRequired();
        entity.Property(e => e.Content).HasColumnType("jsonb").IsRequired();
        entity.Property(e => e.Version)
            .HasColumnName("xmin")
            .IsRowVersion();
        entity.HasOne(e => e.Period)
            .WithMany()
            .HasForeignKey(e => e.PeriodId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.PartyCell)
            .WithMany()
            .HasForeignKey(e => e.PartyCellId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
