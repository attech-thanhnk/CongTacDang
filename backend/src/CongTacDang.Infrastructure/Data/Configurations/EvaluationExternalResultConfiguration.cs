using CongTacDang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CongTacDang.Infrastructure.Data.Configurations;

/// <summary>Kết quả ghi nhận của bước do cấp trên thực hiện (task 15): mỗi hồ sơ một bản ghi cho mỗi bước.</summary>
public sealed class EvaluationExternalResultConfiguration : IEntityTypeConfiguration<EvaluationExternalResult>
{
    public void Configure(EntityTypeBuilder<EvaluationExternalResult> entity)
    {
        entity.ToTable("evaluation_external_results");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.RecordId, e.Step }).IsUnique();
        entity.Property(e => e.AuthorityName).HasMaxLength(300).IsRequired();
        entity.Property(e => e.DocumentNumber).HasMaxLength(100);
        entity.Property(e => e.Comment).HasMaxLength(4000);
        entity.Property(e => e.RecordedByName).HasMaxLength(200);

        // AttachmentId: tham chiếu mềm (không FK) như các tham chiếu biên bản trên hồ sơ.
        entity.HasOne(e => e.Record)
            .WithMany(r => r.ExternalResults)
            .HasForeignKey(e => e.RecordId)
            .OnDelete(DeleteBehavior.Cascade);

        // Hồ sơ đánh giá có bộ lọc xóa mềm → áp cùng điều kiện cho bảng con (tránh cảnh báo quan hệ bắt buộc bị lọc).
        entity.HasQueryFilter(e => !e.Record.IsDeleted);
    }
}
