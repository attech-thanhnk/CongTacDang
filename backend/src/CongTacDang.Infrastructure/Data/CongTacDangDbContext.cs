using Microsoft.EntityFrameworkCore;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Infrastructure.Data
{
    public class CongTacDangDbContext : DbContext
    {
        public CongTacDangDbContext(DbContextOptions<CongTacDangDbContext> options)
            : base(options)
        {
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
            });

            modelBuilder.Entity<AdministrativeDepartment>(entity =>
            {
                entity.ToTable("administrative_departments");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            });

            // 2. Hồ sơ Cán bộ, Đảng viên
            modelBuilder.Entity<PartyMemberProfile>(entity =>
            {
                entity.ToTable("party_member_profiles");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
                entity.Property(e => e.FullName).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Email).HasMaxLength(200);

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
            });

            modelBuilder.Entity<AppRole>(entity =>
            {
                entity.ToTable("roles");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired();

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

            modelBuilder.Entity<EvaluationTask>(entity =>
            {
                entity.ToTable("evaluation_tasks");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TaskName).HasMaxLength(500).IsRequired();

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
                entity.HasIndex(e => e.Token).IsUnique();
                entity.HasIndex(e => e.UserId);
                entity.Property(e => e.Token).HasMaxLength(256).IsRequired();
                entity.Property(e => e.CreatedByIp).HasMaxLength(100);
                entity.Property(e => e.ReplacedByToken).HasMaxLength(256);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
