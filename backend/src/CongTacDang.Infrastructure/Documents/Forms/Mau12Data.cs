using System.Collections.Generic;
using System.Linq;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>
/// Dữ liệu Mẫu 12 — Biên bản hội nghị tập thể lãnh đạo, quản lý (B3a) hoặc hội nghị Đảng ủy/Chi ủy cơ sở (B4) về việc đánh giá,
/// xếp loại chất lượng cán bộ quý. Template: <c>Mau_12_BienBanHoiNghi.docx</c> (biểu mẫu gốc HD03, PDF tr.68–69).
/// </summary>
public sealed class Mau12Data
{
    /// <summary>Tên tệp template.</summary>
    public const string TemplateFileName = "Mau_12_BienBanHoiNghi.docx";

    [TemplateField("PARTY_PARENT")] public string? PartyParent { get; init; }
    [TemplateField("PARTY_ORG")] public string? PartyOrganization { get; init; }

    /// <summary>"Hội nghị tập thể lãnh đạo, quản lý &lt;đơn vị&gt;" (B3a) hoặc "Hội nghị &lt;Đảng ủy/Chi ủy&gt;" (B4).</summary>
    [TemplateField("MEETING_NAME")] public string? MeetingName { get; init; }

    /// <summary>" III/2026" — chỗ điền sau chữ "quý".</summary>
    [TemplateField("PERIOD_TEXT")] public string? PeriodText { get; init; }

    /// <summary>"Căn cứ Quy chế làm việc của … nhiệm kỳ …".</summary>
    [TemplateField("WORKING_RULES")] public string? WorkingRules { get; init; }

    /// <summary>Tập thể/tổ chức Đảng tổ chức hội nghị (đầu đoạn "… đã tổ chức Hội nghị để …").</summary>
    [TemplateField("ORGANIZER")] public string? Organizer { get; init; }

    /// <summary>Mục đích hội nghị theo bước: đề xuất (B3a) hoặc quyết định, phê duyệt (B4) mức xếp loại.</summary>
    [TemplateField("PURPOSE")] public string? Purpose { get; init; }
    [TemplateField("VOTE_PURPOSE")] public string? VotePurpose { get; init; }

    [TemplateField("START_TIME")] public string? StartTime { get; init; }
    [TemplateField("START_DATE")] public string? StartDate { get; init; }
    [TemplateField("END_TIME")] public string? EndTime { get; init; }
    [TemplateField("LOCATION")] public string? Location { get; init; }
    [TemplateField("INVITED")] public string? Invited { get; init; }
    [TemplateField("PRESENT")] public string? Present { get; init; }
    [TemplateField("ABSENT")] public string? Absent { get; init; }

    /// <summary>Mục 3.2 — cán bộ, đảng viên được cử tham dự ghi chép, báo cáo, phục vụ hội nghị.</summary>
    [TemplateCollection("ATTENDEES")] public List<Mau12Attendee> Attendees { get; init; } = new();

    /// <summary>Có người ở mục 3.2 (không có → giữ dòng "…" của biểu mẫu).</summary>
    [TemplateCondition("HAS_ATTENDEES")] public bool HasAttendees { get; init; }

    [TemplateField("CHAIR_NAME")] public string? ChairName { get; init; }
    [TemplateField("CHAIR_TITLE")] public string? ChairTitle { get; init; }
    [TemplateField("SECRETARY_NAME")] public string? SecretaryName { get; init; }
    [TemplateField("SECRETARY_TITLE")] public string? SecretaryTitle { get; init; }

    /// <summary>Cơ quan, đơn vị báo cáo tình hình thực hiện nhiệm vụ trọng tâm (mục II).</summary>
    [TemplateField("REPORTING_UNIT")] public string? ReportingUnit { get; init; }

    /// <summary>Nội dung, diễn biến và kết quả hội nghị đã ghi (không có → không in đoạn này).</summary>
    [TemplateCondition("HAS_CONTENT")] public bool HasContent { get; init; }
    [TemplateField("CONTENT")] public string? Content { get; init; }

    /// <summary>"01 bản lưu tại &lt;Đảng ủy/Chi bộ&gt;".</summary>
    [TemplateField("ARCHIVE_UNIT")] public string? ArchiveUnit { get; init; }

