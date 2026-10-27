using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;

namespace CongTacDang.Api.Controllers;

/// <summary>Tệp đính kèm, minh chứng; quyền trên tệp = quyền trên đối tượng được gắn.</summary>
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

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
            throw new UnauthorizedAccessException("Xác thực danh tính người dùng hiện tại không thành công.");
        return userId;
    }

    /// <summary>Danh sách tệp tin và tài liệu minh chứng mà người dùng được xem</summary>
    [HttpGet("list")]
    [Authorize] // Quyền trên tệp = quyền trên hồ sơ gắn tệp; tệp chưa gắn: chỉ người tải lên (kiểm tra trong service)
    public async Task<IActionResult> GetList()
    {
        var files = await _attachmentService.GetAttachmentsAsync(GetCurrentUserId());
        return Ok(ApiResponse<List<AttachmentDto>>.Ok(files, "Lấy danh mục tệp tin thành công."));
    }

    /// <summary>Tệp (phiên bản hiện hành) của một đối tượng theo ownerType/ownerId.</summary>
    [HttpGet]
    [Authorize] // Quyền trên tệp = quyền trên hồ sơ gắn tệp; tệp chưa gắn: chỉ người tải lên (kiểm tra trong service)
    public async Task<IActionResult> GetByOwner([FromQuery] string ownerType, [FromQuery] Guid ownerId)
    {
        var files = await _attachmentService.GetAttachmentsByOwnerAsync(ownerType, ownerId, GetCurrentUserId());
        return Ok(ApiResponse<List<AttachmentDto>>.Ok(files, "Lấy danh sách tệp của đối tượng thành công."));
    }

    /// <summary>Quyền thêm/thay/gỡ minh chứng của hồ sơ theo giai đoạn.</summary>
    [HttpGet("evidence-rights")]
    [Authorize] // Service: evaluation.read trên hồ sơ (chủ hồ sơ luôn xem được)
    public async Task<IActionResult> GetEvidenceRights([FromQuery] Guid recordId)
    {
        var rights = await _attachmentService.GetEvidenceRightsAsync(recordId, GetCurrentUserId());
        return Ok(ApiResponse<EvidenceRightsDto>.Ok(rights, "Lấy quyền với minh chứng của hồ sơ thành công."));
    }

    /// <summary>
    /// Tải lên minh chứng. Không truyền ownerType/ownerId: tệp chưa gắn, chỉ người tải lên thấy tới khi được gắn.
    /// </summary>
    [HttpPost("upload")]
    [Authorize] // Service: evaluation.self (tệp của mình), evaluation.external.record (văn bản của cấp trên), collective.manage (hồ sơ tập thể), quyền sửa phiếu/biên bản (Mẫu 03/19/20)
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> UploadFile(
        [FromForm] IFormFile file,
        [FromForm] string formCode,
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
    [Authorize] // Service: evaluation.self (tệp của mình), evaluation.external.record (văn bản của cấp trên), collective.manage (hồ sơ tập thể), quyền sửa phiếu/biên bản (Mẫu 03/19/20)
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
    [Authorize] // Quyền trên tệp = quyền trên hồ sơ gắn tệp; tệp chưa gắn: chỉ người tải lên (kiểm tra trong service)
    public async Task<IActionResult> GetVersions(Guid id)
    {
        var versions = await _attachmentService.GetVersionsAsync(id, GetCurrentUserId());
        return Ok(ApiResponse<List<AttachmentDto>>.Ok(versions, "Lấy lịch sử phiên bản thành công."));
    }

    /// <summary>Tải một phiên bản cụ thể của tệp</summary>
    [HttpGet("{id}/versions/{versionNumber:int}/download")]
    [Authorize] // Quyền trên tệp = quyền trên hồ sơ gắn tệp; tệp chưa gắn: chỉ người tải lên (kiểm tra trong service)
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

    private string GetUploaderName() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Cán bộ";

    /// <summary>Tải phiên bản hiện hành (nhận Id của bất kỳ phiên bản nào).</summary>
    [HttpGet("{id}/download")]
    [Authorize] // Quyền trên tệp = quyền trên hồ sơ gắn tệp; tệp chưa gắn: chỉ người tải lên (kiểm tra trong service)
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

    /// <summary>Gỡ minh chứng khỏi hồ sơ (tệp vẫn được lưu).</summary>
    [HttpDelete("{id}")]
    [Authorize] // Service: chủ hồ sơ (evaluation.self) và là người tải lên; hồ sơ tập thể: collective.manage, kỳ chưa đóng
    public async Task<IActionResult> Remove(Guid id)
    {
        await _attachmentService.RemoveAttachmentAsync(id, GetCurrentUserId());
        return Ok(ApiResponse.Ok("Đã gỡ tệp minh chứng."));
    }

    /// <summary>Thông tin chi tiết tệp tin</summary>
    [HttpGet("{id}")]
    [Authorize] // Quyền trên tệp = quyền trên hồ sơ gắn tệp; tệp chưa gắn: chỉ người tải lên (kiểm tra trong service)
    public async Task<IActionResult> GetById(Guid id)
    {
        var file = await _attachmentService.GetAttachmentByIdAsync(id, GetCurrentUserId());
        if (file == null)
            return NotFound(ApiResponse.Fail("Không tìm thấy tệp tin."));

        return Ok(ApiResponse<AttachmentDto>.Ok(file, "Lấy thông tin tệp tin thành công."));
    }
}
