using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ClosedXML.Excel;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>Nhập Excel: file mẫu → xem trước → xác nhận; thiếu quyền 403; còn dòng lỗi thì không ghi dòng nào.</summary>
[Collection(ApiCollection.Name)]
public sealed class ImportIntegrationTests
{
    private readonly ApiFactory _factory;

    public ImportIntegrationTests(ApiFactory factory) => _factory = factory;

    private void SkipIfNoDatabase() => Skip.If(_factory.SkipReason != null, _factory.SkipReason);

    private static readonly string[] ImportAdminPermissions =
    {
        PermissionCodes.SystemImport, PermissionCodes.CatalogManage, PermissionCodes.SystemUsersRead,
        PermissionCodes.SystemUsersManage, PermissionCodes.SystemAssignmentsManage
    };

    private async Task<(TestUser User, HttpClient Client)> ImportAdminAsync()
    {
        var user = await _factory.CreateUserWithPermissionsAsync(ImportAdminPermissions);
        return (user, await _factory.LoginAsAsync(user.Username, user.Password, distinctClientIp: true));
    }

    /// <summary>Tạo một đơn vị và một tổ chức Đảng làm cấp trên cho dữ liệu nhập.</summary>
    private async Task<(string DeptCode, Guid DeptId, string CellCode, Guid CellId)> CreateParentsAsync(string suffix)
    {
        var dept = new AdministrativeDepartment { Code = $"IMP-CTY-{suffix}", Name = $"Công ty nhập {suffix}" };
        var cell = new PartyCell { Code = $"IMP-DU-{suffix}", Name = $"Đảng ủy nhập {suffix}" };
        await _factory.WithDbAsync(async db =>
        {
            dept.Path = $"/{dept.Id:D}/";
            cell.Path = $"/{cell.Id:D}/";
            db.AdministrativeDepartments.Add(dept);
            db.PartyCells.Add(cell);
            await db.SaveChangesAsync();
        });
        return (dept.Code, dept.Id, cell.Code, cell.Id);
    }

