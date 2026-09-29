using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Kịch bản go-live đầu-cuối (T-68) trên PostgreSQL thật, từ <b>CSDL trống</b> (collection riêng, CSDL tạm riêng),
/// đúng thứ tự triển khai thật (docs/deployment.md mục go-live):
/// <list type="number">
/// <item>Khởi động với cấu hình <c>Seed:InitialAdmin:*</c> → tài khoản quản trị ban đầu → đăng nhập → bị buộc đổi mật khẩu → đổi.</item>
/// <item>Khai báo qua API của các trang quản trị: Danh mục (Phòng → Chi bộ) → Tài khoản (nhận mật khẩu tạm) → gán vai trò
/// (kể cả các trường hợp bị từ chối: tự gán, vai trò toàn công ty gán theo đơn vị, gán trùng).</item>
/// <item>Một cán bộ đăng nhập bằng mật khẩu tạm → đổi mật khẩu → quyền + phạm vi đúng theo bản gán.</item>
/// <item>Quản trị gỡ một bản gán → request kế tiếp của người đó bị 403 ở chức năng tương ứng.</item>
/// <item>Cơ quan thẩm định tạo kỳ, thêm người được đánh giá, mở kỳ; quản trị không xem được hồ sơ (tách quản trị kỹ thuật).</item>
/// <item>Hồ sơ đi hết các bước bật của kỳ tới <c>Published</c>, mỗi bước do đúng người có quyền.</item>
/// </list>
/// </summary>
[Collection(GoLiveCollection.Name)]
public sealed class GoLiveScenarioTests
{
    /// <summary>Tên đăng nhập / mật khẩu ban đầu của quản trị (cấu hình <c>Seed:InitialAdmin:*</c> của host).</summary>
    private const string InitialAdminUsername = "quantri";
    private const string InitialAdminPassword = "KhoiTao2026";
    private const string AdminNewPassword = "QuanTri2026Moi";
    private const string CadreNewPassword = "CanBo2026Moi";

    private readonly ApiFactory _factory;

