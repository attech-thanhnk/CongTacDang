using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CongTacDang.Application.Organization;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;
using ScopeFilter = CongTacDang.Application.Common.Security.ScopeFilter;

namespace CongTacDang.Infrastructure.Services;

/// <summary>Bảng tổng hợp xếp loại cán bộ của kỳ: một trang tính, hai khối theo cấp quyết định (cấp trên / cơ sở).</summary>
public partial class ReportService
{
    private const int RatingSummaryColumns = 13;

    /// <summary>Tiêu đề cột (1)…(11) và (13); cột (12) theo cấp quyết định của khối.</summary>
    private static readonly string[] RatingSummaryHeaders =
    {
        "STT", "Mã hiệu", "Họ và tên", "Chức vụ (Đảng, Chính quyền)", "Điểm tiêu chí chung",
        "Điểm tiêu chí kết quả thực hiện chức trách, nhiệm vụ được giao", "Tổng điểm tự chấm", "Mức tự đề xuất xếp loại",
        "Tập thể lãnh đạo phòng/trung tâm, ban, đơn vị đề xuất",
        "Cơ quan tham mưu về công tác TCCB hoặc tổ chức, cá nhân được giao nhiệm vụ thực hiện tổng hợp, thẩm định và đề xuất",
        "Cấp có thẩm quyền trực tiếp sử dụng cán bộ đề xuất", string.Empty, "Hồ sơ kèm theo"
    };

