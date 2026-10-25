using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

/// <summary>Người dự hội nghị (mục 3.2 Mẫu 12).</summary>
public class MeetingAttendeeDto
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Chức vụ Đảng, chính quyền.</summary>
    public string Title { get; set; } = string.Empty;
}

/// <summary>Các mục biên bản Mẫu 12 chưa có cột riêng (jsonb theo mã mục).</summary>
public class MeetingDetailsDto
{
    /// <summary>Phần sau chữ "của" trong "Căn cứ Quy chế làm việc của … nhiệm kỳ …".</summary>
    public string? WorkingRules { get; set; }

    /// <summary>Đơn vị báo cáo tình hình thực hiện nhiệm vụ trọng tâm (mục II).</summary>
    public string? ReportingUnit { get; set; }

    public string? ChairTitle { get; set; }

    public string? SecretaryTitle { get; set; }

    /// <summary>Mục 3.2: người được cử ghi chép, báo cáo, phục vụ hội nghị.</summary>
    public List<MeetingAttendeeDto> Attendees { get; set; } = new();

    /// <summary>Mẫu 13: người đầu tiên là Tổ trưởng, những người sau là Thành viên.</summary>
    public List<MeetingAttendeeDto> CountingCommittee { get; set; } = new();

    /// <summary>Mẫu 13; null = chưa ghi.</summary>
    public int? BallotsIssued { get; set; }

    public int? BallotsCollected { get; set; }

    public int? BallotsValid { get; set; }

    public int? BallotsInvalid { get; set; }
}

/// <summary>Nguyên văn biểu mẫu gốc.</summary>
public class CollectiveFormSectionDto
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

/// <summary>Mục nhập Mẫu 07 và nhóm nội dung Mẫu 08.</summary>
public class CollectiveFormCatalogDto
{
    /// <summary>Mẫu 07 mục I: 4 nội dung (I.1–I.4).</summary>
    public List<CollectiveFormSectionDto> Form07Strengths { get; set; } = new();

    /// <summary>Mẫu 06: số nhiệm vụ, nội dung văn bản và người đứng đầu (mã T.*, S.*, H.*).</summary>
    public List<CollectiveFormSectionDto> Form06Fields { get; set; } = new();

    /// <summary>Mẫu 08: 13 nhóm (mã 1–13).</summary>
    public List<CollectiveFormSectionDto> Form08Categories { get; set; } = new();

    /// <summary>Chỉ có khi gọi kèm <c>periodId</c>; rỗng = nhập thẳng hai điểm nhóm.</summary>
    public List<CollectiveCriteriaGroupDto> CollectiveGroups { get; set; } = new();

    /// <summary>Theo bộ tiêu chí của kỳ (mặc định 30 / 70).</summary>
    public double GeneralMaxScore { get; set; } = 30;
    public double TaskMaxScore { get; set; } = 70;
}

/// <summary>Phần nhập tay của Mẫu 16 (nháp theo kỳ + tổ chức Đảng); trường trống → giữ chữ mẫu.</summary>
public class Form16DraftContentDto
{
    /// <summary>Vd. "Số 15-BC/ĐU".</summary>
    public string? DocumentNumber { get; set; }

    /// <summary>"Kính gửi: …", "Kính trình …".</summary>
    public string? Recipient { get; set; }

    /// <summary>Phần sau chữ "của" trong "Căn cứ Quy chế làm việc của … nhiệm kỳ …".</summary>
    public string? WorkingRules { get; set; }

    /// <summary>Dạng chữ, vd. "15/9/2026"; trống → ngày hội nghị quyết định mới nhất đã ghi.</summary>
    public string? MeetingDate { get; set; }

    /// <summary>Trống → tên tổ chức Đảng của báo cáo.</summary>
    public string? Organizer { get; set; }

    /// <summary>Mục III "Đề xuất của …"; trống → tên tổ chức Đảng của báo cáo.</summary>
    public string? Proposer { get; set; }

    /// <summary>Mục III.1.</summary>
    public string? Proposal1 { get; set; }

    /// <summary>Mục III.2.</summary>
    public string? Proposal2 { get; set; }

    /// <summary>Mục III.3.</summary>
    public string? Proposal3 { get; set; }

    /// <summary>Bí thư ký báo cáo.</summary>
    public string? SignerName { get; set; }
}

/// <summary>Dòng tổng hợp Mẫu 16 theo nhóm chức danh.</summary>
public class Form16SummaryRowDto
{
    public string? StatCode { get; set; }
    public string Subject { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Excellent { get; set; }
    public int Good { get; set; }
    public int Satisfactory { get; set; }
    public int Unsatisfactory { get; set; }
    public int NotRated { get; set; }

    /// <summary>% Xuất sắc trong số tốt trở lên; null khi không có ai tốt trở lên.</summary>
    public double? ExcellentPercent { get; set; }

    /// <summary>Trần Xuất sắc của nhóm (không tính người đứng đầu); null với dòng "Tổng cộng".</summary>
    public int? MaxExcellentAllowed { get; set; }

    /// <summary>Chỉ cảnh báo khi xem trước, không in lên biểu mẫu.</summary>
    public bool IsExceedingQuota { get; set; }
}

/// <summary>Nháp Mẫu 16 kèm số liệu tổng hợp tự động.</summary>
public class Form16DraftDto
{
    public Guid PeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public Guid? PartyCellId { get; set; }
    public string PartyOrganizationName { get; set; } = string.Empty;

    /// <summary>null = chưa lưu lần nào.</summary>
    public uint? Version { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Form16DraftContentDto Content { get; set; } = new();

    /// <summary>Mục I — thẩm quyền cơ sở; dòng cuối là "Tổng cộng".</summary>
    public List<Form16SummaryRowDto> BaseRows { get; set; } = new();

    /// <summary>Mục II — thẩm quyền cấp trên; dòng cuối là "Tổng cộng".</summary>
    public List<Form16SummaryRowDto> SuperiorRows { get; set; } = new();

    /// <summary>Ngày hội nghị quyết định mới nhất đã ghi trong phạm vi.</summary>
    public string? SuggestedMeetingDate { get; set; }
}

public class SaveForm16DraftDto
{
    public uint? Version { get; set; }
    public Form16DraftContentDto Content { get; set; } = new();
}

/// <summary>Nhóm của khung tiêu chí tập thể.</summary>
public class CollectiveCriteriaGroupDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>"General" (nhóm tiêu chí chung) | "Task" (nhóm kết quả thực hiện nhiệm vụ).</summary>
    public string Kind { get; set; } = "General";

    public List<CollectiveCriteriaItemDto> Items { get; set; } = new();
}

public class CollectiveCriteriaItemDto
{
    public string Code { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public double MaxScore { get; set; }
}
