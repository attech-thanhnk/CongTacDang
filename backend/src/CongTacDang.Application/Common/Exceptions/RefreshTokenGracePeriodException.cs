using System;

namespace CongTacDang.Application.Common.Exceptions;

/// <summary>Refresh token cũ bị gửi lại trong khoảng thời gian cho phép do nhiều tab cùng làm mới.</summary>
public sealed class RefreshTokenGracePeriodException : UnauthorizedAccessException
{
    public RefreshTokenGracePeriodException(string message) : base(message)
    {
    }
}
