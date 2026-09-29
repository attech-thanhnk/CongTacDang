using System;
using System.Collections.Generic;
using System.Linq;

namespace CongTacDang.Domain.Evaluation;

/// <summary>
/// Danh mục biểu mẫu gắn với một hồ sơ đánh giá cá nhân (HD 03-HD/TVĐU mục 7) — mã dùng trong cờ
/// <see cref="CriteriaSetContent.RequiredForms"/> của bộ tiêu chí (task 18 — T-82).
/// </summary>
public static class RecordFormCodes
{
    /// <summary>Mẫu 01 — Phiếu giao / đăng ký sản phẩm.</summary>
    public const string Form01 = "01";

    /// <summary>Mẫu 02 — Phiếu tự đánh giá kết quả thực hiện sản phẩm.</summary>
    public const string Form02 = "02";

    /// <summary>Mẫu 09A — Phiếu tự chấm (chấm theo nhiệm vụ Mẫu 01/02).</summary>
    public const string Form09A = CriteriaSetContent.Form09A;

    /// <summary>Mẫu 09B — Phiếu tự chấm trực tiếp theo trục (Quý III/2026).</summary>
    public const string Form09B = CriteriaSetContent.Form09B;

    /// <summary>Mẫu 09C — Bản tự đánh giá, xếp loại của cá nhân (tự luận, tối đa 02 trang A4).</summary>
    public const string Form09C = "09C";

    /// <summary>Mẫu 9D — Phụ lục kết quả thực hiện nhiệm vụ, công việc được giao trong quý (theo trục).</summary>
    public const string Form9D = "9D";

    /// <summary>Mẫu 10 — Phiếu thẩm định, nhận xét, đề xuất xếp loại.</summary>
    public const string Form10 = "10";

    /// <summary>Mọi mã biểu mẫu theo thứ tự hiển thị.</summary>
    public static IReadOnlyList<string> All { get; } = new[] { Form01, Form02, Form09A, Form09B, Form09C, Form9D, Form10 };

    /// <summary>
    /// Chuẩn hóa mã: bỏ khoảng trắng, chữ hoa, bỏ tiền tố "MAU"/"MẪU"; "09D" (cách ghi ở Mẫu 18) = "9D". Mã không có trong
    /// danh mục → giữ nguyên (đã cắt khoảng trắng) để bộ kiểm tra báo lỗi.
    /// </summary>
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
        return All.FirstOrDefault(c => c == value) ?? (code ?? string.Empty).Trim();
    }

    /// <summary>Mã có trong danh mục.</summary>
    public static bool IsKnown(string? code) => All.Contains(code);

    /// <summary>Tên hiển thị của biểu mẫu.</summary>
    public static string DisplayName(string code) => code switch
    {
        Form01 => "Mẫu 01 — Phiếu giao / đăng ký sản phẩm, công việc chuyên môn",
        Form02 => "Mẫu 02 — Phiếu tự đánh giá kết quả thực hiện sản phẩm, công việc",
        Form09A => "Mẫu 09A — Phiếu tự chấm điểm (theo sản phẩm Mẫu 01/02)",
        Form09B => "Mẫu 09B — Phiếu tự chấm điểm (theo trục)",
        Form09C => "Mẫu 09C — Bản tự đánh giá, xếp loại của cá nhân",
        Form9D => "Mẫu 9D — Phụ lục kết quả thực hiện nhiệm vụ trong quý",
        Form10 => "Mẫu 10 — Phiếu thẩm định, nhận xét, đề xuất xếp loại",
        _ => "Mẫu " + code
    };
}

/// <summary>
/// Một mục tự luận của Mẫu 09C (khai báo trong bộ tiêu chí; nội dung hồ sơ lưu theo <see cref="Code"/>). Mẫu gốc Quý III/2026
/// có một mục "I. Tự đánh giá kết quả thực hiện chức trách, nhiệm vụ được giao"; phần "II. Tự đề xuất xếp loại" lấy từ điểm và
/// mức tự đề xuất đã lưu trên hồ sơ.
/// </summary>
public sealed class SelfAssessmentSection
{
    /// <summary>Mã mục (duy nhất trong bộ, ví dụ "I").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tiêu đề in trên biểu mẫu (gồm cả số thứ tự, ví dụ "I. Tự đánh giá …").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Câu dẫn in dưới tiêu đề (tùy chọn).</summary>
    public string? Guidance { get; set; }

    /// <summary>Lưu ý in sau nội dung (tùy chọn).</summary>
    public string? Note { get; set; }

    /// <summary>Số ký tự tối đa của nội dung (Mẫu 09C gốc: "không quá 02 trang A4").</summary>
    public int MaxLength { get; set; } = 6000;

    /// <summary>Bắt buộc nhập khi nộp phiếu tự chấm.</summary>
    public bool Required { get; set; }
}
