namespace CongTacDang.Application.Common.Exceptions;

/// <summary>Dữ liệu đầu vào không hợp lệ.</summary>
public sealed class ValidationException : AppException
{
    public ValidationException(string message) : base(message, 400)
    {
    }
}
