using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.IntegrationTests.Infrastructure;

/// <summary>Kịch bản không kiểm thử cổng hiệu chuẩn / kiểm soát tắt hai cổng này để đi thẳng tới quyết định.</summary>
public static class PeriodGates
{
    /// <summary>Cài đặt kỳ mặc định, mọi hồ sơ luồng tắt hai cổng; cho kỳ dựng thẳng vào CSDL.</summary>
    public static string DefaultSettingsGatesOff()
    {
        var settings = PeriodSettings.Default();
        foreach (var profile in settings.Profiles)
            TurnOff(profile);
        return settings.ToJson();
    }

    /// <summary>Tắt cổng (mặc định cả hai) của mọi hồ sơ luồng qua API; chỉ làm được khi kỳ còn dự thảo.</summary>
    public static async Task DisableAsync(HttpClient manager, Guid periodId, params string[] gates)
    {
        var get = await manager.GetAsync($"/api/evaluations/periods/{periodId}");
        var text = await get.Content.ReadAsStringAsync();
        Assert.True(get.IsSuccessStatusCode, text);
        using var json = JsonDocument.Parse(text);
        var data = json.RootElement.GetProperty("data");
        var settings = JsonNode.Parse(data.GetProperty("settings").GetRawText())!.AsObject();
        DisableIn(settings, gates);
        var put = await manager.PutAsJsonAsync($"/api/evaluations/periods/{periodId}",
            new { version = data.GetProperty("version").GetUInt32(), settings });
        Assert.True(put.IsSuccessStatusCode, await put.Content.ReadAsStringAsync());
    }

    /// <summary>Sửa trực tiếp nút cài đặt kỳ đã đọc sẵn.</summary>
    public static void DisableIn(JsonObject settings, params string[] only)
    {
        var targets = only.Length == 0 ? new[] { WorkflowGates.Calibration, WorkflowGates.Control } : only;
        foreach (var profile in settings["profiles"]!.AsArray())
        {
            var gates = profile!["gates"]?.AsObject() ?? new JsonObject();
            foreach (var gate in targets)
                gates[gate] = new JsonObject { ["mode"] = "Off" };
            profile.AsObject()["gates"] = gates;
        }
    }

    private static void TurnOff(WorkflowProfile profile)
    {
        profile.Gates[WorkflowGates.Calibration] = new GateSetting { Mode = GateMode.Off };
        profile.Gates[WorkflowGates.Control] = new GateSetting { Mode = GateMode.Off };
    }
}
