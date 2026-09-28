using System;

namespace CongTacDang.Application.Common.Exceptions;

/// <summary>Ngoại lệ nghiệp vụ có mã trạng thái HTTP xác định.</summary>
public abstract class AppException : Exception
{
    protected AppException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
