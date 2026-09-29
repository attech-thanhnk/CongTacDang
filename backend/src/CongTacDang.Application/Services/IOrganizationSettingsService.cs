using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Services;

/// <summary>
/// Thông tin đơn vị (task 17 — T-80): đọc có cache (xóa khi sửa), sửa cần <c>system.settings.manage</c> (kiểm tra ở controller).
/// </summary>
public interface IOrganizationSettingsService
{
    /// <summary>Thông tin đơn vị hiện hành (đọc từ cache).</summary>
    Task<OrganizationSettingsDto> GetAsync(CancellationToken ct = default);

    /// <summary>Phần hiển thị trước khi đăng nhập.</summary>
    Task<PublicOrganizationInfoDto> GetPublicAsync(CancellationToken ct = default);

    /// <summary>Sửa thông tin đơn vị (kiểm tra hợp lệ, ghi audit, xóa cache).</summary>
    Task<OrganizationSettingsDto> UpdateAsync(UpdateOrganizationSettingsDto request, CancellationToken ct = default);
}

/// <summary>Nội dung tệp tải xuống.</summary>
public sealed record FileDownload(byte[] Content, string ContentType, string FileName);

/// <summary>
/// Quản lý file mẫu Word của biểu mẫu (task 17 — T-81). Quyền <c>system.templates.manage</c> kiểm tra ở controller.
/// </summary>
public interface IWordTemplateService
{
    /// <summary>Danh mục biểu mẫu kèm phiên bản đang dùng.</summary>
    Task<List<WordTemplateSummaryDto>> GetTemplatesAsync(CancellationToken ct = default);

    /// <summary>Lịch sử phiên bản của một biểu mẫu (mới nhất trước).</summary>
    Task<List<WordTemplateVersionDto>> GetVersionsAsync(string code, CancellationToken ct = default);

    /// <summary>Kiểm tra một tệp (không lưu): tag có trong tệp, tag lạ (lỗi), tag bắt buộc thiếu (cảnh báo), sinh thử.</summary>
    Task<WordTemplateCheckDto> CheckAsync(string code, Stream content, string fileName, long size, CancellationToken ct = default);

    /// <summary>
    /// Tải lên phiên bản mới: kiểm tra như <see cref="CheckAsync"/>; có lỗi → không lưu, trả kết quả với
    /// <see cref="WordTemplateCheckDto.IsValid"/> = false. Hợp lệ → lưu vào kho tệp, kích hoạt nếu <paramref name="activate"/>.
    /// </summary>
    Task<WordTemplateCheckDto> UploadAsync(string code, Stream content, string fileName, long size, string? note, bool activate, CancellationToken ct = default);

    /// <summary>Kích hoạt một phiên bản đã tải lên (kể cả phiên bản cũ).</summary>
    Task<WordTemplateVersionDto> ActivateAsync(string code, Guid versionId, CancellationToken ct = default);

    /// <summary>Quay về file mẫu gốc đi kèm ứng dụng (bỏ kích hoạt mọi phiên bản).</summary>
    Task UseOriginalAsync(string code, CancellationToken ct = default);

    /// <summary>Tải file đang dùng (phiên bản kích hoạt hoặc file gốc).</summary>
    Task<FileDownload> DownloadCurrentAsync(string code, CancellationToken ct = default);

    /// <summary>Tải file mẫu gốc đi kèm ứng dụng.</summary>
    Task<FileDownload> DownloadOriginalAsync(string code, CancellationToken ct = default);

    /// <summary>Tải một phiên bản đã tải lên.</summary>
    Task<FileDownload> DownloadVersionAsync(string code, Guid versionId, CancellationToken ct = default);
}
