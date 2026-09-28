namespace CongTacDang.Application.Common.Exceptions;

/// <summary>Dữ liệu xung đột với trạng thái hiện tại.</summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string message) : base(message, 409)
    {
    }
}
