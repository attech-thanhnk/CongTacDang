using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Infrastructure.Data;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Seeder trên CSDL PostgreSQL riêng (<c>ctd_it_*</c>, dựng bằng migration, tạo/xóa trong test): vai trò mặc định,
/// dữ liệu mẫu chỉ tạo một lần, không gán lại vai trò, không ghi đè quyền đã sửa; index unique <c>lower("Username")</c>.
/// </summary>
public sealed class DataSeederAuthorizationTests
{
    private const string SamplePassword = "MauThu2026x";
    private static readonly string? AdminConnection = Environment.GetEnvironmentVariable(ApiFactory.ConnectionEnvVar);
    private static readonly DataSeeder.SampleDataOptions Sample = new(true, SamplePassword);

    [SkippableFact]
    public async Task SampleData_SeededOnce_NeverReassigned_RolePermissionsNotOverwritten()
    {
        await WithDatabaseAsync(async options =>
        {
            await using (var db = NewContext(options))
                await DataSeeder.SeedAsync(db, Sample);

            Guid adminAssignmentId;
            await using (var db = NewContext(options))
            {
                var roles = await db.Roles.Include(r => r.Permissions).ToListAsync();
                Assert.Equal(9, roles.Count);
                var adminRole = Assert.Single(roles, r => r.IsProtected);
                Assert.Contains(adminRole.Permissions, p => p.Code == PermissionCodes.SystemRolesManage);
                Assert.DoesNotContain(adminRole.Permissions, p => p.Code == PermissionCodes.EvaluationRead); // tách quản trị kỹ thuật

                var permissions = await db.Permissions.ToListAsync();
                Assert.All(PermissionCodes.Definitions, d =>
                    Assert.Contains(permissions, p => p.Code == d.Code && p.Module == d.Module));

                var assignments = await db.Set<UserRoleAssignment>().Include(a => a.User).Include(a => a.Role).ToListAsync();
                var secretary = assignments.Where(a => a.User!.Username == "bithu.kt").ToList();
                var cellKt = await db.PartyCells.SingleAsync(c => c.Code == "CB-KT");
                var deptKt = await db.AdministrativeDepartments.SingleAsync(d => d.Code == "PH-KT");
                Assert.Contains(secretary, a => a.Role!.Code == "CHI_UY_CHI_BO" && a.ScopeType == RoleScopeType.PartyCell && a.ScopeId == cellKt.Id);
                Assert.Contains(assignments, a => a.User!.Username == "truongphong.kt" && a.Role!.Code == "LANH_DAO_PHONG"
                    && a.ScopeType == RoleScopeType.Department && a.ScopeId == deptKt.Id);
                var admin = Assert.Single(assignments, a => a.User!.Username == "admin");
                Assert.Equal(RoleScopeType.Global, admin.ScopeType);
                Assert.Equal(adminRole.Id, admin.RoleId);
                adminAssignmentId = admin.Id;

                // Tài khoản mẫu: bắt buộc đổi mật khẩu, mật khẩu theo cấu hình.
                var users = await db.PartyMemberProfiles.ToListAsync();
                Assert.All(users, u => Assert.True(u.MustChangePassword));
                Assert.All(users, u => Assert.True(BCrypt.Net.BCrypt.Verify(SamplePassword, u.PasswordHash)));

                // Kỳ mẫu Quý III/2026 (chuyển tiếp, đang mở) với hồ sơ ở nhiều bước.
                var period = await db.EvaluationPeriods.SingleAsync();
                Assert.Equal(PeriodStatus.Open, period.Status);
                Assert.Equal("09B", period.GetSettings().SelfScoreForm);
                var statuses = await db.EvaluationRecords.Select(r => r.Status).ToListAsync();
                Assert.Equal(6, statuses.Count);
                Assert.Equal(6, statuses.Distinct().Count());
                Assert.Contains(RecordStatus.Published, statuses);

                // Quản trị thu hồi bản gán của admin và sửa quyền một vai trò mặc định.
                admin.IsDeleted = true;
                var evaluatee = roles.Single(r => r.Code == "NGUOI_DUOC_DANH_GIA");
                evaluatee.Permissions.Clear();
                evaluatee.Permissions.Add(permissions.Single(p => p.Code == PermissionCodes.MeetingRead));
                await db.SaveChangesAsync();
            }

            // Khởi động lại (seed lần 2): không tạo thêm dữ liệu mẫu, không gán lại, không ghi đè.
            await using (var db = NewContext(options))
                await DataSeeder.SeedAsync(db, Sample);

            await using (var db = NewContext(options))
            {
                var adminUser = await db.PartyMemberProfiles.SingleAsync(u => u.Username == "admin");
                Assert.Empty(await db.Set<UserRoleAssignment>().Where(a => a.UserId == adminUser.Id).ToListAsync());
                Assert.True(await db.Set<UserRoleAssignment>().IgnoreQueryFilters().AnyAsync(a => a.Id == adminAssignmentId && a.IsDeleted));
                var evaluatee = await db.Roles.Include(r => r.Permissions).SingleAsync(r => r.Code == "NGUOI_DUOC_DANH_GIA");
                Assert.Equal(new[] { PermissionCodes.MeetingRead }, evaluatee.Permissions.Select(p => p.Code).ToArray());
                Assert.Equal(9, await db.Roles.CountAsync());
                Assert.Equal(1, await db.EvaluationPeriods.CountAsync());
                Assert.Equal(9, await db.PartyMemberProfiles.CountAsync());
            }

            // Tên đăng nhập duy nhất không phân biệt hoa thường (index lower("Username") trong migration).
            await using (var db = NewContext(options))
            {
                var ex = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO party_member_profiles (\"Id\", \"Username\", \"PasswordHash\", \"FullName\", \"Email\", \"PhoneNumber\", "
                    + "\"IsPartyMember\", \"PositionTitle\", \"JobGroup\", \"ApprovalAuthority\", "
                    + "\"SecurityStamp\", \"IsActive\", \"MustChangePassword\", \"FailedLoginCount\", \"CreatedAt\", \"IsDeleted\") "
                    + "VALUES (gen_random_uuid(), 'ADMIN', 'x', 'Trùng', '', '', FALSE, '', 1, 1, 'x', TRUE, FALSE, 0, now(), FALSE)"));
                Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
            }
        });
    }

    [SkippableFact]
    public async Task SampleData_InvalidConfiguredPassword_CreatesNoSampleData()
    {
        await WithDatabaseAsync(async options =>
        {
            await using (var db = NewContext(options))
                await DataSeeder.SeedAsync(db, new DataSeeder.SampleDataOptions(true, "123456"));

            await using (var db = NewContext(options))
            {
                Assert.Equal(9, await db.Roles.CountAsync());
                Assert.Equal(0, await db.PartyMemberProfiles.IgnoreQueryFilters().CountAsync());
                Assert.Equal(0, await db.EvaluationPeriods.CountAsync());
            }
        });
    }

    #region Hỗ trợ

    private static CongTacDangDbContext NewContext(DbContextOptions<CongTacDangDbContext> options) => new(options, new SeederUser());

    private static async Task WithDatabaseAsync(Func<DbContextOptions<CongTacDangDbContext>, Task> test)
    {
        Skip.If(string.IsNullOrWhiteSpace(AdminConnection), $"Bỏ qua: chưa đặt biến môi trường {ApiFactory.ConnectionEnvVar}.");
        var admin = TestDatabaseAdmin.AdminConnectionString(AdminConnection!);
        var name = TestDatabaseNames.NewName(DateTime.UtcNow);

        await TestDatabaseAdmin.CreateAsync(admin, name);
        try
        {
            var options = new DbContextOptionsBuilder<CongTacDangDbContext>()
                .UseNpgsql(TestDatabaseAdmin.TargetConnectionString(admin, name)).Options;
            await using (var db = new CongTacDangDbContext(options, new SeederUser()))
                await db.Database.MigrateAsync();
            await test(options);
        }
        finally
        {
            await TestDatabaseAdmin.DropAsync(admin, name);
        }
    }

    private sealed class SeederUser : ICurrentUserService
    {
        public Guid? UserId => null;
        public string UserName => "seeder-test";
        public string? IpAddress => null;
        public string? UserAgent => null;
        public string? RequestPath => null;
    }

    #endregion
}
