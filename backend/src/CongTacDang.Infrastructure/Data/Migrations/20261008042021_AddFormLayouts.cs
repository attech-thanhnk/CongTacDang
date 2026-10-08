using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFormLayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FormLayoutId",
                table: "positions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FormLayoutOverrideId",
                table: "party_member_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "form_layouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HeaderStyle = table.Column<int>(type: "integer", nullable: false),
                    ShowCellConfirmation = table.Column<bool>(type: "boolean", nullable: false),
                    SingleLinePositions = table.Column<bool>(type: "boolean", nullable: false),
                    ShowDateLineOn9D = table.Column<bool>(type: "boolean", nullable: false),
                    EmptyPositionText = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_form_layouts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_positions_FormLayoutId",
                table: "positions",
                column: "FormLayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_party_member_profiles_FormLayoutOverrideId",
                table: "party_member_profiles",
                column: "FormLayoutOverrideId");

            migrationBuilder.CreateIndex(
                name: "IX_form_layouts_Name",
                table: "form_layouts",
                column: "Name",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.AddForeignKey(
                name: "FK_party_member_profiles_form_layouts_FormLayoutOverrideId",
                table: "party_member_profiles",
                column: "FormLayoutOverrideId",
                principalTable: "form_layouts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_positions_form_layouts_FormLayoutId",
                table: "positions",
                column: "FormLayoutId",
                principalTable: "form_layouts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_party_member_profiles_form_layouts_FormLayoutOverrideId",
                table: "party_member_profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_positions_form_layouts_FormLayoutId",
                table: "positions");

            migrationBuilder.DropTable(
                name: "form_layouts");

            migrationBuilder.DropIndex(
                name: "IX_positions_FormLayoutId",
                table: "positions");

            migrationBuilder.DropIndex(
                name: "IX_party_member_profiles_FormLayoutOverrideId",
                table: "party_member_profiles");

            migrationBuilder.DropColumn(
                name: "FormLayoutId",
                table: "positions");

            migrationBuilder.DropColumn(
                name: "FormLayoutOverrideId",
                table: "party_member_profiles");
        }
    }
}
