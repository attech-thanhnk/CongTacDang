using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using CongTacDang.Application.Imports;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Kịch bản go-live đầu-cuối (task 13, T-68) trên PostgreSQL thật, từ <b>CSDL trống</b> (collection riêng, CSDL tạm riêng),
/// đúng thứ tự triển khai thật:
/// <list type="number">
/// <item>Khởi tạo tài khoản quản trị ban đầu → đăng nhập → bị buộc đổi mật khẩu → đổi.</item>
/// <item>Import Phòng → Chi bộ → cán bộ (nhận tệp mật khẩu tạm) → gán vai trò.</item>
/// <item>Một cán bộ đăng nhập bằng mật khẩu tạm → đổi mật khẩu → quyền + phạm vi đúng theo tệp gán.</item>
/// <item>Quản trị gỡ một bản gán → request kế tiếp của người đó bị 403 ở chức năng tương ứng.</item>
/// <item>Quản trị không xem được hồ sơ đánh giá (tách quản trị kỹ thuật).</item>
/// </list>
/// </summary>
[Collection(GoLiveCollection.Name)]
public sealed class GoLiveScenarioTests
{
    /// <summary>Tên đăng nhập / mật khẩu ban đầu của quản trị (giả lập cấu hình seed — xem <see cref="BootstrapInitialAdministratorAsync"/>).</summary>
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
            GoLiveHttp.BuildFile(new[] { "Mã Phòng", "Tên Phòng", "Thứ tự hiển thị" },
                new[] { "PH-KT", "Phòng Kỹ thuật", "1" },
                new[] { "PH-TCCB", "Phòng Tổ chức cán bộ - Lao động", "2" }),
            expectedCreated: 2);
        await GoLiveHttp.ImportAsync(admin, "party-cells",
            GoLiveHttp.BuildFile(new[] { "Mã Chi bộ", "Tên Chi bộ" },
                new[] { "CB-KT", "Chi bộ Kỹ thuật" },
                new[] { "CB-VP", "Chi bộ Văn phòng" }),
            expectedCreated: 2);

        var userCommit = await GoLiveHttp.ImportAsync(admin, "users",
            GoLiveHttp.BuildFile(
                new[] { "Tên đăng nhập", "Họ và tên", "Email", "Số thẻ Đảng", "Chức danh", "Mã Phòng", "Mã Chi bộ", "Thẩm quyền phê duyệt" },
                new[] { "nguyen.van.a", "Nguyễn Văn A", "a@attech.vn", "0100001", "Trưởng phòng Kỹ thuật", "PH-KT", "CB-KT", "CoSo" },
                new[] { "tran.thi.b", "Trần Thị B", "", "0100002", "Trưởng phòng TCCB-LĐ", "PH-TCCB", "CB-VP", "CoSo" },
                new[] { "le.van.c", "Lê Văn C", "", "", "Kỹ sư", "PH-KT", "CB-KT", "CoSo" }),
            expectedCreated: 3);
        var token = userCommit.GetProperty("resultFileToken").GetString();
        var download = await admin.GetAsync($"/api/imports/results/{token}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        var passwords = GoLiveHttp.ReadPasswords(await download.Content.ReadAsByteArrayAsync());
        Assert.Equal(3, passwords.Count);

        var assignmentHeaders = new[] { "Tên đăng nhập", "Tên vai trò", "Loại phạm vi", "Mã Phòng/Chi bộ", "Từ ngày", "Đến ngày", "Ghi chú" };
        var todayVn = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));
        var today = todayVn.ToString("dd/MM/yyyy");
        var endOfNextYear = $"31/12/{todayVn.Year + 1}";

        // Tệp sai: tự gán cho mình, vai trò quản trị gán theo Phòng, trùng trong tệp → không cho xác nhận, không ghi gì.
        var bad = await GoLiveHttp.PreviewAsync(admin, "role-assignments", GoLiveHttp.BuildFile(assignmentHeaders,
            new[] { InitialAdminUsername, "Người được đánh giá", "Toàn công ty", "", "", "", "" },
            new[] { "le.van.c", "Quản trị hệ thống", "Phòng", "PH-KT", "", "", "" },
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
                new[] { "nguyen.van.a", "Chi ủy / Bí thư Chi bộ", "Chi bộ", "CB-KT", today, endOfNextYear, "Nghị quyết Chi bộ số 01" },
                new[] { "nguyen.van.a", "Lãnh đạo Phòng", "Phòng", "PH-KT", "", "", "" },
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
                "meeting.read", "report.export"
            },
            cadreCodes.OrderBy(c => c, StringComparer.Ordinal));
        // Trường grants của /api/auth/me: kiểm ở GoLiveGrantsTests (chờ tích hợp Đợt 4).

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

        // ============ Bước 5: quản trị không xem được hồ sơ đánh giá ============
        var (periodId, recordId) = await SeedRecordAsync(cadreId);
        Assert.Equal(HttpStatusCode.OK, (await cadre.GetAsync($"/api/evaluations/records/{recordId}")).StatusCode); // chủ hồ sơ xem được
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/evaluations/records/{recordId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/evaluations/records?periodId={periodId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/evaluations/records/{recordId}/history")).StatusCode);
        var adminCodes = GoLiveHttp.Strings((await GoLiveHttp.DataAsync(await admin.GetAsync("/api/auth/me"))).GetProperty("permissions"));
        Assert.DoesNotContain(adminCodes, c => c.StartsWith("evaluation.", StringComparison.Ordinal));
        Assert.NotEqual(Guid.Empty, adminId);

        // Nối tiếp với kỳ đánh giá (tạo kỳ → thêm người được đánh giá → hồ sơ tới Published): chờ task 12 — xem báo cáo task 13.
    }

    /// <summary>
    /// Giả lập bước "hệ thống khởi tạo tài khoản quản trị ban đầu theo cấu hình seed": một tài khoản đang hoạt động,
    /// <c>MustChangePassword = true</c>, gán vai trò quản trị được bảo vệ phạm vi Toàn công ty.
    /// <para><b>Cần phối hợp:</b> hiện <c>DataSeeder</c> chỉ tạo tài khoản quản trị khi bật <c>Database:SeedSampleData</c>
    /// (kèm toàn bộ dữ liệu mẫu, mật khẩu chung) — chưa có cơ chế khởi tạo riêng quản trị ban đầu cho CSDL thật.
    /// Khi có, thay hàm này bằng cấu hình tương ứng của host test.</para>
    /// </summary>
    private async Task<Guid> BootstrapInitialAdministratorAsync()
    {
        var profile = new PartyMemberProfile
        {
            Username = InitialAdminUsername,
            FullName = "Quản trị hệ thống",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(InitialAdminPassword),
            MustChangePassword = true,
            IsActive = true,
            ApprovalAuthority = ApprovalAuthority.CoSo
        };
        Guid roleId = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            db.PartyMemberProfiles.Add(profile);
            await db.SaveChangesAsync();
            roleId = await db.Roles.Where(r => r.IsProtected && !r.IsDeleted).Select(r => r.Id).SingleAsync();
        });
        await _factory.AssignAsync(profile.Id, roleId, RoleScopeType.Global, null);
        return profile.Id;
    }

    private async Task<Guid> GetUserIdAsync(string username)
    {
        var id = Guid.Empty;
        await _factory.WithDbAsync(async db =>
            id = await db.PartyMemberProfiles.Where(m => m.Username == username).Select(m => m.Id).SingleAsync());
        return id;
    }

    /// <summary>Tạo kỳ + hồ sơ đánh giá của cán bộ trực tiếp trong CSDL (luồng tạo kỳ thuộc task 12).</summary>
    private async Task<(Guid PeriodId, Guid RecordId)> SeedRecordAsync(Guid memberId)
    {
        var period = new EvaluationPeriod
        {
            Year = 2026,
            Quarter = EvaluationQuarter.Quy4,
            Name = "Kỳ go-live",
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(60)
        };
        EvaluationRecord? record = null;
        await _factory.WithDbAsync(async db =>
        {
            var member = await db.PartyMemberProfiles.SingleAsync(m => m.Id == memberId);
            record = new EvaluationRecord { Period = period, MemberId = memberId, DepartmentId = member.DepartmentId, PartyCellId = member.PartyCellId };
            db.EvaluationRecords.Add(record);
            await db.SaveChangesAsync();
        });
        return (period.Id, record!.Id);
    }
}

/// <summary>Collection riêng của kịch bản go-live: CSDL tạm riêng, bắt đầu trống.</summary>
[CollectionDefinition(Name)]
public sealed class GoLiveCollection : ICollectionFixture<ApiFactory>
{
    /// <summary>Tên collection.</summary>
    public const string Name = "golive";
}

/// <summary>Hỗ trợ HTTP/Excel cho test task 13.</summary>
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
