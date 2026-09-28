using System;
using System.IO;
using System.Linq;

namespace CongTacDang.Infrastructure.Documents;

/// <summary>Nguồn template Word (.docx) của biểu mẫu.</summary>
public interface IWordTemplateStore
{
    /// <summary>Đọc nội dung template theo tên tệp (ví dụ <c>Mau_01_PhieuGiaoNhiemVu.docx</c>).</summary>
    byte[] Load(string templateFileName);
}

/// <summary>
/// Đọc template từ thư mục trên đĩa. Template là tệp .docx thật (soạn bằng Word), không được sinh bằng code;
/// thiếu tệp thì báo lỗi rõ ràng thay vì tự tạo phôi.
/// </summary>
public sealed class FileWordTemplateStore : IWordTemplateStore
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
