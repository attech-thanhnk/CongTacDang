using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;

namespace CongTacDang.Api.Controllers;

[ApiController]
[Route("api/attachments")]
[Route("api/[controller]")]
public class AttachmentController : ControllerBase
{
    private readonly IAttachmentService _attachmentService;

    public AttachmentController(IAttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    /// <summary>
    /// Danh sach toan bo tap tin va tai lieu minh chung
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetList()
    {
        var files = await _attachmentService.GetAttachmentsAsync();
        return Ok(ApiResponse<List<AttachmentDto>>.Ok(files, "Lấy danh mục tệp tin thành công."));
    }

    /// <summary>
    /// Tai len tep minh chung (PDF, DOCX, XLSX, Anh)
    /// </summary>
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
            using var stream = file.OpenReadStream();
            var result = await _attachmentService.UploadAttachmentAsync(
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                formCode,
                description,
                "Cán bộ ATTECH"
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

    /// <summary>
    /// Tai ve tep tin minh chung theo ID
    /// </summary>
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

    /// <summary>
    /// Thong tin chi tiet tep tin
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var file = await _attachmentService.GetAttachmentByIdAsync(id);
        if (file == null)
            return NotFound(ApiResponse.Fail("Không tìm thấy tệp tin."));

        return Ok(ApiResponse<AttachmentDto>.Ok(file, "Lấy thông tin tệp tin thành công."));
    }

    /// <summary>
    /// Chinh sua thong tin trich yeu va phan loai tep tin
    /// </summary>
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

    /// <summary>
    /// Xoa tep tin khoi he thong
    /// </summary>
    [HttpDelete("{id}")]
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
