using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Models;

namespace CongTacDang.Api.Middlewares;

/// <summary>
/// Middleware xử lý lỗi toàn cục
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    /// <summary>Thực thi middleware chặn và bắt tất cả ngoại lệ chưa được xử lý trong HTTP pipeline</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    /// <summary>Chuẩn hóa thông điệp lỗi và mã trạng thái HTTP theo chuẩn ApiResponse</summary>
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "Lỗi hệ thống tại HTTP {Method} {Path}: {Message}",
            context.Request.Method, context.Request.Path, exception.Message);

        var statusCode = exception switch
        {
            AppException appException => (HttpStatusCode)appException.StatusCode,
            DbUpdateConcurrencyException => HttpStatusCode.Conflict,
            DbUpdateException dbException when IsUniqueViolation(dbException) => HttpStatusCode.Conflict,
            ArgumentException => HttpStatusCode.BadRequest,
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            KeyNotFoundException => HttpStatusCode.NotFound,
            _ => HttpStatusCode.InternalServerError
        };

        var message = exception switch
        {
            AppException appException => appException.Message,
            DbUpdateConcurrencyException => "Dữ liệu đã được người khác cập nhật, vui lòng tải lại.",
            DbUpdateException dbException when IsUniqueViolation(dbException) => "Dữ liệu đã tồn tại trong hệ thống.",
            ArgumentException or InvalidOperationException or UnauthorizedAccessException or KeyNotFoundException
                when exception is not InvalidOperationException => exception.Message,
            _ => _env.IsDevelopment()
                ? $"Lỗi máy chủ nội bộ: {exception.Message}"
                : "Đã xảy ra lỗi xử lý nội bộ tại hệ thống. Vui lòng liên hệ Quản trị viên."
        };

        var response = ApiResponse.Fail(message);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException { SqlState: "23505" };
    }
}
