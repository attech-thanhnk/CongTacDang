using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskRegistrationColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Confirmer",
                table: "evaluation_tasks",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceSource",
                table: "evaluation_tasks",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExceedCriteria",
                table: "evaluation_tasks",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "evaluation_tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductCode",
                table: "evaluation_tasks",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "evaluation_tasks",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Confirmer",
                table: "evaluation_tasks");

            migrationBuilder.DropColumn(
                name: "EvidenceSource",
                table: "evaluation_tasks");

            migrationBuilder.DropColumn(
                name: "ExceedCriteria",
                table: "evaluation_tasks");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "evaluation_tasks");

            migrationBuilder.DropColumn(
                name: "ProductCode",
                table: "evaluation_tasks");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "evaluation_tasks");
        }
    }
}
