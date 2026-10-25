using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Reports;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.Services;

/// <summary>Hồ sơ đánh giá tập thể (Mẫu 06–08) và biên bản hội nghị (Mẫu 12–13).</summary>
public interface ICollectiveEvaluationService
{
    Task<List<CollectiveEvaluationRecordDto>> GetCollectiveRecordsAsync(Guid periodId, Guid requesterId, string? form = null);

    Task<CollectiveEvaluationRecordDto> GetCollectiveRecordAsync(Guid id, Guid requesterId);

    Task<CollectiveEvaluationRecordDto> CreateCollectiveRecordAsync(Guid requesterId, SaveCollectiveEvaluationRequestDto dto);

    /// <summary>Không đổi kỳ, biểu mẫu, tổ chức; kiểm tra phiên bản.</summary>
    Task<CollectiveEvaluationRecordDto> UpdateCollectiveRecordAsync(Guid id, Guid requesterId, SaveCollectiveEvaluationRequestDto dto);

    /// <summary>Có <paramref name="periodId"/> thì kèm khung tiêu chí tập thể và điểm tối đa theo bộ tiêu chí của kỳ.</summary>
    Task<CollectiveFormCatalogDto> GetFormCatalogAsync(Guid? periodId = null);

    /// <summary>Không đổi kỳ, tổ chức, biểu mẫu, kết quả kiểm phiếu; kiểm tra phiên bản.</summary>
    Task<EvaluationMeetingDto> UpdateMeetingAsync(Guid id, Guid requesterId, SaveEvaluationMeetingRequestDto dto);

    Task<List<EvaluationMeetingDto>> GetMeetingsAsync(Guid periodId, Guid requesterId, Guid? partyCellId = null);

    Task<EvaluationMeetingDto> GetMeetingAsync(Guid id, Guid requesterId);

    Task<EvaluationMeetingDto> CreateMeetingAsync(Guid requesterId, SaveEvaluationMeetingRequestDto dto);
}

public class CollectiveEvaluationService : ICollectiveEvaluationService
{
    private readonly ICollectiveEvaluationRepository _collectiveRepo;
    private readonly IEvaluationMeetingRepository _meetingRepo;
    private readonly IEvaluationRepository _evaluationRepo;
    private readonly IUserRepository _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthorizationGuard _guard;

    public CollectiveEvaluationService(
        ICollectiveEvaluationRepository collectiveRepo,
        IEvaluationMeetingRepository meetingRepo,
        IEvaluationRepository evaluationRepo,
        IUserRepository userRepo,
        IUnitOfWork unitOfWork,
        IAuthorizationGuard guard)
    {
        _collectiveRepo = collectiveRepo;
        _meetingRepo = meetingRepo;
        _evaluationRepo = evaluationRepo;
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
        _guard = guard;
    }

    public async Task<List<CollectiveEvaluationRecordDto>> GetCollectiveRecordsAsync(Guid periodId, Guid requesterId, string? form = null)
    {
        var scope = CollectiveReadScope();
        var parsedForm = ParseCollectiveForm(form);
        var records = await _collectiveRepo.GetByPeriodAsync(periodId, parsedForm);
        var visible = records.Where(record => scope.Matches(null, record.DepartmentId, record.PartyCellId)).ToList();
        if (visible.Count == 0)
            return new List<CollectiveEvaluationRecordDto>();
        var period = await _evaluationRepo.GetPeriodByIdAsync(periodId);
        var periodClosed = period?.Status == PeriodStatus.Closed;
        return visible.Select(record => MapCollective(record, periodClosed)).ToList();
    }

    public async Task<CollectiveEvaluationRecordDto> GetCollectiveRecordAsync(Guid id, Guid requesterId)
    {
        var record = await _collectiveRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Không tìm thấy hồ sơ tập thể với Id: {id}");

        var target = new AccessTarget(DepartmentId: record.DepartmentId, PartyCellId: record.PartyCellId);
        if (!_guard.Can(PermissionCodes.EvaluationRead, target) && !_guard.Can(PermissionCodes.CollectiveManage, target))
            throw new ForbiddenException("Bạn không có quyền xem hồ sơ tập thể của tổ chức này (ngoài phạm vi được gán).");
        return MapCollective(record, record.Period?.Status == PeriodStatus.Closed);
    }

