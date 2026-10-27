using ClosedXML.Excel;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Imports;
using CongTacDang.Application.Imports.Definitions;
using CongTacDang.Domain.Enums;
using CongTacDang.Infrastructure.Imports;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Nhập Excel: đọc/kiểm tra từng loại, đọc/ghi file, phiên nhập; dữ liệu tham chiếu giả lập, không cần CSDL.</summary>
public class ImportTests
{
    #region Đơn vị chính quyền / tổ chức Đảng

    private static readonly Guid PhongType = Guid.NewGuid();
    private static readonly Guid CongTyType = Guid.NewGuid();
    private static readonly Guid ChiBoType = Guid.NewGuid();

    private static FakeLookup OrgLookup()
    {
        var root = new OrgUnitRef(Guid.NewGuid(), "CTY", "Công ty", null, CongTyType, true, false);
        var lookup = new FakeLookup();
        lookup.Departments.Add(root);
        lookup.Departments.Add(new OrgUnitRef(Guid.NewGuid(), "PH-KT", "Phòng Kỹ thuật", root.Id, PhongType, true, false));
        lookup.Departments.Add(new OrgUnitRef(Guid.NewGuid(), "PH-CU", "Phòng cũ", root.Id, PhongType, false, true));
        lookup.UnitTypes.Add(new UnitTypeRef(CongTyType, "Công ty", OrgSide.Administrative, true));
        lookup.UnitTypes.Add(new UnitTypeRef(PhongType, "Phòng", OrgSide.Administrative, true));
        lookup.UnitTypes.Add(new UnitTypeRef(ChiBoType, "Chi bộ", OrgSide.Party, true));
        return lookup;
    }

    private static ImportRawRow Row(int number, params (string Key, string Value)[] values)
        => new(number, values.ToDictionary(v => v.Key, v => v.Value));

    private static ImportRawRow Dept(int number, string code, string name, string parent, string type)
        => Row(number, ("code", code), ("name", name), ("parentCode", parent), ("unitType", type));

    [Fact]
    public async Task Departments_CreateUpdate_AndRowErrors()
    {
        var definition = new DepartmentImportDefinition(OrgLookup(), null!);
        var analysis = await definition.AnalyzeAsync(new[]
        {
            Dept(2, "ph-kt", "Phòng Kỹ thuật (mới)", "CTY", "phong"),   // đã có → cập nhật (mã không phân biệt hoa thường, loại không dấu)
            Dept(3, "PH-TC", "Phòng Tài chính", "CTY", "Phòng"),        // tạo mới
            Dept(4, "PH-TC", "Trùng mã", "CTY", "Phòng"),               // trùng trong file
            Dept(5, "PH-X", "", "CTY", "Phòng"),                        // thiếu tên
            Dept(6, "PH-Y", "Phòng Y", "KHONG-CO", "Phòng"),            // cấp trên không có
            Dept(7, "PH-Z", "Phòng Z", "CTY", "Ban"),                   // loại không có
            Dept(8, "PH-CU", "Phòng cũ", "CTY", "Phòng"),               // mã của đơn vị đã xóa
            Dept(9, "GOC2", "Gốc thứ hai", "", "Công ty"),              // cây đã có gốc
            Dept(10, "TO-1", "Tổ 1", "PH-TC", "Phòng"),                 // cấp trên nằm trong file
        });

        ImportRowResult R(int n) => analysis.Rows.Single(r => r.RowNumber == n);
        Assert.Equal(ImportRowAction.Update, R(2).Action);
        Assert.Equal(ImportRowAction.Create, R(3).Action);
        Assert.Contains(R(4).Errors, e => e.Contains("trùng với dòng 3"));
        Assert.Contains(R(5).Errors, e => e.Contains("Thiếu \"Tên đơn vị\""));
        Assert.Contains(R(6).Errors, e => e.Contains("KHONG-CO"));
        Assert.Contains(R(7).Errors, e => e.Contains("Loại \"Ban\""));
        Assert.Contains(R(8).Errors, e => e.Contains("đã xóa"));
        Assert.Contains(R(9).Errors, e => e.Contains("một gốc"));
        Assert.Equal(ImportRowAction.Create, R(10).Action);
        Assert.True(analysis.HasErrors);
        Assert.All(analysis.Rows.Where(r => !r.IsValid), r => Assert.Equal(ImportRowAction.Error, r.Action));
    }

