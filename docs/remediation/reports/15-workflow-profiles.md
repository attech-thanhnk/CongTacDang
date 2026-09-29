# Báo cáo: Task 15 — Luồng theo nhóm đối tượng, bước do cấp trên thực hiện, người thực hiện cấu hình được

- **Branch:** `feat/workflow-profiles` (fast-forward tới `fd3a9f5` trước khi sửa file đầu tiên)
- **Commit cuối:** commit chứa báo cáo này (commit code cuối: `9b1a0bb`)
- **Ngày:** 2026-09-29

## Kết quả theo mã lỗi

| Mã | Trạng thái | Tóm tắt thay đổi | File chính |
|---|---|---|---|
| T-74 | done | `PeriodSettings` schema 2: `profiles[]` (mã, tên, mô tả, 9 bước × `mode` Internal/External/Off × `permission` × `deadline`) + `defaultProfiles` theo `ApprovalAuthority`; bỏ hẳn cấu hình bước cấp kỳ (schema 1 bị từ chối). Hồ sơ lưu `WorkflowProfileCode` (ảnh chụp khi thêm; chọn khi thêm / import / mặc định theo cấp quyết định). Máy trạng thái, `actions`, `work-queue`, kiểm tra quyền dùng hồ sơ luồng của hồ sơ. Bước `External`: `POST records/{id}/external/{step}` (quyền mới `evaluation.external.record`) ghi nhận cơ quan, số/ngày văn bản, nhận xét, mức, điểm, tệp; bảng mới `evaluation_external_results`, chép sang trường của bước để báo cáo/biểu mẫu dùng chung. Quyền mới `evaluation.unit.review` (Lãnh đạo đơn vị đề xuất — gán cho vai trò Lãnh đạo Phòng mặc định). Gỡ `evaluation.decide.external` khỏi danh mục (Văn phòng Đảng ủy nhận `evaluation.external.record`). Đổi hồ sơ luồng từng người/hàng loạt (lý do, chỉ khi chưa qua bước bị ảnh hưởng). Cột "Hồ sơ luồng" trong import người được đánh giá. 2 kiểu kỳ × 3 hồ sơ luồng theo PL III ví dụ 1–3. Xung đột lợi ích áp cho mọi bước không phải của chủ hồ sơ ngay trong service (kể cả quyền cấu hình khác mặc định). | `Domain/Evaluation/PeriodSettings.cs`, `WorkflowSteps.cs`, `RecordStateMachine.cs`, `Domain/Entities/EvaluationExternalResult.cs`, `EvaluationRecord.cs`, `Application/Services/EvaluationWorkflowService.cs`, `WorkflowActions.cs`, `PeriodService.cs`, `EvaluationMapping.cs`, `Common/Security/PermissionCodes.cs`, `Imports/Definitions/PeriodParticipantImportDefinition.cs`, `Api/Controllers/EvaluationController.cs`, `PeriodController.cs`, `Infrastructure/Data/DataSeeder.cs`, `Data/Configurations/EvaluationExternalResultConfiguration.cs` |
| T-75 | done | `GET periods/{id}/readiness`: mỗi hồ sơ chưa công bố × mỗi bước còn phía trước (Nội bộ hoặc Cấp trên) phải có ≥ 1 tài khoản đang hoạt động, không phải chủ hồ sơ, có quyền thực hiện bước trong phạm vi bao trùm hồ sơ (ứng viên lấy từ bản gán hiệu lực; quyền tính bằng `IPermissionResolver` + `AuthorizationGuard.Evaluate`). Bước của chủ hồ sơ: chủ hồ sơ phải hoạt động và có `evaluation.self`. Mở kỳ còn cảnh báo → 409 `code = PERIOD_NOT_READY`, `data` = kết quả, `errors` = thông báo; mở bắt buộc `force = true` + lý do (thiếu lý do → 400), lý do ghi vào `StatusReason`. | `PeriodService.BuildReadinessAsync`, `PeriodController.Open/GetReadiness`, `EvaluationWorkflowRepository.ListActiveUserIdsWithAnyPermissionAsync` |
| T-76 | done | `/periods/[id]`: `WorkflowProfilesEditor` (tab hồ sơ luồng, thêm bằng sao chép/sửa tên-mô tả/xóa, bảng bước × chế độ × quyền thực hiện (danh sách từ `GET periods/step-permissions`) × thời hạn, mặc định theo cấp quyết định), `ReadinessPanel`, mở kỳ 409 → bảng cảnh báo + hộp "Mở kỳ bắt buộc" có lý do, cột Hồ sơ luồng + đổi từng người/hàng loạt (checkbox), chọn hồ sơ luồng khi thêm người. Trang hồ sơ: bước External hiện "Do cấp trên thực hiện — ghi nhận kết quả", form ghi nhận; bước Off hiện "Không áp dụng cho nhóm này"; bảng kết quả cấp trên đã ghi nhận. `/work-queue`: tên hồ sơ luồng, nhãn bước cấp trên, nút "Ghi nhận". | `frontend/app/periods/**`, `app/evaluations/[recordId]/page.tsx`, `app/work-queue/page.tsx`, `components/evaluations/{WorkflowProfilesEditor,ReadinessPanel,RecordActionPanel,RecordProgress}.tsx`, `services/evaluationService.ts` |

