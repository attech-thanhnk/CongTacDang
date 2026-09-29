using System;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CongTacDang.Api.Services;

/// <summary>
/// Tác vụ nền tính lại thẩm quyền phê duyệt suy ra từ chức vụ (task 14): chức vụ có thời hạn tự bắt đầu/kết thúc theo thời gian
/// mà không có thao tác sửa nào, nên giá trị lưu trên hồ sơ cán bộ cần được làm mới định kỳ
/// (cấu hình <c>Organization:ApprovalAuthorityRefreshMinutes</c>, mặc định 60; 0 = tắt).
/// </summary>
public sealed class ApprovalAuthorityRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ApprovalAuthorityRefreshService> _logger;
    private readonly TimeSpan _interval;

    /// <summary>Khởi tạo tác vụ với chu kỳ làm mới.</summary>
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
