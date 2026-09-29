using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Infrastructure.Data;
using CongTacDang.IntegrationTests.Infrastructure;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Test tích hợp task 17: thông tin đơn vị trên biểu mẫu (T-80), quản lý file mẫu Word theo phiên bản (T-81).
/// Các test sửa cài đặt/phiên bản dùng chung CSDL nên luôn khôi phục về mặc định khi xong.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class OrgSettingsTemplateIntegrationTests
{
    private readonly ApiFactory _factory;

    public OrgSettingsTemplateIntegrationTests(ApiFactory factory) => _factory = factory;

    private void SkipIfNoDatabase() => Skip.If(_factory.SkipReason != null, _factory.SkipReason);

    private async Task<HttpClient> ManagerAsync() => await LoginWithAsync(
        PermissionCodes.SystemSettingsManage, PermissionCodes.SystemTemplatesManage,
        PermissionCodes.ReportExport, PermissionCodes.EvaluationRead);

    private async Task<HttpClient> LoginWithAsync(params string[] codes)
    {
        var user = await _factory.CreateUserWithPermissionsAsync(codes);
        return await _factory.LoginAsAsync(user.Username, user.Password, distinctClientIp: true);
    }

    // ===================== S1: đổi thông tin đơn vị → Excel/Word dùng giá trị mới =====================

    [SkippableFact]
    public async Task S1_ChangeOrganizationSettings_ExcelAndWordUseNewValues()
    {
        SkipIfNoDatabase();
        var manager = await ManagerAsync();
        var (periodId, recordId) = await SeedRecordAsync();
        var s = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

        try
        {
            var update = await manager.PutAsJsonAsync("/api/settings/organization", new
            {
                partyCommitteeName = $"ĐẢNG BỘ THỬ NGHIỆM {s}",
                superiorPartyName = $"ĐẢNG BỘ CẤP TRÊN {s}",
                companyName = $"Công ty Thử nghiệm {s}",
                parentCompanyName = $"Tổng công ty Mẹ {s}",
                shortName = $"TN{s}",
                location = "Đà Nẵng",
                systemName = $"Hệ thống {s}"
            });
            Assert.True(update.StatusCode == HttpStatusCode.OK, await update.Content.ReadAsStringAsync());

            // Đọc lại (cache đã xóa khi sửa).
            var settings = await DataAsync(await manager.GetAsync("/api/settings/organization"));
            Assert.Equal($"ĐẢNG BỘ THỬ NGHIỆM {s}", settings.GetProperty("partyCommitteeName").GetString());

            // Excel Mẫu 14: dòng tiêu đề trái = tổ chức Đảng cấp trên / tên Đảng bộ, dòng địa danh.
            var form14 = await manager.GetAsync($"/api/reports/form-14?periodId={periodId}");
            Assert.True(form14.StatusCode == HttpStatusCode.OK, await form14.Content.ReadAsStringAsync());
            using (var workbook = new XLWorkbook(new MemoryStream(await form14.Content.ReadAsByteArrayAsync())))
            {
                var sheet = workbook.Worksheets.First();
                // Task 19: Mẫu 14 theo bố cục HD03 — dòng 1 là "Mẫu 14", tiêu đề trái ở dòng 2–3, dòng địa danh ở khối phải.
                Assert.Equal($"ĐẢNG BỘ CẤP TRÊN {s}", sheet.Cell("A2").GetString());
                Assert.Equal($"ĐẢNG BỘ THỬ NGHIỆM {s}", sheet.Cell("A3").GetString());
                Assert.StartsWith("Đà Nẵng, ngày", sheet.Cell("I3").GetString());
            }

            // Danh sách cán bộ: tên tệp theo tên viết tắt.
            var cadres = await manager.GetAsync("/api/reports/cadres");
            Assert.Equal(HttpStatusCode.OK, cadres.StatusCode);
            Assert.Equal($"DanhSach_CanBo_TN{s}.xlsx", cadres.Content.Headers.ContentDisposition?.FileName?.Trim('"'));

            // Word Mẫu 02: tiêu đề trái = tên công ty (in hoa), dòng địa danh.
            var mau02 = await DocxTextAsync(manager, $"/api/reports/docx/mau-02/{recordId}");
            Assert.Contains($"CÔNG TY THỬ NGHIỆM {s}", mau02);
            Assert.Contains("Đà Nẵng, ngày", mau02);
            Assert.DoesNotContain("KỸ THUẬT QUẢN LÝ BAY", mau02);

            // Word Mẫu 11 toàn Đảng bộ: tổ chức Đảng cấp trên + tên Đảng bộ lập phiếu.
            var mau11 = await DocxTextAsync(manager, $"/api/reports/docx/mau-11?periodId={periodId}");
            Assert.Contains($"ĐẢNG BỘ CẤP TRÊN {s}", mau11);
            Assert.Contains($"ĐẢNG BỘ THỬ NGHIỆM {s}", mau11);
        }
        finally
        {
            await RestoreDefaultSettingsAsync(manager);
        }

        // Mặc định: biểu mẫu giữ đúng chuỗi trước đây.
        using var defaults = new XLWorkbook(new MemoryStream(await (await manager.GetAsync($"/api/reports/form-14?periodId={periodId}")).Content.ReadAsByteArrayAsync()));
        Assert.Equal("ĐẢNG BỘ CÔNG TY TNHH KỸ THUẬT QUẢN LÝ BAY", defaults.Worksheets.First().Cell("A3").GetString());
    }

    [SkippableFact]
    public async Task S2_Settings_ReadByAnyUser_UpdateRequiresPermission_Validated()
    {
        SkipIfNoDatabase();
        var plain = await LoginWithAsync();
        var get = await plain.GetAsync("/api/settings/organization");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var forbidden = await plain.PutAsJsonAsync("/api/settings/organization", new { partyCommitteeName = "X" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // Trang đăng nhập: phần công khai không cần đăng nhập.
        using var anonymous = _factory.CreateClient();
        var pub = await DataAsync(await anonymous.GetAsync("/api/settings/organization/public"));
        Assert.False(string.IsNullOrWhiteSpace(pub.GetProperty("systemName").GetString()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/settings/organization")).StatusCode);

        // Tên viết tắt dùng trong tên tệp: từ chối ký tự đặc biệt, không ghi gì.
        var manager = await ManagerAsync();
        var invalid = await manager.PutAsJsonAsync("/api/settings/organization", new
        {
            partyCommitteeName = "A", superiorPartyName = "B", companyName = "C", parentCompanyName = "D",
            shortName = "A/B", location = "E", systemName = "F"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var message = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync()).RootElement.GetProperty("message").GetString();
        Assert.Contains("Tên viết tắt", message);
        var after = await DataAsync(await manager.GetAsync("/api/settings/organization"));
        Assert.NotEqual("A/B", after.GetProperty("shortName").GetString());
    }

    // ===================== T1–T4: file mẫu Word theo phiên bản =====================

    [SkippableFact]
    public async Task T1_UploadNewVersion_ExportUsesIt_ReactivateOld_BackToOriginal()
    {
        SkipIfNoDatabase();
        var manager = await ManagerAsync();
        var (_, recordId) = await SeedRecordAsync();
        var original = await (await manager.GetAsync("/api/templates/MAU_02/original")).Content.ReadAsByteArrayAsync();

        try
        {
            var v1 = await UploadAsync(manager, "MAU_02", AddParagraph(original, "DAU_PHIEN_BAN_MOT"), "Bản thử 1");
            Assert.True(v1.StatusCode == HttpStatusCode.OK, await v1.Content.ReadAsStringAsync());
            var v1Id = (await DataAsync(v1)).GetProperty("version").GetProperty("id").GetGuid();
            Assert.Contains("DAU_PHIEN_BAN_MOT", await DocxTextAsync(manager, $"/api/reports/docx/mau-02/{recordId}"));

            var v2 = await UploadAsync(manager, "MAU_02", AddParagraph(original, "DAU_PHIEN_BAN_HAI"), "Bản thử 2");
            Assert.True(v2.StatusCode == HttpStatusCode.OK, await v2.Content.ReadAsStringAsync());
            var text2 = await DocxTextAsync(manager, $"/api/reports/docx/mau-02/{recordId}");
            Assert.Contains("DAU_PHIEN_BAN_HAI", text2);
            Assert.DoesNotContain("DAU_PHIEN_BAN_MOT", text2);

            // Kích hoạt lại bản cũ → xuất dùng bản cũ.
            var activate = await manager.PostAsync($"/api/templates/MAU_02/versions/{v1Id}/activate", null);
            Assert.True(activate.StatusCode == HttpStatusCode.OK, await activate.Content.ReadAsStringAsync());
            var text1 = await DocxTextAsync(manager, $"/api/reports/docx/mau-02/{recordId}");
            Assert.Contains("DAU_PHIEN_BAN_MOT", text1);
            Assert.DoesNotContain("DAU_PHIEN_BAN_HAI", text1);

            // Lịch sử: 2 phiên bản, đúng một bản đang kích hoạt; danh mục hiển thị bản đang dùng.
            var versions = (await DataAsync(await manager.GetAsync("/api/templates/MAU_02/versions"))).EnumerateArray().ToList();
            Assert.True(versions.Count >= 2);
            Assert.Single(versions, v => v.GetProperty("isActive").GetBoolean());
            var list = (await DataAsync(await manager.GetAsync("/api/templates"))).EnumerateArray().ToList();
            var mau02 = list.Single(t => t.GetProperty("code").GetString() == "MAU_02");
            Assert.Equal(v1Id, mau02.GetProperty("activeVersion").GetProperty("id").GetGuid());

            // Tải file đang dùng = bản 1.
            var current = await manager.GetAsync("/api/templates/MAU_02/current");
            Assert.Equal(HttpStatusCode.OK, current.StatusCode);
            Assert.Contains("DAU_PHIEN_BAN_MOT", DocxText(await current.Content.ReadAsByteArrayAsync()));
        }
        finally
        {
            await manager.PostAsync("/api/templates/MAU_02/use-original", null);
        }

        var back = await DocxTextAsync(manager, $"/api/reports/docx/mau-02/{recordId}");
        Assert.DoesNotContain("DAU_PHIEN_BAN_MOT", back);
        Assert.DoesNotContain("DAU_PHIEN_BAN_HAI", back);
    }

    [SkippableFact]
    public async Task T2_MissingRequiredTag_Warns_UnknownTag_Rejected()
    {
        SkipIfNoDatabase();
        var manager = await ManagerAsync();
        var original = await (await manager.GetAsync("/api/templates/MAU_02/original")).Content.ReadAsByteArrayAsync();
        var before = (await DataAsync(await manager.GetAsync("/api/templates/MAU_02/versions"))).GetArrayLength();

        try
        {
            // Thiếu tag bắt buộc → cảnh báo, vẫn lưu.
            var check = await DataAsync(await UploadToAsync(manager, "/api/templates/MAU_02/check", RemoveTag(original, "FULL_NAME")));
            Assert.True(check.GetProperty("isValid").GetBoolean());
            Assert.Contains("FULL_NAME", check.GetProperty("missingRequiredTags").EnumerateArray().Select(e => e.GetString()));

            var missing = await UploadAsync(manager, "MAU_02", RemoveTag(original, "FULL_NAME"), "Thiếu họ tên", activate: false);
            Assert.True(missing.StatusCode == HttpStatusCode.OK, await missing.Content.ReadAsStringAsync());
            var saved = await DataAsync(missing);
            Assert.NotEmpty(saved.GetProperty("warnings").EnumerateArray());
            Assert.False(saved.GetProperty("version").GetProperty("isActive").GetBoolean());

            // Tag lạ → 400, không lưu.
            var unknown = await UploadAsync(manager, "MAU_02", AddTag(original, "TAG_KHONG_CO"), "Tag lạ");
            Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
            var rejected = JsonDocument.Parse(await unknown.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
            Assert.False(rejected.GetProperty("isValid").GetBoolean());
            Assert.Contains("TAG_KHONG_CO", rejected.GetProperty("unknownTags").EnumerateArray().Select(e => e.GetString()));

            // Không phải .docx (sai chữ ký tệp) → 400.
            var fake = await UploadAsync(manager, "MAU_02", "không phải docx"u8.ToArray(), null);
            Assert.Equal(HttpStatusCode.BadRequest, fake.StatusCode);

            var after = (await DataAsync(await manager.GetAsync("/api/templates/MAU_02/versions"))).GetArrayLength();
            Assert.Equal(before + 1, after);
        }
        finally
        {
            await manager.PostAsync("/api/templates/MAU_02/use-original", null);
        }
    }

    [SkippableFact]
    public async Task T3_WithoutPermission_Forbidden()
    {
        SkipIfNoDatabase();
        var plain = await LoginWithAsync(PermissionCodes.SystemSettingsManage);
        Assert.Equal(HttpStatusCode.Forbidden, (await plain.GetAsync("/api/templates")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await plain.GetAsync("/api/templates/MAU_02/original")).StatusCode);
        var upload = await UploadAsync(plain, "MAU_02", new byte[] { 0x50, 0x4B, 0x03, 0x04 }, null);
        Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await plain.PostAsync($"/api/templates/MAU_02/versions/{Guid.NewGuid()}/activate", null)).StatusCode);

        var reader = await LoginWithAsync(PermissionCodes.SystemTemplatesManage);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PutAsJsonAsync("/api/settings/organization", new { shortName = "X" })).StatusCode);
    }

    [SkippableFact]
    public async Task T4_Seeder_GrantsNewPermissionsToAdministratorRole()
    {
        SkipIfNoDatabase();
        await _factory.WithDbAsync(async db =>
        {
            var admin = await db.Roles.Include(r => r.Permissions).Where(r => r.IsProtected && r.IsSystem).OrderBy(r => r.CreatedAt).FirstAsync();
            var codes = admin.Permissions.Select(p => p.Code).ToList();
            Assert.Contains(PermissionCodes.SystemSettingsManage, codes);
            Assert.Contains(PermissionCodes.SystemTemplatesManage, codes);
            Assert.True(await db.Set<OrganizationSettings>().AnyAsync());
        });
    }

    // ===================== Hỗ trợ =====================

    private async Task<(Guid PeriodId, Guid RecordId)> SeedRecordAsync()
    {
        var member = await _factory.CreateUserAsync(fullName: "Cán bộ biểu mẫu IT");
        var period = new EvaluationPeriod
        {
            Year = 2080 + Random.Shared.Next(0, 9),
            Quarter = EvaluationQuarter.Quy3,
            Name = $"Kỳ biểu mẫu {Guid.NewGuid():N}"[..40],
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        var record = new EvaluationRecord { Period = period, MemberId = member.Id, UpdatedAt = DateTime.UtcNow };
        await _factory.WithDbAsync(async db =>
        {
            db.EvaluationRecords.Add(record);
            await db.SaveChangesAsync();
        });
        return (period.Id, record.Id);
    }

    private static async Task RestoreDefaultSettingsAsync(HttpClient client)
    {
        var d = DataSeeder.DefaultOrganizationSettings();
        var response = await client.PutAsJsonAsync("/api/settings/organization", new
        {
            d.PartyCommitteeName, d.SuperiorPartyName, d.CompanyName, d.ParentCompanyName, d.ShortName, d.Location, d.SystemName
        });
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, string code, byte[] content, string? note, bool activate = true)
    {
        var extra = new Dictionary<string, string> { ["activate"] = activate ? "true" : "false" };
        if (note != null)
            extra["note"] = note;
        return UploadToAsync(client, $"/api/templates/{code}/versions", content, extra);
    }

    private static Task<HttpResponseMessage> UploadToAsync(HttpClient client, string url, byte[] content, Dictionary<string, string>? fields = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        form.Add(file, "file", "mau-thu.docx");
        foreach (var (key, value) in fields ?? new Dictionary<string, string>())
            form.Add(new StringContent(value), key);
        return client.PostAsync(url, form);
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonDocument.Parse(body).RootElement.GetProperty("data").Clone();
    }

    private static async Task<string> DocxTextAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return DocxText(await response.Content.ReadAsByteArrayAsync());
    }

    private static string DocxText(byte[] content)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(content), false);
        return doc.MainDocumentPart!.Document!.Body!.InnerText;
    }

    private static byte[] Modify(byte[] content, Action<Body> change)
    {
        using var ms = new MemoryStream();
        ms.Write(content);
        ms.Position = 0;
        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            change(doc.MainDocumentPart!.Document!.Body!);
            doc.MainDocumentPart.Document.Save();
        }
        return ms.ToArray();
    }

    private static byte[] AddParagraph(byte[] content, string text) =>
        Modify(content, body => body.PrependChild(new Paragraph(new Run(new Text(text)))));

    private static byte[] AddTag(byte[] content, string tag) =>
        Modify(content, body => body.PrependChild(new Paragraph(new SdtRun(
            new SdtProperties(new Tag { Val = tag }, new SdtId { Val = 991234 }),
            new SdtContentRun(new Run(new Text("x")))))));

    private static byte[] RemoveTag(byte[] content, string tag) =>
        Modify(content, body =>
        {
            foreach (var sdt in body.Descendants<SdtElement>().Where(s => s.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value == tag).ToList())
                sdt.Remove();
        });
}
