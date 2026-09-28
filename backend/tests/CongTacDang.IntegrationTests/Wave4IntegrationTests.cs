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
/// Test tích hợp các chỗ nối sau khi gộp Đợt 4 (task 08 ║ 09 ║ 10): nhập cán bộ ghi cả lô trong một transaction,
/// thông tin phiên có <c>grants</c>, danh sách tài khoản lọc theo phạm vi <c>system.users.read</c>.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class Wave4IntegrationTests
{
    private readonly ApiFactory _factory;

    public Wave4IntegrationTests(ApiFactory factory) => _factory = factory;

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
        var headers = new[] { "Tên đăng nhập", "Họ và tên", "Mã Phòng", "Thẩm quyền phê duyệt" };
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
