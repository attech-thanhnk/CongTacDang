using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>
/// Giao diện dịch vụ ký số và kiểm thực tài liệu PDF theo chuẩn PAdES / e-Signature.
/// </summary>
public interface IDigitalSignatureService
{
    /// <summary>
    /// Ký số lên tệp PDF theo yêu cầu (chuẩn PAdES).
    /// </summary>
    Task<byte[]> SignPdfAsync(byte[] pdfBytes, DigitalSignatureRequestDto request, CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra tính hợp lệ và danh sách chữ ký số trong tệp PDF.
    /// </summary>
    Task<DigitalSignatureVerificationResultDto> VerifyPdfAsync(byte[] pdfBytes, CancellationToken ct = default);
}
