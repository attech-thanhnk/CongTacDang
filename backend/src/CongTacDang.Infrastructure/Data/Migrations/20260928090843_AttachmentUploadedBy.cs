using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AttachmentUploadedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UploadedById",
                table: "task_attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_attachments_UploadedById",
                table: "task_attachments",
                column: "UploadedById");

            migrationBuilder.AddForeignKey(
                name: "FK_task_attachments_party_member_profiles_UploadedById",
                table: "task_attachments",
                column: "UploadedById",
                principalTable: "party_member_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_task_attachments_party_member_profiles_UploadedById",
                table: "task_attachments");

            migrationBuilder.DropIndex(
                name: "IX_task_attachments_UploadedById",
                table: "task_attachments");

            migrationBuilder.DropColumn(
                name: "UploadedById",
                table: "task_attachments");
        }
    }
}
