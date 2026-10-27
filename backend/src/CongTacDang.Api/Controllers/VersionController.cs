using CongTacDang.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CongTacDang.Api.Controllers;

/// <summary>Phiên bản đang chạy của backend, gồm mã commit và thời điểm dựng ảnh.</summary>
public sealed record AppVersionDto(string Commit, string BuildTime, string Environment);

/// <summary>Phiên bản ứng dụng cho người vận hành; không cần đăng nhập và không chứa bí mật.</summary>
[ApiController]
[Route("api/version")]
public class VersionController : ControllerBase
{
    private const string Unknown = "chưa rõ";

    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public VersionController(IConfiguration configuration, IHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get()
    {
        var dto = new AppVersionDto(
            ReadOrUnknown("APP_VERSION"),
            ReadOrUnknown("APP_BUILD_TIME"),
            _environment.EnvironmentName);
        return Ok(ApiResponse<AppVersionDto>.Ok(dto));
    }

    // Biến trống hoặc chỉ có khoảng trắng coi như chưa truyền lúc dựng ảnh.
    private string ReadOrUnknown(string key)
    {
        var value = _configuration[key];
        return string.IsNullOrWhiteSpace(value) ? Unknown : value.Trim();
    }
}