    [Fact]
    public async Task Departments_ShortName_OptionalAndLimitedTo30Characters()
    {
        var definition = new DepartmentImportDefinition(OrgLookup(), null!);
        var columns = await definition.GetTemplateColumnsAsync();
        var shortName = columns.Single(c => c.Key == "shortName");
        Assert.Equal("Tên viết tắt", shortName.Header);
        Assert.False(shortName.Required);

        var analysis = await definition.AnalyzeAsync(new[]
        {
            Row(2, ("code", "PH-TC"), ("name", "Phòng Tài chính"), ("parentCode", "CTY"), ("unitType", "Phòng"), ("shortName", "TC")),
            Row(3, ("code", "PH-NS"), ("name", "Phòng Nhân sự"), ("parentCode", "CTY"), ("unitType", "Phòng"), ("shortName", new string('A', 31))),
            Dept(4, "PH-KH", "Phòng Kế hoạch", "CTY", "Phòng"),
        });

        Assert.True(analysis.Rows.Single(r => r.RowNumber == 2).IsValid);
        Assert.Contains(analysis.Rows.Single(r => r.RowNumber == 3).Errors, e => e.Contains("Tên viết tắt") && e.Contains("30"));
        Assert.True(analysis.Rows.Single(r => r.RowNumber == 4).IsValid);
    }

    [Fact]
    public async Task Departments_DetectCycleInFile()
    {
        var definition = new DepartmentImportDefinition(OrgLookup(), null!);
        var analysis = await definition.AnalyzeAsync(new[]
        {
            Dept(2, "A", "A", "B", "Phòng"),
            Dept(3, "B", "B", "A", "Phòng"),
            Dept(4, "PH-KT", "Phòng Kỹ thuật", "C", "Phòng"),
            Dept(5, "C", "C", "PH-KT", "Phòng")
        });
        Assert.All(analysis.Rows, r => Assert.Contains(r.Errors, e => e.Contains("vòng")));
    }

    [Fact]
    public void Departments_ParentsInFileAreWrittenFirst()
    {
        static ParsedImportRow<OrgUnitImportRow> P(int n, string code, string parent)
            => new(new ImportRowResult(Row(n)), new OrgUnitImportRow { Code = code, ParentCode = parent });
        var ordered = OrgUnitImportDefinition.OrderParentsFirst(new[] { P(2, "C", "B"), P(3, "B", "A"), P(4, "A", "CTY") });
        Assert.Equal(new[] { "A", "B", "C" }, ordered.Select(r => r.Data.Code));
    }

    [Fact]
    public async Task PartyCells_UseOwnSideUnitTypes()
    {
        var lookup = OrgLookup();
        var definition = new PartyCellImportDefinition(lookup, null!);
        var analysis = await definition.AnalyzeAsync(new[]
        {
            Row(2, ("code", "DU"), ("name", "Đảng ủy"), ("unitType", "Chi bộ")),
            Row(3, ("code", "CB-1"), ("name", "Chi bộ 1"), ("parentCode", "DU"), ("unitType", "Phòng")), // loại bên chính quyền
            Row(4, ("code", "CB-2"), ("name", "Chi bộ 2"), ("parentCode", "DU"), ("unitType", "Chi bộ"), ("sortOrder", "abc"))
        });
        Assert.Equal(ImportRowAction.Create, analysis.Rows[0].Action); // cây Đảng chưa có gốc
        Assert.Contains(analysis.Rows[1].Errors, e => e.Contains("Loại \"Phòng\""));
        Assert.Contains(analysis.Rows[2].Errors, e => e.Contains("số nguyên"));
    }

    #endregion

    #region Cán bộ + tài khoản

    private static (FakeLookup Lookup, Guid DeptId, Guid CellId) UserLookup()
    {
        var lookup = OrgLookup();
        var deptId = lookup.Departments.Single(d => d.Code == "PH-KT").Id;
        var cellId = Guid.NewGuid();
        lookup.PartyCells.Add(new OrgUnitRef(cellId, "CB-KT", "Chi bộ Kỹ thuật", null, ChiBoType, true, false));
        lookup.PartyCells.Add(new OrgUnitRef(Guid.NewGuid(), "CB-NGUNG", "Chi bộ ngừng", null, ChiBoType, false, false));
        lookup.Positions.Add(new PositionRef(Guid.NewGuid(), "Trưởng phòng", "M10", PositionSide.Administrative, true));
        lookup.Positions.Add(new PositionRef(Guid.NewGuid(), "Bí thư chi bộ", "M20", PositionSide.Party, true));
        lookup.Positions.Add(new PositionRef(Guid.NewGuid(), "Phó trưởng phòng", "M11", PositionSide.Administrative, true));
        lookup.Positions.Add(new PositionRef(Guid.NewGuid(), "Phó phòng", "M11", PositionSide.Administrative, true));
        lookup.Positions.Add(new PositionRef(Guid.NewGuid(), "Chức vụ cũ", null, PositionSide.Other, false));
        lookup.Users.Add(new UserRef(Guid.NewGuid(), "da.co", "Đã có", false, deptId, cellId, Array.Empty<Guid>()));
        lookup.Users.Add(new UserRef(Guid.NewGuid(), "da.xoa", "Đã xóa", true, null, null, Array.Empty<Guid>()));
        return (lookup, deptId, cellId);
    }

    private static ImportRawRow User(int number, string username, string fullName, params (string Key, string Value)[] more)
        => Row(number, new[] { ("username", username), ("fullName", fullName) }.Concat(more).ToArray());

