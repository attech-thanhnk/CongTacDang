using CongTacDang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CongTacDang.Infrastructure.Data.Configurations;

/// <summary>Bảng <c>permissions</c>: danh mục mã quyền, đồng bộ từ <c>PermissionCodes</c> khi khởi động.</summary>
public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Permission> entity)
    {
        entity.ToTable("permissions");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.Code).IsUnique();
        entity.Property(e => e.Code).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
        entity.Property(e => e.Module).HasMaxLength(50).IsRequired();
        entity.HasQueryFilter(e => !e.IsDeleted);
    }
}
