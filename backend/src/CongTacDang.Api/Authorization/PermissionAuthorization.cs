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
/// Đặt policy bằng mã quyền (thay cho [Authorize(Policy = "...")] với tên policy tự đặt).
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

/// <summary>Yêu cầu có mã quyền (ở phạm vi bất kỳ) theo <see cref="IPermissionResolver"/>.</summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>Khởi tạo yêu cầu theo mã quyền.</summary>
    public PermissionRequirement(string permission) => Permission = permission;

    /// <summary>Mã quyền yêu cầu.</summary>
    public string Permission { get; }
}

/// <summary>
/// Yêu cầu của policy composite/role cũ (<c>Policy_*</c>, <c>Require*</c>): giữ nguyên kết quả như trước
/// nhưng đánh giá từ <see cref="IPermissionResolver"/> thay vì claim/role trong JWT. Task 09 thay bằng mã quyền mới.
/// </summary>
public sealed class LegacyPolicyRequirement : IAuthorizationRequirement
{
    /// <summary>Khởi tạo yêu cầu theo tên policy cũ.</summary>
    public LegacyPolicyRequirement(string policyName, string displayName, Func<EffectivePermissions, bool> evaluate)
    {
        PolicyName = policyName;
        DisplayName = displayName;
        Evaluate = evaluate;
    }

    /// <summary>Tên policy cũ.</summary>
    public string PolicyName { get; }

    /// <summary>Tên hiển thị dùng trong thông báo 403.</summary>
    public string DisplayName { get; }

    /// <summary>Luật đánh giá trên tập quyền hiệu lực.</summary>
    public Func<EffectivePermissions, bool> Evaluate { get; }
}

/// <summary>Danh mục policy composite/role cũ, định nghĩa lại trên <see cref="EffectivePermissions"/>.</summary>
public static class LegacyPolicies
{
    /// <summary>Tên policy cũ → yêu cầu tương ứng (kết quả giống khối AddAuthorization cũ trong Program.cs).</summary>
    public static readonly IReadOnlyDictionary<string, LegacyPolicyRequirement> All = new[]
    {
        new LegacyPolicyRequirement("RequireCaNBo", "Cán bộ, Đảng viên",
            p => p.HasLegacyRole(AppRoles.CAN_BO)),
        new LegacyPolicyRequirement("RequireBiThuChiBo", "Bí thư Chi bộ",
            p => p.HasLegacyRole(AppRoles.BI_THU_CHI_BO) || p.HasLegacyRole(AppRoles.BAN_THUONG_VU) || p.HasLegacyRole(AppRoles.QUAN_TRI_HE_THONG)),
        new LegacyPolicyRequirement("RequireBanThuongVu", "Ban Thường vụ",
            p => p.HasLegacyRole(AppRoles.BAN_THUONG_VU) || p.HasLegacyRole(AppRoles.QUAN_TRI_HE_THONG)),
        new LegacyPolicyRequirement("RequireQuanTriHeTong", "Quản trị hệ thống",
            p => p.HasLegacyRole(AppRoles.QUAN_TRI_HE_THONG)),
        new LegacyPolicyRequirement(AppPermissions.PolicyEvaluationsAppraiseOrApprove, "Thẩm định hoặc chuẩn y hồ sơ đánh giá",
            p => p.Has(AppPermissions.EvaluationsAppraise)
                || p.Has(AppPermissions.EvaluationsApprove)
                || p.HasLegacyRole(AppRoles.QUAN_TRI_HE_THONG)),
        new LegacyPolicyRequirement(AppPermissions.PolicyEvaluationsBranchView, "Xem hồ sơ đánh giá theo Chi bộ",
            p => p.Has(AppPermissions.EvaluationsBranchVote)
                || p.Has(AppPermissions.EvaluationsAppraise)
                || p.Has(AppPermissions.EvaluationsApprove)
                || p.HasLegacyRole(AppRoles.QUAN_TRI_HE_THONG)),
        new LegacyPolicyRequirement(AppPermissions.PolicyManagePeriods, "Quản lý kỳ đánh giá",
            p => p.Has(AppPermissions.EvaluationsApprove)
                || p.HasLegacyRole(AppRoles.QUAN_TRI_HE_THONG)
                || p.HasLegacyRole(AppRoles.BAN_THUONG_VU)),
    }.ToDictionary(r => r.PolicyName, StringComparer.Ordinal);
}