    [SkippableFact]
    public async Task FullFlow_Departments_PartyCells_Users_Assignments()
    {
        SkipIfNoDatabase();
        var (_, admin) = await ImportAdminAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var (deptRoot, deptRootId, cellRoot, cellRootId) = await CreateParentsAsync(suffix);

        // Danh sách loại theo quyền; file mẫu có sheet dữ liệu và hướng dẫn.
        var kinds = await DataAsync(await admin.GetAsync("/api/imports/kinds"));
        var kindCodes = kinds.EnumerateArray().Select(k => k.GetProperty("kind").GetString()).ToList();
        Assert.Equal(new[] { "departments", "party-cells", "users", "role-assignments" }, kindCodes);
        var template = await admin.GetAsync("/api/imports/departments/template");
        Assert.Equal(HttpStatusCode.OK, template.StatusCode);
        using (var xl = new XLWorkbook(new MemoryStream(await template.Content.ReadAsByteArrayAsync())))
        {
            Assert.True(xl.TryGetWorksheet("Dữ liệu", out _));
            Assert.True(xl.TryGetWorksheet("Hướng dẫn", out _));
        }

        // 1. Đơn vị: một dòng cập nhật (đơn vị cấp trên), hai dòng tạo mới (dòng con đứng trước dòng cha trong file).
        var deptChild = $"IMP-PH-{suffix}";
        var deptTeam = $"IMP-TO-{suffix}";
        var deptFile = Workbook(new[] { "Mã đơn vị", "Tên đơn vị", "Mã đơn vị cấp trên", "Loại đơn vị", "Thứ tự" },
            new[] { deptTeam, "Tổ nhập", deptChild, "Đội", "" },
            new[] { deptRoot, "Công ty nhập (đổi tên)", "", "Công ty", "" },
            new[] { deptChild, "Phòng nhập", deptRoot, "Phòng", "2" });
        var deptPreview = await DataAsync(await UploadAsync(admin, "departments", deptFile));
        Assert.Equal(2, deptPreview.GetProperty("summary").GetProperty("create").GetInt32());
        Assert.Equal(1, deptPreview.GetProperty("summary").GetProperty("update").GetInt32());
        Assert.Equal(0, deptPreview.GetProperty("summary").GetProperty("error").GetInt32());
        var deptCommit = await DataAsync(await CommitAsync(admin, deptPreview));
        Assert.Equal(2, deptCommit.GetProperty("created").GetInt32());
        Assert.Equal(1, deptCommit.GetProperty("updated").GetInt32());

        Guid deptChildId = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var root = await db.AdministrativeDepartments.SingleAsync(d => d.Id == deptRootId);
            Assert.Equal("Công ty nhập (đổi tên)", root.Name);
            var child = await db.AdministrativeDepartments.SingleAsync(d => d.Code == deptChild);
            var team = await db.AdministrativeDepartments.SingleAsync(d => d.Code == deptTeam);
            Assert.Equal(deptRootId, child.ParentId);
            Assert.Equal(child.Id, team.ParentId);
            Assert.StartsWith(child.Path, team.Path, StringComparison.Ordinal);
            deptChildId = child.Id;
        });

        // 2. Tổ chức Đảng.
        var cellChild = $"IMP-CB-{suffix}";
        var cellFile = Workbook(new[] { "Mã tổ chức Đảng", "Tên tổ chức Đảng", "Mã tổ chức cấp trên", "Loại tổ chức" },
            new[] { cellChild, "Chi bộ nhập", cellRoot, "Chi bộ" });
        var cellPreview = await DataAsync(await UploadAsync(admin, "party-cells", cellFile));
        Assert.Equal(1, cellPreview.GetProperty("summary").GetProperty("create").GetInt32());
        await DataAsync(await CommitAsync(admin, cellPreview));
        Guid cellChildId = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var cell = await db.PartyCells.SingleAsync(c => c.Code == cellChild);
            Assert.Equal(cellRootId, cell.ParentId);
            cellChildId = cell.Id;
        });

        // 3. Cán bộ + tài khoản: tạo mới hai người, file mật khẩu tạm tải được đúng một lần.
        var userA = $"imp.a.{suffix.ToLowerInvariant()}";
        var userB = $"imp.b.{suffix.ToLowerInvariant()}";
        var userFile = Workbook(
            new[] { "Tên đăng nhập", "Họ và tên", "Ngày sinh", "Email", "Điện thoại", "Mã đơn vị", "Mã tổ chức Đảng", "Đảng viên", "Số thẻ đảng", "Chức vụ" },
            new[] { userA, "Nhập A", "15/08/1980", "", "0912345678", deptChild, cellChild, "Có", "IMP-001", "Bí thư Chi bộ" },
            new[] { userB, "Nhập B", "", "", "", deptChild, "", "Không", "", "" });
        var userPreview = await DataAsync(await UploadAsync(admin, "users", userFile));
        Assert.Equal(2, userPreview.GetProperty("summary").GetProperty("create").GetInt32());
        var userCommit = await DataAsync(await CommitAsync(admin, userPreview));
        Assert.Equal(2, userCommit.GetProperty("created").GetInt32());
        var token = userCommit.GetProperty("resultFileToken").GetString();
        Assert.False(string.IsNullOrEmpty(token));

        var resultFile = await admin.GetAsync($"/api/imports/results/{token}");
        Assert.Equal(HttpStatusCode.OK, resultFile.StatusCode);
        var passwords = new Dictionary<string, string>();
        using (var xl = new XLWorkbook(new MemoryStream(await resultFile.Content.ReadAsByteArrayAsync())))
        {
            var sheet = xl.Worksheet(1);
            for (var r = 2; r <= sheet.LastRowUsed()!.RowNumber(); r++)
                passwords[sheet.Cell(r, 1).GetString()] = sheet.Cell(r, 3).GetString();
        }
        Assert.Equal(new[] { userA, userB }.OrderBy(x => x), passwords.Keys.OrderBy(x => x));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/imports/results/{token}")).StatusCode);

        Guid userAId = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var a = await db.PartyMemberProfiles.SingleAsync(m => m.Username == userA);
            Assert.True(a.MustChangePassword);
            Assert.True(a.IsPartyMember);
            Assert.Equal(new DateOnly(1980, 8, 15), a.DateOfBirth);
            Assert.Equal(deptChildId, a.DepartmentId);
            Assert.Equal(cellChildId, a.PartyCellId);
            var position = await db.MemberPositions.Include(mp => mp.Position).SingleAsync(mp => mp.UserId == a.Id);
            Assert.Equal("Bí thư Chi bộ", position.Position!.Name);
            Assert.Equal(cellChildId, position.PartyCellId);
            Assert.True(position.IsPrimary);
            userAId = a.Id;
        });

        // Đăng nhập được bằng mật khẩu tạm và bị buộc đổi mật khẩu.
        var newcomer = await _factory.LoginAsAsync(userA, passwords[userA], distinctClientIp: true);
        var me = await DataAsync(await newcomer.GetAsync("/api/auth/me"));
        Assert.True(me.GetProperty("mustChangePassword").GetBoolean());

        // Nhập lại cùng tên đăng nhập → cập nhật, không đổi mật khẩu, không có file mật khẩu.
        var again = Workbook(new[] { "Tên đăng nhập", "Họ và tên", "Điện thoại" }, new[] { userA, "Nhập A (sửa)", "0987654321" });
        var againPreview = await DataAsync(await UploadAsync(admin, "users", again));
        Assert.Equal(1, againPreview.GetProperty("summary").GetProperty("update").GetInt32());
        var againCommit = await DataAsync(await CommitAsync(admin, againPreview));
        Assert.Equal(JsonValueKind.Null, againCommit.GetProperty("resultFileToken").ValueKind);
        await _factory.WithDbAsync(async db =>
        {
            var a = await db.PartyMemberProfiles.SingleAsync(m => m.Id == userAId);
            Assert.Equal("Nhập A (sửa)", a.FullName);
            Assert.Equal("0987654321", a.PhoneNumber);
            Assert.Equal(deptChildId, a.DepartmentId); // ô trống giữ nguyên
            Assert.True(BCrypt.Net.BCrypt.Verify(passwords[userA], a.PasswordHash));
        });

        // 4. Gán vai trò theo đơn vị.
        var role = await _factory.CreateRoleAsync(PermissionCodes.EvaluationRead);
        var assignFile = Workbook(new[] { "Tên đăng nhập", "Vai trò", "Loại phạm vi", "Mã đơn vị/tổ chức Đảng", "Từ ngày", "Đến ngày", "Ghi chú" },
            new[] { userA, role.Code, "Đơn vị", deptChild, "", "", "QĐ nhập" });
        var assignPreview = await DataAsync(await UploadAsync(admin, "role-assignments", assignFile));
        Assert.Equal(1, assignPreview.GetProperty("summary").GetProperty("create").GetInt32());
        await DataAsync(await CommitAsync(admin, assignPreview));
        await _factory.WithDbAsync(async db =>
        {
            var assignment = await db.Set<UserRoleAssignment>().SingleAsync(a => a.UserId == userAId && a.RoleId == role.Id);
            Assert.Equal(deptChildId, assignment.ScopeId);
            Assert.Equal("QĐ nhập", assignment.Note);
            Assert.Contains(await db.AuditLogs.Where(l => l.Action == "Import").Select(l => l.EntityId).ToListAsync(), k => k == "role-assignments");
        });

        // Nhập lại cùng bản gán → dòng lỗi (trùng bản gán đang hiệu lực).
        var duplicate = await DataAsync(await UploadAsync(admin, "role-assignments", assignFile));
        Assert.Equal(1, duplicate.GetProperty("summary").GetProperty("error").GetInt32());
    }

    [SkippableFact]
    public async Task WithoutPermission_Forbidden()
    {
        SkipIfNoDatabase();
        var plainUser = await _factory.CreateUserWithPermissionsAsync(PermissionCodes.EvaluationRead);
        var plain = await _factory.LoginAsAsync(plainUser.Username, plainUser.Password, distinctClientIp: true);
        var file = Workbook(new[] { "Mã đơn vị", "Tên đơn vị", "Loại đơn vị" }, new[] { "X", "X", "Phòng" });
        Assert.Equal(HttpStatusCode.Forbidden, (await plain.GetAsync("/api/imports/kinds")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await plain.GetAsync("/api/imports/departments/template")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(plain, "departments", file)).StatusCode);

        // Có system.import nhưng thiếu quyền dữ liệu của loại → 403, loại không có trong danh sách.
        var importOnlyUser = await _factory.CreateUserWithPermissionsAsync(PermissionCodes.SystemImport, PermissionCodes.CatalogManage);
        var importOnly = await _factory.LoginAsAsync(importOnlyUser.Username, importOnlyUser.Password, distinctClientIp: true);
        var kinds = await DataAsync(await importOnly.GetAsync("/api/imports/kinds"));
        Assert.Equal(new[] { "departments", "party-cells" }, kinds.EnumerateArray().Select(k => k.GetProperty("kind").GetString()));
        Assert.Equal(HttpStatusCode.Forbidden, (await importOnly.GetAsync("/api/imports/users/template")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(importOnly, "role-assignments", file)).StatusCode);
    }

    [SkippableFact]
    public async Task OtherUsersSession_CannotCommit_AndErrorRowsBlockCommit()
    {
        SkipIfNoDatabase();
        var (_, owner) = await ImportAdminAsync();
        var (_, other) = await ImportAdminAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var (deptRoot, _, _, _) = await CreateParentsAsync(suffix);
        var code = $"IMP-SS-{suffix}";

        var file = Workbook(new[] { "Mã đơn vị", "Tên đơn vị", "Mã đơn vị cấp trên", "Loại đơn vị" }, new[] { code, "Phòng phiên", deptRoot, "Phòng" });
        var preview = await DataAsync(await UploadAsync(owner, "departments", file));
        Assert.Equal(HttpStatusCode.Forbidden, (await CommitAsync(other, preview)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await CommitAsync(owner, preview)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await CommitAsync(owner, preview)).StatusCode); // phiên dùng một lần

        // Một dòng lỗi → không cho xác nhận, không dòng nào được ghi.
        var okCode = $"IMP-OK-{suffix}";
        var bad = Workbook(new[] { "Mã đơn vị", "Tên đơn vị", "Mã đơn vị cấp trên", "Loại đơn vị" },
            new[] { okCode, "Phòng hợp lệ", deptRoot, "Phòng" },
            new[] { $"IMP-BAD-{suffix}", "Phòng lỗi", "KHONG-CO-" + suffix, "Phòng" });
        var badPreview = await DataAsync(await UploadAsync(owner, "departments", bad));
        Assert.Equal(1, badPreview.GetProperty("summary").GetProperty("error").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await CommitAsync(owner, badPreview)).StatusCode);
        await _factory.WithDbAsync(async db =>
            Assert.False(await db.AdministrativeDepartments.IgnoreQueryFilters().AnyAsync(d => d.Code == okCode)));
    }

    #region Hỗ trợ

    private static byte[] Workbook(string[] headers, params string[][] rows)
    {
        using var xl = new XLWorkbook();
        var sheet = xl.Worksheets.Add("Dữ liệu");
        for (var c = 0; c < headers.Length; c++)
            sheet.Cell(1, c + 1).SetValue(headers[c]);
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                sheet.Cell(r + 2, c + 1).SetValue(rows[r][c]);
        using var stream = new MemoryStream();
        xl.SaveAs(stream);
        return stream.ToArray();
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, string kind, byte[] content)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "file", "nhap.xlsx");
        return client.PostAsync($"/api/imports/{kind}/preview", form);
    }

    private static Task<HttpResponseMessage> CommitAsync(HttpClient client, JsonElement preview)
        => client.PostAsync($"/api/imports/sessions/{preview.GetProperty("sessionId").GetGuid()}/commit", null);

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("data").Clone();
    }

    #endregion
}
