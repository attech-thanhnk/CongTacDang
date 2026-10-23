using CongTacDang.Domain.Enums;

namespace CongTacDang.IntegrationTests;

/// <summary>Cơ cấu Quý III/2026 (tên giả): 54 cán bộ, mã chức danh, điểm và mức đúng bảng kết quả đã nộp.
/// Mỗi người một mã (mã đứng trước khi kiêm nhiệm); 15A đếm M1–M16, 15B đếm M17–M26; tỷ lệ % = xuất sắc / (xuất sắc + tốt).</summary>
internal static class Quarter3Structure
{
    /// <summary>Chức vụ trong danh mục (dạng chung chung) — tên, mã thống kê, thẩm quyền mặc định, bên.</summary>
    internal sealed record PositionSpec(string Key, string Name, string? StatCode, ApprovalAuthority? Authority, PositionSide Side);

    /// <summary>Một cán bộ: chức vụ đang giữ (khóa <see cref="PositionSpec.Key"/>), diện, mã mong đợi, điểm và mức các bước.</summary>
    internal sealed record Cadre(
        string[] Positions,
        ApprovalAuthority Authority,
        string ExpectedCode,
        int GeneralScore,
        int TasksScore,
        EvaluationGrade Collective,
        EvaluationGrade Appraisal,
        EvaluationGrade Director,
        EvaluationGrade Final);

    public static readonly PositionSpec[] Positions =
    {
        new("UVBCH_TCT", "Ủy viên Ban Chấp hành Đảng bộ Tổng công ty", "M6", ApprovalAuthority.CapTren, PositionSide.Party),
        new("BT_DU", "Bí thư Đảng ủy", "M8", ApprovalAuthority.CapTren, PositionSide.Party),
        new("PBT_DU", "Phó Bí thư Đảng ủy", "M9", ApprovalAuthority.CapTren, PositionSide.Party),
        new("CT", "Chủ tịch Công ty", "M14", ApprovalAuthority.CapTren, PositionSide.Administrative),
        new("GD", "Giám đốc Công ty", "M14", ApprovalAuthority.CapTren, PositionSide.Administrative),
        new("PGD", "Phó Giám đốc Công ty", "M14", ApprovalAuthority.CapTren, PositionSide.Administrative),
        new("KSV", "Kiểm soát viên", "M15", ApprovalAuthority.CapTren, PositionSide.Administrative),
        new("UVBTV", "Ủy viên Ban Thường vụ Đảng ủy", "M17", ApprovalAuthority.CoSo, PositionSide.Party),
        new("DUV", "Đảng ủy viên", "M18", ApprovalAuthority.CoSo, PositionSide.Party),
        new("UBKT", "Ủy viên Ủy ban Kiểm tra Đảng ủy", "M19", ApprovalAuthority.CoSo, PositionSide.Party),
        new("PBT_DBBP", "Phó Bí thư Đảng bộ bộ phận", "M21", ApprovalAuthority.CoSo, PositionSide.Party),
        new("BTCB", "Bí thư Chi bộ", "M22", ApprovalAuthority.CoSo, PositionSide.Party),
        new("PBTCB", "Phó Bí thư Chi bộ", "M23", ApprovalAuthority.CoSo, PositionSide.Party),
        new("BTCB_BP", "Bí thư Chi bộ trực thuộc Đảng bộ bộ phận", "M24", ApprovalAuthority.CoSo, PositionSide.Party),
        new("PBTCB_BP", "Phó Bí thư Chi bộ trực thuộc Đảng bộ bộ phận", "M25", ApprovalAuthority.CoSo, PositionSide.Party),
        // Trưởng, phó phòng và tương đương (giám đốc, phó giám đốc đơn vị trực thuộc; đội, đài, trạm trưởng).
        new("TP", "Trưởng phòng (và tương đương)", "M26", ApprovalAuthority.CoSo, PositionSide.Administrative),
        new("PTP", "Phó Trưởng phòng (và tương đương)", "M26", ApprovalAuthority.CoSo, PositionSide.Administrative),
        // Chức vụ không có mã thống kê.
        new("CUV", "Chi ủy viên", null, null, PositionSide.Party),
        new("UVBCH_DBBP", "Ủy viên Ban Chấp hành Đảng bộ bộ phận", null, null, PositionSide.Party),
        new("CTCD", "Chủ tịch Công đoàn", null, null, PositionSide.MassOrganization)
    };

    private const EvaluationGrade Tot = EvaluationGrade.HoanThanhTot;
    private const EvaluationGrade None = EvaluationGrade.ChuaXepLoai;

    /// <summary>Diện Ban Thường vụ cấp trên: cấp trên chưa quyết định; thẩm định để trống (Mẫu 14 cột 9 trống).</summary>
    private static Cadre Superior(string code, int general, int tasks, bool directorProposes, params string[] positions) =>
        new(positions, ApprovalAuthority.CapTren, code, general, tasks, Tot, None, directorProposes ? Tot : None, None);

    /// <summary>Diện Đảng ủy cơ sở: các bước đề xuất và Đảng ủy quyết định đều "Hoàn thành tốt nhiệm vụ".</summary>
    private static Cadre Base(string code, int general, int tasks, params string[] positions) =>
        new(positions, ApprovalAuthority.CoSo, code, general, tasks, Tot, Tot, Tot, Tot);

