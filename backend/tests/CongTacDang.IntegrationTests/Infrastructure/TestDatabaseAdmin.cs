using Npgsql;

namespace CongTacDang.IntegrationTests.Infrastructure;

/// <summary>
/// Nơi duy nhất tạo/xóa CSDL tạm <c>ctd_it_*</c> của test tích hợp trên máy chủ PostgreSQL dùng chung.
/// <list type="bullet">
/// <item><c>CREATE DATABASE … TEMPLATE template0</c>: <c>template1</c> cho phép kết nối (autovacuum, công cụ quản trị, phiên của
/// người khác trên máy chủ dùng chung), khi đó <c>CREATE DATABASE</c> mặc định báo lỗi 55006 "source database template1 is being
/// accessed by other users" — nguyên nhân lỗi thoáng qua ở bước tạo CSDL. <c>template0</c> không nhận kết nối nên không bị chặn.</item>
/// <item>Tạo/xóa trong một tiến trình được tuần tự hóa; lỗi tạm thời của máy chủ (đối tượng đang dùng, quá nhiều kết nối, hết thời gian
/// kết nối…) được thử lại có giãn cách.</item>
/// <item>Chuỗi kết nối bật <c>Include Error Detail</c> để lỗi của máy chủ hiện đủ chi tiết trong kết quả test.</item>
/// <item>Xóa CSDL chỉ xóa pool kết nối của đúng CSDL đó (không <c>ClearAllPools</c> ảnh hưởng tới host/CSDL khác trong tiến trình).</item>
/// </list>
/// </summary>
public static class TestDatabaseAdmin
{
    private const int MaxAttempts = 5;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>Chuỗi kết nối quản trị (CSDL <c>postgres</c>) đã chuẩn hóa từ <c>CONGTACDANG_TEST_PG</c>.</summary>
    public static string AdminConnectionString(string raw)
    {
        var builder = new NpgsqlConnectionStringBuilder(raw) { IncludeErrorDetail = true };
        TestDatabaseNames.EnsureNotForbidden(builder.Database);
        if (builder.Timeout < 30)
            builder.Timeout = 30;
        return builder.ConnectionString;
    }

    /// <summary>Chuỗi kết nối tới CSDL tạm <paramref name="name"/> (cùng máy chủ, tài khoản với chuỗi quản trị).</summary>
    public static string TargetConnectionString(string adminConnectionString, string name)
    {
        TestDatabaseNames.EnsureSafe(name);
        return new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = name }.ConnectionString;
    }

    /// <summary>Tạo CSDL tạm rỗng từ <c>template0</c>.</summary>
    public static async Task CreateAsync(string adminConnectionString, string name)
    {
        TestDatabaseNames.EnsureSafe(name);
        await Gate.WaitAsync();
        try
        {
            await RetryAsync($"tạo CSDL test {name}", async attempt =>
            {
                try
                {
                    await ExecuteAsync(adminConnectionString, $"CREATE DATABASE \"{name}\" TEMPLATE template0");
                }
                catch (PostgresException ex) when (attempt > 1 && ex.SqlState == PostgresErrorCodes.DuplicateDatabase)
                {
                    // Lần thử trước đã tạo xong phía máy chủ nhưng máy khách không nhận được kết quả (tên CSDL là duy nhất).
                }
            });
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Xóa CSDL tạm (ngắt mọi kết nối còn lại tới nó).</summary>
    public static async Task DropAsync(string adminConnectionString, string name)
    {
        TestDatabaseNames.EnsureSafe(name);
        await using (var connection = new NpgsqlConnection(TargetConnectionString(adminConnectionString, name)))
            NpgsqlConnection.ClearPool(connection);

        await Gate.WaitAsync();
        try
        {
            await RetryAsync($"xóa CSDL test {name}",
                _ => ExecuteAsync(adminConnectionString, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)"));
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Tên các CSDL <c>ctd_it_*</c> hiện có trên máy chủ.</summary>
    public static async Task<List<string>> ListAsync(string adminConnectionString)
    {
        var names = new List<string>();
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT datname FROM pg_database WHERE datname LIKE 'ctd\\_it\\_%'", connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            names.Add(reader.GetString(0));
        return names;
    }

    /// <summary>Lỗi tạm thời của máy chủ dùng chung — thử lại được.</summary>
    public static bool IsTransient(Exception ex) => ex switch
    {
        PostgresException pg => pg.SqlState is PostgresErrorCodes.ObjectInUse
            or PostgresErrorCodes.TooManyConnections
            or PostgresErrorCodes.CannotConnectNow
            or PostgresErrorCodes.InsufficientResources
            or PostgresErrorCodes.DeadlockDetected
            or PostgresErrorCodes.LockNotAvailable,
        NpgsqlException npgsql => npgsql.IsTransient,
        TimeoutException => true,
        _ => false
    };

    private static async Task RetryAsync(string operation, Func<int, Task> action)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await action(attempt);
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsTransient(ex))
            {
                Console.WriteLine($"Lỗi tạm thời khi {operation} (lần {attempt}/{MaxAttempts}), thử lại: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)));
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
}
