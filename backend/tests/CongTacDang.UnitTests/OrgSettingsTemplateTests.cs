using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;
using CongTacDang.Infrastructure.Documents;
using CongTacDang.Infrastructure.Documents.Forms;
using CongTacDang.Infrastructure.Services;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Task 17 — thông tin đơn vị trên biểu mẫu (T-80), kiểm tra tag và chọn phiên bản file mẫu (T-81).</summary>
public class OrgSettingsTemplateTests
{
    private static readonly FileWordTemplateStore Bundled = new();

    #region Thông tin đơn vị (T-80)

    [Fact]
    public void DefaultSettings_KeepPreviouslyHardcodedStrings()
    {
        var defaults = DataSeeder.DefaultOrganizationSettings();

        Assert.Equal("ĐẢNG BỘ CÔNG TY TNHH KỸ THUẬT QUẢN LÝ BAY", defaults.PartyCommitteeName);
        Assert.Equal("ĐẢNG BỘ TỔNG CÔNG TY QUẢN LÝ BAY VIỆT NAM", defaults.SuperiorPartyName);
        Assert.Equal("ATTECH", defaults.ShortName);
        Assert.Equal("Hà Nội", defaults.Location);
        // Mẫu 10 in sẵn "TỔNG CÔNG TY QUẢN LÝ BAY VIỆT NAM" — tag in hoa từ giá trị mặc định phải trùng khớp.
        var fields = OrganizationTemplateFields.From(defaults.PartyCommitteeName, defaults.SuperiorPartyName, defaults.CompanyName,
            defaults.ParentCompanyName, defaults.ShortName, defaults.Location);
        Assert.Equal("TỔNG CÔNG TY QUẢN LÝ BAY VIỆT NAM", fields.ParentCompanyNameUpper);
        Assert.Equal("CÔNG TY TNHH KỸ THUẬT QUẢN LÝ BAY", fields.CompanyNameUpper);
    }

    [Fact]
    public void OrganizationFields_EmptyValue_KeepsTemplateDefaultText()
    {
        var fields = OrganizationTemplateFields.From(" ", null, "", null, null, "  ");
        var data = TemplateDataBinder.Bind(fields);

        Assert.All(data.Fields.Values, Assert.Null);
    }

    [Fact]
    public void Mau02_RendersOrganizationTags_FromSettings()
    {
        var (_, records) = DocumentTemplateTests.SampleData();
        var org = OrganizationTemplateFields.From("ĐẢNG BỘ THỬ", "ĐẢNG BỘ CẤP TRÊN THỬ", "Công ty Thử nghiệm", "Tổng công ty Mẹ", "TN", "Đà Nẵng");
        var data = TemplateDataBinder.Bind(Mau02Data.From(records[0], new Dictionary<Guid, string>()))
            .WithShared(TemplateDataBinder.Bind(org));

        var result = DocxTemplateEngine.Render(Bundled.Load(Mau02Data.TemplateFileName), data);

        Assert.Empty(result.MissingTags);
        var text = BodyText(result.Content);
        Assert.Contains("CÔNG TY THỬ NGHIỆM", text);
        Assert.Contains("Đà Nẵng, ngày", text);
        Assert.DoesNotContain("CƠ QUAN QUẢN LÝ CẤP TRÊN", text);
    }

    [Fact]
    public void WithShared_FormDataWins()
    {
        var data = new TemplateData().Field("ORG_LOCATION", "Riêng").WithShared(new TemplateData().Field("ORG_LOCATION", "Chung").Field("X", "1"));

        Assert.Equal("Riêng", data.Fields["ORG_LOCATION"]);
        Assert.Equal("1", data.Fields["X"]);
    }

    #endregion

    #region Kiểm tra tag (T-81)

    public static IEnumerable<object[]> Forms() => WordFormCatalog.All.Select(f => new object[] { f.Code });

