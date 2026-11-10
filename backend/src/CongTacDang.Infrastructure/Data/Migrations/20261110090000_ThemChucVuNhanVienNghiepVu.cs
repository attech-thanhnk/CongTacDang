using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <summary>Danh mục chức vụ đã có không được seed lại: thêm Nhân viên Nhân sự, Nhân viên công tác Đảng, đoàn thể và thi đua (không thuộc diện đánh giá).</summary>
    [DbContext(typeof(CongTacDangDbContext))]
    [Migration("20261110090000_ThemChucVuNhanVienNghiepVu")]
    public partial class ThemChucVuNhanVienNghiepVu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Chỉ thêm khi danh mục đã có dữ liệu và chưa có chức vụ cùng tên.
            foreach (var (name, order) in new[] { ("Nhân viên Nhân sự", 10), ("Nhân viên công tác Đảng, đoàn thể và thi đua", 20) })
            {
                migrationBuilder.Sql($"""
                    INSERT INTO positions ("Id", "Name", "Side", "StatCode", "DefaultApprovalAuthority", "IsLeadership", "IsHead",
                                           "IsActive", "IsDeleted", "SortOrder", "CreatedAt")
                    SELECT gen_random_uuid(), '{name}', 2, NULL, NULL, FALSE, FALSE, TRUE, FALSE,
                           COALESCE((SELECT MAX("SortOrder") FROM positions), 0) + {order}, now()
                    WHERE EXISTS (SELECT 1 FROM positions)
                      AND NOT EXISTS (SELECT 1 FROM positions WHERE "Name" = '{name}' AND "IsDeleted" = FALSE);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
