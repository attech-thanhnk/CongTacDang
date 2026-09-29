using ClosedXML.Excel;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Imports;
using CongTacDang.Application.Imports.Definitions;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Enums;
using CongTacDang.Infrastructure.Imports;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>
/// Kiểm thử khung nhập dữ liệu (task 10, T-64): đọc/ghi Excel, parse/validate từng loại
/// (thiếu cột, sai mã tham chiếu, trùng trong tệp, trùng CSDL, formula injection), phiên và tệp kết quả.
/// </summary>
public class ImportFrameworkTests
{
    private static readonly Guid DeptKh = Guid.NewGuid();
    private static readonly Guid DeptOld = Guid.NewGuid();
    private static readonly Guid CellVp = Guid.NewGuid();
    private static readonly Guid CellClosed = Guid.NewGuid();

    // ===================== Excel =====================

    [Fact]
    public void Template_HasDataAndGuideSheets_WithFixedHeadersAndListValidation()
    {
        var definition = new DepartmentImportDefinition(new FakeLookup(), null!);
        var bytes = new ClosedXmlImportWorkbook().CreateTemplate(definition);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.True(workbook.Worksheets.Contains(ImportLimits.DataSheetName));
        Assert.True(workbook.Worksheets.Contains(ImportLimits.GuideSheetName));

        var sheet = workbook.Worksheet(ImportLimits.DataSheetName);
        var headers = definition.TemplateColumns.Select((c, i) => sheet.Cell(1, i + 1).GetString()).ToList();
        Assert.Equal(definition.TemplateColumns.Select(c => c.Header), headers);

        var statusColumn = definition.TemplateColumns.ToList().FindIndex(c => c.Key == CatalogImportDefinition.StatusKey) + 1;
        var validation = sheet.Cell(2, statusColumn).GetDataValidation();
        Assert.Equal(XLAllowedValues.List, validation.AllowedValues);
        Assert.Contains(CatalogImportDefinition.StatusInactive, validation.Value);
    }

