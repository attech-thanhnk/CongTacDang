using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Phiếu ý kiến chuyên môn độc lập / đối soát dữ liệu chuyên ngành (mỗi phiếu một hồ sơ và một cơ quan đối soát).</summary>
public sealed class Mau03Data
{
    public const string TemplateFileName = "Mau_03_PhieuYKienChuyenMon.docx";

    /// <summary>Tên đơn vị đối soát, chữ in hoa dưới tên cơ quan quản lý cấp trên.</summary>
    [TemplateField("EXPERT_UNIT_UPPER")] public string? ExpertUnitUpper { get; init; }

    /// <summary>Ngày trên phiếu; chưa ghi thì lấy ngày xác nhận, gửi hoặc yêu cầu.</summary>
    [TemplateField("DATE_LINE")] public string? DateLine { get; init; }

    [TemplateField("ADDRESSEE")] public string? Addressee { get; init; }

    [TemplateField("EXPERT_UNIT")] public string? ExpertUnit { get; init; }

    [TemplateField("FULL_NAME")] public string? FullName { get; init; }

    /// <summary>Chức vụ đang giữ của cán bộ được đối soát và đơn vị công tác, lấy từ hồ sơ.</summary>
    [TemplateField("POSITION_DEPT")] public string? PositionDept { get; init; }

    [TemplateCondition("FIELD_SAFETY_QUALITY")] public bool FieldSafetyQuality { get; init; }
    [TemplateCondition("FIELD_CNS")] public bool FieldCns { get; init; }
    [TemplateCondition("FIELD_ATM")] public bool FieldAtm { get; init; }
    [TemplateCondition("FIELD_PLANNING")] public bool FieldPlanning { get; init; }
    [TemplateCondition("FIELD_FINANCE")] public bool FieldFinance { get; init; }
    [TemplateCondition("FIELD_LEGAL")] public bool FieldLegal { get; init; }

    /// <summary>Người xác nhận phiếu (lãnh đạo Ban chuyên môn); chưa xác nhận → trống.</summary>
    [TemplateField("LEADER_NAME")] public string? LeaderName { get; init; }

    /// <summary>Người lập phiếu (cán bộ đối soát dữ liệu); chưa gửi → trống.</summary>
    [TemplateField("REVIEWER_NAME")] public string? ReviewerName { get; init; }

    [TemplateCollection("ITEMS")] public List<Mau03Row> Items { get; init; } = new();

    public static Mau03Data From(ExpertCrosscheckDto crosscheck, string? positionDept)
    {
        ArgumentNullException.ThrowIfNull(crosscheck);
        bool Has(string field) => crosscheck.Fields.Contains(field);
        var issued = crosscheck.IssuedDate is { } date
            ? $"ngày {date.Day:00} tháng {date.Month:00} năm {date.Year}"
            : CollectiveFormText.DateLine(crosscheck.ConfirmedAt ?? crosscheck.SubmittedAt ?? crosscheck.RequestedAt);
        return new Mau03Data
        {
            ExpertUnitUpper = PartyHeader.Upper(crosscheck.ExpertUnitName),
            DateLine = issued,
            Addressee = FormText.OrNull(crosscheck.AddresseeName?.Trim()),
            ExpertUnit = FormText.OrNull(crosscheck.ExpertUnitName?.Trim()),
            FullName = FormText.OrNull(crosscheck.MemberFullName?.Trim()),
            PositionDept = FormText.OrNull(positionDept),
            FieldSafetyQuality = Has(CrosscheckFields.SafetyQuality),
            FieldCns = Has(CrosscheckFields.CnsTechnical),
            FieldAtm = Has(CrosscheckFields.Atm),
            FieldPlanning = Has(CrosscheckFields.PlanningProject),
            FieldFinance = Has(CrosscheckFields.Finance),
            FieldLegal = Has(CrosscheckFields.LegalInspectionAudit),
            LeaderName = FormText.OrNull(crosscheck.ConfirmedByName),
            ReviewerName = FormText.OrNull(crosscheck.SubmittedByName),
            Items = crosscheck.Items.OrderBy(i => i.Order).Select((item, index) => Mau03Row.From(item, index + 1)).ToList()
        };
    }
}

public sealed class Mau03Row
{
    [TemplateField("I_STT")] public string? Order { get; init; }
    [TemplateField("I_NAME")] public string? Name { get; init; }
    [TemplateField("I_SELF")] public string? SelfData { get; init; }
    [TemplateField("I_SOURCE")] public string? SourceData { get; init; }

    [TemplateCondition("I_PASS")] public bool Pass { get; init; }
    [TemplateCondition("I_EXCEED")] public bool Exceed { get; init; }
    [TemplateCondition("I_DEFECT")] public bool Defect { get; init; }

    /// <summary>Tiêu chí không áp dụng: ô mức độ in "K/AD".</summary>
    [TemplateCondition("I_NA")] public bool NotApplicable { get; init; }

    [TemplateField("I_ADJUST")] public string? Adjust { get; init; }
    [TemplateField("I_BASIS")] public string? Basis { get; init; }

    public static Mau03Row From(ExpertCrosscheckItemDto item, int order) => new()
    {
        Order = order.ToString(CultureInfo.InvariantCulture),
        Name = item.ItemName,
        SelfData = item.SelfData,
        SourceData = item.NotApplicable ? string.Empty : item.SourceData,
        Pass = !item.NotApplicable && item.Result == nameof(CrosscheckResult.Pass),
        Exceed = !item.NotApplicable && item.Result == nameof(CrosscheckResult.Exceed),
        Defect = !item.NotApplicable && item.Result == nameof(CrosscheckResult.Defect),
        NotApplicable = item.NotApplicable,
        Adjust = item.AdjustmentText,
        Basis = BasisText(item)
    };

    /// <summary>Nội dung căn cứ, rồi "số … ngày … của …" nếu có thông tin văn bản.</summary>
    private static string BasisText(ExpertCrosscheckItemDto item)
    {
        var document = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.BasisDocNumber))
            document.Add("số " + item.BasisDocNumber.Trim());
        if (item.BasisDocDate is { } date)
            document.Add("ngày " + date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(item.BasisAgency))
            document.Add("của " + item.BasisAgency.Trim());
        var lines = new[] { item.BasisText?.Trim(), document.Count > 0 ? string.Join(' ', document) : null }
            .Where(l => !string.IsNullOrEmpty(l));
        return string.Join("\n", lines);
    }
}
