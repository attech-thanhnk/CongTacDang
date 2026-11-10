using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.IntegrationTests.Infrastructure;

/// <summary>Kịch bản không kiểm thử cổng biên bản họp / hiệu chuẩn / kiểm soát tắt các cổng này để đi thẳng tới quyết định.</summary>
public static class PeriodGates
{
    /// <summary>Cài đặt kỳ mặc định, mọi diện quyết định tắt hai cổng; cho kỳ dựng thẳng vào CSDL.</summary>
    public static string DefaultSettingsGatesOff()
    {
        var settings = PeriodSettings.Default();
        foreach (var profile in settings.Profiles)
            TurnOff(profile);
        return settings.ToJson();
    }

    /// <summary>Tắt cổng (mặc định cả hai) của mọi diện quyết định qua API; chỉ làm được khi kỳ còn dự thảo.</summary>
    public static Task DisableAsync(HttpClient manager, Guid periodId, params string[] gates) =>
        UpdateAsync(manager, periodId, settings => DisableIn(settings, gates));

    /// <summary>Đặt chế độ một cổng cho mọi diện quyết định qua API; chỉ làm được khi kỳ còn dự thảo.</summary>
    public static Task SetModeAsync(HttpClient manager, Guid periodId, string gate, GateMode mode) =>
        UpdateAsync(manager, periodId, settings =>
        {
            foreach (var profile in settings["profiles"]!.AsArray())
            {
                var gates = profile!["gates"]?.AsObject() ?? new JsonObject();
                gates[gate] = new JsonObject { ["mode"] = mode.ToString() };
                profile.AsObject()["gates"] = gates;
            }
        });

    private static async Task UpdateAsync(HttpClient manager, Guid periodId, Action<JsonObject> change)
    {
        var get = await manager.GetAsync($"/api/evaluations/periods/{periodId}");
        var text = await get.Content.ReadAsStringAsync();
        Assert.True(get.IsSuccessStatusCode, text);
        using var json = JsonDocument.Parse(text);
        var data = json.RootElement.GetProperty("data");
        var settings = JsonNode.Parse(data.GetProperty("settings").GetRawText())!.AsObject();
        change(settings);
        var put = await manager.PutAsJsonAsync($"/api/evaluations/periods/{periodId}",
            new { version = data.GetProperty("version").GetUInt32(), settings });
        Assert.True(put.IsSuccessStatusCode, await put.Content.ReadAsStringAsync());
    }

    /// <summary>Sửa trực tiếp nút cài đặt kỳ đã đọc sẵn.</summary>
    public static void DisableIn(JsonObject settings, params string[] only)
    {
        var targets = only.Length == 0 ? DefaultTargets : only;
        foreach (var profile in settings["profiles"]!.AsArray())
        {
            var gates = profile!["gates"]?.AsObject() ?? new JsonObject();
            foreach (var gate in targets)
                gates[gate] = new JsonObject { ["mode"] = "Off" };
            profile.AsObject()["gates"] = gates;
        }
    }

    /// <summary>Kịch bản không lập biên bản họp, hiệu chuẩn, kiểm soát thì tắt cả ba cổng.</summary>
    private static readonly string[] DefaultTargets = { WorkflowGates.Minutes, WorkflowGates.Calibration, WorkflowGates.Control };

    private static void TurnOff(WorkflowProfile profile)
    {
        foreach (var gate in DefaultTargets)
            profile.Gates[gate] = new GateSetting { Mode = GateMode.Off };
    }
}