## Luồng kiểm thử (task file mục Test)

Tích hợp: `backend/tests/CongTacDang.IntegrationTests/WorkflowProfileIntegrationTests.cs` (mới). Unit: `backend/tests/CongTacDang.UnitTests/WorkflowProfileUnitTests.cs` (mới, 25 trường hợp: 14 fact + 11 theory case).

| # | Luồng | Trạng thái | Test |
|---|---|---|---|
| (a) | Hồ sơ Giám đốc (CapTren, hồ sơ luồng `cap-tren`, kiểu kỳ Q3) → tự chấm 09B → Chi bộ → B3a nội bộ → B3b/B3c/B4 **ghi nhận kết quả của cấp trên** → công bố. Kiểm: progress mode; cơ quan thẩm định nội bộ không có thao tác, gọi API thẩm định/trả lại → 409; chủ hồ sơ có `external.record` vẫn 403 (xung đột lợi ích); không quyền → 403; thiếu cơ quan/mức → 400; bước chưa tới → 409; `external/B5_PUBLISH` → 400; kết quả chép vào hồ sơ (mức, điểm, cơ quan); lịch sử 3 lần `RecordExternal`; mở lại về B4 rồi ghi nhận lại cập nhật bản ghi cũ | pass | `G1_DirectorRecord_UpperProfile_ExternalStepsRecorded_ToPublished` |
| (b) | Thêm người kèm hồ sơ luồng `bi-thu-nhan-vien` (mã sai → 400); tới B3c: Giám đốc (director.review) không có thao tác, gọi → 403 nêu "Lãnh đạo đơn vị đề xuất"; Lãnh đạo Phòng khác → 403; Lãnh đạo Phòng đúng Phòng có `DirectorReview`, work-queue có B3c (Giám đốc không); Trưởng phòng đề xuất → B4; đổi sang `co-so` sau khi qua B3c → 409; quyết định, công bố | pass | `G2_CellSecretaryStaffProfile_UnitLeaderProposesInsteadOfDirector` |
| (c) | Kỳ có 1 hồ sơ Phòng 3: readiness sạch → thu hồi (API quản trị) bản gán của người duy nhất có `unit.review` ở Phòng 3 → đúng 1 cảnh báo (B3C, `evaluation.unit.review`, phạm vi Phòng 3); người không có `period.manage` → 403; mở kỳ → 409 `PERIOD_NOT_READY` kèm `data.issues`/`errors`, kỳ vẫn Dự thảo; mở bắt buộc thiếu lý do → 400; có lý do → Đang mở, `statusReason` chứa lý do | pass | `G3_Readiness_DetectsStuckRecord_OpenReturns409_ForceNeedsReason` |
| (d) | Hồ sơ cơ sở đi như cũ: W1 (kiểu kỳ Đầy đủ, 9 bước, người thực hiện như trước, lịch sử đủ 9 bước) giữ nguyên kỳ vọng | pass | `EvaluationWorkflowTests.W1_FullPreset_ManualAndDepartmentParticipants_RecordWalksNineStepsToPublished` (và W2, W3, W5, W7–W10 không đổi kỳ vọng) |
| thêm | Cấu hình qua API (quyền không hợp lệ/B4 Off/B5 External → 400; xóa hồ sơ luồng đang dùng → 409; thêm hồ sơ luồng mới khi dự thảo); đổi hồ sơ luồng (thiếu lý do/không tồn tại → 400, không quyền → 403, hàng loạt bỏ qua hồ sơ sai phiên bản, lịch sử `ChangeProfile`); import cột "Hồ sơ luồng" (mã hoặc tên; sai → lỗi dòng; trống → mặc định); kỳ đã mở chỉ sửa được thời hạn theo hồ sơ luồng | pass | `G4_ProfileConfiguration_ChangeProfile_Bulk_Import_AndValidation` |

