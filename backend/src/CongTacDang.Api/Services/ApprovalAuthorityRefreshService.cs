using System;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CongTacDang.Api.Services;

/// <summary>
/// Làm mới định kỳ thẩm quyền phê duyệt suy ra từ chức vụ, vì chức vụ có thời hạn tự hết/bắt đầu mà không có thao tác sửa.
/// </summary>
public sealed class ApprovalAuthorityRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ApprovalAuthorityRefreshService> _logger;
    private readonly TimeSpan _interval;

    public ApprovalAuthorityRefreshService(IServiceScopeFactory scopes, ILogger<ApprovalAuthorityRefreshService> logger, TimeSpan interval)
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
                var changed = await scope.ServiceProvider.GetRequiredService<IPositionService>()
                    .RecomputeAllApprovalAuthoritiesAsync(stoppingToken);
                if (changed > 0)
                    _logger.LogInformation("Đã cập nhật thẩm quyền phê duyệt suy ra từ chức vụ cho {Count} cán bộ.", changed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không tính lại được thẩm quyền phê duyệt suy ra từ chức vụ; sẽ thử lại ở chu kỳ sau.");
            }
        }
    }
}