    public GoLiveScenarioTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task GoLive_FromEmptyDatabase_DeclareCatalogCadresAssignments_ThenPermissionsFollowAssignments()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);

        // ============ Bước 1: CSDL trống → quản trị ban đầu → buộc đổi mật khẩu ============
        await _factory.WithDbAsync(async db =>
        {
            Assert.Equal(0, await db.PartyMemberProfiles.IgnoreQueryFilters().CountAsync());
            Assert.Equal(0, await db.AdministrativeDepartments.IgnoreQueryFilters().CountAsync());
            Assert.Equal(0, await db.PartyCells.IgnoreQueryFilters().CountAsync());
            Assert.True(await db.Roles.AnyAsync(r => r.IsProtected), "Seeder phải tạo vai trò quản trị được bảo vệ.");
        });
        var adminId = await BootstrapInitialAdministratorAsync();
        Assert.NotEqual(Guid.Empty, adminId);

        var admin = await _factory.LoginAsAsync(InitialAdminUsername, InitialAdminPassword, distinctClientIp: true);
        var me = await GoLiveHttp.DataAsync(await admin.GetAsync("/api/auth/me"));
        Assert.True(me.GetProperty("mustChangePassword").GetBoolean());

        var blocked = await admin.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("PASSWORD_CHANGE_REQUIRED", (await GoLiveHttp.JsonAsync(blocked)).GetProperty("code").GetString());

        await GoLiveHttp.ChangePasswordAsync(admin, InitialAdminPassword, AdminNewPassword);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/users")).StatusCode);

        // ============ Bước 2: trang Danh mục (đơn vị, tổ chức Đảng) → trang Tài khoản → gán vai trò ============
        var deptKt = await GoLiveHttp.CreateCatalogItemAsync(admin, "departments", "PH-KT", "Phòng Kỹ thuật", 1);
        var deptTccb = await GoLiveHttp.CreateCatalogItemAsync(admin, "departments", "PH-TCCB", "Phòng Tổ chức cán bộ - Lao động", 2);
        var cellKt = await GoLiveHttp.CreateCatalogItemAsync(admin, "branches", "CB-KT", "Chi bộ Kỹ thuật", 1);
        var cellVp = await GoLiveHttp.CreateCatalogItemAsync(admin, "branches", "CB-VP", "Chi bộ Văn phòng", 2);
        await _factory.WithDbAsync(async db =>
        {
            Assert.Equal(2, await db.AdministrativeDepartments.CountAsync());
            Assert.Equal(2, await db.PartyCells.CountAsync());
        });

        // Mỗi tài khoản tạo mới trả mật khẩu tạm (hiển thị một lần).
        var passwords = new Dictionary<string, string>
        {
            ["nguyen.van.a"] = await GoLiveHttp.CreateAccountAsync(admin, "nguyen.van.a", "Nguyễn Văn A", "a@attech.vn", "0100001", "Trưởng phòng Kỹ thuật", deptKt, cellKt),
            ["tran.thi.b"] = await GoLiveHttp.CreateAccountAsync(admin, "tran.thi.b", "Trần Thị B", null, "0100002", "Trưởng phòng TCCB-LĐ", deptTccb, cellVp),
            ["le.van.c"] = await GoLiveHttp.CreateAccountAsync(admin, "le.van.c", "Lê Văn C", null, null, "Kỹ sư", deptKt, cellKt)
        };
        Assert.All(passwords.Values, p => Assert.False(string.IsNullOrWhiteSpace(p)));
        var cadreId = await GetUserIdAsync("nguyen.van.a");
        var appraiserId = await GetUserIdAsync("tran.thi.b");
        var leVanCId = await GetUserIdAsync("le.van.c");

        var todayVn = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));
        // Hết hiệu lực hết ngày 31/12 năm sau (giờ Việt Nam) = 00:00 ngày 01/01 năm kế tiếp, lưu theo UTC.
        var endOfNextYearUtc = new DateTime(todayVn.Year + 2, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(-7);
        var evaluatedRole = await RoleIdByNameAsync("Người được đánh giá");
        var adminRole = await RoleIdByNameAsync("Quản trị hệ thống");

        // Gán sai: tự gán cho mình (403), vai trò quản trị gán theo đơn vị (400), gán trùng (400) → không ghi gì thêm.
        var self = await admin.PostAsJsonAsync("/api/admin/assignments", new { userId = adminId, roleId = evaluatedRole, scopeType = "Global" });
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        Assert.Contains("chính mình", (await GoLiveHttp.JsonAsync(self)).GetProperty("message").GetString());
        var adminByDept = await admin.PostAsJsonAsync("/api/admin/assignments",
            new { userId = leVanCId, roleId = adminRole, scopeType = "Department", scopeId = deptKt });
        Assert.Equal(HttpStatusCode.BadRequest, adminByDept.StatusCode);
        Assert.Contains("chỉ được gán với phạm vi Toàn công ty", (await GoLiveHttp.JsonAsync(adminByDept)).GetProperty("message").GetString());
        await GoLiveHttp.AssignAsync(admin, leVanCId, evaluatedRole, "Global", null);
        var duplicate = await admin.PostAsJsonAsync("/api/admin/assignments", new { userId = leVanCId, roleId = evaluatedRole, scopeType = "Global" });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("trùng lặp", (await GoLiveHttp.JsonAsync(duplicate)).GetProperty("message").GetString());
        await _factory.WithDbAsync(async db =>
            Assert.Equal(2, await db.Set<UserRoleAssignment>().CountAsync())); // quản trị ban đầu + le.van.c

        await GoLiveHttp.AssignAsync(admin, cadreId, evaluatedRole, "Global", null);
        await GoLiveHttp.AssignAsync(admin, cadreId, await RoleIdByNameAsync("Chi ủy / Bí thư Chi bộ"), "PartyCell", cellKt,
            validTo: endOfNextYearUtc, note: "Nghị quyết Chi bộ số 01");
        await GoLiveHttp.AssignAsync(admin, cadreId, await RoleIdByNameAsync("Lãnh đạo Phòng"), "Department", deptKt);
        await GoLiveHttp.AssignAsync(admin, cadreId, await RoleIdByNameAsync("Cấp ủy viên Đảng ủy"), "Global", null);
        await GoLiveHttp.AssignAsync(admin, appraiserId, evaluatedRole, "Global", null);
        await GoLiveHttp.AssignAsync(admin, appraiserId, await RoleIdByNameAsync("Cơ quan thẩm định (Phòng TCCB-LĐ)"), "Global", null);

        await _factory.WithDbAsync(async db =>
            Assert.Equal(8, await db.Set<UserRoleAssignment>().CountAsync()));

        // ============ Bước 3: cán bộ đăng nhập bằng mật khẩu tạm → đổi → quyền + phạm vi theo bản gán ============
        var cadre = await _factory.LoginAsAsync("nguyen.van.a", passwords["nguyen.van.a"], distinctClientIp: true);
        Assert.True((await GoLiveHttp.DataAsync(await cadre.GetAsync("/api/auth/me"))).GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await cadre.GetAsync("/api/evaluations/my-record")).StatusCode);
        await GoLiveHttp.ChangePasswordAsync(cadre, passwords["nguyen.van.a"], CadreNewPassword);

        var cadreMe = await GoLiveHttp.DataAsync(await cadre.GetAsync("/api/auth/me"));
        Assert.False(cadreMe.GetProperty("mustChangePassword").GetBoolean());
        var cadreCodes = GoLiveHttp.Strings(cadreMe.GetProperty("permissions"));
        Assert.Equal(
            new[]
            {
                // Task 20: "Người được đánh giá" có thêm xem kết quả, gửi kiến nghị; "Lãnh đạo Phòng" có thêm lập kế hoạch 30-60-90 ngày.
                "collective.manage", "evaluation.appeal.submit", "evaluation.cell.confirm", "evaluation.improvement.manage",
                "evaluation.read", "evaluation.results.view", "evaluation.self", "evaluation.tasks.approve",
                "evaluation.unit.review", // task 15: vai trò mặc định Lãnh đạo Phòng có thêm "Lãnh đạo đơn vị đề xuất"
                "meeting.read", "report.export"
            },
            cadreCodes.OrderBy(c => c, StringComparer.Ordinal));
        var cadreGrants = cadreMe.GetProperty("grants").EnumerateArray()
            .Select(g => $"{g.GetProperty("code").GetString()}@{g.GetProperty("scopeType").GetString()}:{g.GetProperty("scopeName").GetString()}").ToList();
        Assert.Contains("evaluation.cell.confirm@PartyCell:Chi bộ Kỹ thuật", cadreGrants);
        Assert.Contains("evaluation.tasks.approve@Department:Phòng Kỹ thuật", cadreGrants);

        var effective = await GoLiveHttp.DataAsync(await admin.GetAsync($"/api/admin/users/{cadreId}/effective-permissions"));
        var sources = effective.GetProperty("permissions").EnumerateArray()
            .ToDictionary(p => p.GetProperty("code").GetString()!,
                p => p.GetProperty("sources").EnumerateArray()
                    .Select(s => $"{s.GetProperty("scopeType").GetString()}:{s.GetProperty("scopeName").GetString()}:{s.GetProperty("roleName").GetString()}")
                    .OrderBy(s => s, StringComparer.Ordinal).ToList());
        Assert.Equal(new[] { "PartyCell:Chi bộ Kỹ thuật:Chi ủy / Bí thư Chi bộ" }, sources["evaluation.cell.confirm"]);
        Assert.Equal(new[] { "Department:Phòng Kỹ thuật:Lãnh đạo Phòng" }, sources["evaluation.tasks.approve"]);
        Assert.Equal(new[] { "Global:Toàn công ty:Cấp ủy viên Đảng ủy" }, sources["report.export"]);
        Assert.Equal(3, sources["evaluation.read"].Count);
        var cellAssignment = effective.GetProperty("assignments").EnumerateArray()
            .Single(a => a.GetProperty("roleName").GetString() == "Chi ủy / Bí thư Chi bộ");
        Assert.Equal("Nghị quyết Chi bộ số 01", cellAssignment.GetProperty("note").GetString());
        Assert.Equal(endOfNextYearUtc, cellAssignment.GetProperty("validTo").GetDateTime().ToUniversalTime());

        // ============ Bước 4: gỡ một bản gán → request kế tiếp bị 403 ============
        Assert.Equal(HttpStatusCode.OK, (await cadre.GetAsync("/api/reports/cadres")).StatusCode);
        var committeeAssignmentId = effective.GetProperty("assignments").EnumerateArray()
            .Single(a => a.GetProperty("roleName").GetString() == "Cấp ủy viên Đảng ủy")
            .GetProperty("id").GetGuid();
        var removed = await admin.DeleteAsync($"/api/admin/assignments/{committeeAssignmentId}");
        Assert.True(removed.IsSuccessStatusCode, await removed.Content.ReadAsStringAsync());

        var afterRevoke = await cadre.GetAsync("/api/reports/cadres");
        Assert.Equal(HttpStatusCode.Forbidden, afterRevoke.StatusCode);
        Assert.Contains("Xuất báo cáo", (await GoLiveHttp.JsonAsync(afterRevoke)).GetProperty("message").GetString());
        var codesAfter = GoLiveHttp.Strings((await GoLiveHttp.DataAsync(await cadre.GetAsync("/api/auth/me"))).GetProperty("permissions"));
        Assert.DoesNotContain("report.export", codesAfter);
        Assert.Contains("evaluation.cell.confirm", codesAfter); // bản gán khác giữ nguyên

        // ============ Bước 5: cơ quan thẩm định tạo kỳ, thêm người được đánh giá, mở kỳ ============
        var appraiser = await _factory.LoginAsAsync("tran.thi.b", passwords["tran.thi.b"], distinctClientIp: true);
        await GoLiveHttp.ChangePasswordAsync(appraiser, passwords["tran.thi.b"], CadreNewPassword);
        var period = await GoLiveHttp.DataAsync(await appraiser.PostAsJsonAsync("/api/evaluations/periods", new
        {
            year = todayVn.Year, quarter = (todayVn.Month - 1) / 3 + 1, name = "Kỳ đánh giá go-live", preset = "q3-2026-transition",
            startDate = DateTime.UtcNow.AddDays(-1), endDate = DateTime.UtcNow.AddDays(60)
        }));
        var periodId = period.GetProperty("id").GetGuid();
        Assert.Equal("Draft", period.GetProperty("status").GetString());

        // Cấu hình kỳ (khi còn dự thảo): đơn vị chưa phân công thư ký tập thể và cấp trực tiếp sử dụng → tắt B3a, B3c.
        // Task 15: cấu hình bước theo hồ sơ luồng — đặt "Không áp dụng" cho B3a, B3c ở mọi hồ sơ luồng.
        var settings = JsonNode.Parse(period.GetProperty("settings").GetRawText())!.AsObject();
        foreach (var profile in settings["profiles"]!.AsArray())
        {
            profile!["steps"]!["B3A_COLLECTIVE"]!["mode"] = "Off";
            profile["steps"]!["B3C_DIRECTOR"]!["mode"] = "Off";
        }
        await GoLiveHttp.DataAsync(await appraiser.PutAsJsonAsync($"/api/evaluations/periods/{periodId}",
            new { version = period.GetProperty("version").GetUInt32(), settings }));

        var added = await GoLiveHttp.DataAsync(await appraiser.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants",
            new { memberIds = new[] { leVanCId } }));
        Assert.Equal(1, added.GetProperty("added").GetInt32());
        var draft = await GoLiveHttp.DataAsync(await appraiser.GetAsync($"/api/evaluations/periods/{periodId}"));
        // Task 15: người ghi nhận quyết định/công bố được phân công ở bước 6 (sau khi mở kỳ) → kiểm tra kẹt luồng còn cảnh báo,
        // mở bắt buộc kèm lý do.
        await GoLiveHttp.DataAsync(await appraiser.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/open",
            new { version = draft.GetProperty("version").GetUInt32(), force = true, reason = "Phân công ghi nhận quyết định ngay sau khi mở kỳ" }));
        var recordId = (await GoLiveHttp.DataAsync(await appraiser.GetAsync($"/api/evaluations/periods/{periodId}/participants")))
            .EnumerateArray().Single().GetProperty("recordId").GetGuid();

        // Quản trị kỹ thuật không xem được hồ sơ đánh giá.
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/evaluations/records/{recordId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/evaluations/records?periodId={periodId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/evaluations/records/{recordId}/history")).StatusCode);
        var adminCodes = GoLiveHttp.Strings((await GoLiveHttp.DataAsync(await admin.GetAsync("/api/auth/me"))).GetProperty("permissions"));
        Assert.DoesNotContain(adminCodes, c => c.StartsWith("evaluation.", StringComparison.Ordinal));

        // ============ Bước 6: hồ sơ đi hết các bước bật tới Published ============
        // Quản trị giao thêm vai trò ghi nhận quyết định/công bố cho nguyen.van.a qua API gán vai trò.
        var officeRoleId = await RoleIdByNameAsync("Văn phòng Đảng ủy (ghi nhận quyết định)");
        await GoLiveHttp.DataAsync(await admin.PostAsJsonAsync("/api/admin/assignments",
            new { userId = cadreId, roleId = officeRoleId, scopeType = "Global", note = "Phân công ghi nhận quyết định kỳ go-live" }));

        var owner = await _factory.LoginAsAsync("le.van.c", passwords["le.van.c"], distinctClientIp: true);
        await GoLiveHttp.ChangePasswordAsync(owner, passwords["le.van.c"], CadreNewPassword);
        var mine = await GoLiveHttp.DataAsync(await owner.GetAsync($"/api/evaluations/my-record?periodId={periodId}"));
        Assert.Equal(recordId, mine.GetProperty("id").GetGuid());
        Assert.Equal("AwaitingSelfScore", mine.GetProperty("status").GetString());
        var ownerQueue = await GoLiveHttp.DataAsync(await owner.GetAsync($"/api/evaluations/work-queue?periodId={periodId}"));
        Assert.Equal("B2_SELF_SCORE", ownerQueue.GetProperty("groups").EnumerateArray().Single().GetProperty("step").GetString());

        await GoLiveHttp.StepAsync(owner, recordId, "self-score/submit",
            new { generalScores = CriteriaTestData.General("2.1", "2.2", "2.3"), axisScores = CriteriaTestData.Axis(13, 9, 9, 13, 9, 9) }, "AwaitingCellConfirm");
        var cellQueue = await GoLiveHttp.DataAsync(await cadre.GetAsync($"/api/evaluations/work-queue?periodId={periodId}"));
        Assert.Contains(cellQueue.GetProperty("groups").EnumerateArray(), g => g.GetProperty("step").GetString() == "B2_CELL_CONFIRM");
        await GoLiveHttp.StepAsync(cadre, recordId, "cell/confirm", new { comment = "Chi bộ xác nhận" }, "AwaitingAppraisal");
        await GoLiveHttp.StepAsync(appraiser, recordId, "appraisal",
            new { explanation = "Căn cứ thẩm định (test)", appraisalScore = 88.5, comment = "Đủ minh chứng", proposedGrade = "HoanThanhTot" }, "AwaitingDecision");
        await GoLiveHttp.StepAsync(cadre, recordId, "decision", new { finalGrade = "HoanThanhTot", documentNumber = "01-QĐ/ĐU" }, "AwaitingPublish");
        await GoLiveHttp.StepAsync(cadre, recordId, "publish", new { }, "Published");

        var published = await GoLiveHttp.DataAsync(await owner.GetAsync($"/api/evaluations/records/{recordId}"));
        Assert.Equal("Published", published.GetProperty("status").GetString());
        Assert.Equal("HoanThanhTot", published.GetProperty("finalGrade").GetString());
        var history = await GoLiveHttp.DataAsync(await owner.GetAsync($"/api/evaluations/records/{recordId}/history"));
        var steps = history.EnumerateArray().Where(h => h.GetProperty("step").ValueKind == JsonValueKind.String)
            .Select(h => h.GetProperty("step").GetString()).OrderBy(StepOrder).ToList();
        Assert.Equal(new[] { "B2_SELF_SCORE", "B2_CELL_CONFIRM", "B3B_APPRAISAL", "B4_DECISION", "B5_PUBLISH" }, steps);
    }

    /// <summary>
    /// Khởi động host với cấu hình <c>Seed:InitialAdmin:*</c> (như biến môi trường <c>Seed__InitialAdmin__*</c> khi triển khai):
    /// seeder tạo tài khoản quản trị ban đầu (bắt buộc đổi mật khẩu, vai trò quản trị được bảo vệ, phạm vi Toàn công ty).
    /// </summary>
    private async Task<Guid> BootstrapInitialAdministratorAsync()
    {
        await using (var host = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Seed:InitialAdmin:Username", InitialAdminUsername);
            builder.UseSetting("Seed:InitialAdmin:FullName", "Quản trị hệ thống");
            builder.UseSetting("Seed:InitialAdmin:Password", InitialAdminPassword);
        }))
        {
            _ = host.Server;
        }

        return await GetUserIdAsync(InitialAdminUsername);
    }

    private static int StepOrder(string? step) => Array.IndexOf(
        new[] { "B1_REGISTER", "B1_APPROVE", "B2_SELF_SCORE", "B2_CELL_CONFIRM", "B3A_COLLECTIVE", "B3B_APPRAISAL", "B3C_DIRECTOR", "B4_DECISION", "B5_PUBLISH" },
        step);

    private async Task<Guid> RoleIdByNameAsync(string name)
    {
        var id = Guid.Empty;
        await _factory.WithDbAsync(async db => id = await db.Roles.Where(r => r.Name == name).Select(r => r.Id).SingleAsync());
        return id;
    }

    private async Task<Guid> GetUserIdAsync(string username)
    {
        var id = Guid.Empty;
        await _factory.WithDbAsync(async db =>
            id = await db.PartyMemberProfiles.Where(m => m.Username == username).Select(m => m.Id).SingleAsync());
        return id;
    }
}

