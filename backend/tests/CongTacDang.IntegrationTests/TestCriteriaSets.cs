using System.Text.Json;

namespace CongTacDang.IntegrationTests;

/// <summary>Bộ tiêu chí dùng khi tạo kỳ qua API trong test (tạo kỳ bắt buộc chọn bộ đã xuất bản).</summary>
internal static class TestCriteriaSets
{
    /// <summary>Id bộ đã xuất bản mới nhất — tài khoản cần criteria.manage hoặc period.manage.</summary>
    public static async Task<Guid> LatestPublishedAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/criteria-sets");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").EnumerateArray()
            .Where(s => s.GetProperty("status").GetString() == "Published")
            .OrderByDescending(s => s.GetProperty("publishedAt").GetDateTime())
            .Select(s => s.GetProperty("id").GetGuid())
            .First();
    }
}
