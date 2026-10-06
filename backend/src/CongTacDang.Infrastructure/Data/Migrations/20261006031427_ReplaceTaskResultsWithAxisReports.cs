using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceTaskResultsWithAxisReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TaskResults",
                table: "evaluation_records",
                newName: "AxisReports");

            // Mẫu 9D chuyển từ danh sách dòng nhiệm vụ sang nội dung theo trục (nhiệm vụ cho Mẫu 09C, tóm tắt trục cho Mẫu 9D):
            // dữ liệu dạng cũ không chuyển đổi.
            migrationBuilder.Sql("UPDATE evaluation_records SET \"AxisReports\" = '{}'::jsonb;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE evaluation_records SET \"AxisReports\" = '[]'::jsonb;");

            migrationBuilder.RenameColumn(
                name: "AxisReports",
                table: "evaluation_records",
                newName: "TaskResults");
        }
    }
}
