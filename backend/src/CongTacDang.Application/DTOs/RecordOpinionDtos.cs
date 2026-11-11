using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

public sealed class RecordOpinionDto
{
    public Guid Id { get; set; }

    public string Kind { get; set; } = string.Empty;

    public string KindName { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string RecordedByName { get; set; } = string.Empty;

    public DateTime RecordedAt { get; set; }
}

public sealed class RecordOpinionsDto
{
    public Guid RecordId { get; set; }

    /// <summary>Người xem được ghi ý kiến trên hồ sơ này.</summary>
    public bool CanRecord { get; set; }

    /// <summary>Loại ý kiến người xem chọn được, loại mặc định trước.</summary>
    public List<RecordOpinionKindDto> Kinds { get; set; } = new();

    public List<RecordOpinionDto> Items { get; set; } = new();
}

public sealed class RecordOpinionKindDto
{
    public string Value { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

public sealed class CreateRecordOpinionDto
{
    /// <summary><c>ListMeeting</c>, <c>ListCheck</c>, <c>DeputyOnList</c>, <c>DeputyOnReview</c>; <c>MemberExplanation</c> chỉ chủ hồ sơ ghi.</summary>
    public string? Kind { get; set; }

    public string? Content { get; set; }
}
