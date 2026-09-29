using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Documents;
using CongTacDang.Infrastructure.Documents.Forms;

namespace CongTacDang.Infrastructure.Services;

/// <summary>
/// Xuất Mẫu 17 (task 20 — T-88): file mẫu đang kích hoạt (hoặc file gốc <c>Mau_17_KeHoachHoTro.docx</c>) + dữ liệu kế hoạch
/// + tag thông tin đơn vị <c>ORG_*</c>; PDF chuyển phía máy chủ bằng LibreOffice.
/// </summary>
public sealed class ImprovementPlanDocumentService : IImprovementPlanDocument
{
    private const string DocxMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string PdfMimeType = "application/pdf";

    private readonly IWordTemplateStore _templates;
    private readonly IOrganizationSettingsService _orgSettings;
    private readonly IPdfConverter _pdfConverter;

    public ImprovementPlanDocumentService(IWordTemplateStore templates, IOrganizationSettingsService orgSettings, IPdfConverter pdfConverter)
    {
        _templates = templates;
        _orgSettings = orgSettings;
        _pdfConverter = pdfConverter;
    }

    /// <inheritdoc />
    public async Task<ReportFileResult> RenderMau17Async(ImprovementPlan plan, EvaluationRecord record, ReportFormat format, CancellationToken ct = default)
    {
        var template = await _templates.LoadAsync(Mau17Data.TemplateFileName, ct);
        var org = OrganizationSettingsService.ToTemplateFields(await _orgSettings.GetAsync(ct));
        var data = TemplateDataBinder.Bind(Mau17Data.From(plan, record)).WithShared(TemplateDataBinder.Bind(org));
        var bytes = DocxTemplateEngine.Render(template, data).Content;

        var name = string.IsNullOrWhiteSpace(record.Member?.FullName) ? "CanBo" : record.Member!.FullName.Trim().Replace(" ", "_");
        var fileName = $"Mau_17_KeHoach_30_60_90_{name}.docx";
        if (format != ReportFormat.Pdf)
            return new ReportFileResult { FileBytes = bytes, ContentType = DocxMimeType, FileName = fileName };

        var pdf = await _pdfConverter.ConvertToPdfAsync(bytes, ".docx", ct);
        return new ReportFileResult { FileBytes = pdf, ContentType = PdfMimeType, FileName = Path.ChangeExtension(fileName, ".pdf") };
    }
}