    /// <summary>Họ tên dưới chữ ký thư ký / chủ trì.</summary>
    [TemplateField("SECRETARY_SIGN")] public string? SecretarySign { get; init; }
    [TemplateField("CHAIR_SIGN")] public string? ChairSign { get; init; }

    /// <summary>
    /// Dựng từ biên bản đã lưu. <paramref name="unitName"/>: đơn vị tổ chức hội nghị (Phòng / tổ chức Đảng / Đảng bộ);
    /// <paramref name="archiveUnit"/>: tổ chức Đảng lưu biên bản.
    /// </summary>
    public static Mau12Data From(EvaluationMeeting meeting, PartyHeader header, string? unitName, string? archiveUnit, MeetingDetailsDto details)
    {
        var unit = FormText.OrNull(unitName?.Trim());
        var period = meeting.Period;
        var isProposal = meeting.Stage == WorkflowStep.B3A_COLLECTIVE;
        var isDecision = meeting.Stage == WorkflowStep.B4_DECISION;
        var content = string.Join("\n", new[] { meeting.MinutesContent, meeting.OutcomeContent }
            .Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()));
        var attendees = details.Attendees.Where(a => !string.IsNullOrWhiteSpace(a.Name)).ToList();

        return new Mau12Data
        {
            PartyParent = header.Parent,
            PartyOrganization = header.Organization,
            MeetingName = unit == null ? null
                : isProposal ? "Hội nghị tập thể lãnh đạo, quản lý " + unit
                : isDecision ? "Hội nghị " + unit
                : null,
            PeriodText = period == null ? null : " " + CollectiveFormText.RomanQuarter(period.Quarter) + "/" + FormText.Year(period.Year),
            WorkingRules = CollectiveFormText.Spaced(details.WorkingRules),
            Organizer = unit == null ? null
                : isProposal ? "Tập thể lãnh đạo, quản lý " + unit
                : isDecision ? unit
                : null,
            Purpose = isProposal ? "nhận xét, đánh giá, bỏ phiếu đề xuất mức xếp loại"
                : isDecision ? "nhận xét, đánh giá, bỏ phiếu quyết định, phê duyệt mức xếp loại"
                : null,
            VotePurpose = isProposal ? "đề xuất mức xếp loại"
                : isDecision ? "quyết định, phê duyệt mức xếp loại"
                : null,
            StartTime = CollectiveFormText.Time(meeting.StartedAt),
            StartDate = CollectiveFormText.Date(meeting.StartedAt),
            EndTime = meeting.EndedAt.HasValue ? CollectiveFormText.Time(meeting.EndedAt.Value) : null,
            Location = FormText.OrNull(meeting.Location),
            Invited = meeting.InvitedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Present = meeting.PresentCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Absent = meeting.AbsentCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Attendees = attendees.Select((a, i) => new Mau12Attendee
            {
                Order = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                Name = a.Name.Trim(),
                Title = CollectiveFormText.Spaced(a.Title)
            }).ToList(),
            HasAttendees = attendees.Count > 0,
            ChairName = FormText.OrNull(meeting.ChairName),
            ChairTitle = CollectiveFormText.Spaced(details.ChairTitle),
            SecretaryName = FormText.OrNull(meeting.SecretaryName),
            SecretaryTitle = CollectiveFormText.Spaced(details.SecretaryTitle),
            ReportingUnit = CollectiveFormText.Spaced(details.ReportingUnit ?? unit),
            HasContent = content.Length > 0,
            Content = FormText.OrNull(content),
            ArchiveUnit = FormText.OrNull(archiveUnit?.Trim()),
            SecretarySign = FormText.OrNull(meeting.SecretaryName),
            ChairSign = FormText.OrNull(meeting.ChairName)
        };
    }
}

/// <summary>Một người ở mục 3.2 của Mẫu 12.</summary>
public sealed class Mau12Attendee
{
    [TemplateField("A_STT")] public string? Order { get; init; }
    [TemplateField("A_NAME")] public string? Name { get; init; }
    [TemplateField("A_TITLE")] public string? Title { get; init; }
}
