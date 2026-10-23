using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPositionIsHead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHead",
                table: "positions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // CSDL đang chạy: đánh dấu sẵn các chức vụ cấp trưởng của danh mục mặc định (khớp DataSeeder).
            migrationBuilder.Sql("""
                UPDATE positions SET "IsHead" = TRUE
                WHERE NOT "IsDeleted" AND "Name" IN (
                    'Bí thư Đảng ủy Tổng công ty', 'Bí thư Đảng ủy',
                    'Bí thư Chi bộ trực thuộc Đảng ủy bộ phận Văn phòng Tổng công ty',
                    'Chủ tịch Công ty', 'Giám đốc', 'Bí thư Đảng bộ bộ phận', 'Bí thư Chi bộ',
                    'Bí thư Chi bộ trực thuộc Đảng bộ bộ phận', 'Trưởng phòng', 'Trưởng trung tâm', 'Quản đốc',
                    'Giám đốc đơn vị trực thuộc', 'Chánh Văn phòng', 'Đội trưởng', 'Đài trưởng', 'Trạm trưởng');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsHead",
                table: "positions");
        }
    }
}
