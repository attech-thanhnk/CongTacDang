using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Organization;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>Màn hình "Người thực hiện": ai làm được việc gì tại một đơn vị (gán ở đơn vị cha thấy ở đơn vị con).</summary>
[Collection(ApiCollection.Name)]
public sealed class PerformerApiTests
{
    private readonly ApiFactory _factory;

    public PerformerApiTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task Performers_FollowGuardRules_AndAddThroughAssignmentApi()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var adminUser = await _factory.CreateUserAsync();
        await _factory.AssignAsync(adminUser.Id, await _factory.GetAdministratorRoleIdAsync(), RoleScopeType.Global, null);
        using var admin = await _factory.LoginAsAsync(adminUser.Username, adminUser.Password, distinctClientIp: true);

        var sfx = Guid.NewGuid().ToString("N")[..6];
        Guid parentId = Guid.Empty, childId = Guid.Empty, cellId = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var parent = new AdministrativeDepartment { Code = $"IT-PF-P-{sfx}", Name = $"Khối thực hiện {sfx}" };
            parent.Path = OrgTree.BuildPath(null, parent.Id);
            var child = new AdministrativeDepartment { Code = $"IT-PF-C-{sfx}", Name = $"Phòng thực hiện {sfx}", ParentId = parent.Id };
            child.Path = OrgTree.BuildPath(parent.Path, child.Id);
            var cell = new PartyCell { Code = $"IT-PF-B-{sfx}", Name = $"Chi bộ thực hiện {sfx}" };
            cell.Path = OrgTree.BuildPath(null, cell.Id);
            db.AddRange(parent, child, cell);
            await db.SaveChangesAsync();
            (parentId, childId, cellId) = (parent.Id, child.Id, cell.Id);
        });

        var role = await _factory.CreateRoleAsync(PermissionCodes.EvaluationAppraise, PermissionCodes.EvaluationRead);
        var atParent = await _factory.CreateUserAsync(fullName: $"Gán ở cha {sfx}");
        await _factory.AssignAsync(atParent.Id, role.Id, RoleScopeType.Department, parentId);
        var locked = await _factory.CreateUserAsync(fullName: $"Bị khóa {sfx}");
        await _factory.AssignAsync(locked.Id, role.Id, RoleScopeType.Department, childId);
        var expired = await _factory.CreateUserAsync(fullName: $"Hết hạn {sfx}");
        var expiredAssignment = await _factory.AssignAsync(expired.Id, role.Id, RoleScopeType.Department, childId);
        var atCell = await _factory.CreateUserAsync(fullName: $"Gán ở chi bộ {sfx}");
        await _factory.AssignAsync(atCell.Id, role.Id, RoleScopeType.PartyCell, cellId);

        await _factory.WithDbAsync(async db =>
        {
            var user = await db.PartyMemberProfiles.SingleAsync(u => u.Id == locked.Id);
            user.IsActive = false;
            var assignment = await db.Set<UserRoleAssignment>().SingleAsync(a => a.Id == expiredAssignment);
            assignment.ValidFrom = DateTime.UtcNow.AddDays(-10);
            assignment.ValidTo = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        });
        var cache = _factory.Services.GetRequiredService<PermissionCache>();
        cache.InvalidateUser(locked.Id);
        cache.InvalidateUser(expired.Id);

        // Đơn vị con: thấy người gán ở đơn vị cha (không gỡ được ở đây), không thấy tài khoản khóa, bản gán hết hạn, gán bên tổ chức Đảng.
        var child = await Data(await admin.GetAsync($"/api/admin/performers?departmentId={childId}"));
        Assert.Equal($"Phòng thực hiện {sfx}", child.GetProperty("scopeName").GetString());
        var appraise = Job(child, PermissionCodes.EvaluationAppraise);
        Assert.Equal(PermissionCodes.DisplayName(PermissionCodes.EvaluationAppraise), appraise.GetProperty("permissionName").GetString());
        var people = appraise.GetProperty("people").EnumerateArray().ToList();
        var parentRow = Assert.Single(people, p => p.GetProperty("userId").GetGuid() == atParent.Id);
        Assert.False(parentRow.GetProperty("assignedHere").GetBoolean());
        Assert.Equal($"Khối thực hiện {sfx}", parentRow.GetProperty("scopeText").GetString());
        Assert.DoesNotContain(people, p => p.GetProperty("userId").GetGuid() == locked.Id);
        Assert.DoesNotContain(people, p => p.GetProperty("userId").GetGuid() == expired.Id);
        Assert.DoesNotContain(people, p => p.GetProperty("userId").GetGuid() == atCell.Id);
        Assert.Contains(appraise.GetProperty("roles").EnumerateArray(), r => r.GetProperty("id").GetGuid() == role.Id);
        // Vai trò không chứa quyền thì không có trong danh sách chọn của việc đó.
        Assert.DoesNotContain(Job(child, PermissionCodes.EvaluationPublish).GetProperty("roles").EnumerateArray(),
            r => r.GetProperty("id").GetGuid() == role.Id);

        // Đơn vị cha: chính bản gán đó, gỡ được tại đây.
        var parent = await Data(await admin.GetAsync($"/api/admin/performers?departmentId={parentId}"));
        Assert.True(Assert.Single(Job(parent, PermissionCodes.EvaluationAppraise).GetProperty("people").EnumerateArray(),
            p => p.GetProperty("userId").GetGuid() == atParent.Id).GetProperty("assignedHere").GetBoolean());

        // Tổ chức Đảng: chỉ thấy người gán bên cây tổ chức Đảng.
        var cell = Job(await Data(await admin.GetAsync($"/api/admin/performers?partyCellId={cellId}")), PermissionCodes.EvaluationAppraise);
        Assert.Contains(cell.GetProperty("people").EnumerateArray(), p => p.GetProperty("userId").GetGuid() == atCell.Id);
        Assert.DoesNotContain(cell.GetProperty("people").EnumerateArray(), p => p.GetProperty("userId").GetGuid() == atParent.Id);

        // Toàn công ty: người gán theo đơn vị không tính.
        var global = Job(await Data(await admin.GetAsync("/api/admin/performers")), PermissionCodes.EvaluationAppraise);
        Assert.DoesNotContain(global.GetProperty("people").EnumerateArray(), p => p.GetProperty("userId").GetGuid() == atParent.Id);

        // Có cán bộ thuộc Phòng con và sinh hoạt ở Chi bộ: xem theo Phòng thấy người gán ở Chi bộ, xem theo Chi bộ thấy người gán
        // ở Phòng (khối cha) — đúng như guard xét hồ sơ của cán bộ đó; không gỡ được tại màn hình cây kia.
        var member = await _factory.CreateUserAsync(fullName: $"Cán bộ hai cây {sfx}");
        await _factory.WithDbAsync(async db =>
        {
            var user = await db.PartyMemberProfiles.SingleAsync(u => u.Id == member.Id);
            user.DepartmentId = childId;
            user.PartyCellId = cellId;
            await db.SaveChangesAsync();
        });
        var linkedChild = Job(await Data(await admin.GetAsync($"/api/admin/performers?departmentId={childId}")), PermissionCodes.EvaluationAppraise);
        var cellRow = Assert.Single(linkedChild.GetProperty("people").EnumerateArray(), p => p.GetProperty("userId").GetGuid() == atCell.Id);
        Assert.False(cellRow.GetProperty("assignedHere").GetBoolean());
        Assert.Equal($"Chi bộ thực hiện {sfx}", cellRow.GetProperty("scopeText").GetString());
        var linkedCell = Job(await Data(await admin.GetAsync($"/api/admin/performers?partyCellId={cellId}")), PermissionCodes.EvaluationAppraise);
        Assert.False(Assert.Single(linkedCell.GetProperty("people").EnumerateArray(), p => p.GetProperty("userId").GetGuid() == atParent.Id)
            .GetProperty("assignedHere").GetBoolean());

        // "Thêm người" = API gán vai trò sẵn có tại đơn vị đang xem → hiện ngay, gỡ được tại đây.
        var added = await _factory.CreateUserAsync(fullName: $"Thêm mới {sfx}");
        var created = await Data(await admin.PostAsJsonAsync("/api/admin/assignments",
            new { userId = added.Id, roleId = role.Id, scopeType = "Department", scopeId = childId, note = "Thêm từ màn hình người thực hiện" }));
        var refreshed = Job(await Data(await admin.GetAsync($"/api/admin/performers?departmentId={childId}")), PermissionCodes.EvaluationAppraise);
        var addedRow = Assert.Single(refreshed.GetProperty("people").EnumerateArray(), p => p.GetProperty("userId").GetGuid() == added.Id);
        Assert.True(addedRow.GetProperty("assignedHere").GetBoolean());
        Assert.Equal(created.GetProperty("id").GetGuid(), addedRow.GetProperty("assignmentId").GetGuid());

        // Chọn cả hai loại đơn vị → 400; đơn vị không tồn tại → 404.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.GetAsync($"/api/admin/performers?departmentId={childId}&partyCellId={cellId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/admin/performers?departmentId={Guid.NewGuid()}")).StatusCode);

        // Không có quyền gán vai trò → 403 (kể cả khi xem được tài khoản).
        var reader = await _factory.CreateUserWithPermissionsAsync(PermissionCodes.SystemUsersRead);
        using var readerClient = await _factory.LoginAsAsync(reader.Username, reader.Password, distinctClientIp: true);
        Assert.Equal(HttpStatusCode.Forbidden, (await readerClient.GetAsync($"/api/admin/performers?departmentId={childId}")).StatusCode);
    }

    private static JsonElement Job(JsonElement unit, string permission) =>
        unit.GetProperty("jobs").EnumerateArray().Single(j => j.GetProperty("permission").GetString() == permission);

    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("data").Clone();
    }
}
