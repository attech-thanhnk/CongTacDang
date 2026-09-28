# Báo cáo: Task 12 — Luồng đánh giá 5 bước theo cấu hình kỳ

- **Branch:** `feat/evaluation-workflow` (fast-forward tới `473665f` trước khi sửa file đầu tiên)
- **Commit cuối:** commit chứa báo cáo này (commit code cuối: `a1f082a`)
- **Ngày:** 2026-09-28

## Kết quả theo mã lỗi

| Mã | Trạng thái | Tóm tắt thay đổi | File chính |
|---|---|---|---|
| T-66 | done | 9 bước cố định (`WorkflowStep`), trạng thái hồ sơ = bước đang chờ, máy trạng thái thuần `RecordStateMachine` (hoàn thành → bước bật kế tiếp; trả lại chỉ ở B1_APPROVE/B2_CELL_CONFIRM/B3B_APPRAISAL, bắt buộc lý do; mở lại từ `Published`, không sớm hơn tự chấm, bắt buộc lý do). `EvaluationPeriod.Settings` (jsonb, schema version) + `PeriodSettings` (validate, 2 mẫu "Đầy đủ theo HD03"/"Quý III/2026 — chuyển tiếp"). `EvaluationWorkflowService` 13 hành động, mỗi hành động: nạp hồ sơ → `guard.Ensure` → so phiên bản → luật trạng thái kỳ + thời hạn → máy trạng thái → áp dữ liệu → lịch sử (bước, hành động, lý do, điểm/mức trước–sau) → `SaveChanges` với xmin. `PeriodService` (tạo từ mẫu, sửa cấu hình chỉ khi dự thảo — riêng thời hạn khi đã mở, người được đánh giá chọn tay/theo Phòng/Chi bộ/import, sửa ảnh chụp có lý do, `Draft→Open→Locked→Closed`, `Locked→Open` cần lý do). `GET records/{id}/actions`, `GET work-queue`. Biên bản có `DepartmentId` + `Stage` (thư ký tập thể phạm vi Phòng lập được biên bản — mục "Cần phối hợp" của task 09). Frontend: `/work-queue`, `/evaluations/[recordId]`, `/periods`, `/periods/[periodId]`, nút theo `actions`. | `Domain/Evaluation/*`, `Application/Services/EvaluationWorkflowService.cs`, `PeriodService.cs`, `WorkflowActions.cs`, `EvaluationMapping.cs`, `Api/Controllers/EvaluationController.cs`, `PeriodController.cs`, `Infrastructure/Repositories/EvaluationWorkflowRepository.cs`, `Data/Configurations/EvaluationConfigurations.cs`, `frontend/app/{work-queue,periods,evaluations}/**`, `components/evaluations/*` |
| B-01 | done | Không còn `PUT periods/{id}/status` nhận trạng thái bất kỳ: kỳ chuyển qua `open/lock/unlock/close` chỉ tiến (trừ `Locked→Open` có lý do; `close` chỉ khi mọi hồ sơ đã công bố); hồ sơ chỉ chuyển qua máy trạng thái; gọi sai thứ tự → 409 "Hồ sơ đang ở bước …, không thể …". | `PeriodService.cs`, `RecordStateMachine.cs` |
| T-67 | done | Hằng số gom vào `PeriodSettings.parameters` (`EvaluationParameters`) với giá trị **y hệt** code cũ (bảng dưới); công thức tách nguyên văn sang `EvaluationScoring` (thuần). Unit test so **từng bit** với bản sao code cũ trên 20 000 bộ dữ liệu ngẫu nhiên + toàn dải ngưỡng + trần 0–1000 + thông báo lỗi. | `Domain/Evaluation/EvaluationParameters.cs`, `EvaluationScoring.cs` |
| T-24 | done | Mọi hành động ghi của luồng (13 hành động), kỳ (sửa, chuyển trạng thái), người được đánh giá (bỏ, sửa ảnh chụp) **bắt buộc** `version` (thiếu → 400); khác phiên bản → 409; hai request đồng thời cùng phiên bản → đúng một thành công (xmin). Frontend gửi `version` ở mọi hành động ghi; 409 → hiện thông báo của máy chủ + nút "Tải lại hồ sơ". | `EvaluationWorkflowService.ExecuteAsync`, `PeriodService`, `frontend/services/evaluationService.ts`, `app/evaluations/[recordId]/page.tsx` |
| T-53 | done | Tên (và Id) tệp minh chứng của nhiệm vụ lấy theo **phiên bản hiện hành** của nhóm tệp (`GetCurrentEvidenceAsync`), dùng ở mọi DTO hồ sơ. | `EvaluationWorkflowRepository.cs`, `EvaluationMapping.ToTaskDto` |
| B-03 | done | Kết luận ở mục "Kết luận B-03". Kiểm phiếu chỉ lưu tổng hợp theo hồ sơ, gắn biên bản; validate không âm và tổng các mức + không hợp lệ ≤ số có mặt; audit của bảng kiểm phiếu chỉ ghi người nhập (test W1 kiểm tra `ActorId` = thư ký). | `EvaluationWorkflowService.AttachVotesAsync`, `CollectiveEvaluationService.CreateMeetingAsync` |
| B-07 (phần điền được) | partial | Điền Mẫu 10 "nhận xét của cấp trực tiếp sử dụng" (B3c), Mẫu 13 số phiếu không hợp lệ + số có mặt từ biên bản. Ô còn trống: bảng cuối báo cáo. | `Documents/Forms/Mau10Data.cs`, `Mau13Data.cs`, `ReportService.cs` |

