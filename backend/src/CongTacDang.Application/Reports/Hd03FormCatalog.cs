using System;
using System.Collections.Generic;
using System.Linq;

namespace CongTacDang.Application.Reports;

/// <summary>Một mục/nhóm nội dung in sẵn của biểu mẫu HD03.</summary>
/// <param name="Code">Mã mục (khóa lưu dữ liệu).</param>
/// <param name="Title">Tiêu đề đúng nguyên văn biểu mẫu gốc.</param>
/// <param name="HasTaskLines">Mẫu 08: biểu mẫu gốc có dòng "- Nhiệm vụ 1: …" dưới nhóm nội dung.</param>
public sealed record Hd03FormSection(string Code, string Title, bool HasTaskLines = false);

/// <summary>
/// Chữ in sẵn của các biểu mẫu HD03 dùng chung cho nhập liệu, kiểm tra và xuất (đúng nguyên văn
/// <c>docs/2.03-HD.TVDU (HD DGXL CAN BO QUY III-2026) (Bieu mau).docx</c> và PDF HD03 tr.73–76).
/// </summary>
public static class Hd03FormCatalog
{
    /// <summary>Mẫu 07, mục A.I "Ưu điểm, kết quả đạt được" — 4 nội dung (mã I.1–I.4).</summary>
    public static IReadOnlyList<Hd03FormSection> Form07Strengths { get; } = new[]
    {
        new Hd03FormSection("I.1", "Việc chấp hành nguyên tắc tổ chức và hoạt động, nhất là nguyên tắc tập trung dân chủ; thực hiện quy chế làm việc."),
        new Hd03FormSection("I.2", "Kết quả thực hiện các mục tiêu, chỉ tiêu, nhiệm vụ được đề ra trong nghị quyết đại hội nhiệm kỳ, kế hoạch, chương trình công tác năm, được cấp có thẩm quyền giao, phê duyệt và các nhiệm vụ phát sinh, đột xuất được cấp có thẩm quyền giao."),
        new Hd03FormSection("I.3", "Công tác xây dựng, chỉnh đốn Đảng và hệ thống chính trị; xây dựng tổ chức bộ máy tinh gọn, hoạt động hiệu năng, hiệu lực, hiệu quả; tinh giản biên chế gắn với cải cách hành chính và chuyển đổi số; năng lực, trách nhiệm của tập thể lãnh đạo, quản lý; trách nhiệm nêu gương; trách nhiệm giải trình; công tác đấu tranh phòng, chống tham nhũng, tiêu cực, lãng phí và ngăn chặn, đẩy lùi những biểu hiện suy thoái về tư tưởng chính trị, đạo đức, lối sống, \"tự diễn biến\", \"tự chuyển hoá\" trong nội bộ gắn với việc học tập và làm theo tư tưởng, đạo đức, phong cách Hồ Chí Minh; công tác kiểm soát quyền lực, phòng, chống chạy chức, chạy quyền, lợi ích nhóm; xây dựng và thực hiện cơ chế công khai, minh bạch trong công tác cán bộ; công tác kiểm tra, giám sát, kỷ luật đảng và công tác tiếp công dân, giải quyết khiếu nại, tố cáo, kiến nghị, phản ánh của tổ chức, cá nhân."),
        new Hd03FormSection("I.4", "Trách nhiệm của tập thể lãnh đạo, quản lý trong triển khai thực hiện các chủ trương, đường lối, nghị quyết, văn bản chỉ đạo của Đảng, của cấp trên; thực hiện nhiệm vụ chính trị của tổ chức, cơ quan, đơn vị.")
    };

    /// <summary>Mẫu 08 — 13 nhóm nội dung công việc (cột 2 của bảng, mã 1–13).</summary>
    public static IReadOnlyList<Hd03FormSection> Form08Categories { get; } = new[]
    {
        new Hd03FormSection("1", "Thực hiện nhiệm vụ sản xuất, kinh doanh.", true),
        new Hd03FormSection("2", "Công tác xây dựng Đảng.", true),
        new Hd03FormSection("3", "Công tác An ninh-Quốc phòng, thực hiện trách nhiệm xã hội.", true),
        new Hd03FormSection("4", "Công tác đoàn thể, phong trào.", true),
        new Hd03FormSection("5", "Công tác khoa học công nghệ, đổi mới sáng tạo, chuyển đổi số.", true),
        new Hd03FormSection("6", "Công tác sắp xếp, tinh gọn bộ máy, nâng cao hiệu quả hoạt động cơ quan, đơn vị; hoàn thiện thể chế, văn bản quy phạm nội bộ, đẩy mạnh phân cấp, phân quyền gắn với kiểm tra, giám sát.", true),
        new Hd03FormSection("7", "Thực hiện chỉ tiêu tiết giảm chi phí, giải ngân; công tác quản lý dự án.", true),
        new Hd03FormSection("8", "Đảm bảo an toàn bay"),
        new Hd03FormSection("9", "Quản lý hợp đồng"),
        new Hd03FormSection("10", "Thực hiện nguyên tắc Tập trung-Dân chủ; ý thức trách nhiệm với công việc (tuân thủ nghiêm pháp luật, các quy chế quản lý nội bộ và sự lãnh đạo/chỉ đạo của cấp trên); đảm bảo tiến độ, chất lượng các báo cáo, yêu cầu của lãnh đạo cấp trên; xây dựng đoàn kết nội bộ."),
        new Hd03FormSection("11", "Công tác Kiểm tra-Giám sát, thi hành kỷ luật; công tác phòng chống tham nhũng, lãng phí, tiêu cực."),
        new Hd03FormSection("12", "Việc khắc phục các tồn tại, hạn chế đã được các cơ quan chức năng Nhà nước hoặc lãnh đạo cấp trên chỉ ra trong kỳ trước."),
        new Hd03FormSection("13", "Nhiệm vụ phát sinh, đột xuất theo Chỉ đạo, điều hành của cấp có thẩm quyền.")
    };

