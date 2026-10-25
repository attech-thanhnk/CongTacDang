using System;

namespace CongTacDang.Domain.Entities;

/// <summary>Phiếu/biên bản có lịch sử chung.</summary>
public enum EvaluationFormType
{
    /// <summary>Phiếu đối soát chuyên ngành.</summary>
    M03 = 3,

    /// <summary>Phiếu kiểm soát.</summary>
    M19 = 19,

    /// <summary>Biên bản hiệu chuẩn.</summary>
    M20 = 20
}

public enum EvaluationFormAction
{
    Create = 1,
    Submit = 2,
    Confirm = 3,

    /// <summary>Bắt buộc lý do.</summary>
    Return = 4,

    Conclude = 5,

    /// <summary>Bắt buộc lý do.</summary>
    Reopen = 6,

    /// <summary>Bắt buộc lý do.</summary>
    Cancel = 7,

    /// <summary>Áp kết quả hiệu chuẩn vào hồ sơ.</summary>
    Apply = 8
}

/// <summary>Lịch sử thao tác trên phiếu Mẫu 03, 19 và biên bản Mẫu 20; chỉ thêm, không sửa.</summary>
public class EvaluationFormHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public EvaluationFormType FormType { get; set; }

    /// <summary>Tham chiếu mềm tới phiếu theo <see cref="FormType"/> (không FK vì ba bảng khác nhau).</summary>
    public Guid FormId { get; set; }

    public EvaluationFormAction Action { get; set; }

    public Guid? ByUserId { get; set; }

    public string ByUserName { get; set; } = string.Empty;

    public DateTime At { get; set; } = DateTime.UtcNow;

    public string? Reason { get; set; }

    /// <summary>jsonb: nội dung trước khi mở lại hoặc trả lại, null nếu thao tác không cần.</summary>
    public string? Snapshot { get; set; }
}
