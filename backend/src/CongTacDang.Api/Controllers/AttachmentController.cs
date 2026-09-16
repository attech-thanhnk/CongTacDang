using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Application.Common.Models;
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

    /// <summary>Danh sách toàn bộ tệp tin và tài liệu minh chứng</summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetList()
    {
        var files = await _attachmentService.GetAttachmentsAsync();
        return Ok(ApiResponse<List<AttachmentDto>>.Ok(files, "Lấy danh mục tệp tin thành công."));
    }

    /// <summary>Tải lên tệp minh chứng (PDF, DOCX, XLSX, Ảnh)</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> UploadFile(
        [FromForm] IFormFile file,
        [FromForm] string formCode = "GENERAL",
        [FromForm] string description = "")
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("Tệp đính kèm không được để trống."));

        try
        {
            // Lấy tên cán bộ từ JWT claim để ghi log người tải lên
            var uploaderName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Cán bộ ATTECH";

            using var stream = file.OpenReadStream();
            var result = await _attachmentService.UploadAttachmentAsync(
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                formCode,
                description,
                uploaderName
            );

            return Ok(ApiResponse<AttachmentDto>.Ok(result, "Lưu tệp tin thành công vào hệ thống."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse.Fail($"Lỗi trong quá trình xử lý tệp: {ex.Message}"));
        }
    }

    /// <summary>Tải về tệp tin minh chứng theo ID</summary>
    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadFile(Guid id)
    {
        try
        {
            var result = await _attachmentService.DownloadAttachmentAsync(id);
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

    /// <summary>Thông tin chi tiết tệp tin</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var file = await _attachmentService.GetAttachmentByIdAsync(id);
        if (file == null)
            return NotFound(ApiResponse.Fail("Không tìm thấy tệp tin."));

        return Ok(ApiResponse<AttachmentDto>.Ok(file, "Lấy thông tin tệp tin thành công."));
    }

    /// <summary>Chỉnh sửa thông tin trích yếu và phân loại tệp tin</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAttachment(Guid id, [FromBody] UpdateAttachmentDto request)
    {
        try
        {
            var result = await _attachmentService.UpdateAttachmentAsync(id, request);
            return Ok(ApiResponse<AttachmentDto>.Ok(result, "Cập nhật thông tin tệp tin thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>Xóa tệp tin khỏi hệ thống — chỉ Ban Thường vụ trở lên</summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireBanThuongVu")]
    public async Task<IActionResult> DeleteFile(Guid id)
    {
        try
        {
            await _attachmentService.DeleteAttachmentAsync(id);
            return Ok(ApiResponse.Ok("Đã xóa tệp tin thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }
}
