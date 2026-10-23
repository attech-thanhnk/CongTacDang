using CongTacDang.Domain.Evaluation;

namespace CongTacDang.IntegrationTests;

/// <summary>Điểm chấm theo bộ tiêu chí mặc định (17 tiêu chí con, 6 trục T1–T6).</summary>
internal static class CriteriaTestData
{
    /// <summary>Lý do dùng cho tiêu chí "Không đảm bảo" (khoản giảm ≥ 1 điểm phải nêu căn cứ).</summary>
    public const string DeductionReason = "Căn cứ giảm điểm (test)";

    /// <summary>Mọi tiêu chí con "Đảm bảo", trừ các mã <paramref name="notAssured"/> chấm "Không đảm bảo" (0, có căn cứ).</summary>
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

    /// <summary>Ảnh chụp bộ mặc định; <paramref name="collectiveFramework"/> = false bỏ khung tiêu chí tập thể.</summary>
    public static string Snapshot(string form, bool collectiveFramework = true) => new CriteriaSnapshot
    {
        SetId = Guid.Empty,
        Code = form == CriteriaSetContent.Form09B ? CriteriaSetDefaults.Code09B : CriteriaSetDefaults.Code09A,
        Name = form == CriteriaSetContent.Form09B ? CriteriaSetDefaults.Name09B : CriteriaSetDefaults.Name09A,
        SelfScoreForm = form,
        TakenAt = DateTime.UtcNow,
        Content = WithCollective(form == CriteriaSetContent.Form09B ? CriteriaSetDefaults.Build09B() : CriteriaSetDefaults.Build09A(), collectiveFramework)
    }.ToJson();

    private static CriteriaSetContent WithCollective(CriteriaSetContent content, bool keep)
    {
        if (!keep)
            content.CollectiveGroups.Clear();
        return content;
    }
}
