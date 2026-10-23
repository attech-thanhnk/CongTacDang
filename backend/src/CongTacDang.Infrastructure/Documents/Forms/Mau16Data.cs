using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Báo cáo kết quả đánh giá: số liệu mục I/II tính từ kết quả kỳ, phần nhập tay lấy từ bản nháp.</summary>
public sealed class Mau16Data
{
    public const string TemplateFileName = "Mau_16_BaoCaoKetQuaDanhGia.docx";

    [TemplateField("PARTY_PARENT")] public string? PartyParent { get; init; }
    [TemplateField("PARTY_ORG")] public string? PartyOrganization { get; init; }

    /// <summary>Lần lưu bản nháp gần nhất; chưa lưu → giữ dòng chấm.</summary>
    [TemplateField("DATE_LINE")] public string? DateLine { get; init; }

    [TemplateField("DOC_NUMBER")] public string? DocumentNumber { get; init; }

    /// <summary>Chỗ điền liền với chữ trước nên giá trị có khoảng trắng đầu.</summary>
    [TemplateField("QUARTER")] public string? Quarter { get; init; }
    [TemplateField("YEAR")] public string? Year { get; init; }

    [TemplateField("RECIPIENT")] public string? Recipient { get; init; }

    [TemplateField("WORKING_RULES")] public string? WorkingRules { get; init; }

    /// <summary>"Ngày…, ……. đã tổ chức Hội nghị …".</summary>
    [TemplateField("MEETING_DATE")] public string? MeetingDate { get; init; }
    [TemplateField("ORGANIZER")] public string? Organizer { get; init; }

    /// <summary>Mục I: đảng ủy, chi ủy cơ sở quyết định; dòng cuối "Tổng cộng".</summary>
    [TemplateCollection("BASE_ROWS")] public List<Mau16BaseRow> BaseRows { get; init; } = new();

    /// <summary>Mục II: Ban Thường vụ Đảng ủy cấp trên quyết định; dòng cuối "Tổng cộng".</summary>
    [TemplateCollection("SUPERIOR_ROWS")] public List<Mau16SuperiorRow> SuperiorRows { get; init; } = new();

    [TemplateField("PROPOSER")] public string? Proposer { get; init; }
    [TemplateField("PROPOSAL_1")] public string? Proposal1 { get; init; }
    [TemplateField("PROPOSAL_2")] public string? Proposal2 { get; init; }
    [TemplateField("PROPOSAL_3")] public string? Proposal3 { get; init; }
    [TemplateField("SIGNER_NAME")] public string? SignerName { get; init; }

    /// <summary>Trường trống → giữ chữ mẫu của biểu mẫu.</summary>
    public static Mau16Data From(EvaluationPeriod period, PartyHeader header, Form16DraftDto draft)
    {
        var c = draft.Content;
        string? Numbered(string number, string? text) => FormText.OrNull(text) is { } t ? number + ". " + t.Trim() : null;
        return new Mau16Data
        {
            PartyParent = PartyHeader.Lines(header.Parent),
            PartyOrganization = PartyHeader.Lines(header.Organization),
            DateLine = CollectiveFormText.DateLine(draft.UpdatedAt),
            DocumentNumber = FormText.OrNull(c.DocumentNumber),
            Quarter = " " + CollectiveFormText.RomanQuarter(period.Quarter),
            Year = " " + FormText.Year(period.Year),
            Recipient = FormText.OrNull(c.Recipient),
            WorkingRules = FormText.OrNull(c.WorkingRules),
            MeetingDate = CollectiveFormText.Spaced(c.MeetingDate ?? draft.SuggestedMeetingDate),
            Organizer = FormText.OrNull(c.Organizer) ?? header.UnitName,
            BaseRows = draft.BaseRows.Select((r, i) => new Mau16BaseRow(Cells(r, i, draft.BaseRows.Count))).ToList(),
            SuperiorRows = draft.SuperiorRows.Select((r, i) => new Mau16SuperiorRow(Cells(r, i, draft.SuperiorRows.Count))).ToList(),
            Proposer = FormText.OrNull(c.Proposer) ?? header.UnitName,
            Proposal1 = Numbered("1", c.Proposal1),
            Proposal2 = Numbered("2", c.Proposal2),
            Proposal3 = Numbered("3", c.Proposal3),
            SignerName = FormText.OrNull(c.SignerName)
        };
    }

    /// <summary>10 cột của một dòng; dòng cuối "Tổng cộng" không đánh số.</summary>
    private static string?[] Cells(Form16SummaryRowDto row, int index, int count)
    {
        static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
        var isTotal = index == count - 1;
        return new[]
        {
            isTotal ? string.Empty : N(index + 1),
            row.Subject,
            N(row.Total), N(row.Excellent), N(row.Good), N(row.Satisfactory), N(row.Unsatisfactory), N(row.NotRated),
            row.ExcellentPercent.HasValue ? FormText.Number(row.ExcellentPercent.Value, 1) + "%" : string.Empty,
            row.StatCode ?? string.Empty
        };
    }
}

public sealed class Mau16BaseRow
{
    public Mau16BaseRow() { }

    internal Mau16BaseRow(string?[] v)
    {
        (Order, Subject, Total, Excellent, Good, Satisfactory, Unsatisfactory, NotRated, Percent, Note) =
            (v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9]);
    }

    [TemplateField("B_STT")] public string? Order { get; init; }
    [TemplateField("B_SUBJECT")] public string? Subject { get; init; }
    [TemplateField("B_TOTAL")] public string? Total { get; init; }
    [TemplateField("B_EXC")] public string? Excellent { get; init; }
    [TemplateField("B_GOOD")] public string? Good { get; init; }
    [TemplateField("B_SAT")] public string? Satisfactory { get; init; }
    [TemplateField("B_UNSAT")] public string? Unsatisfactory { get; init; }
    [TemplateField("B_NONE")] public string? NotRated { get; init; }
    [TemplateField("B_PCT")] public string? Percent { get; init; }
    [TemplateField("B_NOTE")] public string? Note { get; init; }
}

public sealed class Mau16SuperiorRow
{
    public Mau16SuperiorRow() { }

    internal Mau16SuperiorRow(string?[] v)
    {
        (Order, Subject, Total, Excellent, Good, Satisfactory, Unsatisfactory, NotRated, Percent, Note) =
            (v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9]);
    }

    [TemplateField("S_STT")] public string? Order { get; init; }
    [TemplateField("S_SUBJECT")] public string? Subject { get; init; }
    [TemplateField("S_TOTAL")] public string? Total { get; init; }
    [TemplateField("S_EXC")] public string? Excellent { get; init; }
    [TemplateField("S_GOOD")] public string? Good { get; init; }
    [TemplateField("S_SAT")] public string? Satisfactory { get; init; }
    [TemplateField("S_UNSAT")] public string? Unsatisfactory { get; init; }
    [TemplateField("S_NONE")] public string? NotRated { get; init; }
    [TemplateField("S_PCT")] public string? Percent { get; init; }
    [TemplateField("S_NOTE")] public string? Note { get; init; }
}
