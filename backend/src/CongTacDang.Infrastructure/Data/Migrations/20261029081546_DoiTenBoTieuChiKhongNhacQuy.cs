using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DoiTenBoTieuChiKhongNhacQuy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tên cũ do phiên bản seed trước để lại, nhắc một quý cụ thể.
            migrationBuilder.Sql("UPDATE criteria_sets SET \"Name\" = 'Bộ tiêu chí đánh giá cán bộ' WHERE \"Name\" LIKE 'Từ Quý%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Không khôi phục tên cũ.
        }
    }
}
