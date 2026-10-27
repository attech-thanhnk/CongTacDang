using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CongTacDang.IntegrationTests.Infrastructure;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>GET /api/version công khai, chỉ gồm commit, thời điểm dựng và môi trường.</summary>
[Collection(ApiCollection.Name)]
public sealed class VersionEndpointTests
{
    private readonly ApiFactory _factory;

    public VersionEndpointTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task Version_AnonymousGet_ReturnsOnlyPublicFields()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("commit").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("buildTime").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("environment").GetString()));
        Assert.Equal(3, data.EnumerateObject().Count());
    }
}
