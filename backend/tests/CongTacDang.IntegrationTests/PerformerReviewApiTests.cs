using System.Net;
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

/// <summary>
/// "Người thực hiện" — chức vụ tại đơn vị (<c>holders</c>), quan hệ người được giao với nơi giao (<c>positionAtScope</c>,
/// tính cả chức vụ ở đơn vị cấp trên và "Cán bộ của đơn vị") và danh sách bản gán cần xem lại
/// (<c>GET /api/admin/performers/review</c>).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PerformerReviewApiTests
{
    private readonly ApiFactory _factory;

    public PerformerReviewApiTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task Holders_PositionAtScope_AndReview()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var adminUser = await _factory.CreateUserAsync();
        await _factory.AssignAsync(adminUser.Id, await _factory.GetAdministratorRoleIdAsync(), RoleScopeType.Global, null);
        using var admin = await _factory.LoginAsAsync(adminUser.Username, adminUser.Password, distinctClientIp: true);

        var sfx = Guid.NewGuid().ToString("N")[..6];
        Guid parentId = Guid.Empty, childId = Guid.Empty, otherId = Guid.Empty, cellId = Guid.Empty;
        Guid deputyDirectorPos = Guid.Empty, headPos = Guid.Empty, viceHeadPos = Guid.Empty, secretaryPos = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var parent = new AdministrativeDepartment { Code = $"IT-RV-P-{sfx}", Name = $"Công ty xem lại {sfx}" };
            parent.Path = OrgTree.BuildPath(null, parent.Id);
            var child = new AdministrativeDepartment { Code = $"IT-RV-C-{sfx}", Name = $"Phòng xem lại {sfx}", ParentId = parent.Id };
            child.Path = OrgTree.BuildPath(parent.Path, child.Id);
            var other = new AdministrativeDepartment { Code = $"IT-RV-O-{sfx}", Name = $"Phòng khác {sfx}" };
            other.Path = OrgTree.BuildPath(null, other.Id);
            var cell = new PartyCell { Code = $"IT-RV-B-{sfx}", Name = $"Chi bộ xem lại {sfx}" };
            cell.Path = OrgTree.BuildPath(null, cell.Id);
            var deputy = new Position { Name = $"Phó Giám đốc {sfx}", Side = PositionSide.Administrative, SortOrder = 1 };
            var head = new Position { Name = $"Trưởng phòng {sfx}", Side = PositionSide.Administrative, SortOrder = 2 };
            var vice = new Position { Name = $"Phó Trưởng phòng {sfx}", Side = PositionSide.Administrative, SortOrder = 3 };
            var secretary = new Position { Name = $"Bí thư Chi bộ {sfx}", Side = PositionSide.Party, SortOrder = 1 };
            db.AddRange(parent, child, other, cell, deputy, head, vice, secretary);
            await db.SaveChangesAsync();
            (parentId, childId, otherId, cellId) = (parent.Id, child.Id, other.Id, cell.Id);
            (deputyDirectorPos, headPos, viceHeadPos, secretaryPos) = (deputy.Id, head.Id, vice.Id, secretary.Id);
        });

        var role = await _factory.CreateRoleAsync(PermissionCodes.EvaluationAppraise, PermissionCodes.EvaluationRead);

        var deputyDirector = await _factory.CreateUserAsync(fullName: $"Phó GĐ {sfx}");
        var head = await _factory.CreateUserAsync(departmentId: childId, fullName: $"Trưởng phòng {sfx}");
        var viceHead = await _factory.CreateUserAsync(departmentId: childId, fullName: $"Phó phòng {sfx}");
        var lockedHolder = await _factory.CreateUserAsync(departmentId: childId, fullName: $"Giữ chức bị khóa {sfx}");
        var secretary = await _factory.CreateUserAsync(partyCellId: cellId, fullName: $"Bí thư {sfx}");
        var member = await _factory.CreateUserAsync(departmentId: childId, fullName: $"Cán bộ phòng {sfx}");
        var stale = await _factory.CreateUserAsync(departmentId: otherId, fullName: $"Đã luân chuyển {sfx}");
        var temporary = await _factory.CreateUserAsync(departmentId: otherId, fullName: $"Phụ trách tạm {sfx}");
        var expiring = await _factory.CreateUserAsync(departmentId: otherId, fullName: $"Sắp hết hạn {sfx}");
        var global = await _factory.CreateUserAsync(departmentId: otherId, fullName: $"Toàn công ty {sfx}");

        await _factory.WithDbAsync(async db =>
        {
            var from = DateTime.UtcNow.AddDays(-30);
            db.AddRange(
                new MemberPosition { UserId = deputyDirector.Id, PositionId = deputyDirectorPos, DepartmentId = parentId, ValidFrom = from },
                new MemberPosition { UserId = viceHead.Id, PositionId = viceHeadPos, DepartmentId = childId, ValidFrom = from },
                new MemberPosition { UserId = head.Id, PositionId = headPos, DepartmentId = childId, ValidFrom = from },
                new MemberPosition { UserId = lockedHolder.Id, PositionId = headPos, DepartmentId = childId, ValidFrom = from },
                new MemberPosition { UserId = secretary.Id, PositionId = secretaryPos, PartyCellId = cellId, ValidFrom = from },
                // Chức vụ đã kết thúc ở Phòng → không còn tính.
                new MemberPosition { UserId = stale.Id, PositionId = headPos, DepartmentId = childId, ValidFrom = from, ValidTo = DateTime.UtcNow.AddDays(-1) });
            (await db.PartyMemberProfiles.SingleAsync(u => u.Id == lockedHolder.Id)).IsActive = false;
            await db.SaveChangesAsync();
        });

        var deputyAssignment = await _factory.AssignAsync(deputyDirector.Id, role.Id, RoleScopeType.Department, childId);
        var headAssignment = await _factory.AssignAsync(head.Id, role.Id, RoleScopeType.Department, childId);
        var secretaryAssignment = await _factory.AssignAsync(secretary.Id, role.Id, RoleScopeType.PartyCell, cellId);
        var memberAssignment = await _factory.AssignAsync(member.Id, role.Id, RoleScopeType.Department, parentId);
        var staleAssignment = await _factory.AssignAsync(stale.Id, role.Id, RoleScopeType.Department, childId);
        var temporaryAssignment = await _factory.AssignAsync(temporary.Id, role.Id, RoleScopeType.Department, childId);
        var expiringAssignment = await _factory.AssignAsync(expiring.Id, role.Id, RoleScopeType.Department, childId);
        var globalAssignment = await _factory.AssignAsync(global.Id, role.Id, RoleScopeType.Global, null);

        var expiringValidTo = DateTime.UtcNow.AddDays(3);
        await _factory.WithDbAsync(async db =>
        {
            (await db.Set<UserRoleAssignment>().SingleAsync(a => a.Id == temporaryAssignment)).ValidTo = DateTime.UtcNow.AddDays(30);
            (await db.Set<UserRoleAssignment>().SingleAsync(a => a.Id == expiringAssignment)).ValidTo = expiringValidTo;
            await db.SaveChangesAsync();
        });
        var cache = _factory.Services.GetRequiredService<PermissionCache>();
        cache.InvalidateUser(temporary.Id);
        cache.InvalidateUser(expiring.Id);

        // Chức vụ tại Phòng: theo thứ tự chức vụ, bỏ tài khoản bị khóa; người giữ chức ở đơn vị cha không tính.
        var childView = await Data(await admin.GetAsync($"/api/admin/performers?departmentId={childId}"));
        var holders = childView.GetProperty("holders").EnumerateArray().ToList();
        Assert.Equal(new[] { head.Id, viceHead.Id }, holders.Select(h => h.GetProperty("userId").GetGuid()).ToArray());
        Assert.Equal($"Trưởng phòng {sfx}", holders[0].GetProperty("positionName").GetString());
        Assert.Equal(head.Username, holders[0].GetProperty("username").GetString());

        // Quan hệ với nơi giao: chức vụ tại chính nơi giao / đơn vị cấp trên; không quan hệ → null.
        var people = Job(childView, PermissionCodes.EvaluationAppraise).GetProperty("people").EnumerateArray().ToList();
        string? Relation(Guid assignmentId) =>
            people.Single(p => p.GetProperty("assignmentId").GetGuid() == assignmentId).GetProperty("positionAtScope").GetString();
        Assert.Equal($"Trưởng phòng {sfx}", Relation(headAssignment));
        Assert.Equal($"Phó Giám đốc {sfx}", Relation(deputyAssignment));
        Assert.Equal("Cán bộ của đơn vị", Relation(memberAssignment)); // gán ở Công ty, là cán bộ Phòng con
        Assert.Null(Relation(staleAssignment));
        Assert.Null(Relation(temporaryAssignment));
        Assert.Null(Relation(globalAssignment));

        // Tổ chức Đảng: Bí thư giữ chức ở đúng Chi bộ được giao.
        var cellView = await Data(await admin.GetAsync($"/api/admin/performers?partyCellId={cellId}"));
        var cellHolder = Assert.Single(cellView.GetProperty("holders").EnumerateArray());
        Assert.Equal(secretary.Id, cellHolder.GetProperty("userId").GetGuid());
        Assert.Equal($"Bí thư Chi bộ {sfx}", Assert.Single(
            Job(cellView, PermissionCodes.EvaluationAppraise).GetProperty("people").EnumerateArray(),
            p => p.GetProperty("assignmentId").GetGuid() == secretaryAssignment).GetProperty("positionAtScope").GetString());

        // Toàn công ty: không có danh sách chức vụ.
        Assert.Empty((await Data(await admin.GetAsync("/api/admin/performers"))).GetProperty("holders").EnumerateArray());

        // Cần xem lại: (a) gán không thời hạn mà không còn quan hệ với nơi giao; (b) giao tạm hết hạn trong 7 ngày.
        // Giao tạm còn xa hạn, gán Toàn công ty, người có chức vụ / là cán bộ của đơn vị → không vào danh sách.
        var review = (await Data(await admin.GetAsync("/api/admin/performers/review"))).EnumerateArray().ToList();
        var staleItem = Assert.Single(review, i => i.GetProperty("assignmentId").GetGuid() == staleAssignment);
        Assert.Equal("NoPositionAtScope", staleItem.GetProperty("reasonCode").GetString());
        Assert.Equal("Không còn giữ chức vụ hay sinh hoạt ở nơi được giao", staleItem.GetProperty("reason").GetString());
        Assert.Equal($"Phòng xem lại {sfx}", staleItem.GetProperty("scopeText").GetString());
        Assert.Equal("Department", staleItem.GetProperty("scopeType").GetString());
        var expiringItem = Assert.Single(review, i => i.GetProperty("assignmentId").GetGuid() == expiringAssignment);
        Assert.Equal("ExpiringSoon", expiringItem.GetProperty("reasonCode").GetString());
        Assert.Equal($"Giao tạm, hết hạn ngày {expiringValidTo.AddTicks(-1).AddHours(7):dd/MM/yyyy}", expiringItem.GetProperty("reason").GetString());
        foreach (var notFlagged in new[] { deputyAssignment, headAssignment, secretaryAssignment, memberAssignment, temporaryAssignment, globalAssignment })
            Assert.DoesNotContain(review, i => i.GetProperty("assignmentId").GetGuid() == notFlagged);

        // Lọc theo người.
        var mine = (await Data(await admin.GetAsync($"/api/admin/performers/review?userId={stale.Id}"))).EnumerateArray().ToList();
        Assert.Equal(staleAssignment, Assert.Single(mine).GetProperty("assignmentId").GetGuid());
        Assert.Empty((await Data(await admin.GetAsync($"/api/admin/performers/review?userId={head.Id}"))).EnumerateArray());

        // Tài khoản bị khóa không vào danh sách.
        await _factory.WithDbAsync(async db =>
        {
            (await db.PartyMemberProfiles.SingleAsync(u => u.Id == stale.Id)).IsActive = false;
            await db.SaveChangesAsync();
        });
        Assert.Empty((await Data(await admin.GetAsync($"/api/admin/performers/review?userId={stale.Id}"))).EnumerateArray());

        // Không có quyền gán vai trò → 403.
        var reader = await _factory.CreateUserWithPermissionsAsync(PermissionCodes.SystemUsersRead);
        using var readerClient = await _factory.LoginAsAsync(reader.Username, reader.Password, distinctClientIp: true);
        Assert.Equal(HttpStatusCode.Forbidden, (await readerClient.GetAsync("/api/admin/performers/review")).StatusCode);
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