    public async Task<CollectiveEvaluationRecordDto> CreateCollectiveRecordAsync(Guid requesterId, SaveCollectiveEvaluationRequestDto dto)
    {
        var form = ParseCollectiveForm(dto.Form)
            ?? throw new ArgumentException("Hồ sơ tập thể phải có mã M06, M07 hoặc M08.");
        if (string.IsNullOrWhiteSpace(dto.SubjectName))
            throw new ArgumentException("Tên tập thể hoặc lĩnh vực đánh giá không được để trống.");
        // Mẫu 07 là báo cáo của tập thể cấp ủy: gắn tổ chức Đảng, không gắn Phòng.
        if (form == CollectiveEvaluationForm.M07 && (dto.PartyCellId is null || dto.PartyCellId == Guid.Empty || dto.DepartmentId.HasValue))
            throw new ArgumentException("Mẫu 07 là tự đánh giá của tập thể Đảng ủy, Chi ủy, Chi bộ: hãy chọn tổ chức Đảng.");

        // Mọi đơn vị hồ sơ gắn với đều phải trong phạm vi.
        _guard.Ensure(PermissionCodes.CollectiveManage, AccessTarget.ForUnitsWrite(dto.DepartmentId, dto.PartyCellId));
        var period = await _evaluationRepo.GetPeriodByIdAsync(dto.PeriodId)
            ?? throw new KeyNotFoundException($"Không tìm thấy kỳ đánh giá với Id: {dto.PeriodId}");
        if (period.Status == PeriodStatus.Closed)
            throw new ConflictException($"Kỳ \"{period.Name}\" đã đóng nên không tạo được hồ sơ tập thể. Hãy liên hệ người quản lý kỳ nếu cần mở lại.");
        var scores = ResolveScores(period, form, dto);
        // CSDL cũng chặn bằng chỉ mục duy nhất.
        var existing = await _collectiveRepo.GetByPeriodAsync(dto.PeriodId, form);
        var duplicate = existing.FirstOrDefault(r => r.PartyCellId == dto.PartyCellId && r.DepartmentId == dto.DepartmentId);
        if (duplicate != null)
            throw new ConflictException(
                $"Kỳ \"{period.Name}\" đã có hồ sơ {form} \"{duplicate.SubjectName}\" của tổ chức này. Hãy mở hồ sơ đó để sửa thay vì tạo mới.");

        var record = new CollectiveEvaluationRecord
        {
            PeriodId = dto.PeriodId,
            Form = form,
            PartyCellId = dto.PartyCellId,
            DepartmentId = dto.DepartmentId,
            Status = CollectiveRecordStatus.Submitted,
            CreatedBy = requesterId
        };
        ApplyContent(record, dto, scores, requesterId);
        record.Items = BuildItems(form, dto.Items);

        await _collectiveRepo.AddAsync(record);
        var saved = await _collectiveRepo.GetByIdAsync(record.Id) ?? record;
        return MapCollective(saved, periodClosed: false);
    }

    /// <summary>Kỳ đã đóng thì không sửa được.</summary>
    public async Task<CollectiveEvaluationRecordDto> UpdateCollectiveRecordAsync(Guid id, Guid requesterId, SaveCollectiveEvaluationRequestDto dto)
    {
        var record = await _collectiveRepo.GetForUpdateAsync(id)
            ?? throw new KeyNotFoundException($"Không tìm thấy hồ sơ tập thể với Id: {id}");
        _guard.Ensure(PermissionCodes.CollectiveManage, AccessTarget.ForUnitsWrite(record.DepartmentId, record.PartyCellId));
        if (record.Period.Status == PeriodStatus.Closed)
            throw new ConflictException($"Kỳ \"{record.Period.Name}\" đã đóng nên không sửa được hồ sơ tập thể. Hãy liên hệ người quản lý kỳ nếu cần mở lại.");
        if (string.IsNullOrWhiteSpace(dto.SubjectName))
            throw new ArgumentException("Tên tập thể hoặc lĩnh vực đánh giá không được để trống.");
        // Bắt buộc phiên bản: thiếu phiên bản sẽ bỏ qua kiểm tra đồng thời.
        if (dto.Version is null)
            throw new ValidationException("Thiếu phiên bản hồ sơ tập thể (version). Hãy tải lại hồ sơ rồi lưu lại.");

        var scores = ResolveScores(record.Period, record.Form, dto);

        ApplyContent(record, dto, scores, requesterId);
        _collectiveRepo.ReplaceItems(record, BuildItems(record.Form, dto.Items));
        _unitOfWork.SetOriginalVersion(record, dto.Version);
        await _unitOfWork.SaveChangesAsync();

        var saved = await _collectiveRepo.GetByIdAsync(record.Id) ?? record;
        return MapCollective(saved, periodClosed: false);
    }

    public async Task<CollectiveFormCatalogDto> GetFormCatalogAsync(Guid? periodId = null)
    {
        var catalog = new CollectiveFormCatalogDto
        {
            Form07Strengths = Hd03FormCatalog.Form07Strengths.Select(s => new CollectiveFormSectionDto { Code = s.Code, Title = s.Title }).ToList(),
            Form06Fields = Hd03FormCatalog.Form06Fields.Select(s => new CollectiveFormSectionDto { Code = s.Code, Title = s.Title }).ToList(),
            Form08Categories = Hd03FormCatalog.Form08Categories.Select(s => new CollectiveFormSectionDto { Code = s.Code, Title = s.Title }).ToList()
        };
        if (!periodId.HasValue)
            return catalog;

        var period = await _evaluationRepo.GetPeriodByIdAsync(periodId.Value)
            ?? throw new KeyNotFoundException($"Không tìm thấy kỳ đánh giá với Id: {periodId.Value}");
        var content = EvaluationMapping.SafeCriteria(period)?.Content;
        var parameters = content?.Parameters ?? new CriteriaParameters();
        catalog.GeneralMaxScore = parameters.CollectiveGeneralMaxScore;
        catalog.TaskMaxScore = parameters.CollectiveTaskMaxScore;
        catalog.CollectiveGroups = (content?.CollectiveGroups ?? new List<CollectiveCriteriaGroup>())
            .Select(group => new CollectiveCriteriaGroupDto
            {
                Code = group.Code,
                Name = group.Name,
                Kind = group.Kind.ToString(),
                Items = group.Items.Select(item => new CollectiveCriteriaItemDto
                {
                    Code = item.Code,
                    Text = item.Text,
                    MaxScore = item.MaxScore
                }).ToList()
            })
            .ToList();
        return catalog;
    }

