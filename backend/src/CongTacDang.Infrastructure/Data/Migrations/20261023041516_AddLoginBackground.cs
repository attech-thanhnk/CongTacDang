using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginBackground : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LoginBackgroundContentType",
                table: "organization_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoginBackgroundFileName",
                table: "organization_settings",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoginBackgroundKey",
                table: "organization_settings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LoginBackgroundUpdatedAt",
                table: "organization_settings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoginPanelPosition",
                table: "organization_settings",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "center");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LoginBackgroundContentType",
                table: "organization_settings");

            migrationBuilder.DropColumn(
                name: "LoginBackgroundFileName",
                table: "organization_settings");

            migrationBuilder.DropColumn(
                name: "LoginBackgroundKey",
                table: "organization_settings");

            migrationBuilder.DropColumn(
                name: "LoginBackgroundUpdatedAt",
                table: "organization_settings");

            migrationBuilder.DropColumn(
                name: "LoginPanelPosition",
                table: "organization_settings");
        }
    }
}