/// <summary>Collection riêng của kịch bản go-live: CSDL tạm riêng, bắt đầu trống.</summary>
[CollectionDefinition(Name)]
public sealed class GoLiveCollection : ICollectionFixture<ApiFactory>
{
    /// <summary>Tên collection.</summary>
    public const string Name = "golive";
}

/// <summary>Hỗ trợ HTTP cho kịch bản go-live (dựng dữ liệu bằng API của các trang quản trị).</summary>
internal static class GoLiveHttp
{
    /// <summary>Thêm đơn vị (<c>departments</c>) hoặc tổ chức Đảng (<c>branches</c>) như trang Danh mục; trả Id.</summary>
    public static async Task<Guid> CreateCatalogItemAsync(HttpClient client, string kind, string code, string name, int sortOrder, Guid? parentId = null)
    {
        var data = await DataAsync(await client.PostAsJsonAsync($"/api/organizations/{kind}",
            new { code, name, sortOrder, parentId }));
        return data.GetProperty("id").GetGuid();
    }

    /// <summary>Tạo tài khoản như trang Tài khoản; trả mật khẩu tạm (chỉ hiển thị một lần).</summary>
    public static async Task<string> CreateAccountAsync(HttpClient client, string username, string fullName, string? email,
        string? partyCardNumber, string positionTitle, Guid departmentId, Guid partyCellId)
    {
        var data = await DataAsync(await client.PostAsJsonAsync("/api/users",
            new { username, fullName, email, partyCardNumber, positionTitle, departmentId, partyCellId }));
        Assert.Equal(username, data.GetProperty("username").GetString());
        return data.GetProperty("temporaryPassword").GetString()!;
    }

