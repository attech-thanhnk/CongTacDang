using System.Text.Json;

namespace CongTacDang.IntegrationTests;

/// <summary>Bộ tiêu chí dùng khi tạo kỳ qua API trong test (tạo kỳ bắt buộc chọn bộ đã xuất bản).</summary>
internal static class TestCriteriaSets
{
    /// <summary>Id bộ đã xuất bản mới nhất có mẫu tự chấm <paramref name="form"/> (09A / 09B) — tài khoản cần criteria.manage hoặc period.manage.</summary>
    public static async Task<Guid> LatestPublishedAsync(HttpClient client, string form)
    {
        var response = await client.GetAsync("/api/criteria-sets");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").EnumerateArray()
            .Where(s => s.GetProperty("status").GetString() == "Published" && s.GetProperty("selfScoreForm").GetString() == form)
            .OrderByDescending(s => s.GetProperty("publishedAt").GetDateTime())
            .Select(s => s.GetProperty("id").GetGuid())
            .First();
    }
}
