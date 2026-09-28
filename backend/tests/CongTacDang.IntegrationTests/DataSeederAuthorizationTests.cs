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
/// Task 09 (T-46, T-55): seeder trên CSDL PostgreSQL riêng (<c>ctd_it_*</c>, tạo/xóa trong test) — vai trò mặc định,
/// gán mẫu chỉ một lần, không ghi đè quyền đã sửa. (Chuyển gán vai trò cũ user_roles: xem Wave4MigrationTests.)
/// </summary>
public sealed class DataSeederAuthorizationTests
{
    private static readonly string? AdminConnection = Environment.GetEnvironmentVariable(ApiFactory.ConnectionEnvVar);

    [SkippableFact]
    public async Task SampleData_SeededOnce_NeverReassigned_RolePermissionsNotOverwritten()
    {
        await WithDatabaseAsync(async options =>
        {
            await using (var db = NewContext(options))
                await DataSeeder.SeedAsync(db, seedSampleData: true);

            Guid adminAssignmentId;
            await using (var db = NewContext(options))
            {
                var roles = await db.Roles.Include(r => r.Permissions).ToListAsync();
                Assert.Equal(9, roles.Count);
                var adminRole = roles.Single(r => r.Code == "QUAN_TRI_HE_THONG");
                Assert.True(adminRole.IsProtected);
                Assert.Contains(adminRole.Permissions, p => p.Code == PermissionCodes.SystemRolesManage);
                Assert.DoesNotContain(adminRole.Permissions, p => p.Code == PermissionCodes.EvaluationRead); // tách quản trị kỹ thuật

                var permissions = await db.Permissions.ToListAsync();
                Assert.All(PermissionCodes.Definitions, d =>
                    Assert.Contains(permissions, p => p.Code == d.Code && p.Module == d.Module));

                var assignments = await db.Set<UserRoleAssignment>().Include(a => a.User).Include(a => a.Role).ToListAsync();
                var secretary = assignments.Where(a => a.User!.Username == "bithu_cbkt").ToList();
                var cellKt = await db.PartyCells.SingleAsync(c => c.Code == "CB-KT");
                Assert.Contains(secretary, a => a.Role!.Code == "CHI_UY_CHI_BO" && a.ScopeType == RoleScopeType.PartyCell && a.ScopeId == cellKt.Id);
                Assert.Contains(secretary, a => a.Role!.Code == "LANH_DAO_PHONG" && a.ScopeType == RoleScopeType.Department);
                var admin = Assert.Single(assignments, a => a.User!.Username == "admin");
                Assert.Equal(RoleScopeType.Global, admin.ScopeType);
                adminAssignmentId = admin.Id;

                // Quản trị thu hồi bản gán của admin và sửa quyền một vai trò mặc định.
                admin.IsDeleted = true;
                var evaluatee = roles.Single(r => r.Code == "NGUOI_DUOC_DANH_GIA");
                evaluatee.Permissions.Clear();
                evaluatee.Permissions.Add(permissions.Single(p => p.Code == PermissionCodes.MeetingRead));
                await db.SaveChangesAsync();
            }

            // Khởi động lại (seed lần 2): không gán lại, không ghi đè.
            await using (var db = NewContext(options))
                await DataSeeder.SeedAsync(db, seedSampleData: true);

            await using (var db = NewContext(options))
            {
                var adminUser = await db.PartyMemberProfiles.SingleAsync(u => u.Username == "admin");
                Assert.Empty(await db.Set<UserRoleAssignment>().Where(a => a.UserId == adminUser.Id).ToListAsync());
                Assert.True(await db.Set<UserRoleAssignment>().IgnoreQueryFilters().AnyAsync(a => a.Id == adminAssignmentId && a.IsDeleted));
                var evaluatee = await db.Roles.Include(r => r.Permissions).SingleAsync(r => r.Code == "NGUOI_DUOC_DANH_GIA");
                Assert.Equal(new[] { PermissionCodes.MeetingRead }, evaluatee.Permissions.Select(p => p.Code).ToArray());
                Assert.Equal(9, await db.Roles.CountAsync());
            }
        });
    }

    #region Hỗ trợ

    private static CongTacDangDbContext NewContext(DbContextOptions<CongTacDangDbContext> options) => new(options, new SeederUser());

    private static async Task WithDatabaseAsync(Func<DbContextOptions<CongTacDangDbContext>, Task> test)
    {
        Skip.If(string.IsNullOrWhiteSpace(AdminConnection), $"Bỏ qua: chưa đặt biến môi trường {ApiFactory.ConnectionEnvVar}.");
        var admin = new NpgsqlConnectionStringBuilder(AdminConnection);
        TestDatabaseNames.EnsureNotForbidden(admin.Database);
        var name = TestDatabaseNames.NewName(DateTime.UtcNow);
        TestDatabaseNames.EnsureSafe(name);

        await ExecuteAsync(admin.ConnectionString, $"CREATE DATABASE \"{name}\"");
        try
        {
            var target = new NpgsqlConnectionStringBuilder(AdminConnection) { Database = name };
            var options = new DbContextOptionsBuilder<CongTacDangDbContext>().UseNpgsql(target.ConnectionString).Options;
            await using (var db = new CongTacDangDbContext(options, new SeederUser()))
                await db.Database.EnsureCreatedAsync();
            await test(options);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            TestDatabaseNames.EnsureSafe(name);
            await ExecuteAsync(admin.ConnectionString, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
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
