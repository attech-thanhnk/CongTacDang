using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CongTacDang.Api.Controllers;

/// <summary>
/// Thông tin đơn vị (task 17 — T-80). Xem: mọi người đã đăng nhập (phần tên hệ thống/công ty xem được trước khi đăng nhập
/// để hiện trên trang đăng nhập). Sửa: <c>system.settings.manage</c>.
/// </summary>
[ApiController]
[Route("api/settings/organization")]
public class SettingsController : ControllerBase
{
    private readonly IOrganizationSettingsService _settings;

    /// <summary>Khởi tạo controller.</summary>
    public SettingsController(IOrganizationSettingsService settings) => _settings = settings;

    /// <summary>Thông tin đơn vị hiện hành.</summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var settings = await _settings.GetAsync(ct);
        return Ok(ApiResponse<OrganizationSettingsDto>.Ok(settings, "Lấy thông tin đơn vị thành công."));
    }

    /// <summary>Tên hệ thống và tên công ty (hiển thị trên trang đăng nhập; không cần đăng nhập).</summary>
    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublic(CancellationToken ct)
    {
        var info = await _settings.GetPublicAsync(ct);
        return Ok(ApiResponse<PublicOrganizationInfoDto>.Ok(info));
    }

    /// <summary>Sửa thông tin đơn vị.</summary>
    [HttpPut]
    [Authorize]
    [RequirePermission(PermissionCodes.SystemSettingsManage)]
    public async Task<IActionResult> Update([FromBody] UpdateOrganizationSettingsDto request, CancellationToken ct)
    {
        var settings = await _settings.UpdateAsync(request, ct);
        return Ok(ApiResponse<OrganizationSettingsDto>.Ok(settings, "Đã cập nhật thông tin đơn vị."));
    }
}

/// <summary>
/// Quản lý file mẫu Word của biểu mẫu (task 17 — T-81): danh mục, lịch sử, kiểm tra, tải lên, kích hoạt, tải xuống.
/// Mọi thao tác cần <c>system.templates.manage</c>.
/// </summary>
[ApiController]
[Route("api/templates")]
[Authorize]
[RequirePermission(PermissionCodes.SystemTemplatesManage)]
public class WordTemplateController : ControllerBase
{
    /// <summary>Giới hạn request tải lên (file mẫu tối đa 10 MB + phần form).</summary>
    private const long UploadRequestLimit = 11 * 1024 * 1024;

    private readonly IWordTemplateService _templates;

    /// <summary>Khởi tạo controller.</summary>
    public WordTemplateController(IWordTemplateService templates) => _templates = templates;

