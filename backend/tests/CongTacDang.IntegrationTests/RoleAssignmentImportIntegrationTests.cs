using System.Net;
using System.Net.Http.Json;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Test tích hợp import gán vai trò (task 13, T-64) trên CSDL dùng chung của collection <c>api</c>:
/// quyền dùng loại import, file mẫu, phân tích lại trước khi ghi (không ghi dòng nào khi dữ liệu đã đổi),
/// và trường <c>grants</c> của <c>/api/auth/me</c> (bước 3 kịch bản go-live).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class RoleAssignmentImportIntegrationTests
{
    private static readonly string[] Headers =
        { "Tên đăng nhập", "Tên vai trò", "Loại phạm vi", "Mã Phòng/Chi bộ", "Từ ngày", "Đến ngày", "Ghi chú" };

    private readonly ApiFactory _factory;

    public RoleAssignmentImportIntegrationTests(ApiFactory factory) => _factory = factory;

    private void SkipIfNoDatabase() => Skip.If(_factory.SkipReason != null, _factory.SkipReason);

    [SkippableFact]
    public async Task Kind_RequiresImportAndAssignmentsManage_TemplateDownloads()
    {
        SkipIfNoDatabase();

        var importOnly = await _factory.CreateUserWithPermissionsAsync(PermissionCodes.SystemImport, PermissionCodes.CatalogManage);
        using var importOnlyClient = await _factory.LoginAsAsync(importOnly.Username, importOnly.Password, distinctClientIp: true);
        var kinds = await GoLiveHttp.DataAsync(await importOnlyClient.GetAsync("/api/imports/kinds"));
        Assert.DoesNotContain(kinds.EnumerateArray(), k => k.GetProperty("kind").GetString() == "role-assignments");
        var forbidden = await importOnlyClient.GetAsync("/api/imports/role-assignments/template");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Contains("Gán vai trò", (await GoLiveHttp.JsonAsync(forbidden)).GetProperty("message").GetString());

        var admin = await _factory.CreateUserWithPermissionsAsync(PermissionCodes.SystemImport, PermissionCodes.SystemAssignmentsManage);
        using var adminClient = await _factory.LoginAsAsync(admin.Username, admin.Password, distinctClientIp: true);
        var adminKinds = await GoLiveHttp.DataAsync(await adminClient.GetAsync("/api/imports/kinds"));
        var kind = adminKinds.EnumerateArray().Single(k => k.GetProperty("kind").GetString() == "role-assignments");
        Assert.Equal(Headers, kind.GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("header").GetString()));
        var template = await adminClient.GetAsync("/api/imports/role-assignments/template");
        Assert.Equal(HttpStatusCode.OK, template.StatusCode);
        Assert.True((await template.Content.ReadAsByteArrayAsync()).Length > 0);
    }

    [SkippableFact]
    public async Task Commit_ReanalyzesAndWritesNothing_WhenAnAssignmentAppearedAfterPreview()
    {
        SkipIfNoDatabase();

        var admin = await _factory.CreateUserWithPermissionsAsync(PermissionCodes.SystemImport, PermissionCodes.SystemAssignmentsManage);
        using var client = await _factory.LoginAsAsync(admin.Username, admin.Password, distinctClientIp: true);
        var role = await _factory.CreateRoleAsync(PermissionCodes.EvaluationSelf);
        var roleName = await RoleNameAsync(role.Id);
        var first = await _factory.CreateUserAsync();
        var second = await _factory.CreateUserAsync();

        var file = GoLiveHttp.BuildFile(Headers,
            new[] { first.Username, roleName, "Toàn công ty", "", "", "", "" },
            new[] { second.Username, roleName, "Toàn công ty", "", "", "", "" });
        var preview = await GoLiveHttp.PreviewAsync(client, "role-assignments", file);
        Assert.True(preview.GetProperty("canCommit").GetBoolean(), preview.ToString());

        // Sau khi xem trước, người thứ hai được gán cùng vai trò ở chỗ khác → khi xác nhận, dòng 3 thành lỗi.
        await _factory.AssignAsync(second.Id, role.Id, RoleScopeType.Global, null);

        var commit = await client.PostAsync($"/api/imports/{preview.GetProperty("sessionId").GetGuid()}/commit", null);
        Assert.Equal(HttpStatusCode.BadRequest, commit.StatusCode);
        var message = (await GoLiveHttp.JsonAsync(commit)).GetProperty("message").GetString();
        Assert.Contains("dòng 3", message);
        Assert.Contains("Không dòng nào được ghi", message);

        await _factory.WithDbAsync(async db =>
        {
            Assert.Equal(0, await db.Set<UserRoleAssignment>().CountAsync(a => a.UserId == first.Id));
            Assert.Equal(1, await db.Set<UserRoleAssignment>().CountAsync(a => a.UserId == second.Id));
        });

        // Tải lại tệp đã sửa (bỏ dòng trùng) → ghi được, bản gán ghi qua service (có audit, có người tạo).
        var fixedFile = GoLiveHttp.BuildFile(Headers, new[] { first.Username, roleName, "Toàn công ty", "", "", "", "Tệp đã sửa" });
        await GoLiveHttp.ImportAsync(client, "role-assignments", fixedFile, expectedCreated: 1);
        await _factory.WithDbAsync(async db =>
        {
            var created = await db.Set<UserRoleAssignment>().SingleAsync(a => a.UserId == first.Id);
            Assert.Equal(admin.Id, created.CreatedBy);
            Assert.Equal("Tệp đã sửa", created.Note);
            Assert.True(await db.AuditLogs.AnyAsync(l => l.EntityType == nameof(UserRoleAssignment) && l.EntityId == $"Id={created.Id}" && l.ActorId == admin.Id));
        });
    }

    /// <summary>
    /// Bước 3 kịch bản go-live, phần <c>grants</c>: <c>/api/auth/me</c> trả quyền kèm phạm vi theo bản gán.
    /// <b>Cần phối hợp:</b> trường <c>grants</c> do tích hợp Đợt 4 thêm vào <c>AuthController.Me</c>; trên nền chưa có trường này
    /// test tự skip kèm lý do và sẽ tự chạy khi trường xuất hiện.
    /// </summary>
    [SkippableFact]
    public async Task GoLive_Step3_AuthMe_ReturnsGrantsWithScope()
    {
        SkipIfNoDatabase();

        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var cell = new PartyCell { Code = $"CB-G{suffix}", Name = $"Chi bộ grants {suffix}" };
        await _factory.WithDbAsync(async db =>
        {
            db.PartyCells.Add(cell);
            await db.SaveChangesAsync();
        });
        var role = await _factory.CreateRoleAsync(PermissionCodes.EvaluationRead, PermissionCodes.EvaluationCellConfirm);
        var user = await _factory.CreateUserAsync(partyCellId: cell.Id);
        await _factory.AssignAsync(user.Id, role.Id, RoleScopeType.PartyCell, cell.Id);

        using var client = await _factory.LoginAsAsync(user.Username, user.Password, distinctClientIp: true);
        var me = await GoLiveHttp.DataAsync(await client.GetAsync("/api/auth/me"));
        Skip.IfNot(me.TryGetProperty("grants", out var grants),
            "Chờ tích hợp Đợt 4: /api/auth/me chưa có trường grants (IRoleAssignmentService.GetGrantsAsync). "
            + "Test tự chạy khi trường này có mặt.");

        var items = grants.EnumerateArray()
            .Select(g => (Code: g.GetProperty("code").GetString(), Scope: g.GetProperty("scopeType").GetString(),
                ScopeId: g.TryGetProperty("scopeId", out var id) && id.ValueKind == System.Text.Json.JsonValueKind.String ? id.GetGuid() : (Guid?)null,
                Name: g.GetProperty("scopeName").GetString()))
            .ToList();
        Assert.Contains(items, g => g.Code == PermissionCodes.EvaluationCellConfirm && g.Scope == "PartyCell" && g.ScopeId == cell.Id && g.Name == cell.Name);
        Assert.Contains(items, g => g.Code == PermissionCodes.EvaluationRead && g.Scope == "PartyCell" && g.ScopeId == cell.Id);
        Assert.DoesNotContain(items, g => g.Scope == "Global");
    }

    private async Task<string> RoleNameAsync(Guid roleId)
    {
        var name = string.Empty;
        await _factory.WithDbAsync(async db => name = await db.Roles.Where(r => r.Id == roleId).Select(r => r.Name).SingleAsync());
        return name;
    }
}
