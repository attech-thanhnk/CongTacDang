using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class GradeProposalExplanations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DecisionExplanation",
                table: "evaluation_records",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelfGradeExplanation",
                table: "evaluation_records",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DecisionExplanation",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "SelfGradeExplanation",
                table: "evaluation_records");
        }
    }
}
