using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Test tích hợp task 14 (T-71, T-72, T-73): cây đơn vị dựng qua API danh mục, phạm vi gán vai trò bao trùm cây con,
/// chức vụ kiêm nhiệm → thẩm quyền suy ra → ảnh chụp trên hồ sơ mới, Mẫu 15A/15B theo mã chức danh.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class DynamicOrgIntegrationTests
{
    private readonly ApiFactory _factory;

    public DynamicOrgIntegrationTests(ApiFactory factory) => _factory = factory;

    private void SkipIfNoDatabase() => Skip.If(_factory.SkipReason != null, _factory.SkipReason);

    private async Task<HttpClient> AdminAsync()
    {
        var admin = await _factory.CreateUserWithPermissionsAsync(
            PermissionCodes.CatalogManage, PermissionCodes.SystemUsersManage,
            PermissionCodes.SystemUsersRead, PermissionCodes.PeriodManage, PermissionCodes.ReportExport);
        return await _factory.LoginAsAsync(admin.Username, admin.Password, distinctClientIp: true);
    }

    // ===================== O1: cây 3 cấp + phạm vi bao trùm cây con =====================

    [SkippableFact]
    public async Task O1_ThreeLevelTree_AssignmentAtMiddleNode_CoversDescendantsOnly()
    {
        SkipIfNoDatabase();
        var admin = await AdminAsync();
        var s = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        string R = $"R-{s}", M = $"M-{s}", L = $"L-{s}", S = $"S-{s}";

        // Dựng cây qua trang Danh mục; loại đơn vị theo danh mục mặc định.
        var unitTypes = (await DataAsync(await admin.GetAsync("/api/organizations/unit-types"))).EnumerateArray()
            .Where(t => t.GetProperty("side").GetString() == "Administrative")
            .ToDictionary(t => t.GetProperty("name").GetString()!, t => t.GetProperty("id").GetGuid());
        var rootId = await CreateUnitAsync(admin, "departments", new { code = R, name = "Công ty R", unitTypeId = unitTypes["Công ty"] });
        var middleId = await CreateUnitAsync(admin, "departments", new { code = M, name = "Phòng M", parentId = rootId, unitTypeId = unitTypes["Phòng"] });
        await CreateUnitAsync(admin, "departments", new { code = L, name = "Đội L", parentId = middleId, unitTypeId = unitTypes["Đội"] });
        await CreateUnitAsync(admin, "departments", new { code = S, name = "Phòng S", parentId = rootId, unitTypeId = unitTypes["Phòng"] });

        var departments = (await DataAsync(await admin.GetAsync("/api/organizations/departments"))).EnumerateArray().ToList();
        JsonElement Dept(string code) => departments.Single(d => d.GetProperty("code").GetString() == code);
        Guid Id(string code) => Dept(code).GetProperty("id").GetGuid();
        Assert.Equal(Id(M), Dept(L).GetProperty("parentId").GetGuid());
        Assert.Equal(2, Dept(L).GetProperty("depth").GetInt32());
        Assert.Contains($"/{Id(R)}/", Dept(L).GetProperty("path").GetString());
        Assert.Equal("Đội", Dept(L).GetProperty("unitTypeName").GetString());
        // Danh sách sắp theo cây: cha đứng trước con.
        var order = departments.Select(d => d.GetProperty("code").GetString()).ToList();
        Assert.True(order.IndexOf(R) < order.IndexOf(M) && order.IndexOf(M) < order.IndexOf(L));

        // Đơn vị cha không tồn tại → 400, không tạo.
        var missingParent = await admin.PostAsJsonAsync("/api/organizations/departments",
            new { code = $"X-{s}", name = "Đơn vị X", parentId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, missingParent.StatusCode);
        Assert.Contains("Không tìm thấy đơn vị cha", await MessageAsync(missingParent));

        // Cán bộ ở từng nút; người được gán vai trò ở nút giữa M.
        var userM = await _factory.CreateUserAsync(Id(M));
        var userL = await _factory.CreateUserAsync(Id(L));
        var userS = await _factory.CreateUserAsync(Id(S));
        var role = await _factory.CreateRoleAsync(PermissionCodes.SystemUsersRead, PermissionCodes.SystemUsersManage, PermissionCodes.EvaluationRead);
        var leader = await _factory.CreateUserAsync(Id(M));
        await _factory.AssignAsync(leader.Id, role.Id, RoleScopeType.Department, Id(M));
        var leaderClient = await _factory.LoginAsAsync(leader.Username, leader.Password, distinctClientIp: true);

        var visible = await VisibleUserIdsAsync(leaderClient);
        Assert.Contains(userM.Id, visible);
        Assert.Contains(userL.Id, visible); // cháu của nút được gán
        Assert.DoesNotContain(userS.Id, visible); // nhánh anh em

        var leafDetail = await DataAsync(await leaderClient.GetAsync($"/api/users/{userL.Id}"));
        Assert.True(leafDetail.GetProperty("canManage").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await leaderClient.GetAsync($"/api/users/{userS.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await leaderClient.PutAsJsonAsync($"/api/users/{userL.Id}", new { positionTitle = "Tổ trưởng" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await leaderClient.PutAsJsonAsync($"/api/users/{userS.Id}", new { positionTitle = "Không được" })).StatusCode);

        // Hồ sơ đánh giá: thấy hồ sơ ở nút cháu, không thấy nhánh khác.
        var (recordL, recordS) = await SeedRecordsAsync(userL, Id(L), userS, Id(S));
        Assert.Equal(HttpStatusCode.OK, (await leaderClient.GetAsync($"/api/evaluations/records/{recordL}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await leaderClient.GetAsync($"/api/evaluations/records/{recordS}")).StatusCode);

        // Đổi cha S → dưới M: có hiệu lực ngay (cache quyền được xóa khi cây đổi).
        Assert.Equal(HttpStatusCode.OK,
            (await admin.PutAsJsonAsync($"/api/organizations/departments/{Id(S)}", new { parentId = Id(M) })).StatusCode);
        Assert.Contains(userS.Id, await VisibleUserIdsAsync(leaderClient));
        Assert.Equal(HttpStatusCode.OK, (await leaderClient.GetAsync($"/api/evaluations/records/{recordS}")).StatusCode);

        // Chống vòng qua API; xóa nút còn con → 409 nêu số lượng.
        var cycle = await admin.PutAsJsonAsync($"/api/organizations/departments/{Id(R)}", new { parentId = Id(L) });
        Assert.Equal(HttpStatusCode.BadRequest, cycle.StatusCode);
        Assert.Contains("vòng", await MessageAsync(cycle));
        var delete = await admin.DeleteAsync($"/api/organizations/departments/{Id(M)}");
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        var deleteMessage = await MessageAsync(delete);
        Assert.Contains("cấp dưới", deleteMessage);
        Assert.Contains("bản gán vai trò", deleteMessage);
        Assert.Contains("cán bộ", deleteMessage);
    }

    // ===================== O2: kiêm nhiệm → thẩm quyền suy ra → hồ sơ mới; Mẫu 15A/15B =====================

    [SkippableFact]
    public async Task O2_ConcurrentPositions_DeriveCapTren_SnapshotOnNewRecord_AndForm15GroupsByStatCode()
    {
        SkipIfNoDatabase();
        var admin = await AdminAsync();
        var s = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        // Cây: đơn vị chính quyền (Phòng) + tổ chức Đảng (Đảng ủy → Chi bộ) qua API.
        var deptId = await CreateUnitAsync(admin, "departments", new { code = $"PH-{s}", name = $"Phòng {s}" });
        var partyId = await CreateUnitAsync(admin, "branches", new { code = $"DU-{s}", name = $"Đảng ủy {s}" });
        var cellId = await CreateUnitAsync(admin, "branches", new { code = $"CB-{s}", name = $"Chi bộ {s}", parentId = partyId });
        var cell = (await DataAsync(await admin.GetAsync($"/api/organizations/branches/{cellId}")));
        Assert.Equal(partyId, cell.GetProperty("parentId").GetGuid());

        var x = await _factory.CreateUserAsync(deptId, cellId, fullName: $"Kiêm nhiệm {s}");
        var y = await _factory.CreateUserAsync(deptId, cellId, fullName: $"Bí thư chi bộ {s}");
        var z = await _factory.CreateUserAsync(deptId, cellId, fullName: $"Phó phòng {s}");

        // Chức vụ của cán bộ khai báo qua trang Tài khoản (danh mục chức vụ mặc định theo HD03).
        var catalog = (await DataAsync(await admin.GetAsync("/api/positions"))).EnumerateArray()
            .ToDictionary(p => p.GetProperty("name").GetString()!, p => p.GetProperty("id").GetGuid());
        var from = new DateTime(2025, 12, 31, 17, 0, 0, DateTimeKind.Utc); // 01/01/2026 giờ Việt Nam
        await AddPositionAsync(admin, x.Id, new { positionId = catalog["Trưởng phòng"], departmentId = deptId, isPrimary = true, validFrom = from });
        await AddPositionAsync(admin, x.Id, new { positionId = catalog["Bí thư Đảng ủy"], partyCellId = partyId, isPrimary = false, validFrom = from });
        await AddPositionAsync(admin, y.Id, new { positionId = catalog["Bí thư Chi bộ"], partyCellId = cellId, isPrimary = true, validFrom = from });
        await AddPositionAsync(admin, z.Id, new { positionId = catalog["Phó Trưởng phòng"], departmentId = deptId, isPrimary = true, validFrom = from });

        // Sai đơn vị theo bên chức vụ (chức vụ Đảng + đơn vị chính quyền) → 400.
        var wrong = await admin.PostAsJsonAsync($"/api/users/{y.Id}/positions",
            new { positionId = catalog["Phó Bí thư Chi bộ"], departmentId = deptId });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Contains("chức vụ Đảng", await MessageAsync(wrong));

        var authority = await DataAsync(await admin.GetAsync($"/api/users/{x.Id}/approval-authority"));
        Assert.Equal("CapTren", authority.GetProperty("effective").GetString());
        Assert.Equal("CapTren", authority.GetProperty("derived").GetString());
        Assert.Equal("M8", authority.GetProperty("statCode").GetString());
        Assert.Equal("Trưởng phòng", (await DataAsync(await admin.GetAsync($"/api/users/{x.Id}"))).GetProperty("positionTitle").GetString());
        var positions = (await DataAsync(await admin.GetAsync($"/api/users/{x.Id}/positions"))).EnumerateArray().ToList();
        Assert.Equal(2, positions.Count);
        Assert.Single(positions, p => p.GetProperty("isPrimary").GetBoolean());

        // Kỳ mới + thêm người → ảnh chụp thẩm quyền trên hồ sơ mới tạo.
        var period = await DataAsync(await admin.PostAsJsonAsync("/api/evaluations/periods", new
        {
            year = 2040 + Random.Shared.Next(0, 59), quarter = 1 + Random.Shared.Next(0, 4), name = $"Kỳ tổ chức động {s}",
            startDate = DateTime.UtcNow.AddDays(-10), endDate = DateTime.UtcNow.AddDays(30)
        }));
        var periodId = period.GetProperty("id").GetGuid();
        var add = await admin.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants", new { memberIds = new[] { x.Id, y.Id, z.Id } });
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        await _factory.WithDbAsync(async db =>
        {
            var records = await db.EvaluationRecords.Where(r => r.PeriodId == periodId).ToListAsync();
            Assert.Equal(ApprovalAuthority.CapTren, records.Single(r => r.MemberId == x.Id).ApprovalAuthority);
            Assert.Equal(ApprovalAuthority.CoSo, records.Single(r => r.MemberId == y.Id).ApprovalAuthority);
            Assert.Equal(ApprovalAuthority.CoSo, records.Single(r => r.MemberId == z.Id).ApprovalAuthority);
        });

        // Mẫu 15B: mỗi người một lần theo mã nhỏ nhất — M22 (Bí thư Chi bộ), M26 (Phó phòng); X (M8) nằm ở 15A.
        var form15B = await ReadFormCountsAsync(admin, $"/api/reports/form-15b?periodId={periodId}");
        Assert.Equal(1, form15B["M22"]);
        Assert.Equal(1, form15B["M26"]);
        Assert.False(form15B.ContainsKey("M8"));
        var form15A = await ReadFormCountsAsync(admin, $"/api/reports/form-15a?periodId={periodId}");
        Assert.Equal(1, form15A["M8"]);
        Assert.Equal(0, form15A["M14"]);

        // Đặt tay có lý do (thiếu lý do → 400), bỏ đặt tay → quay về suy ra.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PutAsJsonAsync($"/api/users/{z.Id}/approval-authority", new { @override = "CapTren" })).StatusCode);
        var manual = await DataAsync(await admin.PutAsJsonAsync($"/api/users/{z.Id}/approval-authority",
            new { @override = "CapTren", reason = "Theo văn bản phân cấp số 01" }));
        Assert.Equal("CapTren", manual.GetProperty("effective").GetString());
        Assert.Equal("CoSo", manual.GetProperty("derived").GetString());
        var cleared = await DataAsync(await admin.PutAsJsonAsync($"/api/users/{z.Id}/approval-authority", new { @override = "" }));
        Assert.Equal("CoSo", cleared.GetProperty("effective").GetString());

        // Kết thúc chức vụ CapTren → thẩm quyền suy ra quay về CoSo.
        var partyPosition = positions.Single(p => p.GetProperty("positionName").GetString() == "Bí thư Đảng ủy").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/users/{x.Id}/positions/{partyPosition}/end", null)).StatusCode);
        Assert.Equal("CoSo", (await DataAsync(await admin.GetAsync($"/api/users/{x.Id}/approval-authority"))).GetProperty("effective").GetString());
    }

    // ===================== O3: danh mục chức vụ, loại đơn vị =====================

    [SkippableFact]
    public async Task O3_PositionAndUnitTypeCatalogs_Crud()
    {
        SkipIfNoDatabase();
        var admin = await AdminAsync();
        var s = Guid.NewGuid().ToString("N")[..6];

        // Mã thống kê sai → 400; mã viết thường được chuẩn hóa.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/positions",
            new { name = $"Sai mã {s}", side = "Administrative", statCode = "X9" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/positions",
            new { name = $"Tổ trưởng {s}", side = "Administrative", statCode = "m26", defaultApprovalAuthority = "CoSo", isLeadership = true })).StatusCode);

        var positions = (await DataAsync(await admin.GetAsync("/api/positions"))).EnumerateArray().ToList();
        var created = positions.Single(p => p.GetProperty("name").GetString() == $"Tổ trưởng {s}");
        Assert.Equal("M26", created.GetProperty("statCode").GetString());
        Assert.Equal("CoSo", created.GetProperty("defaultApprovalAuthority").GetString());
        Assert.Contains(positions, p => p.GetProperty("statCode").ValueKind == JsonValueKind.String && p.GetProperty("statCode").GetString() == "M1");

        // Trùng tên → 409; xóa chức vụ chưa gán → OK.
        Assert.Equal(HttpStatusCode.Conflict,
            (await admin.PostAsJsonAsync("/api/positions", new { name = $"tổ trưởng {s}", side = "Administrative" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/positions/{created.GetProperty("id").GetGuid()}")).StatusCode);

        // Loại đơn vị: thêm, trùng → 409, đang dùng → không xóa được.
        var type = await DataAsync(await admin.PostAsJsonAsync("/api/organizations/unit-types", new { name = $"Ban {s}", side = "Administrative" }));
        var typeId = type.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict,
            (await admin.PostAsJsonAsync("/api/organizations/unit-types", new { name = $"ban {s}", side = "Administrative" })).StatusCode);
        var dept = await CreateUnitAsync(admin, "departments", new { code = $"BAN-{s.ToUpperInvariant()}", name = $"Ban {s}", unitTypeId = typeId });
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/organizations/unit-types/{typeId}")).StatusCode);
        // Loại bên chính quyền không dùng được cho tổ chức Đảng.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync("/api/organizations/branches", new { code = $"CB-T{s}", name = "Sai loại", unitTypeId = typeId })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/organizations/departments/{dept}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/organizations/unit-types/{typeId}")).StatusCode);
    }

    // ===================== Hỗ trợ =====================

    private static async Task<HashSet<Guid>> VisibleUserIdsAsync(HttpClient client)
    {
        var page = await DataAsync(await client.GetAsync("/api/users?pageSize=200"));
        return page.GetProperty("items").EnumerateArray().Select(u => u.GetProperty("id").GetGuid()).ToHashSet();
    }

    private async Task<(Guid RecordL, Guid RecordS)> SeedRecordsAsync(TestUser userL, Guid deptL, TestUser userS, Guid deptS)
    {
        var period = new EvaluationPeriod
        {
            Year = 2090 + Random.Shared.Next(0, 9),
            Quarter = EvaluationQuarter.Quy2,
            Name = $"Kỳ cây đơn vị {Guid.NewGuid():N}"[..40],
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        var recordL = new EvaluationRecord { Period = period, MemberId = userL.Id, DepartmentId = deptL, UpdatedAt = DateTime.UtcNow };
        var recordS = new EvaluationRecord { Period = period, MemberId = userS.Id, DepartmentId = deptS, UpdatedAt = DateTime.UtcNow };
        await _factory.WithDbAsync(async db =>
        {
            db.EvaluationRecords.AddRange(recordL, recordS);
            await db.SaveChangesAsync();
        });
        return (recordL.Id, recordS.Id);
    }

    private static async Task<Guid> CreateUnitAsync(HttpClient client, string kind, object body)
    {
        var response = await client.PostAsJsonAsync($"/api/organizations/{kind}", body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await DataAsync(response)).GetProperty("id").GetGuid();
    }

    /// <summary>Đọc Mẫu 15A/15B: mã (cột A) → tổng số (cột C).</summary>
    private static async Task<Dictionary<string, int>> ReadFormCountsAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var workbook = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        var sheet = workbook.Worksheets.First();
        var counts = new Dictionary<string, int>();
        foreach (var row in sheet.RowsUsed())
        {
            // Task 19: bố cục Mẫu 15A/15B theo HD03 — cột 3 mã chức danh, cột 4 tổng số.
            var code = row.Cell(3).GetString();
            if (code.StartsWith('M') && int.TryParse(row.Cell(4).GetString(), out var total))
                counts[code] = total;
        }
        return counts;
    }

    private static async Task AddPositionAsync(HttpClient client, Guid userId, object body)
    {
        var response = await client.PostAsJsonAsync($"/api/users/{userId}/positions", body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("data").Clone();
    }

    private static async Task<string> MessageAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("message").GetString() ?? string.Empty;
    }
}
