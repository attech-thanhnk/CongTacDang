using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Imports;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Infrastructure.Data;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Test tích hợp các chỗ nối giữa tài khoản, phân quyền và nhập dữ liệu: nhập cán bộ ghi cả lô trong một transaction,
/// thông tin phiên có <c>grants</c>, danh sách tài khoản lọc theo phạm vi <c>system.users.read</c>.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CrossModuleIntegrationTests
{
    private readonly ApiFactory _factory;

    public CrossModuleIntegrationTests(ApiFactory factory) => _factory = factory;

    private void SkipIfNoDatabase() => Skip.If(_factory.SkipReason != null, _factory.SkipReason);

    // ===================== Transaction nhập cán bộ =====================

    [SkippableFact]
    public async Task StagedAccounts_AreNotWritten_WhenSaveFailsInsideTransaction()
    {
        SkipIfNoDatabase();
        var existing = await _factory.CreateUserAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var first = $"tx_{suffix}_1";
        var second = $"tx_{suffix}_2";

        using (var scope = _factory.Services.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IUserAccountService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var db = scope.ServiceProvider.GetRequiredService<CongTacDangDbContext>();

            await Assert.ThrowsAsync<DbUpdateException>(() => unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await accounts.StageCreateAsync(Command(first));
                await accounts.StageCreateAsync(Command(second));

                // Trùng với tài khoản đã đưa vào nhưng chưa lưu → bị chặn ngay, không đợi tới lúc lưu.
                await Assert.ThrowsAsync<ConflictException>(() => accounts.StageCreateAsync(Command(first.ToUpperInvariant())));

                // Lỗi phát sinh ở bước ghi (vi phạm unique index) sau khi 2 tài khoản đã được đưa vào.
                db.PartyMemberProfiles.Add(new PartyMemberProfile
                {
                    Username = existing.Username,
                    FullName = "Trùng tên đăng nhập",
                    PasswordHash = "x"
                });
                await unitOfWork.SaveChangesAsync();
            }));
        }

        await _factory.WithDbAsync(async db =>
        {
            var written = await db.PartyMemberProfiles.IgnoreQueryFilters()
                .CountAsync(m => m.Username == first || m.Username == second);
            Assert.Equal(0, written);
        });
    }

    [SkippableFact]
    public async Task ImportUsers_RowFailingAtCommit_WritesNoAccount()
    {
        SkipIfNoDatabase();
        var (deptA, codeA) = await CreateDepartmentAsync();
        var (_, codeB) = await CreateDepartmentAsync();

        // Có system.import (Toàn công ty) nhưng chỉ quản lý tài khoản của Phòng A: xem trước qua được (có quyền ở một
        // phạm vi), bước ghi từ chối dòng thuộc Phòng B (403) → dòng Phòng A đứng trước cũng không được ghi.
        var importRole = await _factory.CreateRoleAsync(PermissionCodes.SystemImport);
        var manageRole = await _factory.CreateRoleAsync(PermissionCodes.SystemUsersManage);
        var importer = await _factory.CreateUserAsync();
        await _factory.AssignAsync(importer.Id, importRole.Id, RoleScopeType.Global, null);
        await _factory.AssignAsync(importer.Id, manageRole.Id, RoleScopeType.Department, deptA);
        using var client = await _factory.LoginAsAsync(importer.Username, importer.Password, distinctClientIp: true);

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var inScope = $"imp_{suffix}_a";
        var outOfScope = $"imp_{suffix}_b";
        var headers = new[] { "Tên đăng nhập", "Họ và tên", "Mã đơn vị công tác", "Thẩm quyền phê duyệt" };
        var file = BuildFile(headers,
            new[] { inScope, "Cán bộ Phòng A", codeA, "CoSo" },
            new[] { outOfScope, "Cán bộ Phòng B", codeB, "CoSo" });

        var previewResponse = await PostFileAsync(client, "users", file);
        Assert.True(previewResponse.StatusCode == HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
        var preview = await DataAsync(previewResponse);
        Assert.True(preview.GetProperty("canCommit").GetBoolean(), preview.ToString());

        var commit = await client.PostAsync($"/api/imports/{preview.GetProperty("sessionId").GetGuid()}/commit", null);
        Assert.Equal(HttpStatusCode.Forbidden, commit.StatusCode);

        await _factory.WithDbAsync(async db =>
        {
            var written = await db.PartyMemberProfiles.IgnoreQueryFilters()
                .CountAsync(m => m.Username == inScope || m.Username == outOfScope);
            Assert.Equal(0, written);
        });
    }

    // ===================== Phiên: grants + roles =====================

    [SkippableFact]
    public async Task LoginAndMe_ReturnGrantsWithScope_AndRoleNamesFromEffectiveAssignments()
    {
        SkipIfNoDatabase();
        var (deptId, _) = await CreateDepartmentAsync();
        var readerRole = await _factory.CreateRoleAsync(PermissionCodes.EvaluationRead);
        var emptyRole = await _factory.CreateRoleAsync();
        var user = await _factory.CreateUserAsync();
        await _factory.AssignAsync(user.Id, readerRole.Id, RoleScopeType.Department, deptId);
        await _factory.AssignAsync(user.Id, emptyRole.Id, RoleScopeType.Global, null);
        string readerName = string.Empty, emptyName = string.Empty, deptName = string.Empty;
        await _factory.WithDbAsync(async db =>
        {
            readerName = (await db.Roles.SingleAsync(r => r.Id == readerRole.Id)).Name;
            emptyName = (await db.Roles.SingleAsync(r => r.Id == emptyRole.Id)).Name;
            deptName = (await db.AdministrativeDepartments.SingleAsync(d => d.Id == deptId)).Name;
        });

        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add(ApiFactory.TestClientIpHeader, $"10.44.{Random.Shared.Next(0, 255)}.{Random.Shared.Next(1, 255)}");
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = user.Username, password = user.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        foreach (var session in new[] { await DataAsync(login), await DataAsync(me) })
        {
            var roles = session.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToList();
            Assert.Contains(readerName, roles);
            Assert.Contains(emptyName, roles); // vai trò chưa có quyền nào vẫn hiện tên
            Assert.Contains(PermissionCodes.EvaluationRead,
                session.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));

            var grant = Assert.Single(session.GetProperty("grants").EnumerateArray());
            Assert.Equal(PermissionCodes.EvaluationRead, grant.GetProperty("code").GetString());
            Assert.Equal("Department", grant.GetProperty("scopeType").GetString());
            Assert.Equal(deptId, grant.GetProperty("scopeId").GetGuid());
            Assert.Equal(deptName, grant.GetProperty("scopeName").GetString());
        }
    }

    // ===================== T-61: danh sách tài khoản theo phạm vi =====================

    [SkippableFact]
    public async Task UserLists_AreFilteredBySystemUsersReadScope()
    {
        SkipIfNoDatabase();
        var (deptA, _) = await CreateDepartmentAsync();
        var (deptB, _) = await CreateDepartmentAsync();
        var inA = await _factory.CreateUserAsync(departmentId: deptA, fullName: "Cán bộ phạm vi A");
        var inB = await _factory.CreateUserAsync(departmentId: deptB, fullName: "Cán bộ phạm vi B");

        var readRole = await _factory.CreateRoleAsync(PermissionCodes.SystemUsersRead);
        var reader = await _factory.CreateUserAsync();
        await _factory.AssignAsync(reader.Id, readRole.Id, RoleScopeType.Department, deptA);
        using var client = await _factory.LoginAsAsync(reader.Username, reader.Password, distinctClientIp: true);

        var paged = await DataAsync(await client.GetAsync("/api/users?pageSize=200"));
        var pagedIds = paged.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(inA.Id, pagedIds);
        Assert.DoesNotContain(inB.Id, pagedIds);
        Assert.DoesNotContain(reader.Id, pagedIds); // người xem không thuộc Phòng A
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/users/{inB.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/users/{inA.Id}")).StatusCode);

        // Hồ sơ theo tên đăng nhập: trong phạm vi → được xem; ngoài phạm vi → 403.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/users/profile?username={inA.Username}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/users/profile?username={inB.Username}")).StatusCode);
    }

    // ===================== Audit =====================

    [SkippableFact]
    public async Task Login_IsNotDuplicatedInAuditLog_AndSecurityFieldsAreMasked()
    {
        SkipIfNoDatabase();
        var user = await _factory.CreateUserAsync();
        var userKey = $"Id={user.Id}";
        var start = DateTime.UtcNow.AddSeconds(-1);

        // Một lần sai mật khẩu (đếm sai) và một lần đúng (lần đăng nhập cuối): chỉ ghi nhật ký đăng nhập.
        using (var anonymous = _factory.CreateClient())
        {
            anonymous.DefaultRequestHeaders.Add(ApiFactory.TestClientIpHeader, $"10.45.{Random.Shared.Next(0, 255)}.{Random.Shared.Next(1, 255)}");
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await anonymous.PostAsJsonAsync("/api/auth/login", new { username = user.Username, password = "SaiMatKhau1" })).StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await anonymous.PostAsJsonAsync("/api/auth/login", new { username = user.Username, password = user.Password })).StatusCode);
        }

        await _factory.WithDbAsync(async db =>
        {
            Assert.Equal(2, await db.Set<LoginEvent>().CountAsync(e => e.UserId == user.Id));
            Assert.False(await db.AuditLogs.AnyAsync(a => a.CreatedAt >= start && a.EntityType == nameof(LoginEvent)));
            var userLogs = await db.AuditLogs.Where(a => a.CreatedAt >= start && a.EntityId == userKey && a.Action != "Create")
                .Select(a => a.Action + " " + a.OldValues + " -> " + a.NewValues).ToListAsync();
            Assert.True(userLogs.Count == 0, string.Join(" | ", userLogs));
        });

        // Thao tác quản trị trên tài khoản vẫn được ghi audit, nhưng không lộ mật khẩu băm / dấu bảo mật.
        var admin = await _factory.CreateUserWithPermissionsAsync(PermissionCodes.SystemUsersManage);
        using var adminClient = await _factory.LoginAsAsync(admin.Username, admin.Password, distinctClientIp: true);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PostAsync($"/api/users/{user.Id}/reset-password", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PostAsync($"/api/users/{user.Id}/unlock", null)).StatusCode);

        await _factory.WithDbAsync(async db =>
        {
            var logs = await db.AuditLogs
                .Where(a => a.CreatedAt >= start && a.EntityId == userKey && a.Action != "Create")
                .ToListAsync();
            Assert.NotEmpty(logs);
            Assert.All(logs, log =>
            {
                Assert.Equal(admin.Id, log.ActorId);
                Assert.DoesNotContain("SecurityStamp", log.NewValues ?? string.Empty);
                Assert.DoesNotContain("SecurityStamp", log.OldValues ?? string.Empty);
                Assert.DoesNotContain("PasswordHash", log.NewValues ?? string.Empty);
            });
        });
    }

    // ===================== Cache quyền xóa sau commit =====================

    [SkippableFact]
    public async Task PermissionCacheInvalidation_InsideTransaction_HappensAfterCommit()
    {
        SkipIfNoDatabase();
        var role = await _factory.CreateRoleAsync(PermissionCodes.EvaluationRead);
        var user = await _factory.CreateUserAsync();

        async Task<bool> CanReadAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var resolver = scope.ServiceProvider.GetRequiredService<IPermissionResolver>();
            return (await resolver.GetAsync(user.Id)).Has(PermissionCodes.EvaluationRead);
        }

        Assert.False(await CanReadAsync()); // nạp vào cache: chưa có quyền

        using (var scope = _factory.Services.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var invalidator = scope.ServiceProvider.GetRequiredService<IAccessCacheInvalidator>();
            var db = scope.ServiceProvider.GetRequiredService<CongTacDangDbContext>();

            await unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                db.Set<UserRoleAssignment>().Add(new UserRoleAssignment
                {
                    UserId = user.Id,
                    RoleId = role.Id,
                    ScopeType = RoleScopeType.Global,
                    ValidFrom = DateTime.UtcNow.AddMinutes(-1)
                });
                await db.SaveChangesAsync();
                invalidator.InvalidateUser(user.Id);

                // Request chen giữa (chưa commit): vẫn thấy dữ liệu cũ; không được làm "đóng băng" quyền cũ sau commit.
                Assert.False(await CanReadAsync());
            });
        }

        Assert.True(await CanReadAsync()); // cache đã được xóa sau commit → thấy quyền mới ngay
    }

    // ===================== Đọc vai trò khi chỉ có quyền gán vai trò =====================

    [SkippableFact]
    public async Task AssignmentsManager_CanReadRolesAndCatalog_ButCannotChangeRoles()
    {
        SkipIfNoDatabase();
        var assigner = await _factory.CreateUserWithPermissionsAsync(PermissionCodes.SystemAssignmentsManage);
        var nobody = await _factory.CreateUserWithPermissionsAsync(PermissionCodes.EvaluationSelf);
        using var client = await _factory.LoginAsAsync(assigner.Username, assigner.Password, distinctClientIp: true);
        using var other = await _factory.LoginAsAsync(nobody.Username, nobody.Password, distinctClientIp: true);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/admin/roles/{assigner.RoleId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/permissions")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/admin/roles", new { name = $"Không được tạo {Guid.NewGuid():N}" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PutAsJsonAsync($"/api/admin/roles/{assigner.RoleId}/permissions", new { permissionCodes = Array.Empty<string>() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/admin/roles/{nobody.RoleId}")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync("/api/admin/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync("/api/admin/permissions")).StatusCode);
    }

    // ===================== Hỗ trợ =====================

    private static CreateAccountCommand Command(string username) =>
        new(username, $"Cán bộ {username}", null, null, null, null, null, ApprovalAuthority.CoSo);

    private async Task<(Guid Id, string Code)> CreateDepartmentAsync()
    {
        var code = $"PH-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var department = new AdministrativeDepartment { Code = code, Name = $"Phòng {code}", IsActive = true };
        await _factory.WithDbAsync(async db =>
        {
            db.AdministrativeDepartments.Add(department);
            await db.SaveChangesAsync();
        });
        return (department.Id, code);
    }

    private static byte[] BuildFile(string[] headers, params string[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(ImportLimits.DataSheetName);
        for (var c = 0; c < headers.Length; c++)
            sheet.Cell(1, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                sheet.Cell(r + 2, c + 1).Value = rows[r][c];
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static Task<HttpResponseMessage> PostFileAsync(HttpClient client, string kind, byte[] file)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(file);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", "du-lieu.xlsx");
        return client.PostAsync($"/api/imports/{kind}/preview", content);
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").Clone();
    }
}
