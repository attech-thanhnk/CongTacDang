using System.Text.Json;
using System.Text.Json.Nodes;

namespace CongTacDang.IntegrationTests;

/// <summary>Bộ 09A bắt buộc xếp mức mọi nhiệm vụ khi thẩm định: tự bổ sung <c>taskLevels</c> nếu thân yêu cầu chưa có.</summary>
internal static class AppraisalTestData
{
    /// <summary>Thêm <c>taskLevels</c> nếu chưa có; người gửi không đọc được hồ sơ (test 403) thì giữ nguyên.</summary>
    public static async Task AddTaskLevelsAsync(HttpClient client, Guid recordId, JsonObject body, int level = 1)
    {
        if (body.ContainsKey("taskLevels"))
            return;
        var response = await client.GetAsync($"/api/evaluations/records/{recordId}");
        if (!response.IsSuccessStatusCode)
            return;
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (!doc.RootElement.TryGetProperty("data", out var data) || !data.TryGetProperty("tasks", out var tasks))
            return;
        var levels = new JsonArray();
        foreach (var task in tasks.EnumerateArray())
            levels.Add(new JsonObject { ["taskId"] = task.GetProperty("id").GetGuid(), ["level"] = level });
        body["taskLevels"] = levels;
    }

    /// <summary>Thân yêu cầu thẩm định đã bổ sung mức nhiệm vụ.</summary>
    public static async Task<JsonObject> BodyAsync(HttpClient client, Guid recordId, object body)
    {
        var json = JsonSerializer.SerializeToNode(body)!.AsObject();
        await AddTaskLevelsAsync(client, recordId, json);
        return json;
    }
}