    /// <inheritdoc />
    public async Task<ReportFileResult> ExportRatingSummaryAsync(Guid? periodId, Guid? partyCellId, ScopeFilter viewerScope,
        ReportFormat format = ReportFormat.Original)
    {
        ArgumentNullException.ThrowIfNull(viewerScope);
        var (period, records, scopeCell) = await LoadExcelScopeAsync(periodId, partyCellId);
        // Chỉ hồ sơ thuộc phạm vi report.export của người xem (theo ảnh chụp trên hồ sơ).
        records = records.Where(r => viewerScope.Matches(null, r.DepartmentId, r.PartyCellId)).ToList();

        var held = await HeldPositionsAsync(records.Select(r => r.MemberId), PositionDate(period));
        var attachmentCounts = await RecordAttachmentCountsAsync(records);
        var org = await _orgSettings.GetAsync();
        var nameComparer = StringComparer.Create(Vietnamese, true);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Tổng hợp xếp loại");
        ws.Cell(1, 1).Value = org.PartyCommitteeName;
        ws.Range(1, 1, 1, 4).Merge().Style.Font.SetBold(true);
        TitleRow(ws, 3, RatingSummaryColumns, "BẢNG TỔNG HỢP XẾP LOẠI CÁN BỘ", bold: true);
        TitleRow(ws, 4, RatingSummaryColumns, ExcelTitleSuffix(period, scopeCell), bold: false);

        var row = 6;
        foreach (var (authority, blockTitle, deciderHeader) in new[]
        {
            (ApprovalAuthority.CapTren, "THUỘC DIỆN BAN THƯỜNG VỤ ĐẢNG ỦY TCT ĐÁNH GIÁ, XẾP LOẠI", "Ban Thường vụ Đảng ủy TCT xếp loại"),
            (ApprovalAuthority.CoSo, "THUỘC DIỆN BAN CHẤP HÀNH ĐẢNG BỘ CÔNG TY ĐÁNH GIÁ, XẾP LOẠI", "Ban Chấp hành Đảng bộ Công ty xếp loại")
        })
        {
            // Tiêu đề khối, dòng tên cột, dòng số cột (1)…(13).
            ws.Cell(row, 1).Value = blockTitle;
            ws.Range(row, 1, row, RatingSummaryColumns).Merge().Style
                .Font.SetBold(true)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#E2E8F0"))
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            var headerRow = row + 1;
            for (var c = 1; c <= RatingSummaryColumns; c++)
            {
                ws.Cell(headerRow, c).Value = c == 12 ? deciderHeader : RatingSummaryHeaders[c - 1];
                ws.Cell(headerRow + 1, c).Value = $"({c})";
            }
            StyleHeader(ws.Range(headerRow, 1, headerRow + 1, RatingSummaryColumns));
            row = headerRow + 2;

            var block = records
                .Where(r => r.ApprovalAuthority == authority)
                .Select(r =>
                {
                    var positions = held.GetValueOrDefault(r.MemberId) ?? new List<MemberPosition>();
                    var code = PositionRules.PersonStatCode(positions.Select(PositionRules.Held));
                    return (Record: r, Positions: positions, Code: code);
                })
                .OrderBy(x => StatCodeOrder(x.Code))
                .ThenBy(x => x.Record.Member?.FullName ?? string.Empty, nameComparer)
                .ToList();

            if (block.Count == 0)
            {
                ws.Cell(row, 1).Value = "Không có cán bộ thuộc diện này trong phạm vi được xem.";
                ws.Range(row, 1, row, RatingSummaryColumns).Merge().Style
                    .Font.SetItalic(true)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                row += 2;
                continue;
            }

            var stt = 1;
            foreach (var (r, positions, code) in block)
            {
                var selfScored = r.SelfScoredAt.HasValue;
                var count = attachmentCounts.GetValueOrDefault(r.Id);
                var values = new object?[]
                {
                    stt++,
                    code,
                    r.Member?.FullName,
                    RatingSummaryPositions(r, positions),
                    selfScored ? r.GeneralCriteriaScore : null,
                    selfScored ? r.TasksScore : null,
                    selfScored ? r.TotalSelfScore : null,
                    GradeOrBlank(r.SelfProposedGrade),
                    GradeOrBlank(r.CollectiveProposedGrade),
                    GradeOrBlank(r.AppraisalProposedGrade),
                    GradeOrBlank(r.DirectorProposedGrade),
                    r.DecisionRecordedAt != null ? FormText.Grade(r.FinalGrade) : GradeOrBlank(r.FinalGrade),
                    count > 0 ? $"{count} tệp đính kèm" : null
                };
                for (var c = 1; c <= RatingSummaryColumns; c++)
                {
                    var cell = ws.Cell(row, c);
                    switch (values[c - 1])
                    {
                        case int n: cell.Value = n; break;
                        case double d: cell.Value = d; break;
                        case string s: cell.Value = s; break;
                    }
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cell.Style.Alignment.WrapText = true;
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                    if (c is 1 or 2 or (>= 5 and <= 7))
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                row++;
            }
            row++;
        }

        ws.Column(1).Width = 5;
        ws.Column(2).Width = 8;
        ws.Column(3).Width = 24;
        ws.Column(4).Width = 40;
        for (var c = 5; c <= 7; c++)
            ws.Column(c).Width = 10;
        for (var c = 8; c <= 12; c++)
            ws.Column(c).Width = 18;
        ws.Column(13).Width = 16;
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.FitToPages(1, 0);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return await ToResultAsync(stream.ToArray(), XlsxMimeType, ExcelFileName("BangTongHop_XepLoaiCanBo", period, scopeCell), format);
    }

    /// <summary>Cột (4): chức vụ Đảng, chính quyền, đoàn thể kèm đơn vị công tác; chưa khai báo → chức danh trên hồ sơ cán bộ.</summary>
    private static string RatingSummaryPositions(EvaluationRecord record, IReadOnlyCollection<MemberPosition> positions)
    {
        var names = positions.Count > 0
            ? string.Join(", ", positions.OrderBy(p => p.Position!.Side).ThenBy(p => p.Position!.SortOrder).Select(PositionRules.DisplayName))
            : record.Member?.PositionTitle;
        var unit = record.Department?.Name ?? record.Member?.Department?.Name;
        return string.Join(" — ", new[] { names, unit }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    /// <summary>Cột (13): số tệp đính kèm hiện hành của hồ sơ, nhiệm vụ và văn bản cấp trên; mỗi nhóm phiên bản tính một lần.</summary>
    private async Task<Dictionary<Guid, int>> RecordAttachmentCountsAsync(IReadOnlyCollection<EvaluationRecord> records)
    {
        var result = new Dictionary<Guid, int>();
        if (records.Count == 0)
            return result;

        var recordIds = records.Select(r => r.Id).ToList();
        var taskToRecord = records.SelectMany(r => r.Tasks.Select(t => (TaskId: t.Id, RecordId: r.Id)))
            .ToDictionary(x => x.TaskId, x => x.RecordId);
        var taskIds = taskToRecord.Keys.ToList();

        var owned = await _db.TaskAttachments.AsNoTracking()
            .Where(a => a.IsActive && !a.IsSuperseded && (
                (a.OwnerType == AttachmentOwnerTypes.EvaluationRecord && a.OwnerId.HasValue && recordIds.Contains(a.OwnerId.Value))
                || (a.OwnerType == AttachmentOwnerTypes.EvaluationTask && a.OwnerId.HasValue && taskIds.Contains(a.OwnerId.Value))
                || (a.OwnerType == null && a.RecordId.HasValue && recordIds.Contains(a.RecordId.Value))
                || (a.OwnerType == null && a.RecordId == null && a.RelatedId.HasValue && taskIds.Contains(a.RelatedId.Value))))
            .Select(a => new { a.Id, a.FileGroupId, a.OwnerType, a.OwnerId, a.RecordId, a.RelatedId })
            .ToListAsync();

        var groups = new Dictionary<Guid, HashSet<Guid>>();
        void Add(Guid recordId, Guid groupId)
        {
            if (!groups.TryGetValue(recordId, out var set))
                groups[recordId] = set = new HashSet<Guid>();
            set.Add(groupId);
        }

        foreach (var a in owned)
        {
            Guid? recordId = a.OwnerType switch
            {
                AttachmentOwnerTypes.EvaluationRecord => a.OwnerId,
                AttachmentOwnerTypes.EvaluationTask => a.OwnerId is { } taskId && taskToRecord.TryGetValue(taskId, out var r1) ? r1 : null,
                _ => a.RecordId ?? (a.RelatedId is { } legacyTask && taskToRecord.TryGetValue(legacyTask, out var r2) ? r2 : null)
            };
            if (recordId.HasValue)
                Add(recordId.Value, a.FileGroupId ?? a.Id);
        }

        // Tệp liên kết theo mã (minh chứng nhiệm vụ, văn bản của cấp trên) — có thể trỏ tới một phiên bản cũ của nhóm tệp.
        var linked = records.SelectMany(r => r.Tasks.Where(t => t.AttachmentId.HasValue).Select(t => (RecordId: r.Id, AttachmentId: t.AttachmentId!.Value)))
            .ToList();
        var external = await _db.Set<EvaluationExternalResult>().AsNoTracking()
            .Where(x => recordIds.Contains(x.RecordId) && x.AttachmentId.HasValue)
            .Select(x => new { x.RecordId, AttachmentId = x.AttachmentId!.Value })
            .ToListAsync();
        linked.AddRange(external.Select(x => (x.RecordId, x.AttachmentId)));
        if (linked.Count > 0)
        {
            var linkedIds = linked.Select(x => x.AttachmentId).Distinct().ToList();
            var linkedGroups = await _db.TaskAttachments.AsNoTracking()
                .Where(a => linkedIds.Contains(a.Id) && a.IsActive)
                .Select(a => new { a.Id, Group = a.FileGroupId ?? a.Id })
                .ToDictionaryAsync(a => a.Id, a => a.Group);
            foreach (var (recordId, attachmentId) in linked)
            {
                if (linkedGroups.TryGetValue(attachmentId, out var group))
                    Add(recordId, group);
            }
        }

        foreach (var (recordId, set) in groups)
            result[recordId] = set.Count;
        return result;
    }
}
