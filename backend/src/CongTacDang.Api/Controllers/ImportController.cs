using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Imports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CongTacDang.Api.Controllers;

/// <summary>Nhập dữ liệu từ Excel; ngoài system.import, service kiểm tra quyền dữ liệu riêng của từng loại.</summary>
[ApiController]
[Route("api/imports")]
[Authorize]
[RequirePermission(PermissionCodes.SystemImport)]
public class ImportController : ControllerBase
{
    /// <summary>Kích thước file tối đa + 1 MB cho phần form.</summary>
    private const long UploadRequestLimit = ImportLimits.MaxFileBytes + 1024 * 1024;

    private readonly IImportService _imports;

    public ImportController(IImportService imports) => _imports = imports;

    /// <summary>Các loại nhập người dùng hiện tại được dùng.</summary>
    [HttpGet("kinds")]
    public IActionResult Kinds()
        => Ok(ApiResponse<IReadOnlyList<ImportKindDto>>.Ok(_imports.GetKinds(), "Lấy danh sách loại nhập thành công."));

    /// <summary>File mẫu .xlsx của loại nhập.</summary>
    [HttpGet("{kind}/template")]
    public async Task<IActionResult> Template(string kind, CancellationToken ct)
    {
        var file = await _imports.GetTemplateAsync(kind, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Xem trước từng dòng và tạo phiên chờ xác nhận (30 phút).</summary>
    [HttpPost("{kind}/preview")]
    [RequestSizeLimit(UploadRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadRequestLimit)]
    public async Task<IActionResult> Preview(string kind, IFormFile? file, CancellationToken ct)
    {
        if (file == null)
            return BadRequest(ApiResponse.Fail("Chưa chọn file. Hãy chọn file Excel (.xlsx)."));

        await using var stream = file.OpenReadStream();
        var preview = await _imports.PreviewAsync(kind, stream, file.FileName, file.Length, ct);
        var message = preview.Summary.Error > 0
            ? $"Có {preview.Summary.Error}/{preview.Summary.Total} dòng lỗi. Hãy sửa file rồi tải lên lại."
            : $"{preview.Summary.Total} dòng hợp lệ, sẵn sàng nhập.";
        return Ok(ApiResponse<ImportPreviewDto>.Ok(preview, message));
    }

    /// <summary>Xác nhận nhập trong một transaction (chỉ người tạo phiên, khi không còn dòng lỗi).</summary>
    [HttpPost("sessions/{sessionId:guid}/commit")]
    public async Task<IActionResult> Commit(Guid sessionId, CancellationToken ct)
    {
        var result = await _imports.CommitAsync(sessionId, ct);
        return Ok(ApiResponse<ImportCommitResultDto>.Ok(result,
            $"Đã nhập {result.Total} dòng: {result.Created} tạo mới, {result.Updated} cập nhật."));
    }

    /// <summary>Tải file mật khẩu tạm của tài khoản mới — chỉ một lần, trong 30 phút.</summary>
    [HttpGet("results/{token}")]
    public IActionResult Result(string token)
    {
        var file = _imports.DownloadResult(token);
        Response.Headers.CacheControl = "no-store";
        return File(file.Content, file.ContentType, file.FileName);
    }
}