    [Fact]
    public async Task Users_ParseAndValidate()
    {
        var (lookup, deptId, cellId) = UserLookup();
        var definition = new UserImportDefinition(lookup, new FakeGuard(), null!, null!, null!);
        var analysis = await definition.AnalyzeAsync(new[]
        {
            User(2, "Nguyen.Van.A", "Nguyễn Văn A", ("dateOfBirth", "15/08/1980"), ("departmentCode", "ph-kt"), ("partyCellCode", "CB-KT"),
                ("isPartyMember", "Có"), ("partyCardNumber", "0123"), ("positions", "Trưởng phòng; M20"), ("weightFrame", "K2")),
            User(3, "", "Thiếu tên đăng nhập"),
            User(4, "nguyen.van.b", ""),                                          // thiếu họ tên
            User(5, "nguyen.van.c", "C", ("dateOfBirth", "1980-08-15")),          // sai định dạng ngày
            User(6, "nguyen.van.d", "D", ("departmentCode", "PH-KHONG")),         // mã đơn vị không có
            User(7, "NGUYEN.VAN.A", "Trùng"),                                     // trùng trong file
            User(8, "nguyen.van.e", "E", ("positions", "Giám đốc")),              // chức vụ không có
            User(9, "nguyen.van.f", "F", ("positions", "M11")),                   // mã thống kê ứng nhiều chức vụ
            User(10, "nguyen.van.g", "G", ("isPartyMember", "Có")),               // đảng viên thiếu số thẻ
            User(11, "da.co", "Đã có (sửa)"),                                     // cập nhật
            User(12, "da.xoa", "Đã xóa"),                                         // tài khoản đã xóa
            User(13, "co khoang trang", "H"),                                     // tên đăng nhập sai mẫu
            User(14, "nguyen.van.i", "I", ("partyCellCode", "CB-NGUNG")),         // tổ chức ngừng hoạt động
            User(15, "nguyen.van.k", "K", ("isPartyMember", "Có lẽ")),            // Có/Không sai
            User(16, "nguyen.van.l", "L", ("dateOfBirth", "01/01/2999")),         // ngày sinh ở tương lai
            User(17, "nguyen.van.m", "M", ("positions", "Chức vụ cũ"))            // chức vụ ngừng dùng
        });

        ImportRowResult R(int n) => analysis.Rows.Single(r => r.RowNumber == n);
        Assert.True(R(2).IsValid, string.Join("; ", R(2).Errors));
        Assert.Equal(ImportRowAction.Create, R(2).Action);
        var parsed = ((List<ParsedImportRow<UserImportRow>>)analysis.Payload!).Single(r => r.RowNumber == 2).Data;
        Assert.Equal("nguyen.van.a", parsed.Username);
        Assert.Equal("1980-08-15", parsed.DateOfBirth);
        Assert.Equal(deptId, parsed.DepartmentId);
        Assert.Equal(cellId, parsed.PartyCellId);
        Assert.Equal(new[] { "Trưởng phòng", "Bí thư chi bộ" }, parsed.Positions.Select(p => p.Name));

        Assert.Contains(R(3).Errors, e => e.Contains("Thiếu \"Tên đăng nhập\""));
        Assert.Contains(R(4).Errors, e => e.Contains("Thiếu \"Họ và tên\""));
        Assert.Contains(R(5).Errors, e => e.Contains("dd/MM/yyyy"));
        Assert.Contains(R(6).Errors, e => e.Contains("PH-KHONG"));
        Assert.Contains(R(7).Errors, e => e.Contains("trùng với dòng 2"));
        Assert.Contains(R(8).Errors, e => e.Contains("Giám đốc"));
        Assert.Contains(R(9).Errors, e => e.Contains("nhiều chức vụ"));
        Assert.Contains(R(10).Errors, e => e.Contains("số thẻ đảng"));
        Assert.Equal(ImportRowAction.Update, R(11).Action);
        Assert.True(R(11).IsValid);
        Assert.Contains(R(11).Notes, n => n.Contains("giữ nguyên mật khẩu"));
        Assert.Contains(R(12).Errors, e => e.Contains("đã xóa"));
        Assert.Contains(R(13).Errors, e => e.Contains("Tên đăng nhập"));
        Assert.Contains(R(14).Errors, e => e.Contains("ngừng hoạt động"));
        Assert.Contains(R(15).Errors, e => e.Contains("Có hoặc Không"));
        Assert.Contains(R(16).Errors, e => e.Contains("tương lai"));
        Assert.Contains(R(17).Errors, e => e.Contains("ngừng dùng"));
    }

