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
        entity.Property(e => e.IsProtected).HasDefaultValue(false);
        entity.HasQueryFilter(e => !e.IsDeleted);

        // Nhiều-nhiều: Role <-> Permission qua role_permissions
        entity.HasMany(r => r.Permissions)
            .WithMany(p => p.Roles)
            .UsingEntity(
                "role_permissions",
                l => l.HasOne(typeof(Permission)).WithMany().HasForeignKey("permission_id"),
                r => r.HasOne(typeof(AppRole)).WithMany().HasForeignKey("role_id"));

        // Quan hệ nhiều-nhiều user ↔ role cũ (user_roles): KHÔNG còn dùng để phân quyền (xem UserRoleAssignment).
        // Giữ ánh xạ tới khi các chỗ còn đọc PartyMemberProfile.Roles (ngoài phạm vi task 09) được gỡ; sau đó xóa khối này
        // cùng AppRole.Members / PartyMemberProfile.Roles và bảng user_roles (xem báo cáo task 09).
#pragma warning disable CS0618
        entity.HasMany(r => r.Members)
            .WithMany(m => m.Roles)
            .UsingEntity(
                "user_roles",
                l => l.HasOne(typeof(PartyMemberProfile)).WithMany().HasForeignKey("user_id"),
                r => r.HasOne(typeof(AppRole)).WithMany().HasForeignKey("role_id"));
#pragma warning restore CS0618
    }
}