B-07 `partial`: task file chỉ yêu cầu "điền được bằng trường mới thì điền"; các ô còn lại chưa có dữ liệu nguồn.

## Luồng W1–W10

Tất cả trong `backend/tests/CongTacDang.IntegrationTests/EvaluationWorkflowTests.cs`, chạy trên PostgreSQL thật (máy chủ 159, CSDL `ctd_it_*` tạm).

| # | Luồng | Trạng thái | Test |
|---|---|---|---|
| W1 | Tạo kỳ mẫu "Đầy đủ theo HD03" → thêm người (chọn tay + theo Phòng) → kỳ dự thảo chặn thao tác (409) → mở kỳ → một hồ sơ đi hết 9 bước, mỗi bước đúng người có quyền (kiểm `actions` của người làm và người không làm) → `Published`; lịch sử đủ 9 bước; thư ký phạm vi Phòng lập biên bản Phòng và ghi kiểm phiếu (vượt số có mặt → 400); B-03 audit | pass | `W1_FullPreset_ManualAndDepartmentParticipants_RecordWalksNineStepsToPublished` |
| W2 | Mẫu "Quý III/2026 — chuyển tiếp": hồ sơ bắt đầu ở tự chấm, B1 hiện `skipped`, gọi API đăng ký → 409; tự chấm 09B (thiếu trục / vượt tối đa → 400; tổng 6 trục → điểm nhiệm vụ) → `Published` | pass | `W2_TransitionPreset_SkipsB1_SelfScores09B_ToPublished` |
| W3 | Không có quyền (Chi ủy, người thường) → 403; có quyền ngoài phạm vi (Lãnh đạo Phòng 1 – hồ sơ Phòng 2) → 403; chủ hồ sơ tự duyệt/tự thẩm định → 403 "xung đột lợi ích"; `actions` rỗng với chủ hồ sơ; xem `actions` ngoài phạm vi → 403 | pass | `W3_NoPermissionOrOutOfScope_Is403_OwnerCannotApproveOwnRecord` |
| W4 | Trả lại ở B1_APPROVE, B2_CELL_CONFIRM, B3B_APPRAISAL (lý do rỗng → 400) → chủ hồ sơ sửa, nộp lại → đi tiếp; `returnReason` hiển thị rồi được xóa khi nộp lại; lịch sử có 3 lần trả lại kèm lý do | pass | `W4_ReturnWithReason_AtB1B2B3b_OwnerFixesAndResubmits` |
| W5 | Gọi thẳng bước sau → 409; thiếu `version` → 400; kỳ `Locked` → chủ hồ sơ 409, bước thẩm định vẫn chạy; `unlock` thiếu lý do → 400; mở kỳ đang mở → 409; đóng kỳ còn hồ sơ chưa công bố → 409; kỳ đã mở không đổi mẫu tự chấm → 409 | pass | `W5_OutOfOrder409_MissingVersion400_LockedPeriodBlocksOwner` |
| W6 | Hồ sơ `CapTren`: chỉ `evaluation.decide.external` ghi nhận được (người có `decide` → 403 nêu "cấp trên"); hồ sơ `CoSo` ngược lại; `actions` tương ứng | pass | `W6_CapTrenDecidedOnlyByExternal_CoSoOnlyByLocal` |
| W7 | `Published` → sửa bị chặn (409), chỉ còn `Reopen`; mở lại thiếu lý do → 400, về bước sớm hơn tự chấm → 409; mở lại về thẩm định → sửa → công bố lại; lịch sử có 2 lần công bố, lần mở lại có lý do và mức trước–sau | pass | `W7_Published_IsLocked_ReopenWithReason_EditAndRepublish_HistoryKeepsAll` |
| W8 | (a) người quản lý kỳ sửa ảnh chụp → người thẩm định dùng phiên bản cũ → 409 "…tải lại…"; (b) hai người thẩm định gửi đồng thời cùng phiên bản → đúng 1 thành công, 1 nhận 409. Frontend: 409 → thông báo + nút "Tải lại hồ sơ" (chỉ kiểm tra bằng `tsc`/`build`, không có test giao diện) | pass (backend) | `W8_TwoUsersSameRecord_SecondGets409` |
| W9 | `work-queue` mặc định chỉ chứa hồ sơ đang chờ chính người dùng, trong phạm vi: Lãnh đạo Phòng 1 chỉ thấy hồ sơ Phòng 1 ở B1_APPROVE; Chi ủy Chi bộ 1 chỉ thấy B2_CELL_CONFIRM; thẩm định/người thường/chủ hồ sơ đang chờ người khác → rỗng; chủ hồ sơ thấy hồ sơ của mình khi tới bước của mình (`isOwnRecord`) | pass | `W9_WorkQueue_ContainsOnlyRecordsWaitingForCurrentUser_InScope` |
| W10 | Import `period-participants` qua khung task 10: loại hiện ở `/api/imports/kinds`, mẫu tải được (người thiếu quyền → 403); tệp có dòng lỗi (tài khoản không có, trùng dòng) → không xác nhận được; tệp đúng → tạo 2 hồ sơ, ảnh chụp Phòng đúng; nhập lại → lỗi dòng "đã có" | pass | `W10_ImportParticipantsFromExcel` |

