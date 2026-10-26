using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;

namespace CongTacDang.Infrastructure.Documents;

public interface IPdfConverter
{
    /// <summary>Báo <see cref="ServiceUnavailableException"/> khi không có LibreOffice, quá thời gian hoặc chuyển lỗi.</summary>
    Task<byte[]> ConvertToPdfAsync(byte[] content, string sourceExtension, CancellationToken cancellationToken = default);
}

/// <summary>Cấu hình <c>Documents:Pdf:*</c>.</summary>
public sealed class PdfConversionOptions
{
    /// <summary>Để trống: tự tìm trong PATH và các thư mục cài đặt mặc định.</summary>
    public string? SofficePath { get; set; }

    /// <summary>Giây.</summary>
    public int TimeoutSeconds { get; set; } = 60;

    public int MaxConcurrency { get; set; } = 2;

    /// <summary>Để trống: thư mục tạm của hệ điều hành.</summary>
    public string? WorkDirectory { get; set; }
}

public static class LibreOfficeLocator
{
    /// <summary>Theo cấu hình, PATH rồi thư mục cài đặt mặc định; không thấy → null.</summary>
    public static string? Find(string? configuredPath = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return File.Exists(configuredPath) ? configuredPath : FindOnPath(configuredPath);

        var names = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { "soffice.exe", "soffice.com" }
            : new[] { "soffice", "libreoffice" };
        foreach (var name in names)
        {
            var onPath = FindOnPath(name);
            if (onPath != null)
                return onPath;
        }

        var candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LibreOffice", "program", "soffice.exe")
            }
            : new[]
            {
                "/usr/bin/soffice",
                "/usr/lib/libreoffice/program/soffice",
                "/opt/libreoffice/program/soffice",
                "/Applications/LibreOffice.app/Contents/MacOS/soffice"
            };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? FindOnPath(string fileName)
    {
        if (fileName.Contains(Path.DirectorySeparatorChar) || fileName.Contains(Path.AltDirectorySeparatorChar))
            return null;

        var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        return paths
            .Select(dir => Path.Combine(dir.Trim(), fileName))
            .FirstOrDefault(File.Exists);
    }
}

/// <summary>Mỗi lần chuyển dùng thư mục tạm và hồ sơ người dùng LibreOffice riêng để chạy song song an toàn.</summary>
public sealed class LibreOfficePdfConverter : IPdfConverter, IDisposable
{
    private readonly PdfConversionOptions _options;
    private readonly SemaphoreSlim _gate;

    public LibreOfficePdfConverter(PdfConversionOptions options)
    {
        _options = options;
        _gate = new SemaphoreSlim(Math.Max(1, options.MaxConcurrency));
    }

    /// <inheritdoc />
    public async Task<byte[]> ConvertToPdfAsync(byte[] content, string sourceExtension, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var extension = sourceExtension.StartsWith('.') ? sourceExtension.ToLowerInvariant() : "." + sourceExtension.ToLowerInvariant();
        if (extension is not (".docx" or ".xlsx"))
            throw new ArgumentException($"Không hỗ trợ chuyển định dạng '{sourceExtension}' sang PDF.", nameof(sourceExtension));

        var soffice = LibreOfficeLocator.Find(_options.SofficePath)
            ?? throw new ServiceUnavailableException(
                "Máy chủ chưa cài LibreOffice nên chưa xuất được PDF. Vui lòng tải bản Word/Excel hoặc liên hệ quản trị hệ thống.");

        await _gate.WaitAsync(cancellationToken);
        var workRoot = string.IsNullOrWhiteSpace(_options.WorkDirectory)
            ? Path.Combine(Path.GetTempPath(), "congtacdang-pdf")
            : _options.WorkDirectory;
        var jobDirectory = Path.Combine(workRoot, Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(jobDirectory);
            var inputPath = Path.Combine(jobDirectory, "document" + extension);
            await File.WriteAllBytesAsync(inputPath, content, cancellationToken);

            var profileUri = new Uri(Path.Combine(jobDirectory, "profile") + Path.DirectorySeparatorChar).AbsoluteUri;
            var startInfo = new ProcessStartInfo(soffice)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = jobDirectory
            };
            foreach (var argument in new[]
            {
                "--headless", "--norestore", "--nologo", "--nodefault", "--nolockcheck", "--nofirststartwizard",
                "-env:UserInstallation=" + profileUri,
                "--convert-to", "pdf",
                "--outdir", jobDirectory,
                inputPath
            })
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = StartProcess(startInfo);
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                if (cancellationToken.IsCancellationRequested)
                    throw;
                throw new ServiceUnavailableException(
                    $"Chuyển PDF quá thời gian cho phép ({_options.TimeoutSeconds} giây). Vui lòng thử lại hoặc tải bản Word/Excel.");
            }

            await Task.WhenAll(stdout, stderr);
            var outputPath = Path.Combine(jobDirectory, "document.pdf");
            if (process.ExitCode != 0 || !File.Exists(outputPath))
            {
                throw new ServiceUnavailableException(
                    $"Chuyển tài liệu sang PDF bằng LibreOffice không thành công (mã thoát {process.ExitCode}). Vui lòng tải bản Word/Excel.");
            }

            return await File.ReadAllBytesAsync(outputPath, cancellationToken);
        }
        finally
        {
            TryDeleteDirectory(jobDirectory);
            _gate.Release();
        }
    }

    private static Process StartProcess(ProcessStartInfo startInfo)
    {
        try
        {
            return Process.Start(startInfo)
                ?? throw new ServiceUnavailableException("Khởi động LibreOffice để xuất PDF không thành công. Vui lòng tải bản Word/Excel.");
        }
        catch (Win32Exception)
        {
            throw new ServiceUnavailableException(
                "Chạy LibreOffice (soffice) trên máy chủ không thành công nên chưa xuất được PDF. Kiểm tra cấu hình Documents:Pdf:SofficePath.");
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Tiến trình đã kết thúc.
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // Tệp có thể còn bị khóa ngay sau khi dừng tiến trình; thư mục tạm của hệ điều hành sẽ được dọn.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();
}
