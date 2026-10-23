using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

/// <summary>Loại chữ ký số / chữ ký điện tử.</summary>
public enum DigitalSignatureType
{
    /// <summary>Chữ ký điện tử nội bộ kèm OTP / mật khẩu và chứng thư niêm phong máy chủ.</summary>
    InternalElectronic = 0,

    /// <summary>Chữ ký số PKI qua thiết bị phần cứng (USB Token / SIM PKI / Ban Cơ yếu).</summary>
    PkiToken = 1,

    /// <summary>Chữ ký số từ xa qua dịch vụ Cloud CA / SmartCA.</summary>
    CloudCa = 2
}

/// <summary>Yêu cầu ký số tài liệu PDF.</summary>
public sealed class DigitalSignatureRequestDto
{
    public Guid DocumentId { get; set; }
    public string FormCode { get; set; } = string.Empty;
    public string SignerName { get; set; } = string.Empty;
    public string? SignerTitle { get; set; }
    public string? Reason { get; set; }
    public string? Location { get; set; }
    public DigitalSignatureType Type { get; set; } = DigitalSignatureType.InternalElectronic;
    public string? VisualSignatureBase64 { get; set; }
    public int? PageNumber { get; set; }
    public float? PositionX { get; set; }
    public float? PositionY { get; set; }
}

/// <summary>Kết quả kiểm thực chữ ký số trên tệp PDF.</summary>
public sealed class DigitalSignatureVerificationResultDto
{
    public bool IsSigned { get; set; }
    public bool IsValid { get; set; }
    public string? Message { get; set; }
    public List<DigitalSignatureItemDto> Signatures { get; set; } = new();
}

/// <summary>Thông tin một chữ ký số trên tài liệu.</summary>
public sealed class DigitalSignatureItemDto
{
    public string SignerName { get; set; } = string.Empty;
    public DateTime SignedAt { get; set; }
    public string? Reason { get; set; }
    public string? CertificateIssuer { get; set; }
    public string? CertificateSubject { get; set; }
    public bool IsValid { get; set; }
    public bool IsTampered { get; set; }
}
