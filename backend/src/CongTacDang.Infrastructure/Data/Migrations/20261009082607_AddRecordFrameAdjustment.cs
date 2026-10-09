using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordFrameAdjustment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "FrameA",
                table: "evaluation_records",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FrameB",
                table: "evaluation_records",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FrameC",
                table: "evaluation_records",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FrameD",
                table: "evaluation_records",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FrameA",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "FrameB",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "FrameC",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "FrameD",
                table: "evaluation_records");
        }
    }
}