    [Theory]
    [MemberData(nameof(Forms))]
    public void BundledTemplates_PassValidation_WithoutWarnings(string code)
    {
        var form = WordFormCatalog.Find(code)!;

        var result = WordTemplateValidator.Check(form, Bundled.Load(form.TemplateFileName));

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.Empty(result.UnknownTags);
        Assert.Empty(result.MissingRequiredTags);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void UnknownTag_IsError()
    {
        var form = WordFormCatalog.Find("MAU_02")!;
        var content = Modify(Bundled.Load(form.TemplateFileName), body =>
            body.PrependChild(new Paragraph(new SdtRun(
                new SdtProperties(new Tag { Val = "TAG_LA" }, new SdtId { Val = 990001 }),
                new SdtContentRun(new Run(new Text("x")))))));

        var result = WordTemplateValidator.Check(form, content);

        Assert.False(result.IsValid);
        Assert.Equal(new[] { "TAG_LA" }, result.UnknownTags);
        Assert.Contains(result.Errors, e => e.Contains("TAG_LA"));
    }

    [Fact]
    public void MissingRequiredTag_IsWarningOnly()
    {
        var form = WordFormCatalog.Find("MAU_02")!;
        var content = Modify(Bundled.Load(form.TemplateFileName), body =>
        {
            foreach (var sdt in body.Descendants<SdtElement>().Where(s => s.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value == "FULL_NAME").ToList())
                sdt.Remove();
        });

        var result = WordTemplateValidator.Check(form, content);

        Assert.True(result.IsValid);
        Assert.Equal(new[] { "FULL_NAME" }, result.MissingRequiredTags);
        Assert.Contains(result.Warnings, w => w.Contains("FULL_NAME"));
    }

    [Fact]
    public void OptionalOrganizationTags_MayBeAbsent()
    {
        var form = WordFormCatalog.Find("MAU_02")!;
        var content = Modify(Bundled.Load(form.TemplateFileName), body =>
        {
            foreach (var sdt in body.Descendants<SdtElement>().Where(s => s.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value?.StartsWith("ORG_") == true).ToList())
                sdt.Remove();
        });

        var result = WordTemplateValidator.Check(form, content);

        Assert.True(result.IsValid);
        Assert.Empty(result.MissingRequiredTags);
    }

    [Fact]
    public void ConditionBlock_OneBranchIsEnough()
    {
        var form = WordFormCatalog.Find("MAU_02")!;
        var conditions = form.FormTags.Where(t => t.Kind == TemplateTagCatalog.IfKind).ToList();
        Assert.NotEmpty(conditions);
        var ifTag = conditions[0].Tag;
        var content = Modify(Bundled.Load(form.TemplateFileName), body =>
        {
            foreach (var sdt in body.Descendants<SdtElement>().Where(s => string.Equals(s.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value, ifTag, StringComparison.OrdinalIgnoreCase)).ToList())
                sdt.Remove();
        });

        var result = WordTemplateValidator.Check(form, content);

        // Còn "ifnot:" nên không báo thiếu (khi template dùng cả hai nhánh).
        var hasIfNot = DocxTemplateEngine.GetTags(content).Any(t => string.Equals(t, "ifnot:" + conditions[0].ConditionName, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(!hasIfNot, result.MissingRequiredTags.Any(m => m.StartsWith(ifTag, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void NotAWordDocument_IsError()
    {
        var form = WordFormCatalog.Find("MAU_01")!;

        var result = WordTemplateValidator.Check(form, new byte[] { 0x50, 0x4B, 0x03, 0x04, 1, 2, 3, 4, 5 });

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void TagCatalog_IncludesRowTagsAndCommonTags()
    {
        var form = WordFormCatalog.Find("MAU_02")!;
        var catalog = WordTemplateService.TagCatalog(form);

        Assert.Contains(catalog, t => t.Tag == "repeat:TASKS" && t.Required);
        Assert.Contains(catalog, t => t.Tag == "T_NAME" && t.Within == "TASKS");
        Assert.Contains(catalog, t => t.Tag == "ORG_LOCATION" && t.IsCommon && !t.Required);
    }

    #endregion

    #region Chọn phiên bản (T-81)

    [Fact]
    public async Task Store_UsesActiveVersion_FromStorage()
    {
        var storage = new MemoryStorage();
        storage.Files["word-templates/mau_02/v2.docx"] = new byte[] { 1, 2, 3 };
        var lookup = new FakeLookup(new WordTemplateVersion { TemplateCode = "MAU_02", VersionNumber = 2, ObjectKey = "word-templates/mau_02/v2.docx", IsActive = true });
        var store = new WordTemplateStore(lookup, storage, Bundled);

        var bytes = await store.LoadAsync(Mau02Data.TemplateFileName);

        Assert.Equal(new byte[] { 1, 2, 3 }, bytes);
        Assert.Equal("MAU_02", lookup.LastCode);
    }

    [Fact]
    public async Task Store_NoActiveVersion_FallsBackToBundled()
    {
        var store = new WordTemplateStore(new FakeLookup(null), new MemoryStorage(), Bundled);

        var bytes = await store.LoadAsync(Mau01Data.TemplateFileName);

        Assert.Equal(Bundled.Load(Mau01Data.TemplateFileName), bytes);
    }

    [Fact]
    public async Task Store_ActiveFileMissing_FallsBackToBundled()
    {
        var lookup = new FakeLookup(new WordTemplateVersion { TemplateCode = "MAU_10", VersionNumber = 1, ObjectKey = "mat-tep.docx", IsActive = true });
        var store = new WordTemplateStore(lookup, new MemoryStorage(), Bundled);

        var bytes = await store.LoadAsync(Mau10Data.TemplateFileName);

        Assert.Equal(Bundled.Load(Mau10Data.TemplateFileName), bytes);
    }

    #endregion

    #region Hỗ trợ

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

    private static string BodyText(byte[] content)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(content), false);
        return doc.MainDocumentPart!.Document!.Body!.InnerText;
    }

    private sealed class FakeLookup : IActiveWordTemplateLookup
    {
        private readonly WordTemplateVersion? _active;

        public FakeLookup(WordTemplateVersion? active) => _active = active;

        public string? LastCode { get; private set; }

        public Task<WordTemplateVersion?> FindActiveAsync(string templateCode, CancellationToken ct = default)
        {
            LastCode = templateCode;
            return Task.FromResult(_active != null && _active.TemplateCode == templateCode ? _active : null);
        }
    }

    private sealed class MemoryStorage : IFileStorageService
    {
        public Dictionary<string, byte[]> Files { get; } = new();

        public Task<string> SaveFileAsync(Stream fileStream, string objectKey, string contentType = "application/octet-stream")
        {
            using var ms = new MemoryStream();
            fileStream.CopyTo(ms);
            Files[objectKey] = ms.ToArray();
            return Task.FromResult(objectKey);
        }

        public Task<Stream?> GetFileStreamAsync(string objectKey) =>
            Task.FromResult<Stream?>(Files.TryGetValue(objectKey, out var b) ? new MemoryStream(b) : null);

        public Task DeleteFileAsync(string objectKey)
        {
            Files.Remove(objectKey);
            return Task.CompletedTask;
        }

        public bool FileExists(string objectKey) => Files.ContainsKey(objectKey);

        public Task<string?> GetDownloadUrlAsync(Guid attachmentId, TimeSpan? expiry = null) => Task.FromResult<string?>(null);
    }

    #endregion
}
