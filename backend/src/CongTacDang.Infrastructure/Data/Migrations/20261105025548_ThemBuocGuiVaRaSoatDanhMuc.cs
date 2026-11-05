using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ThemBuocGuiVaRaSoatDanhMuc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TasksForwardComment",
                table: "evaluation_records",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TasksForwardedAt",
                table: "evaluation_records",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TasksForwardedById",
                table: "evaluation_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TasksForwardedByName",
                table: "evaluation_records",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TasksReviewComment",
                table: "evaluation_records",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TasksReviewedAt",
                table: "evaluation_records",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TasksReviewedById",
                table: "evaluation_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TasksReviewedByName",
                table: "evaluation_records",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TasksForwardComment",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "TasksForwardedAt",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "TasksForwardedById",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "TasksForwardedByName",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "TasksReviewComment",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "TasksReviewedAt",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "TasksReviewedById",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "TasksReviewedByName",
                table: "evaluation_records");
        }
    }
}
