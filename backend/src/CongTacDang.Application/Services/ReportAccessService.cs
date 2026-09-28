using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;

namespace CongTacDang.Application.Services;

/// <summary>Kiểm tra phạm vi dữ liệu trước khi kết xuất biểu mẫu/báo cáo (qua <see cref="IAuthorizationGuard"/>).</summary>
public interface IReportAccessService
{
    /// <summary>Bảo đảm người yêu cầu được xuất biểu mẫu của một hồ sơ đánh giá cá nhân (Mẫu 01, 02, 10): quyền xem hồ sơ.</summary>
    Task EnsureCanExportRecordAsync(Guid requesterId, Guid recordId);

    /// <summary>
    /// Xác định Chi bộ được xuất biểu mẫu/báo cáo theo Chi bộ (Mẫu 11, 13, 14–16) theo phạm vi của một trong các quyền
    /// <paramref name="permissions"/> (mặc định <c>report.export</c>). Trả về null khi được xuất toàn Đảng bộ và không chọn Chi bộ.
    /// </summary>
    Task<Guid?> ResolveBranchExportScopeAsync(Guid requesterId, Guid? branchId, params string[] permissions);

    /// <summary>Bảo đảm người yêu cầu được xuất báo cáo tổng hợp toàn Đảng bộ (<c>report.export</c> phạm vi Toàn công ty).</summary>
    Task EnsureCanExportOrganizationReportAsync(Guid requesterId);

    /// <summary>Phạm vi danh sách cán bộ được xuất (T-61): theo phạm vi <c>report.export</c> (Toàn công ty / Phòng / Chi bộ).</summary>
    ScopeFilter GetCadreExportScope();
}

/// <summary>Triển khai kiểm tra phạm vi kết xuất dựa trên <see cref="IAuthorizationGuard"/>.</summary>
public class ReportAccessService : IReportAccessService
{
    private readonly IEvaluationRepository _evaluationRepo;
    private readonly IAuthorizationGuard _guard;

    public ReportAccessService(IEvaluationRepository evaluationRepo, IAuthorizationGuard guard)
    {
        _evaluationRepo = evaluationRepo;
        _guard = guard;
    }

    /// <inheritdoc />
    public async Task EnsureCanExportRecordAsync(Guid requesterId, Guid recordId)
    {
        var record = await _evaluationRepo.GetRecordByIdAsync(recordId)
            ?? throw new KeyNotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {recordId}");

        _guard.Ensure(PermissionCodes.EvaluationRead, AccessTarget.ForRecord(record));
    }

    /// <inheritdoc />
    public Task<Guid?> ResolveBranchExportScopeAsync(Guid requesterId, Guid? branchId, params string[] permissions)
    {
        var codes = permissions is { Length: > 0 } ? permissions : new[] { PermissionCodes.ReportExport };
        var scope = codes.Select(_guard.GetScope).Aggregate((a, b) => a.Union(b)) with { OwnerId = null };
        var names = string.Join(" hoặc ", codes.Select(c => $"\"{PermissionCodes.DisplayName(c)}\""));

        var requestedBranch = branchId.HasValue && branchId.Value != Guid.Empty ? branchId : null;
        if (requestedBranch.HasValue)
        {
            if (!scope.Matches(null, null, requestedBranch))
                throw new ForbiddenException($"Bạn không có quyền {names} đối với Chi bộ được chọn (ngoài phạm vi được gán).");
            return Task.FromResult(requestedBranch);
        }

        // Không chọn Chi bộ: toàn Đảng bộ nếu có phạm vi Toàn công ty; một Chi bộ duy nhất → Chi bộ đó.
        if (scope.IsGlobal)
            return Task.FromResult<Guid?>(null);
        if (scope.PartyCellIds.Count == 1)
            return Task.FromResult<Guid?>(scope.PartyCellIds[0]);
        if (scope.PartyCellIds.Count > 1)
            throw new ValidationException("Bạn được phân quyền trên nhiều Chi bộ. Hãy chọn Chi bộ cần xuất.");

        throw new ForbiddenException(
            $"Biểu mẫu theo Chi bộ cần quyền {names} ở phạm vi Chi bộ hoặc Toàn công ty. Hãy liên hệ quản trị hệ thống.");
    }

    /// <inheritdoc />
    public Task EnsureCanExportOrganizationReportAsync(Guid requesterId)
    {
        if (!_guard.GetScope(PermissionCodes.ReportExport).IsGlobal)
        {
            throw new ForbiddenException(
                $"Báo cáo tổng hợp toàn Đảng bộ cần quyền \"{PermissionCodes.DisplayName(PermissionCodes.ReportExport)}\" phạm vi Toàn công ty.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ScopeFilter GetCadreExportScope() => _guard.GetScope(PermissionCodes.ReportExport) with { OwnerId = null };
}
