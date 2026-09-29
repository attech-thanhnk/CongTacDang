using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClosedXML.Excel;
using CongTacDang.Application.Imports;
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
/// <item>Import Phòng → Chi bộ → cán bộ (nhận tệp mật khẩu tạm) → gán vai trò.</item>
/// <item>Một cán bộ đăng nhập bằng mật khẩu tạm → đổi mật khẩu → quyền + phạm vi đúng theo tệp gán.</item>
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
    public async Task GoLive_FromEmptyDatabase_ImportCatalogCadresAssignments_ThenPermissionsFollowAssignments()
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

        var blocked = await admin.GetAsync("/api/imports/kinds");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("PASSWORD_CHANGE_REQUIRED", (await GoLiveHttp.JsonAsync(blocked)).GetProperty("code").GetString());

        await GoLiveHttp.ChangePasswordAsync(admin, InitialAdminPassword, AdminNewPassword);

        var kinds = (await GoLiveHttp.DataAsync(await admin.GetAsync("/api/imports/kinds"))).EnumerateArray()
            .Select(k => k.GetProperty("kind").GetString()).ToList();
        Assert.Contains("departments", kinds);
        Assert.Contains("party-cells", kinds);
        Assert.Contains("users", kinds);
        Assert.Contains("role-assignments", kinds);

        // ============ Bước 2: import Phòng → Chi bộ → cán bộ → gán vai trò ============
        await GoLiveHttp.ImportAsync(admin, "departments",
            GoLiveHttp.BuildFile(new[] { "Mã đơn vị", "Tên đơn vị", "Thứ tự hiển thị" },
                new[] { "PH-KT", "Phòng Kỹ thuật", "1" },
                new[] { "PH-TCCB", "Phòng Tổ chức cán bộ - Lao động", "2" }),
            expectedCreated: 2);
        await GoLiveHttp.ImportAsync(admin, "party-cells",
            GoLiveHttp.BuildFile(new[] { "Mã tổ chức Đảng", "Tên tổ chức Đảng" },
                new[] { "CB-KT", "Chi bộ Kỹ thuật" },
                new[] { "CB-VP", "Chi bộ Văn phòng" }),
            expectedCreated: 2);

        var userCommit = await GoLiveHttp.ImportAsync(admin, "users",
            GoLiveHttp.BuildFile(
                new[] { "Tên đăng nhập", "Họ và tên", "Email", "Số thẻ Đảng", "Chức danh", "Mã đơn vị công tác", "Mã tổ chức Đảng", "Thẩm quyền phê duyệt" },
                new[] { "nguyen.van.a", "Nguyễn Văn A", "a@attech.vn", "0100001", "Trưởng phòng Kỹ thuật", "PH-KT", "CB-KT", "CoSo" },
                new[] { "tran.thi.b", "Trần Thị B", "", "0100002", "Trưởng phòng TCCB-LĐ", "PH-TCCB", "CB-VP", "CoSo" },
                new[] { "le.van.c", "Lê Văn C", "", "", "Kỹ sư", "PH-KT", "CB-KT", "CoSo" }),
            expectedCreated: 3);
        var token = userCommit.GetProperty("resultFileToken").GetString();
        var download = await admin.GetAsync($"/api/imports/results/{token}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        var passwords = GoLiveHttp.ReadPasswords(await download.Content.ReadAsByteArrayAsync());
        Assert.Equal(3, passwords.Count);

        var assignmentHeaders = new[] { "Tên đăng nhập", "Tên vai trò", "Loại phạm vi", "Mã đơn vị", "Từ ngày", "Đến ngày", "Ghi chú" };
        var todayVn = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));
        var today = todayVn.ToString("dd/MM/yyyy");
        var endOfNextYear = $"31/12/{todayVn.Year + 1}";

        // Tệp sai: tự gán cho mình, vai trò quản trị gán theo Phòng, trùng trong tệp → không cho xác nhận, không ghi gì.
        var bad = await GoLiveHttp.PreviewAsync(admin, "role-assignments", GoLiveHttp.BuildFile(assignmentHeaders,
            new[] { InitialAdminUsername, "Người được đánh giá", "Toàn công ty", "", "", "", "" },
            new[] { "le.van.c", "Quản trị hệ thống", "Đơn vị chính quyền", "PH-KT", "", "", "" },
            new[] { "le.van.c", "Người được đánh giá", "Toàn công ty", "", "", "", "" },
            new[] { "LE.VAN.C", "người được đánh giá", "toàn công ty", "", "", "", "" }));
        Assert.False(bad.GetProperty("canCommit").GetBoolean());
        var badRows = bad.GetProperty("rows").EnumerateArray().ToList();
        Assert.Contains("chính mình", GoLiveHttp.Errors(badRows[0]));
        Assert.Contains("chỉ gán được với Loại phạm vi \"Toàn công ty\"", GoLiveHttp.Errors(badRows[1]));
        Assert.Equal("create", badRows[2].GetProperty("action").GetString());
        Assert.Contains("Trùng với dòng 4", GoLiveHttp.Errors(badRows[3]));
        await _factory.WithDbAsync(async db =>
            Assert.Equal(1, await db.Set<UserRoleAssignment>().CountAsync())); // chỉ bản gán của quản trị ban đầu

        await GoLiveHttp.ImportAsync(admin, "role-assignments", GoLiveHttp.BuildFile(assignmentHeaders,
                new[] { "nguyen.van.a", "Người được đánh giá", "Toàn công ty", "", "", "", "" },
                new[] { "nguyen.van.a", "Chi ủy / Bí thư Chi bộ", "Tổ chức Đảng", "CB-KT", today, endOfNextYear, "Nghị quyết Chi bộ số 01" },
                new[] { "nguyen.van.a", "Lãnh đạo Phòng", "Đơn vị chính quyền", "PH-KT", "", "", "" },
                new[] { "nguyen.van.a", "Cấp ủy viên Đảng ủy", "Toàn công ty", "", "", "", "" },
                new[] { "tran.thi.b", "Người được đánh giá", "Toàn công ty", "", "", "", "" },
                new[] { "tran.thi.b", "Cơ quan thẩm định (Phòng TCCB-LĐ)", "Toàn công ty", "", "", "", "" },
                new[] { "le.van.c", "Người được đánh giá", "Toàn công ty", "", "", "", "" }),
            expectedCreated: 7);

        await _factory.WithDbAsync(async db =>
        {
            Assert.Equal(8, await db.Set<UserRoleAssignment>().CountAsync());
            Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.EntityType == "Import" && a.EntityId == "role-assignments"));
        });

        // ============ Bước 3: cán bộ đăng nhập bằng mật khẩu tạm → đổi → quyền + phạm vi theo tệp ============
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
                "collective.manage", "evaluation.cell.confirm", "evaluation.read", "evaluation.self", "evaluation.tasks.approve",
                "evaluation.unit.review", // task 15: vai trò mặc định Lãnh đạo Phòng có thêm "Lãnh đạo đơn vị đề xuất"
                "meeting.read", "report.export"
            },
            cadreCodes.OrderBy(c => c, StringComparer.Ordinal));
        var cadreGrants = cadreMe.GetProperty("grants").EnumerateArray()
            .Select(g => $"{g.GetProperty("code").GetString()}@{g.GetProperty("scopeType").GetString()}:{g.GetProperty("scopeName").GetString()}").ToList();
        Assert.Contains("evaluation.cell.confirm@PartyCell:Chi bộ Kỹ thuật", cadreGrants);
        Assert.Contains("evaluation.tasks.approve@Department:Phòng Kỹ thuật", cadreGrants);

        var cadreId = await GetUserIdAsync("nguyen.van.a");
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
        Assert.Equal(new DateTime(todayVn.Year + 2, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(-7),
            cellAssignment.GetProperty("validTo").GetDateTime().ToUniversalTime());

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

        var leVanCId = await GetUserIdAsync("le.van.c");
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
            new { generalScores = new[] { 4.5, 4.5, 4.5, 4.5, 4.5, 4.5 }, axisScores = new[] { 13.0, 9, 9, 13, 9, 9 } }, "AwaitingCellConfirm");
        var cellQueue = await GoLiveHttp.DataAsync(await cadre.GetAsync($"/api/evaluations/work-queue?periodId={periodId}"));
        Assert.Contains(cellQueue.GetProperty("groups").EnumerateArray(), g => g.GetProperty("step").GetString() == "B2_CELL_CONFIRM");
        await GoLiveHttp.StepAsync(cadre, recordId, "cell/confirm", new { comment = "Chi bộ xác nhận" }, "AwaitingAppraisal");
        await GoLiveHttp.StepAsync(appraiser, recordId, "appraisal",
            new { appraisalScore = 88.5, comment = "Đủ minh chứng", proposedGrade = "HoanThanhTot" }, "AwaitingDecision");
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

