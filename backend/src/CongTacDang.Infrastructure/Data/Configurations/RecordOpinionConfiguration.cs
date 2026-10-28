using CongTacDang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CongTacDang.Infrastructure.Data.Configurations;

public sealed class RecordOpinionConfiguration : IEntityTypeConfiguration<RecordOpinion>
{
    public void Configure(EntityTypeBuilder<RecordOpinion> entity)
    {
        entity.ToTable("record_opinions");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.RecordId, e.Kind });
        entity.Property(e => e.Kind).HasConversion<int>();
        entity.Property(e => e.Content).HasMaxLength(4000).IsRequired();
        entity.Property(e => e.RecordedByName).HasMaxLength(200).IsRequired();

        entity.HasOne(e => e.Record)
            .WithMany()
            .HasForeignKey(e => e.RecordId)
            .OnDelete(DeleteBehavior.Cascade);
        // Id người ghi là tham chiếu mềm (không FK).

        entity.HasQueryFilter(e => !e.Record.IsDeleted);
    }
}