/// <summary>
/// Cung cấp policy động: tên policy là mã quyền (cũ trong <see cref="AppPermissions"/> hoặc mới trong
/// <see cref="PermissionCodes"/>) hoặc tên policy composite cũ; tên khác chuyển cho provider mặc định.
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
    public static bool IsKnownPermission(string code) =>
        PermissionCodes.IsDefined(code) || AppPermissions.All.Contains(code, StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    /// <inheritdoc />
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    /// <inheritdoc />
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (_policies.TryGetValue(policyName, out var cached))
            return Task.FromResult<AuthorizationPolicy?>(cached);

        IAuthorizationRequirement? requirement = null;
        if (IsKnownPermission(policyName))
            requirement = new PermissionRequirement(policyName);
        else if (LegacyPolicies.All.TryGetValue(policyName, out var legacy))
            requirement = legacy;

        if (requirement == null)
            return _fallback.GetPolicyAsync(policyName);

        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(requirement)
            .Build();
        return Task.FromResult<AuthorizationPolicy?>(_policies.GetOrAdd(policyName, policy));
    }
}

/// <summary>Đánh giá <see cref="PermissionRequirement"/> và <see cref="LegacyPolicyRequirement"/> từ <see cref="IPermissionResolver"/>.</summary>
public sealed class PermissionAuthorizationHandler : IAuthorizationHandler
{
    private readonly IPermissionResolver _resolver;

    /// <summary>Khởi tạo handler.</summary>
    public PermissionAuthorizationHandler(IPermissionResolver resolver) => _resolver = resolver;

    /// <inheritdoc />
    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        var pending = context.PendingRequirements
            .Where(r => r is PermissionRequirement or LegacyPolicyRequirement)
            .ToList();
        if (pending.Count == 0)
            return;

        var userId = context.User.GetUserId();
        if (userId == null)
            return;

        var permissions = await _resolver.GetAsync(userId.Value);
        foreach (var requirement in pending)
        {
            var satisfied = requirement switch
            {
                PermissionRequirement p => permissions.Has(p.Permission),
                LegacyPolicyRequirement l => l.Evaluate(permissions),
                _ => false
            };
            if (satisfied)
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
                .Select(r => r switch
                {
                    PermissionRequirement p => DisplayNameOf(p.Permission),
                    LegacyPolicyRequirement l => l.DisplayName,
                    _ => null
                })
                .Where(name => name != null)
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
                    var message = $"Bạn không có quyền \"{string.Join("\", \"", names)}\" để thực hiện thao tác này. "
                        + "Hãy liên hệ quản trị hệ thống nếu cần được cấp quyền.";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(ApiResponse.Fail(message), JsonOptions));
                }
                return;
            }
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }

    /// <summary>Tên hiển thị của mã quyền (mới hoặc cũ).</summary>
    private static string DisplayNameOf(string code)
    {
        var definition = PermissionCodes.Find(code);
        if (definition != null)
            return definition.Name;

        // Mã cũ: dùng tên của mã mới tương đương nếu có, tránh lộ mã kỹ thuật.
        if (LegacyPermissionMap.OldToNew.TryGetValue(code, out var mapped) && mapped.Length > 0)
            return string.Join(" / ", mapped.Select(PermissionCodes.DisplayName));

        return code switch
        {
            AppPermissions.BranchesRead => "Xem tổ chức Chi bộ",
            AppPermissions.AttachmentsRead => "Xem & tải tài liệu",
            _ => "Quyền được yêu cầu"
        };
    }
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
