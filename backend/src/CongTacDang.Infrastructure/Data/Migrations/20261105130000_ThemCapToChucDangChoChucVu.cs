using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ThemCapToChucDangChoChucVu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PartyUnitTypeId",
                table: "positions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_positions_PartyUnitTypeId",
                table: "positions",
                column: "PartyUnitTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_positions_org_unit_types_PartyUnitTypeId",
                table: "positions",
                column: "PartyUnitTypeId",
                principalTable: "org_unit_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Điền cấp cho chức vụ Đảng có sẵn theo mã thống kê/tên; loại tổ chức chưa có (CSDL mới) thì câu lệnh không làm gì.
            // Cấp Tổng công ty (M1–M7, M10–M12) nằm ngoài cây nên để trống.
            migrationBuilder.Sql("""
                UPDATE positions p
                SET "PartyUnitTypeId" = t."Id"
                FROM org_unit_types t
                WHERE t."Side" = 1 AND t."IsDeleted" = FALSE
                  AND p."Side" = 1 AND p."IsDeleted" = FALSE AND p."PartyUnitTypeId" IS NULL
                  AND (
                      (t."Name" = 'Đảng ủy' AND p."StatCode" IN ('M8', 'M9', 'M17', 'M18', 'M19'))
                   OR (t."Name" = 'Đảng bộ bộ phận' AND (p."StatCode" IN ('M20', 'M21') OR p."Name" = 'Ủy viên Ban Chấp hành Đảng bộ bộ phận'))
                   OR (t."Name" = 'Chi bộ' AND (p."StatCode" IN ('M22', 'M23', 'M24', 'M25') OR p."Name" IN ('Chi ủy viên', 'Đảng viên')))
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_positions_org_unit_types_PartyUnitTypeId",
                table: "positions");

            migrationBuilder.DropIndex(
                name: "IX_positions_PartyUnitTypeId",
                table: "positions");

            migrationBuilder.DropColumn(
                name: "PartyUnitTypeId",
                table: "positions");
        }
    }
}
