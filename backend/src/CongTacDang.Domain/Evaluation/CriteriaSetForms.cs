using System;
using System.Collections.Generic;
using System.Linq;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Mã biểu mẫu cá nhân dùng trong <see cref="CriteriaSetContent.RequiredForms"/>.</summary>
public static class RecordFormCodes
{
    /// <summary>Phiếu giao / đăng ký sản phẩm.</summary>
    public const string Form01 = "01";

    /// <summary>Phiếu tự đánh giá kết quả thực hiện sản phẩm.</summary>
    public const string Form02 = "02";

    /// <summary>Phiếu tự chấm điểm theo sản phẩm Mẫu 01/02; luôn áp dụng.</summary>
    public const string Form09A = "09A";

    /// <summary>Bản tự đánh giá, xếp loại cá nhân (tự luận).</summary>
    public const string Form09C = "09C";

    /// <summary>Phụ lục kết quả thực hiện nhiệm vụ trong quý.</summary>
    public const string Form9D = "9D";

    /// <summary>Phiếu thẩm định, nhận xét, đề xuất xếp loại.</summary>
    public const string Form10 = "10";

    /// <summary>Phiếu đề nghị điều chỉnh, chuyển tiếp, loại trừ việc; xuất theo từng đề nghị, không nằm trong bộ tiêu chí.</summary>
    public const string Form04 = "04";

    /// <summary>Phiếu ghi nhận việc đột xuất; xuất theo từng đề nghị, không nằm trong bộ tiêu chí.</summary>
    public const string Form05 = "05";

    /// <summary>Mẫu xuất theo từng đề nghị thay đổi việc.</summary>
    public static IReadOnlyList<string> RequestForms { get; } = new[] { Form04, Form05 };

    /// <summary>Theo thứ tự hiển thị.</summary>
    public static IReadOnlyList<string> All { get; } = new[] { Form01, Form02, Form09A, Form09C, Form9D, Form10 };

    /// <summary>Bỏ khoảng trắng, tiền tố "MAU"/"MẪU"; "09D" = "9D". Mã lạ giữ nguyên để bộ kiểm tra báo lỗi.</summary>
    public static string Normalize(string? code)
    {
        var value = (code ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", string.Empty).Replace("_", string.Empty);
        foreach (var prefix in new[] { "MẪUSỐ", "MẪU", "MAUSO", "MAU" })
        {
            if (value.StartsWith(prefix, StringComparison.Ordinal))
            {
                value = value[prefix.Length..];
                break;
            }
        }
        if (value == "09D")
            value = Form9D;
        return All.Concat(RequestForms).FirstOrDefault(c => c == value) ?? (code ?? string.Empty).Trim();
    }

    public static bool IsKnown(string? code) => All.Contains(code);

    public static string DisplayName(string code) => code switch
    {
        Form01 => "Mẫu 01 — Phiếu giao / đăng ký sản phẩm, công việc chuyên môn",
        Form02 => "Mẫu 02 — Phiếu tự đánh giá kết quả thực hiện sản phẩm, công việc",
        Form04 => "Mẫu 04 — Phiếu đề nghị điều chỉnh, chuyển tiếp, loại trừ việc",
        Form05 => "Mẫu 05 — Phiếu ghi nhận nhiệm vụ bổ sung, khẩn cấp, đột xuất",
        Form09A => "Mẫu 09A — Phiếu tự chấm điểm",
        Form09C => "Mẫu 09C — Bản tự đánh giá, xếp loại của cá nhân",
        Form9D => "Mẫu 9D — Phụ lục kết quả thực hiện nhiệm vụ trong quý",
        Form10 => "Mẫu 10 — Phiếu thẩm định, nhận xét, đề xuất xếp loại",
        _ => "Mẫu " + code
    };
}

/// <summary>Mục tự luận của Mẫu 09C, khai báo trong bộ tiêu chí; nội dung hồ sơ lưu theo <see cref="Code"/>.</summary>
public sealed class SelfAssessmentSection
{
    /// <summary>Duy nhất trong bộ, ví dụ "I".</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gồm cả số thứ tự, ví dụ "I. Tự đánh giá …".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Câu dẫn in dưới tiêu đề.</summary>
    public string? Guidance { get; set; }

    /// <summary>Lưu ý in sau nội dung.</summary>
    public string? Note { get; set; }

    /// <summary>Số ký tự tối đa (mẫu gốc: không quá 02 trang A4).</summary>
    public int MaxLength { get; set; } = 6000;

    /// <summary>Bắt buộc khi nộp phiếu tự chấm.</summary>
    public bool Required { get; set; }
}

/// <summary>Mức tự đánh giá nhiệm vụ (Mẫu 09C/9D).</summary>
public sealed class TaskLevel
{
    /// <summary>Số nguyên dương, duy nhất trong bộ.</summary>
    public int Code { get; set; }

    public string Label { get; set; } = string.Empty;
}
