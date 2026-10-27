using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Phiếu kiểm soát tính hợp lệ, công bằng và nhất quán trước khi trình phê duyệt.</summary>
public sealed class Mau19Data
{
    public const string TemplateFileName = "Mau_19_PhieuKiemSoat.docx";

    /// <summary>Ngày xác nhận; chưa xác nhận thì ngày lập phiếu.</summary>
    [TemplateField("DATE_LINE")] public string? DateLine { get; init; }

    /// <summary>Đơn vị của phiếu; phiếu không gắn đơn vị thì tên công ty trong cài đặt.</summary>
    [TemplateField("UNIT_NAME")] public string? UnitName { get; init; }

    [TemplateField("PERIOD_FROM")] public string? PeriodFrom { get; init; }
    [TemplateField("PERIOD_TO")] public string? PeriodTo { get; init; }

    [TemplateField("GROUP_NAME")] public string? GroupName { get; init; }

    /// <summary>Người lập phiếu; in ở đầu phiếu và dưới ô ký.</summary>
    [TemplateField("CONTROLLER_NAME")] public string? ControllerName { get; init; }

    [TemplateField("CONTROLLER_DATE")] public string? ControllerDate { get; init; }

    [TemplateField("CONFIRMER_NAME")] public string? ConfirmerName { get; init; }

    [TemplateField("CONFIRMER_DATE")] public string? ConfirmerDate { get; init; }

    [TemplateCondition("CONCLUSION_ELIGIBLE")] public bool ConclusionEligible { get; init; }
    [TemplateCondition("CONCLUSION_NEEDS_WORK")] public bool ConclusionNeedsWork { get; init; }

    /// <summary>Nội dung cần hoàn thiện (nếu có).</summary>
    [TemplateField("TO_COMPLETE")] public string? ToComplete { get; init; }

    /// <summary>Chưa xác nhận thì phiếu in dòng cảnh báo bản nháp ở đầu văn bản.</summary>
    [TemplateCondition("DRAFT")] public bool Draft { get; init; }

    [TemplateCollection("ITEMS")] public List<Mau19Row> Items { get; init; } = new();

    public static Mau19Data From(ControlCheckDto check, string? unitName, DateTime? periodStart, DateTime? periodEnd)
    {
        ArgumentNullException.ThrowIfNull(check);
        return new Mau19Data
        {
            DateLine = CollectiveFormText.DateLine(check.ConfirmedAt ?? check.PerformedAt ?? check.CreatedAt),
            UnitName = FormText.OrNull(unitName),
            PeriodFrom = periodStart.HasValue ? FormText.Date(periodStart.Value) : null,
            PeriodTo = periodEnd.HasValue ? FormText.Date(periodEnd.Value) : null,
            GroupName = FormText.OrNull(check.GroupName?.Trim()),
            ControllerName = FormText.OrNull(check.PerformedByName),
            ControllerDate = check.PerformedAt.HasValue ? CollectiveFormText.Date(check.PerformedAt.Value) : null,
            ConfirmerName = FormText.OrNull(check.ConfirmedByName),
            ConfirmerDate = check.ConfirmedAt.HasValue ? CollectiveFormText.Date(check.ConfirmedAt.Value) : null,
            ConclusionEligible = check.Conclusion == nameof(ControlConclusion.Eligible),
            ConclusionNeedsWork = check.Conclusion == nameof(ControlConclusion.NeedsWork),
            ToComplete = FormText.OrNull(check.NeedsWorkContent?.Trim()),
            Draft = check.Status != nameof(ControlCheckStatus.Confirmed),
            Items = check.Items.OrderBy(i => i.ItemCode, StringComparer.Ordinal)
                .Select((item, index) => Mau19Row.From(item, index + 1)).ToList()
        };
    }
}

public sealed class Mau19Row
{
    [TemplateField("I_STT")] public string? Order { get; init; }

    /// <summary>Nguyên văn mục đã chụp vào phiếu.</summary>
    [TemplateField("I_TEXT")] public string? Text { get; init; }

    [TemplateCondition("I_PASS")] public bool Pass { get; init; }
    [TemplateCondition("I_FAIL")] public bool Fail { get; init; }

    /// <summary>Không áp dụng: ô "Chưa đạt/K/AD" in "K/AD".</summary>
    [TemplateCondition("I_NA")] public bool NotApplicable { get; init; }

    [TemplateField("I_EVIDENCE")] public string? Evidence { get; init; }

    public static Mau19Row From(ControlCheckItemDto item, int order) => new()
    {
        Order = order.ToString(CultureInfo.InvariantCulture),
        Text = item.ItemText,
        Pass = item.Result == nameof(ControlItemResult.Pass),
        Fail = item.Result == nameof(ControlItemResult.Fail),
        NotApplicable = item.Result == nameof(ControlItemResult.NotApplicable),
        Evidence = item.Evidence?.Trim() ?? string.Empty
    };
}