    /// <summary>Danh sách theo đúng thứ tự bảng kết quả Quý III/2026.</summary>
    public static readonly Cadre[] Cadres =
    {
        // Diện Ban Thường vụ Đảng ủy cấp trên (Mẫu 14, 15A, Mẫu 16 mục II).
        Superior("M6", 30, 58, false, "UVBCH_TCT", "BT_DU", "CT"),
        Superior("M9", 30, 58, false, "PBT_DU", "GD"),
        Superior("M15", 30, 57, false, "KSV"),
        Superior("M14", 30, 59, true, "UVBTV", "UBKT", "PGD", "CTCD"),
        Superior("M14", 30, 53, true, "DUV", "PGD"),
        Superior("M14", 30, 59, true, "DUV", "PGD"),

        // Diện Đảng ủy cơ sở (Mẫu 15B, Mẫu 16 mục I).
        Base("M18", 30, 58, "DUV", "BTCB", "TP"),
        Base("M18", 30, 58, "DUV", "BTCB", "TP"),
        Base("M18", 30, 58, "DUV", "BTCB", "TP"),
        Base("M18", 30, 55, "DUV", "BTCB"),
        Base("M18", 30, 53, "DUV", "BTCB", "TP"),
        Base("M18", 28, 57, "DUV", "BTCB", "TP"),
        Base("M18", 30, 54, "DUV", "BTCB", "TP"),
        Base("M18", 30, 58, "DUV", "BTCB", "TP"),
        Base("M22", 30, 55, "BTCB", "TP"),
        Base("M22", 29, 58, "BTCB", "TP"),
        Base("M19", 30, 58, "UBKT", "PBTCB", "PTP"),
        Base("M19", 30, 56, "UBKT", "PBTCB", "PTP"),
        Base("M23", 30, 59, "PBTCB", "PTP"),
        Base("M19", 30, 54, "UBKT", "PTP"),
        Base("M23", 30, 54, "PBTCB", "PTP"),
        Base("M23", 30, 53, "PBTCB", "PTP"),
        Base("M26", 30, 54, "CUV", "PTP"),
        Base("M26", 30, 50, "PTP"),
        Base("M26", 30, 55, "PTP"),
        Base("M23", 28, 56, "PBTCB", "PTP"),
        Base("M23", 28, 58, "PBTCB", "PTP"),
        Base("M26", 28, 58, "PTP"),
        Base("M23", 30, 56, "PBTCB", "PTP"),
        Base("M26", 30, 57, "PTP"),
        Base("M23", 27, 55, "PBTCB", "PTP"),
        Base("M23", 30, 58, "PBTCB", "PTP"),
        Base("M26", 30, 58, "CUV", "PTP"),
        Base("M19", 30, 58, "UBKT", "PBT_DBBP", "PTP"),
        Base("M26", 30, 58, "UVBCH_DBBP", "PTP"),
        Base("M26", 30, 58, "UVBCH_DBBP", "PTP"),
        Base("M24", 30, 59, "BTCB_BP", "TP"),
        Base("M25", 30, 57, "PBTCB_BP", "TP"),
        Base("M24", 30, 56, "BTCB_BP", "TP"),
        Base("M25", 30, 56, "UVBCH_DBBP", "PBTCB_BP", "TP"),
        Base("M24", 30, 59, "BTCB_BP", "TP"),
        Base("M24", 30, 56, "BTCB_BP", "TP"),
        Base("M24", 30, 56, "BTCB_BP", "TP"),
        Base("M25", 30, 53, "PBTCB_BP", "TP"),
        Base("M24", 30, 55, "BTCB_BP", "TP"),
        Base("M25", 30, 54, "PBTCB_BP", "TP"),
        Base("M24", 30, 56, "BTCB_BP", "TP"),
        Base("M25", 30, 56, "PBTCB_BP", "TP"),
        Base("M24", 30, 55, "BTCB_BP", "TP"),
        Base("M24", 30, 56, "BTCB_BP", "TP"),
        Base("M25", 30, 55, "PBTCB_BP", "TP"),
        Base("M24", 30, 55, "BTCB_BP", "TP"),
        Base("M24", 30, 58, "BTCB_BP", "TP"),
        Base("M25", 30, 58, "PBTCB_BP", "TP")
    };

    /// <summary>
    /// Số liệu bảng kết quả đã nộp (chép từ Mẫu 15A và mục II Mẫu 16): mã → tổng số, xuất sắc, tốt, hoàn thành, không hoàn
    /// thành, chưa xếp loại. Mã không có trong bảng: mọi cột bằng 0.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int[]> Form15A = new Dictionary<string, int[]>
    {
        ["M6"] = new[] { 1, 0, 1, 0, 0, 0 },
        ["M9"] = new[] { 1, 0, 1, 0, 0, 0 },
        ["M14"] = new[] { 3, 0, 3, 0, 0, 0 },
        ["M15"] = new[] { 1, 0, 1, 0, 0, 0 }
    };

    /// <summary>Số liệu bảng kết quả đã nộp (Mẫu 15B và mục I Mẫu 16).</summary>
    public static readonly IReadOnlyDictionary<string, int[]> Form15B = new Dictionary<string, int[]>
    {
        ["M18"] = new[] { 8, 0, 8, 0, 0, 0 },
        ["M19"] = new[] { 4, 0, 4, 0, 0, 0 },
        ["M22"] = new[] { 2, 0, 2, 0, 0, 0 },
        ["M23"] = new[] { 8, 0, 8, 0, 0, 0 },
        ["M24"] = new[] { 11, 0, 11, 0, 0, 0 },
        ["M25"] = new[] { 7, 0, 7, 0, 0, 0 },
        ["M26"] = new[] { 8, 0, 8, 0, 0, 0 }
    };
}
