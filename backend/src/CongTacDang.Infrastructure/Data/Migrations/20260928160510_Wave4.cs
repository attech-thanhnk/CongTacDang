using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Wave4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Kiểm tra trước dữ liệu cũ cho các ràng buộc duy nhất mới — dừng với thông báo rõ ràng, không tự sửa dữ liệu.
            migrationBuilder.Sql("""
                DO $$
                DECLARE duplicates text;
                BEGIN
                    SELECT string_agg(format('"%s" (%s tài khoản)', u.lower_name, u.total), ', ')
                      INTO duplicates
                      FROM (SELECT lower("Username") AS lower_name, count(*) AS total
                              FROM party_member_profiles
                             GROUP BY lower("Username")
                            HAVING count(*) > 1) u;
                    IF duplicates IS NOT NULL THEN
                        RAISE EXCEPTION 'Wave4: tên đăng nhập trùng nhau khi không phân biệt hoa thường (kể cả tài khoản đã xóa): %. Hãy đổi tên đăng nhập của các tài khoản này cho khác nhau rồi chạy lại migration.', duplicates;
                    END IF;

                    SELECT string_agg(format('"%s"', r."Name"), ', ')
                      INTO duplicates
                      FROM (SELECT "Name" FROM roles WHERE "IsDeleted" = FALSE GROUP BY "Name" HAVING count(*) > 1) r;
                    IF duplicates IS NOT NULL THEN
                        RAISE EXCEPTION 'Wave4: có vai trò (chưa xóa) trùng tên: %. Hãy đổi tên cho khác nhau rồi chạy lại migration.', duplicates;
                    END IF;

                    SELECT string_agg(format('"%s"', r."Code"), ', ')
                      INTO duplicates
                      FROM roles r WHERE length(r."Description") > 1000;
                    IF duplicates IS NOT NULL THEN
                        RAISE EXCEPTION 'Wave4: mô tả vai trò dài quá 1000 ký tự: %. Hãy rút ngắn mô tả rồi chạy lại migration.', duplicates;
                    END IF;
                END $$;
                """);

            // Tên đăng nhập lưu chữ thường; duy nhất không phân biệt hoa thường (kể cả tài khoản đã xóa).
            migrationBuilder.Sql("""UPDATE party_member_profiles SET "Username" = lower("Username") WHERE "Username" <> lower("Username");""");
            migrationBuilder.Sql("""CREATE UNIQUE INDEX "IX_party_member_profiles_Username_lower" ON party_member_profiles (lower("Username"));""");

            migrationBuilder.DropColumn(
                name: "HeadId",
                table: "administrative_departments");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "roles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<bool>(
                name: "IsProtected",
                table: "roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Module",
                table: "permissions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "permissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginAt",
                table: "party_member_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "party_cells",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "administrative_departments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "login_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsernameAttempted = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_login_events_party_member_profiles_UserId",
                        column: x => x.UserId,
                        principalTable: "party_member_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_role_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeType = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_role_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_role_assignments_party_member_profiles_UserId",
                        column: x => x.UserId,
                        principalTable: "party_member_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_role_assignments_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_roles_Name",
                table: "roles",
                column: "Name",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_login_events_CreatedAt",
                table: "login_events",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_login_events_UserId_CreatedAt",
                table: "login_events",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_user_role_assignments_RoleId",
                table: "user_role_assignments",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_user_role_assignments_ScopeType_ScopeId",
                table: "user_role_assignments",
                columns: new[] { "ScopeType", "ScopeId" });

            migrationBuilder.CreateIndex(
                name: "IX_user_role_assignments_UserId",
                table: "user_role_assignments",
                column: "UserId");

            // Chuyển gán vai trò cũ (user_roles) thành bản gán phạm vi Toàn công ty, hiệu lực từ lúc nâng cấp,
            // TRƯỚC khi xóa bảng cũ. Vai trò giữ nguyên; mã quyền cũ của vai trò cũ không còn hiệu lực — quản trị rà lại bản gán.
            migrationBuilder.Sql("""
                INSERT INTO user_role_assignments
                    ("Id", "UserId", "RoleId", "ScopeType", "ScopeId", "ValidFrom", "ValidTo", "Note", "CreatedAt", "IsDeleted")
                SELECT gen_random_uuid(), ur.user_id, ur.role_id, 0, NULL, now(), NULL,
                       'Chuyển từ gán vai trò cũ (user_roles) khi nâng cấp Wave4.', now(), FALSE
                  FROM user_roles ur;
                """);

            migrationBuilder.DropTable(
                name: "user_roles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dựng lại bảng user_roles và chép các bản gán Toàn công ty đang hiệu lực, chưa xóa (bản gán theo Phòng/Chi bộ
            // và thời hạn không biểu diễn được ở mô hình cũ → bị bỏ).
            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.role_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_user_roles_party_member_profiles_user_id",
                        column: x => x.user_id,
                        principalTable: "party_member_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_user_id",
                table: "user_roles",
                column: "user_id");

            migrationBuilder.Sql("""
                INSERT INTO user_roles (role_id, user_id)
                SELECT DISTINCT a."RoleId", a."UserId"
                  FROM user_role_assignments a
                 WHERE a."ScopeType" = 0 AND a."IsDeleted" = FALSE
                   AND a."ValidFrom" <= now() AND (a."ValidTo" IS NULL OR a."ValidTo" > now());
                """);

            // Tên đăng nhập đã hạ chữ thường không khôi phục được cách viết cũ (vẫn đăng nhập được, không phân biệt hoa thường).
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_party_member_profiles_Username_lower";""");

            migrationBuilder.DropTable(
                name: "login_events");

            migrationBuilder.DropTable(
                name: "user_role_assignments");

            migrationBuilder.DropIndex(
                name: "IX_roles_Name",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "IsProtected",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "Module",
                table: "permissions");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "permissions");

            migrationBuilder.DropColumn(
                name: "LastLoginAt",
                table: "party_member_profiles");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "party_cells");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "administrative_departments");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "roles",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            // HeadId không còn dùng (người đứng đầu xác định bằng gán vai trò phạm vi Phòng) → khôi phục cột rỗng.
            migrationBuilder.AddColumn<Guid>(
                name: "HeadId",
                table: "administrative_departments",
                type: "uuid",
                nullable: true);
        }
    }
}
