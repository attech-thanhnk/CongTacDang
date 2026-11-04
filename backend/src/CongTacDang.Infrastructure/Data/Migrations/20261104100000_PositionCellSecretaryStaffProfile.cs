using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <summary>CSDL đã có danh mục chức vụ không được seed lại: gán diện Bí thư chi bộ là nhân viên cho M24, M25 còn trống.</summary>
    [DbContext(typeof(CongTacDangDbContext))]
    [Migration("20261104100000_PositionCellSecretaryStaffProfile")]
    public partial class PositionCellSecretaryStaffProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Chỉ điền chỗ trống, không ghi đè diện quản trị đã chọn.
            migrationBuilder.Sql("""
                UPDATE positions SET "DefaultWorkflowProfileCode" = 'bi-thu-nhan-vien'
                WHERE "StatCode" IN ('M24', 'M25') AND "DefaultWorkflowProfileCode" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