Unit test (`backend/tests/CongTacDang.UnitTests/EvaluationWorkflowUnitTests.cs`, 20 test): máy trạng thái **32 cấu hình bật/tắt × 10 trạng thái × 27 lệnh** (8 640 trường hợp) đối chiếu oracle viết độc lập; đi hết 9 bước; mẫu chuyển tiếp; thông báo sai thứ tự; bỏ qua bước tắt; `PeriodSettings` (2 mẫu hợp lệ, 4 bước bắt buộc không tắt được, B1_APPROVE thiếu B1_REGISTER, 09A thiếu B1, mẫu tự chấm sai, schema version, mã bước lạ, tham số sai, JSON khứ hồi/chuẩn hóa/rỗng, chỉ khác thời hạn); `EvaluationScoring` bằng đúng code cũ (tham số mặc định, 20 000 bộ ngẫu nhiên so từng bit, ngưỡng −1..101 bước 0,01, trần 0..1000, thông báo lỗi), TC-1/TC-2 để so sánh, 09B.

## Cổng kiểm tra

| Bước | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build backend/CongTacDang.slnx` | pass | 0 lỗi; 6 warning đều CS0618 (`[Obsolete]` sẵn có ở file ngoài phạm vi: `UserController`, `SecurityTests`…), không thêm warning mới |
| `dotnet test` — unit | pass | 190 test: **189 pass, 1 skip** (`PdfConversionTests.Mau01…` — thiếu LibreOffice, skip sẵn có), 0 fail |
| `dotnet test` — integration, có `CONGTACDANG_TEST_PG` (máy chủ 159) | pass | 57 test **đã chạy**: 57 pass, **0 skip**, 0 fail (10 mới của task 12) |
| `dotnet test` — integration, không có biến | pass (skip) | 8 pass (tên CSDL), **49 skip** — skip không tính là pass |
| `npx tsc --noEmit` | pass | |
| `npm run build` | pass | `next lint` trên `app/evaluations`, `app/work-queue`, `app/periods`, `app/collective-evaluations`, `components/evaluations`, `services`: không cảnh báo |
| Chạy app / thử giao diện thủ công | không chạy | Giao diện chỉ kiểm tra bằng `tsc` + `build` + lint; luồng API kiểm tra bằng test tích hợp |
| CSDL tạm | chỉ `ctd_it_*` | `ApiFactory` tạo/xóa; không đụng `congtacdang_test`; không tạo `ctd_it_ui12` (không chạy app) |

## Bảng endpoint mới (thay endpoint cũ)

Mọi phản hồi bọc `ApiResponse<T>`. "Đăng nhập" = `[Authorize]`, service kiểm tra trên đối tượng. Mọi hành động ghi nhận `version` (xmin đã đọc) — thiếu → 400, khác → 409.

| Method | Route | Policy controller | Kiểm tra trong service / ghi chú |
|---|---|---|---|
| GET | `/api/evaluations/periods` | Đăng nhập | Danh sách kỳ kèm `settings`, `totalRecords`, `isActive` (kỳ Open/Locked mới nhất) |
| GET | `/api/evaluations/periods/active` | Đăng nhập | Kỳ Open/Locked mới nhất hoặc `null` |
| GET | `/api/evaluations/periods/presets` | Đăng nhập | 2 mẫu cấu hình |
| GET | `/api/evaluations/periods/{id}` | Đăng nhập | |
| POST | `/api/evaluations/periods` | `period.manage` | `{ year, quarter, name, startDate, endDate, preset }` → kỳ `Draft` |
| PUT | `/api/evaluations/periods/{id}` | `period.manage` | `{ version, name?, startDate?, endDate?, settings? }`; Draft: mọi thứ (validate cấu hình, tính lại bước đầu của hồ sơ chưa thao tác); Open/Locked: chỉ thời hạn (khác → 409); Closed → 409 |
| POST | `/api/evaluations/periods/{id}/open` \| `lock` \| `unlock` \| `close` | `period.manage` | `{ version, reason? }`; chỉ tiến; `unlock` bắt buộc lý do; `close` chỉ khi mọi hồ sơ `Published` |
| GET | `/api/evaluations/periods/{id}/participants` | `period.manage` | Người được đánh giá (ảnh chụp, trạng thái, version) |
| GET | `/api/evaluations/periods/{id}/candidates?departmentId&partyCellId&q` | `period.manage` | Cán bộ đang hoạt động, đánh dấu người đã có |
| POST | `/api/evaluations/periods/{id}/participants` | `period.manage` | `{ memberIds[], departmentId?, partyCellId? }` → `{ added, skipped[] }`; kỳ Draft/Open |
| DELETE | `/api/evaluations/periods/{id}/participants/{recordId}?version=` | `period.manage` | Draft; hoặc Open mà hồ sơ chưa có dữ liệu (xóa mềm; thêm lại → khôi phục) |
| PUT | `/api/evaluations/periods/{id}/participants/{recordId}/snapshot` | `period.manage` | `{ version, departmentId, partyCellId, jobGroup, approvalAuthority, reason }` (lý do bắt buộc, ghi lịch sử `EditSnapshot`); kỳ Draft/Open |
| GET | `/api/evaluations/my-record?periodId` | Đăng nhập | Hồ sơ của mình (null nếu không có trong danh sách) |
| GET | `/api/evaluations/records/{id}` \| `/history` | Đăng nhập | `evaluation.read` trên hồ sơ (chủ hồ sơ luôn xem); DTO có `progress` (9 bước: done/current/pending/skipped + thời hạn), `currentStep`, `returnReason` |
| GET | `/api/evaluations/records/{id}/actions` | Đăng nhập | `evaluation.read` trên hồ sơ → `{ recordId, version, status, actions: [{ action, step, label, requiresReason, reasonOptional, overdue, targetSteps? }] }` (tính từ trạng thái hồ sơ + cấu hình kỳ + trạng thái kỳ + thời hạn + guard) |
| GET | `/api/evaluations/work-queue?periodId` | Đăng nhập | `{ total, groups: [{ step, stepName, count, items[] }] }`; mặc định mọi kỳ Open/Locked; lọc sơ bộ theo quyền rồi `guard.Can` trên từng hồ sơ |
| GET | `/api/evaluations/records`, `/branch-records`, `/branch-quotas` | `evaluation.read` | như trước (lọc `GetScope`); `branch-records` lọc theo Chi bộ ảnh chụp trên hồ sơ |
| POST | `/api/evaluations/records/{id}/tasks/submit` | `evaluation.self` | B1_REGISTER `{ version, tasks[] }` (số lượng/tổng trọng số theo tham số) |
| POST | `…/tasks/approve` \| `…/tasks/return` | `evaluation.tasks.approve` | B1_APPROVE `{ version, comment? }` \| `{ version, reason }` |
| POST | `…/self-score/submit` | `evaluation.self` | B2_SELF_SCORE `{ version, generalScores[6], taskScores[] (09A) \| axisScores[6] (09B), selfProposedGrade? }` |
| POST | `…/cell/confirm` \| `…/cell/return` | `evaluation.cell.confirm` | B2_CELL_CONFIRM `{ version, comment? }` \| `{ version, reason }` |
| POST | `…/collective` | `evaluation.collective.record` | B3A_COLLECTIVE `{ version, proposedGrade, comment?, meetingId?, votes? }` (có `votes` thì bắt buộc biên bản) |
| POST | `…/appraisal` \| `…/appraisal/return` | `evaluation.appraise` | B3B_APPRAISAL `{ version, appraisalScore?, comment, proposedGrade }` \| `{ version, reason }` |
| POST | `…/director-review` | `evaluation.director.review` | B3C_DIRECTOR `{ version, comment, proposedGrade }` |
| POST | `…/decision` | `evaluation.decide` \| `evaluation.decide.external` | B4_DECISION `{ version, finalGrade, finalScore?, documentNumber?, documentDate?, authorityName?, meetingId?, votes? }`; service chọn quyền theo `ApprovalAuthority` ảnh chụp |
| POST | `…/publish` | `evaluation.publish` | B5_PUBLISH `{ version }` |
| POST | `…/reopen` | `evaluation.reopen` | `{ version, reason, targetStep }` (kỳ Closed → tự về Locked, ghi lý do vào kỳ) |
| POST | `/api/evaluations/meetings` | `meeting.manage` | **Đổi**: nhận thêm `departmentId`, `stage` (`B3A_COLLECTIVE`/`B4_DECISION`); không bắt buộc Chi bộ (không có cả hai = cấp Công ty, chỉ phạm vi Global); M13 kiểm tổng phiếu ≤ số có mặt, mỗi hồ sơ một dòng |
| GET | `/api/evaluations/meetings`, `/{id}` | `meeting.read` \| `meeting.manage` | **Đổi**: phạm vi Phòng khớp `DepartmentId` của biên bản |

**Đã xóa:** `POST /tasks/register`, `POST /self-score`, `POST /branch-review`, `POST /branch-meeting-review`, `POST /appraisal`, `POST /approve-final`, `PUT /periods/{id}/status`, `PUT /periods/{id}/activate` (kỳ hiện hành tự suy từ trạng thái). Mọi lệnh gọi trong frontend đã đổi cùng task.

Import mới: loại `period-participants` — cột `Năm*`, `Quý*` (1–4), `Tên kỳ` (khi nhiều kỳ cùng quý), `Tên đăng nhập*`, `Ghi chú`; quyền `system.import` + `period.manage`; kỳ phải Draft/Open; lỗi dòng: không có kỳ / nhiều kỳ / kỳ đã khóa / tài khoản không có / bị khóa / đã có trong danh sách / trùng trong tệp.

## Bảng trường dữ liệu mới / đổi

| Bảng.trường | Loại | Ý nghĩa / ghi chú |
|---|---|---|
| `evaluation_periods.Status` | **đổi giá trị** | `Draft=0, Open=10, Locked=11, Closed=12` (cũ: 0–5) |
| `evaluation_periods.Settings` | mới, `jsonb NOT NULL` | Cấu hình kỳ (schema version 1); `'{}'`/rỗng = mẫu "Đầy đủ theo HD03" |
| `evaluation_periods.StatusReason`, `StatusChangedAt`, `StatusChangedBy` | mới | Lý do/thời điểm/người chuyển trạng thái kỳ gần nhất |
| `evaluation_periods.IsActive` | **xóa** (+ unique index lọc `IsActive`) | Kỳ hiện hành = kỳ Open/Locked mới nhất; DTO vẫn có `isActive` (tính) |
| `evaluation_records.Status` | **đổi giá trị** | `AwaitingRegistration=10 … AwaitingPublish=18, Published=19` (cũ: 0–7) |
| `evaluation_records.PartyCellComment` | **đổi nghĩa** | Ý kiến **xác nhận** của Chi bộ trên phiếu tự chấm (B2_CELL_CONFIRM); trước: nhận xét của Chi bộ tại hội nghị bỏ phiếu |
| `evaluation_records.PartyCellProposedGrade`, `VotesExcellent/Good/Satisfactory/Unsatisfactory`, `TotalVoters` | **dữ liệu cũ, chỉ đọc** | Luồng mới không ghi; kết quả kiểm phiếu nay lưu ở `evaluation_meeting_vote_summaries` (gắn biên bản). Báo cáo/biểu mẫu dùng làm giá trị dự phòng cho hồ sơ cũ |
| `evaluation_records.ReturnReason` | mới | Lý do trả lại gần nhất (xóa khi chủ hồ sơ nộp lại) |
| `…TasksApprovedById/ByName/At`, `TasksApprovalComment` | mới | B1_APPROVE |
| `…AxisScoreT1..T6`, `SelfScoreForm`, `SelfScoredAt` | mới | Tự chấm 09B (6 trục), mẫu đã dùng, thời điểm nộp |
| `…CellConfirmedById/ByName/At` | mới | B2_CELL_CONFIRM (người, thời điểm) |
| `…CollectiveProposedGrade` (int, mặc định 0), `CollectiveComment`, `CollectiveMeetingId`, `CollectiveRecordedById/ByName/At` | mới | B3A — **tách** khỏi `PartyCell*`/`Votes*`; `CollectiveMeetingId` là tham chiếu mềm (không FK, tránh vòng khóa ngoại) |
| `…AppraisedById/ByName/At` | mới | B3B (điểm/ý kiến/mức giữ trường cũ `Appraisal*`) |
| `…DirectorComment`, `DirectorProposedGrade` (int, mặc định 0), `DirectorReviewedById/ByName/At` | mới | B3C |
| `…DecisionDocumentNumber`, `DecisionDocumentDate`, `DecisionAuthorityName`, `DecisionMeetingId`, `DecisionRecordedById/ByName/At` | mới | B4 (mức/điểm giữ `FinalGrade`/`FinalScore`; `FinalScore` mặc định = điểm thẩm định, không có thì điểm tự chấm) |
| `…PublishedById/ByName/At` | mới | B5 |
| index `evaluation_records (PeriodId, Status)` | mới | work-queue |
| `evaluation_record_histories.Step` (int null), `Action` (int, mặc định 1 = Complete), `Reason`, `ScoreBefore/After` (double null), `GradeBefore/After` (int null) | mới | Lịch sử theo bước/hành động/lý do + ảnh chụp điểm/mức trước–sau |
| `evaluation_meetings.DepartmentId` (uuid null, FK → `administrative_departments`, SetNull), `Stage` (int null) | mới | Biên bản cấp Phòng; bước B3a/B4; index `(PeriodId, DepartmentId)` |
| `collective_evaluation_records.Status` | đổi kiểu enum | `CollectiveRecordStatus { Draft=0, Submitted=3 }` — cùng giá trị số, không cần chuyển dữ liệu |

## Tham số (`PeriodSettings.parameters`) — giá trị gốc

Dòng tính theo commit `473665f` (trước task 12).

| Tham số | Giá trị | Gốc |
|---|---|---|
| `minTasks` / `maxTasks` | 3 / 7 | `EvaluationService.cs:215` |
| `totalTaskWeight` / `taskWeightTolerance` | 70.0 / 0.05 (so sau `Math.Round(sum, 2)`) | `EvaluationService.cs:220–221` |
| `generalCriterionMaxScore` | 5.0 (6 tiêu chí T1–T6 là cấu trúc dữ liệu, không phải tham số) | `EvaluationService.cs:326, 331` |
| `jobGroupWeights` | Khung1 0.25/0.35/0.20/0.20; Khung2 0.15/0.50/0.15/0.20; Khung3 0.20/0.30/0.35/0.15; Khung4 0.15/0.30/0.20/0.35 | `EvaluationService.cs:669–672` |
| `fallbackWeights` | 0.25/0.25/0.25/0.25 | `EvaluationService.cs:673` |
| `excellentMinScore` / `goodMinScore` / `satisfactoryMinScore` | 90 / 70 / 50 | `EvaluationService.cs:680–682` |
| `excellentQuotaRatio` | 0.20, làm tròn xuống | `EvaluationService.cs:571`; `ReportService.cs:265, 314` |
| `collectiveGeneralMaxScore` / `collectiveTaskMaxScore` | 30.0 / 70.0 | `CollectiveEvaluationService.cs:95–96` |
| `axisMaxScores` | 15/10/10/15/10/10 | **mới** (code cũ không có 09B) — theo trích xuất HD03 mục 8.3, chờ nghiệp vụ xác nhận |

Không phải tham số (giữ nguyên công thức): làm tròn 2 chữ số ở điểm nhiệm vụ, tổng chung, tổng nhiệm vụ, tổng điểm; giới hạn tỷ lệ A–D trong 0..1; khởi tạo điểm nhiệm vụ = trọng số khi đăng ký. "Ngưỡng chênh lệch cần giải trình (5 điểm)" **không có trong code cũ** (chỉ là chú thích ở entity) nên không thêm tham số.

## Kết luận B-03

- **Không có dữ liệu định danh người bỏ phiếu**: hệ thống không tổ chức bỏ phiếu điện tử. Bảng `evaluation_meeting_vote_summaries` chỉ có `MeetingId`, `RecordId` (hồ sơ **được** đánh giá), số phiếu từng mức, phiếu không hợp lệ, ghi chú — không có cột người bỏ phiếu. Trường cũ `Votes*`/`TotalVoters` trên hồ sơ cũng chỉ là tổng hợp.
- **Audit**: `PrepareAuditEntries` ghi `ActorId` = người đăng nhập thực hiện request, tức **thư ký nhập kết quả**, và `NewValues` = số phiếu tổng hợp. Test W1 kiểm tra mọi bản ghi audit của dòng kiểm phiếu có `ActorId` = thư ký. `EvaluationMeeting.ChairId/SecretaryId` là người chủ trì/thư ký hội nghị (thông tin công khai của biên bản), không phải người bỏ phiếu.
- **Kiểm tra số liệu**: không âm; tổng các mức + không hợp lệ ≤ số có mặt của biên bản (B3a/B4 và tạo M13); mỗi hồ sơ một dòng trên một biên bản.
- → B-03 **đóng theo thiết kế** (luong-danh-gia.md mục 4).

## Ô biểu mẫu còn trống (B-07)

| Mẫu | Ô (field template) | Lý do |
|---|---|---|
| 01 | `SUPERVISOR_NAME` (người quản lý trực tiếp) | Chưa có dữ liệu người quản lý trực tiếp (người duyệt danh mục `TasksApprovedByName` không chắc là cùng người — chờ nghiệp vụ) |
| 01 | `T_CODE`, `T_AXIS`, `T_ROLE`, `T_EXCEED`, `T_EVIDENCE`, `T_SIGNER` | Danh mục sản phẩm chưa lưu mã SP, trục, vai trò chủ trì/phối hợp, tiêu chí vượt chuẩn, nguồn minh chứng, người ký |
| 10 | `GENERAL_APPRAISAL_SCORE`, `GENERAL_DIFF`, `TASKS_APPRAISAL_SCORE`, `TASKS_DIFF` | Thẩm định chỉ lưu tổng điểm, chưa lưu điểm theo nhóm |
| 13 | `INVALID_BALLOTS` khi các hồ sơ trên biên bản có số phiếu không hợp lệ khác nhau hoặc chưa có kết quả kiểm phiếu | Template chỉ có một ô cho cả biên bản |
| 09B (chưa có template) | Tóm tắt kết quả / minh chứng từng trục | Chỉ lưu điểm 6 trục |

## Thay đổi schema (cần migration `Wave5`)

Cấu hình entity đánh giá chuyển từ `OnModelCreating` sang `Infrastructure/Data/Configurations/EvaluationConfigurations.cs` (cùng bảng/index cũ, trừ các thay đổi dưới). Chi tiết cột ở "Bảng trường dữ liệu mới / đổi". Tóm tắt:

- `evaluation_periods`: thêm `Settings jsonb NOT NULL DEFAULT '{}'`, `StatusReason varchar(1000) NULL`, `StatusChangedAt timestamptz NULL`, `StatusChangedBy uuid NULL`; **xóa** `IsActive` và index unique lọc `"IsActive" = TRUE`.
- `evaluation_records`: thêm các cột B1/B2/B3a/B3b/B3c/B4/B5 và 09B như bảng trên (`CollectiveProposedGrade`, `DirectorProposedGrade` `integer NOT NULL DEFAULT 0`; còn lại NULL); độ dài: tên người 200, ý kiến 4000, lý do 2000, số văn bản 100, cơ quan 300, `SelfScoreForm` 10; index `(PeriodId, Status)`.
- `evaluation_record_histories`: thêm `Step integer NULL`, `Action integer NOT NULL DEFAULT 1`, `Reason varchar(2000) NULL`, `ScoreBefore/ScoreAfter double precision NULL`, `GradeBefore/GradeAfter integer NULL`.
- `evaluation_meetings`: thêm `DepartmentId uuid NULL` (FK `administrative_departments`, `ON DELETE SET NULL`), `Stage integer NULL`; index `(PeriodId, DepartmentId)`.

**Chuyển dữ liệu cũ** (chạy trong migration, sau khi thêm cột):
```sql
-- Hồ sơ: trạng thái cũ → bước đang chờ (mẫu "Đầy đủ theo HD03")
UPDATE evaluation_records SET "Status" = CASE "Status"
  WHEN 0 THEN 10  -- Draft          → AwaitingRegistration
  WHEN 1 THEN 11  -- TasksSubmitted → AwaitingTaskApproval
  WHEN 2 THEN 12  -- TasksApproved  → AwaitingSelfScore
  WHEN 3 THEN 13  -- SelfEvaluated  → AwaitingCellConfirm
  WHEN 4 THEN 15  -- Voted          → AwaitingAppraisal (hội nghị Chi bộ cũ ≈ đề xuất tập thể đã có)
  WHEN 5 THEN 16  -- Reviewed       → AwaitingDirectorReview
  WHEN 6 THEN 18  -- Approved       → AwaitingPublish
  WHEN 7 THEN 19  -- Published      → Published
  ELSE "Status" END
