using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <summary>Điền mã SPnn cho sản phẩm đang trống mã: theo từng hồ sơ, thứ tự hiển thị, tiếp sau số SPnn lớn nhất (kể cả sản phẩm đã xóa).</summary>
    public partial class ThemMaSanPham : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH base AS (
                    SELECT "RecordId",
                           COALESCE(MAX(CAST(substring(btrim("ProductCode") FROM '^[Ss][Pp]([0-9]{1,6})$') AS integer)), 0) AS max_no
                    FROM evaluation_tasks
                    GROUP BY "RecordId"
                ), missing AS (
                    SELECT t."Id", t."RecordId",
                           ROW_NUMBER() OVER (PARTITION BY t."RecordId" ORDER BY t."TaskOrder", t."CreatedAt", t."Id") AS rn
                    FROM evaluation_tasks t
                    WHERE NOT t."IsDeleted" AND (t."ProductCode" IS NULL OR btrim(t."ProductCode") = '')
                )
                UPDATE evaluation_tasks e
                SET "ProductCode" = 'SP' || CASE WHEN b.max_no + m.rn < 10 THEN '0' ELSE '' END || (b.max_no + m.rn)::text
                FROM missing m
                JOIN base b ON b."RecordId" = m."RecordId"
                WHERE e."Id" = m."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Không phân biệt được mã tự đánh với mã người dùng nhập nên giữ nguyên.
        }
    }
}
