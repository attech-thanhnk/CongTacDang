using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EvaluationRecordNotRated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NotRatedAt",
                table: "evaluation_records",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NotRatedById",
                table: "evaluation_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotRatedByName",
                table: "evaluation_records",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NotRatedFromStatus",
                table: "evaluation_records",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotRatedReason",
                table: "evaluation_records",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotRatedAt",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "NotRatedById",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "NotRatedByName",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "NotRatedFromStatus",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "NotRatedReason",
                table: "evaluation_records");
        }
    }
}
