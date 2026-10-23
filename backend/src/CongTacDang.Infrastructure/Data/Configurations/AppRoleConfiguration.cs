using CongTacDang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CongTacDang.Infrastructure.Data.Configurations;

public sealed class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AppRole> entity)
    {
        entity.ToTable("roles");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.Code).IsUnique();
        // Tên duy nhất trong các vai trò chưa xóa.
        entity.HasIndex(e => e.Name).IsUnique().HasFilter("\"IsDeleted\" = FALSE");
        entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
        entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(1000);
        entity.HasQueryFilter(e => !e.IsDeleted);

        entity.HasMany(r => r.Permissions)
            .WithMany(p => p.Roles)
            .UsingEntity(
                "role_permissions",
                l => l.HasOne(typeof(Permission)).WithMany().HasForeignKey("permission_id"),
                r => r.HasOne(typeof(AppRole)).WithMany().HasForeignKey("role_id"));

    }
}
