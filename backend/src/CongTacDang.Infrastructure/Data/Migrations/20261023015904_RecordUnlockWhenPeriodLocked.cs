using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RecordUnlockWhenPeriodLocked : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnlockReason",
                table: "evaluation_records",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UnlockedAt",
                table: "evaluation_records",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UnlockedById",
                table: "evaluation_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnlockedByName",
                table: "evaluation_records",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UnlockedUntil",
                table: "evaluation_records",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnlockReason",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "UnlockedAt",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "UnlockedById",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "UnlockedByName",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "UnlockedUntil",
                table: "evaluation_records");
        }
    }
}
