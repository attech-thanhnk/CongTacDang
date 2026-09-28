using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuthHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Token cũ lưu nguyên văn, không chuyển sang hash được: thu hồi toàn bộ, người dùng đăng nhập lại một lần.
            migrationBuilder.Sql("DELETE FROM refresh_tokens;");

            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_Token",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "ReplacedByToken",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "Token",
                table: "refresh_tokens");

            migrationBuilder.AddColumn<string>(
                name: "ReplacedByTokenHash",
                table: "refresh_tokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAt",
                table: "refresh_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TokenHash",
                table: "refresh_tokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "FailedLoginCount",
                table: "party_member_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEnd",
                table: "party_member_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "party_member_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_TokenHash",
                table: "refresh_tokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM refresh_tokens;");

            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_TokenHash",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "ReplacedByTokenHash",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "TokenHash",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "FailedLoginCount",
                table: "party_member_profiles");

            migrationBuilder.DropColumn(
                name: "LockoutEnd",
                table: "party_member_profiles");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "party_member_profiles");

            migrationBuilder.AddColumn<string>(
                name: "ReplacedByToken",
                table: "refresh_tokens",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Token",
                table: "refresh_tokens",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_Token",
                table: "refresh_tokens",
                column: "Token",
                unique: true);
        }
    }
}
