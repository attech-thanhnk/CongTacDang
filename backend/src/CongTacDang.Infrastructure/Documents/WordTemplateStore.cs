using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CongTacDang.Infrastructure.Documents;

/// <summary>Nguồn template Word (.docx) dùng khi xuất biểu mẫu.</summary>
public interface IWordTemplateStore
{
    /// <summary>
    /// Đọc template theo tên file mẫu gốc (ví dụ <c>Mau_01_PhieuGiaoNhiemVu.docx</c>): phiên bản đang kích hoạt trong CSDL,
    /// không có thì file mẫu gốc đi kèm ứng dụng.
    /// </summary>
    Task<byte[]> LoadAsync(string templateFileName, CancellationToken ct = default);
}

/// <summary>File mẫu gốc đi kèm ứng dụng.</summary>
public interface IBundledWordTemplates
{
    /// <summary>Đọc file mẫu gốc theo tên tệp.</summary>
    byte[] Load(string templateFileName);
}

/// <summary>Tra phiên bản file mẫu đang kích hoạt của một biểu mẫu.</summary>
public interface IActiveWordTemplateLookup
{
    /// <summary>Phiên bản đang kích hoạt của biểu mẫu <paramref name="templateCode"/>; null = dùng file mẫu gốc.</summary>
    Task<WordTemplateVersion?> FindActiveAsync(string templateCode, CancellationToken ct = default);
}

/// <summary>
/// Đọc file mẫu gốc từ thư mục trên đĩa. Template là tệp .docx thật (soạn bằng Word), không được sinh bằng code;
/// thiếu tệp thì báo lỗi rõ ràng thay vì tự tạo phôi.
/// </summary>
public sealed class FileWordTemplateStore : IBundledWordTemplates
{
    private readonly string[] _directories;

    /// <param name="templateDirectory">
    /// Thư mục template cấu hình (<c>Documents:TemplatePath</c>). Để trống thì dùng <c>Templates/Word</c> trong thư mục chạy ứng dụng.
    /// </param>
    public FileWordTemplateStore(string? templateDirectory = null)
    {
        _directories = string.IsNullOrWhiteSpace(templateDirectory)
            ? new[] { Path.Combine(AppContext.BaseDirectory, "Templates", "Word") }
            : new[] { Path.GetFullPath(templateDirectory) };
    }

    /// <inheritdoc />
    public byte[] Load(string templateFileName)
    {
        if (string.IsNullOrWhiteSpace(templateFileName)
            || templateFileName != Path.GetFileName(templateFileName)
            || !templateFileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Tên template Word không hợp lệ.", nameof(templateFileName));
        }

        var path = _directories
            .Select(dir => Path.Combine(dir, templateFileName))
            .FirstOrDefault(File.Exists);
        if (path == null)
        {
            throw new FileNotFoundException(
                $"Không tìm thấy template Word '{templateFileName}' trong thư mục: {string.Join(", ", _directories)}.",
                templateFileName);
        }

        return File.ReadAllBytes(path);
    }
}

/// <summary>Tra phiên bản đang kích hoạt trong bảng <c>word_template_versions</c>.</summary>
public sealed class DbActiveWordTemplateLookup : IActiveWordTemplateLookup
{
    private readonly CongTacDangDbContext _db;

    /// <summary>Khởi tạo.</summary>
    public DbActiveWordTemplateLookup(CongTacDangDbContext db) => _db = db;

    /// <inheritdoc />
    public Task<WordTemplateVersion?> FindActiveAsync(string templateCode, CancellationToken ct = default) =>
        _db.Set<WordTemplateVersion>().AsNoTracking()
            .Where(v => v.TemplateCode == templateCode && v.IsActive)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);
}

/// <summary>
/// Nguồn template khi xuất (task 17 — T-81): phiên bản đang kích hoạt (metadata trong CSDL, nội dung trong kho tệp),
/// không có thì file mẫu gốc. Tệp của phiên bản kích hoạt bị mất khỏi kho → ghi log lỗi và dùng file mẫu gốc để việc xuất
/// không bị gián đoạn.
/// </summary>
public sealed class WordTemplateStore : IWordTemplateStore
{
    private readonly IActiveWordTemplateLookup _lookup;
    private readonly IFileStorageService _storage;
    private readonly IBundledWordTemplates _bundled;
    private readonly ILogger<WordTemplateStore> _logger;

    /// <summary>Khởi tạo.</summary>
    public WordTemplateStore(
        IActiveWordTemplateLookup lookup,
        IFileStorageService storage,
        IBundledWordTemplates bundled,
        ILogger<WordTemplateStore>? logger = null)
    {
        _lookup = lookup;
        _storage = storage;
        _bundled = bundled;
        _logger = logger ?? NullLogger<WordTemplateStore>.Instance;
    }

    /// <inheritdoc />
    public async Task<byte[]> LoadAsync(string templateFileName, CancellationToken ct = default)
    {
        var form = WordFormCatalog.FindByFileName(templateFileName);
        if (form != null)
        {
            var active = await _lookup.FindActiveAsync(form.Code, ct);
            if (active != null)
            {
                var stream = await _storage.GetFileStreamAsync(active.ObjectKey);
                if (stream != null)
                {
                    await using (stream)
                    {
                        using var buffer = new MemoryStream();
                        await stream.CopyToAsync(buffer, ct);
                        return buffer.ToArray();
                    }
                }

                _logger.LogError(
                    "Không tìm thấy tệp của file mẫu {Code} phiên bản {Version} ({ObjectKey}) trong kho tệp; dùng file mẫu gốc.",
                    form.Code, active.VersionNumber, active.ObjectKey);
            }
        }

        return _bundled.Load(templateFileName);
    }
}
