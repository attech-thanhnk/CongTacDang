using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ThemBangPhieuDoiSoatMau03 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "expert_crosscheck_items");

            migrationBuilder.DropTable(
                name: "expert_crosschecks");
        }
    }
}
