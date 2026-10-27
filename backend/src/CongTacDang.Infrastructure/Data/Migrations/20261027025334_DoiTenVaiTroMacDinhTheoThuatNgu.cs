using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DoiTenVaiTroMacDinhTheoThuatNgu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Chỉ đổi khi tên (và mô tả) còn đúng giá trị mặc định cũ nên tên quản trị đã sửa được giữ nguyên;
            // bỏ qua nếu tên mới đã bị vai trò khác dùng (chỉ mục duy nhất theo tên). Chạy lại không đổi thêm gì.
            // Cùng danh sách với bản vá trong docker/chuyen-du-lieu-goc.sh.
            migrationBuilder.Sql("""
                UPDATE roles r
                SET "Name" = v.new_name,
                    "Description" = CASE WHEN r."Description" = v.old_desc THEN v.new_desc ELSE r."Description" END,
                    "UpdatedAt" = now()
                FROM (VALUES
                    ('LANH_DAO_PHONG', 'Lãnh đạo Phòng', 'Lãnh đạo đơn vị',
                     'Duyệt danh mục sản phẩm, đề xuất mức, lập kế hoạch 30-60-90 ngày (Mẫu 17) cho cán bộ của Phòng.',
                     'Duyệt danh mục sản phẩm, đề xuất mức, lập kế hoạch 30-60-90 ngày (Mẫu 17) cho cán bộ của đơn vị.'),
                    ('THU_KY_TAP_THE', 'Thư ký tập thể lãnh đạo', 'Thư ký hội nghị',
                     'Ghi nhận đề xuất của tập thể lãnh đạo, lập biên bản hội nghị.',
                     'Ghi nhận đề xuất của tập thể lãnh đạo, lập biên bản hội nghị.'),
                    ('CHI_UY_CHI_BO', 'Chi ủy / Bí thư Chi bộ', 'Chi bộ',
                     'Xác nhận phiếu tự chấm, lập hồ sơ tập thể của Chi bộ.',
                     'Chi ủy, Bí thư Chi bộ: xác nhận phiếu tự chấm, lập hồ sơ tập thể của Chi bộ.'),
                    ('CO_QUAN_THAM_DINH', 'Phòng TCCB-LĐ (mở kỳ, thẩm định)', 'Cơ quan thẩm định',
                     'Thẩm định hồ sơ, quản lý kỳ đánh giá và bộ tiêu chí.',
                     'Mở kỳ và thẩm định hồ sơ, quản lý kỳ đánh giá và bộ tiêu chí.'),
                    ('CAP_TRUC_TIEP_SU_DUNG', 'Cấp trực tiếp sử dụng (Giám đốc/Chủ tịch)', 'Cấp trực tiếp sử dụng',
                     'Nhận xét, đề xuất mức; lập kế hoạch 30-60-90 ngày (Mẫu 17).',
                     'Giám đốc, Chủ tịch: nhận xét, đề xuất mức; lập kế hoạch 30-60-90 ngày (Mẫu 17).'),
                    ('CAP_UY_VIEN', 'Cấp ủy viên Đảng ủy', 'Cấp ủy viên',
                     'Xem hồ sơ, biên bản, báo cáo.',
                     'Cấp ủy viên Đảng ủy: xem hồ sơ, biên bản, báo cáo.'),
                    ('VAN_PHONG_DANG_UY', 'Văn phòng Đảng ủy (quyết định, công bố)', 'Văn phòng Đảng ủy',
                     'Ghi nhận quyết định và kết quả của cấp trên, công bố, mở lại hồ sơ, xử lý kiến nghị; xác nhận phiếu đối soát do cơ quan ngoài hệ thống (người yêu cầu nhập thay).',
                     'Quyết định, công bố: ghi nhận quyết định và kết quả của cấp trên, công bố, mở lại hồ sơ, xử lý kiến nghị; xác nhận phiếu đối soát do cơ quan ngoài hệ thống (người yêu cầu nhập thay).')
                ) AS v(code, old_name, new_name, old_desc, new_desc)
                WHERE r."Code" = v.code AND r."Name" = v.old_name AND r."IsDeleted" = FALSE
                  AND NOT EXISTS (
                      SELECT 1 FROM roles o
                      WHERE o."Name" = v.new_name AND o."IsDeleted" = FALSE AND o."Id" <> r."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
