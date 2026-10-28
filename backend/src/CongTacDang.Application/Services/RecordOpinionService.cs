using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Services;

/// <summary>Ý kiến ghi nhận trước phê duyệt danh mục và trước nhận xét; không chặn bước nào của luồng.</summary>
public interface IRecordOpinionService
{
    /// <summary><c>evaluation.read</c> trên hồ sơ.</summary>
    Task<RecordOpinionsDto> GetAsync(Guid recordId, CancellationToken ct = default);

    /// <summary><c>evaluation.opinion.record</c> trên hồ sơ, không phải chủ hồ sơ, hồ sơ chưa công bố.</summary>
    Task<RecordOpinionsDto> CreateAsync(Guid recordId, CreateRecordOpinionDto request, CancellationToken ct = default);
}

public sealed class RecordOpinionService : IRecordOpinionService
{
    public const int MaxContentLength = 4000;

    private readonly IEvaluationWorkflowRepository _records;
    private readonly IRecordOpinionRepository _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;

    public RecordOpinionService(
        IEvaluationWorkflowRepository records,
        IRecordOpinionRepository repo,
        IUnitOfWork unitOfWork,
        IAuthorizationGuard guard,
        ICurrentUserService currentUser)
    {
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

    /// <inheritdoc />
    public async Task<RecordOpinionsDto> GetAsync(Guid recordId, CancellationToken ct = default)
    {
        var record = await RequireRecordAsync(recordId, ct);
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
        return new RecordOpinionsDto
        {
            RecordId = record.Id,
            CanRecord = !IsOwner(record) && record.Status != RecordStatus.Published
                && _guard.Can(PermissionCodes.EvaluationOpinionRecord, AccessTarget.ForRecord(record)),
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

    private bool IsOwner(EvaluationRecord record) => _currentUser.UserId is { } id && record.MemberId == id;

    private async Task<EvaluationRecord> RequireRecordAsync(Guid recordId, CancellationToken ct) =>
        await _records.FindRecordAsync(recordId, ct)
        ?? throw new NotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {recordId}.");
}
