using CongTacDang.IntegrationTests.Infrastructure;
using Npgsql;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>Chốt an toàn tên CSDL trên máy chủ dùng chung (chạy không cần PostgreSQL).</summary>
public sealed class TestDatabaseNamesTests
{
    [Fact]
    public void NewName_IsOwnAndSafe()
    {
        var name = TestDatabaseNames.NewName(new DateTime(2026, 9, 28, 10, 11, 12, DateTimeKind.Utc));

        Assert.StartsWith("ctd_it_20260928101112_", name);
        Assert.True(TestDatabaseNames.IsOwnName(name));
        TestDatabaseNames.EnsureSafe(name);
    }

    [Theory]
    [InlineData("congtacdang_test")]
    [InlineData("postgres")]
    [InlineData("ctd_it_")]
    [InlineData("ctd_it_abc")]
    [InlineData("ctd_it_20260928101112_12345678\"; DROP DATABASE x; --")]
    public void EnsureSafe_RejectsForeignNames(string name)
    {
        Assert.Throws<InvalidOperationException>(() => TestDatabaseNames.EnsureSafe(name));
    }

    [Fact]
    public void EnsureNotForbidden_RejectsCoordinatorDatabase()
    {
        Assert.Throws<InvalidOperationException>(() => TestDatabaseNames.EnsureNotForbidden("congtacdang_test"));
        TestDatabaseNames.EnsureNotForbidden("postgres");
    }

    [Fact]
    public void IsStale_OnlyForOwnNamesOlderThan24Hours()
    {
        var now = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(TestDatabaseNames.IsStale("ctd_it_20260927110000_0123abcd", now));
        Assert.False(TestDatabaseNames.IsStale("ctd_it_20260927130000_0123abcd", now));
        Assert.False(TestDatabaseNames.IsStale("ctd_it_manual_backup", now));
        Assert.False(TestDatabaseNames.IsStale("congtacdang_test", now));
    }

    [Theory]
    [InlineData(PostgresErrorCodes.ObjectInUse, true)]          // "source database template1 is being accessed by other users"
    [InlineData(PostgresErrorCodes.TooManyConnections, true)]
    [InlineData(PostgresErrorCodes.CannotConnectNow, true)]
    [InlineData(PostgresErrorCodes.InternalError, true)]         // "tuple concurrently updated" (phiên khác sửa pg_database)
    [InlineData(PostgresErrorCodes.DuplicateDatabase, false)]
    [InlineData(PostgresErrorCodes.InsufficientPrivilege, false)]
    [InlineData(PostgresErrorCodes.InvalidPassword, false)]
    [InlineData(PostgresErrorCodes.InvalidCatalogName, false)]
    public void TestDatabaseAdmin_RetriesOnlyTransientServerErrors(string sqlState, bool transient)
    {
        Assert.Equal(transient, TestDatabaseAdmin.IsTransient(new PostgresException("x", "ERROR", "ERROR", sqlState)));
        Assert.True(TestDatabaseAdmin.IsTransient(new TimeoutException()));
        Assert.False(TestDatabaseAdmin.IsTransient(new InvalidOperationException()));
    }

    [Fact]
    public void TestDatabaseAdmin_ConnectionStrings_IncludeErrorDetail_RejectForbidden()
    {
        var admin = TestDatabaseAdmin.AdminConnectionString("Host=h;Database=postgres;Username=u;Password=p");
        var builder = new NpgsqlConnectionStringBuilder(admin);
        Assert.True(builder.IncludeErrorDetail);
        Assert.True(builder.Timeout >= 30);
        var target = new NpgsqlConnectionStringBuilder(TestDatabaseAdmin.TargetConnectionString(admin, "ctd_it_20260928101112_0123abcd"));
        Assert.Equal("ctd_it_20260928101112_0123abcd", target.Database);
        Assert.Throws<InvalidOperationException>(() => TestDatabaseAdmin.TargetConnectionString(admin, "congtacdang_test"));
        Assert.Throws<InvalidOperationException>(() => TestDatabaseAdmin.AdminConnectionString("Host=h;Database=congtacdang_test;Username=u"));
    }
}
