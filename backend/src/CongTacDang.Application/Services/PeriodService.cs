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
/// Quản lý kỳ đánh giá (docs/thiet-ke/luong-danh-gia.md mục 3): tạo từ mẫu cấu hình, sửa cấu hình (chỉ khi dự thảo — riêng
/// thời hạn sửa được khi đang mở/khóa dữ liệu), danh sách người được đánh giá, chuyển trạng thái kỳ. Quyền <c>period.manage</c>.
/// </summary>
public interface IPeriodService
{
    Task<List<EvaluationPeriodDto>> GetPeriodsAsync(CancellationToken ct = default);
    Task<EvaluationPeriodDto?> GetActivePeriodAsync(CancellationToken ct = default);
    Task<EvaluationPeriodDto> GetPeriodAsync(Guid id, CancellationToken ct = default);
    IReadOnlyList<PeriodPresetDto> GetPresets();
    Task<EvaluationPeriodDto> CreatePeriodAsync(CreatePeriodDto dto, CancellationToken ct = default);
    Task<EvaluationPeriodDto> UpdatePeriodAsync(Guid id, UpdatePeriodDto dto, CancellationToken ct = default);
    Task<EvaluationPeriodDto> OpenAsync(Guid id, PeriodTransitionDto dto, CancellationToken ct = default);
    Task<EvaluationPeriodDto> LockAsync(Guid id, PeriodTransitionDto dto, CancellationToken ct = default);
    Task<EvaluationPeriodDto> UnlockAsync(Guid id, PeriodTransitionDto dto, CancellationToken ct = default);
    Task<EvaluationPeriodDto> CloseAsync(Guid id, PeriodTransitionDto dto, CancellationToken ct = default);

    Task<List<PeriodParticipantDto>> GetParticipantsAsync(Guid periodId, CancellationToken ct = default);
    Task<List<ParticipantCandidateDto>> GetCandidatesAsync(Guid periodId, Guid? departmentId, Guid? partyCellId, string? query, CancellationToken ct = default);
    Task<AddParticipantsResultDto> AddParticipantsAsync(Guid periodId, AddParticipantsDto dto, CancellationToken ct = default);

    /// <summary>
    /// Thêm người vào kỳ nhưng <b>không lưu</b> (dùng trong transaction của khung import). Trả số hồ sơ tạo mới và
    /// danh sách lý do bỏ qua.
    /// </summary>
    Task<AddParticipantsResultDto> StageParticipantsAsync(Guid periodId, IReadOnlyCollection<Guid> memberIds, string source, CancellationToken ct = default);

    Task RemoveParticipantAsync(Guid periodId, Guid recordId, uint? version, CancellationToken ct = default);
    Task<PeriodParticipantDto> UpdateSnapshotAsync(Guid periodId, Guid recordId, UpdateSnapshotDto dto, CancellationToken ct = default);
}

/// <summary>Triển khai quản lý kỳ đánh giá.</summary>
public sealed class PeriodService : IPeriodService
{
    private readonly IEvaluationWorkflowRepository _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;

