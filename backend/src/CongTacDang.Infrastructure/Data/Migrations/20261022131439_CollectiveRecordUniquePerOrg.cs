using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CollectiveRecordUniquePerOrg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Chỉ mục cũ coi NULL là khác nhau nên có thể đã có hồ sơ trùng (Mẫu 07 luôn DepartmentId NULL). Trước khi tạo chỉ mục
            // mới: trong mỗi nhóm trùng (chưa xóa) giữ hồ sơ sửa gần nhất, các hồ sơ còn lại được XÓA MỀM (không xóa cứng — dữ liệu,
            // dòng nội dung và tệp đính kèm vẫn còn trong CSDL để quản trị khôi phục/đối chiếu nếu cần).
            migrationBuilder.Sql("""
                UPDATE collective_evaluation_records AS r
                SET "IsDeleted" = TRUE, "DeletedAt" = now()
                FROM (
                    SELECT "Id", row_number() OVER (
                        PARTITION BY "PeriodId", "Form", "PartyCellId", "DepartmentId"
                        ORDER BY COALESCE("UpdatedAt", "CreatedAt") DESC, "CreatedAt" DESC, "Id") AS rn
                    FROM collective_evaluation_records
                    WHERE "IsDeleted" = FALSE
                ) AS d
                WHERE r."Id" = d."Id" AND d.rn > 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_collective_evaluation_records_PeriodId_Form_PartyCellId_Dep~",
                table: "collective_evaluation_records");

            migrationBuilder.CreateIndex(
                name: "IX_collective_evaluation_records_PeriodId_Form_PartyCellId_Dep~",
                table: "collective_evaluation_records",
                columns: new[] { "PeriodId", "Form", "PartyCellId", "DepartmentId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE")
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_collective_evaluation_records_PeriodId_Form_PartyCellId_Dep~",
                table: "collective_evaluation_records");

            migrationBuilder.CreateIndex(
                name: "IX_collective_evaluation_records_PeriodId_Form_PartyCellId_Dep~",
                table: "collective_evaluation_records",
                columns: new[] { "PeriodId", "Form", "PartyCellId", "DepartmentId" },
                unique: true);
        }
    }
}
