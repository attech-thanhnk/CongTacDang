using CongTacDang.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CongTacDang.Infrastructure.Data.Configurations;

/// <summary>Cấu hình bảng nhật ký đăng nhập <c>login_events</c>.</summary>
public sealed class LoginEventConfiguration : IEntityTypeConfiguration<LoginEvent>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<LoginEvent> entity)
    {
        entity.ToTable("login_events");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.UsernameAttempted).HasMaxLength(LoginEvent.MaxUsernameLength).IsRequired();
        entity.Property(e => e.Result).IsRequired();
        entity.Property(e => e.IpAddress).HasMaxLength(100);
        entity.Property(e => e.UserAgent).HasMaxLength(LoginEvent.MaxUserAgentLength);
        entity.HasIndex(e => new { e.UserId, e.CreatedAt });
        entity.HasIndex(e => e.CreatedAt);

        // Không cascade: tài khoản chỉ xóa mềm; nếu bị xóa vật lý thì giữ nhật ký, bỏ liên kết.
        entity.HasOne<PartyMemberProfile>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
