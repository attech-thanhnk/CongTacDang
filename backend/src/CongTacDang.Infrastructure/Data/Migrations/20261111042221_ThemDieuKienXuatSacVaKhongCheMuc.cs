using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ThemDieuKienXuatSacVaKhongCheMuc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppraisalCapReason",
                table: "evaluation_records",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppraisalConditionOverrides",
                table: "evaluation_records",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AppraisalGradeCap",
                table: "evaluation_records",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SelfDeclaresNoCap",
                table: "evaluation_records",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppraisalCapReason",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "AppraisalConditionOverrides",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "AppraisalGradeCap",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "SelfDeclaresNoCap",
                table: "evaluation_records");
        }
    }
}