    /// <summary>Danh mục biểu mẫu kèm phiên bản đang dùng và danh mục tag.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _templates.GetTemplatesAsync(ct);
        return Ok(ApiResponse<List<WordTemplateSummaryDto>>.Ok(items, "Lấy danh mục biểu mẫu thành công."));
    }

    /// <summary>Lịch sử phiên bản của biểu mẫu.</summary>
    [HttpGet("{code}/versions")]
    public async Task<IActionResult> Versions(string code, CancellationToken ct)
    {
        var items = await _templates.GetVersionsAsync(code, ct);
        return Ok(ApiResponse<List<WordTemplateVersionDto>>.Ok(items, "Lấy lịch sử phiên bản thành công."));
    }

    /// <summary>Kiểm tra một file mẫu (không lưu): tag có trong tệp, tag lạ, tag bắt buộc thiếu, sinh thử.</summary>
    [HttpPost("{code}/check")]
    [RequestSizeLimit(UploadRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadRequestLimit)]
    public async Task<IActionResult> Check(string code, IFormFile? file, CancellationToken ct)
    {
        if (file == null)
            return BadRequest(ApiResponse.Fail("Chưa chọn tệp. Hãy chọn file mẫu Word (.docx)."));

        await using var stream = file.OpenReadStream();
        var result = await _templates.CheckAsync(code, stream, file.FileName, file.Length, ct);
        return Ok(ApiResponse<WordTemplateCheckDto>.Ok(result, CheckMessage(result)));
    }

    /// <summary>
    /// Tải lên phiên bản mới (mặc định kích hoạt ngay). Tệp có lỗi (tag lạ, không mở được, sinh thử thất bại) → 400 kèm kết quả
    /// kiểm tra, không lưu. Tag bắt buộc thiếu chỉ là cảnh báo.
    /// </summary>
    [HttpPost("{code}/versions")]
    [RequestSizeLimit(UploadRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadRequestLimit)]
    public async Task<IActionResult> Upload(string code, IFormFile? file, [FromForm] string? note, [FromForm] bool activate = true, CancellationToken ct = default)
    {
        if (file == null)
            return BadRequest(ApiResponse.Fail("Chưa chọn tệp. Hãy chọn file mẫu Word (.docx)."));

        await using var stream = file.OpenReadStream();
        var result = await _templates.UploadAsync(code, stream, file.FileName, file.Length, note, activate, ct);
        if (!result.IsValid)
        {
            return BadRequest(new ApiResponse<WordTemplateCheckDto>
            {
                Success = false,
                Data = result,
                Message = "File mẫu không hợp lệ nên chưa được lưu. Hãy sửa các lỗi rồi tải lên lại.",
                Errors = result.Errors
            });
        }

        var message = result.Version?.IsActive == true
            ? $"Đã tải lên và kích hoạt phiên bản {result.Version.VersionNumber}."
            : $"Đã tải lên phiên bản {result.Version?.VersionNumber} (chưa kích hoạt).";
        if (result.Warnings.Count > 0)
            message += $" Có {result.Warnings.Count} cảnh báo — hãy xem lại.";
        return Ok(ApiResponse<WordTemplateCheckDto>.Ok(result, message));
    }

    /// <summary>Kích hoạt một phiên bản (kể cả phiên bản cũ).</summary>
    [HttpPost("{code}/versions/{versionId:guid}/activate")]
    public async Task<IActionResult> Activate(string code, Guid versionId, CancellationToken ct)
    {
        var version = await _templates.ActivateAsync(code, versionId, ct);
        return Ok(ApiResponse<WordTemplateVersionDto>.Ok(version, $"Đã kích hoạt phiên bản {version.VersionNumber}."));
    }

    /// <summary>Quay về file mẫu gốc đi kèm ứng dụng.</summary>
    [HttpPost("{code}/use-original")]
    public async Task<IActionResult> UseOriginal(string code, CancellationToken ct)
    {
        await _templates.UseOriginalAsync(code, ct);
        return Ok(ApiResponse.Ok("Đã chuyển về file mẫu gốc."));
    }

    /// <summary>Tải file đang dùng.</summary>
    [HttpGet("{code}/current")]
    public async Task<IActionResult> DownloadCurrent(string code, CancellationToken ct)
    {
        var file = await _templates.DownloadCurrentAsync(code, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Tải file mẫu gốc.</summary>
    [HttpGet("{code}/original")]
    public async Task<IActionResult> DownloadOriginal(string code, CancellationToken ct)
    {
        var file = await _templates.DownloadOriginalAsync(code, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Tải một phiên bản đã tải lên.</summary>
    [HttpGet("{code}/versions/{versionId:guid}/file")]
    public async Task<IActionResult> DownloadVersion(string code, Guid versionId, CancellationToken ct)
    {
        var file = await _templates.DownloadVersionAsync(code, versionId, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    private static string CheckMessage(WordTemplateCheckDto result) =>
        !result.IsValid
            ? $"File mẫu có {result.Errors.Count} lỗi — không tải lên được."
            : result.Warnings.Count > 0
                ? $"File mẫu hợp lệ, có {result.Warnings.Count} cảnh báo."
                : "File mẫu hợp lệ.";
}