    [Fact]
    public void ReadRows_TrimsValues_SkipsEmptyRows_AndKeepsExcelRowNumbers()
    {
        var definition = new DepartmentImportDefinition(new FakeLookup(), null!);
        var bytes = BuildWorkbook(new[] { "Mã Phòng", "Tên Phòng" },
            new[] { "  ph-a ", " Phòng A  " },
            new[] { "", "" },
            new[] { "PH-B", "Phòng B" },
            new[] { "", "" },
            new[] { "  ", "" });

        var rows = new ClosedXmlImportWorkbook().ReadRows(new MemoryStream(bytes), definition);

        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows[0].RowNumber);
        Assert.Equal("ph-a", rows[0].Get(CatalogImportDefinition.CodeKey));
        Assert.Equal("Phòng A", rows[0].Get(CatalogImportDefinition.NameKey));
        Assert.Equal(4, rows[1].RowNumber);
        Assert.Equal(string.Empty, rows[1].Get(CatalogImportDefinition.StatusKey)); // cột không có trong tệp → rỗng
    }

    [Fact]
    public void ReadRows_MissingRequiredColumn_Throws400WithColumnName()
    {
        var definition = new UserImportDefinition(new FakeLookup(), new FakeAccounts());
        var bytes = BuildWorkbook(new[] { "Tên đăng nhập" }, new[] { "a" });

        var ex = Assert.Throws<ValidationException>(() => new ClosedXmlImportWorkbook().ReadRows(new MemoryStream(bytes), definition));
        Assert.Contains("Họ và tên", ex.Message); // task 14: "Thẩm quyền phê duyệt" không còn bắt buộc (suy ra từ chức vụ)
    }

    [Fact]
    public void ReadRows_HeaderMatchIgnoresCaseAndRequiredMarker()
    {
        var definition = new DepartmentImportDefinition(new FakeLookup(), null!);
        var bytes = BuildWorkbook(new[] { "mã phòng *", "TÊN PHÒNG" }, new[] { "X", "Y" });

        var rows = new ClosedXmlImportWorkbook().ReadRows(new MemoryStream(bytes), definition);
        Assert.Equal("X", Assert.Single(rows).Get(CatalogImportDefinition.CodeKey));
    }

    [Fact]
    public void ReadRows_MoreThanMaxRows_Throws()
    {
        var definition = new DepartmentImportDefinition(new FakeLookup(), null!);
        var data = Enumerable.Range(1, ImportLimits.MaxRows + 1).Select(i => new[] { $"C{i}", $"N{i}" }).ToArray();
        var bytes = BuildWorkbook(new[] { "Mã Phòng", "Tên Phòng" }, data);

        var ex = Assert.Throws<ValidationException>(() => new ClosedXmlImportWorkbook().ReadRows(new MemoryStream(bytes), definition));
        Assert.Contains("2.000", ex.Message.Replace(",", "."));
    }

    [Fact]
    public void ReadRows_NotAnXlsx_Throws400()
    {
        var definition = new DepartmentImportDefinition(new FakeLookup(), null!);
        Assert.Throws<ValidationException>(() =>
            new ClosedXmlImportWorkbook().ReadRows(new MemoryStream("not an excel file"u8.ToArray()), definition));
    }

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+84", "'+84")]
    [InlineData("-cmd", "'-cmd")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tx", "'\tx")]
    [InlineData("abc=1", "abc=1")]
    [InlineData("", "")]
    public void SanitizeCellText_PrefixesFormulaTriggers(string input, string expected)
        => Assert.Equal(expected, ClosedXmlImportWorkbook.SanitizeCellText(input));

    [Fact]
    public void ResultFile_EscapesFormulaInjection_AndStoresTextNotFormulas()
    {
        var table = new ImportResultTable("kq.xlsx", "Kết quả", "Tiêu đề", new[] { "Tên đăng nhập", "Mật khẩu tạm" },
            new[] { new string?[] { "=HYPERLINK(\"http://x\")", "-abc123" }, new string?[] { "binh", "Abc12345xyz9" } });

        var bytes = new ClosedXmlImportWorkbook().CreateResultFile(table);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("Kết quả");
        var cells = sheet.CellsUsed().ToList();
        Assert.DoesNotContain(cells, c => c.HasFormula);
        // Tiền tố ' được lưu theo cách chuẩn của Excel: ô văn bản có cờ quotePrefix (Excel/LibreOffice không coi là công thức,
        // hiển thị đúng giá trị gốc).
        var injected = cells.Single(c => c.GetString() == "=HYPERLINK(\"http://x\")");
        Assert.Equal(XLDataType.Text, injected.DataType);
        Assert.True(injected.Style.IncludeQuotePrefix);
        Assert.True(cells.Single(c => c.GetString() == "-abc123").Style.IncludeQuotePrefix);
        Assert.False(cells.Single(c => c.GetString() == "Abc12345xyz9").Style.IncludeQuotePrefix);
    }

    // ===================== Danh mục =====================

    [Fact]
    public async Task Catalog_ExistingCode_IsUpdate_NewCode_IsCreate_CaseInsensitive()
    {
        var analysis = await AnalyzeAsync(new DepartmentImportDefinition(new FakeLookup(), null!),
            Row(2, ("code", "ph-kh"), ("name", "Phòng Kế hoạch mới")),
            Row(3, ("code", "PH-MOI"), ("name", "Phòng mới")));

        Assert.False(analysis.HasErrors);
        Assert.Equal(ImportRowAction.Update, analysis.Rows[0].Action);
        Assert.Equal(ImportRowAction.Create, analysis.Rows[1].Action);
    }

    [Fact]
    public async Task Catalog_DuplicateInFile_DeletedCode_BadValues_AreRowErrors()
    {
        var analysis = await AnalyzeAsync(new DepartmentImportDefinition(new FakeLookup(), null!),
            Row(2, ("code", "PH-A"), ("name", "A")),
            Row(3, ("code", "ph-a"), ("name", "A lần 2")),
            Row(4, ("code", "PH-OLD"), ("name", "Đã xóa")),
            Row(5, ("code", "PH B"), ("name", "Có khoảng trắng")),
            Row(6, ("code", "PH-C"), ("name", "C"), ("sortOrder", "-1")),
            Row(7, ("code", "PH-D"), ("name", "D"), ("status", "Tạm dừng")),
            Row(8, ("code", "PH-E"), ("name", "")),
            Row(9, ("code", "PH-F"), ("name", "F"), ("status", "ngừng hoạt động")));

        Assert.True(analysis.HasErrors);
        Assert.Equal(ImportRowAction.Create, analysis.Rows[0].Action);
        Assert.Contains("trùng với dòng 2", Errors(analysis, 3));
        Assert.Contains("đã bị xóa", Errors(analysis, 4));
        Assert.Contains("khoảng trắng", Errors(analysis, 5));
        Assert.Contains("số nguyên không âm", Errors(analysis, 6));
        Assert.Contains("Chỉ nhận", Errors(analysis, 7));
        Assert.Contains("Thiếu \"Tên Phòng\"", Errors(analysis, 8));
        Assert.Equal(ImportRowAction.Create, analysis.Rows[7].Action); // giá trị hợp lệ không phân biệt hoa thường
        Assert.Equal(CatalogImportDefinition.StatusInactive, analysis.Rows[7].Data["status"]);
    }

    // ===================== Cán bộ =====================

    [Fact]
    public async Task Users_ValidRow_ResolvesCodes_AndIsCreate()
    {
        var definition = new UserImportDefinition(new FakeLookup(), new FakeAccounts());
        var source = UserRow(2, "nguyenvana", departmentCode: "ph-kh", partyCellCode: "CB-VP", approval: "captren");
        var parseErrors = new List<string>();
        var row = new ImportRow<UserImportRow>(source, definition.ParseRow(source, parseErrors));

        await definition.ValidateAsync(new[] { row }, default);

        Assert.Empty(parseErrors);
        Assert.False(row.HasErrors, string.Join(" ", row.Errors));
        Assert.Equal(ImportRowAction.Create, row.Action);
        Assert.Equal(DeptKh, row.Data.DepartmentId);
        Assert.Equal(CellVp, row.Data.PartyCellId);
        Assert.Equal(ApprovalAuthority.CapTren, row.Data.ApprovalAuthority);
    }

    [Fact]
    public async Task Users_InvalidReferences_Duplicates_AndBadValues_AreRowErrors()
    {
        var definition = new UserImportDefinition(new FakeLookup("DaCo"), new FakeAccounts());
        var rows = await AnalyzeAsync(definition,
            UserRow(2, "aa1", departmentCode: "PH-KHONGCO"),
            UserRow(3, "aa2", partyCellCode: "CB-DONG"),
            UserRow(4, "aa3", departmentCode: "PH-OLD"),
            UserRow(5, "bb1"),
            UserRow(6, "BB1"),
            UserRow(7, "daco"),
            UserRow(8, "c 1"),
            UserRow(9, "cc2", email: "khong-hop-le"),
            UserRow(10, "cc3", approval: "Khac"),
            UserRow(11, "cc4", approval: ""));

        Assert.Contains("không có trong danh mục", Errors(rows, 2));
        Assert.Contains("ngừng hoạt động", Errors(rows, 3));
        Assert.Contains("không có trong danh mục", Errors(rows, 4)); // Phòng đã xóa mềm
        Assert.Equal(ImportRowAction.Create, rows.Rows.Single(r => r.RowNumber == 5).Action);
        Assert.Contains("trùng với dòng 5", Errors(rows, 6));
        Assert.Contains("đã có tài khoản", Errors(rows, 7));
        Assert.Contains("khoảng trắng", Errors(rows, 8));
        Assert.Contains("Email", Errors(rows, 9));
        Assert.Contains("Chỉ nhận", Errors(rows, 10));
        // Task 14: để trống "Thẩm quyền phê duyệt" → suy ra từ chức vụ (không còn là lỗi).
        var blankAuthority = rows.Rows.Single(r => r.RowNumber == 11);
        Assert.Empty(blankAuthority.Errors);
        Assert.Equal(ImportRowAction.Create, blankAuthority.Action);
        Assert.All(rows.Rows.Where(r => r.Errors.Count > 0), r => Assert.Equal(ImportRowAction.Error, r.Action));
    }

    [Fact]
    public async Task Users_Commit_CreatesAccountsViaService_AndReturnsPasswordFile()
    {
        var accounts = new FakeAccounts();
        var definition = new UserImportDefinition(new FakeLookup(), accounts);
        var processor = new ImportProcessor<UserImportRow>(definition);
        var analysis = await processor.AnalyzeAsync(new[] { UserRow(2, "user1", departmentCode: "PH-KH"), UserRow(3, "user2") }, default);

        var result = await processor.CommitAsync(analysis, default);

        Assert.Equal(2, result.Created);
        Assert.Equal(new[] { "user1", "user2" }, accounts.Commands.Select(c => c.Username));
        Assert.Equal(DeptKh, accounts.Commands[0].DepartmentId);
        Assert.NotNull(result.ResultFile);
        Assert.Equal(new[] { "1", "user1", "Họ tên user1", "Pw-user1" }, result.ResultFile!.Rows[0]);
    }

    // ===================== Khung: quyền, phiên, tệp kết quả =====================

    [Fact]
    public async Task Kinds_AreFilteredByImportAndDataPermissions()
    {
        var withCatalog = CreateService(Guid.NewGuid(), PermissionCodes.SystemImport, PermissionCodes.CatalogManage);
        var kinds = (await withCatalog.Service.GetKindsAsync()).Select(k => k.Kind).OrderBy(k => k).ToList();
        Assert.Equal(new[] { "departments", "party-cells" }, kinds);

        var withoutImport = CreateService(Guid.NewGuid(), PermissionCodes.CatalogManage, PermissionCodes.SystemUsersManage);
        Assert.Empty(await withoutImport.Service.GetKindsAsync());

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => withCatalog.Service.GetTemplateAsync("users"));
        Assert.Contains("Quản lý tài khoản", ex.Message);
    }

    [Fact]
    public async Task Preview_RejectsNonXlsxAndTooLargeFiles()
    {
        var ctx = CreateService(Guid.NewGuid(), PermissionCodes.SystemImport, PermissionCodes.CatalogManage);
        await Assert.ThrowsAsync<ValidationException>(() =>
            ctx.Service.PreviewAsync("departments", "data.csv", 10, new MemoryStream(new byte[10])));
        await Assert.ThrowsAsync<ValidationException>(() =>
            ctx.Service.PreviewAsync("departments", "data.xlsx", ImportLimits.MaxFileBytes + 1, new MemoryStream(new byte[10])));
    }

    [Fact]
    public async Task Commit_OnlyByCreator_Once_AndNotWhenRowsHaveErrors()
    {
        var userId = Guid.NewGuid();
        var sessions = new InMemoryImportSessionStore();
        var owner = CreateService(userId, sessions, PermissionCodes.SystemImport, PermissionCodes.CatalogManage);
        var other = CreateService(Guid.NewGuid(), sessions, PermissionCodes.SystemImport, PermissionCodes.CatalogManage);

        var okFile = BuildWorkbook(new[] { "Mã Phòng", "Tên Phòng" }, new[] { "PH-NEW", "Phòng mới" });
        var preview = await owner.Service.PreviewAsync("departments", "a.xlsx", okFile.Length, new MemoryStream(okFile));
        Assert.True(preview.CanCommit);

        await Assert.ThrowsAsync<ForbiddenException>(() => other.Service.CommitAsync(preview.SessionId));

        // Ghi thật cần repository → ở đây chỉ kiểm tra khung: định nghĩa ném khi thiếu repository, transaction bị hủy.
        await Assert.ThrowsAnyAsync<Exception>(() => owner.Service.CommitAsync(preview.SessionId));
        Assert.Equal(0, owner.Audit.Count);
        await Assert.ThrowsAsync<NotFoundException>(() => owner.Service.CommitAsync(preview.SessionId)); // chỉ một lần

        var badFile = BuildWorkbook(new[] { "Mã Phòng", "Tên Phòng" }, new[] { "PH-X", "X" }, new[] { "PH-X", "X trùng" });
        var bad = await owner.Service.PreviewAsync("departments", "b.xlsx", badFile.Length, new MemoryStream(badFile));
        Assert.False(bad.CanCommit);
        Assert.Equal(1, bad.Summary.Error);
        var ex = await Assert.ThrowsAsync<ValidationException>(() => owner.Service.CommitAsync(bad.SessionId));
        Assert.Contains("Không dòng nào được ghi", ex.Message);
        Assert.Equal(0, owner.Audit.Count);
    }

    [Fact]
    public void ResultStore_TakeOnce_OwnerOnly_AndExpires()
    {
        var time = new ManualTime(DateTimeOffset.UtcNow);
        var store = new InMemoryImportResultStore(time);
        var owner = Guid.NewGuid();

        var (token, _) = store.Add(owner, "a.xlsx", new byte[] { 1, 2, 3 });
        Assert.Null(store.Take(token, Guid.NewGuid()));        // người khác không lấy được…
        Assert.NotNull(store.Take(token, owner));              // …và không làm mất tệp của người tạo
        Assert.Null(store.Take(token, owner));                 // chỉ một lần

        var (expiring, _) = store.Add(owner, "b.xlsx", new byte[] { 1 });
        time.Advance(ImportLimits.SessionLifetime + TimeSpan.FromSeconds(1));
        Assert.Null(store.Take(expiring, owner));
    }

    [Fact]
    public void SessionStore_ExpiresAfter30Minutes()
    {
        var time = new ManualTime(DateTimeOffset.UtcNow);
        var store = new InMemoryImportSessionStore(time);
        var session = store.Create("departments", Guid.NewGuid(), "a.xlsx", Array.Empty<ImportSourceRow>());

        time.Advance(TimeSpan.FromMinutes(29));
        Assert.NotNull(store.Peek(session.Id));
        time.Advance(TimeSpan.FromMinutes(2));
        Assert.Null(store.Peek(session.Id));
        Assert.Null(store.Take(session.Id));
    }

    // ===================== Hỗ trợ =====================

    private static ImportSourceRow Row(int number, params (string Key, string Value)[] values)
        => new(number, values.ToDictionary(v => v.Key, v => v.Value));

    private static ImportSourceRow UserRow(int number, string username, string? departmentCode = null, string? partyCellCode = null,
        string approval = "CoSo", string? email = null)
        => Row(number,
            (UserImportDefinition.UsernameKey, username),
            (UserImportDefinition.FullNameKey, $"Họ tên {username}"),
            (UserImportDefinition.EmailKey, email ?? string.Empty),
            (UserImportDefinition.DepartmentKey, departmentCode ?? string.Empty),
            (UserImportDefinition.PartyCellKey, partyCellCode ?? string.Empty),
            (UserImportDefinition.ApprovalKey, approval));

    private static Task<ImportAnalysis> AnalyzeAsync<TRow>(IImportDefinition<TRow> definition, params ImportSourceRow[] rows)
        where TRow : class
        => new ImportProcessor<TRow>(definition).AnalyzeAsync(rows, default);

    private static string Errors(ImportAnalysis analysis, int rowNumber)
        => string.Join(" ", analysis.Rows.Single(r => r.RowNumber == rowNumber).Errors);

    private static byte[] BuildWorkbook(string[] headers, params string[][] rows)
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

    private sealed record ServiceContext(ImportService Service, FakeAudit Audit);

    private static ServiceContext CreateService(Guid userId, params string[] codes)
        => CreateService(userId, new InMemoryImportSessionStore(), codes);

    private static ServiceContext CreateService(Guid userId, IImportSessionStore sessions, params string[] codes)
    {
        var lookup = new FakeLookup();
        var processors = new IImportProcessor[]
        {
            new ImportProcessor<CatalogImportRow>(new DepartmentImportDefinition(lookup, null!)),
            new ImportProcessor<CatalogImportRow>(new PartyCellImportDefinition(lookup, null!)),
            new ImportProcessor<UserImportRow>(new UserImportDefinition(lookup, new FakeAccounts()))
        };
        var audit = new FakeAudit();
        var service = new ImportService(processors, new ClosedXmlImportWorkbook(), sessions, new InMemoryImportResultStore(),
            audit, new FakeUnitOfWork(), new FakeCurrentUser(userId), new FakeResolver(codes));
        return new ServiceContext(service, audit);
    }

    private sealed class FakeLookup : IImportLookup
    {
        private readonly HashSet<string> _usernames;

        public FakeLookup(params string[] usernames)
            => _usernames = usernames.Select(u => u.ToLowerInvariant()).ToHashSet();

        public Task<IReadOnlyList<CatalogLookupEntry>> GetDepartmentsAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CatalogLookupEntry>>(new[]
            {
                new CatalogLookupEntry(DeptKh, "PH-KH", "Phòng Kế hoạch", true, false),
                new CatalogLookupEntry(DeptOld, "PH-OLD", "Phòng cũ", false, true)
            });

        public Task<IReadOnlyList<CatalogLookupEntry>> GetPartyCellsAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CatalogLookupEntry>>(new[]
            {
                new CatalogLookupEntry(CellVp, "CB-VP", "Chi bộ Văn phòng", true, false),
                new CatalogLookupEntry(CellClosed, "CB-DONG", "Chi bộ đã ngừng", false, false)
            });

        public Task<IReadOnlySet<string>> FindExistingUsernamesAsync(IReadOnlyCollection<string> usernames, CancellationToken ct)
            => Task.FromResult<IReadOnlySet<string>>(usernames.Where(_usernames.Contains).ToHashSet());
    }

    private sealed class FakeAccounts : IUserAccountService
    {
        public List<CreateAccountCommand> Commands { get; } = new();

        public Task<CreatedAccount> CreateAsync(CreateAccountCommand cmd, CancellationToken ct = default)
        {
            Commands.Add(cmd);
            return Task.FromResult(new CreatedAccount(Guid.NewGuid(), cmd.Username, $"Pw-{cmd.Username}"));
        }

        public Task<CreatedAccount> StageCreateAsync(CreateAccountCommand cmd, CancellationToken ct = default) => CreateAsync(cmd, ct);

        // Các thao tác còn lại không được import dùng tới.
        public Task<PagedResult<AccountListItemDto>> SearchAsync(AccountSearchQuery query, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<AccountListItemDto> GetAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AccountListItemDto> UpdateAsync(Guid id, UpdateAccountCommand cmd, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UnlockAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CreatedAccount> ResetPasswordAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeAudit : IImportAuditLog
    {
        public int Count { get; private set; }

        public void Add(string kind, string displayName, string? fileName, int rowCount, int created, int updated) => Count++;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default) => operation();
        public void SetOriginalVersion(object entity, uint? version) { }
        public uint GetVersion(object entity) => 0;
    }

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public FakeCurrentUser(Guid id) => UserId = id;
        public Guid? UserId { get; }
        public string UserName => "test";
        public string? IpAddress => null;
        public string? UserAgent => null;
        public string? RequestPath => null;
    }

    private sealed class FakeResolver : IPermissionResolver
    {
        private readonly string[] _codes;

        public FakeResolver(string[] codes) => _codes = codes;

        public Task<EffectivePermissions> GetAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(new EffectivePermissions(userId,
                _codes.Select(c => new PermissionGrant(c, ScopeType.Global, null, Guid.Empty, "test"))));
    }

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now;

        public ManualTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }
}