    /// <summary>Khoảng mã chức danh của Mẫu 15A (đối tượng đề nghị BTV Đảng ủy Tổng công ty quyết định).</summary>
    public const int Form15AFirstCode = 1, Form15ALastCode = 16;

    /// <summary>Khoảng mã chức danh của Mẫu 15B (đối tượng thuộc diện Đảng ủy/Chi ủy cơ sở quyết định).</summary>
    public const int Form15BFirstCode = 17, Form15BLastCode = 26;

    /// <summary>Cột 2 "Đối tượng đánh giá, xếp loại" của Mẫu 15A/15B theo mã chức danh (nguyên văn PDF HD03 tr.73–76).</summary>
    public static IReadOnlyDictionary<string, string> StatCodeSubjects { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["M1"] = "Bí thư Đảng ủy Tổng công ty.",
        ["M2"] = "Phó Bí thư Đảng ủy Tổng công ty.",
        ["M3"] = "Thành viên HĐTV.",
        ["M4"] = "Phó Tổng giám đốc.",
        ["M5"] = "Ủy viên Ban Thường vụ ĐUTCT.",
        ["M6"] = "Ủy viên Ban Chấp hành ĐBTCT.",
        ["M7"] = "Ủy viên UBKT ĐUTCT.",
        ["M8"] = "Bí thư các đảng bộ, chi bộ trực thuộc Đảng ủy Tổng công ty.",
        ["M9"] = "Phó Bí thư các đảng bộ, chi bộ trực thuộc Đảng ủy TCT.",
        ["M10"] = "Bí thư các chi bộ trực thuộc ĐUBP Văn phòng TCT.",
        ["M11"] = "Phó Bí thư các chi bộ trực thuộc ĐUBP Văn phòng TCT.",
        ["M12"] = "Trưởng, phó chuyên trách các cơ quan tham mưu, giúp việc Đảng ủy Tổng công ty.",
        ["M13"] = "Kế toán trưởng.",
        ["M14"] = "Phó Trưởng Ban, Phó Giám đốc.",
        ["M15"] = "Kiểm soát viên của Tổng công ty tại Công ty con",
        ["M16"] = "Bí thư Đoàn thanh niên TCT.",
        ["M17"] = "Ủy viên ban thường vụ đảng ủy cơ sở",
        ["M18"] = "Ủy viên BCH đảng bộ cơ sở",
        ["M19"] = "Ủy viên UBKT đảng ủy cơ sở",
        ["M20"] = "Bí thư các đảng bộ bộ phận trực thuộc đảng ủy cơ sở.",
        ["M21"] = "Phó Bí thư các đảng bộ bộ phận trực thuộc đảng ủy cơ sở.",
        ["M22"] = "Bí thư các chi bộ trực thuộc đảng ủy cơ sở.",
        ["M23"] = "Phó Bí thư các chi bộ trực thuộc đảng ủy cơ sở.",
        ["M24"] = "Bí thư các chi bộ trực thuộc các đảng ủy bộ phận.",
        ["M25"] = "Phó Bí thư các chi bộ trực thuộc các đảng ủy bộ phận.",
        ["M26"] = "Trưởng phòng, Phó Trưởng phòng (và tương đương)."
    };

    /// <summary>Dòng dành cho cán bộ chưa có chức vụ mang mã chức danh thống kê (không có trong biểu mẫu gốc).</summary>
    public const string NoStatCodeSubject = "Cán bộ chưa có mã chức danh thống kê";

    /// <summary>Tìm nhóm nội dung Mẫu 08 theo mã; null nếu không có.</summary>
    public static Hd03FormSection? FindForm08Category(string? code) =>
        Form08Categories.FirstOrDefault(c => string.Equals(c.Code, code?.Trim(), StringComparison.Ordinal));
}
