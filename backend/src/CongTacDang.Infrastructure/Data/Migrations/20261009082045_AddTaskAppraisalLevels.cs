using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskAppraisalLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppraisalComment",
                table: "evaluation_tasks",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AppraisalLevel",
                table: "evaluation_tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AppraisalGeneralScore",
                table: "evaluation_records",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AppraisalTasksScore",
                table: "evaluation_records",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppraisalComment",
                table: "evaluation_tasks");

            migrationBuilder.DropColumn(
                name: "AppraisalLevel",
                table: "evaluation_tasks");

            migrationBuilder.DropColumn(
                name: "AppraisalGeneralScore",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "AppraisalTasksScore",
                table: "evaluation_records");
        }
    }
}
