using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PositionDefaultWorkflowProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultWorkflowProfileCode",
                table: "positions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // CSDL đang chạy: Phó Giám đốc Công ty theo cách xét duyệt riêng của danh mục mặc định (khớp DataSeeder).
            migrationBuilder.Sql("""
                UPDATE positions SET "DefaultWorkflowProfileCode" = 'pho-giam-doc'
                WHERE NOT "IsDeleted" AND "Name" = 'Phó Giám đốc' AND "DefaultWorkflowProfileCode" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultWorkflowProfileCode",
                table: "positions");
        }
    }
}
