using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Imports;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CongTacDang.Api.Controllers;

/// <summary>
/// Nhập dữ liệu từ Excel: tải mẫu → tải tệp lên (xem trước) → xác nhận (1 transaction) → tải tệp kết quả (1 lần).
/// Cần <c>system.import</c> và quyền dữ liệu của từng loại (kiểm tra trong <see cref="IImportService"/>).
/// Controller không biết các loại cụ thể — thêm loại mới chỉ cần đăng ký <c>AddImportDefinition&lt;T&gt;()</c>.
/// </summary>
[ApiController]
[Route("api/imports")]
[RequirePermission(PermissionCodes.SystemImport)]
public class ImportController : ControllerBase
{
    /// <summary>Giới hạn body của request tải lên (lớn hơn 5 MB một chút để service trả thông báo rõ ràng).</summary>
    private const long UploadRequestLimit = ImportLimits.MaxFileBytes + 1024 * 1024;

    private readonly IImportService _imports;

    public ImportController(IImportService imports)
    {
        _imports = imports;
    }

    /// <summary>Các loại dữ liệu người dùng hiện tại được nhập.</summary>
    [HttpGet("kinds")]
    public async Task<IActionResult> GetKinds(CancellationToken ct)
    {
        var kinds = await _imports.GetKindsAsync(ct);
        return Ok(ApiResponse<List<ImportKindDto>>.Ok(kinds, "Lấy danh sách loại dữ liệu nhập thành công."));
    }

    /// <summary>Tải file mẫu .xlsx của loại.</summary>
    [HttpGet("{kind}/template")]
    public async Task<IActionResult> GetTemplate(string kind, CancellationToken ct)
    {
        var (fileName, content) = await _imports.GetTemplateAsync(kind, ct);
        return File(content, ImportService.ContentType, fileName);
    }

    /// <summary>Tải tệp lên, trả kết quả kiểm tra từng dòng và Id phiên để xác nhận.</summary>
    [HttpPost("{kind}/preview")]
    [RequestSizeLimit(UploadRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadRequestLimit)]
    public async Task<IActionResult> Preview(string kind, IFormFile? file, CancellationToken ct)
    {
        if (file == null)
            return BadRequest(ApiResponse.Fail("Chưa chọn tệp. Hãy chọn tệp Excel (.xlsx) theo file mẫu."));

        await using var stream = file.OpenReadStream();
        var preview = await _imports.PreviewAsync(kind, file.FileName, file.Length, stream, ct);
        var message = preview.CanCommit
            ? "Tệp hợp lệ. Hãy kiểm tra và xác nhận để ghi dữ liệu."
            : "Tệp có dòng lỗi hoặc không có dữ liệu. Hãy sửa tệp và tải lên lại.";
        return Ok(ApiResponse<ImportPreviewDto>.Ok(preview, message));
    }

    /// <summary>Xác nhận phiên: ghi toàn bộ trong một transaction.</summary>
    [HttpPost("{sessionId:guid}/commit")]
    public async Task<IActionResult> Commit(Guid sessionId, CancellationToken ct)
    {
        var result = await _imports.CommitAsync(sessionId, ct);
        return Ok(ApiResponse<ImportCommitResultDto>.Ok(result,
            $"Đã nhập dữ liệu: {result.Created} bản ghi tạo mới, {result.Updated} bản ghi cập nhật."));
    }

    /// <summary>Tải tệp kết quả — chỉ một lần, hết hạn sau 30 phút, chỉ người thực hiện nhập tải được.</summary>
    [HttpGet("results/{token}")]
    public IActionResult DownloadResult(string token)
    {
        var file = _imports.TakeResultFile(token);
        if (file == null)
            return NotFound(ApiResponse.Fail(
                "Tệp kết quả không còn: tệp chỉ tải được một lần và hết hạn sau 30 phút. "
                + "Nếu chưa lưu được mật khẩu tạm, hãy đặt lại mật khẩu cho các tài khoản trong màn hình quản lý tài khoản."));

        Response.Headers.CacheControl = "no-store";
        return File(file.Content, ImportService.ContentType, file.FileName);
    }
}
