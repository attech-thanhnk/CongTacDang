using CongTacDang.Api.Controllers;
using CongTacDang.Application.Common.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CongTacDang.UnitTests;

public class VersionControllerTests
{
    private static AppVersionDto Call(Dictionary<string, string?> values)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var env = new TestEnv { EnvironmentName = "Production" };
        var result = Assert.IsType<OkObjectResult>(new VersionController(config, env).Get());
        return Assert.IsType<ApiResponse<AppVersionDto>>(result.Value).Data!;
    }

    [Fact]
    public void ReturnsCommitAndBuildTime_WhenProvided()
    {
        var dto = Call(new() { ["APP_VERSION"] = " abc1234 ", ["APP_BUILD_TIME"] = "2026-10-27T01:02:03Z" });
        Assert.Equal("abc1234", dto.Commit);
        Assert.Equal("2026-10-27T01:02:03Z", dto.BuildTime);
        Assert.Equal("Production", dto.Environment);
    }

    [Fact]
    public void ReturnsUnknown_WhenMissingOrBlank()
    {
        var dto = Call(new() { ["APP_VERSION"] = "  " });
        Assert.Equal("chưa rõ", dto.Commit);
        Assert.Equal("chưa rõ", dto.BuildTime);
    }

    private sealed class TestEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "";
        public string ApplicationName { get; set; } = "";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
