using System;
using CongTacDang.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CongTacDang.Infrastructure.Data;

/// <summary>Khởi tạo DbContext cho dotnet ef mà không cần kết nối CSDL thật.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CongTacDangDbContext>
{
    public CongTacDangDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=congtacdang_design;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<CongTacDangDbContext>()
            .UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .Options;

        return new CongTacDangDbContext(options, new DesignTimeCurrentUserService());
    }

    private sealed class DesignTimeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => null;
        public string UserName => "design-time";
        public string? IpAddress => null;
        public string? UserAgent => null;
        public string? RequestPath => null;
    }
}