Unit (`WorkflowProfileUnitTests`): 3 hồ sơ luồng × 2 kiểu kỳ đúng PL III; mã quyền dùng trong Domain đều thuộc `PermissionCodes`, `decide.external` đã gỡ; danh sách quyền gán được cho bước; bước bắt buộc không Off (3), không External (3), B3b được Off; cấu hình không nhất quán (duyệt không đăng ký, 09A thiếu B1, mẫu tự chấm, schema 1, mã bước lạ, trùng mã, mã sai mẫu, thiếu tên, thiếu/sai mặc định, không có hồ sơ luồng); quyền không hợp lệ (5 mã); chuẩn hóa; JSON khứ hồi, mode là chuỗi, khóa không phân biệt hoa thường, mode lạ → FormatException; chỉ khác thời hạn; tra cứu hồ sơ luồng (mã → mặc định → đầu tiên); bước khác nhau giữa hai hồ sơ luồng; máy trạng thái với External/Off; `Realign`; quyền theo chế độ; hành động không khớp chế độ. `EvaluationWorkflowUnitTests` (task 12): máy trạng thái nay đối chiếu **64** cấu hình (B3b tùy chọn) × 10 trạng thái × 27 lệnh; test `PeriodSettings` cũ chuyển sang file mới.

## Cổng kiểm tra

