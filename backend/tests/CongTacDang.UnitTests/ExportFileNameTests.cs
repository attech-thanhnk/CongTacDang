using CongTacDang.Application.Common;
using CongTacDang.Domain.Entities;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Tên tệp xuất không dấu theo quy cách "Mau bao cao_Ten viet tat_Ky bao cao".</summary>
public class ExportFileNameTests
{
    [Fact]
    public void Build_MatchesGuidanceExample() =>
        Assert.Equal("Mau 16_QLBMB_Quy III-2026.docx", ExportFileName.Build("Mau 16", "QLBMB", null, 3, 2026, ".docx"));

    [Theory]
    [InlineData("Đảng ủy Nguyễn Đức", "Dang uy Nguyen Duc")]
    [InlineData("ĐỖ VĂN ƯỚC", "DO VAN UOC")]
    [InlineData("Chi bộ A/B: \"Kỹ thuật\" <1>", "Chi bo A B Ky thuat 1")]
    [InlineData("  a   b  ", "a b")]
    [InlineData("a\\b|c?d*e", "a b c d e")]
    [InlineData("😀 ☒", "")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void Ascii_RemovesDiacriticsAndForbiddenCharacters(string? input, string expected) =>
        Assert.Equal(expected, ExportFileName.Ascii(input));

    [Fact]
    public void Build_UnitAbbreviationHasNoSpaces_AndBlankUnitFallsBack()
    {
        Assert.Equal("Mau 12_CBKyThuat_Quy IV-2027.docx", ExportFileName.Build("Mau 12", "CB Kỹ Thuật", null, 4, 2027, ".docx"));
        Assert.Equal("Mau 12_DonVi_Quy I-2027.docx", ExportFileName.Build("Mau 12", " ?? ", null, 1, 2027, ".docx"));
    }

    [Fact]
    public void Build_PersonalForm_AddsNameBetweenUnitAndPeriod() =>
        Assert.Equal("Mau 09A_ATTECH_Nguyen Van Dung_Quy III-2026.docx",
            ExportFileName.Build("Mau 09A", "ATTECH", "Nguyễn Văn Dũng", 3, 2026, ".docx"));

    [Fact]
    public void Build_WithoutPeriod_OmitsPeriodSegment() =>
        Assert.Equal("Danh sach can bo_ATTECH.xlsx", ExportFileName.Build("Danh sach can bo", "ATTECH", null, null, null, ".xlsx"));

    [Fact]
    public void Build_VeryLongSegments_AreCappedAndKeepFormAndPeriod()
    {
        var name = ExportFileName.Build("Mau 09A", new string('Đ', 300), new string('ư', 300) + " ơ", 3, 2026, ".docx");

        Assert.EndsWith("_Quy III-2026.docx", name);
        Assert.StartsWith("Mau 09A_", name);
        Assert.True(name.Length <= ExportFileName.MaxBaseLength + ".docx".Length, name.Length.ToString());
        Assert.DoesNotContain(name, c => c > 127);
    }

    [Theory]
    [InlineData(1, "Quy I-2026")]
    [InlineData(2, "Quy II-2026")]
    [InlineData(4, "Quy IV-2026")]
    public void Period_UsesRomanQuarter(int quarter, string expected) => Assert.Equal(expected, ExportFileName.Period(quarter, 2026));

    [Theory]
    [InlineData("KT", "PH-KT", "ATTECH", "KT")]
    [InlineData(null, "PH-KT", "ATTECH", "PH-KT")]
    [InlineData("  ", "PH-KT", "ATTECH", "PH-KT")]
    [InlineData(null, "", "ATTECH", "ATTECH")]
    public void Unit_PrefersUnitShortName_ThenCode_ThenOrganizationShortName(string? unitShort, string? code, string? org, string expected) =>
        Assert.Equal(expected, ExportFileName.Unit(unitShort, code, org));

    [Fact]
    public void Unit_NothingAvailable_IsNull() => Assert.Null(ExportFileName.Unit(null, null, " "));

    [Fact]
    public void Unit_FromOrgUnit_UsesShortNameThenCode()
    {
        var withShort = new PartyCell { Code = "CB-KT", ShortName = "CBKT" };
        var withoutShort = new AdministrativeDepartment { Code = "PH-KT" };

        Assert.Equal("CBKT", ExportFileName.Unit(withShort, "ATTECH"));
        Assert.Equal("PH-KT", ExportFileName.Unit(withoutShort, "ATTECH"));
        Assert.Equal("ATTECH", ExportFileName.Unit(null, "ATTECH"));
        Assert.Equal("Mau 16_CBKT_Quy III-2026.docx", ExportFileName.Build("Mau 16", ExportFileName.Unit(withShort, "ATTECH"), null, 3, 2026, ".docx"));
    }

    [Fact]
    public void PersonalUnit_UsesShortNameOnly_NeverCode()
    {
        var cell = new PartyCell { Code = "CB-KT", ShortName = "CBKT" };
        var cellNoShort = new PartyCell { Code = "CB-KT" };
        var department = new AdministrativeDepartment { Code = "PH-KT", ShortName = "KT" };

        Assert.Equal("CBKT", ExportFileName.PersonalUnit(cell, department, "ATTECH"));
        Assert.Equal("KT", ExportFileName.PersonalUnit(cellNoShort, department, "ATTECH"));
        Assert.Equal("ATTECH", ExportFileName.PersonalUnit(cellNoShort, new AdministrativeDepartment { Code = "PH-KT" }, "ATTECH"));
        Assert.Equal("ATTECH", ExportFileName.PersonalUnit(null, null, "ATTECH"));
    }

    [Fact]
    public void Period_MissingPart_IsNull()
    {
        Assert.Null(ExportFileName.Period(null, 2026));
        Assert.Null(ExportFileName.Period(3, null));
    }
}