WHERE "Status" < 10;
-- Mức Chi bộ đề xuất cũ → đề xuất của tập thể lãnh đạo (trường mới B3a)
UPDATE evaluation_records SET "CollectiveProposedGrade" = "PartyCellProposedGrade"
WHERE "CollectiveProposedGrade" = 0 AND "PartyCellProposedGrade" <> 0;
-- Lịch sử: cùng bảng ánh xạ cho FromStatus/ToStatus (giữ Action = 1, Step = NULL)
UPDATE evaluation_record_histories SET
  "FromStatus" = CASE "FromStatus" WHEN 0 THEN 10 WHEN 1 THEN 11 WHEN 2 THEN 12 WHEN 3 THEN 13 WHEN 4 THEN 15 WHEN 5 THEN 16 WHEN 6 THEN 18 WHEN 7 THEN 19 ELSE "FromStatus" END,
  "ToStatus"   = CASE "ToStatus"   WHEN 0 THEN 10 WHEN 1 THEN 11 WHEN 2 THEN 12 WHEN 3 THEN 13 WHEN 4 THEN 15 WHEN 5 THEN 16 WHEN 6 THEN 18 WHEN 7 THEN 19 ELSE "ToStatus" END
WHERE "ToStatus" < 10 OR "FromStatus" < 10;
-- Kỳ: TaskRegistration/SelfEvaluation/BranchReview → Open; Appraisal → Locked; Completed → Closed (còn hồ sơ chưa công bố → Locked)
UPDATE evaluation_periods SET "Status" = CASE "Status" WHEN 1 THEN 10 WHEN 2 THEN 10 WHEN 3 THEN 10 WHEN 4 THEN 11 WHEN 5 THEN 12 ELSE "Status" END
WHERE "Status" BETWEEN 1 AND 5;
UPDATE evaluation_periods p SET "Status" = 11
WHERE "Status" = 12 AND EXISTS (SELECT 1 FROM evaluation_records r WHERE r."PeriodId" = p."Id" AND NOT r."IsDeleted" AND r."Status" <> 19);
```
Sau đó mới xóa cột `IsActive`. Kỳ cũ có `Settings = '{}'` được đọc là mẫu "Đầy đủ theo HD03" (09A) — người quản lý kỳ có thể chỉnh thời hạn; bật/tắt bước chỉ khi kỳ còn dự thảo.

## Key config mới
Không có. Thời hạn so theo ngày giờ Việt Nam (UTC+7, hằng số `EvaluationMapping.BusinessUtcOffset`).

## Thay đổi hành vi API / breaking change
- Endpoint hành động cũ và `PUT periods/{id}/status`, `PUT periods/{id}/activate` bị xóa (bảng trên). `evaluation.self` **không tự tạo hồ sơ** nữa: chỉ người trong danh sách được đánh giá của kỳ mới có hồ sơ.
- `version` bắt buộc với mọi hành động ghi của luồng/kỳ/danh sách người được đánh giá (400 nếu thiếu, 409 nếu khác).
- DTO hồ sơ đổi: `status` nhận giá trị mới (`Awaiting…`/`Published`), thêm `progress`, `currentStep`, `returnReason`, các trường B1–B5; `partyCellName/departmentName/partyCellId` lấy theo **ảnh chụp** trên hồ sơ (trước lấy theo hồ sơ cán bộ hiện tại). DTO kỳ: `status` mới, thêm `settings`, `statusReason`; `isActive` là giá trị tính.
- Lịch sử hồ sơ: thêm `step`, `stepName`, `action`, `actionName`, `reason`, điểm/mức trước–sau, tên trạng thái.
- 409 của luồng trả thông báo cụ thể ("Hồ sơ đang ở bước …", "Kỳ đã khóa dữ liệu…", "…đã được người khác cập nhật… tải lại"); frontend giữ nguyên thông báo máy chủ.
- `branch-quotas`, Mẫu 14/15/15A/15B/16: mức đề xuất trước thẩm định lấy từ đề xuất của tập thể lãnh đạo (hồ sơ cũ: mức Chi bộ cũ); Mẫu 14 đổi tiêu đề cột "Chi bộ đề xuất" → "Tập thể lãnh đạo đề xuất". Công thức trần giữ nguyên (tham số mặc định 20 %, làm tròn xuống).
- Seed dữ liệu mẫu (`SeedSampleData=true`): kỳ Q3/2026 `Open`, cấu hình đầy đủ, hồ sơ ở "Chờ cấp trực tiếp sử dụng" (trước: `Reviewed` kèm cả mức chính thức — không nhất quán); chỉ sửa khối seed đánh giá của `DataSeeder`.
- Frontend: `/evaluations` thành danh sách hồ sơ (hồ sơ của tôi + trong phạm vi + kiểm soát trần); thao tác ở `/evaluations/[recordId]`; trang chính luồng là `/work-queue`; cấu hình kỳ ở `/periods`. Bỏ `Step1..5*`, `EvaluationStepNav`, `EvaluationPeriodHeader`, `EvaluationHistoryModal`.

## Cần phối hợp
- **Người điều phối:** migration `Wave5` theo "Thay đổi schema" + SQL chuyển dữ liệu (chạy SQL trước khi xóa `IsActive`).
- **Task 11 (`AppSidebar.tsx`, trang tổng quan `app/page.tsx`):** thêm menu `/work-queue` (Việc cần xử lý — mọi người đã đăng nhập) và `/periods` (Kỳ đánh giá — mọi người xem; cấu hình cần `period.manage`). `app/page.tsx` (không thuộc phạm vi task 12) còn tự suy tiến trình từ các trường cũ (`partyCellComment`, `totalVoters`…) và trỏ tới `/evaluations?step=` — nên chuyển sang `record.progress`/`work-queue` (vẫn biên dịch được vì DTO giữ các trường cũ).
- **Test của task khác đã sửa** (task file yêu cầu xóa endpoint cũ): `AuthorizationMatrixTests` — kịch bản đặt kỳ `Open`, hồ sơ `AwaitingRegistration`; 4 test bước ghi đổi sang endpoint mới và tự đặt trạng thái cần có (kỳ vọng 200/403 giữ nguyên); phần `branch-meeting-review` (endpoint đã xóa) thay bằng kiểm tra trả lại cần lý do; test kỳ đổi `PUT …/status` → `POST …/lock` (vẫn 403).
- **`CLAUDE.md` mục Cấu trúc:** thêm `Domain/Evaluation`, `app/work-queue`, `app/periods` (không sửa trong task này).
- `frontend/services/auditService.ts` (không thuộc task 12) còn kiểu `EvaluationRecordHistoryDto` cũ — không còn nơi nào dùng cho hồ sơ; kiểu mới ở `evaluationService.ts`.

## Phát hiện thêm
- 🟡 **TC-1 (chỉ so sánh, không sửa công thức):** code làm tròn điểm từng sản phẩm 2 chữ số rồi cộng → SP-05 = 4,57/4,58, tổng nhiệm vụ ≈ 67,47; văn bản làm tròn 1 chữ số từng dòng → 67,6 (và "làm tròn 0,5" ở PDF tr.9 cho 97,5). Kết luận mức (HTXS) trùng. Cần nghiệp vụ chốt quy tắc làm tròn (B-04 đang `deferred`).
- 🟡 **TC-3 trần Xuất sắc:** code (giữ nguyên) = làm tròn **xuống** 20 % × số "Hoàn thành tốt trở lên" (gồm cả XS); trích xuất HD03 nêu 20 % × số HTT với ".5 lên 1" (8 HTT → 2; code → 1), mẫu 15A/15B/16 lại ghi "trong số xếp loại tốt trở lên". Cần nghiệp vụ xác nhận mẫu số và cách làm tròn (tham số `excellentQuotaRatio` đã có, chưa có tham số cách làm tròn).
- 🟡 **09B là cách chấm mới:** điểm nhiệm vụ = tổng 6 trục (tối đa 15/10/10/15/10/10 theo trích xuất, chưa xác nhận); mức gợi ý dùng cùng ngưỡng 90/70/50. Chưa lưu tóm tắt/minh chứng từng trục.
- 🟡 **Chênh lệch ≥ 5 điểm phải giải trình (Mẫu 10, PL II III.1):** code cũ chưa có, task 12 không thêm (không đổi nghiệp vụ). Đề xuất mã mới.
- ⚪ Mở lại hồ sơ khi kỳ **Đã đóng** tự chuyển kỳ về **Khóa dữ liệu** (ghi lý do vào kỳ) để các bước sau khi mở lại thao tác được — quyết định kỹ thuật, cần nghiệp vụ xác nhận (câu hỏi 5 của thiết kế).
- ⚪ Mở lại về bước tự chấm khi kỳ đang Khóa dữ liệu: chủ hồ sơ chỉ sửa được sau khi người quản lý kỳ mở lại kỳ (có lý do) — đúng luật kỳ, nhưng giao diện chỉ báo "không có thao tác".
- ⚪ Chưa có API sửa/xóa biên bản hội nghị; kết quả kiểm phiếu của một hồ sơ chỉ cập nhật qua hành động B3a/B4 (ghi lại dòng của hồ sơ trên biên bản).
- ⚪ `IEvaluationRepository` (cũ) còn được `ReportService`/`CollectiveEvaluationService` dùng song song `IEvaluationWorkflowRepository` — có thể gộp sau.
- ⚪ Trang `/evaluations` chưa đọc tham số `?periodId=` từ liên kết của trang tổng quan (luôn chọn kỳ hiện hành).