| Bước | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build backend/CongTacDang.slnx --no-incremental` | pass | 0 lỗi, **0 warning** |
| `dotnet test` — unit | pass | 210 test: 209 pass, 1 skip (`PdfConversionTests.Mau01…` — thiếu LibreOffice, skip sẵn có), 0 fail |
| `dotnet test` — integration, có `CONGTACDANG_TEST_PG` (máy chủ 159) | pass | **76 test đã chạy: 76 pass, 0 skip**, 0 fail (4 mới của task 15). Lần chạy đầu `DataSeederAuthorizationTests.SampleData_SeededOnce…` lỗi ở bước `CREATE DATABASE` (lỗi máy chủ, chi tiết bị ẩn — nghi tạo CSDL đồng thời); chạy lại 2 lần đều pass, không liên quan thay đổi |
| `npx tsc --noEmit` | pass | |
| `npm run build` | pass | `next lint`: không cảnh báo mới ở file của task (cảnh báo còn lại là của file khác, có sẵn) |
| Chạy app / thử giao diện thủ công | không chạy | Không tạo `ctd_it_g15`; giao diện kiểm bằng `tsc` + `build` + lint, API bằng test tích hợp |
| CSDL tạm | chỉ `ctd_it_*` | Fixture tạo/xóa; không đụng `congtacdang_test` |

**Migration tạm để chạy test (không commit):** test tích hợp dựng CSDL bằng `MigrateAsync`, nên trước khi chạy đã sinh cục bộ `dotnet ef migrations add TmpTask15Local -p backend/src/CongTacDang.Infrastructure -s backend/src/CongTacDang.Api -o Data/Migrations` (chỉ gồm cột `WorkflowProfileCode` + bảng `evaluation_external_results`), chạy test, rồi xóa 2 tệp `*_TmpTask15Local*.cs` và `git checkout` lại `CongTacDangDbContextModelSnapshot.cs`; build lại 0 warning. Không commit nào chứa migration/snapshot đã đổi.

## Cấu trúc `PeriodSettings` mới (schema 2)

```json
{
  "schemaVersion": 2,
  "profiles": [
    { "code": "co-so", "name": "Diện Đảng ủy cơ sở", "description": "…",
      "steps": { "B1_REGISTER": { "mode": "Internal", "permission": null, "deadline": null }, "…": "…",
                 "B3C_DIRECTOR": { "mode": "Internal", "permission": "evaluation.director.review", "deadline": "2026-12-14" } } }
  ],
  "defaultProfiles": { "CoSo": "co-so", "CapTren": "cap-tren" },
  "enforceDeadlines": false,
  "selfScoreForm": "09A",
  "parameters": { "…": "như task 12" }
}
```
- `mode` ∈ `Internal | External | Off` (JSON chuỗi). `permission` chỉ giữ ở bước Nội bộ không phải của chủ hồ sơ (chuẩn hóa: bỏ trống → quyền mặc định của bước; chế độ khác → null). Phải là mã module `evaluation` trong `PermissionCodes`, trừ `self`/`read`/`reopen`.
- Bắt buộc không `Off`: `B2_SELF_SCORE`, `B4_DECISION`, `B5_PUBLISH`. Không `External`: `B1_REGISTER`, `B2_SELF_SCORE`, `B5_PUBLISH`. `B1_APPROVE` áp dụng cần `B1_REGISTER` áp dụng; `09A` cần `B1_REGISTER` ở mọi hồ sơ luồng. Mã hồ sơ luồng `^[a-z0-9][a-z0-9-]{0,49}$`, duy nhất, ≤ 20 hồ sơ luồng; `defaultProfiles` phải có `CoSo` và `CapTren` trỏ tới hồ sơ luồng tồn tại.
- `deadline` theo bước của từng hồ sơ luồng. Kỳ đã mở: chỉ sửa thời hạn (so sánh cấu hình bỏ thời hạn).

## Bảng kiểu kỳ × hồ sơ luồng × bước (mặc định — chờ nghiệp vụ xác nhận)

| Kiểu kỳ | Hồ sơ luồng | B1_REGISTER | B1_APPROVE | B2_SELF | B2_CELL | B3A | B3B | B3C | B4 | B5 | Mẫu |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Đầy đủ (`full`) | Diện Đảng ủy cơ sở (`co-so`, mặc định CoSo) | Nội bộ | Nội bộ `tasks.approve` | Nội bộ | Nội bộ `cell.confirm` | Nội bộ `collective.record` | Nội bộ `appraise` | Nội bộ `director.review` | Nội bộ `decide` | Nội bộ `publish` | 09A |
| | Diện BTV Đảng ủy TCT (`cap-tren`, mặc định CapTren) | Nội bộ | Nội bộ | Nội bộ | Nội bộ | Nội bộ | **Cấp trên** | **Cấp trên** | **Cấp trên** | Nội bộ | |
| | Bí thư/Phó bí thư Chi bộ là nhân viên (`bi-thu-nhan-vien`) | Nội bộ | Nội bộ | Nội bộ | Nội bộ | Nội bộ | Nội bộ | Nội bộ **`unit.review`** | Nội bộ `decide` | Nội bộ | |
| Quý III/2026 — chuyển tiếp (`q3-2026-transition`) | 3 hồ sơ luồng như trên | **Off** | **Off** | như trên | | | | | | | 09B |

(Cấp trên = ghi nhận bằng `evaluation.external.record`.)

## Endpoint mới / đổi

| Method | Route | Policy controller | Ghi chú |
|---|---|---|---|
| POST | `/api/evaluations/records/{id}/external/{step}` | `evaluation.external.record` | **Mới.** `{ version, authorityName*, documentNumber?, documentDate?, comment?, grade (bắt buộc B3a/B3b/B3c/B4), score? (0–100), attachmentId? }` → hồ sơ; bước không cho cấp trên → 400; bước đang Nội bộ/Off hoặc chưa tới → 409; chủ hồ sơ → 403 |
| GET | `/api/evaluations/periods/{id}/readiness` | `period.manage` | **Mới.** `{ periodId, ready, checkedRecords, issues[] }` |
| GET | `/api/evaluations/periods/step-permissions` | `period.manage` | **Mới.** Quyền chọn được làm quyền thực hiện bước `[{ code, name, description }]` |
| PUT | `/api/evaluations/periods/{id}/participants/{recordId}/profile` | `period.manage` | **Mới.** `{ version, workflowProfileCode, reason* }` → người được đánh giá |
| PUT | `/api/evaluations/periods/{id}/participants/profile` | `period.manage` | **Mới.** `{ items: [{ recordId, version }], workflowProfileCode, reason* }` → `{ updated, skipped[] }` |
| POST | `/api/evaluations/periods/{id}/open` | `period.manage` | **Đổi.** Body thêm `force`; còn cảnh báo kẹt luồng → **409** `{ success:false, code:"PERIOD_NOT_READY", message, data: readiness, errors: [...] }`; `force` thiếu lý do → 400 |
| POST | `/api/evaluations/periods/{id}/participants` | `period.manage` | **Đổi.** Body thêm `workflowProfileCode?` (mã sai → 400) |
| PUT | `/api/evaluations/periods/{id}` | `period.manage` | **Đổi.** `settings` theo schema 2; xóa hồ sơ luồng đang dùng → 409 |
| POST | `…/tasks/approve`, `tasks/return`, `cell/confirm`, `cell/return`, `collective`, `appraisal`, `appraisal/return`, `director-review`, `decision`, `publish` | **Đăng nhập** (trước: mã quyền cố định) | Quyền thực hiện theo hồ sơ luồng của hồ sơ → service: chế độ bước (409 nếu không khớp) → xung đột lợi ích (403) → `guard.Ensure(quyền của bước)` (403 nêu tên quyền) |
| GET | `records/{id}`, `records/{id}/actions`, `work-queue`, `periods/{id}/participants` | như cũ | DTO thêm: hồ sơ `workflowProfileCode/Name`, `progress[].mode`, `externalResults[]`; actions có `RecordExternal` (bước ở `step`); work-queue item `workflowProfileName`, `mode`; participant `workflowProfileCode/Name` |
| Import | `period-participants` | như cũ | Thêm cột tùy chọn **"Hồ sơ luồng"** (khóa `workflowProfile`; mã hoặc tên) |

## Thay đổi schema (cần migration — người điều phối gộp vào `InitialCreate`)
- `evaluation_records`: thêm `WorkflowProfileCode character varying(50) NOT NULL` (mặc định `''`).
- Bảng mới `evaluation_external_results`: `Id uuid PK`, `RecordId uuid NOT NULL` (FK `evaluation_records`, `ON DELETE CASCADE`), `Step integer NOT NULL`, `AuthorityName varchar(300) NOT NULL`, `DocumentNumber varchar(100) NULL`, `DocumentDate timestamptz NULL`, `Comment varchar(4000) NULL`, `Grade integer NOT NULL`, `Score double precision NULL`, `AttachmentId uuid NULL` (tham chiếu mềm, không FK), `RecordedById uuid NULL`, `RecordedByName varchar(200) NULL`, `RecordedAt timestamptz NOT NULL`, `CreatedAt/CreatedBy/UpdatedAt/UpdatedBy`; **unique index** `(RecordId, Step)`. Query filter theo hồ sơ chưa xóa. Cấu hình: `Data/Configurations/EvaluationExternalResultConfiguration.cs`.
- `evaluation_periods.Settings` (jsonb): nội dung schema 2 (không đổi cột).
- Danh mục quyền (seed đồng bộ từ `PermissionCodes`): thêm `evaluation.unit.review`, `evaluation.external.record`; bỏ `evaluation.decide.external`.

## Key config mới
Không có.

## Thay đổi hành vi API / breaking change
- `settings` schema 1 (`steps.{B…}.enabled`) không còn được nhận (400 "Phiên bản cấu hình 1 không được hỗ trợ…"). Frontend đã đổi cùng task.
- Hồ sơ diện cấp trên (mặc định `cap-tren`): B3b/B3c/B4 không còn làm bằng API nội bộ (409) — dùng `external/{step}`. Quyền `evaluation.decide.external` không còn.
- `B3B_APPRAISAL` không còn là bước bắt buộc (đặt được "Không áp dụng" cho một nhóm).
- Các API bước không phải của chủ hồ sơ không còn chặn ở controller: người không có quyền nhận 403 từ service (thông báo nêu tên quyền của bước theo hồ sơ luồng); hồ sơ không tồn tại → 404 thay vì 403.
- Mở kỳ có thể trả 409 `PERIOD_NOT_READY` (trước luôn mở nếu cấu hình hợp lệ).
- Vai trò mặc định: Lãnh đạo Phòng thêm `evaluation.unit.review`; Văn phòng Đảng ủy `evaluation.decide.external` → `evaluation.external.record` (chỉ áp dụng khi seed CSDL mới / `ResetRolePermissions`).
- Dữ liệu mẫu: hồ sơ Giám đốc (CapTren, Đã công bố) đi hồ sơ luồng `cap-tren`, B3b/B3c/B4 là kết quả cấp trên (có bản ghi `evaluation_external_results`, lịch sử `RecordExternal`).

## Cần phối hợp
- **`AuthorizationGuard` (task 14 đang sửa — không được sửa trong task này):**
  1. Gỡ hằng số `PermissionCodes.EvaluationDecideExternal` (hiện chỉ còn để guard biên dịch; đã gỡ khỏi `Definitions`, resolver bỏ qua) cùng các nhánh `decide.external` trong `Evaluate`/`Forbidden` và `ConflictOfInterestCodes`; sửa `AuthorizationGuardTests` tương ứng.
  2. Thêm `evaluation.external.record`, `evaluation.unit.review` vào `ConflictOfInterestCodes` (service luồng đã tự chặn chủ hồ sơ cho mọi bước không phải của chủ hồ sơ, nên hiện không có lỗ hổng — chỉ để guard nhất quán).
  3. Luật "`evaluation.decide` chỉ trên hồ sơ `CoSo`" vẫn nằm trong guard: nếu cấu hình B4 Nội bộ cho hồ sơ `CapTren` (hoặc chọn `evaluation.decide` cho bước khác), guard sẽ từ chối — kiểm tra kẹt luồng phát hiện được. Đề nghị bỏ ràng buộc theo `ApprovalAuthority` khỏi guard (hồ sơ luồng đã quyết định luồng), sau khi nghiệp vụ xác nhận.
- **`docs/thiet-ke/phan-quyen.md` mục 3, 4, 6** (không thuộc phạm vi file task 15; task 14 đang sửa mục phạm vi): thay dòng `evaluation.decide.external` bằng `evaluation.external.record` ("Ghi nhận kết quả của cấp trên"), thêm `evaluation.unit.review` ("Lãnh đạo đơn vị đề xuất"); mục 4: bỏ luật `decide.external`, thêm hai mã mới vào danh sách xung đột lợi ích; mục 6: Lãnh đạo Phòng + `evaluation.unit.review`, Văn phòng Đảng ủy `decide.external` → `external.record`.
- **`AttachmentService` (task 05/khác):** tệp đính kèm của kết quả cấp trên được tải lên dạng "không gắn hồ sơ" (chỉ người có `evaluation.self` hoặc `attachment.general.manage` tải lên được, và chỉ người tải lên xem được). Đề nghị: cho người có `evaluation.external.record` trên hồ sơ gắn tệp vào hồ sơ (`ownerType = EvaluationRecord`) và coi `evaluation_external_results.AttachmentId` là liên kết hồ sơ khi kiểm tra quyền xem.
- **`EvaluationConfigurations.cs`**: thêm 1 dòng `WorkflowProfileCode` (max 50) trong `EvaluationRecordConfiguration` — có thể đụng chỗ task 14 sửa khi merge.
- **Người điều phối:** sinh lại `InitialCreate` sau khi merge (mục "Thay đổi schema").
- **Test của task khác đã sửa kỳ vọng** (task file yêu cầu đổi hành vi): `EvaluationWorkflowTests` — W4 chuyển hồ sơ CapTren sang `co-so` bằng API đổi hồ sơ luồng trước khi kiểm tra trả lại ở B3b; W6 thay bằng `W6_CapTrenDecisionRecordedAsExternal_CoSoDecidedInternally`; vai trò `externalDecider` dùng `evaluation.external.record`; mở kỳ trong helper dùng `force` (thế giới W có người thực hiện theo phạm vi từng Phòng nên còn cảnh báo kẹt luồng). `AuthorizationMatrixTests.ApproveFinal_FollowsApprovalAuthority` — hồ sơ CapTren: `decision` → 409, ghi nhận qua `external/B4_DECISION`, người chỉ có `decide` → 403 "cấp trên". `GoLiveScenarioTests` — cấu hình B3a/B3c "Off" theo hồ sơ luồng, mở kỳ bắt buộc (người ghi nhận quyết định được phân công sau khi mở), danh sách quyền của Lãnh đạo Phòng có thêm `evaluation.unit.review`. `AuthzContractTests` — danh mục mã quyền mới. `EvaluationWorkflowUnitTests` — mục "Kiểm thử" ở trên.

## Câu hỏi nghiệp vụ cần xác nhận
1. Ba hồ sơ luồng dựng sẵn có đúng và đủ nhóm đối tượng của ATTECH không (Phó Giám đốc — IV.3c dòng 4b "Chủ tịch công ty con đề xuất"; Kiểm soát viên — dòng 5; kiêm nhiệm hai cấp — trích xuất mục 1.5)?
2. Diện BTV ĐUTCT: B2 "Chi bộ xác nhận" và B3a "tập thể lãnh đạo Công ty" làm trong hệ thống (như mặc định) hay cũng do cấp trên?
3. Ai ghi nhận kết quả thẩm định (Ban TCĐU), nhận xét (HĐTV) và quyết định (BTV ĐUTCT) của cấp trên — Văn phòng Đảng ủy (mặc định có `evaluation.external.record`) hay Phòng TCCB-LĐ?
4. Ví dụ 3: "Trưởng Phòng đề xuất mức" là bước riêng thay B3c (như cấu hình mặc định, quyền `evaluation.unit.review`) hay là một phần của B3a?
5. Mở kỳ khi còn cảnh báo kẹt luồng: cho phép "mở bắt buộc có lý do" (như hiện nay) hay chặn tuyệt đối?
6. Kết quả của cấp trên: mức đề xuất của Ban TCĐU/HĐTV có bắt buộc không, có cần điểm không (hiện: bắt buộc mức ở B3a/B3b/B3c/B4, điểm tùy chọn ở B3b/B4)?

## Phát hiện thêm
- 🟡 Kiểm tra kẹt luồng gọi `IPermissionResolver` cho từng ứng viên (tài khoản có vai trò chứa quyền cần) và từng chủ hồ sơ — có cache, đủ cho quy mô ATTECH (vài trăm người); nếu kỳ rất lớn nên thêm truy vấn phạm vi gộp ở repository.
- ⚪ Sửa ảnh chụp "cấp quyết định" không tự đổi hồ sơ luồng (tránh đổi luồng ngầm) — giao diện ghi chú "dùng Đổi hồ sơ luồng nếu cần". Cần nghiệp vụ xác nhận hành vi này.
- ⚪ Đổi hồ sơ luồng hàng loạt lưu một lần: nếu một hồ sơ bị người khác sửa đồng thời trong lúc lưu, cả lô báo 409 (hồ sơ sai phiên bản đã được lọc trước khi lưu).
- ⚪ `DataSeederAuthorizationTests` có lúc lỗi ở `CREATE DATABASE` khi chạy đồng thời với fixture khác trên máy chủ 159 (một lần/3 lần chạy) — đề xuất thử lại khi tạo CSDL test.
