using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Quyết định xếp loại kèm Phụ lục (trang 2 khổ ngang) — diện tổ chức Đảng cơ sở quyết định.</summary>
public sealed class MauQdData
{
    public const string TemplateFileName = "Mau_QD_QuyetDinhXepLoai.docx";

    [TemplateField("PARTY_PARENT")] public string? PartyParent { get; init; }
    [TemplateField("PARTY_ORG")] public string? PartyOrganization { get; init; }

    /// <summary>Số văn bản quyết định chung của các hồ sơ (nếu đã ghi).</summary>
    [TemplateField("DOC_NUMBER")] public string? DocumentNumber { get; init; }

    /// <summary>Ngày văn bản quyết định chung của các hồ sơ.</summary>
    [TemplateField("DATE_LINE")] public string? DateLine { get; init; }

    /// <summary>Quý dạng La Mã ("III").</summary>
    [TemplateField("QUARTER")] public string? Quarter { get; init; }
    [TemplateField("YEAR")] public string? Year { get; init; }

    /// <summary>Cấp ủy ra quyết định, dùng giữa câu ("Đảng ủy Công ty …", "Chi bộ …").</summary>
    [TemplateField("COMMITTEE")] public string? Committee { get; init; }

    /// <summary>Cấp ủy ra quyết định, chữ in hoa ("ĐẢNG ỦY CÔNG TY … QUYẾT ĐỊNH").</summary>
    [TemplateField("COMMITTEE_UPPER")] public string? CommitteeUpper { get; init; }

    /// <summary>"Ban Chấp hành Đảng bộ …".</summary>
    [TemplateField("EXECUTIVE")] public string? Executive { get; init; }

    /// <summary>Nhiệm kỳ ("2025-2030").</summary>
    [TemplateField("TERM")] public string? Term { get; init; }

    /// <summary>Ngày hội nghị cấp ủy quyết định ("15/09/2026").</summary>
    [TemplateField("MEETING_DATE")] public string? MeetingDate { get; init; }

    /// <summary>Cấp ủy cấp trên ở "Nơi nhận: - BTV … (để b/c)".</summary>
    [TemplateField("SUPERIOR_COMMITTEE")] public string? SuperiorCommittee { get; init; }

    /// <summary>"T/M ĐẢNG ỦY" / "T/M CHI BỘ".</summary>
    [TemplateField("SIGN_ON_BEHALF")] public string? SignOnBehalf { get; init; }

    /// <summary>Họ tên người ký (chủ trì hội nghị quyết định); không có → để trống.</summary>
    [TemplateField("SIGNER_NAME")] public string? SignerName { get; init; }

    /// <summary>"(kèm theo Quyết định số … ngày …)" dưới tiêu đề Phụ lục.</summary>
    [TemplateField("DECISION_REF")] public string? DecisionReference { get; init; }

    [TemplateCollection("ROWS")] public List<MauQdRow> Rows { get; init; } = new();

    /// <summary>Thiếu dữ liệu → giữ chỗ chấm của template.</summary>
    public static MauQdData From(EvaluationPeriod period, PartyHeader header, string? superiorInSentence,
        DecisionDocumentInfo info, IEnumerable<MauQdLine> lines)
    {
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(info);
        var unit = FormText.OrNull(header.UnitName?.Trim());
        var committeeUpper = CollectiveFormText.CommitteeUpper(unit);
        var number = DocumentNumberText(info.DocumentNumber);
        var date = info.DocumentDate?.Date;

        return new MauQdData
        {
            PartyParent = PartyHeader.Lines(header.Parent),
            PartyOrganization = PartyHeader.Lines(header.Organization),
            DocumentNumber = number != null ? "Số " + number : null,
            DateLine = date.HasValue
                ? date.Value.ToString("'ngày 'dd' tháng 'MM' năm 'yyyy", CultureInfo.InvariantCulture)
                : null,
            Quarter = CollectiveFormText.RomanQuarter(period.Quarter),
            Year = FormText.Year(period.Year),
            Committee = CollectiveFormText.CommitteeInSentence(unit),
            CommitteeUpper = committeeUpper,
            Executive = CollectiveFormText.ExecutiveInSentence(unit),
            Term = CollectiveFormText.PartyTerm(period.Year),
            MeetingDate = info.MeetingAt.HasValue ? CollectiveFormText.Date(info.MeetingAt.Value) : null,
            SuperiorCommittee = CollectiveFormText.CommitteeInSentence(superiorInSentence),
            SignOnBehalf = CollectiveFormText.SignOnBehalf(committeeUpper),
            SignerName = FormText.OrNull(info.ChairName?.Trim()),
            DecisionReference = number == null && date == null
                ? null
                : "(kèm theo Quyết định số " + (number ?? "…..-QĐ/……") + " ngày "
                  + (date.HasValue ? FormText.Date(date.Value) : "…… tháng …… năm ……") + ")",
            Rows = lines.Select((l, i) => new MauQdRow
            {
                Order = (i + 1).ToString(CultureInfo.InvariantCulture),
                Name = FormText.OrNull(l.FullName),
                Position = FormText.OrNull(l.Position),
                Self = GradeOrBlank(l.Self),
                Collective = GradeOrBlank(l.Collective),
                Appraisal = GradeOrBlank(l.Appraisal),
                Director = GradeOrBlank(l.Director),
                Final = FormText.Grade(l.Final),
                Note = FormText.OrNull(l.Note)
            }).ToList()
        };
    }

    /// <summary>Số văn bản không kèm chữ "Số" ở đầu (người dùng có thể đã gõ "Số 12-QĐ/ĐU").</summary>
    private static string? DocumentNumberText(string? value)
    {
        var text = FormText.OrNull(value?.Trim());
        if (text != null && text.StartsWith("Số ", StringComparison.OrdinalIgnoreCase))
            text = FormText.OrNull(text[3..].Trim());
        return text;
    }

    /// <summary>Bước chưa có mức → ô trống (không in "Chưa xếp loại").</summary>
    private static string GradeOrBlank(EvaluationGrade grade) =>
        grade == EvaluationGrade.ChuaXepLoai ? string.Empty : FormText.Grade(grade);
}

/// <summary>Số/ngày văn bản chỉ có khi mọi hồ sơ ghi cùng giá trị; MeetingAt là UTC.</summary>
public sealed record DecisionDocumentInfo(string? DocumentNumber, DateTime? DocumentDate, DateTime? MeetingAt, string? ChairName);

public sealed record MauQdLine(
    string? FullName,
    string? Position,
    EvaluationGrade Self,
    EvaluationGrade Collective,
    EvaluationGrade Appraisal,
    EvaluationGrade Director,
    EvaluationGrade Final,
    string? Note = null);

public sealed class MauQdRow
{
    [TemplateField("R_STT")] public string? Order { get; init; }
    [TemplateField("R_NAME")] public string? Name { get; init; }
    [TemplateField("R_POSITION")] public string? Position { get; init; }
    [TemplateField("R_SELF")] public string? Self { get; init; }
    [TemplateField("R_COLLECTIVE")] public string? Collective { get; init; }
    [TemplateField("R_APPRAISAL")] public string? Appraisal { get; init; }
    [TemplateField("R_DIRECTOR")] public string? Director { get; init; }
    [TemplateField("R_FINAL")] public string? Final { get; init; }
    [TemplateField("R_NOTE")] public string? Note { get; init; }
}
