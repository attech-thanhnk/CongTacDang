using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Microsoft.EntityFrameworkCore;
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
        public DbSet<PartyMemberProfile> PartyMemberProfiles => Set<PartyMemberProfile>();
        public DbSet<TaskAttachment> TaskAttachments => Set<TaskAttachment>();

        // Dynamic RBAC & Authentication DbSets
        public DbSet<AppRole> Roles => Set<AppRole>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        // Evaluation DbSets (Hướng dẫn 03-HD/TVĐU)
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

            // 1. Tổ chức Chi bộ Đảng & Phòng ban Chuyên môn
            modelBuilder.Entity<PartyCell>(entity =>
            {
                entity.ToTable("party_cells");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            modelBuilder.Entity<AdministrativeDepartment>(entity =>
            {
                entity.ToTable("administrative_departments");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            // 2. Hồ sơ Cán bộ, Đảng viên
            modelBuilder.Entity<PartyMemberProfile>(entity =>
            {
                entity.ToTable("party_member_profiles");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.PartyCellId);
                entity.HasIndex(e => e.DepartmentId);
                entity.HasIndex(e => e.IsActive);
                entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
                entity.Property(e => e.FullName).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Email).HasMaxLength(200);
                entity.Property(e => e.MustChangePassword).HasDefaultValue(false);
                entity.Property(e => e.FailedLoginCount).HasDefaultValue(0);
                entity.HasQueryFilter(e => !e.IsDeleted);

                entity.HasOne(m => m.PartyCell)
                    .WithMany(c => c.Members)
                    .HasForeignKey(m => m.PartyCellId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(m => m.Department)
                    .WithMany(d => d.Members)
                    .HasForeignKey(m => m.DepartmentId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // 3. Tệp đính kèm minh chứng & tài liệu
            modelBuilder.Entity<TaskAttachment>(entity =>
            {
                entity.ToTable("task_attachments");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TaskId);
                entity.HasIndex(e => e.RecordId);
                entity.HasIndex(e => e.UploadedById);
                entity.HasOne<PartyMemberProfile>()
                    .WithMany()
                    .HasForeignKey(e => e.UploadedById)
                    .OnDelete(DeleteBehavior.SetNull);
                // Mô hình file theo đối tượng + phiên bản (T-36)
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

            // Dynamic RBAC Configuration
            modelBuilder.Entity<Permission>(entity =>
            {
                entity.ToTable("permissions");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Code).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Resource).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Action).HasMaxLength(50).IsRequired();
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            modelBuilder.Entity<AppRole>(entity =>
            {
                entity.ToTable("roles");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
                entity.HasQueryFilter(e => !e.IsDeleted);

                // Many-to-Many: Role <-> Permission via role_permissions
                entity.HasMany(r => r.Permissions)
                    .WithMany(p => p.Roles)
                    .UsingEntity(
                        "role_permissions",
                        l => l.HasOne(typeof(Permission)).WithMany().HasForeignKey("permission_id"),
                        r => r.HasOne(typeof(AppRole)).WithMany().HasForeignKey("role_id")
                    );

                // Many-to-Many: Role <-> PartyMemberProfile via user_roles
                entity.HasMany(r => r.Members)
                    .WithMany(m => m.Roles)
                    .UsingEntity(
                        "user_roles",
                        l => l.HasOne(typeof(PartyMemberProfile)).WithMany().HasForeignKey("user_id"),
                        r => r.HasOne(typeof(AppRole)).WithMany().HasForeignKey("role_id")
                    );
            });

            // Quy trình Đánh giá cán bộ 03-HD/TVĐU
            modelBuilder.Entity<EvaluationPeriod>(entity =>
            {
                entity.ToTable("evaluation_periods");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
                entity.HasIndex(e => new { e.Year, e.Quarter });
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.IsActive)
                    .IsUnique()
                    .HasFilter("\"IsActive\" = TRUE");
                entity.Property(e => e.Version)
                    .HasColumnName("xmin")
                    .IsRowVersion();

                entity.HasMany(p => p.Records)
                    .WithOne(r => r.Period)
                    .HasForeignKey(r => r.PeriodId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<EvaluationRecord>(entity =>
            {
                entity.ToTable("evaluation_records");
                entity.HasKey(e => e.Id);

                // 1 Cán bộ chỉ có 1 hồ sơ trong 1 kỳ đánh giá
                entity.HasIndex(e => new { e.PeriodId, e.MemberId }).IsUnique();
                entity.HasIndex(e => e.MemberId);
                entity.HasIndex(e => e.PartyCellId);
                entity.HasIndex(e => e.DepartmentId);
                entity.HasIndex(e => e.Status);
                entity.Property(e => e.Version)
                    .HasColumnName("xmin")
                    .IsRowVersion();

                entity.HasOne(r => r.Member)
                    .WithMany()
                    .HasForeignKey(r => r.MemberId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.PartyCell)
                    .WithMany()
                    .HasForeignKey(r => r.PartyCellId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(r => r.Department)
                    .WithMany()
                    .HasForeignKey(r => r.DepartmentId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasMany(r => r.Tasks)
                    .WithOne(t => t.Record)
                    .HasForeignKey(t => t.RecordId)
                     .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CollectiveEvaluationRecord>(entity =>
            {
                entity.ToTable("collective_evaluation_records");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.PeriodId, e.Form, e.PartyCellId, e.DepartmentId }).IsUnique();
                entity.HasIndex(e => new { e.PeriodId, e.Status });
                entity.HasIndex(e => e.PartyCellId);
                entity.HasIndex(e => e.DepartmentId);
                entity.Property(e => e.SubjectName).HasMaxLength(300).IsRequired();
                entity.Property(e => e.Form).HasConversion<int>();
                entity.Property(e => e.Version)
                    .HasColumnName("xmin")
                    .IsRowVersion();
                entity.HasOne(e => e.Period)
                    .WithMany(p => p.CollectiveRecords)
                    .HasForeignKey(e => e.PeriodId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.PartyCell)
                    .WithMany()
                    .HasForeignKey(e => e.PartyCellId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(e => e.Department)
                    .WithMany()
                    .HasForeignKey(e => e.DepartmentId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(e => e.Head)
                    .WithMany()
                    .HasForeignKey(e => e.HeadId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasMany(e => e.Items)
                    .WithOne(i => i.CollectiveRecord)
                    .HasForeignKey(i => i.CollectiveRecordId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CollectiveEvaluationItem>(entity =>
            {
                entity.ToTable("collective_evaluation_items");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Category).HasMaxLength(200);
                entity.Property(e => e.TaskName).HasMaxLength(500);
            });

            modelBuilder.Entity<EvaluationMeeting>(entity =>
            {
                entity.ToTable("evaluation_meetings");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.PeriodId, e.PartyCellId, e.FormCode });
                entity.Property(e => e.Version)
                    .HasColumnName("xmin")
                    .IsRowVersion();
                entity.Property(e => e.FormCode).HasMaxLength(10).IsRequired();
                entity.Property(e => e.MeetingType).HasMaxLength(200);
                entity.Property(e => e.Location).HasMaxLength(300);
                entity.HasOne(e => e.Period)
                    .WithMany(p => p.Meetings)
                    .HasForeignKey(e => e.PeriodId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.PartyCell)
                    .WithMany()
                    .HasForeignKey(e => e.PartyCellId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasMany(e => e.VoteSummaries)
                    .WithOne(v => v.Meeting)
                    .HasForeignKey(v => v.MeetingId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<EvaluationMeetingVoteSummary>(entity =>
            {
                entity.ToTable("evaluation_meeting_vote_summaries");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.MeetingId, e.RecordId }).IsUnique();
                entity.HasOne(e => e.Record)
                    .WithMany()
                    .HasForeignKey(e => e.RecordId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<EvaluationPeriod>(entity =>
            {
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            modelBuilder.Entity<EvaluationRecord>(entity =>
            {
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            modelBuilder.Entity<CollectiveEvaluationRecord>(entity =>
            {
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            modelBuilder.Entity<EvaluationMeeting>(entity =>
            {
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            modelBuilder.Entity<EvaluationTask>(entity =>
            {
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            modelBuilder.Entity<EvaluationRecordHistory>(entity =>
            {
                entity.ToTable("evaluation_record_histories");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.RecordId, e.CreatedAt });
                entity.HasOne(e => e.Record)
                    .WithMany()
                    .HasForeignKey(e => e.RecordId)
                    .OnDelete(DeleteBehavior.Cascade);
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

            modelBuilder.Entity<EvaluationTask>(entity =>
            {
                entity.ToTable("evaluation_tasks");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.RecordId, e.TaskOrder });
                entity.HasIndex(e => e.AttachmentId);
                entity.Property(e => e.TaskName).HasMaxLength(500).IsRequired();
                entity.Property(e => e.Version)
                    .HasColumnName("xmin")
                    .IsRowVersion();

                entity.HasOne(t => t.Attachment)
                    .WithMany()
                    .HasForeignKey(t => t.AttachmentId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Refresh Tokens (Token Rotation)
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
            });
        }

        /// <summary>Chuẩn hóa xóa mềm và tạo audit log trước khi lưu đồng bộ.</summary>
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            PrepareAuditEntries();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        /// <summary>Lưu thay đổi đồng bộ và tự động ghi audit.</summary>
        public override int SaveChanges()
        {
            return SaveChanges(true);
        }

        /// <summary>Chuẩn hóa xóa mềm và tạo audit log trước khi lưu bất đồng bộ.</summary>
        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            PrepareAuditEntries();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        /// <summary>Lưu thay đổi bất đồng bộ và tự động ghi audit.</summary>
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return SaveChangesAsync(true, cancellationToken);
        }

        /// <summary>Đổi thao tác xóa thành xóa mềm, cập nhật metadata và tạo audit entries.</summary>
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
                    EntityType = entry.Metadata.ClrType.Name,
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

        /// <summary>Loại bỏ các trường bí mật khỏi snapshot audit.</summary>
        private static bool IsSensitiveProperty(string propertyName)
        {
            return propertyName is "PasswordHash" or "Token" or "TokenHash" or "ReplacedByTokenHash";
        }
    }
}
