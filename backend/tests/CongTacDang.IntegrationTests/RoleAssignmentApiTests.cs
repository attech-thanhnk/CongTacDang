using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Task 09: API quản trị vai trò / gán vai trò trên PostgreSQL thật, các chốt chặn (tự gán, vai trò bảo vệ,
/// thu hồi quản trị cuối) và "thay đổi có hiệu lực ngay" (không chờ token hết hạn).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class RoleAssignmentApiTests
{
    private readonly ApiFactory _factory;

    public RoleAssignmentApiTests(ApiFactory factory) => _factory = factory;

    private void SkipIfNoDatabase() => Skip.If(_factory.SkipReason != null, _factory.SkipReason);

    private async Task<(TestUser User, HttpClient Client)> AdministratorAsync()
    {
        var user = await _factory.CreateUserAsync();
        await _factory.AssignAsync(user.Id, await _factory.GetRoleIdAsync("QUAN_TRI_HE_THONG"), RoleScopeType.Global, null);
        return (user, await _factory.LoginAsAsync(user.Username, user.Password, distinctClientIp: true));
    }

    [SkippableFact]
    public async Task RoleAndAssignmentLifecycle_ThroughApi()
    {
        SkipIfNoDatabase();
        var (_, admin) = await AdministratorAsync();
        Guid deptId = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var dept = new AdministrativeDepartment { Code = "IT-RA-" + Guid.NewGuid().ToString("N")[..6], Name = "Phòng gán vai trò" };
            db.AdministrativeDepartments.Add(dept);
            await db.SaveChangesAsync();
            deptId = dept.Id;
        });

        // Danh mục quyền nhóm theo phân hệ, có appliesScope.
        var catalog = await Data(await admin.GetAsync("/api/admin/permissions"));
        var system = catalog.EnumerateArray().Single(m => m.GetProperty("module").GetString() == "system");
        Assert.Contains(system.GetProperty("permissions").EnumerateArray(),
            p => p.GetProperty("code").GetString() == PermissionCodes.SystemRolesManage && !p.GetProperty("appliesScope").GetBoolean());

        // Tạo vai trò → đặt quyền → đổi tên.
        var name = "Thư ký phòng " + Guid.NewGuid().ToString("N")[..6];
        var role = await Data(await admin.PostAsJsonAsync("/api/admin/roles", new { name, description = "Test", permissionCodes = new[] { PermissionCodes.EvaluationRead } }));
        var roleId = role.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/roles", new { name })).StatusCode);
        var updated = await Data(await admin.PutAsJsonAsync($"/api/admin/roles/{roleId}/permissions",
            new { permissionCodes = new[] { PermissionCodes.EvaluationRead, PermissionCodes.MeetingRead } }));
        Assert.Equal(2, updated.GetProperty("permissionCodes").GetArrayLength());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/admin/roles/{roleId}/permissions",
            new { permissionCodes = new[] { "users.read" } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/admin/roles/{roleId}", new { name = name + " (sửa)", description = "Mới" })).StatusCode);

        // Gán theo Phòng; tra cứu; "người này làm được gì".
        var target = await _factory.CreateUserAsync(deptId);
        var assignment = await Data(await admin.PostAsJsonAsync("/api/admin/assignments",
            new { userId = target.Id, roleId, scopeType = "Department", scopeId = deptId, note = "QĐ số 1" }));
        var assignmentId = assignment.GetProperty("id").GetGuid();
        Assert.Equal("Phòng gán vai trò", assignment.GetProperty("scopeName").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/assignments",
            new { userId = target.Id, roleId, scopeType = "Department", scopeId = deptId })).StatusCode); // trùng
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/assignments",
            new { userId = target.Id, roleId, scopeType = "Department" })).StatusCode); // thiếu Phòng

        var listed = await Data(await admin.GetAsync($"/api/admin/assignments?userId={target.Id}&activeOn={DateTime.UtcNow:O}"));
        Assert.Contains(listed.EnumerateArray(), a => a.GetProperty("id").GetGuid() == assignmentId);

        var effective = await Data(await admin.GetAsync($"/api/admin/users/{target.Id}/effective-permissions"));
        var read = effective.GetProperty("permissions").EnumerateArray().Single(p => p.GetProperty("code").GetString() == PermissionCodes.EvaluationRead);
        var source = Assert.Single(read.GetProperty("sources").EnumerateArray());
        Assert.Equal("Department", source.GetProperty("scopeType").GetString());
        Assert.Equal("Phòng gán vai trò", source.GetProperty("scopeName").GetString());

        // Vai trò đang được gán theo Phòng không nhận quyền chỉ áp dụng Toàn công ty.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/admin/roles/{roleId}/permissions",
            new { permissionCodes = new[] { PermissionCodes.EvaluationRead, PermissionCodes.PeriodManage } })).StatusCode);

        // Xóa vai trò đang được gán → 409 kèm số bản gán; kết thúc bản gán → xóa được.
        var conflict = await admin.DeleteAsync($"/api/admin/roles/{roleId}");
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Contains("1 bản gán", await Message(conflict), StringComparison.Ordinal);
        var ended = await Data(await admin.PostAsync($"/api/admin/assignments/{assignmentId}/end", null));
        Assert.Equal("Expired", ended.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/admin/roles/{roleId}")).StatusCode);
    }

    [SkippableFact]
    public async Task Guardrail_CannotAssignSelf_NorDeleteProtectedRole()
    {
        SkipIfNoDatabase();
        var (adminUser, admin) = await AdministratorAsync();
        var evaluatee = await _factory.GetRoleIdAsync("NGUOI_DUOC_DANH_GIA");

        var self = await admin.PostAsJsonAsync("/api/admin/assignments", new { userId = adminUser.Id, roleId = evaluatee, scopeType = "Global" });
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        Assert.Contains("chính mình", await Message(self), StringComparison.Ordinal);

        var protectedRole = await _factory.GetRoleIdAsync("QUAN_TRI_HE_THONG");
        var delete = await admin.DeleteAsync($"/api/admin/roles/{protectedRole}");
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        Assert.Contains("bảo vệ", await Message(delete), StringComparison.Ordinal);

        // Không tự sửa quyền của vai trò mình đang được gán.
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PutAsJsonAsync($"/api/admin/roles/{protectedRole}/permissions",
            new { permissionCodes = PermissionCodes.All })).StatusCode);
    }

    [SkippableFact]
    public async Task Guardrail_CannotRevokeLastAdministrator()
    {
        SkipIfNoDatabase();
        var (lastAdmin, _) = await AdministratorAsync();

        // Đưa CSDL test về trạng thái "chỉ còn một quản trị": tạm kết thúc mọi bản gán khác đang cấp quyền quản trị
        // (khôi phục ở cuối test để không ảnh hưởng test khác dùng chung CSDL).
        var suspended = new List<Guid>();
        await _factory.WithDbAsync(async db =>
        {
            var now = DateTime.UtcNow;
            var adminCodes = new[] { PermissionCodes.SystemRolesManage, PermissionCodes.SystemAssignmentsManage };
            var others = await db.Set<UserRoleAssignment>()
                .Where(a => a.UserId != lastAdmin.Id && a.ValidTo == null
                    && a.Role!.Permissions.Any(p => adminCodes.Contains(p.Code)))
                .ToListAsync();
            foreach (var assignment in others)
            {
                assignment.ValidTo = now;
                suspended.Add(assignment.Id);
            }
            await db.SaveChangesAsync();
        });
        _factory.Services.GetRequiredService<PermissionCache>().InvalidateAll();

        try
        {
            // Người hỗ trợ chỉ có quyền gán vai trò → sau khi thu hồi, không còn ai có "Quản lý vai trò".
            var helperRole = await _factory.CreateRoleAsync(PermissionCodes.SystemAssignmentsManage);
            var helper = await _factory.CreateUserAsync();
            await _factory.AssignAsync(helper.Id, helperRole.Id, RoleScopeType.Global, null);
            using var helperClient = await _factory.LoginAsAsync(helper.Username, helper.Password, distinctClientIp: true);

            var assignments = await Data(await helperClient.GetAsync($"/api/admin/assignments?userId={lastAdmin.Id}"));
            var adminAssignment = assignments.EnumerateArray().Single().GetProperty("id").GetGuid();

            var end = await helperClient.PostAsync($"/api/admin/assignments/{adminAssignment}/end", null);
            Assert.Equal(HttpStatusCode.Conflict, end.StatusCode);
            Assert.Contains("Quản lý vai trò", await Message(end), StringComparison.Ordinal);
            Assert.Equal(HttpStatusCode.Conflict, (await helperClient.DeleteAsync($"/api/admin/assignments/{adminAssignment}")).StatusCode);
        }
        finally
        {
            await _factory.WithDbAsync(async db =>
            {
                var rows = await db.Set<UserRoleAssignment>().Where(a => suspended.Contains(a.Id)).ToListAsync();
                foreach (var assignment in rows)
                    assignment.ValidTo = null;
                await db.SaveChangesAsync();
            });
            _factory.Services.GetRequiredService<PermissionCache>().InvalidateAll();
        }
    }

    [SkippableFact]
    public async Task RevokingPermission_TakesEffectOnNextRequest()
    {
        SkipIfNoDatabase();
        var (_, admin) = await AdministratorAsync();
        Guid recordOwnerId = Guid.Empty, recordId = Guid.Empty, cellId = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var cell = new PartyCell { Code = "IT-RV-" + Guid.NewGuid().ToString("N")[..6], Name = "Chi bộ thu hồi" };
            var period = new EvaluationPeriod { Year = 2032, Quarter = EvaluationQuarter.Quy2, Name = "Kỳ thu hồi", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(30) };
            db.AddRange(cell, period);
            await db.SaveChangesAsync();
            cellId = cell.Id;
            var owner = new PartyMemberProfile { Username = "it_rv_" + Guid.NewGuid().ToString("N")[..6], FullName = "Chủ hồ sơ", PartyCellId = cell.Id };
            db.PartyMemberProfiles.Add(owner);
            await db.SaveChangesAsync();
            var record = new EvaluationRecord { PeriodId = period.Id, MemberId = owner.Id, PartyCellId = cell.Id };
            db.EvaluationRecords.Add(record);
            await db.SaveChangesAsync();
            (recordOwnerId, recordId) = (owner.Id, record.Id);
        });

        var reader = await _factory.CreateUserAsync();
        var reading = await Data(await admin.PostAsJsonAsync("/api/admin/assignments",
            new { userId = reader.Id, roleId = await _factory.GetRoleIdAsync("CHI_UY_CHI_BO"), scopeType = "PartyCell", scopeId = cellId }));
        using var readerClient = await _factory.LoginAsAsync(reader.Username, reader.Password, distinctClientIp: true);
        Assert.Equal(HttpStatusCode.OK, (await readerClient.GetAsync($"/api/evaluations/records/{recordId}")).StatusCode);

        // Thu hồi qua API → request kế tiếp (cùng access token) bị 403 ngay.
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/assignments/{reading.GetProperty("id").GetGuid()}/end", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await readerClient.GetAsync($"/api/evaluations/records/{recordId}")).StatusCode);
        Assert.NotEqual(Guid.Empty, recordOwnerId);
    }

    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("data").Clone();
    }

    private static async Task<string> Message(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("message").GetString() ?? string.Empty;
    }
}
