using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Infrastructure.Data;
using CongTacDang.Infrastructure.Repositories;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Migration Wave4 trên CSDL PostgreSQL thật (<c>ctd_it_*</c>, tạo/xóa trong test): dữ liệu ở trạng thái Wave3
/// (gán vai trò qua bảng <c>user_roles</c>, tên đăng nhập có chữ hoa) được chuyển đúng khi nâng lên Wave4, và Down đảo ngược.
/// </summary>
public sealed class Wave4MigrationTests
{
    private static readonly string? AdminConnection = Environment.GetEnvironmentVariable(ApiFactory.ConnectionEnvVar);

    private static readonly Guid CadreRole = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid AdminRole = Guid.Parse("11111111-0000-0000-0000-000000000002");
    private static readonly Guid Cadre = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid Admin = Guid.Parse("22222222-0000-0000-0000-000000000002");

    [SkippableFact]
    public async Task Wave3Data_IsMigrated_UserRolesBecomeGlobalAssignments_UsernamesLowercased_AndDownRestores()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            var options = Options(connectionString);
            await using (var db = NewContext(options))
                await db.GetService<IMigrator>().MigrateAsync("Wave3");

            await ExecuteAsync(connectionString, $"""
                INSERT INTO roles ("Id", "Code", "Name", "Description", "IsSystem", "CreatedAt", "IsDeleted") VALUES
                    ('{CadreRole}', 'CAN_BO', 'Cán bộ, Đảng viên', 'Vai trò cũ', TRUE, now(), FALSE),
                    ('{AdminRole}', 'QUAN_TRI_HE_THONG', 'Quản trị hệ thống', 'Vai trò cũ', TRUE, now(), FALSE);
                INSERT INTO party_member_profiles
                    ("Id", "Username", "FullName", "Email", "PhoneNumber", "PositionTitle", "PasswordHash", "SecurityStamp",
                     "AdminPosition", "ApprovalAuthority", "JobGroup", "PartyRole", "IsActive", "IsDeleted", "IsPartyMember", "CreatedAt") VALUES
                    ('{Cadre}', 'Nguyen.Van.A', 'Nguyễn Văn A', '', '', 'Cán bộ', 'x', 'stamp-a', 1, 1, 1, 1, TRUE, FALSE, TRUE, now()),
                    ('{Admin}', 'admin', 'Quản trị', '', '', 'Quản trị', 'x', 'stamp-b', 1, 1, 1, 1, TRUE, FALSE, TRUE, now());
                INSERT INTO user_roles (role_id, user_id) VALUES
                    ('{CadreRole}', '{Cadre}'), ('{AdminRole}', '{Admin}'), ('{CadreRole}', '{Admin}');
                """);

            var before = DateTime.UtcNow.AddMinutes(-1);
            await using (var db = NewContext(options))
                await db.Database.MigrateAsync();

