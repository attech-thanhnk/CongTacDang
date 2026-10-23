using System.Text.Json;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.IntegrationTests;

/// <summary>Điểm chấm theo bộ tiêu chí mặc định (17 tiêu chí con, chấm theo sản phẩm đăng ký).</summary>
internal static class CriteriaTestData
{
    /// <summary>Lý do dùng cho tiêu chí "Không đảm bảo" (khoản giảm ≥ 1 điểm phải nêu căn cứ).</summary>
    public const string DeductionReason = "Căn cứ giảm điểm (test)";

    /// <summary>Mọi tiêu chí con "Đảm bảo", trừ các mã <paramref name="notAssured"/> chấm "Không đảm bảo" (0, có căn cứ).</summary>
    public static Dictionary<string, GeneralItemScore> General(params string[] notAssured) =>
        CriteriaSetDefaults.Build().AllItems.ToDictionary(
            x => x.Item.Code,
            x => notAssured.Contains(x.Item.Code)
                ? new GeneralItemScore { Score = 0, Reason = DeductionReason }
                : new GeneralItemScore { Score = x.Item.MaxScore });

    /// <summary>Danh mục Mẫu 01 hợp lệ: ba sản phẩm 30/20/20 = 70 điểm.</summary>
    public static object Tasks(string axisCode = "T1") => new
    {
        tasks = new[]
        {
            new { taskName = "Sản phẩm 1", targetOutput = "Kết quả 1", weight = 30.0, axisCode, kind = "TrongTam", role = "ChuTri", exceedCriteria = "Vượt tiến độ", evidenceSource = "Biên bản nghiệm thu", confirmer = "Giám đốc" },
            new { taskName = "Sản phẩm 2", targetOutput = "Kết quả 2", weight = 20.0, axisCode, kind = "TrongTam", role = "ChuTri", exceedCriteria = "Vượt tiến độ", evidenceSource = "Biên bản nghiệm thu", confirmer = "Giám đốc" },
            new { taskName = "Sản phẩm 3", targetOutput = "Kết quả 3", weight = 20.0, axisCode, kind = "ThuongXuyen", role = "PhoiHop", exceedCriteria = "Vượt tiến độ", evidenceSource = "Biên bản nghiệm thu", confirmer = "Giám đốc" }
        }
    };

    /// <summary>Cùng một tỷ lệ cho cả A-B-C-D của mọi sản phẩm (DTO hồ sơ từ API): điểm nhiệm vụ = 70 × tỷ lệ, không phụ thuộc khung.</summary>
    public static object[] TaskScores(JsonElement record, double ratio = 1.0) => record.GetProperty("tasks").EnumerateArray()
        .Select(t => (object)new
        {
            taskId = t.GetProperty("id").GetGuid(), criteriaA_Ratio = ratio, criteriaB_Ratio = ratio, criteriaC_Ratio = ratio, criteriaD_Ratio = ratio,
            isExceedStandard = false
        })
        .ToArray();

    /// <summary>Ảnh chụp bộ mặc định; <paramref name="collectiveFramework"/> = false bỏ khung tiêu chí tập thể.</summary>
    public static string Snapshot(bool collectiveFramework = true) => new CriteriaSnapshot
    {
        SetId = Guid.Empty,
        Code = CriteriaSetDefaults.Code,
        Name = CriteriaSetDefaults.Name,
        TakenAt = DateTime.UtcNow,
        Content = WithCollective(CriteriaSetDefaults.Build(), collectiveFramework)
    }.ToJson();

    private static CriteriaSetContent WithCollective(CriteriaSetContent content, bool keep)
    {
        if (!keep)
            content.CollectiveGroups.Clear();
        return content;
    }
}
