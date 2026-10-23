using CongTacDang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CongTacDang.Infrastructure.Data.Configurations;

internal static class OrganizationColumns
{
    /// <summary>≈ 50 cấp × 37 ký tự.</summary>
    public const int PathMaxLength = 2000;
}

public sealed class PartyCellConfiguration : IEntityTypeConfiguration<PartyCell>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PartyCell> entity)
    {
        entity.ToTable("party_cells");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.Code).IsUnique();
        entity.HasIndex(e => e.ParentId);
        entity.HasIndex(e => e.Path);
        entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
        entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
        entity.Property(e => e.Path).HasMaxLength(OrganizationColumns.PathMaxLength).IsRequired();
        entity.HasQueryFilter(e => !e.IsDeleted);

        entity.HasOne<PartyCell>()
            .WithMany()
            .HasForeignKey(e => e.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.UnitType)
            .WithMany()
            .HasForeignKey(e => e.UnitTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AdministrativeDepartmentConfiguration : IEntityTypeConfiguration<AdministrativeDepartment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AdministrativeDepartment> entity)
    {
        entity.ToTable("administrative_departments");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.Code).IsUnique();
        entity.HasIndex(e => e.ParentId);
        entity.HasIndex(e => e.Path);
        entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
        entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
        entity.Property(e => e.Path).HasMaxLength(OrganizationColumns.PathMaxLength).IsRequired();
        entity.HasQueryFilter(e => !e.IsDeleted);

        entity.HasOne<AdministrativeDepartment>()
            .WithMany()
            .HasForeignKey(e => e.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.UnitType)
            .WithMany()
            .HasForeignKey(e => e.UnitTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OrgUnitTypeConfiguration : IEntityTypeConfiguration<OrgUnitType>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OrgUnitType> entity)
    {
        entity.ToTable("org_unit_types");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Side).HasConversion<int>();
        // Duy nhất trong cùng một bên, chỉ tính bản ghi chưa xóa.
        entity.HasIndex(e => new { e.Side, e.Name }).IsUnique().HasFilter("\"IsDeleted\" = FALSE");
        entity.HasQueryFilter(e => !e.IsDeleted);
    }
}

public sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Position> entity)
    {
        entity.ToTable("positions");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
        entity.Property(e => e.Side).HasConversion<int>();
        entity.Property(e => e.StatCode).HasMaxLength(10);
        entity.Property(e => e.DefaultApprovalAuthority).HasConversion<int?>();
        entity.Property(e => e.DefaultWorkflowProfileCode).HasMaxLength(50);
        entity.HasIndex(e => e.Name).IsUnique().HasFilter("\"IsDeleted\" = FALSE");
        entity.HasQueryFilter(e => !e.IsDeleted);

        entity.HasOne(e => e.FormLayout)
            .WithMany()
            .HasForeignKey(e => e.FormLayoutId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FormLayoutConfiguration : IEntityTypeConfiguration<FormLayout>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FormLayout> entity)
    {
        entity.ToTable("form_layouts");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).HasMaxLength(FormLayout.MaxNameLength).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(FormLayout.MaxDescriptionLength);
        entity.Property(e => e.EmptyPositionText).HasMaxLength(FormLayout.MaxEmptyPositionTextLength);
        entity.HasIndex(e => e.Name).IsUnique().HasFilter("\"IsDeleted\" = FALSE");
        entity.HasQueryFilter(e => !e.IsDeleted);
    }
}

public sealed class MemberPositionConfiguration : IEntityTypeConfiguration<MemberPosition>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MemberPosition> entity)
    {
        entity.ToTable("member_positions");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.UserId);
        entity.HasIndex(e => e.PositionId);
        entity.HasIndex(e => e.PartyCellId);
        entity.HasIndex(e => e.DepartmentId);
        entity.Property(e => e.Note).HasMaxLength(1000);
        entity.Property(e => e.IsActing).HasDefaultValue(false);
        entity.HasQueryFilter(e => !e.IsDeleted);

        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Position)
            .WithMany()
            .HasForeignKey(e => e.PositionId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.PartyCell)
            .WithMany()
            .HasForeignKey(e => e.PartyCellId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Department)
            .WithMany()
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Holiday> entity)
    {
        entity.ToTable("holidays");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
        // Chỉ tính bản ghi chưa xóa.
        entity.HasIndex(e => e.Date).IsUnique().HasFilter("\"IsDeleted\" = FALSE");
        entity.HasQueryFilter(e => !e.IsDeleted);
    }
}
