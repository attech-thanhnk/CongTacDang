using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddControlChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "control_checks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PartyCellId = table.Column<Guid>(type: "uuid", nullable: true),
                    GroupName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Conclusion = table.Column<int>(type: "integer", nullable: true),
                    NeedsWorkContent = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ItemsVersion = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    PerformedById = table.Column<Guid>(type: "uuid", nullable: true),
                    PerformedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PerformedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfirmedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReturnReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_control_checks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_control_checks_evaluation_periods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "evaluation_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "control_check_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ItemText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Result = table.Column<int>(type: "integer", nullable: true),
                    Evidence = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_control_check_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_control_check_items_control_checks_CheckId",
                        column: x => x.CheckId,
                        principalTable: "control_checks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "control_check_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    GradeAtCheck = table.Column<int>(type: "integer", nullable: false),
                    ScoreAtCheck = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_control_check_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_control_check_records_control_checks_CheckId",
                        column: x => x.CheckId,
                        principalTable: "control_checks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_control_check_records_evaluation_records_RecordId",
                        column: x => x.RecordId,
                        principalTable: "evaluation_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_control_check_items_CheckId_ItemCode",
                table: "control_check_items",
                columns: new[] { "CheckId", "ItemCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_control_check_records_CheckId_RecordId",
                table: "control_check_records",
                columns: new[] { "CheckId", "RecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_control_check_records_RecordId",
                table: "control_check_records",
                column: "RecordId",
                unique: true,
                filter: "\"IsCurrent\"");

            migrationBuilder.CreateIndex(
                name: "IX_control_checks_DepartmentId",
                table: "control_checks",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_control_checks_PartyCellId",
                table: "control_checks",
                column: "PartyCellId");

            migrationBuilder.CreateIndex(
                name: "IX_control_checks_PeriodId_Status",
                table: "control_checks",
                columns: new[] { "PeriodId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "control_check_items");

            migrationBuilder.DropTable(
                name: "control_check_records");

            migrationBuilder.DropTable(
                name: "control_checks");
        }
    }
}
