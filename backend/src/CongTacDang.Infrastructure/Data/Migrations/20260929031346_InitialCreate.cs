using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OldValues = table.Column<string>(type: "text", nullable: false),
                    NewValues = table.Column<string>(type: "text", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RequestPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_periods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Quarter = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Settings = table.Column<string>(type: "jsonb", nullable: false),
                    StatusReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StatusChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StatusChangedBy = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_evaluation_periods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "org_unit_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Side = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_org_unit_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "positions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Side = table.Column<int>(type: "integer", nullable: false),
                    StatCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DefaultApprovalAuthority = table.Column<int>(type: "integer", nullable: true),
                    IsLeadership = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_positions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsProtected = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "administrative_departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    UnitTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Path = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_administrative_departments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_administrative_departments_administrative_departments_Paren~",
                        column: x => x.ParentId,
                        principalTable: "administrative_departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_administrative_departments_org_unit_types_UnitTypeId",
                        column: x => x.UnitTypeId,
                        principalTable: "org_unit_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "party_cells",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    UnitTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Path = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_party_cells", x => x.Id);
                    table.ForeignKey(
                        name: "FK_party_cells_org_unit_types_UnitTypeId",
                        column: x => x.UnitTypeId,
                        principalTable: "org_unit_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_party_cells_party_cells_ParentId",
                        column: x => x.ParentId,
                        principalTable: "party_cells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => new { x.permission_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_meetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyCellId = table.Column<Guid>(type: "uuid", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Stage = table.Column<int>(type: "integer", nullable: true),
                    FormCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MeetingType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvitedCount = table.Column<int>(type: "integer", nullable: false),
                    PresentCount = table.Column<int>(type: "integer", nullable: false),
                    AbsentCount = table.Column<int>(type: "integer", nullable: false),
                    AbsentReasons = table.Column<string>(type: "text", nullable: false),
                    ChairId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChairName = table.Column<string>(type: "text", nullable: false),
                    SecretaryId = table.Column<Guid>(type: "uuid", nullable: true),
                    SecretaryName = table.Column<string>(type: "text", nullable: false),
                    MinutesContent = table.Column<string>(type: "text", nullable: false),
                    OutcomeContent = table.Column<string>(type: "text", nullable: false),
                    VoteCountingContent = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_evaluation_meetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_evaluation_meetings_administrative_departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "administrative_departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_evaluation_meetings_evaluation_periods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "evaluation_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_evaluation_meetings_party_cells_PartyCellId",
                        column: x => x.PartyCellId,
                        principalTable: "party_cells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "party_member_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    AvatarUrl = table.Column<string>(type: "text", nullable: true),
                    IsPartyMember = table.Column<bool>(type: "boolean", nullable: false),
                    PartyCardNumber = table.Column<string>(type: "text", nullable: true),
                    JoinPartyDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OfficialPartyDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PartyCellId = table.Column<Guid>(type: "uuid", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PositionTitle = table.Column<string>(type: "text", nullable: false),
                    JobGroup = table.Column<int>(type: "integer", nullable: false),
                    ApprovalAuthority = table.Column<int>(type: "integer", nullable: false),
                    ApprovalAuthorityOverride = table.Column<int>(type: "integer", nullable: true),
                    ApprovalAuthorityOverrideReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SecurityStamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    FailedLoginCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LockoutEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_party_member_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_party_member_profiles_administrative_departments_Department~",
                        column: x => x.DepartmentId,
                        principalTable: "administrative_departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_party_member_profiles_party_cells_PartyCellId",
                        column: x => x.PartyCellId,
                        principalTable: "party_cells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "collective_evaluation_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    Form = table.Column<int>(type: "integer", nullable: false),
                    PartyCellId = table.Column<Guid>(type: "uuid", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    HeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubjectName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Strengths = table.Column<string>(type: "text", nullable: false),
                    Limitations = table.Column<string>(type: "text", nullable: false),
                    Causes = table.Column<string>(type: "text", nullable: false),
                    PreviousRemediation = table.Column<string>(type: "text", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: false),
                    Responsibilities = table.Column<string>(type: "text", nullable: false),
                    RemediationPlan = table.Column<string>(type: "text", nullable: false),
                    GeneralCriteriaScore = table.Column<double>(type: "double precision", nullable: false),
                    TaskCriteriaScore = table.Column<double>(type: "double precision", nullable: false),
                    TotalScore = table.Column<double>(type: "double precision", nullable: false),
                    SelfProposedGrade = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_collective_evaluation_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_collective_evaluation_records_administrative_departments_De~",
                        column: x => x.DepartmentId,
                        principalTable: "administrative_departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_collective_evaluation_records_evaluation_periods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "evaluation_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_collective_evaluation_records_party_cells_PartyCellId",
                        column: x => x.PartyCellId,
                        principalTable: "party_cells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_collective_evaluation_records_party_member_profiles_HeadId",
                        column: x => x.HeadId,
                        principalTable: "party_member_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyCellId = table.Column<Guid>(type: "uuid", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    JobGroup = table.Column<int>(type: "integer", nullable: false),
                    ApprovalAuthority = table.Column<int>(type: "integer", nullable: false),
                    WorkflowProfileCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReturnReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TasksApprovedById = table.Column<Guid>(type: "uuid", nullable: true),
                    TasksApprovedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TasksApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TasksApprovalComment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    GeneralScoreT1 = table.Column<double>(type: "double precision", nullable: false),
                    GeneralScoreT2 = table.Column<double>(type: "double precision", nullable: false),
                    GeneralScoreT3 = table.Column<double>(type: "double precision", nullable: false),
                    GeneralScoreT4 = table.Column<double>(type: "double precision", nullable: false),
                    GeneralScoreT5 = table.Column<double>(type: "double precision", nullable: false),
                    GeneralScoreT6 = table.Column<double>(type: "double precision", nullable: false),
                    GeneralCriteriaScore = table.Column<double>(type: "double precision", nullable: false),
                    TasksScore = table.Column<double>(type: "double precision", nullable: false),
                    AxisScoreT1 = table.Column<double>(type: "double precision", nullable: true),
                    AxisScoreT2 = table.Column<double>(type: "double precision", nullable: true),
                    AxisScoreT3 = table.Column<double>(type: "double precision", nullable: true),
                    AxisScoreT4 = table.Column<double>(type: "double precision", nullable: true),
                    AxisScoreT5 = table.Column<double>(type: "double precision", nullable: true),
                    AxisScoreT6 = table.Column<double>(type: "double precision", nullable: true),
                    SelfScoreForm = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    TotalSelfScore = table.Column<double>(type: "double precision", nullable: false),
                    SelfProposedGrade = table.Column<int>(type: "integer", nullable: false),
                    SelfScoredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PartyCellComment = table.Column<string>(type: "text", nullable: false),
                    CellConfirmedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CellConfirmedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CellConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CollectiveProposedGrade = table.Column<int>(type: "integer", nullable: false),
                    CollectiveComment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CollectiveMeetingId = table.Column<Guid>(type: "uuid", nullable: true),
                    CollectiveRecordedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CollectiveRecordedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CollectiveRecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppraisalScore = table.Column<double>(type: "double precision", nullable: true),
                    AppraisalComment = table.Column<string>(type: "text", nullable: false),
                    AppraisalProposedGrade = table.Column<int>(type: "integer", nullable: false),
                    AppraisedById = table.Column<Guid>(type: "uuid", nullable: true),
                    AppraisedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AppraisedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DirectorComment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DirectorProposedGrade = table.Column<int>(type: "integer", nullable: false),
                    DirectorReviewedById = table.Column<Guid>(type: "uuid", nullable: true),
                    DirectorReviewedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DirectorReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinalScore = table.Column<double>(type: "double precision", nullable: false),
                    FinalGrade = table.Column<int>(type: "integer", nullable: false),
                    DecisionDocumentNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DecisionDocumentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionAuthorityName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DecisionMeetingId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionRecordedById = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionRecordedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DecisionRecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedById = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_evaluation_records_administrative_departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "administrative_departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_evaluation_records_evaluation_periods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "evaluation_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_evaluation_records_party_cells_PartyCellId",
                        column: x => x.PartyCellId,
                        principalTable: "party_cells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_evaluation_records_party_member_profiles_MemberId",
                        column: x => x.MemberId,
                        principalTable: "party_member_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "member_positions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyCellId = table.Column<Guid>(type: "uuid", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_member_positions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_member_positions_administrative_departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "administrative_departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_member_positions_party_cells_PartyCellId",
                        column: x => x.PartyCellId,
                        principalTable: "party_cells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_member_positions_party_member_profiles_UserId",
                        column: x => x.UserId,
                        principalTable: "party_member_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_member_positions_positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReplacedByTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByIp = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_party_member_profiles_UserId",
                        column: x => x.UserId,
                        principalTable: "party_member_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    OriginalFileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ObjectKey = table.Column<string>(type: "text", nullable: false),
                    Checksum = table.Column<string>(type: "text", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UploadedBy = table.Column<string>(type: "text", nullable: false),
                    UploadedById = table.Column<Guid>(type: "uuid", nullable: true),
                    FormCode = table.Column<string>(type: "text", nullable: false),
                    RelatedId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    OwnerType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    IsSuperseded = table.Column<bool>(type: "boolean", nullable: false),
                    SupersededAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SupersededById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_attachments_party_member_profiles_UploadedById",
                        column: x => x.UploadedById,
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

            migrationBuilder.CreateTable(
                name: "collective_evaluation_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectiveRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemOrder = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TaskName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PlanOrDirection = table.Column<string>(type: "text", nullable: false),
                    Result = table.Column<string>(type: "text", nullable: false),
                    Limitations = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collective_evaluation_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_collective_evaluation_items_collective_evaluation_records_C~",
                        column: x => x.CollectiveRecordId,
                        principalTable: "collective_evaluation_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_external_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Step = table.Column<int>(type: "integer", nullable: false),
                    AuthorityName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DocumentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Grade = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: true),
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedById = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_external_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_evaluation_external_results_evaluation_records_RecordId",
                        column: x => x.RecordId,
                        principalTable: "evaluation_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_meeting_vote_summaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    VotesExcellent = table.Column<int>(type: "integer", nullable: false),
                    VotesGood = table.Column<int>(type: "integer", nullable: false),
                    VotesSatisfactory = table.Column<int>(type: "integer", nullable: false),
                    VotesUnsatisfactory = table.Column<int>(type: "integer", nullable: false),
                    InvalidVotes = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_meeting_vote_summaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_evaluation_meeting_vote_summaries_evaluation_meetings_Meeti~",
                        column: x => x.MeetingId,
                        principalTable: "evaluation_meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_evaluation_meeting_vote_summaries_evaluation_records_Record~",
                        column: x => x.RecordId,
                        principalTable: "evaluation_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_record_histories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: true),
                    ToStatus = table.Column<int>(type: "integer", nullable: false),
                    Step = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ScoreBefore = table.Column<double>(type: "double precision", nullable: true),
                    ScoreAfter = table.Column<double>(type: "double precision", nullable: true),
                    GradeBefore = table.Column<int>(type: "integer", nullable: true),
                    GradeAfter = table.Column<int>(type: "integer", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorName = table.Column<string>(type: "text", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_record_histories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_evaluation_record_histories_evaluation_records_RecordId",
                        column: x => x.RecordId,
                        principalTable: "evaluation_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskOrder = table.Column<int>(type: "integer", nullable: false),
                    TaskName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TargetOutput = table.Column<string>(type: "text", nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
                    Deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CriteriaA_Ratio = table.Column<double>(type: "double precision", nullable: false),
                    CriteriaB_Ratio = table.Column<double>(type: "double precision", nullable: false),
                    CriteriaC_Ratio = table.Column<double>(type: "double precision", nullable: false),
                    CriteriaD_Ratio = table.Column<double>(type: "double precision", nullable: false),
                    SelfScore = table.Column<double>(type: "double precision", nullable: false),
                    SupervisorScore = table.Column<double>(type: "double precision", nullable: true),
                    IsExceedStandard = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_evaluation_tasks_evaluation_records_RecordId",
                        column: x => x.RecordId,
                        principalTable: "evaluation_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_evaluation_tasks_task_attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "task_attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_administrative_departments_Code",
                table: "administrative_departments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_administrative_departments_ParentId",
                table: "administrative_departments",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_administrative_departments_Path",
                table: "administrative_departments",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_administrative_departments_UnitTypeId",
                table: "administrative_departments",
                column: "UnitTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_CreatedAt",
                table: "audit_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_EntityType_EntityId",
                table: "audit_logs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_collective_evaluation_items_CollectiveRecordId",
                table: "collective_evaluation_items",
                column: "CollectiveRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_collective_evaluation_records_DepartmentId",
                table: "collective_evaluation_records",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_collective_evaluation_records_HeadId",
                table: "collective_evaluation_records",
                column: "HeadId");

            migrationBuilder.CreateIndex(
                name: "IX_collective_evaluation_records_PartyCellId",
                table: "collective_evaluation_records",
                column: "PartyCellId");

            migrationBuilder.CreateIndex(
                name: "IX_collective_evaluation_records_PeriodId_Form_PartyCellId_Dep~",
                table: "collective_evaluation_records",
                columns: new[] { "PeriodId", "Form", "PartyCellId", "DepartmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_collective_evaluation_records_PeriodId_Status",
                table: "collective_evaluation_records",
                columns: new[] { "PeriodId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_external_results_RecordId_Step",
                table: "evaluation_external_results",
                columns: new[] { "RecordId", "Step" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_meeting_vote_summaries_MeetingId_RecordId",
                table: "evaluation_meeting_vote_summaries",
                columns: new[] { "MeetingId", "RecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_meeting_vote_summaries_RecordId",
                table: "evaluation_meeting_vote_summaries",
                column: "RecordId");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_meetings_DepartmentId",
                table: "evaluation_meetings",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_meetings_PartyCellId",
                table: "evaluation_meetings",
                column: "PartyCellId");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_meetings_PeriodId_DepartmentId",
                table: "evaluation_meetings",
                columns: new[] { "PeriodId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_meetings_PeriodId_PartyCellId_FormCode",
                table: "evaluation_meetings",
                columns: new[] { "PeriodId", "PartyCellId", "FormCode" });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_periods_Status",
                table: "evaluation_periods",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_periods_Year_Quarter",
                table: "evaluation_periods",
                columns: new[] { "Year", "Quarter" });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_record_histories_RecordId_CreatedAt",
                table: "evaluation_record_histories",
                columns: new[] { "RecordId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_records_DepartmentId",
                table: "evaluation_records",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_records_MemberId",
                table: "evaluation_records",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_records_PartyCellId",
                table: "evaluation_records",
                column: "PartyCellId");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_records_PeriodId_MemberId",
                table: "evaluation_records",
                columns: new[] { "PeriodId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_records_PeriodId_Status",
                table: "evaluation_records",
                columns: new[] { "PeriodId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_records_Status",
                table: "evaluation_records",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_tasks_AttachmentId",
                table: "evaluation_tasks",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_tasks_RecordId_TaskOrder",
                table: "evaluation_tasks",
                columns: new[] { "RecordId", "TaskOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_login_events_CreatedAt",
                table: "login_events",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_login_events_UserId_CreatedAt",
                table: "login_events",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_member_positions_DepartmentId",
                table: "member_positions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_member_positions_PartyCellId",
                table: "member_positions",
                column: "PartyCellId");

            migrationBuilder.CreateIndex(
                name: "IX_member_positions_PositionId",
                table: "member_positions",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_member_positions_UserId",
                table: "member_positions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_org_unit_types_Side_Name",
                table: "org_unit_types",
                columns: new[] { "Side", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_party_cells_Code",
                table: "party_cells",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_party_cells_ParentId",
                table: "party_cells",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_party_cells_Path",
                table: "party_cells",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_party_cells_UnitTypeId",
                table: "party_cells",
                column: "UnitTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_party_member_profiles_DepartmentId",
                table: "party_member_profiles",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_party_member_profiles_IsActive",
                table: "party_member_profiles",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_party_member_profiles_PartyCellId",
                table: "party_member_profiles",
                column: "PartyCellId");

            migrationBuilder.CreateIndex(
                name: "IX_party_member_profiles_Username",
                table: "party_member_profiles",
                column: "Username",
                unique: true);

            // Tên đăng nhập duy nhất không phân biệt hoa thường (kể cả tài khoản đã xóa mềm); đăng nhập so khớp lower("Username").
            // Index biểu thức nằm ngoài model EF (Npgsql không khai báo được bằng HasIndex) nên has-pending-model-changes không thấy.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_party_member_profiles_Username_lower\" ON party_member_profiles (lower(\"Username\"));");

            migrationBuilder.CreateIndex(
                name: "IX_permissions_Code",
                table: "permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_positions_Name",
                table: "positions",
                column: "Name",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_TokenHash",
                table: "refresh_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UserId",
                table: "refresh_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_role_id",
                table: "role_permissions",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_Code",
                table: "roles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_Name",
                table: "roles",
                column: "Name",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_task_attachments_FileGroupId",
                table: "task_attachments",
                column: "FileGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_task_attachments_OwnerType_OwnerId",
                table: "task_attachments",
                columns: new[] { "OwnerType", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_task_attachments_RecordId",
                table: "task_attachments",
                column: "RecordId");

            migrationBuilder.CreateIndex(
                name: "IX_task_attachments_RelatedId",
                table: "task_attachments",
                column: "RelatedId");

            migrationBuilder.CreateIndex(
                name: "IX_task_attachments_UploadedById",
                table: "task_attachments",
                column: "UploadedById");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "collective_evaluation_items");

            migrationBuilder.DropTable(
                name: "evaluation_external_results");

            migrationBuilder.DropTable(
                name: "evaluation_meeting_vote_summaries");

            migrationBuilder.DropTable(
                name: "evaluation_record_histories");

            migrationBuilder.DropTable(
                name: "evaluation_tasks");

            migrationBuilder.DropTable(
                name: "login_events");

            migrationBuilder.DropTable(
                name: "member_positions");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "user_role_assignments");

            migrationBuilder.DropTable(
                name: "collective_evaluation_records");

            migrationBuilder.DropTable(
                name: "evaluation_meetings");

            migrationBuilder.DropTable(
                name: "evaluation_records");

            migrationBuilder.DropTable(
                name: "task_attachments");

            migrationBuilder.DropTable(
                name: "positions");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "evaluation_periods");

            migrationBuilder.DropTable(
                name: "party_member_profiles");

            migrationBuilder.DropTable(
                name: "administrative_departments");

            migrationBuilder.DropTable(
                name: "party_cells");

            migrationBuilder.DropTable(
                name: "org_unit_types");
        }
    }
}
