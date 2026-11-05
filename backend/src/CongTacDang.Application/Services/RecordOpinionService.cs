using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.Services;

/// <summary>Ý kiến ghi nhận trước phê duyệt danh mục và trước nhận xét; không chặn bước nào của luồng.</summary>
public interface IRecordOpinionService
{
    /// <summary><c>evaluation.read</c> trên hồ sơ.</summary>
    Task<RecordOpinionsDto> GetAsync(Guid recordId, CancellationToken ct = default);

    /// <summary><c>evaluation.opinion.record</c> trên hồ sơ, không phải chủ hồ sơ, hồ sơ chưa công bố.</summary>
    Task<RecordOpinionsDto> CreateAsync(Guid recordId, CreateRecordOpinionDto request, CancellationToken ct = default);
}

/// <summary>Việc "Ghi ý kiến" của Phó Giám đốc phụ trách: hồ sơ chờ duyệt danh mục hoặc chờ nhận xét, mình chưa ghi ý kiến.</summary>
public interface IRecordOpinionWorkQueue
{
    /// <summary>Nhóm rỗng không trả về.</summary>
    Task<List<WorkQueueGroupDto>> GetGroupsAsync(Guid? periodId, CancellationToken ct = default);
}

public sealed class RecordOpinionService : IRecordOpinionService, IRecordOpinionWorkQueue
{
    public const int MaxContentLength = 4000;

    public const string WorkQueueGroup = "RECORD_OPINIONS";

    /// <summary>Quyền của Phòng TCCB-LĐ: rà soát danh mục, thẩm định.</summary>
    private static readonly string[] HrCodes = { PermissionCodes.EvaluationTasksReview, PermissionCodes.EvaluationAppraise };

    /// <summary>Quyền của lãnh đạo đơn vị, thư ký: người họp rà soát danh mục.</summary>
    private static readonly string[] MeetingCodes =
    {
        PermissionCodes.EvaluationTasksForward, PermissionCodes.EvaluationTasksApprove, PermissionCodes.EvaluationCollectiveRecord,
        PermissionCodes.EvaluationUnitReview
    };

    private static readonly string[] NonDeputyCodes = HrCodes.Concat(MeetingCodes).ToArray();

    private readonly IEvaluationWorkflowRepository _records;
    private readonly IRecordOpinionRepository _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;
    private readonly IStepPerformers? _stepPerformers;

    public RecordOpinionService(
        IEvaluationWorkflowRepository records,
        IRecordOpinionRepository repo,
        IUnitOfWork unitOfWork,
        IAuthorizationGuard guard,
        ICurrentUserService currentUser,
        IStepPerformers? stepPerformers = null)
    {
        _stepPerformers = stepPerformers;
        _records = records;
        _repo = repo;
        _unitOfWork = unitOfWork;
        _guard = guard;
        _currentUser = currentUser;
    }

    public static string KindName(RecordOpinionKind kind) => kind switch
    {
        RecordOpinionKind.ListMeeting => "Kết quả họp rà soát danh mục",
        RecordOpinionKind.ListCheck => "Kết quả rà soát hợp lệ",
        RecordOpinionKind.DeputyOnList => "Ý kiến Phó Giám đốc phụ trách về danh mục",
        RecordOpinionKind.DeputyOnReview => "Ý kiến Phó Giám đốc phụ trách về đề xuất mức",
        _ => kind.ToString()
    };

    /// <summary>
    /// Loại ý kiến người ghi chọn được, loại mặc định trước, theo quyền của người ghi trên hồ sơ. Không có quyền của TCCB-LĐ hay lãnh đạo
    /// đơn vị thì là Phó Giám đốc phụ trách, mặc định theo giai đoạn của hồ sơ.
    /// </summary>
    public static List<RecordOpinionKind> KindsFor(Func<string, bool> can, RecordStatus status)
    {
        ArgumentNullException.ThrowIfNull(can);
        if (HrCodes.Any(can))
            return new List<RecordOpinionKind> { RecordOpinionKind.ListMeeting, RecordOpinionKind.ListCheck };
        if (MeetingCodes.Any(can))
            return new List<RecordOpinionKind> { RecordOpinionKind.ListMeeting };
        return IsListPhase(status)
            ? new List<RecordOpinionKind> { RecordOpinionKind.DeputyOnList, RecordOpinionKind.DeputyOnReview }
            : new List<RecordOpinionKind> { RecordOpinionKind.DeputyOnReview, RecordOpinionKind.DeputyOnList };
    }

