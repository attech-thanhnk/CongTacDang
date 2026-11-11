using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Documents;
using CongTacDang.Infrastructure.Documents.Forms;
using CongTacDang.Infrastructure.Services;
using DocumentFormat.OpenXml.Packaging;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Dòng 1 tiêu đề trái Mẫu 01, 02 là cơ quan chủ quản trực tiếp của đơn vị hồ sơ; đơn vị trực thuộc Công ty in tên công ty.</summary>
public sealed class OrgHeaderLineTests
{
    private static readonly OrganizationTemplateFields Org =
        OrganizationTemplateFields.From("Đảng bộ A", null, "Công ty TNHH Kỹ thuật Quản lý bay", null, "A", "Hà Nội");

    private static readonly OrganizationTemplateFields OrgWithGroup =
        OrganizationTemplateFields.From("Đảng bộ A", null, "Công ty TNHH Kỹ thuật Quản lý bay", "Tổng công ty Quản lý bay Việt Nam", "A", "Hà Nội");

    [Fact]
    public void DirectParent_NonRootParent_UpperOneLine_RootParent_Null()
    {
        Assert.Equal("TRUNG TÂM BẢO ĐẢM KỸ THUẬT", OrgHeaderLine.DirectParent("Trung tâm Bảo đảm kỹ thuật", parentIsRoot: false));
        Assert.Null(OrgHeaderLine.DirectParent("Công ty TNHH Kỹ thuật Quản lý bay", parentIsRoot: true));
        Assert.Null(OrgHeaderLine.DirectParent(null, parentIsRoot: true));
    }

    private static EvaluationRecord Record()
    {
        var record = DocumentTemplateTests.SampleData().Records[0];
        record.Department = new AdministrativeDepartment { Name = "Đội CNTT", ParentId = Guid.NewGuid() };
        return record;
    }

    private static string Header(string file, object data, OrganizationTemplateFields? org = null)
    {
        var result = DocxTemplateEngine.Render(new FileWordTemplateStore().Load(file), TemplateDataBinder.Bind(data).WithShared(TemplateDataBinder.Bind(org ?? Org)));
        Assert.Empty(result.MissingTags);
        using var doc = WordprocessingDocument.Open(new MemoryStream(result.Content), false);
        // Ô đầu bảng tiêu đề: dòng 1 + dòng 2 bên trái.
        return doc.MainDocumentPart!.Document!.Body!.Descendants<DocumentFormat.OpenXml.Wordprocessing.TableCell>().First().InnerText;
    }

    public static IEnumerable<object[]> Files() => new[] { new object[] { Mau01Data.TemplateFileName }, new object[] { Mau02Data.TemplateFileName } };

    private static object Data(string file, EvaluationRecord record, string? parentLine) => file == Mau01Data.TemplateFileName
        ? Mau01Data.From(record, parentLine)
        : Mau02Data.From(record, new Dictionary<Guid, string>(), parentLine);

    [Theory]
    [MemberData(nameof(Files))]
    public void Word_UnitUnderCenter_PrintsCenterThenUnit(string file)
    {
        var header = Header(file, Data(file, Record(), OrgHeaderLine.DirectParent("Trung tâm Bảo đảm kỹ thuật", parentIsRoot: false)));
        Assert.StartsWith("TRUNG TÂM BẢO ĐẢM KỸ THUẬT", header);
        Assert.Contains("Đội CNTT", header);
        Assert.DoesNotContain("CÔNG TY TNHH", header);
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Word_UnitUnderCompany_KeepsCompanyName(string file)
    {
        var header = Header(file, Data(file, Record(), null));
        Assert.StartsWith("CÔNG TY TNHH KỸ THUẬT QUẢN LÝ BAY", header);
        Assert.Contains("Đội CNTT", header);
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Word_CompanyLeader_PrintsGroupThenCompany_OrCompanyOnly(string file)
    {
        var record = Record();
        record.Department = new AdministrativeDepartment { Name = "Công ty TNHH Kỹ thuật Quản lý bay" };

        var header = Header(file, Data(file, record, null), OrgWithGroup);
        Assert.Equal("TỔNG CÔNG TY QUẢN LÝ BAY VIỆT NAMCÔNG TY TNHH KỸ THUẬT QUẢN LÝ BAY", header);
        Assert.Equal("CÔNG TY TNHH KỸ THUẬT QUẢN LÝ BAY", Header(file, Data(file, record, null)));
        // Đơn vị có phòng: không in dòng tổng công ty dù cài đặt có.
        Assert.StartsWith("CÔNG TY TNHH KỸ THUẬT QUẢN LÝ BAYĐội CNTT", Header(file, Data(file, Record(), null), OrgWithGroup));
    }

    [Fact]
    public void Excel_CompanyLeader_PrintsGroupThenCompany()
    {
        var record = Record();
        record.Department = new AdministrativeDepartment { Name = "Công ty TNHH Kỹ thuật Quản lý bay" };
        using var wb = new XLWorkbook(new MemoryStream(PersonalFormWorkbooks.Mau01(Mau01Data.From(record), OrgWithGroup)));
        Assert.Equal("TỔNG CÔNG TY QUẢN LÝ BAY VIỆT NAM", wb.Worksheet(1).Cell(2, 1).GetString());
        Assert.Equal("CÔNG TY TNHH KỸ THUẬT QUẢN LÝ BAY", wb.Worksheet(1).Cell(3, 1).GetString());
        Assert.True(wb.Worksheet(1).Cell(3, 1).Style.Font.Bold);
    }

    [Fact]
    public void Excel_UsesSameLineAsWord()
    {
        var parent = OrgHeaderLine.DirectParent("Trung tâm Bảo đảm kỹ thuật", parentIsRoot: false);
        foreach (var bytes in new[]
                 {
                     PersonalFormWorkbooks.Mau01(Mau01Data.From(Record(), parent), Org),
                     PersonalFormWorkbooks.Mau02(Mau02Data.From(Record(), null, parent), Org)
                 })
        {
            using var wb = new XLWorkbook(new MemoryStream(bytes));
            Assert.Equal("TRUNG TÂM BẢO ĐẢM KỸ THUẬT", wb.Worksheet(1).Cell(2, 1).GetString());
        }

        using var company = new XLWorkbook(new MemoryStream(PersonalFormWorkbooks.Mau01(Mau01Data.From(Record()), Org)));
        Assert.Equal("CÔNG TY TNHH KỸ THUẬT QUẢN LÝ BAY", company.Worksheet(1).Cell(2, 1).GetString());
    }
}
