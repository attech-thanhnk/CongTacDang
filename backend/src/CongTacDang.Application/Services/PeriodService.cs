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

/// <summary>
/// Quản lý kỳ đánh giá (docs/thiet-ke/luong-danh-gia.md mục 3): tạo từ kiểu kỳ dựng sẵn (sinh sẵn các hồ sơ luồng), sửa cấu hình
/// (chỉ khi dự thảo — riêng thời hạn sửa được khi đang mở/khóa dữ liệu), danh sách người được đánh giá và hồ sơ luồng của từng
/// người, kiểm tra kẹt luồng, chuyển trạng thái kỳ. Quyền <c>period.manage</c>.
/// </summary>
public interface IPeriodService
{
    Task<List<EvaluationPeriodDto>> GetPeriodsAsync(CancellationToken ct = default);
    Task<EvaluationPeriodDto?> GetActivePeriodAsync(CancellationToken ct = default);
    Task<EvaluationPeriodDto> GetPeriodAsync(Guid id, CancellationToken ct = default);
    IReadOnlyList<PeriodPresetDto> GetPresets();

    /// <summary>Mã quyền chọn được làm quyền thực hiện bước nội bộ trong hồ sơ luồng.</summary>
    IReadOnlyList<StepPermissionOptionDto> GetStepPermissions();

    Task<EvaluationPeriodDto> CreatePeriodAsync(CreatePeriodDto dto, CancellationToken ct = default);
    Task<EvaluationPeriodDto> UpdatePeriodAsync(Guid id, UpdatePeriodDto dto, CancellationToken ct = default);

    /// <summary>
    /// Dự thảo → Đang mở. Còn cảnh báo kẹt luồng và không mở bắt buộc → không mở, trả kết quả kiểm tra (controller trả 409).
    /// Mở bắt buộc khi còn cảnh báo → bắt buộc lý do (ghi vào kỳ).
    /// </summary>
    Task<OpenPeriodOutcome> OpenAsync(Guid id, PeriodTransitionDto dto, CancellationToken ct = default);
    Task<EvaluationPeriodDto> LockAsync(Guid id, PeriodTransitionDto dto, CancellationToken ct = default);
    Task<EvaluationPeriodDto> UnlockAsync(Guid id, PeriodTransitionDto dto, CancellationToken ct = default);
    Task<EvaluationPeriodDto> CloseAsync(Guid id, PeriodTransitionDto dto, CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra kẹt luồng: với mỗi hồ sơ chưa công bố và mỗi bước còn phía trước (nội bộ hoặc cấp trên thực hiện), có ít nhất
    /// một tài khoản đang hoạt động (không phải chủ hồ sơ; bước của chủ hồ sơ thì chính chủ hồ sơ) có quyền thực hiện bước
    /// trong phạm vi bao trùm hồ sơ (tính bằng resolver + guard).
    /// </summary>
    Task<PeriodReadinessDto> GetReadinessAsync(Guid periodId, CancellationToken ct = default);

    Task<List<PeriodParticipantDto>> GetParticipantsAsync(Guid periodId, CancellationToken ct = default);
    Task<List<ParticipantCandidateDto>> GetCandidatesAsync(Guid periodId, Guid? departmentId, Guid? partyCellId, string? query, CancellationToken ct = default);
    Task<AddParticipantsResultDto> AddParticipantsAsync(Guid periodId, AddParticipantsDto dto, CancellationToken ct = default);

    /// <summary>
    /// Thêm người vào kỳ nhưng <b>không lưu</b> (dùng trong transaction của khung import). Trả số hồ sơ tạo mới và
    /// danh sách lý do bỏ qua. <paramref name="profileByMember"/>: hồ sơ luồng chỉ định cho từng người (không có → mặc định theo
    /// cấp quyết định).
    /// </summary>
    Task<AddParticipantsResultDto> StageParticipantsAsync(Guid periodId, IReadOnlyCollection<Guid> memberIds, string source,
        CancellationToken ct = default, IReadOnlyDictionary<Guid, string>? profileByMember = null);

    Task RemoveParticipantAsync(Guid periodId, Guid recordId, uint? version, CancellationToken ct = default);
    Task<PeriodParticipantDto> UpdateSnapshotAsync(Guid periodId, Guid recordId, UpdateSnapshotDto dto, CancellationToken ct = default);

    /// <summary>Đổi hồ sơ luồng của một hồ sơ (có lý do; chỉ khi hồ sơ chưa qua bước bị ảnh hưởng).</summary>
    Task<PeriodParticipantDto> ChangeProfileAsync(Guid periodId, Guid recordId, ChangeProfileDto dto, CancellationToken ct = default);

    /// <summary>Đổi hồ sơ luồng hàng loạt: hồ sơ không đổi được được liệt kê lý do, các hồ sơ còn lại vẫn đổi.</summary>
    Task<BulkChangeProfileResultDto> BulkChangeProfileAsync(Guid periodId, BulkChangeProfileDto dto, CancellationToken ct = default);
}

/// <summary>Triển khai quản lý kỳ đánh giá.</summary>
public sealed class PeriodService : IPeriodService
{
    private const int MaxReasonLength = 1000;

    private readonly IEvaluationWorkflowRepository _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionResolver _permissions;
    private readonly ICriteriaSetRepository _criteriaSets;

    public PeriodService(
        IEvaluationWorkflowRepository repo,
        IUnitOfWork unitOfWork,
        IAuthorizationGuard guard,
        ICurrentUserService currentUser,
        IPermissionResolver permissions,
        ICriteriaSetRepository criteriaSets)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
        _guard = guard;
        _currentUser = currentUser;
        _permissions = permissions;
        _criteriaSets = criteriaSets;
    }

    #region Đọc

    /// <inheritdoc />
    public async Task<List<EvaluationPeriodDto>> GetPeriodsAsync(CancellationToken ct = default)
    {
        var periods = await _repo.ListPeriodsAsync(ct);
        var counts = await _repo.CountRecordsByPeriodAsync(ct);
        var active = EvaluationMapping.ActivePeriod(periods)?.Id;
        return periods.Select(p => EvaluationMapping.ToPeriodDto(p, counts.GetValueOrDefault(p.Id), active)).ToList();
    }

    /// <inheritdoc />
    public async Task<EvaluationPeriodDto?> GetActivePeriodAsync(CancellationToken ct = default)
    {
        var periods = await _repo.ListPeriodsAsync(ct);
        var active = EvaluationMapping.ActivePeriod(periods);
        if (active == null)
            return null;
        var counts = await _repo.CountRecordsByPeriodAsync(ct);
        return EvaluationMapping.ToPeriodDto(active, counts.GetValueOrDefault(active.Id), active.Id);
    }

