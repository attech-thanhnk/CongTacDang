using CongTacDang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CongTacDang.Infrastructure.Data.Configurations;

/// <summary>Bộ tiêu chí và thang điểm (task 16): mã duy nhất, nội dung jsonb, xmin.</summary>
public sealed class CriteriaSetConfiguration : IEntityTypeConfiguration<CriteriaSet>
{
    public void Configure(EntityTypeBuilder<CriteriaSet> entity)
    {
        entity.ToTable("criteria_sets");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
        entity.HasIndex(e => e.Code).IsUnique();
        entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
        entity.Property(e => e.SelfScoreForm).HasMaxLength(10).IsRequired();
        entity.Property(e => e.Notes).HasMaxLength(4000);
        entity.Property(e => e.Content).HasColumnType("jsonb").IsRequired();
        entity.HasIndex(e => e.Status);
        entity.Property(e => e.Version)
            .HasColumnName("xmin")
            .IsRowVersion();
    }
}