    [Fact]
    public async Task Users_ActingPositionPrefix_ResolvesPositionAndMarksActing()
    {
        var (lookup, _, _) = UserLookup();
        var head = lookup.Positions.Single(p => p.Name == "Trưởng phòng");
        var secretary = lookup.Positions.Single(p => p.Name == "Bí thư chi bộ");
        var definition = new UserImportDefinition(lookup, new FakeGuard(), null!, null!, null!);
        var analysis = await definition.AnalyzeAsync(new[]
        {
            User(2, "nguyen.van.a", "A", ("positions", "Quyền Trưởng phòng; Bí thư chi bộ")),
            User(3, "nguyen.van.b", "B", ("positions", "q. bí thư chi bộ")),
            User(4, "nguyen.van.c", "C", ("positions", "Quyền Giám đốc"))
        });

        var parsed = ((List<ParsedImportRow<UserImportRow>>)analysis.Payload!).ToDictionary(r => r.RowNumber, r => r.Data);
        Assert.True(analysis.Rows[0].IsValid, string.Join("; ", analysis.Rows[0].Errors));
        Assert.Equal(new[] { head.Id, secretary.Id }, parsed[2].Positions.Select(p => p.Id));
        Assert.Equal(new[] { head.Id }, parsed[2].ActingPositionIds);
        Assert.Equal(new[] { secretary.Id }, parsed[3].ActingPositionIds);
        Assert.Contains(analysis.Rows.Single(r => r.RowNumber == 4).Errors, e => e.Contains("Giám đốc"));
    }

    [Fact]
    public async Task Users_FormLayoutColumn_NameOrByPosition()
    {
        var (lookup, _, _) = UserLookup();
        var leader = new FormLayoutRef(Guid.NewGuid(), "Lãnh đạo Công ty");
        lookup.FormLayouts.Add(leader);
        var definition = new UserImportDefinition(lookup, new FakeGuard(), null!, null!, null!);
        var analysis = await definition.AnalyzeAsync(new[]
        {
            User(2, "nguyen.van.a", "A", ("formLayout", "lãnh đạo công ty")),
            User(3, "da.co", "Đã có", ("formLayout", "theo chuc vu")),
            User(4, "nguyen.van.b", "B", ("formLayout", "Kiểu không có")),
            User(5, "nguyen.van.c", "C")
        });

        var parsed = ((List<ParsedImportRow<UserImportRow>>)analysis.Payload!).ToDictionary(r => r.RowNumber, r => r.Data);
        Assert.Equal((leader.Id, false), (parsed[2].FormLayoutId, parsed[2].ClearFormLayout));
        Assert.Equal(((Guid?)null, true), (parsed[3].FormLayoutId, parsed[3].ClearFormLayout));
        Assert.Contains(analysis.Rows.Single(r => r.RowNumber == 4).Errors, e => e.Contains("Kiểu không có"));
        Assert.Equal(((Guid?)null, false), (parsed[5].FormLayoutId, parsed[5].ClearFormLayout));
    }

    [Fact]
    public async Task Users_OutOfScope_IsRowError()
    {
        var (lookup, deptId, _) = UserLookup();
        var otherDept = Guid.NewGuid();
        lookup.Departments.Add(new OrgUnitRef(otherDept, "PH-KHAC", "Phòng khác", null, PhongType, true, false));
        // Chỉ quản lý được tài khoản của Phòng Kỹ thuật.
        var guard = new FakeGuard(target => target.DepartmentId == deptId);
        var definition = new UserImportDefinition(lookup, guard, null!, null!, null!);
        var analysis = await definition.AnalyzeAsync(new[]
        {
            User(2, "trong.pham.vi", "A", ("departmentCode", "PH-KT")),
            User(3, "ngoai.pham.vi", "B", ("departmentCode", "PH-KHAC")),
            User(4, "khong.don.vi", "C")
        });
        Assert.True(analysis.Rows[0].IsValid);
        Assert.Contains(analysis.Rows[1].Errors, e => e.Contains("không có quyền"));
        Assert.Contains(analysis.Rows[2].Errors, e => e.Contains("không có quyền"));
    }

    [Fact]
    public void Users_UpdateKeepsBlankFields_AndNotPartyMemberClearsCard()
    {
        var command = UserImportDefinition.BuildUpdate(new UserImportRow { Username = "a", FullName = "A", IsPartyMember = false });
        Assert.Equal("A", command.FullName);
        Assert.Null(command.Email);
        Assert.Null(command.PhoneNumber);
        Assert.Null(command.DepartmentId);
        Assert.Null(command.DateOfBirth);
        Assert.Equal(string.Empty, command.PartyCardNumber);

        var keep = UserImportDefinition.BuildUpdate(new UserImportRow { Username = "a", FullName = "A" });
        Assert.Null(keep.PartyCardNumber);
    }

    #endregion

    #region Gán vai trò

    private static (FakeLookup Lookup, Guid Me, Guid Target, Guid ReadRole, Guid AdminRole) AssignmentLookup()
    {
        var (lookup, deptId, _) = UserLookup();
        var me = Guid.NewGuid();
        var target = lookup.Users.Single(u => u.Username == "da.co").Id;
        lookup.Users.Add(new UserRef(me, "toi", "Tôi", false, null, null, Array.Empty<Guid>()));
        var readRole = Guid.NewGuid();
        var adminRole = Guid.NewGuid();
        lookup.Roles.Add(new RoleRef(readRole, "LANH_DAO_PHONG", "Lãnh đạo đơn vị", new[] { PermissionCodes.EvaluationRead }));
        lookup.Roles.Add(new RoleRef(adminRole, "QUAN_TRI", "Quản trị hệ thống", new[] { PermissionCodes.SystemRolesManage }));
        lookup.Assignments.Add(new AssignmentRef(target, readRole, RoleScopeType.Department, deptId, DateTime.UtcNow.AddDays(-10), null));
        return (lookup, me, target, readRole, adminRole);
    }