    /// <inheritdoc />
    public async Task<EvaluationPeriodDto> GetPeriodAsync(Guid id, CancellationToken ct = default)
    {
        var periods = await _repo.ListPeriodsAsync(ct);
        var period = periods.FirstOrDefault(p => p.Id == id)
            ?? throw new NotFoundException($"Không tìm thấy kỳ đánh giá với Id: {id}.");
        var counts = await _repo.CountRecordsByPeriodAsync(ct);
        return EvaluationMapping.ToPeriodDto(period, counts.GetValueOrDefault(id), EvaluationMapping.ActivePeriod(periods)?.Id);
    }

    /// <inheritdoc />
    public IReadOnlyList<PeriodPresetDto> GetPresets() =>
        PeriodSettings.Presets.Select(p => new PeriodPresetDto
        {
            Code = p.Code,
            Name = p.Name,
            Description = p.Description,
            SuggestedForm = p.SuggestedForm,
            Settings = p.Build()
        }).ToList();

    /// <inheritdoc />
    public IReadOnlyList<StepPermissionOptionDto> GetStepPermissions() =>
        WorkflowActions.AssignableStepPermissions()
            .Select(d => new StepPermissionOptionDto { Code = d.Code, Name = d.Name, Description = d.Description })
            .ToList();

    #endregion

    #region Tạo, sửa, chuyển trạng thái

    /// <inheritdoc />
    public async Task<EvaluationPeriodDto> CreatePeriodAsync(CreatePeriodDto dto, CancellationToken ct = default)
    {
        EnsureManage();
        ArgumentNullException.ThrowIfNull(dto);
        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Tên kỳ đánh giá không được để trống.");
        if (name.Length > 200)
            throw new ValidationException("Tên kỳ đánh giá không được dài quá 200 ký tự.");
        if (dto.Quarter is < 1 or > 4)
            throw new ValidationException("Quý phải từ 1 đến 4.");
        if (dto.Year is < 2000 or > 2100)
            throw new ValidationException("Năm đánh giá không hợp lệ.");
        if (dto.EndDate < dto.StartDate)
            throw new ValidationException("Ngày kết thúc phải sau ngày bắt đầu.");

        var preset = string.IsNullOrWhiteSpace(dto.Preset)
            ? PeriodSettings.FindPreset(PeriodSettings.PresetFull)!
            : PeriodSettings.FindPreset(dto.Preset)
              ?? throw new ValidationException($"Kiểu kỳ \"{dto.Preset}\" không tồn tại. Hãy chọn một trong: "
                  + string.Join(", ", PeriodSettings.Presets.Select(p => p.Name)) + ".");

        var now = DateTime.UtcNow;
        var period = new EvaluationPeriod
        {
            Id = Guid.NewGuid(),
            Year = dto.Year,
            Quarter = (EvaluationQuarter)dto.Quarter,
            Name = name,
            StartDate = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc),
            EndDate = DateTime.SpecifyKind(dto.EndDate, DateTimeKind.Utc),
            Status = PeriodStatus.Draft,
            Settings = preset.Build().ToJson(),
            CreatedAt = now
        };

        // Bộ tiêu chí: chỉ định → phải đã xuất bản; không chỉ định → bộ đã xuất bản mới nhất có mẫu tự chấm gợi ý của kiểu kỳ.
        var set = dto.CriteriaSetId.HasValue
            ? await LoadPublishedSetAsync(dto.CriteriaSetId.Value, ct)
            : await _criteriaSets.LatestPublishedAsync(preset.SuggestedForm, ct);
        if (set != null)
        {
            var errors = ValidateSettings(preset.Build(), set.SelfScoreForm);
            if (errors.Count > 0)
                throw new ValidationException($"Kiểu kỳ \"{preset.Name}\" không dùng được với bộ tiêu chí \"{set.Name}\": " + string.Join(" ", errors));
            AttachCriteria(period, set, now);
        }

