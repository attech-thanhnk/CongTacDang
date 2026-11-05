using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Kế hoạch 30-60-90 ngày: người quản lý trực tiếp lập, thủ trưởng đơn vị duyệt. Vai trò Cấp trực tiếp sử dụng bỏ quyền lập;
    /// quyền duyệt mới do seeder thêm vào vai trò mặc định khi tạo mã quyền.
    /// </summary>
    [DbContext(typeof(CongTacDangDbContext))]
    [Migration("20261105120000_CapTrucTiepChiDuyetKeHoach")]
    public partial class CapTrucTiepChiDuyetKeHoach : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bảng quyền có thể chưa có dòng nào (CSDL mới, seeder chạy sau): câu lệnh không làm gì.
            migrationBuilder.Sql("""
                DELETE FROM role_permissions rp
                USING roles r, permissions p
                WHERE rp.role_id = r."Id" AND rp.permission_id = p."Id"
                  AND r."Code" = 'CAP_TRUC_TIEP_SU_DUNG'
                  AND p."Code" = 'evaluation.improvement.manage';
                """);

            // Chỉ đổi mô tả còn đúng bản mặc định cũ, không ghi đè mô tả quản trị đã sửa.
            migrationBuilder.Sql("""
                UPDATE roles
                SET "Description" = 'Giám đốc, Chủ tịch: duyệt danh mục sản phẩm, nhận xét, đề xuất mức; duyệt kế hoạch 30-60-90 ngày (Mẫu 17) do người quản lý trực tiếp lập.'
                WHERE "Code" = 'CAP_TRUC_TIEP_SU_DUNG'
                  AND "Description" IN (
                      'Giám đốc, Chủ tịch: duyệt danh mục sản phẩm, nhận xét, đề xuất mức; lập kế hoạch 30-60-90 ngày (Mẫu 17).',
                      'Giám đốc, Chủ tịch: nhận xét, đề xuất mức; lập kế hoạch 30-60-90 ngày (Mẫu 17).');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
