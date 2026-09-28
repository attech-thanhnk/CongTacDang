using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace CongTacDang.Api.Authorization;

/// <summary>
/// Yêu cầu endpoint theo mã quyền: [RequirePermission(PermissionCodes.X)] = "có X ở phạm vi nào đó".
/// Service vẫn phải gọi <see cref="IAuthorizationGuard"/> trên đối tượng cụ thể.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    /// <summary>Khai báo endpoint yêu cầu mã quyền <paramref name="permission"/>.</summary>
    public RequirePermissionAttribute(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
            throw new ArgumentException("Mã quyền không được để trống.", nameof(permission));

        Permission = permission;
        Policy = permission;
    }

    /// <summary>Mã quyền yêu cầu.</summary>
    public string Permission { get; }
}

/// <summary>
/// Yêu cầu endpoint có <b>ít nhất một</b> trong các mã quyền (ở phạm vi bất kỳ) — dùng khi nhiều quyền cùng dẫn tới một chức năng
/// (vd. ghi nhận quyết định của cơ sở hoặc của cấp trên). Tên policy: <c>any:</c> + các mã nối bằng <c>|</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireAnyPermissionAttribute : AuthorizeAttribute
{
    /// <summary>Tiền tố tên policy.</summary>
    public const string PolicyPrefix = "any:";

    /// <summary>Khai báo endpoint yêu cầu một trong các mã quyền.</summary>
    public RequireAnyPermissionAttribute(params string[] permissions)
    {
        if (permissions == null || permissions.Length == 0 || permissions.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Phải khai báo ít nhất một mã quyền hợp lệ.", nameof(permissions));

        Permissions = permissions;
        Policy = PolicyPrefix + string.Join("|", permissions);
    }

    /// <summary>Các mã quyền (chỉ cần một).</summary>
    public IReadOnlyList<string> Permissions { get; }
}

/// <summary>Yêu cầu có một trong các mã quyền (ở phạm vi bất kỳ) theo <see cref="IPermissionResolver"/>.</summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>Khởi tạo yêu cầu theo một mã quyền.</summary>
    public PermissionRequirement(string permission) : this(new[] { permission })
    {
    }

    /// <summary>Khởi tạo yêu cầu "một trong các mã quyền".</summary>
    public PermissionRequirement(IReadOnlyList<string> permissions)
    {
        Permissions = permissions;
        Permission = permissions.Count > 0 ? permissions[0] : string.Empty;
    }

    /// <summary>Mã quyền đầu tiên (tương thích task 07).</summary>
    public string Permission { get; }

    /// <summary>Các mã quyền được chấp nhận.</summary>
    public IReadOnlyList<string> Permissions { get; }
}

/// <summary>
/// Cung cấp policy động: tên policy là mã quyền trong <see cref="PermissionCodes"/>, hoặc <c>any:a|b</c>.
/// Tên khác chuyển cho provider mặc định.
/// </summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;
    private readonly ConcurrentDictionary<string, AuthorizationPolicy> _policies = new(StringComparer.Ordinal);

    /// <summary>Khởi tạo provider.</summary>
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    /// <summary>Mã quyền được nhận làm tên policy.</summary>
    public static bool IsKnownPermission(string code) => PermissionCodes.IsDefined(code);

    /// <summary>Dựng yêu cầu cho tên policy; null nếu không phải policy theo mã quyền.</summary>
    public static PermissionRequirement? CreateRequirement(string policyName)
    {
        if (PermissionCodes.IsDefined(policyName))
            return new PermissionRequirement(policyName);

        if (policyName.StartsWith(RequireAnyPermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            var codes = policyName[RequireAnyPermissionAttribute.PolicyPrefix.Length..]
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (codes.Length > 0 && codes.All(PermissionCodes.IsDefined))
                return new PermissionRequirement(codes);
            return null;
        }

        return null;
    }

    /// <inheritdoc />
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    /// <inheritdoc />
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    /// <inheritdoc />
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (_policies.TryGetValue(policyName, out var cached))
            return Task.FromResult<AuthorizationPolicy?>(cached);

        var requirement = CreateRequirement(policyName);
        if (requirement == null)
            return _fallback.GetPolicyAsync(policyName);

        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(requirement)
            .Build();
        return Task.FromResult<AuthorizationPolicy?>(_policies.GetOrAdd(policyName, policy));
    }
}

/// <summary>Đánh giá <see cref="PermissionRequirement"/> từ <see cref="IPermissionResolver"/> (không đọc claim quyền/vai trò trong JWT).</summary>
public sealed class PermissionAuthorizationHandler : IAuthorizationHandler
{
    private readonly IPermissionResolver _resolver;

    /// <summary>Khởi tạo handler.</summary>
    public PermissionAuthorizationHandler(IPermissionResolver resolver) => _resolver = resolver;

    /// <inheritdoc />
    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        var pending = context.PendingRequirements.OfType<PermissionRequirement>().ToList();
        if (pending.Count == 0)
            return;

        var userId = context.User.GetUserId();
        if (userId == null)
            return;

        var permissions = await _resolver.GetAsync(userId.Value);
        if (!permissions.IsActive)
            return;

        foreach (var requirement in pending)
        {
            if (requirement.Permissions.Any(permissions.Has))
                context.Succeed(requirement);
        }
    }
}

/// <summary>
/// Trả 403 kèm <see cref="ApiResponse"/> nêu tên quyền hiển thị khi thiếu quyền (RULES 8.5);
/// các trường hợp khác giữ xử lý mặc định.
/// </summary>
public sealed class PermissionAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    /// <inheritdoc />
    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && authorizeResult.AuthorizationFailure != null)
        {
            var names = authorizeResult.AuthorizationFailure.FailedRequirements
                .OfType<PermissionRequirement>()
                .Select(r => string.Join(" hoặc ", r.Permissions.Select(DisplayNameOf).Distinct().Select(n => $"\"{n}\"")))
                .Where(name => name.Length > 0)
                .Distinct()
                .ToList();

            if (names.Count > 0)
            {
                // Ghi ra challenge/forbid mặc định trước để giữ hành vi của scheme (WWW-Authenticate, log), rồi thêm nội dung.
                if (policy.AuthenticationSchemes.Count > 0)
                {
                    foreach (var scheme in policy.AuthenticationSchemes)
                        await context.ForbidAsync(scheme);
                }
                else
                {
                    await context.ForbidAsync();
                }
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    var message = $"Bạn không có quyền {string.Join(", ", names)} để thực hiện thao tác này. "
                        + "Hãy liên hệ quản trị hệ thống nếu cần được cấp quyền.";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(ApiResponse.Fail(message), JsonOptions));
                }
                return;
            }
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }

    /// <summary>Tên hiển thị của mã quyền (không lộ mã kỹ thuật).</summary>
    private static string DisplayNameOf(string code) => PermissionCodes.Find(code)?.Name ?? "Quyền được yêu cầu";
}

/// <summary>Tiện ích đọc danh tính từ <see cref="ClaimsPrincipal"/> (chỉ danh tính, không đọc vai trò/quyền).</summary>
public static class ClaimsPrincipalIdentityExtensions
{
    /// <summary>Id người dùng từ claim NameIdentifier hoặc sub; null nếu chưa đăng nhập/không hợp lệ.</summary>
    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
            return null;

        var value = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
