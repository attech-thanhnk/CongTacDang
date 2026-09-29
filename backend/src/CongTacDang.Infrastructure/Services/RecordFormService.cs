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
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Evaluation;
using CongTacDang.Infrastructure.Data;
using CongTacDang.Infrastructure.Documents;
using CongTacDang.Infrastructure.Documents.Forms;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Services;

/// <summary>
/// Xuất biểu mẫu cá nhân của hồ sơ đánh giá (task 18 — T-83). Mẫu 09A/09B/09C/9D dựng từ template Word (phiên bản đang kích hoạt
/// hoặc file gốc) + thông tin đơn vị (<c>ORG_*</c>); Mẫu 01/02/10 dùng lại bộ xuất hiện có. Mẫu áp dụng theo bộ tiêu chí của kỳ.
/// </summary>
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

    /// <summary>Khởi tạo.</summary>
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
        return Available(record, ReadCriteria(record.Period))
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
        if (!Available(record, criteria).Contains(code))
        {
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

        var source = new IndividualFormSource(record, criteria.Content, await PositionsAsync(record, ct));
        var (templateFileName, data) = code switch
        {
            RecordFormCodes.Form09A => (Mau09AData.TemplateFileName, (object)Mau09AData.From(source)),
            RecordFormCodes.Form09B => (Mau09BData.TemplateFileName, Mau09BData.From(source)),
            RecordFormCodes.Form09C => (Mau09CData.TemplateFileName, Mau09CData.From(source)),
            _ => (Mau9DData.TemplateFileName, (object)Mau9DData.From(source))
        };

        var template = await _templates.LoadAsync(templateFileName, ct);
        var org = OrganizationSettingsService.ToTemplateFields(await _orgSettings.GetAsync(ct));
        var bytes = DocxTemplateEngine.Render(template, TemplateDataBinder.Bind(data).WithShared(TemplateDataBinder.Bind(org))).Content;
        var fileName = FileName(code, record);

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

    /// <summary>
    /// Mẫu áp dụng: theo bộ tiêu chí của kỳ; thêm mẫu tự chấm hồ sơ đã dùng (09A/09B) nếu khác — "xuất được nếu dữ liệu có".
    /// </summary>
    private static IReadOnlyList<string> Available(EvaluationRecord record, CriteriaSnapshot? criteria)
    {
        var codes = (criteria?.ApplicableForms() ?? Array.Empty<string>()).ToHashSet();
        if (criteria != null && record.SelfScoredAt.HasValue && CriteriaSetContent.IsValidForm(record.SelfScoreForm))
            codes.Add(record.SelfScoreForm!);
        return RecordFormCodes.All.Where(codes.Contains).ToList();
    }

    private static CriteriaSnapshot? ReadCriteria(EvaluationPeriod period)
    {
        try
        {
            return period.GetCriteria();
        }
        catch (FormatException)
        {
            throw new ConflictException("Ảnh chụp bộ tiêu chí của kỳ bị lỗi định dạng. Hãy liên hệ người quản lý kỳ.");
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
            .Select(mp => (mp.Position!.Side, mp.Position.Name)));
    }

    /// <summary>Tên tệp không dấu theo quy cách HD03 ("Mau …_…_Quy …"), ví dụ <c>Mau_09B_Nguyen_Van_A_Q3-2026.docx</c>.</summary>
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
