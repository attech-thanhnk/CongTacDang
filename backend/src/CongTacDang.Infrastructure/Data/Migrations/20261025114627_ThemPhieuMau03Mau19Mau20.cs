using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ThemPhieuMau03Mau19Mau20 : Migration
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
                name: "expert_crosschecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PartyCellId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpertDepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpertAgencyName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Fields = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    AddresseeName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RequestReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequestedById = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedById = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfirmedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IssuedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReturnReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CancelledById = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_expert_crosschecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_expert_crosschecks_administrative_departments_ExpertDepartm~",
                        column: x => x.ExpertDepartmentId,
                        principalTable: "administrative_departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expert_crosschecks_evaluation_records_RecordId",
                        column: x => x.RecordId,
                        principalTable: "evaluation_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.CreateTable(
                name: "expert_crosscheck_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CrosscheckId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SelfData = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SourceData = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Result = table.Column<int>(type: "integer", nullable: true),
                    AdjustmentType = table.Column<int>(type: "integer", nullable: false),
                    AdjustmentPoints = table.Column<double>(type: "double precision", nullable: true),
                    BasisText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    BasisDocNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BasisDocDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BasisAgency = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    NotApplicable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expert_crosscheck_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_expert_crosscheck_items_expert_crosschecks_CrosscheckId",
                        column: x => x.CrosscheckId,
                        principalTable: "expert_crosschecks",
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

            migrationBuilder.CreateIndex(
                name: "IX_expert_crosscheck_items_CrosscheckId_Order",
                table: "expert_crosscheck_items",
                columns: new[] { "CrosscheckId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_expert_crosschecks_ExpertDepartmentId_Status",
                table: "expert_crosschecks",
                columns: new[] { "ExpertDepartmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_expert_crosschecks_PeriodId",
                table: "expert_crosschecks",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_expert_crosschecks_RecordId_Status",
                table: "expert_crosschecks",
                columns: new[] { "RecordId", "Status" });
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
                name: "control_check_items");

            migrationBuilder.DropTable(
                name: "control_check_records");

            migrationBuilder.DropTable(
                name: "expert_crosscheck_items");

            migrationBuilder.DropTable(
                name: "calibration_sessions");

            migrationBuilder.DropTable(
                name: "control_checks");

            migrationBuilder.DropTable(
                name: "expert_crosschecks");

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