    private sealed record CollectiveScores(double General, double Task, string CriteriaScoresJson);

    /// <summary>
    /// Bộ có khung tập thể và mẫu M06/M07 → chấm từng tiêu chí, điểm nhóm là tổng theo nhóm (bỏ qua điểm nhóm gửi lên);
    /// ngược lại nhập thẳng hai điểm nhóm.
    /// </summary>
    private static CollectiveScores ResolveScores(EvaluationPeriod period, CollectiveEvaluationForm form, SaveCollectiveEvaluationRequestDto dto)
    {
        var content = EvaluationMapping.SafeCriteria(period)?.Content;
        var parameters = content?.Parameters ?? new CriteriaParameters();
        var groups = content?.CollectiveGroups ?? new List<CollectiveCriteriaGroup>();
        var usesFramework = groups.Any(g => g.Items.Count > 0)
            && form is CollectiveEvaluationForm.M06 or CollectiveEvaluationForm.M07;

        if (!usesFramework)
        {
            ValidateScore(dto.GeneralCriteriaScore, parameters.CollectiveGeneralMaxScore, "Điểm nhóm tiêu chí chung");
            ValidateScore(dto.TaskCriteriaScore, parameters.CollectiveTaskMaxScore, "Điểm nhóm kết quả thực hiện nhiệm vụ");
            return new CollectiveScores(dto.GeneralCriteriaScore, dto.TaskCriteriaScore, "{}");
        }

        if (dto.CriteriaScores == null || dto.CriteriaScores.Count == 0)
            throw new ArgumentException("Bộ tiêu chí của kỳ có khung tiêu chí tập thể: hãy chấm điểm từng tiêu chí (criteriaScores).");

        var input = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (rawCode, value) in dto.CriteriaScores)
        {
            var code = rawCode?.Trim() ?? string.Empty;
            if (!input.TryAdd(code, value))
                throw new ArgumentException($"Tiêu chí \"{code}\" được chấm nhiều lần.");
        }