    /// <summary>Hồ sơ còn ở bước 1 (danh mục sản phẩm).</summary>
    public static bool IsListPhase(RecordStatus status) => status is RecordStatus.AwaitingRegistration or RecordStatus.AwaitingUnitSubmit
        or RecordStatus.AwaitingTaskReview or RecordStatus.AwaitingTaskApproval;

    /// <summary>Loại ý kiến Phó Giám đốc phụ trách cần ghi ở trạng thái này; null khi không chờ ý kiến.</summary>
    public static RecordOpinionKind? DeputyKindAwaited(RecordStatus status) => status switch
    {
        RecordStatus.AwaitingTaskApproval => RecordOpinionKind.DeputyOnList,
        RecordStatus.AwaitingDirectorReview => RecordOpinionKind.DeputyOnReview,
        _ => null
    };

    /// <inheritdoc />
    public async Task<RecordOpinionsDto> GetAsync(Guid recordId, CancellationToken ct = default)
    {
        var record = await RequireRecordAsync(recordId, ct);
        // Ý kiến nội bộ trước phê duyệt không hiện cho chính người được đánh giá.
        if (IsOwner(record))
            throw new ForbiddenException("Bạn không xem ý kiến trước phê duyệt trên hồ sơ của chính mình.");
        _guard.Ensure(PermissionCodes.EvaluationRead, AccessTarget.ForRecord(record));
        return await BuildAsync(record, ct);
    }

    /// <inheritdoc />
    public async Task<RecordOpinionsDto> CreateAsync(Guid recordId, CreateRecordOpinionDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var record = await RequireRecordAsync(recordId, ct);
        _guard.Ensure(PermissionCodes.EvaluationOpinionRecord, AccessTarget.ForRecord(record));
        if (IsOwner(record))
            throw new ForbiddenException("Bạn không ghi ý kiến trên hồ sơ của chính mình.");
        if (record.Status == RecordStatus.Published)
            throw new ConflictException("Hồ sơ đã công bố, không ghi thêm ý kiến.");
        if (!Enum.TryParse<RecordOpinionKind>(request.Kind?.Trim(), true, out var kind) || !Enum.IsDefined(kind))
            throw new ValidationException("Hãy chọn loại ý kiến.");

        var content = request.Content?.Trim();
        if (string.IsNullOrEmpty(content))
            throw new ValidationException("Hãy nhập nội dung ý kiến.");
        if (content.Length > MaxContentLength)
            throw new ValidationException($"Nội dung ý kiến không được dài quá {MaxContentLength} ký tự.");

        var now = DateTime.UtcNow;
        var name = _currentUser.UserId is { } id ? await _records.GetMemberNameAsync(id, ct) : null;
        _repo.Add(new RecordOpinion
        {
            RecordId = record.Id,
            Kind = kind,
            Content = content,
            RecordedById = _currentUser.UserId,
            RecordedByName = name ?? _currentUser.UserName,
            RecordedAt = now,
            CreatedAt = now
        });
        await _unitOfWork.SaveChangesAsync(ct);
        return await BuildAsync(record, ct);
    }

