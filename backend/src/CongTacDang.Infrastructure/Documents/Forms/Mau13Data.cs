using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>
/// Dữ liệu Mẫu 13 — Biên bản kiểm phiếu hội nghị tập thể lãnh đạo, quản lý (B3a) hoặc hội nghị Đảng ủy/Chi ủy cơ sở (B4) về
/// việc đánh giá, xếp loại chất lượng cán bộ quý. Template: <c>Mau_13_BienBanKiemPhieu.docx</c>, dựng từ đúng phần Mẫu 13 của
/// biểu mẫu gốc HD03 (PDF tr.70–71). Dữ liệu lấy từ một biên bản hội nghị đã lưu và kết quả kiểm phiếu tổng hợp theo hồ sơ.
/// </summary>
public sealed class Mau13Data
{
    /// <summary>Tên tệp template.</summary>
    public const string TemplateFileName = "Mau_13_BienBanKiemPhieu.docx";

    [TemplateField("PARTY_PARENT")] public string? PartyParent { get; init; }
    [TemplateField("PARTY_ORG")] public string? PartyOrganization { get; init; }

    /// <summary>"Hội nghị tập thể lãnh đạo, quản lý &lt;đơn vị&gt;" (B3a) hoặc "Hội nghị &lt;Đảng ủy/Chi ủy&gt;" (B4).</summary>
    [TemplateField("MEETING_NAME")] public string? MeetingName { get; init; }

    /// <summary>"III/2026" — chỗ điền "……../….." của dòng tiêu đề.</summary>
    [TemplateField("QUARTER_YEAR")] public string? QuarterYear { get; init; }

    /// <summary>" III/2026" — chỗ điền ngay sau chữ "quý" trong đoạn văn.</summary>
    [TemplateField("PERIOD_TEXT")] public string? PeriodText { get; init; }

    [TemplateField("WORKING_RULES")] public string? WorkingRules { get; init; }
    [TemplateField("ORGANIZER")] public string? Organizer { get; init; }
    [TemplateField("PURPOSE")] public string? Purpose { get; init; }
    [TemplateField("VOTE_PURPOSE")] public string? VotePurpose { get; init; }
    [TemplateField("START_TIME")] public string? StartTime { get; init; }
    [TemplateField("START_DATE")] public string? StartDate { get; init; }
    [TemplateField("END_TIME")] public string? EndTime { get; init; }
    [TemplateField("LOCATION")] public string? Location { get; init; }
    [TemplateField("INVITED")] public string? Invited { get; init; }
    [TemplateField("PRESENT")] public string? Present { get; init; }
    [TemplateField("ABSENT")] public string? Absent { get; init; }

    /// <summary>Mục I.3.2 — cán bộ, đảng viên được cử tham dự ghi chép, báo cáo, phục vụ hội nghị.</summary>
    [TemplateCollection("ATTENDEES")] public List<Mau12Attendee> Attendees { get; init; } = new();
    [TemplateCondition("HAS_ATTENDEES")] public bool HasAttendees { get; init; }

    [TemplateField("CHAIR_NAME")] public string? ChairName { get; init; }
    [TemplateField("CHAIR_TITLE")] public string? ChairTitle { get; init; }
    [TemplateField("SECRETARY_NAME")] public string? SecretaryName { get; init; }
    [TemplateField("SECRETARY_TITLE")] public string? SecretaryTitle { get; init; }
    [TemplateField("REPORTING_UNIT")] public string? ReportingUnit { get; init; }

    /// <summary>Tổ kiểm phiếu (mục II): người đầu là Tổ trưởng, còn lại là Thành viên; không có → giữ các dòng của biểu mẫu.</summary>
    [TemplateCollection("COUNTERS")] public List<Mau13Counter> Counters { get; init; } = new();
    [TemplateCondition("HAS_COUNTERS")] public bool HasCounters { get; init; }

    [TemplateField("BALLOTS_ISSUED")] public string? BallotsIssued { get; init; }
    [TemplateField("BALLOTS_COLLECTED")] public string? BallotsCollected { get; init; }
    [TemplateField("BALLOTS_VALID")] public string? BallotsValid { get; init; }
    [TemplateField("BALLOTS_INVALID")] public string? BallotsInvalid { get; init; }

    /// <summary>Mục I của bảng: cán bộ thuộc thẩm quyền quyết định, phê duyệt mức xếp loại của BTVĐUTCT.</summary>
    [TemplateCollection("SUPERIOR_ROWS")] public List<Mau13Row> SuperiorRows { get; init; } = new();
    [TemplateCondition("HAS_SUPERIOR_ROWS")] public bool HasSuperiorRows { get; init; }

    /// <summary>Mục II của bảng: cán bộ thuộc thẩm quyền quyết định, phê duyệt mức xếp loại của Đảng ủy/Chi ủy cơ sở.</summary>
    [TemplateCollection("BASE_ROWS")] public List<Mau13Row> BaseRows { get; init; } = new();
    [TemplateCondition("HAS_BASE_ROWS")] public bool HasBaseRows { get; init; }

    [TemplateField("ARCHIVE_UNIT")] public string? ArchiveUnit { get; init; }

    /// <summary>Họ tên dưới chữ ký Tổ trưởng Tổ kiểm phiếu / chủ trì hội nghị.</summary>
    [TemplateField("COUNTER_HEAD_SIGN")] public string? CounterHeadSign { get; init; }
    [TemplateField("CHAIR_SIGN")] public string? ChairSign { get; init; }

