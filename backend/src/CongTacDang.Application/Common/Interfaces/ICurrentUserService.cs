using System;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>Actor cho audit ở tầng persistence, không phụ thuộc ASP.NET Core.</summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }

    string UserName { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }

    string? RequestPath { get; }
}
