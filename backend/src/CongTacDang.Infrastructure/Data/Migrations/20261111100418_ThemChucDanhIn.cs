using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ThemChucDanhIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FormTitle",
                table: "positions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // Điền sẵn chức danh in theo đúng tên chức vụ; không ghi đè giá trị đã có.
            migrationBuilder.Sql(@"
                UPDATE positions p SET ""FormTitle"" = v.title
                FROM (VALUES
                    ('Bí thư Đảng ủy', 'Bí thư Đảng ủy Công ty'),
                    ('Phó Bí thư Đảng ủy', 'Phó Bí thư Đảng ủy Công ty'),
                    ('Ủy viên Ban Thường vụ Đảng ủy', 'Ủy viên Ban Thường vụ Đảng ủy Công ty'),
                    ('Đảng ủy viên', 'Ủy viên Ban Chấp hành Đảng bộ Công ty'),
                    ('Chủ nhiệm Ủy ban Kiểm tra Đảng ủy', 'Chủ nhiệm Ủy ban Kiểm tra Đảng ủy Công ty'),
                    ('Phó Chủ nhiệm Ủy ban Kiểm tra Đảng ủy', 'Phó Chủ nhiệm Ủy ban Kiểm tra Đảng ủy Công ty'),
                    ('Ủy viên Ủy ban Kiểm tra Đảng ủy', 'Ủy viên Ủy ban Kiểm tra Đảng ủy Công ty'),
                    ('Bí thư Đảng bộ bộ phận', 'Bí thư {don_vi}'),
                    ('Phó Bí thư Đảng bộ bộ phận', 'Phó Bí thư {don_vi}'),
                    ('Ủy viên Ban Chấp hành Đảng bộ bộ phận', 'Ủy viên Ban Chấp hành {don_vi}'),
                    ('Bí thư Chi bộ', 'Bí thư {don_vi}'),
                    ('Phó Bí thư Chi bộ', 'Phó Bí thư {don_vi}'),
                    ('Bí thư Chi bộ trực thuộc Đảng bộ bộ phận', 'Bí thư {don_vi}'),
                    ('Phó Bí thư Chi bộ trực thuộc Đảng bộ bộ phận', 'Phó Bí thư {don_vi}'),
                    ('Chi ủy viên', 'Chi ủy viên {don_vi}'),
                    ('Chủ tịch Công ty', 'Chủ tịch Công ty'),
                    ('Giám đốc', 'Giám đốc Công ty'),
                    ('Phó Giám đốc', 'Phó Giám đốc Công ty'),
                    ('Kiểm soát viên', 'Kiểm soát viên chủ sở hữu tại Công ty'),
                    ('Trưởng phòng', 'Trưởng {don_vi}'),
                    ('Phó Trưởng phòng', 'Phó Trưởng {don_vi}'),
                    ('Trưởng trung tâm', 'Trưởng {don_vi}'),
                    ('Phó Trưởng trung tâm', 'Phó Trưởng {don_vi}'),
                    ('Quản đốc', 'Quản đốc {don_vi}'),
                    ('Phó Quản đốc', 'Phó Quản đốc {don_vi}'),
                    ('Giám đốc đơn vị trực thuộc', 'Giám đốc {don_vi}'),
                    ('Phó Giám đốc đơn vị trực thuộc', 'Phó Giám đốc {don_vi}'),
                    ('Chánh Văn phòng', 'Chánh {don_vi}'),
                    ('Phó Chánh Văn phòng', 'Phó Chánh {don_vi}'),
                    ('Đội trưởng', 'Đội trưởng {don_vi}'),
                    ('Đài trưởng', 'Đài trưởng {don_vi}'),
                    ('Trạm trưởng', 'Trạm trưởng {don_vi}')
                ) AS v(name, title)
                WHERE p.""Name"" = v.name AND p.""FormTitle"" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FormTitle",
                table: "positions");
        }
    }
}
