using CongTacDang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CongTacDang.Infrastructure.Data.Configurations;

/// <summary>Bảng <c>user_role_assignments</c>: gán vai trò kèm phạm vi và thời hạn.</summary>
public sealed class UserRoleAssignmentConfiguration : IEntityTypeConfiguration<UserRoleAssignment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserRoleAssignment> entity)
    {
        entity.ToTable("user_role_assignments");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.UserId);
        entity.HasIndex(e => e.RoleId);
        entity.HasIndex(e => new { e.ScopeType, e.ScopeId });
        entity.Property(e => e.ScopeType).HasConversion<int>();
        entity.Property(e => e.Note).HasMaxLength(1000);
        entity.HasQueryFilter(e => !e.IsDeleted);

        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Role)
            .WithMany()
            .HasForeignKey(e => e.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
