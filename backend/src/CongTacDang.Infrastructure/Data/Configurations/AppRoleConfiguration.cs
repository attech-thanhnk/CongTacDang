using CongTacDang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CongTacDang.Infrastructure.Data.Configurations;

/// <summary>Bảng <c>roles</c> và quan hệ <c>role_permissions</c>.</summary>
public sealed class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AppRole> entity)
    {
        entity.ToTable("roles");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.Code).IsUnique();
        // Tên vai trò duy nhất trong các vai trò chưa xóa (docs/thiet-ke/phan-quyen.md mục 2).
        entity.HasIndex(e => e.Name).IsUnique().HasFilter("\"IsDeleted\" = FALSE");
        entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
        entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(1000);
        entity.HasQueryFilter(e => !e.IsDeleted);

        // Nhiều-nhiều: Role <-> Permission qua role_permissions
        entity.HasMany(r => r.Permissions)
            .WithMany(p => p.Roles)
            .UsingEntity(
                "role_permissions",
                l => l.HasOne(typeof(Permission)).WithMany().HasForeignKey("permission_id"),
                r => r.HasOne(typeof(AppRole)).WithMany().HasForeignKey("role_id"));

        // Gán vai trò cho người dùng: bảng user_role_assignments (UserRoleAssignment, có phạm vi và thời hạn).
    }
}
