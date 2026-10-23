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

/// <summary>Kế hoạch hỗ trợ, khắc phục 30-60-90 ngày (Mẫu 17).</summary>
public interface IImprovementPlanService
{
    /// <summary>Có bắt buộc không, kế hoạch (nếu có) và quyền của người hiện tại.</summary>
    Task<RecordImprovementPlanDto> GetRecordPlanAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Hồ sơ đã công bố, chưa có kế hoạch.</summary>
    Task<ImprovementPlanDto> CreateAsync(Guid recordId, SaveImprovementPlanRequestDto request, CancellationToken ct = default);

    Task<ImprovementPlanDto> UpdateAsync(Guid planId, SaveImprovementPlanRequestDto request, CancellationToken ct = default);

    /// <summary>Đủ các mục bắt buộc → chờ cá nhân xác nhận.</summary>
    Task<ImprovementPlanDto> ApproveAsync(Guid planId, VersionedRequestDto request, CancellationToken ct = default);

    Task<ImprovementPlanDto> AcknowledgeAsync(Guid planId, AcknowledgePlanRequestDto request, CancellationToken ct = default);

    /// <summary>Ghi mốc 90 ngày thì đóng kế hoạch.</summary>
    Task<ImprovementPlanDto> RecordResultAsync(Guid planId, MilestoneResultRequestDto request, CancellationToken ct = default);

    /// <summary>Kiểm tra quyền xem.</summary>
    Task<(ImprovementPlan Plan, EvaluationRecord Record)> GetForExportAsync(Guid planId, CancellationToken ct = default);
}

public interface IImprovementPlanDocument
{
    /// <summary><paramref name="format"/> PDF → chuyển phía máy chủ.</summary>
    Task<ReportFileResult> RenderMau17Async(ImprovementPlan plan, EvaluationRecord record, ReportFormat format, CancellationToken ct = default);
}

/// <summary>
/// Bắt buộc khi mức chính thức thuộc <c>ImprovementPlanRequiredGrades</c> của bộ tiêu chí; hồ sơ công bố khác vẫn lập được.
/// Lập/sửa/duyệt/ghi kết quả: <c>evaluation.improvement.manage</c>, không trên hồ sơ của mình; xác nhận: chủ hồ sơ.
/// </summary>
public sealed class ImprovementPlanService : IImprovementPlanService
{
    private const int MaxCommentLength = 2000;

    /// <summary>Ngày bắt đầu sớm nhất = ngày công bố trừ số ngày này. Giá trị tạm, chờ nghiệp vụ xác nhận.</summary>
    public const int MaxStartDaysBeforePublish = 30;

    /// <summary>Ngày bắt đầu muộn nhất = ngày công bố cộng số ngày này. Giá trị tạm, chờ nghiệp vụ xác nhận.</summary>
    public const int MaxStartDaysAfterPublish = 180;

    private readonly IPostPublishRepository _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;

