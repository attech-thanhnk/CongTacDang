using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CongTacDang.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class BoChamTheoTruc09B : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Xóa kỳ dùng bộ tiêu chí chấm theo trục (09B) cùng mọi dữ liệu phụ thuộc, rồi xóa bộ 09B; CSDL không có 09B thì không đổi gì.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE _p09b AS
                SELECT p."Id" FROM evaluation_periods p
                WHERE p."CriteriaSetId" IN (SELECT "Id" FROM criteria_sets WHERE "SelfScoreForm" = '09B')
                   OR p."CriteriaSnapshot" ->> 'selfScoreForm' = '09B';
                CREATE TEMP TABLE _r09b AS SELECT "Id" FROM evaluation_records WHERE "PeriodId" IN (SELECT "Id" FROM _p09b);
                CREATE TEMP TABLE _t09b AS SELECT "Id" FROM evaluation_tasks WHERE "RecordId" IN (SELECT "Id" FROM _r09b);
                CREATE TEMP TABLE _a09b AS SELECT "Id" FROM evaluation_appeals WHERE "RecordId" IN (SELECT "Id" FROM _r09b);
                CREATE TEMP TABLE _c09b AS SELECT "Id" FROM collective_evaluation_records WHERE "PeriodId" IN (SELECT "Id" FROM _p09b);
                CREATE TEMP TABLE _m09b AS SELECT "Id" FROM evaluation_meetings WHERE "PeriodId" IN (SELECT "Id" FROM _p09b);

                -- Tệp đính kèm chuyển sang "chưa gắn" để dịch vụ dọn tệp xóa cả bản ghi lẫn tệp trên đĩa.
                UPDATE task_attachments
                SET "OwnerType" = 'Unlinked', "OwnerId" = NULL, "RecordId" = NULL, "RelatedId" = NULL
                WHERE "RecordId" IN (SELECT "Id" FROM _r09b)
                   OR "RelatedId" IN (SELECT "Id" FROM _t09b)
                   OR ("OwnerType" = 'EvaluationRecord' AND "OwnerId" IN (SELECT "Id" FROM _r09b))
                   OR ("OwnerType" = 'EvaluationTask' AND "OwnerId" IN (SELECT "Id" FROM _t09b))
                   OR ("OwnerType" = 'EvaluationAppeal' AND "OwnerId" IN (SELECT "Id" FROM _a09b))
                   OR ("OwnerType" = 'CollectiveEvaluationRecord' AND "OwnerId" IN (SELECT "Id" FROM _c09b));

                DELETE FROM evaluation_meeting_vote_summaries
                WHERE "MeetingId" IN (SELECT "Id" FROM _m09b) OR "RecordId" IN (SELECT "Id" FROM _r09b);
                UPDATE evaluation_records SET "CollectiveMeetingId" = NULL WHERE "CollectiveMeetingId" IN (SELECT "Id" FROM _m09b);
                UPDATE evaluation_records SET "DecisionMeetingId" = NULL WHERE "DecisionMeetingId" IN (SELECT "Id" FROM _m09b);
                DELETE FROM evaluation_meetings WHERE "Id" IN (SELECT "Id" FROM _m09b);

                DELETE FROM task_change_requests WHERE "RecordId" IN (SELECT "Id" FROM _r09b);
                DELETE FROM improvement_plans WHERE "RecordId" IN (SELECT "Id" FROM _r09b);
                DELETE FROM evaluation_appeals WHERE "RecordId" IN (SELECT "Id" FROM _r09b);
                DELETE FROM evaluation_external_results WHERE "RecordId" IN (SELECT "Id" FROM _r09b);
                DELETE FROM evaluation_record_histories WHERE "RecordId" IN (SELECT "Id" FROM _r09b);
                DELETE FROM evaluation_tasks WHERE "RecordId" IN (SELECT "Id" FROM _r09b);
                DELETE FROM evaluation_records WHERE "Id" IN (SELECT "Id" FROM _r09b);

                DELETE FROM collective_evaluation_items WHERE "CollectiveRecordId" IN (SELECT "Id" FROM _c09b);
                DELETE FROM collective_evaluation_records WHERE "Id" IN (SELECT "Id" FROM _c09b);
                DELETE FROM report_drafts WHERE "PeriodId" IN (SELECT "Id" FROM _p09b);
                DELETE FROM evaluation_periods WHERE "Id" IN (SELECT "Id" FROM _p09b);

                UPDATE criteria_sets SET "SourceSetId" = NULL
                WHERE "SourceSetId" IN (SELECT "Id" FROM criteria_sets WHERE "SelfScoreForm" = '09B');
                DELETE FROM criteria_sets WHERE "SelfScoreForm" = '09B';
                DELETE FROM word_template_versions WHERE "TemplateCode" = 'MAU_09B';
                UPDATE evaluation_periods SET "CriteriaSnapshot" = "CriteriaSnapshot" - 'selfScoreForm'
                WHERE "CriteriaSnapshot" IS NOT NULL AND jsonb_exists("CriteriaSnapshot", 'selfScoreForm');

                DROP TABLE _p09b, _r09b, _t09b, _a09b, _c09b, _m09b;
                """);

            migrationBuilder.DropColumn(
                name: "HeaderStyle",
                table: "form_layouts");

            migrationBuilder.DropColumn(
                name: "ShowCellConfirmation",
                table: "form_layouts");

            migrationBuilder.DropColumn(
                name: "AxisNotes",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "AxisScores",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "SelfScoreForm",
                table: "evaluation_records");

            migrationBuilder.DropColumn(
                name: "SelfScoreForm",
                table: "criteria_sets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Chỉ thêm lại cột; dữ liệu 09B đã xóa không khôi phục được (dùng bản sao lưu CSDL).
            migrationBuilder.AddColumn<int>(
                name: "HeaderStyle",
                table: "form_layouts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ShowCellConfirmation",
                table: "form_layouts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AxisNotes",
                table: "evaluation_records",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AxisScores",
                table: "evaluation_records",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelfScoreForm",
                table: "evaluation_records",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelfScoreForm",
                table: "criteria_sets",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "09A");
        }
    }
}
