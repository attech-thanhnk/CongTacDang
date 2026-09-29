using CongTacDang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CongTacDang.Infrastructure.Data.Configurations;

/// <summary>Cấu hình bảng thông tin đơn vị <c>organization_settings</c> (task 17).</summary>
public sealed class OrganizationSettingsConfiguration : IEntityTypeConfiguration<OrganizationSettings>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OrganizationSettings> entity)
    {
        entity.ToTable("organization_settings");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedNever();
        entity.Property(e => e.PartyCommitteeName).HasMaxLength(OrganizationSettings.MaxNameLength).IsRequired();
        entity.Property(e => e.SuperiorPartyName).HasMaxLength(OrganizationSettings.MaxNameLength).IsRequired();
        entity.Property(e => e.CompanyName).HasMaxLength(OrganizationSettings.MaxNameLength).IsRequired();
        entity.Property(e => e.ParentCompanyName).HasMaxLength(OrganizationSettings.MaxNameLength).IsRequired();
        entity.Property(e => e.ShortName).HasMaxLength(OrganizationSettings.MaxShortNameLength).IsRequired();
        entity.Property(e => e.Location).HasMaxLength(OrganizationSettings.MaxLocationLength).IsRequired();
        entity.Property(e => e.SystemName).HasMaxLength(OrganizationSettings.MaxNameLength).IsRequired();
    }
}

/// <summary>Cấu hình bảng phiên bản file mẫu Word <c>word_template_versions</c> (task 17).</summary>
public sealed class WordTemplateVersionConfiguration : IEntityTypeConfiguration<WordTemplateVersion>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WordTemplateVersion> entity)
    {
        entity.ToTable("word_template_versions");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.TemplateCode).HasMaxLength(WordTemplateVersion.MaxCodeLength).IsRequired();
        entity.Property(e => e.OriginalFileName).HasMaxLength(WordTemplateVersion.MaxFileNameLength).IsRequired();
        entity.Property(e => e.ObjectKey).HasMaxLength(500).IsRequired();
        entity.Property(e => e.Checksum).HasMaxLength(64).IsRequired();
        entity.Property(e => e.Note).HasMaxLength(WordTemplateVersion.MaxNoteLength);
        entity.Property(e => e.TagsJson).IsRequired();
        entity.Property(e => e.WarningsJson).IsRequired();
        entity.Property(e => e.UploadedByName).HasMaxLength(200);
        entity.Property(e => e.ActivatedByName).HasMaxLength(200);

        entity.HasIndex(e => new { e.TemplateCode, e.VersionNumber }).IsUnique();
        // Mỗi biểu mẫu tối đa một phiên bản đang kích hoạt.
        entity.HasIndex(e => e.TemplateCode).IsUnique().HasFilter("\"IsActive\" = TRUE");
    }
}
