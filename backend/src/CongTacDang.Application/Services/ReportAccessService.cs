using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Services;

/// <summary>Kiểm tra phạm vi dữ liệu trước khi kết xuất biểu mẫu/báo cáo.</summary>
public interface IReportAccessService
{
    /// <summary>Bảo đảm người yêu cầu được xuất biểu mẫu của một hồ sơ đánh giá cá nhân (Mẫu 01, 02, 10).</summary>
    Task EnsureCanExportRecordAsync(Guid requesterId, Guid recordId);

    /// <summary>
    /// Xác định Chi bộ được xuất biểu mẫu theo Chi bộ (Mẫu 11, 13).
    /// Trả về null khi người yêu cầu được xuất toàn Đảng bộ và không chọn Chi bộ.
    /// </summary>
    Task<Guid?> ResolveBranchExportScopeAsync(Guid requesterId, Guid? branchId);

    /// <summary>Bảo đảm người yêu cầu được xuất báo cáo tổng hợp toàn Đảng bộ (Excel Mẫu 14, 15, 15A, 15B, 16).</summary>
    Task EnsureCanExportOrganizationReportAsync(Guid requesterId);
}

/// <summary>Triển khai kiểm tra phạm vi kết xuất dựa trên <see cref="IAccessPolicy"/>.</summary>
public class ReportAccessService : IReportAccessService
{
    private readonly IUserRepository _userRepo;
    private readonly IEvaluationRepository _evaluationRepo;
    private readonly IAccessPolicy _accessPolicy;

    public ReportAccessService(IUserRepository userRepo, IEvaluationRepository evaluationRepo, IAccessPolicy accessPolicy)
    {
        _userRepo = userRepo;
        _evaluationRepo = evaluationRepo;
        _accessPolicy = accessPolicy;
    }

    /// <inheritdoc />
    public async Task EnsureCanExportRecordAsync(Guid requesterId, Guid recordId)
    {
        var requester = await GetRequesterAsync(requesterId);
        var record = await _evaluationRepo.GetRecordByIdAsync(recordId)
            ?? throw new KeyNotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {recordId}");

        if (!_accessPolicy.CanAccessRecord(requester, record, AccessOperation.Export))
            throw new ForbiddenException("Bạn không có quyền xuất biểu mẫu của hồ sơ đánh giá này.");
    }

    /// <inheritdoc />
    public async Task<Guid?> ResolveBranchExportScopeAsync(Guid requesterId, Guid? branchId)
    {
        var requester = await GetRequesterAsync(requesterId);
        var requestedBranch = branchId.HasValue && branchId.Value != Guid.Empty ? branchId : null;

        // Không chọn Chi bộ: cấp cao được xuất toàn Đảng bộ, Chi bộ mặc định xuất Chi bộ của mình.
        if (requestedBranch == null && !_accessPolicy.CanAccessBranch(requester, null, AccessOperation.Export))
            requestedBranch = requester.PartyCellId;

        if (!_accessPolicy.CanAccessBranch(requester, requestedBranch, AccessOperation.Export))
            throw new ForbiddenException("Bạn chỉ được xuất biểu mẫu của Chi bộ mình.");

        return requestedBranch;
    }

    /// <inheritdoc />
    public async Task EnsureCanExportOrganizationReportAsync(Guid requesterId)
    {
        var requester = await GetRequesterAsync(requesterId);
        if (!_accessPolicy.CanAccessBranch(requester, null, AccessOperation.Export))
            throw new ForbiddenException("Báo cáo tổng hợp toàn Đảng bộ chỉ dành cho cấp thẩm định, phê duyệt hoặc quản trị.");
    }

    private async Task<PartyMemberProfile> GetRequesterAsync(Guid requesterId)
    {
        return await _userRepo.GetWithRolesAndPermissionsByIdAsync(requesterId)
            ?? throw new ForbiddenException("Không tìm thấy hồ sơ người dùng hiện tại.");
    }
}
