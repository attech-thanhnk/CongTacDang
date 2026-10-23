using System;
using System.Collections.Generic;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.DTOs;

public class CriteriaSetListItemDto
{
    public Guid Id { get; set; }
    /// <summary>Phiên bản xmin — gửi lại khi sửa/xuất bản/lưu trữ/xóa.</summary>
    public uint Version { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Draft / Published / Archived.</summary>
    public string Status { get; set; } = string.Empty;
    public string StatusDisplayName { get; set; } = string.Empty;
    /// <summary>09A / 09B.</summary>
    public string SelfScoreForm { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public Guid? SourceSetId { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int GeneralItemCount { get; set; }
    public int AxisCount { get; set; }
    public int WeightFrameCount { get; set; }
    /// <summary>Tên các kỳ đang chọn bộ này.</summary>
    public List<string> UsedByPeriods { get; set; } = new();
}

public class CriteriaSetDto : CriteriaSetListItemDto
{
    public CriteriaSetContent Content { get; set; } = new();
    /// <summary>Rỗng = xuất bản được.</summary>
    public List<string> ValidationErrors { get; set; } = new();
}

public class CreateCriteriaSetDto
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    /// <summary>09A / 09B.</summary>
    public string? SelfScoreForm { get; set; }
    public string? Notes { get; set; }
    /// <summary>Trống → nội dung mặc định theo mẫu tự chấm đã chọn.</summary>
    public CriteriaSetContent? Content { get; set; }
}

/// <summary>Sửa bộ tiêu chí nháp (trường null giữ nguyên).</summary>
public class UpdateCriteriaSetDto
{
    public uint? Version { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? SelfScoreForm { get; set; }
    public string? Notes { get; set; }
    public CriteriaSetContent? Content { get; set; }
}

public class CloneCriteriaSetDto
{
    /// <summary>Trống → mã gốc + hậu tố.</summary>
    public string? Code { get; set; }
    /// <summary>Trống → tên gốc + " (bản nháp)".</summary>
    public string? Name { get; set; }
}

/// <summary>Xuất bản / lưu trữ / xóa bộ tiêu chí.</summary>
public class CriteriaSetActionDto
{
    public uint? Version { get; set; }
}

/// <summary>Khung tỷ trọng chọn được cho cán bộ (lấy từ bộ tiêu chí đang dùng gần nhất).</summary>
public class WeightFrameOptionDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double A { get; set; }
    public double B { get; set; }
    public double C { get; set; }
    public double D { get; set; }
}

public class WeightFrameOptionsDto
{
    /// <summary>Kỳ gần nhất đã chọn bộ, hoặc bộ đã xuất bản mới nhất.</summary>
    public string? SourceName { get; set; }
    public List<WeightFrameOptionDto> Frames { get; set; } = new();
}