    /// <summary>
    /// Dựng từ biên bản đã lưu (nạp kèm kỳ) và các dòng kết quả kiểm phiếu. Phần chung với Mẫu 12 (tiêu đề, thời gian, thành phần,
    /// chủ trì, thư ký) dùng cùng quy tắc với <see cref="Mau12Data"/>.
    /// </summary>
    public static Mau13Data From(
        EvaluationMeeting meeting, PartyHeader header, string? unitName, string? archiveUnit, MeetingDetailsDto details,
        IReadOnlyList<Mau13Line> lines)
    {
        var common = Mau12Data.From(meeting, header, unitName, archiveUnit, details);
        var period = meeting.Period;
        var committee = details.CountingCommittee.Where(c => !string.IsNullOrWhiteSpace(c.Name)).ToList();

        List<Mau13Row> Rows(ApprovalAuthority authority) => lines
            .Where(l => l.Authority == authority)
            .Select((l, i) => new Mau13Row
            {
                Order = (i + 1).ToString(CultureInfo.InvariantCulture),
                Name = FormText.OrNull(l.FullName),
                PositionAndUnit = FormText.OrNull(l.PositionAndUnit),
                VotesExcellent = Count(l.Summary.VotesExcellent),
                VotesGood = Count(l.Summary.VotesGood),
                VotesSatisfactory = Count(l.Summary.VotesSatisfactory),
                VotesUnsatisfactory = Count(l.Summary.VotesUnsatisfactory),
                VotesNotRated = Count(l.Summary.VotesNotRated),
                Note = FormText.OrNull(l.Summary.Notes?.Trim())
            })
            .ToList();

        var superior = Rows(ApprovalAuthority.CapTren);
        var baseRows = Rows(ApprovalAuthority.CoSo);

        // Số phiếu không hợp lệ: ưu tiên số ghi trên biên bản; chưa ghi thì dùng số chung của các dòng kiểm phiếu (khi mọi dòng
        // cùng một số).
        var invalid = details.BallotsInvalid;
        if (invalid == null)
        {
            var values = lines.Select(l => l.Summary.InvalidVotes).Distinct().ToList();
            if (values.Count == 1)
                invalid = values[0];
        }

        return new Mau13Data
        {
            PartyParent = common.PartyParent,
            PartyOrganization = common.PartyOrganization,
            MeetingName = common.MeetingName,
            QuarterYear = period == null ? null : CollectiveFormText.RomanQuarter(period.Quarter) + "/" + FormText.Year(period.Year),
            PeriodText = common.PeriodText,
            WorkingRules = common.WorkingRules,
            Organizer = common.Organizer,
            Purpose = common.Purpose,
            VotePurpose = common.VotePurpose,
            StartTime = common.StartTime,
            StartDate = common.StartDate,
            EndTime = common.EndTime,
            Location = common.Location,
            Invited = common.Invited,
            Present = common.Present,
            Absent = common.Absent,
            Attendees = common.Attendees,
            HasAttendees = common.HasAttendees,
            ChairName = common.ChairName,
            ChairTitle = common.ChairTitle,
            SecretaryName = common.SecretaryName,
            SecretaryTitle = common.SecretaryTitle,
            ReportingUnit = common.ReportingUnit,
            Counters = committee.Select((c, i) => new Mau13Counter
            {
                Order = (i + 1).ToString(CultureInfo.InvariantCulture),
                Name = c.Name.Trim(),
                Title = CollectiveFormText.Spaced(c.Title),
                Role = i == 0 ? "Tổ trưởng" : "Thành viên"
            }).ToList(),
            HasCounters = committee.Count > 0,
            BallotsIssued = Number(details.BallotsIssued),
            BallotsCollected = Number(details.BallotsCollected),
            BallotsValid = Number(details.BallotsValid),
            BallotsInvalid = Number(invalid),
            SuperiorRows = superior,
            HasSuperiorRows = superior.Count > 0,
            BaseRows = baseRows,
            HasBaseRows = baseRows.Count > 0,
            ArchiveUnit = common.ArchiveUnit,
            CounterHeadSign = committee.Count > 0 ? committee[0].Name.Trim() : null,
            ChairSign = common.ChairSign
        };
    }

    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string? Number(int? value) => value?.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Một dòng kết quả kiểm phiếu của biên bản kèm thông tin cán bộ để in Mẫu 13.</summary>
public sealed record Mau13Line(EvaluationMeetingVoteSummary Summary, string? FullName, string? PositionAndUnit, ApprovalAuthority Authority);

/// <summary>Một thành viên Tổ kiểm phiếu.</summary>
public sealed class Mau13Counter
{
    [TemplateField("C_STT")] public string? Order { get; init; }
    [TemplateField("C_NAME")] public string? Name { get; init; }
    [TemplateField("C_TITLE")] public string? Title { get; init; }
    [TemplateField("C_ROLE")] public string? Role { get; init; }
}

/// <summary>Một dòng cán bộ của bảng kết quả Mẫu 13 (cùng tag cho khối lặp mục I và mục II).</summary>
public sealed class Mau13Row
{
    [TemplateField("R_STT")] public string? Order { get; init; }
    [TemplateField("R_NAME")] public string? Name { get; init; }
    [TemplateField("R_POSITION")] public string? PositionAndUnit { get; init; }
    [TemplateField("R_EXC")] public string? VotesExcellent { get; init; }
    [TemplateField("R_GOOD")] public string? VotesGood { get; init; }
    [TemplateField("R_SAT")] public string? VotesSatisfactory { get; init; }
    [TemplateField("R_UNSAT")] public string? VotesUnsatisfactory { get; init; }
    [TemplateField("R_NONE")] public string? VotesNotRated { get; init; }
    [TemplateField("R_NOTE")] public string? Note { get; init; }
}