        var known = groups.SelectMany(g => g.Items).Select(i => i.Code).ToHashSet(StringComparer.Ordinal);
        var unknown = input.Keys.Where(code => !known.Contains(code)).ToList();
        if (unknown.Count > 0)
            throw new ArgumentException(
                $"Tiêu chí {string.Join(", ", unknown.Select(c => $"\"{c}\""))} không có trong khung tiêu chí tập thể của kỳ.");

        var clean = new SortedDictionary<string, double>(StringComparer.Ordinal);
        double general = 0, task = 0;
        foreach (var group in groups)
        {
            foreach (var item in group.Items)
            {
                if (!input.TryGetValue(item.Code, out var score))
                    throw new ArgumentException($"Chưa chấm điểm tiêu chí {item.Code} — {item.Text}.");
                if (!double.IsFinite(score) || score < 0 || score > item.MaxScore)
                    throw new ArgumentException($"Điểm tiêu chí {item.Code} — {item.Text} phải từ 0 đến {item.MaxScore} điểm.");
                clean[item.Code] = score;
                if (group.Kind == CollectiveScoreKind.Task)
                    task += score;
                else
                    general += score;
            }
        }

        return new CollectiveScores(Math.Round(general, 2), Math.Round(task, 2), JsonSerializer.Serialize(clean));
    }

    /// <summary>Dữ liệu hỏng → rỗng.</summary>
    public static Dictionary<string, double> DeserializeCriteriaScores(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, double>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, double>>(json) ?? new Dictionary<string, double>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, double>();
        }
    }

    private static void ApplyContent(CollectiveEvaluationRecord record, SaveCollectiveEvaluationRequestDto dto, CollectiveScores scores, Guid requesterId)
    {
        record.HeadId = dto.HeadId;
        record.SubjectName = Limit(dto.SubjectName.Trim(), 300, "Tên tập thể hoặc lĩnh vực đánh giá");
        record.Strengths = Limit(dto.Strengths, MaxTextLength, "Ưu điểm, kết quả đạt được");
        record.Limitations = Limit(dto.Limitations, MaxTextLength, "Hạn chế, khuyết điểm");
        record.Causes = Limit(dto.Causes, MaxTextLength, "Nguyên nhân của hạn chế, khuyết điểm");
        record.PreviousRemediation = Limit(dto.PreviousRemediation, MaxTextLength, "Kết quả khắc phục hạn chế, khuyết điểm kỳ trước");
        record.Explanation = Limit(dto.Explanation, MaxTextLength, "Giải trình những vấn đề được gợi ý kiểm điểm");
        record.Responsibilities = Limit(dto.Responsibilities, MaxTextLength, "Trách nhiệm của tập thể, cá nhân");
        record.RemediationPlan = Limit(dto.RemediationPlan, MaxTextLength, "Phương hướng, biện pháp khắc phục");
        record.GeneralCriteriaScore = scores.General;
        record.TaskCriteriaScore = scores.Task;
        record.TotalScore = Math.Round(scores.General + scores.Task, 2);
        record.CriteriaScores = scores.CriteriaScoresJson;
        record.SelfProposedGrade = ParseGrade(dto.SelfProposedGrade);
        record.Sections = SerializeSections(record.Form, dto.Sections);
        record.UpdatedBy = requesterId;
        record.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Khoảng 3–4 trang A4.</summary>
    private const int MaxTextLength = 20000;

    private static string Limit(string? value, int max, string name)
    {
        var text = value ?? string.Empty;
        if (text.Length > max)
            throw new ArgumentException($"Nội dung \"{name}\" dài {text.Length} ký tự, vượt giới hạn {max} ký tự. Hãy rút gọn hoặc gửi kèm phụ lục.");
        return text;
    }

    /// <summary>Chỉ nhận mã có trong biểu mẫu.</summary>
    private static string SerializeSections(CollectiveEvaluationForm form, Dictionary<string, string>? sections)
    {
        var allowed = form switch
        {
            CollectiveEvaluationForm.M07 => Hd03FormCatalog.Form07Strengths.ToDictionary(s => s.Code, s => s.Title),
            CollectiveEvaluationForm.M06 => Hd03FormCatalog.Form06Fields.ToDictionary(s => s.Code, s => s.Title),
            _ => new Dictionary<string, string>()
        };
        var clean = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (code, text) in sections ?? new Dictionary<string, string>())
        {
            if (!allowed.TryGetValue(code?.Trim() ?? string.Empty, out var title))
                throw new ArgumentException(
                    $"Mục \"{code}\" không có trong biểu mẫu {form}. Các mục hợp lệ: {(allowed.Count == 0 ? "(không có)" : string.Join(", ", allowed.Keys))}.");
            if (!string.IsNullOrWhiteSpace(text))
                clean[code!.Trim()] = Limit(text, MaxTextLength, $"Mục {code} — {title}");
        }
        if (form == CollectiveEvaluationForm.M06)
            ValidateForm06Counts(clean);
        return JsonSerializer.Serialize(clean);
    }

    /// <summary>Bốn số nhiệm vụ của Mẫu 06 là số nguyên không âm; tổng các nhóm không vượt số nhiệm vụ được giao.</summary>
    private static void ValidateForm06Counts(IReadOnlyDictionary<string, string> sections)
    {
        var counts = new Dictionary<string, int>();
        foreach (var code in new[] { Hd03FormCatalog.Form06AssignedCode, Hd03FormCatalog.Form06OnTimeCode, Hd03FormCatalog.Form06LateCode, Hd03FormCatalog.Form06ObjectiveCode })
        {
            if (!sections.TryGetValue(code, out var raw))
                continue;
            if (!int.TryParse(raw.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value) || value > 100000)
                throw new ArgumentException($"\"{Hd03FormCatalog.Form06Fields.First(f => f.Code == code).Title}\" phải là số nguyên không âm.");
            counts[code] = value;
        }
        if (counts.TryGetValue(Hd03FormCatalog.Form06AssignedCode, out var assigned)
            && counts.Where(c => c.Key != Hd03FormCatalog.Form06AssignedCode).Sum(c => c.Value) > assigned)
            throw new ArgumentException("Tổng số nhiệm vụ đúng hạn, chậm tiến độ và chưa hoàn thành vượt tổng số nhiệm vụ được giao. Hãy kiểm tra lại.");
    }

    /// <summary>Dữ liệu hỏng → rỗng.</summary>
    public static Dictionary<string, string> DeserializeSections(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    /// <summary>Mẫu 08 bắt buộc mã nhóm nội dung 1–13 và tên nhiệm vụ hoặc kết quả.</summary>
    private static List<CollectiveEvaluationItem> BuildItems(CollectiveEvaluationForm form, IEnumerable<CollectiveEvaluationItemDto>? items)
    {
        var list = (items ?? Enumerable.Empty<CollectiveEvaluationItemDto>()).ToList();
        if (list.Count > 200)
            throw new ArgumentException($"Hồ sơ có {list.Count} dòng nội dung, vượt giới hạn 200 dòng.");
        var result = new List<CollectiveEvaluationItem>();
        for (var index = 0; index < list.Count; index++)
        {
            var item = list[index];
            var category = item.Category?.Trim() ?? string.Empty;
            if (form == CollectiveEvaluationForm.M08)
            {
                if (Hd03FormCatalog.FindForm08Category(category) == null)
                    throw new ArgumentException(
                        $"Dòng {index + 1}: nhóm nội dung \"{item.Category}\" không có trong Mẫu 08. Hãy chọn một trong các nhóm 1–13 của biểu mẫu.");
                if (string.IsNullOrWhiteSpace(item.TaskName) && string.IsNullOrWhiteSpace(item.Result))
                    throw new ArgumentException($"Dòng {index + 1}: cần nhập tên nhiệm vụ hoặc kết quả thực hiện.");
            }
            result.Add(new CollectiveEvaluationItem
            {
                ItemOrder = item.ItemOrder > 0 ? item.ItemOrder : index + 1,
                Category = Limit(category, 200, "Nhóm nội dung"),
                TaskName = Limit(item.TaskName?.Trim(), 500, "Tên nhiệm vụ"),
                PlanOrDirection = Limit(item.PlanOrDirection, 5000, "Kế hoạch hoặc sự chỉ đạo, yêu cầu"),
                Result = Limit(item.Result, 5000, "Kết quả thực hiện trong kỳ"),
                Limitations = Limit(item.Limitations, 5000, "Tồn tại, hạn chế hoặc thành tích"),
                Notes = Limit(item.Notes, 2000, "Ghi chú")
            });
        }
        return result;
    }

    public async Task<List<EvaluationMeetingDto>> GetMeetingsAsync(Guid periodId, Guid requesterId, Guid? partyCellId = null)
    {
        // Không có phạm vi phù hợp → danh sách rỗng.
        var scope = _guard.GetScope(PermissionCodes.MeetingRead).Union(_guard.GetScope(PermissionCodes.MeetingManage));
        if (scope.IsEmpty)
            return new List<EvaluationMeetingDto>();

        var meetings = await _meetingRepo.GetByPeriodAsync(periodId, partyCellId);
        return meetings
            .Where(meeting => scope.Matches(null, meeting.DepartmentId, meeting.PartyCellId))
            .Select(MapMeeting)
            .ToList();
    }

    public async Task<EvaluationMeetingDto> GetMeetingAsync(Guid id, Guid requesterId)
    {
        var meeting = await _meetingRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Không tìm thấy biên bản hội nghị với Id: {id}");

        var target = new AccessTarget(DepartmentId: meeting.DepartmentId, PartyCellId: meeting.PartyCellId);
        if (!_guard.Can(PermissionCodes.MeetingRead, target) && !_guard.Can(PermissionCodes.MeetingManage, target))
            throw new ForbiddenException("Bạn không có quyền xem biên bản của đơn vị này (ngoài phạm vi được gán). Hãy liên hệ quản trị hệ thống nếu cần được cấp quyền.");

        return MapMeeting(meeting);
    }

    /// <summary>
    /// Chỉ lưu tổng hợp phiếu không định danh. Biên bản gắn Chi bộ, Phòng, hoặc không gắn
    /// (cấp Công ty, chỉ phạm vi Toàn công ty).
    /// </summary>
    public async Task<EvaluationMeetingDto> CreateMeetingAsync(Guid requesterId, SaveEvaluationMeetingRequestDto dto)
    {
        var partyCellId = dto.PartyCellId == Guid.Empty ? null : dto.PartyCellId;
        var departmentId = dto.DepartmentId == Guid.Empty ? null : dto.DepartmentId;
        if (dto.FormCode is not ("M12" or "M13"))
            throw new ArgumentException("Biên bản chỉ hỗ trợ M12 hoặc M13.");
        WorkflowStep? stage = null;
        if (!string.IsNullOrWhiteSpace(dto.Stage))
        {
            stage = WorkflowSteps.Parse(dto.Stage);
            if (stage is not (WorkflowStep.B3A_COLLECTIVE or WorkflowStep.B4_DECISION))
                throw new ArgumentException("Biên bản chỉ dùng cho bước đề xuất của tập thể lãnh đạo (B3A_COLLECTIVE) hoặc quyết định (B4_DECISION).");
        }
        // Hội nghị quyết định mức là hội nghị cấp ủy, không phải Phòng.
        if (stage == WorkflowStep.B4_DECISION && departmentId.HasValue)
            throw new ArgumentException("Hội nghị quyết định mức xếp loại do cấp ủy tổ chức: chọn tổ chức Đảng hoặc Cấp Công ty, không chọn Phòng / đơn vị.");
        _guard.Ensure(PermissionCodes.MeetingManage, AccessTarget.ForUnitsWrite(departmentId, partyCellId));
        if (dto.FormCode == "M13" && dto.VoteSummaries.Count == 0)
            throw new ArgumentException("Mẫu 13 phải có tổng hợp phiếu theo từng hồ sơ cán bộ.");
        if (dto.InvitedCount < 0 || dto.PresentCount < 0 || dto.AbsentCount < 0 || dto.PresentCount > dto.InvitedCount)
            throw new ArgumentException("Số lượng triệu tập, có mặt, vắng mặt không hợp lệ: không âm và số có mặt không vượt số triệu tập.");
        _ = await _evaluationRepo.GetPeriodByIdAsync(dto.PeriodId)
            ?? throw new KeyNotFoundException($"Không tìm thấy kỳ đánh giá với Id: {dto.PeriodId}");

        var meeting = new EvaluationMeeting
        {
            PeriodId = dto.PeriodId,
            PartyCellId = partyCellId,
            DepartmentId = departmentId,
            Stage = stage,
            FormCode = dto.FormCode,
            MeetingType = dto.MeetingType,
            Location = dto.Location,
            StartedAt = dto.StartedAt,
            EndedAt = dto.EndedAt,
            InvitedCount = dto.InvitedCount,
            PresentCount = dto.PresentCount,
            AbsentCount = dto.AbsentCount,
            AbsentReasons = dto.AbsentReasons,
            ChairId = dto.ChairId,
            ChairName = dto.ChairName,
            SecretaryId = dto.SecretaryId,
            SecretaryName = dto.SecretaryName,
            MinutesContent = dto.MinutesContent,
            OutcomeContent = dto.OutcomeContent,
            VoteCountingContent = dto.VoteCountingContent,
            Details = SerializeDetails(dto.Details, dto.PresentCount),
            CreatedBy = requesterId,
            UpdatedBy = requesterId,
            UpdatedAt = DateTime.UtcNow
        };

        if (dto.FormCode == "M13")
        {
            foreach (var vote in dto.VoteSummaries)
            {
                if (vote.VotesExcellent < 0 || vote.VotesGood < 0 || vote.VotesSatisfactory < 0 || vote.VotesUnsatisfactory < 0
                    || vote.VotesNotRated < 0 || vote.InvalidVotes < 0)
                    throw new ArgumentException("Số phiếu không được âm.");
                var total = vote.VotesExcellent + vote.VotesGood + vote.VotesSatisfactory + vote.VotesUnsatisfactory + vote.VotesNotRated + vote.InvalidVotes;
                if (total > dto.PresentCount)
                    throw new ArgumentException(
                        $"Tổng số phiếu các mức và phiếu không hợp lệ ({total}) vượt số người có mặt ({dto.PresentCount}). Hãy kiểm tra lại kết quả kiểm phiếu.");
                if (meeting.VoteSummaries.Any(v => v.RecordId == vote.RecordId))
                    throw new ArgumentException("Mỗi hồ sơ chỉ có một dòng kết quả kiểm phiếu trong biên bản.");

                var record = await _evaluationRepo.GetRecordByIdAsync(vote.RecordId)
                    ?? throw new KeyNotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {vote.RecordId}");
                // Biên bản ở tổ chức cấp trên chứa được hồ sơ của cây con.
                if (record.PeriodId != dto.PeriodId
                    || !await OrgSubtreeRules.RecordWithinMeetingAsync(
                        _evaluationRepo, departmentId, partyCellId, record.DepartmentId, record.PartyCellId))
                    throw new ForbiddenException(
                        "Biên bản chỉ được chứa hồ sơ cùng kỳ, thuộc đơn vị/tổ chức Đảng của biên bản hoặc đơn vị, tổ chức cấp dưới của nó.");

                meeting.VoteSummaries.Add(new EvaluationMeetingVoteSummary
                {
                    RecordId = vote.RecordId,
                    VotesExcellent = vote.VotesExcellent,
                    VotesGood = vote.VotesGood,
                    VotesSatisfactory = vote.VotesSatisfactory,
                    VotesUnsatisfactory = vote.VotesUnsatisfactory,
                    VotesNotRated = vote.VotesNotRated,
                    InvalidVotes = vote.InvalidVotes,
                    Notes = vote.Notes
                });
            }
        }

        await _meetingRepo.AddAsync(meeting);
        var saved = await _meetingRepo.GetByIdAsync(meeting.Id) ?? meeting;
        return MapMeeting(saved);
    }

    /// <summary>Kỳ, tổ chức, biểu mẫu, bước và kết quả kiểm phiếu (đã dùng ghi nhận kết quả hồ sơ) không đổi.</summary>
    public async Task<EvaluationMeetingDto> UpdateMeetingAsync(Guid id, Guid requesterId, SaveEvaluationMeetingRequestDto dto)
    {
        var meeting = await _meetingRepo.GetForUpdateAsync(id)
            ?? throw new KeyNotFoundException($"Không tìm thấy biên bản hội nghị với Id: {id}");
        _guard.Ensure(PermissionCodes.MeetingManage, AccessTarget.ForUnitsWrite(meeting.DepartmentId, meeting.PartyCellId));
        if (meeting.Period.Status == PeriodStatus.Closed)
            throw new ConflictException($"Kỳ \"{meeting.Period.Name}\" đã đóng nên không sửa được biên bản. Hãy liên hệ người quản lý kỳ nếu cần mở lại.");
        if (dto.InvitedCount < 0 || dto.PresentCount < 0 || dto.AbsentCount < 0 || dto.PresentCount > dto.InvitedCount)
            throw new ArgumentException("Số lượng triệu tập, có mặt, vắng mặt không hợp lệ: không âm và số có mặt không vượt số triệu tập.");
        // Mẫu 12 cũng có kết quả kiểm phiếu khi bước đề xuất của tập thể ghi phiếu vào biên bản.
        if (meeting.VoteSummaries.Count > 0 && dto.PresentCount < MaxBallots(meeting))
            throw new ArgumentException("Số người có mặt nhỏ hơn tổng số phiếu đã ghi trong biên bản kiểm phiếu. Hãy kiểm tra lại.");
        if (dto.EndedAt.HasValue && dto.EndedAt.Value < dto.StartedAt)
            throw new ArgumentException("Thời điểm kết thúc hội nghị phải sau thời điểm bắt đầu.");

        meeting.MeetingType = Limit(dto.MeetingType, 200, "Loại hội nghị");
        meeting.Location = Limit(dto.Location, 300, "Địa điểm");
        meeting.StartedAt = dto.StartedAt;
        meeting.EndedAt = dto.EndedAt;
        meeting.InvitedCount = dto.InvitedCount;
        meeting.PresentCount = dto.PresentCount;
        meeting.AbsentCount = dto.AbsentCount;
        meeting.AbsentReasons = Limit(dto.AbsentReasons, 2000, "Lý do vắng mặt");
        meeting.ChairId = dto.ChairId;
        meeting.ChairName = Limit(dto.ChairName, 200, "Chủ trì hội nghị");
        meeting.SecretaryId = dto.SecretaryId;
        meeting.SecretaryName = Limit(dto.SecretaryName, 200, "Thư ký hội nghị");
        meeting.MinutesContent = Limit(dto.MinutesContent, MaxTextLength, "Nội dung biên bản");
        meeting.OutcomeContent = Limit(dto.OutcomeContent, MaxTextLength, "Kết quả hội nghị");
        meeting.VoteCountingContent = Limit(dto.VoteCountingContent, MaxTextLength, "Nội dung kiểm phiếu");
        meeting.Details = SerializeDetails(dto.Details, dto.PresentCount);
        meeting.UpdatedBy = requesterId;
        meeting.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.SetOriginalVersion(meeting, dto.Version);
        await _unitOfWork.SaveChangesAsync();

        var saved = await _meetingRepo.GetByIdAsync(meeting.Id) ?? meeting;
        return MapMeeting(saved);
    }

    private static int MaxBallots(EvaluationMeeting meeting) =>
        meeting.VoteSummaries.Count == 0 ? 0 : meeting.VoteSummaries.Max(v => v.TotalBallots);

    private static readonly JsonSerializerOptions DetailsJson = new(JsonSerializerDefaults.Web);

    /// <summary>Jsonb theo mã mục.</summary>
    private static string SerializeDetails(MeetingDetailsDto? details, int presentCount)
    {
        var value = details ?? new MeetingDetailsDto();
        var attendees = CleanPeople(value.Attendees, "người dự hội nghị");
        if (attendees.Count > 50)
            throw new ArgumentException($"Mục 3.2 có {attendees.Count} người, vượt giới hạn 50 người.");
        var committee = CleanPeople(value.CountingCommittee, "thành viên Tổ kiểm phiếu");
        if (committee.Count > 15)
            throw new ArgumentException($"Tổ kiểm phiếu có {committee.Count} người, vượt giới hạn 15 người.");

        var ballots = new[] { value.BallotsIssued, value.BallotsCollected, value.BallotsValid, value.BallotsInvalid };
        if (ballots.Any(b => b < 0))
            throw new ArgumentException("Số phiếu phát ra, thu về, hợp lệ, không hợp lệ không được âm.");
        if (value.BallotsIssued > presentCount)
            throw new ArgumentException($"Số phiếu phát ra ({value.BallotsIssued}) vượt số đại biểu có mặt ({presentCount}). Hãy kiểm tra lại.");
        if (value.BallotsCollected > value.BallotsIssued)
            throw new ArgumentException($"Số phiếu thu về ({value.BallotsCollected}) vượt số phiếu phát ra ({value.BallotsIssued}). Hãy kiểm tra lại.");
        if (value.BallotsCollected.HasValue && value.BallotsValid.HasValue && value.BallotsInvalid.HasValue
            && value.BallotsValid + value.BallotsInvalid != value.BallotsCollected)
            throw new ArgumentException(
                $"Số phiếu hợp lệ ({value.BallotsValid}) cộng số phiếu không hợp lệ ({value.BallotsInvalid}) phải bằng số phiếu thu về ({value.BallotsCollected}).");

        var clean = new MeetingDetailsDto
        {
            WorkingRules = TrimOrNull(Limit(value.WorkingRules, 500, "Quy chế làm việc")),
            ReportingUnit = TrimOrNull(Limit(value.ReportingUnit, 300, "Cơ quan, đơn vị báo cáo")),
            ChairTitle = TrimOrNull(Limit(value.ChairTitle, 300, "Chức vụ của chủ trì")),
            SecretaryTitle = TrimOrNull(Limit(value.SecretaryTitle, 300, "Chức vụ của thư ký")),
            Attendees = attendees,
            CountingCommittee = committee,
            BallotsIssued = value.BallotsIssued,
            BallotsCollected = value.BallotsCollected,
            BallotsValid = value.BallotsValid,
            BallotsInvalid = value.BallotsInvalid
        };
        return JsonSerializer.Serialize(clean, DetailsJson);
    }

    private static List<MeetingAttendeeDto> CleanPeople(IEnumerable<MeetingAttendeeDto>? people, string label) =>
        (people ?? Enumerable.Empty<MeetingAttendeeDto>())
            .Where(a => a != null && !string.IsNullOrWhiteSpace(a.Name))
            .Select(a => new MeetingAttendeeDto
            {
                Name = Limit(a.Name.Trim(), 200, "Họ tên " + label),
                Title = Limit(a.Title?.Trim(), 300, "Chức vụ " + label)
            })
            .ToList();

    /// <summary>Dữ liệu hỏng → rỗng.</summary>
    public static MeetingDetailsDto DeserializeDetails(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new MeetingDetailsDto();
        try
        {
            return JsonSerializer.Deserialize<MeetingDetailsDto>(json, DetailsJson) ?? new MeetingDetailsDto();
        }
        catch (JsonException)
        {
            return new MeetingDetailsDto();
        }
    }

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Hợp phạm vi <c>evaluation.read</c> (không tính luật chủ hồ sơ) và <c>collective.manage</c>.</summary>
    private ScopeFilter CollectiveReadScope()
    {
        var read = _guard.GetScope(PermissionCodes.EvaluationRead) with { OwnerId = null };
        return read.Union(_guard.GetScope(PermissionCodes.CollectiveManage));
    }

    private static CollectiveEvaluationForm? ParseCollectiveForm(string? form)
    {
        if (string.IsNullOrWhiteSpace(form)) return null;
        if (string.Equals(form, "M06", StringComparison.OrdinalIgnoreCase)) return CollectiveEvaluationForm.M06;
        if (string.Equals(form, "M07", StringComparison.OrdinalIgnoreCase)) return CollectiveEvaluationForm.M07;
        if (string.Equals(form, "M08", StringComparison.OrdinalIgnoreCase)) return CollectiveEvaluationForm.M08;
        throw new ArgumentException("Hồ sơ tập thể chỉ hỗ trợ M06, M07 hoặc M08.");
    }

    private static void ValidateScore(double score, double maximum, string name)
    {
        if (score < 0 || score > maximum)
            throw new ArgumentException($"{name} phải từ 0 đến {maximum} điểm.");
    }

    private static EvaluationGrade ParseGrade(string? value)
    {
        return Enum.TryParse<EvaluationGrade>(value, true, out var grade)
            ? grade
            : EvaluationGrade.ChuaXepLoai;
    }

    /// <summary>Cùng luật với AttachmentService: <c>collective.manage</c> bao trùm tổ chức của hồ sơ và kỳ chưa đóng.</summary>
    private bool CanAttach(CollectiveEvaluationRecord record, bool periodClosed) =>
        !periodClosed && _guard.Can(PermissionCodes.CollectiveManage,
            new CollectiveAttachmentOwner(record.Id, record.DepartmentId, record.PartyCellId, periodClosed).WriteTarget);

    private CollectiveEvaluationRecordDto MapCollective(CollectiveEvaluationRecord record, bool periodClosed)
    {
        return new CollectiveEvaluationRecordDto
        {
            Id = record.Id,
            CanAttach = CanAttach(record, periodClosed),
            Version = _unitOfWork.GetVersion(record),
            PeriodId = record.PeriodId,
            Form = record.Form.ToString(),
            PartyCellId = record.PartyCellId,
            PartyCellName = record.PartyCell?.Name,
            DepartmentId = record.DepartmentId,
            DepartmentName = record.Department?.Name,
            HeadId = record.HeadId,
            HeadName = record.Head?.FullName,
            SubjectName = record.SubjectName,
            Strengths = record.Strengths,
            Limitations = record.Limitations,
            Causes = record.Causes,
            PreviousRemediation = record.PreviousRemediation,
            Explanation = record.Explanation,
            Responsibilities = record.Responsibilities,
            RemediationPlan = record.RemediationPlan,
            GeneralCriteriaScore = record.GeneralCriteriaScore,
            TaskCriteriaScore = record.TaskCriteriaScore,
            CriteriaScores = DeserializeCriteriaScores(record.CriteriaScores),
            TotalScore = record.TotalScore,
            SelfProposedGrade = record.SelfProposedGrade.ToString(),
            Status = record.Status.ToString(),
            Sections = DeserializeSections(record.Sections),
            Items = record.Items.Select(item => new CollectiveEvaluationItemDto
            {
                Id = item.Id,
                ItemOrder = item.ItemOrder,
                Category = item.Category,
                TaskName = item.TaskName,
                PlanOrDirection = item.PlanOrDirection,
                Result = item.Result,
                Limitations = item.Limitations,
                Notes = item.Notes
            }).ToList()
        };
    }

    private EvaluationMeetingDto MapMeeting(EvaluationMeeting meeting)
    {
        return new EvaluationMeetingDto
        {
            Id = meeting.Id,
            Version = _unitOfWork.GetVersion(meeting),
            PeriodId = meeting.PeriodId,
            PartyCellId = meeting.PartyCellId,
            PartyCellName = meeting.PartyCell?.Name,
            DepartmentId = meeting.DepartmentId,
            DepartmentName = meeting.Department?.Name,
            Stage = meeting.Stage.HasValue ? WorkflowSteps.Code(meeting.Stage.Value) : null,
            FormCode = meeting.FormCode,
            MeetingType = meeting.MeetingType,
            Location = meeting.Location,
            StartedAt = meeting.StartedAt,
            EndedAt = meeting.EndedAt,
            InvitedCount = meeting.InvitedCount,
            PresentCount = meeting.PresentCount,
            AbsentCount = meeting.AbsentCount,
            AbsentReasons = meeting.AbsentReasons,
            ChairId = meeting.ChairId,
            ChairName = meeting.ChairName,
            SecretaryId = meeting.SecretaryId,
            SecretaryName = meeting.SecretaryName,
            MinutesContent = meeting.MinutesContent,
            OutcomeContent = meeting.OutcomeContent,
            VoteCountingContent = meeting.VoteCountingContent,
            Details = DeserializeDetails(meeting.Details),
            VoteSummaries = meeting.VoteSummaries.Select(vote => new EvaluationMeetingVoteSummaryDto
            {
                Id = vote.Id,
                RecordId = vote.RecordId,
                FullName = vote.Record?.Member?.FullName,
                VotesExcellent = vote.VotesExcellent,
                VotesGood = vote.VotesGood,
                VotesSatisfactory = vote.VotesSatisfactory,
                VotesUnsatisfactory = vote.VotesUnsatisfactory,
                VotesNotRated = vote.VotesNotRated,
                InvalidVotes = vote.InvalidVotes,
                Notes = vote.Notes
            }).ToList()
        };
    }
}
