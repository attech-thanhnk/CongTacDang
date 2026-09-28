using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;

namespace CongTacDang.Api.Controllers;

/// <summary>Quản lý tệp đính kèm và tài liệu minh chứng</summary>
[ApiController]
[Route("api/attachments")]
[Authorize] // Tất cả endpoint yêu cầu đăng nhập
public class AttachmentController : ControllerBase
{
    private readonly IAttachmentService _attachmentService;

    public AttachmentController(IAttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    /// <summary>Lấy Id người dùng hiện tại từ JWT.</summary>
    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
            throw new UnauthorizedAccessException("Không xác thực được danh tính người dùng hiện tại.");
        return userId;
    }

    /// <summary>Danh sách tệp tin và tài liệu minh chứng mà người dùng được xem</summary>
    [HttpGet("list")]
    [Authorize(Policy = AppPermissions.AttachmentsRead)]
    public async Task<IActionResult> GetList()
    {
        var files = await _attachmentService.GetAttachmentsAsync(GetCurrentUserId());
        return Ok(ApiResponse<List<AttachmentDto>>.Ok(files, "Lấy danh mục tệp tin thành công."));
    }

    /// <summary>Danh sách tệp (phiên bản hiện hành) của một đối tượng: <c>ownerType</c> = General | EvaluationRecord | EvaluationTask</summary>
    [HttpGet]
    [Authorize(Policy = AppPermissions.AttachmentsRead)]
    public async Task<IActionResult> GetByOwner([FromQuery] string ownerType, [FromQuery] Guid ownerId)
    {
        var files = await _attachmentService.GetAttachmentsByOwnerAsync(ownerType, ownerId, GetCurrentUserId());
        return Ok(ApiResponse<List<AttachmentDto>>.Ok(files, "Lấy danh sách tệp của đối tượng thành công."));
    }

    /// <summary>
    /// Tải lên tệp minh chứng (PDF, DOCX, XLSX, Ảnh). Truyền <c>ownerType</c>/<c>ownerId</c> để gắn tệp vào đối tượng
    /// (cần quyền cập nhật hồ sơ liên quan); một đối tượng có thể có nhiều tệp.
    /// </summary>
    [HttpPost("upload")]
    [Authorize(Policy = AppPermissions.AttachmentsUpload)]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> UploadFile(
        [FromForm] IFormFile file,
        [FromForm] string formCode = "GENERAL",
        [FromForm] string description = "",
        [FromForm] string? ownerType = null,
        [FromForm] Guid? ownerId = null)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("Tệp đính kèm không được để trống."));

        using var stream = file.OpenReadStream();
        var result = await _attachmentService.UploadAttachmentAsync(
            stream,
            file.FileName,
            file.Length,
            formCode,
            description,
            GetUploaderName(),
            GetCurrentUserId(),
            ownerType,
            ownerId
        );

        return Ok(ApiResponse<AttachmentDto>.Ok(result, "Lưu tệp tin thành công vào hệ thống."));
    }

    /// <summary>Thay tệp bằng phiên bản mới (phiên bản cũ được giữ lại trong lịch sử)</summary>
    [HttpPost("{id}/versions")]
    [Authorize(Policy = AppPermissions.AttachmentsUpload)]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> UploadNewVersion(Guid id, [FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("Tệp đính kèm không được để trống."));

        using var stream = file.OpenReadStream();
        var result = await _attachmentService.ReplaceAttachmentAsync(
            id, stream, file.FileName, file.Length, GetUploaderName(), GetCurrentUserId());
        return Ok(ApiResponse<AttachmentDto>.Ok(result, $"Đã lưu phiên bản {result.VersionNumber} của tệp."));
    }

    /// <summary>Lịch sử phiên bản của tệp (mới nhất trước)</summary>
    [HttpGet("{id}/versions")]
    [Authorize(Policy = AppPermissions.AttachmentsRead)]
    public async Task<IActionResult> GetVersions(Guid id)
    {
        var versions = await _attachmentService.GetVersionsAsync(id, GetCurrentUserId());
        return Ok(ApiResponse<List<AttachmentDto>>.Ok(versions, "Lấy lịch sử phiên bản thành công."));
    }

    /// <summary>Tải một phiên bản cụ thể của tệp</summary>
    [HttpGet("{id}/versions/{versionNumber:int}/download")]
    [Authorize(Policy = AppPermissions.AttachmentsRead)]
    public async Task<IActionResult> DownloadVersion(Guid id, int versionNumber)
    {
        try
        {
            var result = await _attachmentService.DownloadVersionAsync(id, versionNumber, GetCurrentUserId());
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(result.Stream, result.ContentType, result.FileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>Tên hiển thị người tải lên lấy từ JWT claim.</summary>
    private string GetUploaderName() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Cán bộ ATTECH";

    /// <summary>Tải về phiên bản hiện hành của tệp tin minh chứng (Id của bất kỳ phiên bản nào trong tệp)</summary>
    [HttpGet("{id}/download")]
    [Authorize(Policy = AppPermissions.AttachmentsRead)]
    public async Task<IActionResult> DownloadFile(Guid id)
    {
        try
        {
            var result = await _attachmentService.DownloadAttachmentAsync(id, GetCurrentUserId());
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(result.Stream, result.ContentType, result.FileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>Xem trực tiếp tệp tin minh chứng (PDF, ảnh) trong trình duyệt (inline)</summary>
    [HttpGet("{id}/view")]
    [Authorize(Policy = AppPermissions.AttachmentsRead)]
    public async Task<IActionResult> ViewFile(Guid id)
    {
        try
        {
            var result = await _attachmentService.DownloadAttachmentAsync(id, GetCurrentUserId());
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            var isInline = result.ContentType is "application/pdf" or "image/png" or "image/jpeg";
            var disposition = new ContentDispositionHeaderValue(isInline ? "inline" : "attachment")
            {
                FileNameStar = result.FileName
            };
            Response.Headers["Content-Disposition"] = disposition.ToString();
            return File(result.Stream, result.ContentType);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>Thông tin chi tiết tệp tin</summary>
    [HttpGet("{id}")]
    [Authorize(Policy = AppPermissions.AttachmentsRead)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var file = await _attachmentService.GetAttachmentByIdAsync(id, GetCurrentUserId());
        if (file == null)
            return NotFound(ApiResponse.Fail("Không tìm thấy tệp tin."));

        return Ok(ApiResponse<AttachmentDto>.Ok(file, "Lấy thông tin tệp tin thành công."));
    }


    /// <summary>Xóa mềm tệp tin (mọi phiên bản)</summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = AppPermissions.AttachmentsDelete)]
    public async Task<IActionResult> DeleteFile(Guid id)
    {
        try
        {
            await _attachmentService.DeleteAttachmentAsync(id, GetCurrentUserId());
            return Ok(ApiResponse.Ok("Đã xóa tệp tin thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }
}