    private async Task<RecordOpinionsDto> BuildAsync(EvaluationRecord record, CancellationToken ct)
    {
        var items = await _repo.ListByRecordAsync(record.Id, ct);
        var target = AccessTarget.ForRecord(record);
        return new RecordOpinionsDto
        {
            RecordId = record.Id,
            CanRecord = !IsOwner(record) && record.Status != RecordStatus.Published
                && _guard.Can(PermissionCodes.EvaluationOpinionRecord, target),
            Kinds = KindsFor(code => _guard.Can(code, target), record.Status)
                .Select(k => new RecordOpinionKindDto { Value = k.ToString(), Name = KindName(k) })
                .ToList(),
            Items = items.Select(o => new RecordOpinionDto
            {
                Id = o.Id,
                Kind = o.Kind.ToString(),
                KindName = KindName(o.Kind),
                Content = o.Content,
                RecordedByName = o.RecordedByName,
                RecordedAt = o.RecordedAt
            }).ToList()
        };
    }

    /// <inheritdoc />
    public async Task<List<WorkQueueGroupDto>> GetGroupsAsync(Guid? periodId, CancellationToken ct = default)
    {
        var none = new List<WorkQueueGroupDto>();
        if (_currentUser.UserId is not { } userId || !_guard.HasAny(PermissionCodes.EvaluationOpinionRecord))
            return none;
        var periods = (await _records.ListPeriodsAsync(ct))
            .Where(p => p.Status is PeriodStatus.Open or PeriodStatus.Locked && (periodId is null || periodId == Guid.Empty || p.Id == periodId))
            .ToDictionary(p => p.Id);
        if (periods.Count == 0)
            return none;

        var records = await _records.ListRecordsByStatusAsync(periods.Keys.ToList(),
            new[] { RecordStatus.AwaitingTaskApproval, RecordStatus.AwaitingDirectorReview }, ct);
        var candidates = new List<EvaluationRecord>();
        foreach (var record in records.Where(r => r.MemberId != userId))
        {
            var target = AccessTarget.ForRecord(record);
            if (!_guard.Can(PermissionCodes.EvaluationOpinionRecord, target) || NonDeputyCodes.Any(code => _guard.Can(code, target)))
                continue;
            // Giao người gần hồ sơ nhất trong số Phó Giám đốc phụ trách (bỏ người có quyền của TCCB-LĐ, lãnh đạo đơn vị).
            if (_stepPerformers != null
                && !(await _stepPerformers.ListAsync(PermissionCodes.EvaluationOpinionRecord, target, NonDeputyCodes, ct)).Contains(userId))
                continue;
            candidates.Add(record);
        }
        if (candidates.Count == 0)
            return none;

        var written = await _repo.ListKindsByAuthorAsync(userId, candidates.Select(r => r.Id).ToList(), ct);
        var items = candidates
            .Where(r => !written.Contains((r.Id, DeputyKindAwaited(r.Status)!.Value)))
            .Select(r => new WorkQueueItemDto
            {
                RecordId = r.Id,
                Version = r.Version,
                PeriodId = r.PeriodId,
                PeriodName = periods[r.PeriodId].Name,
                MemberId = r.MemberId,
                FullName = r.Member?.FullName ?? string.Empty,
                DepartmentName = r.Department?.Name,
                PartyCellName = r.PartyCell?.Name,
                ApprovalAuthority = r.ApprovalAuthority.ToString(),
                WorkflowProfileName = string.Empty,
                Status = r.Status.ToString(),
                StatusDisplayName = r.Status == RecordStatus.AwaitingTaskApproval ? "Chờ duyệt danh mục, bạn chưa ghi ý kiến" : "Chờ nhận xét, bạn chưa ghi ý kiến",
                Mode = nameof(StepMode.Internal)
            })
            .OrderBy(i => i.PeriodName).ThenBy(i => i.FullName)
            .ToList();
        return items.Count == 0
            ? none
            : new List<WorkQueueGroupDto> { new() { Step = WorkQueueGroup, StepName = "Ghi ý kiến", Count = items.Count, Items = items } };
    }

    private bool IsOwner(EvaluationRecord record) => _currentUser.UserId is { } id && record.MemberId == id;

    private async Task<EvaluationRecord> RequireRecordAsync(Guid recordId, CancellationToken ct) =>
        await _records.FindRecordAsync(recordId, ct)
        ?? throw new NotFoundException("Không tìm thấy hồ sơ đánh giá.");
}
