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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Tep dinh kem minh chung & tai lieu
            modelBuilder.Entity<TaskAttachment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TaskId);
                entity.HasIndex(e => e.RecordId);
            });
        }
    }
}
