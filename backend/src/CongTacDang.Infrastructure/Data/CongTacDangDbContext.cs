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
        public DbSet<EvaluationPeriod> EvaluationPeriods => Set<EvaluationPeriod>();
        public DbSet<EvaluationSetting> EvaluationSettings => Set<EvaluationSetting>();
        public DbSet<EvaluationRecord> EvaluationRecords => Set<EvaluationRecord>();
        public DbSet<EvaluationTask> EvaluationTasks => Set<EvaluationTask>();
        public DbSet<SecretBallot> SecretBallots => Set<SecretBallot>();
        public DbSet<VotingSession> VotingSessions => Set<VotingSession>();
        public DbSet<VotingResult> VotingResults => Set<VotingResult>();
        public DbSet<TaskAttachment> TaskAttachments => Set<TaskAttachment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Bo phieu kin tuyet doi khong luu UserId nguoi bo phieu de dam bao nguyen tac Dieu le Dang
            modelBuilder.Entity<SecretBallot>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.SessionId, e.TargetMemberId });
            });

            // Tep dinh kem minh chung
            modelBuilder.Entity<TaskAttachment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TaskId);
                entity.HasIndex(e => e.RecordId);
            });
        }
    }
}
