using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Imports;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Infrastructure.Data;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Test tích hợp task 10 (T-62, T-64): danh mục Phòng/Chi bộ (L1, L2) và nhập dữ liệu Excel (L3–L6) trên PostgreSQL thật.
/// Đăng nhập tối thiểu (giới hạn 10 lần/phút/IP): một client quản trị dùng chung cho cả lớp + một lần đăng nhập
/// bằng mật khẩu tạm trong tệp kết quả.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CatalogImportIntegrationTests
{
    private static readonly SemaphoreSlim AdminLock = new(1, 1);
    private static ApiFactory? _adminFactory;
    private static HttpClient? _adminClient;

    private readonly ApiFactory _factory;

    public CatalogImportIntegrationTests(ApiFactory factory) => _factory = factory;

    private void SkipIfNoDatabase() => Skip.If(_factory.SkipReason != null, _factory.SkipReason);

    /// <summary>Client quản trị (system.import + catalog.manage + system.users.manage), đăng nhập một lần cho cả lớp.</summary>
    private async Task<HttpClient> AdminAsync()
    {
        await AdminLock.WaitAsync();
        try
        {
            if (_adminClient == null || !ReferenceEquals(_adminFactory, _factory))
            {
                var admin = await _factory.CreateUserWithPermissionsAsync(
                    PermissionCodes.SystemImport, PermissionCodes.CatalogManage, PermissionCodes.SystemUsersManage);
                _adminClient = await _factory.LoginAsAsync(admin);
                _adminFactory = _factory;
            }
            return _adminClient;
        }
        finally
        {
            AdminLock.Release();
        }
    }

    // ===================== L1, L2: danh mục =====================

    [SkippableFact]
    public async Task L1_Departments_Crud_UniqueCode_Deactivate_DeleteBlockedWhileInUse()
    {
        SkipIfNoDatabase();
        var client = await AdminAsync();
        var code = UniqueCode("PH");

        var created = await client.PostAsJsonAsync("/api/organizations/departments",
            new { code = code.ToLowerInvariant(), name = "Phòng Kiểm thử", description = "Tạo bởi test", sortOrder = 5 });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var dept = await DataAsync(created);
        var id = dept.GetProperty("id").GetGuid();
        Assert.Equal(code, dept.GetProperty("code").GetString()); // chuẩn hóa chữ hoa

        // Mã duy nhất, không phân biệt hoa thường.
        var duplicate = await client.PostAsJsonAsync("/api/organizations/departments", new { code, name = "Trùng mã" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        // Sửa + ngừng hoạt động.
        var updated = await client.PutAsJsonAsync($"/api/organizations/departments/{id}",
            new { name = "Phòng Kiểm thử (đổi tên)", sortOrder = 1, isActive = false });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var updatedDto = await DataAsync(updated);
        Assert.False(updatedDto.GetProperty("isActive").GetBoolean());
        Assert.Equal(1, updatedDto.GetProperty("sortOrder").GetInt32());
        Assert.Equal("Tạo bởi test", updatedDto.GetProperty("description").GetString()); // trường null giữ nguyên

        // Còn cán bộ và hồ sơ đánh giá → 409 nêu số lượng.
        var (memberId, recordId) = await SeedMemberAndRecordAsync(departmentId: id, partyCellId: null);
        var blocked = await client.DeleteAsync($"/api/organizations/departments/{id}");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var message = await MessageAsync(blocked);
        Assert.Contains("1 cán bộ", message);
        Assert.Contains("1 hồ sơ đánh giá", message);

        // Chỉ còn hồ sơ đánh giá → vẫn 409, khuyên chuyển sang ngừng hoạt động.
        await ExecuteDbAsync(async db =>
        {
            var member = await db.PartyMemberProfiles.SingleAsync(m => m.Id == memberId);
            member.DepartmentId = null;
            await db.SaveChangesAsync();
        });
        var stillBlocked = await client.DeleteAsync($"/api/organizations/departments/{id}");
        Assert.Equal(HttpStatusCode.Conflict, stillBlocked.StatusCode);
        Assert.Contains("Ngừng hoạt động", await MessageAsync(stillBlocked));

        // Hết tham chiếu → xóa được; mã đã xóa không dùng lại được.
        await ExecuteDbAsync(async db =>
        {
            var record = await db.EvaluationRecords.SingleAsync(r => r.Id == recordId);
            db.EvaluationRecords.Remove(record);
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/organizations/departments/{id}")).StatusCode);
        var list = await DataAsync(await client.GetAsync("/api/organizations/departments"));
        Assert.DoesNotContain(list.EnumerateArray(), d => d.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync("/api/organizations/departments", new { code, name = "Tạo lại" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/organizations/departments/{id}")).StatusCode);
    }

    [SkippableFact]
    public async Task L2_PartyCells_Crud_DeleteBlockedWhileInUse()
    {
        SkipIfNoDatabase();
        var client = await AdminAsync();
        var code = UniqueCode("CB");

        var created = await client.PostAsJsonAsync("/api/organizations/branches", new { code, name = "Chi bộ Kiểm thử" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var id = (await DataAsync(created)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync("/api/organizations/branches", new { code = code.ToLowerInvariant(), name = "Trùng" })).StatusCode);

        var deactivated = await client.PutAsJsonAsync($"/api/organizations/branches/{id}", new { isActive = false });
        Assert.False((await DataAsync(deactivated)).GetProperty("isActive").GetBoolean());

        var (memberId, recordId) = await SeedMemberAndRecordAsync(departmentId: null, partyCellId: id);
        var blocked = await client.DeleteAsync($"/api/organizations/branches/{id}");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var message = await MessageAsync(blocked);
        Assert.Contains("1 cán bộ", message);
        Assert.Contains("1 hồ sơ đánh giá", message);

        await ExecuteDbAsync(async db =>
        {
            (await db.PartyMemberProfiles.SingleAsync(m => m.Id == memberId)).PartyCellId = null;
            db.EvaluationRecords.Remove(await db.EvaluationRecords.SingleAsync(r => r.Id == recordId));
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/organizations/branches/{id}")).StatusCode);
    }

    // ===================== L3, L4: import Phòng / Chi bộ =====================

    [SkippableFact]
    public async Task L3_L4_ImportDepartmentsAndPartyCells_TemplatePreviewCommit_CreateAndUpdateByCode()
    {
        SkipIfNoDatabase();
        var client = await AdminAsync();

        var kinds = await DataAsync(await client.GetAsync("/api/imports/kinds"));
        var kindCodes = kinds.EnumerateArray().Select(k => k.GetProperty("kind").GetString()).ToList();
        Assert.Contains("departments", kindCodes);
        Assert.Contains("party-cells", kindCodes);
        Assert.Contains("users", kindCodes);

        // Tải mẫu.
        var template = await client.GetAsync("/api/imports/departments/template");
        Assert.Equal(HttpStatusCode.OK, template.StatusCode);
        using (var workbook = new XLWorkbook(new MemoryStream(await template.Content.ReadAsByteArrayAsync())))
        {
            Assert.True(workbook.Worksheets.Contains(ImportLimits.DataSheetName));
            Assert.True(workbook.Worksheets.Contains(ImportLimits.GuideSheetName));
        }

        // Phòng đã có → cập nhật; mã mới → tạo mới.
        var existingCode = UniqueCode("PH");
        var newCode = UniqueCode("PH");
        var existing = await client.PostAsJsonAsync("/api/organizations/departments", new { code = existingCode, name = "Tên cũ", description = "Mô tả cũ" });
        var existingId = (await DataAsync(existing)).GetProperty("id").GetGuid();

        var file = BuildFile(new[] { "Mã đơn vị", "Tên đơn vị", "Mô tả", "Thứ tự hiển thị", "Trạng thái" },
            new[] { existingCode.ToLowerInvariant(), "Tên mới", "", "3", "Ngừng hoạt động" },
            new[] { newCode, "Phòng nhập từ Excel", "Mô tả mới", "", "" });
        var preview = await PreviewAsync(client, "departments", file);
        Assert.True(preview.GetProperty("canCommit").GetBoolean());
        var actions = preview.GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("action").GetString()).ToList();
        Assert.Equal(new[] { "update", "create" }, actions);
        Assert.Equal(1, preview.GetProperty("summary").GetProperty("update").GetInt32());
        Assert.Equal(1, preview.GetProperty("summary").GetProperty("create").GetInt32());
        Assert.Equal("Tên mới", preview.GetProperty("rows")[0].GetProperty("data").GetProperty("name").GetString());

        var sessionId = preview.GetProperty("sessionId").GetGuid();
        var commit = await client.PostAsync($"/api/imports/{sessionId}/commit", null);
        Assert.Equal(HttpStatusCode.OK, commit.StatusCode);
        var result = await DataAsync(commit);
        Assert.Equal(1, result.GetProperty("created").GetInt32());
        Assert.Equal(1, result.GetProperty("updated").GetInt32());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("resultFileToken").ValueKind);

        // Phiên chỉ xác nhận được một lần.
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/imports/{sessionId}/commit", null)).StatusCode);

        var departments = (await DataAsync(await client.GetAsync("/api/organizations/departments"))).EnumerateArray().ToList();
        var updated = departments.Single(d => d.GetProperty("id").GetGuid() == existingId);
        Assert.Equal("Tên mới", updated.GetProperty("name").GetString());
        Assert.Equal("Mô tả cũ", updated.GetProperty("description").GetString()); // ô trống khi cập nhật → giữ nguyên
        Assert.Equal(3, updated.GetProperty("sortOrder").GetInt32());
        Assert.False(updated.GetProperty("isActive").GetBoolean());
        var createdDept = departments.Single(d => d.GetProperty("code").GetString() == newCode);
        Assert.True(createdDept.GetProperty("isActive").GetBoolean());

        // Audit tóm tắt 1 bản ghi cho lần xác nhận.
        await ExecuteDbAsync(async db =>
        {
            var audits = await db.AuditLogs.Where(a => a.Action == "Import" && a.EntityId == "departments").ToListAsync();
            Assert.Contains(audits, a => a.NewValues.Contains("\"created\":1") && a.NewValues.Contains("\"updated\":1"));
        });

        // Chi bộ: cùng khung, loại khác.
        var cellCode = UniqueCode("CB");
        var cellPreview = await PreviewAsync(client, "party-cells",
            BuildFile(new[] { "Mã tổ chức Đảng", "Tên tổ chức Đảng" }, new[] { cellCode, "Chi bộ nhập từ Excel" }));
        Assert.Equal("create", cellPreview.GetProperty("rows")[0].GetProperty("action").GetString());
        var cellCommit = await client.PostAsync($"/api/imports/{cellPreview.GetProperty("sessionId").GetGuid()}/commit", null);
        Assert.Equal(1, (await DataAsync(cellCommit)).GetProperty("created").GetInt32());
        var cells = await DataAsync(await client.GetAsync("/api/organizations/branches"));
        Assert.Contains(cells.EnumerateArray(), c => c.GetProperty("code").GetString() == cellCode);
    }

    [SkippableFact]
    public async Task L3_OneErrorRow_BlocksCommit_AndNothingIsWritten()
    {
        SkipIfNoDatabase();
        var client = await AdminAsync();
        var a = UniqueCode("PH");
        var b = UniqueCode("PH");

        var file = BuildFile(new[] { "Mã đơn vị", "Tên đơn vị", "Thứ tự hiển thị" },
            new[] { a, "Phòng A", "1" },
            new[] { b, "Phòng B", "không phải số" },
            new[] { UniqueCode("PH"), "Phòng C", "2" });
        var preview = await PreviewAsync(client, "departments", file);

        Assert.False(preview.GetProperty("canCommit").GetBoolean());
        var errorRow = preview.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("action").GetString() == "error");
        Assert.Equal(3, errorRow.GetProperty("rowNumber").GetInt32());
        Assert.Contains("Thứ tự hiển thị", errorRow.GetProperty("errors")[0].GetString());

        var commit = await client.PostAsync($"/api/imports/{preview.GetProperty("sessionId").GetGuid()}/commit", null);
        Assert.Equal(HttpStatusCode.BadRequest, commit.StatusCode);
        Assert.Contains("Không dòng nào được ghi", await MessageAsync(commit));

        var departments = await DataAsync(await client.GetAsync("/api/organizations/departments"));
        Assert.DoesNotContain(departments.EnumerateArray(), d => d.GetProperty("code").GetString() == a);

        // Tệp sai định dạng / thiếu cột → 400 rõ lý do.
        var wrongType = await PostFileAsync(client, "departments", file, "data.csv");
        Assert.Equal(HttpStatusCode.BadRequest, wrongType.StatusCode);
        var missingColumn = await PostFileAsync(client, "departments", BuildFile(new[] { "Mã đơn vị" }, new[] { a }), "a.xlsx");
        Assert.Equal(HttpStatusCode.BadRequest, missingColumn.StatusCode);
        Assert.Contains("Tên đơn vị", await MessageAsync(missingColumn));
    }

    // ===================== L5, L6: import cán bộ =====================

    [SkippableFact]
    public async Task L5_L6_ImportUsers_CreatesAccounts_ResultFileOnce_LoginForcesPasswordChange_ReimportIsRowError()
    {
        SkipIfNoDatabase();
        var client = await AdminAsync();
        var deptCode = UniqueCode("PH");
        var cellCode = UniqueCode("CB");
        var deptId = (await DataAsync(await client.PostAsJsonAsync("/api/organizations/departments", new { code = deptCode, name = "Phòng cho cán bộ" })))
            .GetProperty("id").GetGuid();
        var cellId = (await DataAsync(await client.PostAsJsonAsync("/api/organizations/branches", new { code = cellCode, name = "Chi bộ cho cán bộ" })))
            .GetProperty("id").GetGuid();

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var user1 = $"imp_{suffix}_1";
        var user2 = $"imp_{suffix}_2";
        var headers = new[] { "Tên đăng nhập", "Họ và tên", "Email", "Số thẻ Đảng", "Chức danh", "Mã đơn vị công tác", "Mã tổ chức Đảng", "Thẩm quyền phê duyệt" };
        var file = BuildFile(headers,
            new[] { user1, "Nguyễn Văn Nhập", "nhap@example.vn", "0123456", "Trưởng phòng", deptCode.ToLowerInvariant(), cellCode, "CapTren" },
            new[] { user2, "Trần Thị Nhập", "", "", "", "", "", "CoSo" });

        var preview = await PreviewAsync(client, "users", file);
        Assert.True(preview.GetProperty("canCommit").GetBoolean(), preview.ToString());
        Assert.All(preview.GetProperty("rows").EnumerateArray(), r => Assert.Equal("create", r.GetProperty("action").GetString()));

        var commit = await client.PostAsync($"/api/imports/{preview.GetProperty("sessionId").GetGuid()}/commit", null);
        Assert.Equal(HttpStatusCode.OK, commit.StatusCode);
        var result = await DataAsync(commit);
        Assert.Equal(2, result.GetProperty("created").GetInt32());
        var token = result.GetProperty("resultFileToken").GetString();
        Assert.False(string.IsNullOrEmpty(token));

        // Tệp kết quả: tải được đúng một lần.
        var download = await client.GetAsync($"/api/imports/results/{token}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Contains("no-store", download.Headers.CacheControl?.ToString() ?? string.Empty);
        var passwords = ReadPasswords(await download.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/imports/results/{token}")).StatusCode);

        // Hồ sơ được tạo đúng tham chiếu, không lưu mật khẩu rõ.
        await ExecuteDbAsync(async db =>
        {
            var profile = await db.PartyMemberProfiles.SingleAsync(m => m.Username == user1);
            Assert.Equal(deptId, profile.DepartmentId);
            Assert.Equal(cellId, profile.PartyCellId);
            Assert.Equal(ApprovalAuthority.CapTren, profile.ApprovalAuthority);
            Assert.True(profile.IsPartyMember);
            Assert.True(profile.MustChangePassword);
            Assert.NotEqual(passwords[user1], profile.PasswordHash);
            var second = await db.PartyMemberProfiles.SingleAsync(m => m.Username == user2);
            Assert.Null(second.DepartmentId);
            Assert.Equal(ApprovalAuthority.CoSo, second.ApprovalAuthority);
        });

        // Đăng nhập bằng mật khẩu tạm trong tệp → thành công và bị buộc đổi mật khẩu.
        using var userClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var login = await userClient.PostAsJsonAsync("/api/auth/login", new { username = user1, password = passwords[user1] });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.True((await DataAsync(login)).GetProperty("mustChangePassword").GetBoolean());
        // Cán bộ mới không có quyền quản lý danh mục.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await userClient.PostAsJsonAsync("/api/organizations/departments", new { code = UniqueCode("PH"), name = "Không được" })).StatusCode);

        // L6: nhập lại cùng tệp → mọi dòng lỗi "đã có tài khoản", không cho xác nhận, tài khoản cũ giữ nguyên.
        var again = await PreviewAsync(client, "users", file);
        Assert.False(again.GetProperty("canCommit").GetBoolean());
        Assert.All(again.GetProperty("rows").EnumerateArray(), r =>
        {
            Assert.Equal("error", r.GetProperty("action").GetString());
            Assert.Contains("đã có tài khoản", r.GetProperty("errors")[0].GetString());
        });
        await ExecuteDbAsync(async db =>
            Assert.Equal(1, await db.PartyMemberProfiles.CountAsync(m => m.Username == user1)));
    }

    // ===================== Hỗ trợ =====================

    private static string UniqueCode(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    private async Task ExecuteDbAsync(Func<CongTacDangDbContext, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<CongTacDangDbContext>());
    }

    /// <summary>Tạo một cán bộ và một hồ sơ đánh giá gắn với Phòng/Chi bộ (trực tiếp trong CSDL).</summary>
    private async Task<(Guid MemberId, Guid RecordId)> SeedMemberAndRecordAsync(Guid? departmentId, Guid? partyCellId)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var member = new PartyMemberProfile
        {
            Username = $"seed_{suffix}",
            FullName = $"Cán bộ {suffix}",
            PasswordHash = "x",
            DepartmentId = departmentId,
            PartyCellId = partyCellId,
            IsActive = true
        };
        var period = new EvaluationPeriod
        {
            Year = 2090 + Random.Shared.Next(0, 9),
            Quarter = EvaluationQuarter.Quy1,
            Name = $"Kỳ test {suffix}",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        var record = new EvaluationRecord { Period = period, Member = member, DepartmentId = departmentId, PartyCellId = partyCellId };

        await ExecuteDbAsync(async db =>
        {
            db.EvaluationRecords.Add(record);
            await db.SaveChangesAsync();
        });
        return (member.Id, record.Id);
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

    private static Task<HttpResponseMessage> PostFileAsync(HttpClient client, string kind, byte[] file, string fileName)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(file);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", fileName);
        return client.PostAsync($"/api/imports/{kind}/preview", content);
    }

    private static async Task<JsonElement> PreviewAsync(HttpClient client, string kind, byte[] file)
    {
        var response = await PostFileAsync(client, kind, file, "du-lieu.xlsx");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await DataAsync(response);
    }

    private static Dictionary<string, string> ReadPasswords(byte[] xlsx)
    {
        using var workbook = new XLWorkbook(new MemoryStream(xlsx));
        var sheet = workbook.Worksheets.First();
        var header = sheet.CellsUsed().First(c => c.GetString() == "Tên đăng nhập");
        var passwords = new Dictionary<string, string>();
        for (var row = header.Address.RowNumber + 1; row <= sheet.LastRowUsed()!.RowNumber(); row++)
            passwords[sheet.Cell(row, header.Address.ColumnNumber).GetString()] = sheet.Cell(row, header.Address.ColumnNumber + 2).GetString();
        return passwords;
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").Clone();
    }

    private static async Task<string> MessageAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("message").GetString() ?? string.Empty;
    }
}
