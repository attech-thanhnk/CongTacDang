using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>
/// Dựng model EF (provider Npgsql, không kết nối CSDL) và bắt cảnh báo khi dựng model (T-70).
/// </summary>
public class DbModelWarningTests
{
    [Fact]
    public void Model_HasNoRequiredNavigationQueryFilterWarning()
    {
        var messages = new List<string>();
        var options = new DbContextOptionsBuilder<CongTacDangDbContext>()
            .UseNpgsql("Host=localhost;Database=ctd_model_only;Username=x;Password=x")
            // Không dùng chung service provider nội bộ → model được dựng lại, cảnh báo được ghi trong test này.
            .EnableServiceProviderCaching(false)
            .LogTo(
                (eventId, _) => eventId == CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning,
                data => messages.Add(data.ToString()))
            .Options;

        using var db = new CongTacDangDbContext(options, new NoUser());
        Assert.NotNull(db.Model);

        Assert.True(messages.Count == 0,
            "Cảnh báo EF 10622 (query filter trên đầu bắt buộc của quan hệ):" + Environment.NewLine
            + string.Join(Environment.NewLine, messages));
    }

    private sealed class NoUser : ICurrentUserService
    {
        public Guid? UserId => null;
        public string UserName => "test";
        public string? IpAddress => null;
        public string? UserAgent => null;
        public string? RequestPath => null;
    }
}
