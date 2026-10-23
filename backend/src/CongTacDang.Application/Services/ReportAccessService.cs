using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;

namespace CongTacDang.Application.Services;

/// <summary>Kiểm tra phạm vi dữ liệu trước khi kết xuất biểu mẫu/báo cáo.</summary>
public interface IReportAccessService
{
    /// <summary>Cần quyền xem hồ sơ.</summary>
    Task EnsureCanExportRecordAsync(Guid requesterId, Guid recordId);

    /// <summary>Theo phạm vi một trong <paramref name="permissions"/> (mặc định <c>report.export</c>); null = toàn Đảng bộ.</summary>
    Task<Guid?> ResolveBranchExportScopeAsync(Guid requesterId, Guid? branchId, params string[] permissions);

    /// <summary>Cần <c>report.export</c> phạm vi Toàn công ty.</summary>
    Task EnsureCanExportOrganizationReportAsync(Guid requesterId);

    ScopeFilter GetCadreExportScope();

    /// <summary>Như quyền xem hồ sơ tập thể: <c>evaluation.read</c> hoặc <c>collective.manage</c> trên tổ chức của hồ sơ.</summary>
    Task EnsureCanExportCollectiveAsync(Guid collectiveRecordId);

    /// <summary>Cần <c>meeting.read</c> hoặc <c>meeting.manage</c> trên đơn vị của biên bản.</summary>
    Task EnsureCanExportMeetingAsync(Guid meetingId);
}

public class ReportAccessService : IReportAccessService
{
    private readonly IEvaluationRepository _evaluationRepo;
    private readonly ICollectiveEvaluationRepository _collectiveRepo;
    private readonly IEvaluationMeetingRepository _meetingRepo;
    private readonly IAuthorizationGuard _guard;

    public ReportAccessService(
        IEvaluationRepository evaluationRepo,
        ICollectiveEvaluationRepository collectiveRepo,
        IEvaluationMeetingRepository meetingRepo,
        IAuthorizationGuard guard)
    {
        _evaluationRepo = evaluationRepo;
        _collectiveRepo = collectiveRepo;
        _meetingRepo = meetingRepo;
        _guard = guard;
    }

    /// <inheritdoc />
    public async Task EnsureCanExportCollectiveAsync(Guid collectiveRecordId)
    {
        var record = await _collectiveRepo.GetByIdAsync(collectiveRecordId)
            ?? throw new KeyNotFoundException($"Không tìm thấy hồ sơ tập thể với Id: {collectiveRecordId}");
        var target = new AccessTarget(DepartmentId: record.DepartmentId, PartyCellId: record.PartyCellId);
        if (!_guard.Can(PermissionCodes.EvaluationRead, target) && !_guard.Can(PermissionCodes.CollectiveManage, target))
            throw new ForbiddenException(
                $"Bạn không có quyền \"{PermissionCodes.DisplayName(PermissionCodes.EvaluationRead)}\" hoặc \"{PermissionCodes.DisplayName(PermissionCodes.CollectiveManage)}\" "
                + "đối với tổ chức của hồ sơ tập thể này (ngoài phạm vi được gán).");
    }

    /// <inheritdoc />
    public async Task EnsureCanExportMeetingAsync(Guid meetingId)
    {
        var meeting = await _meetingRepo.GetByIdAsync(meetingId)
            ?? throw new KeyNotFoundException($"Không tìm thấy biên bản hội nghị với Id: {meetingId}");
        var target = new AccessTarget(DepartmentId: meeting.DepartmentId, PartyCellId: meeting.PartyCellId);
        if (!_guard.Can(PermissionCodes.MeetingRead, target) && !_guard.Can(PermissionCodes.MeetingManage, target))
            throw new ForbiddenException(
                $"Bạn không có quyền \"{PermissionCodes.DisplayName(PermissionCodes.MeetingRead)}\" đối với đơn vị của biên bản này (ngoài phạm vi được gán).");
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
