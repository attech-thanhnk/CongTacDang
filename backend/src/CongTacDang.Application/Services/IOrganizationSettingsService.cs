using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Services;

/// <summary>Thông tin đơn vị: đọc có cache (xóa khi sửa); quyền sửa kiểm tra ở controller.</summary>
public interface IOrganizationSettingsService
{
    Task<OrganizationSettingsDto> GetAsync(CancellationToken ct = default);

    /// <summary>Phần hiển thị trước khi đăng nhập.</summary>
    Task<PublicOrganizationInfoDto> GetPublicAsync(CancellationToken ct = default);

    Task<OrganizationSettingsDto> UpdateAsync(UpdateOrganizationSettingsDto request, CancellationToken ct = default);

    /// <summary>JPG/PNG/WEBP tối đa 5 MB, kiểm tra chữ ký tệp; thay ảnh cũ (xóa tệp cũ).</summary>
    Task<OrganizationSettingsDto> SetLoginBackgroundAsync(Stream content, string fileName, long size, CancellationToken ct = default);

    Task<OrganizationSettingsDto> RemoveLoginBackgroundAsync(CancellationToken ct = default);

    /// <summary>null khi chưa có ảnh hoặc tệp đã mất.</summary>
    Task<LoginBackgroundFile?> OpenLoginBackgroundAsync(CancellationToken ct = default);
}

public sealed record FileDownload(byte[] Content, string ContentType, string FileName);

/// <summary>Quản lý phiên bản file mẫu Word; quyền kiểm tra ở controller.</summary>
public interface IWordTemplateService
{
    Task<List<WordTemplateSummaryDto>> GetTemplatesAsync(CancellationToken ct = default);

    /// <summary>Mới nhất trước.</summary>
    Task<List<WordTemplateVersionDto>> GetVersionsAsync(string code, CancellationToken ct = default);

    /// <summary>Không lưu: tag lạ là lỗi, thiếu tag bắt buộc là cảnh báo, kèm sinh thử.</summary>
    Task<WordTemplateCheckDto> CheckAsync(string code, Stream content, string fileName, long size, CancellationToken ct = default);

    /// <summary>Kiểm tra như <see cref="CheckAsync"/>; có lỗi thì không lưu và trả <c>IsValid = false</c>.</summary>
    Task<WordTemplateCheckDto> UploadAsync(string code, Stream content, string fileName, long size, string? note, bool activate, CancellationToken ct = default);

    /// <summary>Kích hoạt được cả phiên bản cũ.</summary>
    Task<WordTemplateVersionDto> ActivateAsync(string code, Guid versionId, CancellationToken ct = default);

    /// <summary>Quay về file mẫu gốc đi kèm ứng dụng (bỏ kích hoạt mọi phiên bản).</summary>
    Task UseOriginalAsync(string code, CancellationToken ct = default);

    /// <summary>Phiên bản kích hoạt hoặc file gốc.</summary>
    Task<FileDownload> DownloadCurrentAsync(string code, CancellationToken ct = default);

    Task<FileDownload> DownloadOriginalAsync(string code, CancellationToken ct = default);

    Task<FileDownload> DownloadVersionAsync(string code, Guid versionId, CancellationToken ct = default);
}
