using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ThemTepDinhKemDieuChinh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AttachmentId",
                table: "task_change_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_change_requests_AttachmentId",
                table: "task_change_requests",
                column: "AttachmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_task_change_requests_task_attachments_AttachmentId",
                table: "task_change_requests",
                column: "AttachmentId",
                principalTable: "task_attachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_task_change_requests_task_attachments_AttachmentId",
                table: "task_change_requests");

            migrationBuilder.DropIndex(
                name: "IX_task_change_requests_AttachmentId",
                table: "task_change_requests");

            migrationBuilder.DropColumn(
                name: "AttachmentId",
                table: "task_change_requests");
        }
    }
}
