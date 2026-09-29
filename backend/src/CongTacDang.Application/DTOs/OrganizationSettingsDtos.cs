using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

/// <summary>Thông tin đơn vị dùng trên biểu mẫu, báo cáo, giao diện (task 17 — T-80).</summary>
public sealed class OrganizationSettingsDto
{
    /// <summary>Tên Đảng bộ — dòng tiêu đề trái của biểu mẫu (ghi đúng như in).</summary>
    public string PartyCommitteeName { get; set; } = string.Empty;

    /// <summary>Tên tổ chức Đảng cấp trên (ghi đúng như in).</summary>
    public string SuperiorPartyName { get; set; } = string.Empty;

    /// <summary>Tên đầy đủ của công ty.</summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Tên đơn vị chủ quản cấp trên của công ty.</summary>
    public string ParentCompanyName { get; set; } = string.Empty;

    /// <summary>Tên viết tắt (dùng trong tên tệp tải xuống).</summary>
    public string ShortName { get; set; } = string.Empty;

    /// <summary>Địa danh ở dòng "…, ngày … tháng … năm …".</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>Tên hiển thị của hệ thống trên giao diện.</summary>
    public string SystemName { get; set; } = string.Empty;

    /// <summary>Lần cập nhật gần nhất (UTC); null = giá trị mặc định chưa sửa.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Người cập nhật gần nhất.</summary>
    public string? UpdatedByName { get; set; }
}

/// <summary>Phần thông tin đơn vị hiển thị trước khi đăng nhập (trang đăng nhập).</summary>
public sealed class PublicOrganizationInfoDto
{
    /// <summary>Tên hiển thị của hệ thống.</summary>
    public string SystemName { get; set; } = string.Empty;

    /// <summary>Tên đầy đủ của công ty.</summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Tên đơn vị chủ quản cấp trên.</summary>
    public string ParentCompanyName { get; set; } = string.Empty;

    /// <summary>Tên viết tắt (đặt tên tệp tải xuống phía giao diện).</summary>
    public string ShortName { get; set; } = string.Empty;
}

/// <summary>Dữ liệu sửa thông tin đơn vị.</summary>
public sealed class UpdateOrganizationSettingsDto
{
    public string? PartyCommitteeName { get; set; }
    public string? SuperiorPartyName { get; set; }
    public string? CompanyName { get; set; }
    public string? ParentCompanyName { get; set; }
    public string? ShortName { get; set; }
    public string? Location { get; set; }
    public string? SystemName { get; set; }
}

/// <summary>Một biểu mẫu Word trong danh mục kèm phiên bản đang dùng (task 17 — T-81).</summary>
public sealed class WordTemplateSummaryDto
{
    /// <summary>Mã biểu mẫu (ví dụ <c>MAU_02</c>).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên biểu mẫu.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Tên file mẫu gốc đi kèm ứng dụng.</summary>
    public string TemplateFileName { get; set; } = string.Empty;

    /// <summary>Phiên bản đang dùng; null = đang dùng file mẫu gốc.</summary>
    public WordTemplateVersionDto? ActiveVersion { get; set; }

    /// <summary>Số phiên bản đã tải lên.</summary>
    public int VersionCount { get; set; }

    /// <summary>Danh mục tag của biểu mẫu.</summary>
    public List<WordTemplateTagDto> Tags { get; set; } = new();
}

/// <summary>Một tag trong danh mục tag của biểu mẫu.</summary>
public sealed class WordTemplateTagDto
{
    /// <summary>Tag đúng như gõ trong Word (ví dụ <c>FULL_NAME</c>, <c>repeat:TASKS</c>, <c>if:HAS_COMMENT</c>).</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>Loại: <c>field</c>, <c>repeat</c>, <c>if</c>, <c>ifnot</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Tag bắt buộc (thiếu → cảnh báo); tag thông tin đơn vị dùng chung là tùy chọn.</summary>
    public bool Required { get; set; }

    /// <summary>Tên khối lặp chứa tag (tag của dòng lặp); null = tag cấp tài liệu.</summary>
    public string? Within { get; set; }

    /// <summary>Tag thông tin đơn vị dùng chung (mọi biểu mẫu).</summary>
    public bool IsCommon { get; set; }
}

/// <summary>Một phiên bản file mẫu đã tải lên.</summary>
public sealed class WordTemplateVersionDto
{
    public Guid Id { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Checksum { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? Note { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public DateTime UploadedAt { get; set; }
    public string? UploadedByName { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public string? ActivatedByName { get; set; }
}

/// <summary>Kết quả kiểm tra một file mẫu (trước khi tải lên hoặc khi tải lên).</summary>
public sealed class WordTemplateCheckDto
{
    /// <summary>Không có lỗi → được lưu/kích hoạt (cảnh báo không chặn).</summary>
    public bool IsValid { get; set; }

    /// <summary>Tag có trong tệp.</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>Lỗi (chặn tải lên): không mở được, tag không nhận diện, sinh thử thất bại…</summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>Cảnh báo (không chặn): tag bắt buộc bị thiếu, tài liệu sinh thử có lỗi cấu trúc…</summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>Tag không nhận diện.</summary>
    public List<string> UnknownTags { get; set; } = new();

    /// <summary>Tag bắt buộc bị thiếu.</summary>
    public List<string> MissingRequiredTags { get; set; } = new();

    /// <summary>Phiên bản đã lưu (khi tải lên thành công).</summary>
    public WordTemplateVersionDto? Version { get; set; }
}