    private static ImportRawRow Assign(int number, string username, string role, string scope, string code = "", string from = "", string to = "")
        => Row(number, ("username", username), ("role", role), ("scopeType", scope), ("scopeCode", code), ("validFrom", from), ("validTo", to));

    [Fact]
    public async Task RoleAssignments_ParseAndValidate()
    {
        var (lookup, me, target, readRole, _) = AssignmentLookup();
        var definition = new RoleAssignmentImportDefinition(lookup, null!, new FakeCurrentUser(me));
        var analysis = await definition.AnalyzeAsync(new[]
        {
            Assign(2, "da.co", "Lãnh đạo đơn vị", "Tổ chức Đảng", "CB-KT", "01/10/2026", "31/12/2026"),
            Assign(3, "da.co", "LANH_DAO_PHONG", "to chuc dang", "CB-KT"),             // trùng dòng 2 (thời gian chồng lấn)
            Assign(4, "da.co", "Lãnh đạo đơn vị", "Đơn vị", "PH-KT"),                   // trùng bản gán đang hiệu lực
            Assign(5, "toi", "Lãnh đạo đơn vị", "Toàn công ty"),                        // tự gán cho mình
            Assign(6, "khong.co", "Lãnh đạo đơn vị", "Toàn công ty"),                   // người không có
            Assign(7, "da.co", "Vai trò lạ", "Toàn công ty"),                          // vai trò không có
            Assign(8, "da.co", "Quản trị hệ thống", "Đơn vị", "PH-KT"),                // quyền chỉ áp dụng toàn công ty
            Assign(9, "da.co", "Lãnh đạo đơn vị", "Đơn vị"),                            // thiếu mã phạm vi
            Assign(10, "da.co", "Lãnh đạo đơn vị", "Toàn công ty", "PH-KT"),            // toàn công ty kèm mã
            Assign(11, "da.co", "Lãnh đạo đơn vị", "Phòng ban"),                        // loại phạm vi sai
            Assign(12, "da.co", "Lãnh đạo đơn vị", "Toàn công ty", "", "2026-10-01"),   // ngày sai định dạng
            Assign(13, "da.co", "Lãnh đạo đơn vị", "Đơn vị", "PH-KHONG"),               // mã đơn vị không có
            Assign(14, "da.co", "Quản trị hệ thống", "Toàn công ty")                   // hợp lệ
        });

        ImportRowResult R(int n) => analysis.Rows.Single(r => r.RowNumber == n);
        Assert.True(R(2).IsValid, string.Join("; ", R(2).Errors));
        Assert.Equal(ImportRowAction.Create, R(2).Action);
        var parsed = ((List<ParsedImportRow<RoleAssignmentImportRow>>)analysis.Payload!).Single(r => r.RowNumber == 2).Data;
        Assert.Equal(target, parsed.UserId);
        Assert.Equal(readRole, parsed.RoleId);
        Assert.Equal(new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc), parsed.ValidFrom); // 00:00 giờ Việt Nam
        Assert.Equal(new DateTime(2026, 12, 31, 17, 0, 0, DateTimeKind.Utc), parsed.ValidTo);  // hết ngày 31/12

