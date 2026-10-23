using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

/// <summary>Thông tin đơn vị dùng trên biểu mẫu, báo cáo, giao diện.</summary>
public sealed class OrganizationSettingsDto
{
    /// <summary>Dòng tiêu đề trái của biểu mẫu (ghi đúng như in).</summary>
    public string PartyCommitteeName { get; set; } = string.Empty;

    /// <summary>Ghi đúng như in.</summary>
    public string SuperiorPartyName { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    public string ParentCompanyName { get; set; } = string.Empty;

    /// <summary>Dùng trong tên tệp tải xuống.</summary>
    public string ShortName { get; set; } = string.Empty;

    /// <summary>Địa danh ở dòng "…, ngày … tháng … năm …".</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>Dòng thứ hai tiêu đề Mẫu 10; rỗng = giữ chữ của biểu mẫu.</summary>
    public string AppraisalUnitName { get; set; } = string.Empty;

    public string SystemName { get; set; } = string.Empty;

    /// <summary>URL tương đối kèm <c>?v=</c>; null = không có ảnh nền đăng nhập.</summary>
    public string? LoginBackgroundUrl { get; set; }

    public string? LoginBackgroundFileName { get; set; }

    /// <summary><c>center</c> | <c>left</c> | <c>right</c>.</summary>
    public string LoginPanelPosition { get; set; } = "center";

    /// <summary>UTC; null = giá trị mặc định chưa sửa.</summary>
    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedByName { get; set; }
}

/// <summary>Hiển thị trước khi đăng nhập.</summary>
public sealed class PublicOrganizationInfoDto
{
    public string SystemName { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    public string ParentCompanyName { get; set; } = string.Empty;

    /// <summary>Dùng đặt tên tệp tải xuống.</summary>
    public string ShortName { get; set; } = string.Empty;

    /// <summary>URL tương đối kèm <c>?v=</c>; null = không có ảnh nền.</summary>
    public string? LoginBackgroundUrl { get; set; }

    public string LoginPanelPosition { get; set; } = "center";
}

public sealed class UpdateOrganizationSettingsDto
{
    public string? PartyCommitteeName { get; set; }
    public string? SuperiorPartyName { get; set; }
    public string? CompanyName { get; set; }
    public string? ParentCompanyName { get; set; }
    public string? ShortName { get; set; }
    public string? Location { get; set; }
    public string? AppraisalUnitName { get; set; }
    public string? SystemName { get; set; }

    /// <summary>null = giữ nguyên.</summary>
    public string? LoginPanelPosition { get; set; }
}

/// <summary>Ảnh nền đăng nhập trả về trình duyệt.</summary>
public sealed record LoginBackgroundFile(System.IO.Stream Content, string ContentType);

/// <summary>Biểu mẫu Word kèm phiên bản đang dùng.</summary>
public sealed class WordTemplateSummaryDto
{
    /// <summary>Vd. <c>MAU_02</c>.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>File mẫu gốc đi kèm ứng dụng.</summary>
    public string TemplateFileName { get; set; } = string.Empty;

    /// <summary>null = đang dùng file mẫu gốc.</summary>
    public WordTemplateVersionDto? ActiveVersion { get; set; }

    public int VersionCount { get; set; }

    /// <summary>Tag bắt buộc mà phiên bản đang dùng thiếu so với danh mục hiện tại; rỗng khi dùng file gốc.</summary>
    public List<string> ActiveMissingTags { get; set; } = new();

    public List<WordTemplateTagDto> Tags { get; set; } = new();
}

public sealed class WordTemplateTagDto
{
    /// <summary>Đúng như gõ trong Word (vd. <c>FULL_NAME</c>, <c>repeat:TASKS</c>, <c>if:HAS_COMMENT</c>).</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary><c>field</c> | <c>repeat</c> | <c>if</c> | <c>ifnot</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Thiếu → cảnh báo; tag thông tin đơn vị dùng chung là tùy chọn.</summary>
    public bool Required { get; set; }

    /// <summary>Khối lặp chứa tag; null = tag cấp tài liệu.</summary>
    public string? Within { get; set; }

    /// <summary>Tag thông tin đơn vị dùng chung mọi biểu mẫu.</summary>
    public bool IsCommon { get; set; }
}

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

/// <summary>Kiểm tra file mẫu trước khi hoặc khi tải lên.</summary>
public sealed class WordTemplateCheckDto
{
    /// <summary>Không có lỗi → được lưu/kích hoạt; cảnh báo không chặn.</summary>
    public bool IsValid { get; set; }

    public List<string> Tags { get; set; } = new();

    /// <summary>Chặn tải lên: không mở được, tag không nhận diện, sinh thử thất bại…</summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>Không chặn: thiếu tag bắt buộc, tài liệu sinh thử lỗi cấu trúc…</summary>
    public List<string> Warnings { get; set; } = new();

    public List<string> UnknownTags { get; set; } = new();

    public List<string> MissingRequiredTags { get; set; } = new();

    /// <summary>Khi tải lên thành công.</summary>
    public WordTemplateVersionDto? Version { get; set; }
}
