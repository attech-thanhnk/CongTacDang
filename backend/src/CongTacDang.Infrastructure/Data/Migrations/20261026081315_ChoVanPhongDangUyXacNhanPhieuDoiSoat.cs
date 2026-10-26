using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChoVanPhongDangUyXacNhanPhieuDoiSoat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Quyền xác nhận phiếu đối soát đã có trong CSDL nên seeder không tự thêm; cấp một lần cho Văn phòng Đảng ủy, chạy lại không nhân đôi.
            migrationBuilder.Sql("""
                INSERT INTO role_permissions (role_id, permission_id)
                SELECT r."Id", p."Id"
                FROM roles r
                CROSS JOIN permissions p
                WHERE r."Code" = 'VAN_PHONG_DANG_UY' AND r."IsDeleted" = FALSE
                  AND p."Code" = 'evaluation.crosscheck.confirm'
                  AND NOT EXISTS (
                      SELECT 1 FROM role_permissions rp WHERE rp.role_id = r."Id" AND rp.permission_id = p."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
