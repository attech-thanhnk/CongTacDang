using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Organization;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Evaluation;
using CongTacDang.Infrastructure.Data;
using CongTacDang.Infrastructure.Documents;
using CongTacDang.Infrastructure.Documents.Forms;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Services;

/// <summary>Xuất biểu mẫu cá nhân: 04/05/09A/09C/9D từ template Word, 01/02/10 dùng bộ xuất có sẵn.</summary>
public sealed class RecordFormService : IRecordFormService
{
    private const string DocxMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string PdfMimeType = "application/pdf";

    private readonly CongTacDangDbContext _db;
    private readonly IAuthorizationGuard _guard;
    private readonly IWordTemplateStore _templates;
    private readonly IPdfConverter _pdfConverter;
    private readonly IOrganizationSettingsService _orgSettings;
    private readonly IReportService _reports;

    public RecordFormService(
        CongTacDangDbContext db,
        IAuthorizationGuard guard,
        IWordTemplateStore templates,
        IPdfConverter pdfConverter,
        IOrganizationSettingsService orgSettings,
        IReportService reports)
    {
        _db = db;
        _guard = guard;
        _templates = templates;
        _pdfConverter = pdfConverter;
        _orgSettings = orgSettings;
        _reports = reports;
    }

    /// <inheritdoc />
    public async Task<List<RecordFormDto>> GetFormsAsync(Guid recordId, CancellationToken ct = default)
    {
        var record = await LoadAsync(recordId, ct);
        return Available(ReadCriteria(record.Period))
            .Select(code => new RecordFormDto { Code = code, Name = RecordFormCodes.DisplayName(code) })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ReportFileResult> ExportAsync(Guid recordId, string formCode, ReportFormat format, CancellationToken ct = default)
    {
        var code = RecordFormCodes.Normalize(formCode);
        if (!RecordFormCodes.IsKnown(code))
            throw new ValidationException($"Không có biểu mẫu cá nhân \"{formCode}\". Các mẫu: {string.Join(", ", RecordFormCodes.All)}.");

        var record = await LoadAsync(recordId, ct);
        var criteria = ReadCriteria(record.Period);
        if (!Available(criteria).Contains(code))
        {
            if (!RecordFormCodes.Exportable.Contains(code))
                throw new ConflictException($"{RecordFormCodes.DisplayName(code)} chưa có chức năng xuất trên hồ sơ.");
            throw new ConflictException(
                $"Kỳ \"{record.Period.Name}\" không áp dụng {RecordFormCodes.DisplayName(code)} (theo bộ tiêu chí của kỳ). "
                + "Hãy chọn biểu mẫu khác hoặc liên hệ người quản lý kỳ.");
        }

        switch (code)
        {
            case RecordFormCodes.Form01:
                return await _reports.ExportMau01DocxAsync(recordId, format);
            case RecordFormCodes.Form02:
                return await _reports.ExportMau02DocxAsync(recordId, format);
            case RecordFormCodes.Form10:
                return await _reports.ExportMau10DocxAsync(recordId, format);
        }

        if (criteria == null)
            throw new ConflictException("Kỳ đánh giá chưa có bộ tiêu chí nên chưa xuất được biểu mẫu tự chấm. Hãy liên hệ người quản lý kỳ.");

        var source = new IndividualFormSource(record, criteria.Content, await PositionsAsync(record, ct), await LayoutAsync(record, ct));
        var (templateFileName, data) = code switch
        {
            RecordFormCodes.Form09A => (Mau09AData.TemplateFileName, (object)Mau09AData.From(source)),
            RecordFormCodes.Form09C => (Mau09CData.TemplateFileName, Mau09CData.From(source)),
            _ => (Mau9DData.TemplateFileName, (object)Mau9DData.From(source))
        };

        return await RenderAsync(templateFileName, data, FileName(code, record), format, ct);
    }

    /// <inheritdoc />
    public async Task<List<RecordFormDto>> GetRequestFormsAsync(Guid recordId, CancellationToken ct = default)
    {
        await LoadAsync(recordId, ct);
        var requests = await _db.Set<TaskChangeRequest>().AsNoTracking()
            .Where(r => r.RecordId == recordId)
            .OrderBy(r => r.RequestedAt)
            .ToListAsync(ct);
        return requests.Select(r =>
        {
            var code = FormCodeOf(r.Type);
            var task = string.IsNullOrWhiteSpace(r.TaskNameSnapshot) ? string.Empty : $" ({r.TaskNameSnapshot.Trim()})";
            return new RecordFormDto { Code = code, Name = RecordFormCodes.DisplayName(code) + task, RequestId = r.Id };
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<ReportFileResult> ExportRequestFormAsync(Guid recordId, string formCode, Guid requestId, ReportFormat format, CancellationToken ct = default)
    {
        var code = RecordFormCodes.Normalize(formCode);
        if (!RecordFormCodes.RequestForms.Contains(code))
            throw new ValidationException($"Không có biểu mẫu đề nghị thay đổi việc \"{formCode}\". Các mẫu: {string.Join(", ", RecordFormCodes.RequestForms)}.");

        var record = await LoadAsync(recordId, ct);
        var request = await _db.Set<TaskChangeRequest>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId && r.RecordId == recordId, ct)
            ?? throw new NotFoundException($"Không tìm thấy đề nghị thay đổi việc với Id: {requestId}.");
        if (FormCodeOf(request.Type) != code)
        {
            throw new ConflictException(
                $"Đề nghị \"{TaskChangeRules.TypeName(request.Type)}\" dùng {RecordFormCodes.DisplayName(FormCodeOf(request.Type))}, không dùng {RecordFormCodes.DisplayName(code)}.");
        }

        var source = new TaskChangeFormSource(record, request, (await PositionsAsync(record, ct)).All());
        var (templateFileName, data) = code == RecordFormCodes.Form05
            ? (Mau05Data.TemplateFileName, (object)Mau05Data.From(source))
            : (Mau04Data.TemplateFileName, Mau04Data.From(source));
        return await RenderAsync(templateFileName, data, FileName(code, record), format, ct);
    }

    private static string FormCodeOf(TaskChangeType type) => type == TaskChangeType.Urgent ? RecordFormCodes.Form05 : RecordFormCodes.Form04;

    private async Task<ReportFileResult> RenderAsync(string templateFileName, object data, string fileName, ReportFormat format, CancellationToken ct)
    {
        var template = await _templates.LoadAsync(templateFileName, ct);
        var org = OrganizationSettingsService.ToTemplateFields(await _orgSettings.GetAsync(ct));
        var bound = TemplateDataBinder.Bind(data);
        var bytes = DocxTemplateEngine.Render(template, bound.WithShared(TemplateDataBinder.Bind(org))).Content;

        if (format != ReportFormat.Pdf)
            return new ReportFileResult { FileBytes = bytes, ContentType = DocxMimeType, FileName = fileName };

        var pdf = await _pdfConverter.ConvertToPdfAsync(bytes, ".docx");
        return new ReportFileResult { FileBytes = pdf, ContentType = PdfMimeType, FileName = Path.ChangeExtension(fileName, ".pdf") };
    }

    /// <summary>Nạp hồ sơ (kèm kỳ, cán bộ, Phòng, Chi bộ, nhiệm vụ) và kiểm tra quyền xem hồ sơ.</summary>
    private async Task<EvaluationRecord> LoadAsync(Guid recordId, CancellationToken ct)
    {
        var record = await _db.EvaluationRecords
            .AsNoTracking()
            .Include(r => r.Period)
            .Include(r => r.Member)
            .Include(r => r.Department)
            .Include(r => r.PartyCell)
            .Include(r => r.Tasks)
            .FirstOrDefaultAsync(r => r.Id == recordId, ct)
            ?? throw new NotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {recordId}.");

        _guard.Ensure(PermissionCodes.EvaluationRead, AccessTarget.ForRecord(record));
        return record;
    }

    /// <summary>Mẫu theo bộ tiêu chí của kỳ.</summary>
    private static IReadOnlyList<string> Available(CriteriaSnapshot? criteria) =>
        criteria?.ApplicableForms().Where(RecordFormCodes.Exportable.Contains).ToList() ?? new List<string>();

    private static CriteriaSnapshot? ReadCriteria(EvaluationPeriod period)
    {
        try
        {
            return period.GetCriteria();
        }
        catch (FormatException)
        {
            throw new ConflictException("Bộ tiêu chí đã lưu trong kỳ bị lỗi định dạng. Hãy liên hệ người quản lý kỳ.");
        }
    }

    /// <summary>Chức vụ đang giữ tại cuối kỳ (hoặc hôm nay nếu kỳ chưa kết thúc).</summary>
    private async Task<MemberPositionNames> PositionsAsync(EvaluationRecord record, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var at = record.Period.EndDate < now ? record.Period.EndDate : now;
        var rows = await _db.MemberPositions.AsNoTracking()
            .Include(mp => mp.Position)
            .Where(mp => mp.UserId == record.MemberId && mp.Position != null
                         && mp.ValidFrom <= at && (mp.ValidTo == null || mp.ValidTo > at))
            .ToListAsync(ct);
        return MemberPositionNames.From(rows
            .OrderByDescending(mp => mp.IsPrimary)
            .ThenBy(mp => mp.Position!.SortOrder)
            .Select(mp => (mp.Position!.Side, PositionRules.DisplayName(mp))));
    }

    /// <summary>Kiểu trình bày biểu mẫu: kiểu đặt riêng của cán bộ, rồi kiểu của chức vụ đang giữ, rồi mặc định.</summary>
    private async Task<FormLayout?> LayoutAsync(EvaluationRecord record, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var at = record.Period.EndDate < now ? record.Period.EndDate : now;
        var overrideId = await _db.PartyMemberProfiles.AsNoTracking()
            .Where(m => m.Id == record.MemberId)
            .Select(m => m.FormLayoutOverrideId)
            .FirstOrDefaultAsync(ct);
        var layouts = await _db.FormLayouts.AsNoTracking().ToListAsync(ct);
        var positionLayoutIds = await _db.MemberPositions.AsNoTracking()
            .Where(mp => mp.UserId == record.MemberId && mp.Position != null && mp.Position.FormLayoutId != null
                         && mp.ValidFrom <= at && (mp.ValidTo == null || mp.ValidTo > at))
            .Select(mp => mp.Position!.FormLayoutId!.Value)
            .ToListAsync(ct);
        return FormLayout.Resolve(
            layouts.FirstOrDefault(l => l.Id == overrideId),
            positionLayoutIds.Select(id => layouts.FirstOrDefault(l => l.Id == id)),
            layouts.FirstOrDefault(l => l.IsDefault));
    }

    /// <summary>Tên tệp không dấu, ví dụ <c>Mau_09A_Nguyen_Van_A_Q1-2027.docx</c>.</summary>
    private static string FileName(string code, EvaluationRecord record)
    {
        var name = Ascii(record.Member?.FullName);
        var period = record.Period != null ? $"_Q{(int)record.Period.Quarter}-{record.Period.Year}" : string.Empty;
        return $"Mau_{code}_{(string.IsNullOrEmpty(name) ? "CanBo" : name)}{period}.docx";
    }

    private static string Ascii(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var normalized = value.Trim().Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            builder.Append(char.IsLetterOrDigit(ch) && ch < 128 ? ch : '_');
        }
        return string.Join("_", builder.ToString().Split('_', StringSplitOptions.RemoveEmptyEntries));
    }
}
