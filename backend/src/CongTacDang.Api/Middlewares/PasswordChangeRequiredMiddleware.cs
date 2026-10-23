using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using CongTacDang.Application.Accounts;
using CongTacDang.Application.Common.Models;
using Microsoft.AspNetCore.Http;

namespace CongTacDang.Api.Middlewares;

/// <summary>
/// Tài khoản còn mật khẩu tạm: mọi API ngoài danh sách cho phép trả 403 <see cref="Code"/>.
/// Đặt sau <c>UseAuthentication</c>, trước <c>UseAuthorization</c>; trạng thái do <c>OnTokenValidated</c> nạp.
/// </summary>
public sealed class PasswordChangeRequiredMiddleware
{
    public const string Code = "PASSWORD_CHANGE_REQUIRED";

    /// <summary>Khóa <see cref="HttpContext.Items"/> chứa <see cref="AccountState"/> của request.</summary>
    public const string AccountStateItemKey = "CongTacDang.AccountState";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly HashSet<(string Method, string Path)> Allowed = new()
    {
        ("GET", "/api/auth/me"),
        ("POST", "/api/auth/change-password"),
        ("POST", "/api/auth/logout"),
        ("POST", "/api/auth/refresh-token"),
        ("POST", "/api/auth/login"),
    };

    private readonly RequestDelegate _next;

    public PasswordChangeRequiredMiddleware(RequestDelegate next) => _next = next;

    public static bool IsAllowed(string method, string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        var normalized = path.Length > 1 ? path.TrimEnd('/') : path;
        return Allowed.Contains((method.ToUpperInvariant(), normalized.ToLowerInvariant()));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.Items.TryGetValue(AccountStateItemKey, out var value)
            && value is AccountState { MustChangePassword: true }
            && !HttpMethods.IsOptions(context.Request.Method)
            && !IsAllowed(context.Request.Method, context.Request.Path.Value))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            var body = ApiResponse.FailWithCode(Code,
                "Bạn đang dùng mật khẩu tạm. Hãy đổi mật khẩu trước khi tiếp tục sử dụng hệ thống.");
            await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
            return;
        }

        await _next(context);
    }
}
