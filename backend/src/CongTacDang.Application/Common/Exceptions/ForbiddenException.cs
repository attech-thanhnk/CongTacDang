namespace CongTacDang.Application.Common.Exceptions;

/// <summary>Người dùng đã xác thực nhưng không có quyền thao tác.</summary>
public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message) : base(message, 403)
    {
    }
}