    public ImprovementPlanService(IPostPublishRepository repo, IUnitOfWork unitOfWork, IAuthorizationGuard guard, ICurrentUserService currentUser)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
        _guard = guard;
        _currentUser = currentUser;
    }

    /// <summary>Kỳ chưa có bộ / lỗi ảnh chụp → mặc định của tham số.</summary>
    public static IReadOnlyList<EvaluationGrade> RequiredGrades(EvaluationPeriod period)
    {
        try
        {
            var grades = period.GetCriteria()?.Content.Parameters.ImprovementPlanRequiredGrades;
            if (grades != null)
                return grades;
        }
        catch (FormatException)
        {
        }
        return new CriteriaParameters().ImprovementPlanRequiredGrades;
    }

    /// <summary>Đang mở/khóa dữ liệu, hoặc đã đóng chưa quá <see cref="CriteriaParameters.ImprovementPlanAlertDays"/> ngày.</summary>
    public static bool InAlertWindow(EvaluationPeriod period, DateTime utcNow)
    {
        if (period.Status is PeriodStatus.Open or PeriodStatus.Locked)
            return true;
        if (period.Status != PeriodStatus.Closed)
            return false;
        int days;
        try
        {
            days = period.GetCriteria()?.Content.Parameters.ImprovementPlanAlertDays ?? new CriteriaParameters().ImprovementPlanAlertDays;
        }
        catch (FormatException)
        {
            days = new CriteriaParameters().ImprovementPlanAlertDays;
        }
        var closedAt = period.StatusChangedAt ?? period.UpdatedAt ?? period.CreatedAt;
        return closedAt >= utcNow.AddDays(-days);
    }

    public static bool IsRequired(EvaluationRecord record) =>
        record.Status == RecordStatus.Published
        && record.FinalGrade != EvaluationGrade.ChuaXepLoai
        && RequiredGrades(record.Period).Contains(record.FinalGrade);

    /// <inheritdoc />
    public async Task<RecordImprovementPlanDto> GetRecordPlanAsync(Guid recordId, CancellationToken ct = default)
    {
        var record = await RequireRecordAsync(recordId, ct);
        EnsureCanView(record);
        var plan = await _repo.FindPlanByRecordAsync(recordId, ct);
        var canManage = CanManage(record);
        return new RecordImprovementPlanDto
        {
            RecordId = record.Id,
            IsPublished = record.Status == RecordStatus.Published,
            Required = IsRequired(record),
            FinalGrade = EvaluationMapping.GradeCode(record.FinalGrade),
            FinalGradeName = CriteriaSetContent.GradeName(record.FinalGrade),
            RequiredGradeNames = RequiredGrades(record.Period).Select(CriteriaSetContent.GradeName).ToList(),
            Plan = plan == null ? null : ToDto(plan),
            CanManage = canManage && record.Status == RecordStatus.Published,
            CanAcknowledge = plan?.Status == ImprovementPlanStatus.Approved
                && _guard.Can(PermissionCodes.EvaluationSelf, AccessTarget.ForRecord(record)),
            CanApprove = canManage && record.Status == RecordStatus.Published
                && plan?.Status == ImprovementPlanStatus.Draft && !IsPreparer(plan, _currentUser.UserId)
        };
    }

    /// <inheritdoc />
    public async Task<ImprovementPlanDto> CreateAsync(Guid recordId, SaveImprovementPlanRequestDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var record = await RequireRecordAsync(recordId, ct);
        _guard.Ensure(PermissionCodes.EvaluationImprovementManage, AccessTarget.ForRecord(record));
        if (record.Status != RecordStatus.Published)
            throw new ConflictException("Chỉ lập kế hoạch 30-60-90 ngày cho hồ sơ đã công bố kết quả.");
        if (await _repo.FindPlanByRecordAsync(recordId, ct) != null)
            throw new ConflictException("Hồ sơ đã có kế hoạch 30-60-90 ngày. Hãy tải lại trang để xem và sửa kế hoạch hiện có.");

        var content = BuildContent(new ImprovementPlanContent(), request);
        EnsureStartDate(record, request.StartDate);
        var now = DateTime.UtcNow;
        var plan = new ImprovementPlan
        {
            RecordId = record.Id,
            Content = content.ToJson(),
            StartDate = request.StartDate,
            Status = ImprovementPlanStatus.Draft,
            PreparedById = _currentUser.UserId,
            PreparedByName = await ActorNameAsync(ct),
            CreatedAt = now
        };
        _repo.AddPlan(plan);
        await _unitOfWork.SaveChangesAsync(ct);
        return ToDto(plan);
    }

    /// <inheritdoc />
    public Task<ImprovementPlanDto> UpdateAsync(Guid planId, SaveImprovementPlanRequestDto request, CancellationToken ct = default) =>
        ChangeAsync(planId, request, ManageRule, (plan, _) =>
        {
            if (plan.Status != ImprovementPlanStatus.Draft)
                throw new ConflictException("Kế hoạch đã được duyệt nên không sửa nội dung được nữa; chỉ ghi kết quả từng mốc.");
            plan.Content = BuildContent(ImprovementPlanContent.Parse(plan.Content), request).ToJson();
            if (request.StartDate != plan.StartDate)
                EnsureStartDate(plan.Record, request.StartDate);
            plan.StartDate = request.StartDate;
            return Task.CompletedTask;
        }, ct);

    /// <inheritdoc />
    public Task<ImprovementPlanDto> ApproveAsync(Guid planId, VersionedRequestDto request, CancellationToken ct = default) =>
        ChangeAsync(planId, request, ManageRule, async (plan, now) =>
        {
            if (plan.Status != ImprovementPlanStatus.Draft)
                throw new ConflictException("Kế hoạch đã được duyệt trước đó.");
            // Người lập không tự duyệt: cần người thứ hai kiểm soát nội dung kế hoạch.
            if (IsPreparer(plan, _currentUser.UserId))
                throw new ForbiddenException("Bạn là người lập kế hoạch này nên không tự duyệt được. Kế hoạch cần do người khác có quyền "
                    + $"\"{PermissionCodes.DisplayName(PermissionCodes.EvaluationImprovementManage)}\" duyệt.");
            var missing = ImprovementPlanContent.Parse(plan.Content).MissingForApproval();
            if (missing.Count > 0)
                throw new ValidationException("Kế hoạch chưa đủ nội dung theo Mẫu 17 để duyệt: " + string.Join(" ", missing));
            plan.Status = ImprovementPlanStatus.Approved;
            plan.ApprovedById = _currentUser.UserId;
            plan.ApprovedByName = await ActorNameAsync(ct);
            plan.ApprovedAt = now;
            plan.StartDate ??= EvaluationMapping.Today(now);
        }, ct);

    /// <inheritdoc />
    public Task<ImprovementPlanDto> AcknowledgeAsync(Guid planId, AcknowledgePlanRequestDto request, CancellationToken ct = default) =>
        ChangeAsync(planId, request, record => _guard.Ensure(PermissionCodes.EvaluationSelf, AccessTarget.ForRecord(record)), (plan, now) =>
        {
            if (plan.Status != ImprovementPlanStatus.Approved)
            {
                throw new ConflictException(plan.Status == ImprovementPlanStatus.Draft
                    ? "Kế hoạch chưa được thủ trưởng đơn vị duyệt nên chưa xác nhận được."
                    : "Bạn đã xác nhận kế hoạch này.");
            }
            var comment = request.Comment?.Trim();
            if (comment is { Length: > MaxCommentLength })
                throw new ValidationException($"Ý kiến dài tối đa {MaxCommentLength} ký tự.");
            plan.Status = ImprovementPlanStatus.Acknowledged;
            plan.AcknowledgedAt = now;
            plan.AcknowledgementComment = string.IsNullOrEmpty(comment) ? null : comment;
            return Task.CompletedTask;
        }, ct);

    /// <inheritdoc />
    public Task<ImprovementPlanDto> RecordResultAsync(Guid planId, MilestoneResultRequestDto request, CancellationToken ct = default) =>
        ChangeAsync(planId, request, ManageRule, async (plan, now) =>
        {
            if (plan.Status is ImprovementPlanStatus.Draft)
                throw new ConflictException("Kế hoạch chưa được duyệt nên chưa ghi kết quả mốc được.");
            if (plan.Status is ImprovementPlanStatus.Closed)
                throw new ConflictException("Kế hoạch đã đóng (đã đánh giá mốc 90 ngày).");
            var code = request.Milestone?.Trim().ToUpperInvariant();
            if (!ImprovementPlanContent.IsMilestone(code))
                throw new ValidationException("Hãy chọn mốc 30, 60 hoặc 90 ngày.");
            if (request.Result is not (ImprovementPlanContent.ResultAchieved or ImprovementPlanContent.ResultNotAchieved))
                throw new ValidationException("Hãy chọn kết quả của mốc (đạt / chưa đạt).");
            var note = request.Note?.Trim();
            if (note is { Length: > ImprovementPlanContent.MaxTextLength })
                throw new ValidationException($"Ghi chú kết quả dài tối đa {ImprovementPlanContent.MaxTextLength} ký tự.");

            var content = ImprovementPlanContent.Parse(plan.Content);
            // Ghi theo thứ tự mốc; không chặn theo ngày đến hạn (ghi sớm/muộn do nghiệp vụ quyết định).
            var missingEarlier = ImprovementPlanContent.Milestones
                .TakeWhile(m => m.Code != code)
                .Where(m => content.Stage(m.Code).Result == null)
                .Select(m => m.Name)
                .ToList();
            if (missingEarlier.Count > 0)
            {
                throw new ValidationException($"Chưa ghi được kết quả {ImprovementPlanContent.Milestones.First(m => m.Code == code).Name}: "
                    + $"hãy ghi kết quả theo thứ tự mốc, còn thiếu {string.Join(", ", missingEarlier)}.");
            }
            var stage = content.Stage(code!);
            stage.Result = request.Result;
            stage.ResultNote = string.IsNullOrEmpty(note) ? null : note;
            stage.ResultRecordedByName = await ActorNameAsync(ct);
            stage.ResultRecordedAt = now;
            plan.Content = content.ToJson();

            // Mốc 90 ngày: cả "Đạt" và "Không đạt" đều kết thúc kế hoạch.
            if (code == ImprovementPlanContent.M90)
            {
                plan.Status = ImprovementPlanStatus.Closed;
                plan.ClosedAt = now;
            }
        }, ct);

    /// <inheritdoc />
    public async Task<(ImprovementPlan Plan, EvaluationRecord Record)> GetForExportAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await _repo.FindPlanAsync(planId, ct)
            ?? throw new NotFoundException("Không tìm thấy kế hoạch 30-60-90 ngày.");
        EnsureCanView(plan.Record);
        return (plan, plan.Record);
    }

    #region Hỗ trợ

    /// <summary>Kế hoạch không lưu người lập (dữ liệu cũ) → không chặn.</summary>
    public static bool IsPreparer(ImprovementPlan plan, Guid? userId) =>
        userId.HasValue && plan.PreparedById == userId;

    private void ManageRule(EvaluationRecord record) =>
        _guard.Ensure(PermissionCodes.EvaluationImprovementManage, AccessTarget.ForRecord(record));

    private bool CanManage(EvaluationRecord record) =>
        _guard.Can(PermissionCodes.EvaluationImprovementManage, AccessTarget.ForRecord(record));

    private async Task<ImprovementPlanDto> ChangeAsync(
        Guid planId,
        VersionedRequestDto request,
        Action<EvaluationRecord> ensure,
        Func<ImprovementPlan, DateTime, Task> apply,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Version is null)
            throw new ValidationException("Thiếu phiên bản dữ liệu (version) của kế hoạch. Hãy tải lại trang rồi thực hiện lại.");

        var plan = await _repo.FindPlanAsync(planId, ct)
            ?? throw new NotFoundException("Không tìm thấy kế hoạch 30-60-90 ngày.");
        ensure(plan.Record);
        if (plan.Version != request.Version.Value)
            throw new ConflictException("Kế hoạch đã được người khác cập nhật sau khi bạn mở. Hãy tải lại để xem dữ liệu mới nhất.");

        var now = DateTime.UtcNow;
        await apply(plan, now);
        plan.UpdatedAt = now;
        _unitOfWork.SetOriginalVersion(plan, request.Version);
        await _unitOfWork.SaveChangesAsync(ct);

        var saved = await _repo.FindPlanAsync(planId, ct) ?? plan;
        return ToDto(saved);
    }

    /// <summary>Hồ sơ không có ngày công bố → không kiểm tra.</summary>
    private static void EnsureStartDate(EvaluationRecord record, DateOnly? startDate)
    {
        if (startDate is not { } start || record.PublishedAt is not { } publishedAt)
            return;
        var published = EvaluationMapping.Today(publishedAt);
        var earliest = published.AddDays(-MaxStartDaysBeforePublish);
        var latest = published.AddDays(MaxStartDaysAfterPublish);
        if (start < earliest || start > latest)
        {
            throw new ValidationException($"Ngày bắt đầu kế hoạch phải từ {Vn(earliest)} đến {Vn(latest)} "
                + $"(không sớm hơn {MaxStartDaysBeforePublish} ngày trước và không muộn hơn {MaxStartDaysAfterPublish} ngày sau ngày công bố "
                + $"kết quả {Vn(published)}).");
        }
    }

    private static string Vn(DateOnly date) => date.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

    private async Task<EvaluationRecord> RequireRecordAsync(Guid recordId, CancellationToken ct) =>
        await _repo.FindRecordAsync(recordId, ct)
        ?? throw new NotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {recordId}.");

    /// <summary>Chủ hồ sơ, người lập kế hoạch hoặc người xem hồ sơ trong phạm vi.</summary>
    private void EnsureCanView(EvaluationRecord record)
    {
        var target = AccessTarget.ForRecord(record);
        if (_guard.Can(PermissionCodes.EvaluationRead, target) || CanManage(record))
            return;
        throw new ForbiddenException(
            $"Bạn không có quyền \"{PermissionCodes.DisplayName(PermissionCodes.EvaluationRead)}\" hoặc "
            + $"\"{PermissionCodes.DisplayName(PermissionCodes.EvaluationImprovementManage)}\" trên hồ sơ này (ngoài phạm vi được gán).");
    }

    /// <summary>Chỉ các mục nội dung; kết quả mốc không đổi.</summary>
    private static ImprovementPlanContent BuildContent(ImprovementPlanContent content, SaveImprovementPlanRequestDto request)
    {
        content.SupporterName = request.SupporterName;
        content.SupporterTitle = request.SupporterTitle;
        foreach (var input in request.Milestones ?? new List<ImprovementMilestoneInputDto>())
        {
            var code = input.Code?.Trim().ToUpperInvariant();
            if (!ImprovementPlanContent.IsMilestone(code))
                throw new ValidationException($"Mốc \"{input.Code}\" không có trong Mẫu 17 (chỉ M30, M60, M90).");
            var stage = content.Stage(code!);
            stage.Limitation = input.Limitation;
            stage.Target = input.Target;
            stage.Measures = input.Measures;
            stage.Coordination = input.Coordination;
        }
        content.Normalize();
        var errors = content.ValidateLengths();
        if (errors.Count > 0)
            throw new ValidationException(string.Join(" ", errors));
        return content;
    }

    private async Task<string> ActorNameAsync(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        var name = userId.HasValue ? await _repo.GetMemberNameAsync(userId.Value, ct) : null;
        return string.IsNullOrWhiteSpace(name) ? _currentUser.UserName : name;
    }

    public static string StatusName(ImprovementPlanStatus status) => status switch
    {
        ImprovementPlanStatus.Draft => "Đang lập",
        ImprovementPlanStatus.Approved => "Đã duyệt, chờ cá nhân xác nhận",
        ImprovementPlanStatus.Acknowledged => "Đang thực hiện",
        ImprovementPlanStatus.Closed => "Đã đóng",
        _ => status.ToString()
    };

    public static ImprovementPlanDto ToDto(ImprovementPlan plan)
    {
        ImprovementPlanContent content;
        try
        {
            content = ImprovementPlanContent.Parse(plan.Content);
        }
        catch (FormatException)
        {
            content = new ImprovementPlanContent().Normalize();
        }

        return new ImprovementPlanDto
        {
            Id = plan.Id,
            Version = plan.Version,
            RecordId = plan.RecordId,
            Status = plan.Status.ToString(),
            StatusName = StatusName(plan.Status),
            StartDate = plan.StartDate,
            SupporterName = content.SupporterName,
            SupporterTitle = content.SupporterTitle,
            Milestones = ImprovementPlanContent.Milestones.Select(m =>
            {
                var stage = content.Stage(m.Code);
                return new ImprovementMilestoneDto
                {
                    Code = m.Code,
                    Name = m.Name,
                    Days = m.Days,
                    DueDate = plan.StartDate?.AddDays(m.Days),
                    Limitation = stage.Limitation,
                    Target = stage.Target,
                    Measures = stage.Measures,
                    Coordination = stage.Coordination,
                    Result = stage.Result,
                    ResultName = ImprovementPlanContent.ResultName(m.Code, stage.Result),
                    ResultNote = stage.ResultNote,
                    ResultRecordedByName = stage.ResultRecordedByName,
                    ResultRecordedAt = stage.ResultRecordedAt
                };
            }).ToList(),
            PreparedByName = plan.PreparedByName,
            ApprovedByName = plan.ApprovedByName,
            ApprovedAt = plan.ApprovedAt,
            AcknowledgedAt = plan.AcknowledgedAt,
            AcknowledgementComment = plan.AcknowledgementComment,
            ClosedAt = plan.ClosedAt,
            CreatedAt = plan.CreatedAt
        };
    }

    #endregion
}
