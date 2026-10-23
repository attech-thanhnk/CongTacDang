using System;

namespace CongTacDang.Application.Common;

/// <summary>Kiểm tra chữ ký đầu tệp (magic bytes) theo phần mở rộng, chặn tệp đổi đuôi.</summary>
public static class FileSignatures
{
    /// <summary>Số byte đầu tệp cần đọc để nhận diện mọi định dạng hỗ trợ.</summary>
    public const int HeaderLength = 12;

    /// <summary>Phần mở rộng (chữ thường, có dấu chấm) không hỗ trợ → false.</summary>
    public static bool Matches(string extension, ReadOnlySpan<byte> header) => extension switch
    {
        ".pdf" => header.StartsWith("%PDF"u8),
        ".png" => header.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        ".jpg" or ".jpeg" => header.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]),
        ".webp" => header.Length >= 12 && header.StartsWith("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8),
        ".docx" or ".xlsx" => header.StartsWith((ReadOnlySpan<byte>)[0x50, 0x4B, 0x03, 0x04]),
        _ => false
    };
}
