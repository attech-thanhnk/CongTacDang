using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ThemTenVietTatVaVanBanHuongDan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "party_cells",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "GuideDocumentDate",
                table: "organization_settings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuideDocumentNumber",
                table: "organization_settings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "administrative_departments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            // Giá trị đang in sẵn trên biểu mẫu, chỉ điền khi cả hai cột còn trống; không ghi đè giá trị đã nhập.
            migrationBuilder.Sql("""
                UPDATE organization_settings
                SET "GuideDocumentNumber" = '03-HD/TVĐU', "GuideDocumentDate" = DATE '2026-09-10'
                WHERE "GuideDocumentNumber" IS NULL AND "GuideDocumentDate" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "party_cells");

            migrationBuilder.DropColumn(
                name: "GuideDocumentDate",
                table: "organization_settings");

            migrationBuilder.DropColumn(
                name: "GuideDocumentNumber",
                table: "organization_settings");

            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "administrative_departments");
        }
    }
}
