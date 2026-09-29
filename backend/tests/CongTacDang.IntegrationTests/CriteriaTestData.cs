using CongTacDang.Domain.Evaluation;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Task 16: dữ liệu chấm điểm theo bộ tiêu chí mặc định (17 tiêu chí con, 6 trục T1–T6) dùng chung cho các test tích hợp
/// — thay mảng 6 điểm cố định trước đây.
/// </summary>
internal static class CriteriaTestData
{
    /// <summary>Lý do dùng cho tiêu chí "Không đảm bảo" (khoản giảm ≥ 1 điểm phải nêu căn cứ).</summary>
    public const string DeductionReason = "Căn cứ giảm điểm (test)";

    /// <summary>
    /// Điểm tiêu chí chung theo bộ mặc định: mọi tiêu chí con "Đảm bảo" (tối đa), trừ các mã trong <paramref name="notAssured"/>
    /// chấm "Không đảm bảo" (0, có căn cứ). Tổng = 30 − tổng điểm tối đa các mã đó.
    /// </summary>
    public static Dictionary<string, GeneralItemScore> General(params string[] notAssured) =>
        CriteriaSetDefaults.Build09B().AllItems.ToDictionary(
            x => x.Item.Code,
            x => notAssured.Contains(x.Item.Code)
                ? new GeneralItemScore { Score = 0, Reason = DeductionReason }
                : new GeneralItemScore { Score = x.Item.MaxScore });

    /// <summary>Điểm theo trục T1..T6 (Mẫu 09B).</summary>
    public static Dictionary<string, double> Axis(double t1, double t2, double t3, double t4, double t5, double t6) => new()
    {
        ["T1"] = t1, ["T2"] = t2, ["T3"] = t3, ["T4"] = t4, ["T5"] = t5, ["T6"] = t6
    };

    /// <summary>Ảnh chụp bộ mặc định (dựng kỳ trực tiếp trong CSDL cho test).</summary>
    public static string Snapshot(string form) => new CriteriaSnapshot
    {
        SetId = Guid.Empty,
        Code = form == CriteriaSetContent.Form09B ? CriteriaSetDefaults.Code09B : CriteriaSetDefaults.Code09A,
        Name = form == CriteriaSetContent.Form09B ? CriteriaSetDefaults.Name09B : CriteriaSetDefaults.Name09A,
        SelfScoreForm = form,
        TakenAt = DateTime.UtcNow,
        Content = form == CriteriaSetContent.Form09B ? CriteriaSetDefaults.Build09B() : CriteriaSetDefaults.Build09A()
    }.ToJson();
}
