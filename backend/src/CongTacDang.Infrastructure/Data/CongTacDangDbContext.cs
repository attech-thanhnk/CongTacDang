using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Infrastructure.Data
{
    public class CongTacDangDbContext : DbContext
    {
        private readonly ICurrentUserService _currentUser;

        public CongTacDangDbContext(
            DbContextOptions<CongTacDangDbContext> options,
            ICurrentUserService currentUser)
            : base(options)
        {
            _currentUser = currentUser;
        }

        public DbSet<PartyCell> PartyCells => Set<PartyCell>();
        public DbSet<AdministrativeDepartment> AdministrativeDepartments => Set<AdministrativeDepartment>();
        public DbSet<OrgUnitType> OrgUnitTypes => Set<OrgUnitType>();
        public DbSet<Holiday> Holidays => Set<Holiday>();
        public DbSet<FormLayout> FormLayouts => Set<FormLayout>();
        public DbSet<Position> Positions => Set<Position>();
        public DbSet<MemberPosition> MemberPositions => Set<MemberPosition>();
        public DbSet<PartyMemberProfile> PartyMemberProfiles => Set<PartyMemberProfile>();
        public DbSet<TaskAttachment> TaskAttachments => Set<TaskAttachment>();

        public DbSet<AppRole> Roles => Set<AppRole>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        public DbSet<EvaluationPeriod> EvaluationPeriods => Set<EvaluationPeriod>();
        public DbSet<EvaluationRecord> EvaluationRecords => Set<EvaluationRecord>();
        public DbSet<EvaluationTask> EvaluationTasks => Set<EvaluationTask>();
        public DbSet<EvaluationRecordHistory> EvaluationRecordHistories => Set<EvaluationRecordHistory>();
        public DbSet<CollectiveEvaluationRecord> CollectiveEvaluationRecords => Set<CollectiveEvaluationRecord>();
        public DbSet<CollectiveEvaluationItem> CollectiveEvaluationItems => Set<CollectiveEvaluationItem>();
        public DbSet<EvaluationMeeting> EvaluationMeetings => Set<EvaluationMeeting>();
        public DbSet<EvaluationMeetingVoteSummary> EvaluationMeetingVoteSummaries => Set<EvaluationMeetingVoteSummary>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình entity mới đặt trong Data/Configurations/ (nạp tự động).
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(CongTacDangDbContext).Assembly);

            modelBuilder.Entity<PartyMemberProfile>(entity =>
            {
                entity.ToTable("party_member_profiles");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Username).IsUnique();
                // Index unique lower("Username") tạo bằng SQL trong migration InitialCreate (index biểu thức).
                entity.HasIndex(e => e.PartyCellId);
                entity.HasIndex(e => e.DepartmentId);
                entity.HasIndex(e => e.IsActive);
                entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
                entity.Property(e => e.FullName).HasMaxLength(200).IsRequired();
                entity.Property(e => e.DateOfBirth).HasColumnType("date");
                entity.Property(e => e.Email).HasMaxLength(200);
                entity.Property(e => e.MustChangePassword).HasDefaultValue(false);
                entity.Property(e => e.FailedLoginCount).HasDefaultValue(0);
                entity.Property(e => e.SecurityStamp).HasMaxLength(64).IsRequired();
                entity.Property(e => e.ApprovalAuthorityOverrideReason).HasMaxLength(1000);
                entity.HasQueryFilter(e => !e.IsDeleted);

                entity.HasOne(m => m.PartyCell)
                    .WithMany(c => c.Members)
                    .HasForeignKey(m => m.PartyCellId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(m => m.Department)
                    .WithMany(d => d.Members)
                    .HasForeignKey(m => m.DepartmentId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(m => m.FormLayoutOverride)
                    .WithMany()
                    .HasForeignKey(m => m.FormLayoutOverrideId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TaskAttachment>(entity =>
            {
                entity.ToTable("task_attachments");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.RelatedId);
                entity.HasIndex(e => e.RecordId);
                entity.HasIndex(e => e.UploadedById);
                entity.HasOne<PartyMemberProfile>()
                    .WithMany()
                    .HasForeignKey(e => e.UploadedById)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.Property(e => e.OwnerType).HasMaxLength(50);
                entity.HasIndex(e => new { e.OwnerType, e.OwnerId });
                entity.HasIndex(e => e.FileGroupId);
                entity.Property(e => e.VersionNumber).HasDefaultValue(1);
                entity.Ignore(e => e.IsCurrent);
                entity.Ignore(e => e.GroupId);
                entity.Ignore(e => e.EffectiveOwnerType);
                entity.Ignore(e => e.EffectiveOwnerId);
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.ToTable("audit_logs");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.EntityType, e.EntityId });
                entity.HasIndex(e => e.CreatedAt);
                entity.Property(e => e.EntityType).HasMaxLength(200).IsRequired();
                entity.Property(e => e.EntityId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Action).HasMaxLength(100).IsRequired();
                entity.Property(e => e.ActorName).HasMaxLength(200);
                entity.Property(e => e.IpAddress).HasMaxLength(100);
                entity.Property(e => e.UserAgent).HasMaxLength(1000);
                entity.Property(e => e.RequestPath).HasMaxLength(1000);
            });

            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.ToTable("refresh_tokens");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TokenHash).IsUnique();
                entity.HasIndex(e => e.UserId);
                entity.Ignore(e => e.Token);
                entity.Property(e => e.TokenHash).HasMaxLength(64).IsRequired();
                entity.Property(e => e.CreatedByIp).HasMaxLength(100);
                entity.Property(e => e.ReplacedByTokenHash).HasMaxLength(64);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                // Lọc token của tài khoản đã xóa mềm cho khớp query filter (tránh cảnh báo EF 10622);
                // thu hồi theo tài khoản bỏ qua bộ lọc này để vẫn thu hồi được sau khi xóa.
                entity.HasQueryFilter(e => !e.User.IsDeleted);
            });
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            PrepareAuditEntries();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override int SaveChanges()
        {
            return SaveChanges(true);
        }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            PrepareAuditEntries();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return SaveChangesAsync(true, cancellationToken);
        }

        /// <summary>Đổi xóa thành xóa mềm, cập nhật metadata và tạo audit entries.</summary>
        private void PrepareAuditEntries()
        {
            ChangeTracker.DetectChanges();
            var now = DateTime.UtcNow;
            var auditLogs = new List<AuditLog>();

            foreach (var entry in ChangeTracker.Entries().ToList())
            {
                if (entry.Entity is AuditLog || entry.Entity is EvaluationRecordHistory)
                    continue;

                if (entry.Entity is ISoftDeletable softDelete && entry.State == EntityState.Deleted)
                {
                    softDelete.IsDeleted = true;
                    softDelete.DeletedAt = now;
                    softDelete.DeletedBy = _currentUser.UserId;
                    entry.State = EntityState.Modified;

                    var isActive = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "IsActive");
                    if (isActive != null)
                        isActive.CurrentValue = false;
                }

                if (entry.Entity is IAuditableEntity auditable)
                {
                    if (entry.State == EntityState.Added)
                    {
                        if (auditable.CreatedAt == default)
                            auditable.CreatedAt = now;
                        auditable.CreatedBy ??= _currentUser.UserId;
                    }
                    else if (entry.State == EntityState.Modified)
                    {
                        auditable.UpdatedAt = now;
                        auditable.UpdatedBy = _currentUser.UserId;
                    }
                }

                if (entry.State is not (EntityState.Added or EntityState.Modified))
                    continue;

                // Cập nhật trạng thái đăng nhập không ghi audit — đã có nhật ký đăng nhập riêng.
                if (entry.Entity is LoginEvent || IsLoginBookkeeping(entry))
                    continue;

                var changedProperties = entry.Properties
                    .Where(p => p.IsModified || entry.State == EntityState.Added)
                    .Where(p => !IsSensitiveProperty(p.Metadata.Name))
                    .ToList();

                var oldValues = entry.State == EntityState.Added
                    ? new Dictionary<string, object?>()
                    : changedProperties.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue);
                var newValues = changedProperties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
                var isDelete = entry.Entity is ISoftDeletable deleted && deleted.IsDeleted;
                var key = entry.Metadata.FindPrimaryKey();
                var entityId = key == null
                    ? string.Empty
                    : string.Join(",", key.Properties.Select(property =>
                        $"{property.Name}={entry.Property(property.Name).CurrentValue}"));

                auditLogs.Add(new AuditLog
                {
                    ActorId = _currentUser.UserId,
                    ActorName = string.IsNullOrWhiteSpace(_currentUser.UserName) ? "system" : _currentUser.UserName,
                    Action = entry.State == EntityState.Added ? "Create" : isDelete ? "Delete" : "Update",
                    // Bảng nối nhiều-nhiều dùng chung kiểu Dictionary: ghi tên bảng thay cho tên kiểu.
                    EntityType = entry.Metadata.HasSharedClrType ? entry.Metadata.Name : entry.Metadata.ClrType.Name,
                    EntityId = entityId,
                    OldValues = JsonSerializer.Serialize(oldValues),
                    NewValues = JsonSerializer.Serialize(newValues),
                    IpAddress = _currentUser.IpAddress,
                    UserAgent = _currentUser.UserAgent,
                    RequestPath = _currentUser.RequestPath,
                    CreatedAt = now
                });
            }

            if (auditLogs.Count > 0)
                AuditLogs.AddRange(auditLogs);
        }

        /// <summary>Các trường chỉ luồng đăng nhập cập nhật.</summary>
        private static readonly HashSet<string> LoginBookkeepingProperties = new(StringComparer.Ordinal)
        {
            nameof(PartyMemberProfile.LastLoginAt),
            nameof(PartyMemberProfile.FailedLoginCount),
            nameof(PartyMemberProfile.LockoutEnd),
            nameof(PartyMemberProfile.UpdatedAt),
            nameof(PartyMemberProfile.UpdatedBy)
        };

        /// <summary>
        /// Chỉ đổi trường theo dõi đăng nhập (request ẩn danh) — so theo giá trị thực đổi vì repository có thể đánh dấu mọi cột đã sửa.
        /// Thao tác của quản trị (mở khóa) vẫn ghi audit.
        /// </summary>
        private bool IsLoginBookkeeping(EntityEntry entry)
        {
            if (entry.Entity is not PartyMemberProfile || entry.State != EntityState.Modified || _currentUser.UserId != null)
                return false;

            var changed = entry.Properties
                .Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
                .Select(p => p.Metadata.Name)
                .ToList();
            return changed.Count > 0 && changed.All(LoginBookkeepingProperties.Contains);
        }

        private static bool IsSensitiveProperty(string propertyName)
        {
            return propertyName is "PasswordHash" or "SecurityStamp" or "Token" or "TokenHash" or "ReplacedByTokenHash";
        }
    }
}
