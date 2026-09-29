using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

/// <summary>Một người dự hội nghị ở mục 3.2 của Mẫu 12 (cán bộ được cử ghi chép, báo cáo, phục vụ hội nghị).</summary>
public class MeetingAttendeeDto
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Chức vụ Đảng, chính quyền.</summary>
    public string Title { get; set; } = string.Empty;
}

/// <summary>Các mục của biên bản hội nghị Mẫu 12 chưa có cột riêng (lưu jsonb theo mã mục).</summary>
public class MeetingDetailsDto
{
    /// <summary>"Căn cứ Quy chế làm việc của … nhiệm kỳ …" — phần sau chữ "của".</summary>
    public string? WorkingRules { get; set; }

    /// <summary>Cơ quan, đơn vị, tổ chức báo cáo tình hình thực hiện nhiệm vụ trọng tâm (mục II).</summary>
    public string? ReportingUnit { get; set; }

    /// <summary>Chức vụ Đảng, chính quyền của chủ trì hội nghị.</summary>
    public string? ChairTitle { get; set; }

    /// <summary>Chức vụ Đảng, chính quyền của thư ký hội nghị.</summary>
    public string? SecretaryTitle { get; set; }

    /// <summary>Mục 3.2: cán bộ, đảng viên khác được cử tham dự để ghi chép, báo cáo, phục vụ hội nghị.</summary>
    public List<MeetingAttendeeDto> Attendees { get; set; } = new();
}

/// <summary>Một mục/nhóm nội dung của biểu mẫu tập thể (đúng nguyên văn biểu mẫu gốc HD03).</summary>
public class CollectiveFormSectionDto
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

/// <summary>Danh mục mục nhập của Mẫu 07 và nhóm nội dung của Mẫu 08 (để trang nhập liệu hiển thị đúng biểu mẫu).</summary>
public class CollectiveFormCatalogDto
{
    /// <summary>Mẫu 07, mục I "Ưu điểm, kết quả đạt được": 4 nội dung (mã I.1–I.4).</summary>
    public List<CollectiveFormSectionDto> Form07Strengths { get; set; } = new();

    /// <summary>Mẫu 08: 13 nhóm nội dung công việc (mã 1–13).</summary>
    public List<CollectiveFormSectionDto> Form08Categories { get; set; } = new();
}

/// <summary>Phần nhập tay của báo cáo Mẫu 16 (bản nháp theo kỳ + tổ chức Đảng). Trường để trống → giữ chữ mẫu của biểu mẫu.</summary>
public class Form16DraftContentDto
{
    /// <summary>Số văn bản, ví dụ "Số 15-BC/ĐU".</summary>
    public string? DocumentNumber { get; set; }

    /// <summary>Nơi gửi ("Kính gửi: …", "Kính trình …").</summary>
    public string? Recipient { get; set; }

    /// <summary>"Căn cứ Quy chế làm việc của … nhiệm kỳ …" — phần sau chữ "của".</summary>
    public string? WorkingRules { get; set; }

    /// <summary>Ngày tổ chức hội nghị (chữ, ví dụ "15/9/2026"); để trống → lấy ngày hội nghị quyết định mới nhất đã ghi.</summary>
    public string? MeetingDate { get; set; }

    /// <summary>Tổ chức Đảng tổ chức hội nghị; để trống → tên tổ chức Đảng của báo cáo.</summary>
    public string? Organizer { get; set; }

    /// <summary>Mục III "Đề xuất của …"; để trống → tên tổ chức Đảng của báo cáo.</summary>
    public string? Proposer { get; set; }

    /// <summary>Mục III.1 (thay đoạn mẫu "Kính đề nghị … xem xét, quyết định đánh giá, xếp loại …").</summary>
    public string? Proposal1 { get; set; }

    /// <summary>Mục III.2 (điều chuyển, bố trí công tác khác đối với cán bộ vi phạm…).</summary>
    public string? Proposal2 { get; set; }

    /// <summary>Mục III.3 (ý kiến đề xuất khác).</summary>
    public string? Proposal3 { get; set; }

    /// <summary>Họ tên Bí thư ký báo cáo.</summary>
    public string? SignerName { get; set; }
}

/// <summary>Một dòng số liệu tổng hợp của Mẫu 16 (theo nhóm chức danh).</summary>
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

    /// <summary>Tỷ lệ % xếp loại xuất sắc trong số xếp loại tốt trở lên; null khi không có ai tốt trở lên.</summary>
    public double? ExcellentPercent { get; set; }
}

/// <summary>Bản nháp Mẫu 16 kèm số liệu tổng hợp tự động (xem trước trước khi xuất).</summary>
public class Form16DraftDto
{
    public Guid PeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public Guid? PartyCellId { get; set; }
    public string PartyOrganizationName { get; set; } = string.Empty;

    /// <summary>Phiên bản bản nháp (null = chưa lưu lần nào).</summary>
    public uint? Version { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Form16DraftContentDto Content { get; set; } = new();

    /// <summary>Mục I — thuộc thẩm quyền đảng ủy, chi ủy cơ sở (dòng cuối là "Tổng cộng").</summary>
    public List<Form16SummaryRowDto> BaseRows { get; set; } = new();

    /// <summary>Mục II — thuộc thẩm quyền Ban Thường vụ Đảng ủy Tổng công ty (dòng cuối là "Tổng cộng").</summary>
    public List<Form16SummaryRowDto> SuperiorRows { get; set; } = new();

    /// <summary>Ngày hội nghị quyết định mới nhất đã ghi trong phạm vi (gợi ý cho "Ngày…").</summary>
    public string? SuggestedMeetingDate { get; set; }
}

/// <summary>Lưu bản nháp Mẫu 16.</summary>
public class SaveForm16DraftDto
{
    public uint? Version { get; set; }
    public Form16DraftContentDto Content { get; set; } = new();
}