        Assert.Contains(R(3).Errors, e => e.Contains("dòng 2"));
        Assert.Contains(R(4).Errors, e => e.Contains("trùng thời gian"));
        Assert.Contains(R(5).Errors, e => e.Contains("chính mình"));
        Assert.Contains(R(6).Errors, e => e.Contains("khong.co"));
        Assert.Contains(R(7).Errors, e => e.Contains("Vai trò lạ"));
        Assert.Contains(R(8).Errors, e => e.Contains("chỉ gán được phạm vi Toàn công ty"));
        Assert.Contains(R(9).Errors, e => e.Contains("cần mã"));
        Assert.Contains(R(10).Errors, e => e.Contains("không kèm mã"));
        Assert.Contains(R(11).Errors, e => e.Contains("Loại phạm vi"));
        Assert.Contains(R(12).Errors, e => e.Contains("dd/MM/yyyy"));
        Assert.Contains(R(13).Errors, e => e.Contains("PH-KHONG"));
        Assert.True(R(14).IsValid, string.Join("; ", R(14).Errors));
    }

    #endregion

    #region Giá trị ô

    [Theory]
    [InlineData("05/09/2026", 2026, 9, 5)]
    [InlineData("5/9/2026", 2026, 9, 5)]
    public void ParseDate_AcceptsDayMonthYear(string text, int y, int m, int d)
    {
        Assert.Equal(new DateOnly(y, m, d), ImportValues.ParseDate(text, out var ok));
        Assert.True(ok);
    }

    [Theory]
    [InlineData("2026-09-05")]
    [InlineData("31/02/2026")]
    [InlineData("abc")]
    public void ParseDate_RejectsOtherFormats(string text)
    {
        Assert.Null(ImportValues.ParseDate(text, out var ok));
        Assert.False(ok);
    }

    [Fact]
    public void YesNo_And_List()
    {
        Assert.True(ImportValues.ParseYesNo("có", out _));
        Assert.True(ImportValues.ParseYesNo("Co", out _));
        Assert.False(ImportValues.ParseYesNo("KHÔNG", out _));
        Assert.Null(ImportValues.ParseYesNo("", out var blankOk));
        Assert.True(blankOk);
        ImportValues.ParseYesNo("có lẽ", out var badOk);
        Assert.False(badOk);
        Assert.Equal(new[] { "A", "B" }, ImportValues.SplitList(" A ; ;B; a "));
    }

    #endregion

    #region File Excel

    private static IReadOnlyList<ImportColumn> UserColumns() => new UserImportDefinition(new FakeLookup(), new FakeGuard(), null!, null!, null!).Columns;

    [Fact]
    public void Template_HasDataAndGuideSheets_AndReadsBackFilledRows()
    {
        var workbook = new ExcelImportWorkbook();
        var columns = UserColumns().Select(c => c.Key == "positions" ? c.WithAllowedValues(new[] { "Trưởng phòng" }) : c).ToList();
        var template = workbook.BuildTemplate("Cán bộ", "Mô tả", columns);

        using var filled = new MemoryStream();
        using (var xl = new XLWorkbook(new MemoryStream(template)))
        {
            Assert.NotNull(xl.Worksheet(ImportLimits.DataSheetName));
            Assert.NotNull(xl.Worksheet(ImportLimits.GuideSheetName));
            var data = xl.Worksheet(ImportLimits.DataSheetName);
            Assert.Equal("Tên đăng nhập *", data.Cell(1, 1).GetString());
            Assert.Contains(data.DataValidations, v => v.Ranges.Any(r => r.FirstColumn().ColumnNumber() == 8)); // Đảng viên: danh sách Có/Không

            data.Cell(2, 1).SetValue("nguyen.van.a");
            data.Cell(2, 2).SetValue("Nguyễn Văn A");
            data.Cell(2, 3).SetValue(new DateTime(1980, 8, 15)); // ô kiểu ngày (người dùng đổi định dạng ô sang ngày)
            data.Cell(2, 3).Style.DateFormat.Format = "dd/mm/yyyy";
            data.Cell(2, 5).SetValue(912345678);                 // ô kiểu số
            data.Cell(4, 1).SetValue("tran.b");                  // dòng 3 trống bị bỏ qua
            data.Cell(4, 2).SetValue("  Trần B  ");
            xl.SaveAs(filled);
        }

        filled.Position = 0;
        var rows = workbook.Read(filled, UserColumns());
        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows[0].RowNumber);
        Assert.Equal("15/08/1980", rows[0].Get("dateOfBirth"));
        Assert.Equal("912345678", rows[0].Get("phone"));
        Assert.Equal(4, rows[1].RowNumber);
        Assert.Equal("Trần B", rows[1].Get("fullName"));
    }

    [Fact]
    public void Read_MissingRequiredColumn_Throws()
    {
        using var stream = new MemoryStream();
        using (var xl = new XLWorkbook())
        {
            var sheet = xl.Worksheets.Add("Dữ liệu");
            sheet.Cell(1, 1).SetValue("Tên đăng nhập");
            sheet.Cell(2, 1).SetValue("a.b");
            xl.SaveAs(stream);
        }
        stream.Position = 0;
        var ex = Assert.Throws<ValidationException>(() => new ExcelImportWorkbook().Read(stream, UserColumns()));
        Assert.Contains("Họ và tên", ex.Message);
    }

    [Fact]
    public void Read_NotExcel_Throws()
    {
        using var stream = new MemoryStream("không phải excel"u8.ToArray());
        Assert.Throws<ValidationException>(() => new ExcelImportWorkbook().Read(stream, UserColumns()));
    }

    [Fact]
    public void Read_TooManyRows_Throws()
    {
        using var stream = new MemoryStream();
        using (var xl = new XLWorkbook())
        {
            var sheet = xl.Worksheets.Add("Dữ liệu");
            sheet.Cell(1, 1).SetValue("Tên đăng nhập");
            sheet.Cell(1, 2).SetValue("Họ và tên");
            for (var r = 2; r <= ImportLimits.MaxRows + 2; r++)
                sheet.Cell(r, 1).SetValue("u" + r);
            xl.SaveAs(stream);
        }
        stream.Position = 0;
        var ex = Assert.Throws<ValidationException>(() => new ExcelImportWorkbook().Read(stream, UserColumns()));
        Assert.Contains(ImportLimits.MaxRows.ToString(), ex.Message);
    }

    [Fact]
    public void ResultFile_PreventsFormulaInjection()
    {
        var bytes = new ExcelImportWorkbook().BuildTable("Tài khoản", new[] { "Tên", "Mật khẩu" },
            new[] { (IReadOnlyList<string>)new[] { "=HYPERLINK(\"x\")", "+1" }, new[] { "binh.thuong", "abc123" } });
        using var xl = new XLWorkbook(new MemoryStream(bytes));
        var sheet = xl.Worksheet("Tài khoản");
        Assert.False(sheet.Cell(2, 1).HasFormula);
        Assert.Equal("=HYPERLINK(\"x\")", sheet.Cell(2, 1).GetString());
        Assert.True(sheet.Cell(2, 1).Style.IncludeQuotePrefix);
        Assert.True(sheet.Cell(2, 2).Style.IncludeQuotePrefix);
        Assert.False(sheet.Cell(3, 1).Style.IncludeQuotePrefix);
    }

    #endregion

    #region Phiên nhập

    [Fact]
    public async Task Service_OnlyOwnerCommits_ResultDownloadsOnce_ErrorsBlockCommit()
    {
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var store = new MemoryImportSessionStore();
        var definition = new FakeDefinition();
        var currentUser = new FakeCurrentUser(owner);
        var service = new ImportService(new[] { definition }, new ExcelImportWorkbook(), store, new FakeAudit(),
            new FakeGuard(), currentUser, new FakeUnitOfWork());

        var file = BuildFile(("Tên", "a"), ("Tên", "b"));
        var preview = await service.PreviewAsync("fake", new MemoryStream(file), "du-lieu.xlsx", file.Length);
        Assert.Equal(2, preview.Summary.Create);

        // Người khác không xác nhận được phiên.
        currentUser.UserId = other;
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CommitAsync(preview.SessionId));

        currentUser.UserId = owner;
        var result = await service.CommitAsync(preview.SessionId);
        Assert.Equal(2, result.Created);
        Assert.Equal(2, definition.Committed);
        Assert.NotNull(result.ResultFileToken);

        // Phiên dùng một lần; file kết quả tải một lần, chỉ người tạo.
        await Assert.ThrowsAsync<NotFoundException>(() => service.CommitAsync(preview.SessionId));
        currentUser.UserId = other;
        Assert.Throws<NotFoundException>(() => service.DownloadResult(result.ResultFileToken!));
        currentUser.UserId = owner;
        Assert.NotEmpty(service.DownloadResult(result.ResultFileToken!).Content);
        Assert.Throws<NotFoundException>(() => service.DownloadResult(result.ResultFileToken!));

        // Có dòng lỗi → không xác nhận được, không ghi dòng nào.
        var bad = BuildFile(("Tên", "a"), ("Tên", "loi"));
        var badPreview = await service.PreviewAsync("fake", new MemoryStream(bad), "loi.xlsx", bad.Length);
        Assert.Equal(1, badPreview.Summary.Error);
        await Assert.ThrowsAsync<ConflictException>(() => service.CommitAsync(badPreview.SessionId));
        Assert.Equal(2, definition.Committed);

        await Assert.ThrowsAsync<ValidationException>(() => service.PreviewAsync("fake", new MemoryStream(file), "du-lieu.csv", file.Length));
        await Assert.ThrowsAsync<ValidationException>(() => service.PreviewAsync("fake", new MemoryStream(file), "lon.xlsx", ImportLimits.MaxFileBytes + 1));
        await Assert.ThrowsAsync<NotFoundException>(() => service.PreviewAsync("khong-co", new MemoryStream(file), "a.xlsx", file.Length));
    }

    [Fact]
    public async Task Service_KindsFollowPermissions()
    {
        var definition = new FakeDefinition();
        ImportService Service(params string[] codes) => new(new[] { definition }, new ExcelImportWorkbook(), new MemoryImportSessionStore(),
            new FakeAudit(), new FakeGuard(codes: codes), new FakeCurrentUser(Guid.NewGuid()), new FakeUnitOfWork());

        Assert.Empty(Service(PermissionCodes.CatalogManage).GetKinds());               // thiếu system.import
        Assert.Empty(Service(PermissionCodes.SystemImport).GetKinds());                // thiếu quyền dữ liệu
        Assert.Single(Service(PermissionCodes.SystemImport, PermissionCodes.CatalogManage).GetKinds());
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(PermissionCodes.SystemImport).GetTemplateAsync("fake"));
    }

    private static byte[] BuildFile(params (string Header, string Value)[] rows)
    {
        using var xl = new XLWorkbook();
        var sheet = xl.Worksheets.Add("Dữ liệu");
        sheet.Cell(1, 1).SetValue(rows[0].Header);
        for (var i = 0; i < rows.Length; i++)
            sheet.Cell(i + 2, 1).SetValue(rows[i].Value);
        using var stream = new MemoryStream();
        xl.SaveAs(stream);
        return stream.ToArray();
    }

    #endregion

    #region Giả lập

    private sealed class FakeLookup : IImportLookup
    {
        public List<OrgUnitRef> Departments { get; } = new();
        public List<OrgUnitRef> PartyCells { get; } = new();
        public List<UnitTypeRef> UnitTypes { get; } = new();
        public List<PositionRef> Positions { get; } = new();
        public List<RoleRef> Roles { get; } = new();
        public List<UserRef> Users { get; } = new();
        public List<AssignmentRef> Assignments { get; } = new();

        public Task<IReadOnlyList<OrgUnitRef>> GetDepartmentsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<OrgUnitRef>>(Departments);
        public Task<IReadOnlyList<OrgUnitRef>> GetPartyCellsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<OrgUnitRef>>(PartyCells);
        public Task<IReadOnlyList<UnitTypeRef>> GetUnitTypesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<UnitTypeRef>>(UnitTypes);
        public Task<IReadOnlyList<PositionRef>> GetPositionsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PositionRef>>(Positions);
        public Task<IReadOnlyList<RoleRef>> GetRolesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RoleRef>>(Roles);
        public List<FormLayoutRef> FormLayouts { get; } = new();
        public Task<IReadOnlyList<FormLayoutRef>> GetFormLayoutsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<FormLayoutRef>>(FormLayouts);

        public Task<IReadOnlyList<UserRef>> FindUsersAsync(IReadOnlyCollection<string> usernames, DateTime now, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<UserRef>>(Users.Where(u => usernames.Contains(u.Username, StringComparer.OrdinalIgnoreCase)).ToList());

        public Task<IReadOnlyList<AssignmentRef>> GetCurrentOrFutureAssignmentsAsync(IReadOnlyCollection<Guid> userIds, DateTime now, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AssignmentRef>>(Assignments.Where(a => userIds.Contains(a.UserId) && (a.ValidTo == null || a.ValidTo > now)).ToList());
    }

    /// <summary>Guard giả: có các mã quyền cho trước (mặc định: mọi quyền), <c>Can</c> theo điều kiện trên đối tượng.</summary>
    private sealed class FakeGuard : IAuthorizationGuard
    {
        private readonly Func<AccessTarget, bool> _can;
        private readonly HashSet<string>? _codes;

        public FakeGuard(Func<AccessTarget, bool>? can = null, string[]? codes = null)
        {
            _can = can ?? (_ => true);
            _codes = codes == null ? null : new HashSet<string>(codes);
        }

        public bool HasAny(string permission) => _codes == null || _codes.Contains(permission);
        public bool Can(string permission, AccessTarget target) => HasAny(permission) && _can(target);
        public void Ensure(string permission, AccessTarget target)
        {
            if (!Can(permission, target))
                throw new ForbiddenException(permission);
        }
        public ScopeFilter GetScope(string permission) => HasAny(permission) ? ScopeFilter.Global : ScopeFilter.None;
    }

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public FakeCurrentUser(Guid? userId) => UserId = userId;
        public Guid? UserId { get; set; }
        public string UserName => "test";
        public string? IpAddress => null;
        public string? UserAgent => null;
        public string? RequestPath => null;
    }

    private sealed class FakeAudit : IImportAuditWriter
    {
        public void AddSummary(string kind, string fileName, int rows, int created, int updated) { }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default) => operation();
        public void SetOriginalVersion(object entity, uint? version) { }
        public uint GetVersion(object entity) => 0;
    }

    /// <summary>Loại nhập giả: một cột "Tên"; giá trị "loi" là dòng lỗi; mỗi dòng ghi tạo một tài khoản kèm mật khẩu tạm.</summary>
    private sealed class FakeDefinition : ImportDefinition<string>
    {
        public int Committed { get; private set; }
        public override string Kind => "fake";
        public override string Name => "Giả";
        public override string Description => "Giả";
        public override int Order => 1;
        public override IReadOnlyList<string> RequiredPermissions { get; } = new[] { PermissionCodes.CatalogManage };
        public override IReadOnlyList<ImportColumn> Columns { get; } = new[] { new ImportColumn("name", "Tên", true, "Tên") };

        protected override string? ParseRow(ImportRawRow raw, ImportRowResult result)
        {
            if (raw.Get("name") == "loi")
                result.AddError("Dòng lỗi.");
            return raw.Get("name");
        }

        protected override Task ValidateAsync(IReadOnlyList<ParsedImportRow<string>> rows, CancellationToken ct) => Task.CompletedTask;

        protected override Task<ImportCommitOutcome> CommitRowsAsync(IReadOnlyList<ParsedImportRow<string>> rows, CancellationToken ct)
        {
            var outcome = new ImportCommitOutcome();
            foreach (var row in rows)
            {
                Committed++;
                outcome.Created++;
                outcome.Credentials.Add(new ImportCredential(row.Data, row.Data, "Matkhau123"));
            }
            return Task.FromResult(outcome);
        }
    }

    #endregion
}
