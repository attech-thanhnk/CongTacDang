using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Wave3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalAuthority",
                table: "party_member_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "party_member_profiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ApprovalAuthority",
                table: "evaluation_records",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Chuyển dữ liệu: IsApprovedByAttech true → CoSo (1), false → CapTren (2);
            // mỗi tài khoản một SecurityStamp riêng; hồ sơ đánh giá lấy thẩm quyền từ hồ sơ cán bộ.
            migrationBuilder.Sql(@"
UPDATE party_member_profiles
SET ""ApprovalAuthority"" = CASE WHEN ""IsApprovedByAttech"" THEN 1 ELSE 2 END,
    ""SecurityStamp"" = md5(random()::text || clock_timestamp()::text || ""Id""::text);

UPDATE evaluation_records r
SET ""ApprovalAuthority"" = p.""ApprovalAuthority""
FROM party_member_profiles p
WHERE p.""Id"" = r.""MemberId"";

UPDATE evaluation_records SET ""ApprovalAuthority"" = 1 WHERE ""ApprovalAuthority"" = 0;
");

            migrationBuilder.DropColumn(
                name: "IsApprovedByAttech",
                table: "party_member_profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsApprovedByAttech",
                table: "party_member_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(@"UPDATE party_member_profiles SET ""IsApprovedByAttech"" = (""ApprovalAuthority"" <> 2);");

            migrationBuilder.DropColumn(
                name: "ApprovalAuthority",
                table: "party_member_profiles");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "party_member_profiles");

            migrationBuilder.DropColumn(
                name: "ApprovalAuthority",
                table: "evaluation_records");
        }
    }
}
