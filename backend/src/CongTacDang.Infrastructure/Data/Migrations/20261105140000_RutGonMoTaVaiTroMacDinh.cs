using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <summary>Rút gọn mô tả vai trò mặc định theo thuật ngữ giao diện; tên và quyền của vai trò không đổi.</summary>
    [DbContext(typeof(CongTacDangDbContext))]
    [Migration("20261105140000_RutGonMoTaVaiTroMacDinh")]
    public partial class RutGonMoTaVaiTroMacDinh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Chỉ đổi mô tả còn đúng bản mặc định cũ, không ghi đè mô tả quản trị đã sửa.
            migrationBuilder.Sql("""
                UPDATE roles r
                SET "Description" = v.new_desc, "UpdatedAt" = now()
                FROM (VALUES
                    ('LANH_DAO_PHONG', 'Trưởng đơn vị gửi danh mục sản phẩm, đề xuất mức, lập kế hoạch 30-60-90 ngày (Mẫu 17) cho cán bộ của đơn vị.',
                     'Trưởng đơn vị gửi danh mục sản phẩm, đề xuất mức, lập kế hoạch 30-60-90 ngày cho cán bộ của đơn vị.'),
                    ('LANH_DAO_PHONG', 'Duyệt danh mục sản phẩm, đề xuất mức, lập kế hoạch 30-60-90 ngày (Mẫu 17) cho cán bộ của đơn vị.',
                     'Trưởng đơn vị gửi danh mục sản phẩm, đề xuất mức, lập kế hoạch 30-60-90 ngày cho cán bộ của đơn vị.'),
                    ('PHO_GIAM_DOC_PHU_TRACH', 'Phó Giám đốc phụ trách phòng, đơn vị: xem hồ sơ và ghi ý kiến trước khi Giám đốc phê duyệt danh mục và nhận xét; gán theo đơn vị phụ trách.',
                     'Xem hồ sơ và ghi ý kiến trước khi Giám đốc duyệt danh mục và nhận xét.'),
                    ('CO_QUAN_THAM_DINH', 'Rà soát, trình duyệt danh mục sản phẩm; mở kỳ và thẩm định hồ sơ, quản lý kỳ đánh giá và bộ tiêu chí.',
                     'Rà soát danh mục, thẩm định hồ sơ, quản lý kỳ đánh giá và bộ tiêu chí.'),
                    ('CO_QUAN_THAM_DINH', 'Mở kỳ và thẩm định hồ sơ, quản lý kỳ đánh giá và bộ tiêu chí.',
                     'Rà soát danh mục, thẩm định hồ sơ, quản lý kỳ đánh giá và bộ tiêu chí.'),
                    ('CAP_TRUC_TIEP_SU_DUNG', 'Giám đốc, Chủ tịch: duyệt danh mục sản phẩm, nhận xét, đề xuất mức; duyệt kế hoạch 30-60-90 ngày (Mẫu 17) do người quản lý trực tiếp lập.',
                     'Giám đốc, Chủ tịch: duyệt danh mục sản phẩm, nhận xét, đề xuất mức, duyệt kế hoạch 30-60-90 ngày.'),
                    ('VAN_PHONG_DANG_UY', 'Quyết định, công bố: ghi nhận quyết định và kết quả của cấp trên, công bố, mở lại hồ sơ, xử lý kiến nghị; xác nhận phiếu đối soát do cơ quan ngoài hệ thống (người yêu cầu nhập thay).',
                     'Ghi nhận quyết định và kết quả của cấp trên, công bố, mở lại hồ sơ, xử lý kiến nghị, xác nhận phiếu.'),
                    ('VAN_PHONG_DANG_UY', 'Ghi nhận quyết định và kết quả của cấp trên, công bố, mở lại hồ sơ, xử lý kiến nghị; xác nhận phiếu đối soát do cơ quan ngoài hệ thống (người yêu cầu nhập thay).',
                     'Ghi nhận quyết định và kết quả của cấp trên, công bố, mở lại hồ sơ, xử lý kiến nghị, xác nhận phiếu.'),
                    ('CO_QUAN_CHUYEN_MON', 'Phòng, đơn vị chuyên môn của ATTECH (ví dụ Phòng Kỹ thuật - An toàn - Chất lượng, Phòng Tài chính - Kế toán): lập và xác nhận Phiếu đối soát dữ liệu chuyên ngành (Mẫu 03) gửi tới đơn vị mình; gán theo đơn vị của từng phòng, đơn vị.',
                     'Phòng, đơn vị chuyên môn: lập và xác nhận phiếu xác nhận số liệu gửi tới đơn vị mình.'),
                    ('QUAN_TRI_HE_THONG', 'Quản trị kỹ thuật: tài khoản, vai trò, gán vai trò, nhật ký, danh mục, thông tin đơn vị, file mẫu biểu mẫu Word. Không xem nội dung đánh giá (Mẫu 18).',
                     'Quản trị tài khoản, vai trò, danh mục, thông tin đơn vị, file mẫu; không xem nội dung đánh giá.')
                ) AS v(code, old_desc, new_desc)
                WHERE r."Code" = v.code AND r."Description" = v.old_desc;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
