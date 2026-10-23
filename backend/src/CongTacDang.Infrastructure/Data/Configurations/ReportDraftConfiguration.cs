using CongTacDang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CongTacDang.Infrastructure.Data.Configurations;

/// <summary>Một bản nháp cho mỗi kỳ + tổ chức Đảng + biểu mẫu.</summary>
public sealed class ReportDraftConfiguration : IEntityTypeConfiguration<ReportDraft>
{
    public void Configure(EntityTypeBuilder<ReportDraft> entity)
    {
        entity.ToTable("report_drafts");
        entity.HasKey(e => e.Id);
        // PartyCellId null (toàn Đảng bộ) cũng chỉ một bản nháp.
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
        // Khớp query filter của kỳ — tránh cảnh báo EF 10622.
        entity.HasQueryFilter(e => !e.Period.IsDeleted);
    }
}