    public PeriodService(
        IEvaluationWorkflowRepository repo,
        IUnitOfWork unitOfWork,
        IAuthorizationGuard guard,
        ICurrentUserService currentUser)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
        _guard = guard;
        _currentUser = currentUser;
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
            Settings = p.Build()
        }).ToList();

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
              ?? throw new ValidationException($"Mẫu cấu hình \"{dto.Preset}\" không tồn tại. Hãy chọn một trong: "
                  + string.Join(", ", PeriodSettings.Presets.Select(p => p.Name)) + ".");

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
            CreatedAt = DateTime.UtcNow
        };
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
        var settingsChanged = false;
        if (dto.Settings != null)
        {
            var next = dto.Settings.Normalize();
            var errors = next.Validate();
            if (errors.Count > 0)
                throw new ValidationException("Cấu hình kỳ chưa hợp lệ: " + string.Join(" ", errors));

            if (period.Status != PeriodStatus.Draft && !next.DiffersOnlyInDeadlines(current))
                throw new ConflictException("Kỳ đã mở: chỉ được sửa thời hạn các bước. Bật/tắt bước, mẫu tự chấm, tham số chỉ sửa được khi kỳ còn dự thảo.");

            settingsChanged = next.ToJson() != current.ToJson();
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

        // Kỳ dự thảo đổi cấu hình bước → trạng thái đầu của hồ sơ tính lại theo cấu hình mới.
        if (settingsChanged && period.Status == PeriodStatus.Draft)
            await RecalculateInitialStatusAsync(period, ct);

        _unitOfWork.SetOriginalVersion(period, dto.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await GetPeriodAsync(period.Id, ct);
    }

    /// <inheritdoc />
    public async Task<EvaluationPeriodDto> OpenAsync(Guid id, PeriodTransitionDto dto, CancellationToken ct = default)
    {
        EnsureManage();
        var period = await LoadForWriteAsync(id, dto?.Version, ct);
        if (period.Status != PeriodStatus.Draft)
            throw new ConflictException($"Kỳ đang ở trạng thái \"{WorkflowSteps.PeriodStatusDisplayName(period.Status)}\", chỉ mở được kỳ dự thảo.");

        var errors = ReadSettings(period).Validate();
        if (errors.Count > 0)
            throw new ValidationException("Cấu hình kỳ chưa hợp lệ, chưa mở được kỳ: " + string.Join(" ", errors));

        await RecalculateInitialStatusAsync(period, ct);
        return await TransitionAsync(period, PeriodStatus.Open, dto?.Reason, dto?.Version, ct);
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
        if (clean is { Length: > 1000 })
            throw new ValidationException("Lý do không được dài quá 1000 ký tự.");
        period.Status = next;
        period.StatusReason = string.IsNullOrEmpty(clean) ? null : clean;
        period.StatusChangedAt = DateTime.UtcNow;
        period.StatusChangedBy = _currentUser.UserId;
        _unitOfWork.SetOriginalVersion(period, version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await GetPeriodAsync(period.Id, ct);
    }

    /// <summary>Hồ sơ còn ở trạng thái đầu (chưa ai thao tác) → đặt theo bước bật đầu tiên của cấu hình hiện tại.</summary>
    private async Task RecalculateInitialStatusAsync(EvaluationPeriod period, CancellationToken ct)
    {
        var initial = RecordStateMachine.Initial(ReadSettings(period).EnabledSteps());
        var initialStates = new[] { RecordStatus.AwaitingRegistration, RecordStatus.AwaitingTaskApproval, RecordStatus.AwaitingSelfScore };
        foreach (var record in await _repo.ListRecordsForUpdateAsync(period.Id, ct))
        {
            if (record.Status == initial || !initialStates.Contains(record.Status) || record.SelfScoredAt.HasValue
                || record.Tasks.Any(t => !t.IsDeleted))
                continue;

            _repo.AddHistory(NewHistory(record, record.Status, initial, WorkflowAction.Recalculate, null,
                "Tính lại bước đầu theo cấu hình kỳ."));
            record.Status = initial;
        }
    }

    #endregion

    #region Người được đánh giá

    /// <inheritdoc />
    public async Task<List<PeriodParticipantDto>> GetParticipantsAsync(Guid periodId, CancellationToken ct = default)
    {
        EnsureManage();
        _ = await _repo.FindPeriodAsync(periodId, ct) ?? throw new NotFoundException($"Không tìm thấy kỳ đánh giá với Id: {periodId}.");
        var records = await _repo.ListRecordsAsync(periodId, ct);
        return records.Select(ToParticipant).ToList();
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

        var result = await StageParticipantsAsync(periodId, ids, string.Join(", ", sources), ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return result;
    }

    /// <inheritdoc />
    public async Task<AddParticipantsResultDto> StageParticipantsAsync(Guid periodId, IReadOnlyCollection<Guid> memberIds, string source, CancellationToken ct = default)
    {
        EnsureManage();
        var period = await _repo.FindPeriodAsync(periodId, ct)
            ?? throw new NotFoundException($"Không tìm thấy kỳ đánh giá với Id: {periodId}.");
        if (period.Status is not (PeriodStatus.Draft or PeriodStatus.Open))
            throw new ConflictException("Chỉ thêm người được đánh giá khi kỳ còn dự thảo hoặc đang mở.");

        var initial = RecordStateMachine.Initial(ReadSettings(period).EnabledSteps());
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

            // Hồ sơ đã bị bỏ khỏi danh sách trước đó (xóa mềm) → khôi phục thay vì tạo mới (unique kỳ + cán bộ).
            var record = await _repo.FindRecordIncludingDeletedAsync(periodId, id, ct);
            var isNew = record == null;
            record ??= new EvaluationRecord { Id = Guid.NewGuid(), PeriodId = periodId, MemberId = id, CreatedAt = now };
            record.IsDeleted = false;
            record.DeletedAt = null;
            record.DeletedBy = null;
            record.DepartmentId = member.DepartmentId;
            record.PartyCellId = member.PartyCellId;
            record.JobGroup = member.JobGroup;
            record.ApprovalAuthority = member.ApprovalAuthority;
            var from = isNew ? (RecordStatus?)null : record.Status;
            record.Status = initial;
            record.UpdatedAt = now;
            if (isNew)
                _repo.AddRecord(record);

            _repo.AddHistory(NewHistory(record, from, initial, WorkflowAction.Create, null,
                $"Thêm vào danh sách được đánh giá ({source}); ảnh chụp Phòng: {member.DepartmentName ?? "—"}, Chi bộ: {member.PartyCellName ?? "—"}."));
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

        var initial = RecordStateMachine.Initial(ReadSettings(record.Period).EnabledSteps());
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
            throw new ConflictException("Chỉ sửa ảnh chụp Phòng/Chi bộ/khung/cấp quyết định khi kỳ còn dự thảo hoặc đang mở.");
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
        if (!string.IsNullOrWhiteSpace(dto.JobGroup))
        {
            if (!Enum.TryParse<JobGroup>(dto.JobGroup, true, out var group) || !Enum.IsDefined(group))
                throw new ValidationException("Khung chức danh không hợp lệ.");
            if (group != record.JobGroup)
            {
                changes.Add("khung chức danh");
                record.JobGroup = group;
            }
        }
        if (!string.IsNullOrWhiteSpace(dto.ApprovalAuthority))
        {
            if (!Enum.TryParse<ApprovalAuthority>(dto.ApprovalAuthority, true, out var authority) || !Enum.IsDefined(authority))
                throw new ValidationException("Cấp quyết định phải là CoSo hoặc CapTren.");
            if (authority != record.ApprovalAuthority)
            {
                changes.Add("cấp quyết định");
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

        var saved = (await _repo.ListRecordsAsync(periodId, ct)).First(r => r.Id == recordId);
        return ToParticipant(saved);
    }

    #endregion

    #region Hỗ trợ

    private void EnsureManage() => _guard.Ensure(PermissionCodes.PeriodManage, AccessTarget.None);

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

    private static PeriodParticipantDto ToParticipant(EvaluationRecord r) => new()
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
        JobGroup = r.JobGroup.ToString(),
        ApprovalAuthority = r.ApprovalAuthority.ToString(),
        Status = r.Status.ToString(),
        StatusDisplayName = WorkflowSteps.StatusDisplayName(r.Status)
    };

    #endregion
}