            await using (var db = NewContext(options))
            {
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
                Assert.False(await ScalarAsync<bool>(connectionString, "SELECT to_regclass('user_roles') IS NOT NULL"));

                var assignments = await db.Set<Domain.Entities.UserRoleAssignment>().AsNoTracking().ToListAsync();
                Assert.Equal(3, assignments.Count);
                Assert.All(assignments, a =>
                {
                    Assert.Equal(Domain.Enums.RoleScopeType.Global, a.ScopeType);
                    Assert.Null(a.ScopeId);
                    Assert.Null(a.ValidTo);
                    Assert.False(a.IsDeleted);
                    Assert.True(a.ValidFrom >= before && a.ValidFrom <= DateTime.UtcNow.AddMinutes(1));
                });
                Assert.Equal(
                    new[] { (Admin, AdminRole), (Admin, CadreRole), (Cadre, CadreRole) }.OrderBy(x => x.ToString()),
                    assignments.Select(a => (a.UserId, a.RoleId)).OrderBy(x => x.ToString()));

                // Tên đăng nhập hạ chữ thường, duy nhất không phân biệt hoa thường; cột mới có giá trị cho dữ liệu cũ.
                var cadre = await db.PartyMemberProfiles.AsNoTracking().SingleAsync(m => m.Id == Cadre);
                Assert.Equal("nguyen.van.a", cadre.Username);
                Assert.Null(cadre.LastLoginAt);
                var duplicate = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connectionString, $"""
                    INSERT INTO party_member_profiles
                        ("Id", "Username", "FullName", "Email", "PhoneNumber", "PositionTitle", "PasswordHash", "SecurityStamp",
                         "AdminPosition", "ApprovalAuthority", "JobGroup", "PartyRole", "IsActive", "IsDeleted", "IsPartyMember", "CreatedAt")
                    VALUES ('{Guid.NewGuid()}', 'NGUYEN.VAN.A', 'Trùng', '', '', '', 'x', 's', 1, 1, 1, 1, TRUE, FALSE, TRUE, now());
                    """));
                Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
                Assert.Equal("IX_party_member_profiles_Username_lower", duplicate.ConstraintName);

                var role = await db.Roles.AsNoTracking().SingleAsync(r => r.Id == AdminRole);
                Assert.False(role.IsProtected); // seeder đánh dấu và bổ sung quyền quản trị khi khởi động
            }

            // Khởi động ứng dụng (seeder): vai trò quản trị cũ được bảo vệ + có quyền quản trị → admin vẫn quản trị được.
            await using (var db = NewContext(options))
                await DataSeeder.SeedAsync(db, seedSampleData: false);
            await using (var db = NewContext(options))
            {
                // ValidFrom = now() của máy chủ CSDL; đồng hồ máy chạy test có thể chậm hơn vài giây → chờ qua mốc đó.
                var validFrom = await db.Set<Domain.Entities.UserRoleAssignment>().MaxAsync(a => a.ValidFrom);
                var wait = validFrom - DateTime.UtcNow;
                if (wait > TimeSpan.Zero)
                    await Task.Delay(wait + TimeSpan.FromMilliseconds(200));

                var resolver = new PermissionResolver(new RoleAssignmentRepository(db), new PermissionCache());
                var admin = await resolver.GetAsync(Admin);
                Assert.Contains(admin.GrantsFor(PermissionCodes.SystemAssignmentsManage), g => g.ScopeType == ScopeType.Global);
                Assert.Contains("Quản trị hệ thống", admin.RoleNames);
            }

            // Down: bảng user_roles được dựng lại từ bản gán Toàn công ty đang hiệu lực.
            await using (var db = NewContext(options))
                await db.GetService<IMigrator>().MigrateAsync("Wave3");
            Assert.Equal(3L, await ScalarAsync<long>(connectionString, "SELECT count(*) FROM user_roles"));
            Assert.False(await ScalarAsync<bool>(connectionString, "SELECT to_regclass('user_role_assignments') IS NOT NULL"));
        });
    }

    [SkippableFact]
    public async Task Wave3Data_WithUsernamesDifferingOnlyByCase_FailsWithClearMessage()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            var options = Options(connectionString);
            await using (var db = NewContext(options))
                await db.GetService<IMigrator>().MigrateAsync("Wave3");

            await ExecuteAsync(connectionString, $"""
                INSERT INTO party_member_profiles
                    ("Id", "Username", "FullName", "Email", "PhoneNumber", "PositionTitle", "PasswordHash", "SecurityStamp",
                     "AdminPosition", "ApprovalAuthority", "JobGroup", "PartyRole", "IsActive", "IsDeleted", "IsPartyMember", "CreatedAt") VALUES
                    ('{Guid.NewGuid()}', 'TranVanB', 'Trần Văn B', '', '', '', 'x', 's1', 1, 1, 1, 1, TRUE, FALSE, TRUE, now()),
                    ('{Guid.NewGuid()}', 'tranvanb', 'Trần Văn B (2)', '', '', '', 'x', 's2', 1, 1, 1, 1, TRUE, TRUE, TRUE, now());
                """);

            await using var db2 = NewContext(options);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => db2.Database.MigrateAsync());
            Assert.Contains("tên đăng nhập trùng", ex.MessageText);
            Assert.Contains("tranvanb", ex.MessageText);
            // Không tự sửa: dữ liệu giữ nguyên, CSDL vẫn ở Wave3.
            Assert.Equal(2L, await ScalarAsync<long>(connectionString,
                "SELECT count(*) FROM party_member_profiles WHERE \"Username\" IN ('TranVanB', 'tranvanb')"));
            Assert.Contains("Wave4", string.Join(",", await db2.Database.GetPendingMigrationsAsync()));
        });
    }

    #region Hỗ trợ

    private static DbContextOptions<CongTacDangDbContext> Options(string connectionString) =>
        new DbContextOptionsBuilder<CongTacDangDbContext>().UseNpgsql(connectionString).Options;

    private static CongTacDangDbContext NewContext(DbContextOptions<CongTacDangDbContext> options) => new(options, new MigrationUser());

    private static async Task WithDatabaseAsync(Func<string, Task> test)
    {
        Skip.If(string.IsNullOrWhiteSpace(AdminConnection), $"Bỏ qua: chưa đặt biến môi trường {ApiFactory.ConnectionEnvVar}.");
        var admin = new NpgsqlConnectionStringBuilder(AdminConnection);
        TestDatabaseNames.EnsureNotForbidden(admin.Database);
        var name = TestDatabaseNames.NewName(DateTime.UtcNow);
        TestDatabaseNames.EnsureSafe(name);

        await ExecuteAsync(admin.ConnectionString, $"CREATE DATABASE \"{name}\"");
        try
        {
            await test(new NpgsqlConnectionStringBuilder(AdminConnection) { Database = name }.ConnectionString);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            TestDatabaseNames.EnsureSafe(name);
            await DropWithRetryAsync(admin.ConnectionString, name);
        }
    }

    /// <summary>Xóa CSDL tạm; máy chủ dùng chung có thể bận (khóa catalog) → thử lại vài lần, lỗi cuối nêu rõ lý do.</summary>
    private static async Task DropWithRetryAsync(string adminConnectionString, string name)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await ExecuteAsync(adminConnectionString, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
                return;
            }
            catch (PostgresException ex) when (attempt < 5)
            {
                Console.WriteLine($"Chưa xóa được CSDL tạm {name} (lần {attempt}): {ex.SqlState} {ex.MessageText}");
                await Task.Delay(TimeSpan.FromSeconds(attempt));
            }
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private sealed class MigrationUser : ICurrentUserService
    {
        public Guid? UserId => null;
        public string UserName => "migration-test";
        public string? IpAddress => null;
        public string? UserAgent => null;
        public string? RequestPath => null;
    }

    #endregion
}
