using System;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CongTacDang.Api.Services;

/// <summary>
/// Tác vụ nền dọn tệp tải lên chưa gắn đối tượng quá hạn (<c>Attachments:UnlinkedCleanup</c>: <c>Enabled</c>,
/// <c>RetentionHours</c> mặc định 24, <c>IntervalMinutes</c> mặc định 60).
/// </summary>
public sealed class UnlinkedAttachmentCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<UnlinkedAttachmentCleanupService> _logger;
    private readonly TimeSpan _interval;

    /// <summary>Khởi tạo tác vụ với chu kỳ dọn.</summary>
    public UnlinkedAttachmentCleanupService(IServiceScopeFactory scopes, ILogger<UnlinkedAttachmentCleanupService> logger, TimeSpan interval)
    {
        _scopes = scopes;
        _logger = logger;
        _interval = interval;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_interval <= TimeSpan.Zero)
            return;

        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var removed = await scope.ServiceProvider.GetRequiredService<IUnlinkedAttachmentCleaner>()
                    .CleanupAsync(DateTime.UtcNow, stoppingToken);
                if (removed > 0)
                    _logger.LogInformation("Đã dọn {Count} tệp tải lên chưa gắn vào đối tượng nào quá thời hạn.", removed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không dọn được tệp tải lên chưa gắn; sẽ thử lại ở chu kỳ sau.");
            }
        }
    }
}
