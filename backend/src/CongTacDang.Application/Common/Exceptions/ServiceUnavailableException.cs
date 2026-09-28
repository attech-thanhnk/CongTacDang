namespace CongTacDang.Application.Common.Exceptions;

/// <summary>Chức năng phụ thuộc thành phần máy chủ chưa sẵn sàng (ví dụ LibreOffice để xuất PDF).</summary>
public sealed class ServiceUnavailableException : AppException
{
    public ServiceUnavailableException(string message) : base(message, 503)
    {
    }
}
