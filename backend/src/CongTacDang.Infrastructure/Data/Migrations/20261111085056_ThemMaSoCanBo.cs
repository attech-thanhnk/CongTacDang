using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ThemMaSoCanBo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmployeeCode",
                table: "party_member_profiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // Điền một lần cho kiểu mặc định cũ; sau đó quản trị tự sửa.
            migrationBuilder.Sql("UPDATE form_layouts SET \"EmptyPositionText\" = 'Không' WHERE \"IsDefault\" AND \"Name\" = 'Cán bộ phòng ban' AND \"EmptyPositionText\" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmployeeCode",
                table: "party_member_profiles");
        }
    }
}
