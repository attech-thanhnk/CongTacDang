using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ThemBienBanHieuChuanMau20 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CalibratedAt",
                table: "evaluation_records",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CalibratedGrade",
                table: "evaluation_records",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CalibratedScore",
                table: "evaluation_records",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CalibrationSessionId",
                table: "evaluation_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "calibration_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PartyCellId = table.Column<Guid>(type: "uuid", nullable: true),
                    GroupName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    HeldAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChairName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChairMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    SecretaryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SecretaryMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScopeNote = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ExtraConclusion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    PreparedById = table.Column<Guid>(type: "uuid", nullable: true),
                    PreparedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PreparedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConcludedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcludedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ConcludedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReopenReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_calibration_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_calibration_sessions_administrative_departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "administrative_departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_calibration_sessions_evaluation_periods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "evaluation_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_calibration_sessions_party_cells_PartyCellId",
                        column: x => x.PartyCellId,
                        principalTable: "party_cells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "calibration_attendees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    Capacity = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DissentNote = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calibration_attendees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_calibration_attendees_calibration_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "calibration_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calibration_cases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    GradeBefore = table.Column<int>(type: "integer", nullable: false),
                    ScoreBefore = table.Column<double>(type: "double precision", nullable: true),
                    Clarification = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    GradeAfter = table.Column<int>(type: "integer", nullable: true),
                    ScoreAfter = table.Column<double>(type: "double precision", nullable: true),
                    Basis = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ResponsibleName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ResponsibleMemberId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calibration_cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_calibration_cases_calibration_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "calibration_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calibration_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    GradeBefore = table.Column<int>(type: "integer", nullable: false),
                    ScoreBefore = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calibration_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_calibration_records_calibration_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "calibration_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_calibration_records_evaluation_records_RecordId",
                        column: x => x.RecordId,
                        principalTable: "evaluation_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "calibration_summary_rows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GradeCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NotableDifference = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PreliminaryConclusion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calibration_summary_rows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_calibration_summary_rows_calibration_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "calibration_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_records_CalibrationSessionId",
                table: "evaluation_records",
                column: "CalibrationSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_calibration_attendees_SessionId_Order",
                table: "calibration_attendees",
                columns: new[] { "SessionId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_calibration_cases_SessionId_Order",
                table: "calibration_cases",
                columns: new[] { "SessionId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_calibration_records_RecordId",
                table: "calibration_records",
                column: "RecordId",
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_calibration_records_SessionId",
                table: "calibration_records",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_calibration_sessions_DepartmentId",
                table: "calibration_sessions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_calibration_sessions_PartyCellId",
                table: "calibration_sessions",
                column: "PartyCellId");

            migrationBuilder.CreateIndex(
                name: "IX_calibration_sessions_PeriodId_Status",
                table: "calibration_sessions",
                columns: new[] { "PeriodId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_calibration_summary_rows_SessionId_GradeCode",
                table: "calibration_summary_rows",
                columns: new[] { "SessionId", "GradeCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calibration_attendees");

            migrationBuilder.DropTable(
                name: "calibration_cases");

            migrationBuilder.DropTable(
                name: "calibration_records");

            migrationBuilder.DropTable(
                name: "calibration_summary_rows");

            migrationBuilder.DropTable(
                name: "calibration_sessions");

            migrationBuilder.DropIndex(
                name: "IX_evaluation_records_CalibrationSessionId",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "CalibratedAt",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "CalibratedGrade",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "CalibratedScore",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "CalibrationSessionId",
                table: "evaluation_records");
        }
    }
}