    /// <summary>Gán vai trò như trang Gán vai trò; trả Id bản gán.</summary>
    public static async Task<Guid> AssignAsync(HttpClient client, Guid userId, Guid roleId, string scopeType, Guid? scopeId,
        DateTime? validTo = null, string? note = null)
    {
        var data = await DataAsync(await client.PostAsJsonAsync("/api/admin/assignments",
            new { userId, roleId, scopeType, scopeId, validTo, note }));
        return data.GetProperty("id").GetGuid();
    }

    /// <summary>Thực hiện một hành động của luồng với phiên bản hiện tại của hồ sơ; kiểm tra trạng thái sau.</summary>
    public static async Task<JsonElement> StepAsync(HttpClient client, Guid recordId, string action, object body, string expectedStatus)
    {
        var current = await DataAsync(await client.GetAsync($"/api/evaluations/records/{recordId}"));
        var json = JsonSerializer.SerializeToNode(body)!.AsObject();
        json["version"] = current.GetProperty("version").GetUInt32();
        var result = await DataAsync(await client.PostAsJsonAsync($"/api/evaluations/records/{recordId}/{action}", json));
        Assert.Equal(expectedStatus, result.GetProperty("status").GetString());
        return result;
    }

    public static async Task ChangePasswordAsync(HttpClient client, string current, string next)
    {
        var response = await client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = current, newPassword = next });
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    public static List<string> Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()!).ToList();

    public static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    public static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {body}");
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("data").Clone();
    }
}