        _repo.AddPeriod(period);
        await _unitOfWork.SaveChangesAsync(ct);
        return await GetPeriodAsync(period.Id, ct);
    }

    /// <inheritdoc />
    public async Task<EvaluationPeriodDto> UpdatePeriodAsync(Guid id, UpdatePeriodDto dto, CancellationToken ct = default)
    {
        EnsureManage();
        ArgumentNullException.ThrowIfNull(dto);
        var period = await LoadForWriteAsync(id, dto.Version, ct);
        if (period.Status == PeriodStatus.Closed)
            throw new ConflictException("Kỳ đánh giá đã đóng, không sửa được cấu hình.");

        var current = ReadSettings(period);
        var form = ReadCriteria(period)?.SelfScoreForm;
        if (dto.CriteriaSetId.HasValue && dto.CriteriaSetId.Value != period.CriteriaSetId)
        {
            if (period.Status != PeriodStatus.Draft)
                throw new ConflictException("Kỳ đã mở: không đổi được bộ tiêu chí (bộ tiêu chí đã được chụp vào kỳ khi mở).");
            var set = await LoadPublishedSetAsync(dto.CriteriaSetId.Value, ct);
            AttachCriteria(period, set, DateTime.UtcNow);
            form = set.SelfScoreForm;
            if (dto.Settings == null)
            {
                var errors = ValidateSettings(current, form);
                if (errors.Count > 0)
                    throw new ValidationException($"Cấu hình kỳ không dùng được với bộ tiêu chí \"{set.Name}\": " + string.Join(" ", errors));
            }
        }

        var settingsChanged = false;
        if (dto.Settings != null)
        {
            var next = dto.Settings.Normalize();
            var errors = ValidateSettings(next, form);
            if (errors.Count > 0)
                throw new ValidationException("Cấu hình kỳ chưa hợp lệ: " + string.Join(" ", errors));

            if (period.Status != PeriodStatus.Draft && !next.DiffersOnlyInDeadlines(current))
                throw new ConflictException("Kỳ đã mở: chỉ được sửa thời hạn các bước. Hồ sơ luồng, chế độ và quyền thực hiện bước "
                    + "chỉ sửa được khi kỳ còn dự thảo.");

            settingsChanged = next.ToJson() != current.ToJson();
            if (settingsChanged && period.Status == PeriodStatus.Draft)
                await EnsureProfilesInUseKeptAsync(period.Id, next, ct);
            period.Settings = next.ToJson();
        }

        if (dto.Name != null || dto.StartDate.HasValue || dto.EndDate.HasValue)
        {
            if (period.Status != PeriodStatus.Draft)
                throw new ConflictException("Kỳ đã mở: không sửa được tên và thời gian kỳ, chỉ sửa được thời hạn các bước.");
            if (dto.Name != null)
            {
                var name = dto.Name.Trim();
                if (name.Length == 0 || name.Length > 200)
                    throw new ValidationException("Tên kỳ đánh giá phải có từ 1 đến 200 ký tự.");
                period.Name = name;
            }
            if (dto.StartDate.HasValue)
                period.StartDate = DateTime.SpecifyKind(dto.StartDate.Value, DateTimeKind.Utc);
            if (dto.EndDate.HasValue)
                period.EndDate = DateTime.SpecifyKind(dto.EndDate.Value, DateTimeKind.Utc);
            if (period.EndDate < period.StartDate)
                throw new ValidationException("Ngày kết thúc phải sau ngày bắt đầu.");
        }

        // Kỳ dự thảo đổi cấu hình bước → trạng thái đầu của hồ sơ tính lại theo hồ sơ luồng của từng hồ sơ.
        if (settingsChanged && period.Status == PeriodStatus.Draft)
            await RecalculateInitialStatusAsync(period, ct);

        _unitOfWork.SetOriginalVersion(period, dto.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await GetPeriodAsync(period.Id, ct);
    }

    /// <summary>Không được xóa hồ sơ luồng đang được hồ sơ đánh giá dùng (hãy chuyển hồ sơ sang hồ sơ luồng khác trước).</summary>
    private async Task EnsureProfilesInUseKeptAsync(Guid periodId, PeriodSettings next, CancellationToken ct)
    {
        var missing = (await _repo.ListRecordsAsync(periodId, ct))
            .Where(r => !string.IsNullOrEmpty(r.WorkflowProfileCode) && next.FindProfile(r.WorkflowProfileCode) == null)
            .GroupBy(r => r.WorkflowProfileCode)
            .Select(g => $"\"{g.Key}\" ({g.Count()} hồ sơ)")
            .ToList();
        if (missing.Count > 0)
        {
            throw new ConflictException("Không xóa được hồ sơ luồng đang được dùng: " + string.Join(", ", missing)
                + ". Hãy chuyển các hồ sơ đó sang hồ sơ luồng khác trước.");
        }
    }

    /// <inheritdoc />
    public async Task<OpenPeriodOutcome> OpenAsync(Guid id, PeriodTransitionDto dto, CancellationToken ct = default)
    {
        EnsureManage();
        var period = await LoadForWriteAsync(id, dto?.Version, ct);
        if (period.Status != PeriodStatus.Draft)
            throw new ConflictException($"Kỳ đang ở trạng thái \"{WorkflowSteps.PeriodStatusDisplayName(period.Status)}\", chỉ mở được kỳ dự thảo.");

        var settings = ReadSettings(period);
        if (period.CriteriaSetId is not { } setId)
            throw new ValidationException("Kỳ chưa chọn bộ tiêu chí nên chưa mở được. Hãy chọn một bộ tiêu chí đã xuất bản trong cấu hình kỳ.");
        var set = await _criteriaSets.FindAsync(setId, ct);
        if (set == null || set.Status != CriteriaSetStatus.Published)
            throw new ConflictException("Bộ tiêu chí đã chọn không còn ở trạng thái \"Đã xuất bản\" (đã lưu trữ hoặc bị xóa). Hãy chọn bộ tiêu chí khác cho kỳ.");
        var criteriaErrors = set.GetContent().Validate(set.SelfScoreForm);
        if (criteriaErrors.Count > 0)
            throw new ValidationException($"Bộ tiêu chí \"{set.Name}\" chưa hợp lệ: " + string.Join(" ", criteriaErrors));
        var errors = ValidateSettings(settings, set.SelfScoreForm);
        if (errors.Count > 0)
            throw new ValidationException("Cấu hình kỳ chưa hợp lệ, chưa mở được kỳ: " + string.Join(" ", errors));

        // Chụp nguyên bộ vào kỳ (bất biến cho kỳ này).
        AttachCriteria(period, set, DateTime.UtcNow);

        var readiness = await BuildReadinessAsync(period, ct);
        var reason = dto?.Reason?.Trim();
        if (!readiness.Ready)
        {
            if (dto?.Force != true)
                return new OpenPeriodOutcome { Readiness = readiness };
            if (string.IsNullOrWhiteSpace(reason))
                throw new ValidationException("Mở kỳ bắt buộc khi còn cảnh báo kẹt luồng: hãy nhập lý do.");
            reason = $"Mở kỳ bắt buộc khi còn {readiness.Issues.Count} cảnh báo kẹt luồng. Lý do: {reason}";
            if (reason.Length > MaxReasonLength)
                reason = reason[..MaxReasonLength];
        }

        await RecalculateInitialStatusAsync(period, ct);
        return new OpenPeriodOutcome { Period = await TransitionAsync(period, PeriodStatus.Open, reason, dto?.Version, ct) };
    }

    /// <inheritdoc />
    public async Task<EvaluationPeriodDto> LockAsync(Guid id, PeriodTransitionDto dto, CancellationToken ct = default)
    {
        EnsureManage();
        var period = await LoadForWriteAsync(id, dto?.Version, ct);
        if (period.Status != PeriodStatus.Open)
            throw new ConflictException($"Kỳ đang ở trạng thái \"{WorkflowSteps.PeriodStatusDisplayName(period.Status)}\", chỉ khóa dữ liệu được kỳ đang mở.");
        return await TransitionAsync(period, PeriodStatus.Locked, dto?.Reason, dto?.Version, ct);
    }

    /// <inheritdoc />
    public async Task<EvaluationPeriodDto> UnlockAsync(Guid id, PeriodTransitionDto dto, CancellationToken ct = default)
    {
        EnsureManage();
        var period = await LoadForWriteAsync(id, dto?.Version, ct);
        if (period.Status != PeriodStatus.Locked)
            throw new ConflictException($"Kỳ đang ở trạng thái \"{WorkflowSteps.PeriodStatusDisplayName(period.Status)}\", chỉ mở lại được kỳ đang khóa dữ liệu.");
        if (string.IsNullOrWhiteSpace(dto?.Reason))
            throw new ValidationException("Hãy nhập lý do mở lại kỳ (chuyển từ \"Khóa dữ liệu\" về \"Đang mở\").");
        return await TransitionAsync(period, PeriodStatus.Open, dto.Reason, dto.Version, ct);
    }

    /// <inheritdoc />
    public async Task<EvaluationPeriodDto> CloseAsync(Guid id, PeriodTransitionDto dto, CancellationToken ct = default)
    {
        EnsureManage();
        var period = await LoadForWriteAsync(id, dto?.Version, ct);
        if (period.Status is not (PeriodStatus.Open or PeriodStatus.Locked))
            throw new ConflictException($"Kỳ đang ở trạng thái \"{WorkflowSteps.PeriodStatusDisplayName(period.Status)}\", chỉ đóng được kỳ đang mở hoặc khóa dữ liệu.");

        var records = await _repo.ListRecordsAsync(period.Id, ct);
        var pending = records.Count(r => r.Status != RecordStatus.Published);
        if (pending > 0)
            throw new ConflictException($"Chưa đóng được kỳ vì còn {pending} hồ sơ chưa công bố. Hãy công bố toàn bộ hồ sơ trước.");
        return await TransitionAsync(period, PeriodStatus.Closed, dto?.Reason, dto?.Version, ct);
    }

    private async Task<EvaluationPeriodDto> TransitionAsync(EvaluationPeriod period, PeriodStatus next, string? reason, uint? version, CancellationToken ct)
    {
        var clean = reason?.Trim();
        if (clean is { Length: > MaxReasonLength })
            throw new ValidationException($"Lý do không được dài quá {MaxReasonLength} ký tự.");
        period.Status = next;
        period.StatusReason = string.IsNullOrEmpty(clean) ? null : clean;
        period.StatusChangedAt = DateTime.UtcNow;
        period.StatusChangedBy = _currentUser.UserId;
        _unitOfWork.SetOriginalVersion(period, version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await GetPeriodAsync(period.Id, ct);
    }

    /// <summary>
    /// Hồ sơ còn ở trạng thái đầu (chưa ai thao tác) → đặt theo bước áp dụng đầu tiên của hồ sơ luồng của hồ sơ.
    /// </summary>
    private async Task RecalculateInitialStatusAsync(EvaluationPeriod period, CancellationToken ct)
    {
        var settings = ReadSettings(period);
        var initialStates = new[] { RecordStatus.AwaitingRegistration, RecordStatus.AwaitingTaskApproval, RecordStatus.AwaitingSelfScore };
        foreach (var record in await _repo.ListRecordsForUpdateAsync(period.Id, ct))
        {
            var profile = EvaluationMapping.ProfileOf(settings, record);
            var initial = RecordStateMachine.Initial(profile.ActiveSteps());
            if (record.Status == initial || !initialStates.Contains(record.Status) || record.SelfScoredAt.HasValue
                || record.Tasks.Any(t => !t.IsDeleted))
                continue;

            _repo.AddHistory(NewHistory(record, record.Status, initial, WorkflowAction.Recalculate, null,
                "Tính lại bước đầu theo cấu hình kỳ."));
            record.Status = initial;
        }
    }

    #endregion

    #region Kiểm tra kẹt luồng

    /// <inheritdoc />
    public async Task<PeriodReadinessDto> GetReadinessAsync(Guid periodId, CancellationToken ct = default)
    {
        EnsureManage();
        var period = await _repo.FindPeriodAsync(periodId, ct)
            ?? throw new NotFoundException($"Không tìm thấy kỳ đánh giá với Id: {periodId}.");
        return await BuildReadinessAsync(period, ct);
    }

    private async Task<PeriodReadinessDto> BuildReadinessAsync(EvaluationPeriod period, CancellationToken ct)
    {
        var settings = ReadSettings(period);
        var records = (await _repo.ListRecordsAsync(period.Id, ct)).Where(r => r.Status != RecordStatus.Published).ToList();
        var result = new PeriodReadinessDto { PeriodId = period.Id, CheckedRecords = records.Count };
        if (records.Count == 0)
        {
            result.Ready = true;
            return result;
        }

        // Các bước còn phía trước của từng hồ sơ và quyền thực hiện theo hồ sơ luồng của hồ sơ.
        var pending = records.SelectMany(record =>
        {
            var profile = EvaluationMapping.ProfileOf(settings, record);
            var current = WorkflowSteps.StepOf(record.Status) is { } step ? WorkflowSteps.IndexOf(step) : int.MaxValue;
            return WorkflowSteps.Ordered
                .Where(s => WorkflowSteps.IndexOf(s) >= current)
                .Select(s => (record, profile, step: s, action: WorkflowActions.AdvanceOf(s, profile)))
                .Where(x => x.action != null)
                .Select(x => (x.record, x.profile, x.step, x.action, permission: WorkflowActions.PermissionFor(x.action!, x.profile)));
        }).ToList();

        // Ứng viên: tài khoản đang hoạt động có vai trò chứa quyền cần; quyền hiệu lực + phạm vi tính bằng resolver, kiểm tra bằng guard.
        var codes = pending.Select(x => x.permission).Where(c => c != PermissionCodes.EvaluationSelf).Distinct().ToList();
        var candidateIds = codes.Count == 0
            ? new List<Guid>()
            : await _repo.ListActiveUserIdsWithAnyPermissionAsync(codes, DateTime.UtcNow, ct);
        var effective = new Dictionary<Guid, EffectivePermissions>();
        foreach (var userId in candidateIds.Concat(records.Select(r => r.MemberId)).Distinct())
            effective[userId] = await _permissions.GetAsync(userId, ct);

        foreach (var (record, profile, step, _, permission) in pending)
        {
            var target = AccessTarget.ForRecord(record);
            bool covered;
            if (permission == PermissionCodes.EvaluationSelf)
            {
                covered = effective.TryGetValue(record.MemberId, out var own) && AuthorizationGuard.Evaluate(own, permission, target);
            }
            else
            {
                covered = candidateIds.Any(userId => userId != record.MemberId
                    && AuthorizationGuard.Evaluate(effective[userId], permission, target));
            }
            if (covered)
                continue;

            var mode = profile.Mode(step);
            var scope = ScopeText(record);
            var permissionName = PermissionCodes.DisplayName(permission);
            var fullName = record.Member?.FullName ?? string.Empty;
            var message = permission == PermissionCodes.EvaluationSelf
                ? $"Hồ sơ của {fullName} ({profile.Name}) sẽ kẹt ở bước \"{WorkflowSteps.DisplayName(step)}\" vì tài khoản của chủ hồ sơ "
                  + $"không hoạt động hoặc không có quyền \"{permissionName}\". Hãy kiểm tra tài khoản và vai trò của người này."
                : $"Hồ sơ của {fullName} ({profile.Name}) sẽ kẹt ở bước \"{WorkflowSteps.DisplayName(step)}\""
                  + (mode == StepMode.External ? " (do cấp trên thực hiện)" : string.Empty)
                  + $" vì không có tài khoản đang hoạt động nào (ngoài chủ hồ sơ) có quyền \"{permissionName}\" trong phạm vi {scope}. "
                  + "Hãy gán vai trò có quyền này hoặc sửa cấu hình bước của hồ sơ luồng.";
            result.Issues.Add(new ReadinessIssueDto
            {
                RecordId = record.Id,
                MemberId = record.MemberId,
                FullName = fullName,
                WorkflowProfileCode = profile.Code,
                WorkflowProfileName = profile.Name,
                Step = WorkflowSteps.Code(step),
                StepName = WorkflowSteps.DisplayName(step),
                Mode = mode.ToString(),
                Permission = permission,
                PermissionName = permissionName,
                Scope = scope,
                Message = message
            });
        }

        // Khung tỷ trọng của hồ sơ phải có trong bộ tiêu chí của kỳ (Mẫu 09A tính A-B-C-D theo khung).
        var criteria = ReadCriteria(period);
        if (criteria is { UsesAxisScoring: false })
        {
            foreach (var record in records.Where(r => criteria.Content.FindFrame(r.WeightFrameCode) == null))
            {
                var profile = EvaluationMapping.ProfileOf(settings, record);
                var fullName = record.Member?.FullName ?? string.Empty;
                result.Issues.Add(new ReadinessIssueDto
                {
                    RecordId = record.Id,
                    MemberId = record.MemberId,
                    FullName = fullName,
                    WorkflowProfileCode = profile.Code,
                    WorkflowProfileName = profile.Name,
                    Step = WorkflowSteps.Code(WorkflowStep.B2_SELF_SCORE),
                    StepName = WorkflowSteps.DisplayName(WorkflowStep.B2_SELF_SCORE),
                    Mode = profile.Mode(WorkflowStep.B2_SELF_SCORE).ToString(),
                    Permission = string.Empty,
                    PermissionName = string.Empty,
                    Scope = string.Empty,
                    Message = string.IsNullOrEmpty(record.WeightFrameCode)
                        ? $"Hồ sơ của {fullName} chưa có khung tỷ trọng A-B-C-D nên không tự chấm được theo Mẫu 09A. "
                          + $"Hãy chọn khung trong danh sách người được đánh giá (bộ \"{criteria.Name}\" có: {FrameList(criteria)})."
                        : $"Khung tỷ trọng \"{record.WeightFrameCode}\" của hồ sơ {fullName} không có trong bộ tiêu chí \"{criteria.Name}\" "
                          + $"của kỳ (có: {FrameList(criteria)}). Hãy sửa khung của hồ sơ trong danh sách người được đánh giá."
                });
            }
        }

        result.Ready = result.Issues.Count == 0;
        return result;
    }

    private static string FrameList(CriteriaSnapshot criteria) =>
        criteria.Content.WeightFrames.Count == 0 ? "không có khung nào" : string.Join(", ", criteria.Content.WeightFrames.Select(f => f.Code));

    private static string ScopeText(EvaluationRecord record)
    {
        // Phạm vi gán bao trùm cây con: bản gán ở đơn vị của hồ sơ, ở đơn vị cấp trên của nó hoặc Toàn công ty đều đủ.
        var parts = new List<string>();
        if (record.Department != null)
            parts.Add($"đơn vị chính quyền \"{record.Department.Name}\"");
        if (record.PartyCell != null)
            parts.Add($"tổ chức Đảng \"{record.PartyCell.Name}\"");
        return parts.Count == 0
            ? "Toàn công ty"
            : string.Join(" hoặc ", parts) + " (gán tại đơn vị đó, đơn vị cấp trên của nó hoặc Toàn công ty)";
    }

    #endregion

    #region Người được đánh giá

    /// <inheritdoc />
    public async Task<List<PeriodParticipantDto>> GetParticipantsAsync(Guid periodId, CancellationToken ct = default)
    {
        EnsureManage();
        var period = await _repo.FindPeriodAsync(periodId, ct) ?? throw new NotFoundException($"Không tìm thấy kỳ đánh giá với Id: {periodId}.");
        var settings = ReadSettings(period);
        var criteria = ReadCriteria(period);
        var records = await _repo.ListRecordsAsync(periodId, ct);
        return records.Select(r => ToParticipant(r, settings, criteria)).ToList();
    }

    /// <inheritdoc />
    public async Task<List<ParticipantCandidateDto>> GetCandidatesAsync(Guid periodId, Guid? departmentId, Guid? partyCellId, string? query, CancellationToken ct = default)
    {
        EnsureManage();
        _ = await _repo.FindPeriodAsync(periodId, ct) ?? throw new NotFoundException($"Không tìm thấy kỳ đánh giá với Id: {periodId}.");
        var existing = await _repo.GetParticipantIdsAsync(periodId, ct);
        var members = await _repo.SearchMembersAsync(departmentId, partyCellId, query, ct);
        return members.Select(m => new ParticipantCandidateDto
        {
            MemberId = m.Id,
            Username = m.Username,
            FullName = m.FullName,
            DepartmentName = m.DepartmentName,
            PartyCellName = m.PartyCellName,
            AlreadyAdded = existing.Contains(m.Id)
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<AddParticipantsResultDto> AddParticipantsAsync(Guid periodId, AddParticipantsDto dto, CancellationToken ct = default)
    {
        EnsureManage();
        ArgumentNullException.ThrowIfNull(dto);
        var ids = new HashSet<Guid>(dto.MemberIds?.Where(id => id != Guid.Empty) ?? Enumerable.Empty<Guid>());
        var sources = new List<string>();
        if (ids.Count > 0)
            sources.Add("chọn tay");

        if (dto.DepartmentId.HasValue || dto.PartyCellId.HasValue)
        {
            if (dto.DepartmentId.HasValue && !await _repo.DepartmentExistsAsync(dto.DepartmentId.Value, ct))
                throw new ValidationException("Phòng/đơn vị đã chọn không tồn tại.");
            if (dto.PartyCellId.HasValue && !await _repo.PartyCellExistsAsync(dto.PartyCellId.Value, ct))
                throw new ValidationException("Chi bộ đã chọn không tồn tại.");
            foreach (var member in await _repo.SearchMembersAsync(dto.DepartmentId, dto.PartyCellId, null, ct))
                ids.Add(member.Id);
            sources.Add(dto.DepartmentId.HasValue ? "theo Phòng" : "theo Chi bộ");
        }

        if (ids.Count == 0)
            throw new ValidationException("Hãy chọn cán bộ hoặc Phòng/Chi bộ cần thêm vào danh sách được đánh giá.");

        var profileCode = dto.WorkflowProfileCode?.Trim();
        var profiles = string.IsNullOrEmpty(profileCode) ? null : ids.ToDictionary(id => id, _ => profileCode);
        var result = await StageParticipantsAsync(periodId, ids, string.Join(", ", sources), ct, profiles);
        await _unitOfWork.SaveChangesAsync(ct);
        return result;
    }

    /// <inheritdoc />
    public async Task<AddParticipantsResultDto> StageParticipantsAsync(Guid periodId, IReadOnlyCollection<Guid> memberIds, string source,
        CancellationToken ct = default, IReadOnlyDictionary<Guid, string>? profileByMember = null)
    {
        EnsureManage();
        var period = await _repo.FindPeriodAsync(periodId, ct)
            ?? throw new NotFoundException($"Không tìm thấy kỳ đánh giá với Id: {periodId}.");
        if (period.Status is not (PeriodStatus.Draft or PeriodStatus.Open))
            throw new ConflictException("Chỉ thêm người được đánh giá khi kỳ còn dự thảo hoặc đang mở.");

        var settings = ReadSettings(period);
        foreach (var code in (profileByMember?.Values ?? Enumerable.Empty<string>()).Distinct())
        {
            if (settings.FindProfile(code) == null)
                throw new ValidationException($"Hồ sơ luồng \"{code}\" không có trong cấu hình kỳ. Hãy chọn một trong: "
                    + string.Join(", ", settings.Profiles.Select(p => $"{p.Code} ({p.Name})")) + ".");
        }

        // Khung tỷ trọng: khung mặc định của cán bộ; cán bộ chưa có → khung mặc định của bộ tiêu chí của kỳ (nếu có).
        var defaultFrame = ReadCriteria(period)?.Content.Parameters.DefaultWeightFrameCode;
        var existing = await _repo.GetParticipantIdsAsync(periodId, ct);
        var members = (await _repo.GetMembersAsync(memberIds, ct)).ToDictionary(m => m.Id);
        var result = new AddParticipantsResultDto();
        var now = DateTime.UtcNow;

        foreach (var id in memberIds.Distinct())
        {
            if (!members.TryGetValue(id, out var member))
            {
                result.Skipped.Add($"Không tìm thấy cán bộ (Id {id}).");
                continue;
            }
            if (!member.IsActive)
            {
                result.Skipped.Add($"{member.FullName}: tài khoản đang bị khóa.");
                continue;
            }
            if (existing.Contains(id))
            {
                result.Skipped.Add($"{member.FullName}: đã có trong danh sách được đánh giá.");
                continue;
            }

            var profile = profileByMember != null && profileByMember.TryGetValue(id, out var chosen)
                ? settings.FindProfile(chosen)!
                : settings.ResolveProfile(null, member.ApprovalAuthority);
            var initial = RecordStateMachine.Initial(profile.ActiveSteps());

            // Hồ sơ đã bị bỏ khỏi danh sách trước đó (xóa mềm) → khôi phục thay vì tạo mới (unique kỳ + cán bộ).
            var record = await _repo.FindRecordIncludingDeletedAsync(periodId, id, ct);
            var isNew = record == null;
            record ??= new EvaluationRecord { Id = Guid.NewGuid(), PeriodId = periodId, MemberId = id, CreatedAt = now };
            record.IsDeleted = false;
            record.DeletedAt = null;
            record.DeletedBy = null;
            record.DepartmentId = member.DepartmentId;
            record.PartyCellId = member.PartyCellId;
            record.WeightFrameCode = member.WeightFrameCode ?? defaultFrame ?? string.Empty;
            record.ApprovalAuthority = member.ApprovalAuthority;
            record.WorkflowProfileCode = profile.Code;
            var from = isNew ? (RecordStatus?)null : record.Status;
            record.Status = initial;
            record.UpdatedAt = now;
            if (isNew)
                _repo.AddRecord(record);

            _repo.AddHistory(NewHistory(record, from, initial, WorkflowAction.Create, null,
                $"Thêm vào danh sách được đánh giá ({source}); ảnh chụp Phòng: {member.DepartmentName ?? "—"}, Chi bộ: {member.PartyCellName ?? "—"}; "
                + $"hồ sơ luồng: {profile.Name}."));
            existing.Add(id);
            result.Added++;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task RemoveParticipantAsync(Guid periodId, Guid recordId, uint? version, CancellationToken ct = default)
    {
        EnsureManage();
        var record = await _repo.FindRecordAsync(recordId, ct);
        if (record == null || record.PeriodId != periodId)
            throw new NotFoundException("Không tìm thấy người được đánh giá trong kỳ này.");
        if (version is null)
            throw new ValidationException("Thiếu phiên bản dữ liệu (version) của hồ sơ. Hãy tải lại danh sách.");
        if (record.Version != version)
            throw new ConflictException("Hồ sơ đã được cập nhật sau khi bạn mở danh sách. Hãy tải lại.");

        var profile = EvaluationMapping.ProfileOf(ReadSettings(record.Period), record);
        var initial = RecordStateMachine.Initial(profile.ActiveSteps());
        var untouched = record.Status == initial && !record.SelfScoredAt.HasValue && record.Tasks.All(t => t.IsDeleted);
        if (record.Period.Status == PeriodStatus.Draft ? false : record.Period.Status != PeriodStatus.Open || !untouched)
            throw new ConflictException("Chỉ bỏ được người khỏi danh sách khi kỳ còn dự thảo, hoặc kỳ đang mở mà hồ sơ chưa có dữ liệu nào.");

        _repo.RemoveRecord(record);
        _unitOfWork.SetOriginalVersion(record, version);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<PeriodParticipantDto> UpdateSnapshotAsync(Guid periodId, Guid recordId, UpdateSnapshotDto dto, CancellationToken ct = default)
    {
        EnsureManage();
        ArgumentNullException.ThrowIfNull(dto);
        var record = await _repo.FindRecordAsync(recordId, ct);
        if (record == null || record.PeriodId != periodId)
            throw new NotFoundException("Không tìm thấy người được đánh giá trong kỳ này.");
        if (dto.Version is null)
            throw new ValidationException("Thiếu phiên bản dữ liệu (version) của hồ sơ. Hãy tải lại danh sách.");
        if (record.Version != dto.Version)
            throw new ConflictException("Hồ sơ đã được cập nhật sau khi bạn mở danh sách. Hãy tải lại.");
        if (record.Period.Status is not (PeriodStatus.Draft or PeriodStatus.Open))
            throw new ConflictException("Chỉ sửa ảnh chụp Phòng/Chi bộ/khung tỷ trọng/cấp quyết định khi kỳ còn dự thảo hoặc đang mở.");
        var reason = dto.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            throw new ValidationException("Hãy nhập lý do sửa thông tin ảnh chụp của hồ sơ.");
        if (dto.DepartmentId.HasValue && !await _repo.DepartmentExistsAsync(dto.DepartmentId.Value, ct))
            throw new ValidationException("Phòng/đơn vị đã chọn không tồn tại.");
        if (dto.PartyCellId.HasValue && !await _repo.PartyCellExistsAsync(dto.PartyCellId.Value, ct))
            throw new ValidationException("Chi bộ đã chọn không tồn tại.");

        var changes = new List<string>();
        if (dto.DepartmentId != record.DepartmentId)
        {
            changes.Add("Phòng");
            record.DepartmentId = dto.DepartmentId;
        }
        if (dto.PartyCellId != record.PartyCellId)
        {
            changes.Add("Chi bộ");
            record.PartyCellId = dto.PartyCellId;
        }
        if (!string.IsNullOrWhiteSpace(dto.WeightFrameCode))
        {
            var criteria = ReadCriteria(record.Period);
            var frame = criteria?.Content.FindFrame(dto.WeightFrameCode);
            if (criteria != null && frame == null)
                throw new ValidationException($"Khung tỷ trọng \"{dto.WeightFrameCode.Trim()}\" không có trong bộ tiêu chí \"{criteria.Name}\" của kỳ "
                    + $"(có: {FrameList(criteria)}).");
            var code = frame?.Code ?? UserAccountService.NormalizeWeightFrameCode(dto.WeightFrameCode)!;
            if (!string.Equals(code, record.WeightFrameCode, StringComparison.Ordinal))
            {
                changes.Add("khung tỷ trọng");
                record.WeightFrameCode = code;
            }
        }
        if (!string.IsNullOrWhiteSpace(dto.ApprovalAuthority))
        {
            if (!Enum.TryParse<ApprovalAuthority>(dto.ApprovalAuthority, true, out var authority) || !Enum.IsDefined(authority))
                throw new ValidationException("Cấp quyết định phải là CoSo hoặc CapTren.");
            if (authority != record.ApprovalAuthority)
            {
                changes.Add("cấp quyết định (hồ sơ luồng giữ nguyên — đổi riêng nếu cần)");
                record.ApprovalAuthority = authority;
            }
        }

        if (changes.Count == 0)
            throw new ValidationException("Không có thông tin nào thay đổi.");

        _repo.AddHistory(NewHistory(record, record.Status, record.Status, WorkflowAction.EditSnapshot, reason,
            "Sửa ảnh chụp: " + string.Join(", ", changes) + "."));
        record.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.SetOriginalVersion(record, dto.Version);
        await _unitOfWork.SaveChangesAsync(ct);

        return await ReloadParticipantAsync(periodId, recordId, ct);
    }

    /// <inheritdoc />
    public async Task<PeriodParticipantDto> ChangeProfileAsync(Guid periodId, Guid recordId, ChangeProfileDto dto, CancellationToken ct = default)
    {
        EnsureManage();
        ArgumentNullException.ThrowIfNull(dto);
        var (profileCode, reason) = ValidateProfileChangeInput(dto.WorkflowProfileCode, dto.Reason);
        var record = await _repo.FindRecordAsync(recordId, ct);
        if (record == null || record.PeriodId != periodId)
            throw new NotFoundException("Không tìm thấy người được đánh giá trong kỳ này.");
        var error = ApplyProfileChange(record, dto.Version, profileCode, reason);
        if (error != null)
            throw error;

        _unitOfWork.SetOriginalVersion(record, dto.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadParticipantAsync(periodId, recordId, ct);
    }

    /// <inheritdoc />
    public async Task<BulkChangeProfileResultDto> BulkChangeProfileAsync(Guid periodId, BulkChangeProfileDto dto, CancellationToken ct = default)
    {
        EnsureManage();
        ArgumentNullException.ThrowIfNull(dto);
        var (profileCode, reason) = ValidateProfileChangeInput(dto.WorkflowProfileCode, dto.Reason);
        var items = (dto.Items ?? new List<ProfileChangeItemDto>()).Where(i => i.RecordId != Guid.Empty)
            .GroupBy(i => i.RecordId).Select(g => g.First()).ToList();
        if (items.Count == 0)
            throw new ValidationException("Hãy chọn ít nhất một người được đánh giá để đổi hồ sơ luồng.");

        var result = new BulkChangeProfileResultDto();
        foreach (var item in items)
        {
            var record = await _repo.FindRecordAsync(item.RecordId, ct);
            if (record == null || record.PeriodId != periodId)
            {
                result.Skipped.Add($"Không tìm thấy hồ sơ {item.RecordId} trong kỳ này.");
                continue;
            }
            var error = ApplyProfileChange(record, item.Version, profileCode, reason);
            if (error != null)
            {
                result.Skipped.Add($"{record.Member?.FullName}: {error.Message}");
                continue;
            }
            _unitOfWork.SetOriginalVersion(record, item.Version);
            result.Updated++;
        }

        if (result.Updated > 0)
            await _unitOfWork.SaveChangesAsync(ct);
        return result;
    }

    private static (string ProfileCode, string Reason) ValidateProfileChangeInput(string? profileCode, string? reason)
    {
        var code = profileCode?.Trim();
        if (string.IsNullOrEmpty(code))
            throw new ValidationException("Hãy chọn hồ sơ luồng mới.");
        var clean = reason?.Trim();
        if (string.IsNullOrWhiteSpace(clean))
            throw new ValidationException("Hãy nhập lý do đổi hồ sơ luồng.");
        if (clean.Length > 2000)
            throw new ValidationException("Lý do không được dài quá 2000 ký tự.");
        return (code, clean);
    }

    /// <summary>
    /// Đổi hồ sơ luồng của một hồ sơ (được theo dõi). Chỉ khi kỳ chưa đóng và hồ sơ chưa qua bước nào mà hai hồ sơ luồng
    /// khác nhau (chế độ hoặc quyền thực hiện); bước đang chờ không còn áp dụng → chuyển tới bước áp dụng kế tiếp.
    /// Trả ngoại lệ mô tả lý do không đổi được (không ném) để dùng chung cho đổi hàng loạt.
    /// </summary>
    private AppException? ApplyProfileChange(EvaluationRecord record, uint? version, string profileCode, string reason)
    {
        if (version is null)
            return new ValidationException("Thiếu phiên bản dữ liệu (version) của hồ sơ. Hãy tải lại danh sách.");
        if (record.Version != version)
            return new ConflictException("Hồ sơ đã được cập nhật sau khi bạn mở danh sách. Hãy tải lại.");
        if (record.Period.Status == PeriodStatus.Closed)
            return new ConflictException("Kỳ đã đóng, không đổi được hồ sơ luồng.");

        var settings = ReadSettings(record.Period);
        var next = settings.FindProfile(profileCode);
        if (next == null)
            return new ValidationException($"Hồ sơ luồng \"{profileCode}\" không có trong cấu hình kỳ.");
        var current = EvaluationMapping.ProfileOf(settings, record);
        if (string.Equals(current.Code, next.Code, StringComparison.Ordinal) && record.WorkflowProfileCode == next.Code)
            return new ValidationException($"Hồ sơ đang dùng hồ sơ luồng \"{next.Name}\".");

        var differing = current.DifferingSteps(next);
        if (differing.Count > 0)
        {
            var firstAffected = differing.Min(WorkflowSteps.IndexOf);
            var position = WorkflowSteps.StepOf(record.Status) is { } step ? WorkflowSteps.IndexOf(step) : int.MaxValue;
            if (position > firstAffected)
            {
                var affected = WorkflowSteps.Ordered[firstAffected];
                return new ConflictException(
                    $"Hồ sơ đang ở bước \"{WorkflowSteps.StatusDisplayName(record.Status)}\", đã qua bước \"{WorkflowSteps.DisplayName(affected)}\" "
                    + $"mà hồ sơ luồng \"{next.Name}\" cấu hình khác — không đổi được. Hãy mở lại hồ sơ về trước bước đó nếu cần.");
            }
        }

        var from = record.Status;
        var to = RecordStateMachine.Realign(record.Status, next.ActiveSteps());
        _repo.AddHistory(NewHistory(record, from, to, WorkflowAction.ChangeProfile, reason,
            $"Đổi hồ sơ luồng: {current.Name} → {next.Name}."));
        record.WorkflowProfileCode = next.Code;
        record.Status = to;
        record.UpdatedAt = DateTime.UtcNow;
        return null;
    }

    private async Task<PeriodParticipantDto> ReloadParticipantAsync(Guid periodId, Guid recordId, CancellationToken ct)
    {
        var period = await _repo.FindPeriodAsync(periodId, ct) ?? throw new NotFoundException($"Không tìm thấy kỳ đánh giá với Id: {periodId}.");
        var saved = (await _repo.ListRecordsAsync(periodId, ct)).First(r => r.Id == recordId);
        return ToParticipant(saved, ReadSettings(period), ReadCriteria(period));
    }

    #endregion

    #region Hỗ trợ

    private void EnsureManage() => _guard.Ensure(PermissionCodes.PeriodManage, AccessTarget.None);

    /// <summary>
    /// Kiểm tra cấu hình kỳ, kể cả quyền thực hiện bước nội bộ phải là quyền đánh giá trong danh mục; bộ tiêu chí 09A cần bước
    /// đăng ký sản phẩm ở mọi hồ sơ luồng.
    /// </summary>
    private static List<string> ValidateSettings(PeriodSettings settings, string? selfScoreForm) =>
        settings.Validate(WorkflowActions.IsAssignableStepPermission, selfScoreForm);

    /// <summary>Bộ tiêu chí theo Id — phải đã xuất bản (bản nháp/lưu trữ không chọn được cho kỳ).</summary>
    private async Task<CriteriaSet> LoadPublishedSetAsync(Guid id, CancellationToken ct)
    {
        var set = await _criteriaSets.FindAsync(id, ct)
            ?? throw new ValidationException("Không tìm thấy bộ tiêu chí đã chọn.");
        if (set.Status != CriteriaSetStatus.Published)
            throw new ValidationException($"Bộ tiêu chí \"{set.Name}\" đang ở trạng thái \"{CriteriaSetService.StatusName(set.Status)}\"; "
                + "kỳ chỉ chọn được bộ đã xuất bản.");
        return set;
    }

    /// <summary>Gắn bộ tiêu chí vào kỳ và chụp nguyên bộ.</summary>
    private static void AttachCriteria(EvaluationPeriod period, CriteriaSet set, DateTime now)
    {
        period.CriteriaSetId = set.Id;
        period.CriteriaSnapshot = set.TakeSnapshot(now).ToJson();
    }

    /// <summary>Ảnh chụp bộ tiêu chí của kỳ (null nếu chưa chọn).</summary>
    private static CriteriaSnapshot? ReadCriteria(EvaluationPeriod period)
    {
        try
        {
            return period.GetCriteria();
        }
        catch (FormatException)
        {
            throw new ConflictException("Ảnh chụp bộ tiêu chí của kỳ bị lỗi định dạng. Hãy chọn lại bộ tiêu chí cho kỳ (khi kỳ còn dự thảo).");
        }
    }

    private async Task<EvaluationPeriod> LoadForWriteAsync(Guid id, uint? version, CancellationToken ct)
    {
        if (version is null)
            throw new ValidationException("Thiếu phiên bản dữ liệu (version) của kỳ. Hãy tải lại trang rồi thực hiện lại.");
        var period = await _repo.FindPeriodAsync(id, ct)
            ?? throw new NotFoundException($"Không tìm thấy kỳ đánh giá với Id: {id}.");
        if (period.Version != version)
            throw new ConflictException("Kỳ đánh giá đã được người khác cập nhật. Hãy tải lại trang để xem dữ liệu mới nhất.");
        return period;
    }

    private static PeriodSettings ReadSettings(EvaluationPeriod period)
    {
        try
        {
            return period.GetSettings();
        }
        catch (FormatException)
        {
            throw new ConflictException("Cấu hình của kỳ đánh giá bị lỗi định dạng. Hãy lưu lại cấu hình kỳ.");
        }
    }

    private EvaluationRecordHistory NewHistory(EvaluationRecord record, RecordStatus? from, RecordStatus to, WorkflowAction action, string? reason, string comment) => new()
    {
        RecordId = record.Id,
        FromStatus = from,
        ToStatus = to,
        Action = action,
        Reason = reason,
        ScoreBefore = record.EffectiveScore(),
        ScoreAfter = record.EffectiveScore(),
        GradeBefore = record.EffectiveGrade(),
        GradeAfter = record.EffectiveGrade(),
        ActorId = _currentUser.UserId,
        ActorName = string.IsNullOrWhiteSpace(_currentUser.UserName) ? "system" : _currentUser.UserName,
        Comment = comment,
        CreatedAt = DateTime.UtcNow
    };

    private static PeriodParticipantDto ToParticipant(EvaluationRecord r, PeriodSettings settings, CriteriaSnapshot? criteria)
    {
        var profile = EvaluationMapping.ProfileOf(settings, r);
        return new PeriodParticipantDto
        {
            RecordId = r.Id,
            Version = r.Version,
            MemberId = r.MemberId,
            Username = r.Member?.Username ?? string.Empty,
            FullName = r.Member?.FullName ?? string.Empty,
            DepartmentId = r.DepartmentId,
            DepartmentName = r.Department?.Name,
            PartyCellId = r.PartyCellId,
            PartyCellName = r.PartyCell?.Name,
            WeightFrameCode = r.WeightFrameCode,
            WeightFrameName = criteria?.Content.FindFrame(r.WeightFrameCode)?.Name,
            ApprovalAuthority = r.ApprovalAuthority.ToString(),
            WorkflowProfileCode = profile.Code,
            WorkflowProfileName = profile.Name,
            Status = r.Status.ToString(),
            StatusDisplayName = WorkflowSteps.StatusDisplayName(r.Status)
        };
    }

    #endregion
}