/// <summary>Hỗ trợ HTTP/Excel cho kịch bản go-live và test import.</summary>
internal static class GoLiveHttp
{
    public static byte[] BuildFile(string[] headers, params string[][] rows)
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

    public static async Task<JsonElement> PreviewAsync(HttpClient client, string kind, byte[] file)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(file);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", $"{kind}.xlsx");
        var response = await client.PostAsync($"/api/imports/{kind}/preview", content);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await DataAsync(response);
    }

    /// <summary>Xem trước (phải không có lỗi) rồi xác nhận; trả dữ liệu kết quả xác nhận.</summary>
    public static async Task<JsonElement> ImportAsync(HttpClient client, string kind, byte[] file, int expectedCreated)
    {
        var preview = await PreviewAsync(client, kind, file);
        Assert.True(preview.GetProperty("canCommit").GetBoolean(), preview.ToString());
        var commit = await client.PostAsync($"/api/imports/{preview.GetProperty("sessionId").GetGuid()}/commit", null);
        Assert.True(commit.StatusCode == HttpStatusCode.OK, await commit.Content.ReadAsStringAsync());
        var result = await DataAsync(commit);
        Assert.Equal(expectedCreated, result.GetProperty("created").GetInt32());
        return result;
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

    public static Dictionary<string, string> ReadPasswords(byte[] xlsx)
    {
        using var workbook = new XLWorkbook(new MemoryStream(xlsx));
        var sheet = workbook.Worksheets.First();
        var header = sheet.CellsUsed().First(c => c.GetString() == "Tên đăng nhập");
        var passwordColumn = sheet.Row(header.Address.RowNumber).CellsUsed().First(c => c.GetString() == "Mật khẩu tạm").Address.ColumnNumber;
        var passwords = new Dictionary<string, string>();
        for (var row = header.Address.RowNumber + 1; row <= sheet.LastRowUsed()!.RowNumber(); row++)
            passwords[sheet.Cell(row, header.Address.ColumnNumber).GetString()] = sheet.Cell(row, passwordColumn).GetString();
        return passwords;
    }

    public static string Errors(JsonElement row) => string.Join(" | ", Strings(row.GetProperty("errors")));

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
