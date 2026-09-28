# Task 12 — Luồng đánh giá 5 bước theo cấu hình kỳ (Đợt 5)

- **Agent:** E · **Branch:** `feat/evaluation-workflow` (tạo từ `main` **sau khi** task 08, 09, 10 đã merge)
- **Mã:** B-01, B-03, T-24, T-53, T-66, T-67
- **Báo cáo:** `docs/remediation/reports/12-evaluation-workflow.md`
- **Đọc trước:** `CLAUDE.md`, `RULES.md` (mục 8), **toàn bộ** `docs/thiet-ke/luong-danh-gia.md`, `docs/thiet-ke/phan-quyen.md` (mục 4), `docs/nghiep-vu/hd03-trich-xuat.md` mục 6, báo cáo task 09 (guard) và 10 (khung import).

## Nguyên tắc
- **Không đổi công thức tính điểm, ngưỡng xếp loại, trần tỷ lệ** — chỉ gom hằng số vào `parameters` với giá trị mặc định **y hệt** hiện tại, có test chứng minh kết quả tính không đổi.
- Mọi kiểm tra quyền qua `IAuthorizationGuard` (task 09). Không đọc vai trò.
- Frontend không tự suy luật: hiển thị theo API `actions` và `work-queue`.

## Luồng phải chạy đúng (mỗi luồng = ≥ 1 test tích hợp)

| # | Luồng |
|---|---|
| W1 | Tạo kỳ từ mẫu "Đầy đủ theo HD03" → thêm người được đánh giá (chọn tay / theo Phòng) → mở kỳ → một hồ sơ đi hết 9 bước với đúng người có quyền ở mỗi bước → `Published` |
| W2 | Kỳ mẫu "Quý III/2026 — chuyển tiếp": bước B1 bị bỏ qua, tự chấm theo 09B, đi tới `Published` |
| W3 | Người **không** có quyền/phạm vi ở một bước → 403; chủ hồ sơ không tự duyệt/thẩm định hồ sơ của mình → 403 (xung đột lợi ích) |
| W4 | Trả lại ở B1_APPROVE / B2_CELL_CONFIRM / B3B_APPRAISAL (bắt buộc lý do) → chủ hồ sơ sửa → nộp lại → đi tiếp |
| W5 | Chuyển trạng thái sai thứ tự (gọi thẳng API bước sau) → 409; kỳ `Locked` → chủ hồ sơ không sửa được |
| W6 | Hồ sơ `CapTren`: B4 chỉ người có `evaluation.decide.external` ghi nhận được; hồ sơ `CoSo` ngược lại |
| W7 | `Published` → sửa bị chặn → `Reopen` có lý do → sửa → công bố lại; lịch sử thể hiện đủ các lần |
| W8 | Hai người cùng thao tác một hồ sơ → người sau nhận 409 (xmin); frontend hiện thông báo tải lại |
| W9 | `work-queue` của từng vai trò mặc định chỉ chứa hồ sơ đang chờ họ, trong phạm vi |
| W10 | Import danh sách người được đánh giá bằng Excel (khung task 10) |

## Việc cần làm

### 1. Domain
- Enum mã bước, enum trạng thái hồ sơ mới, enum trạng thái kỳ mới (thiết kế mục 1–3). Máy trạng thái thuần trong `Domain/Evaluation/RecordStateMachine.cs`: input (trạng thái, cấu hình bước bật, hành động) → trạng thái mới hoặc lỗi.
- `EvaluationPeriod.Settings` (`jsonb`) + lớp `PeriodSettings` có validate (bước bắt buộc không tắt được, schema version); hai preset.
- Danh sách người được đánh giá (`period_participants` hoặc tạo hồ sơ khi thêm người — chọn và ghi lý do); ảnh chụp `DepartmentId`, `PartyCellId`, `JobGroup`, `ApprovalAuthority`.
- Trường mới trên `EvaluationRecord` cho các bước chưa có dữ liệu: nhận xét/mức đề xuất của tập thể lãnh đạo (B3a — tách khỏi dữ liệu Chi bộ hiện tại `PartyCellComment/Votes*`), xác nhận Chi bộ (người, thời điểm, ý kiến), nhận xét/mức đề xuất của cấp trực tiếp sử dụng (B3c), quyết định (mức, số/ngày văn bản, cơ quan quyết định), công bố (người, thời điểm). Xác định rõ trường cũ nào được đổi nghĩa / di chuyển, ghi bảng trong báo cáo.
- `EvaluationRecordHistory`: thêm `Step`, `Action`, `Reason`.

### 2. Application
- `EvaluationWorkflowService` (mới) — mỗi hành động một phương thức: `SubmitTasks`, `ApproveTasks`, `ReturnTasks`, `SubmitSelfScore`, `ConfirmByCell`, `ReturnByCell`, `RecordCollectiveProposal`, `Appraise`, `ReturnByAppraiser`, `DirectorReview`, `RecordDecision`, `Publish`, `Reopen`. Mỗi phương thức: nạp hồ sơ → `guard.Ensure` → kiểm trạng thái kỳ + máy trạng thái → áp dữ liệu → ghi lịch sử → `SaveChanges` với `version`.
- Tách phần tính điểm hiện có trong `EvaluationService` ra `EvaluationScoring` (thuần, nhận `parameters`), **không đổi kết quả**. Liệt kê mọi tham số (tên, giá trị, file:dòng gốc).
- Kỳ: `PeriodService` — tạo từ preset, sửa cấu hình (chỉ `Draft`, riêng thời hạn sửa được khi `Open`), quản lý người được đánh giá, chuyển trạng thái kỳ (`Locked → Open` cần lý do). Quyền `period.manage`.
- Biên bản hội nghị / kết quả kiểm phiếu (B3a, B4): chỉ lưu tổng hợp (thiết kế mục 4), validate số phiếu. Rà `CollectiveEvaluationService` và audit để chắc chắn không có dữ liệu định danh người bỏ phiếu (B-03) — ghi kết luận trong báo cáo.
- `GET /records/{id}/actions`, `GET /work-queue` (thiết kế mục 5).
- Import người được đánh giá: thêm loại import `period-participants` theo hướng dẫn trong báo cáo task 10.
- T-53: tên tệp minh chứng hiển thị theo phiên bản hiện hành.
- Endpoint cũ (`/tasks/register`, `/self-score`, `/branch-review`, `/branch-meeting-review`, `/appraisal`, `/approve-final`, `/periods/{id}/status`) → thay bằng endpoint mới theo hành động; xóa endpoint cũ (không cần tương thích ngược — frontend đổi cùng task).

### 3. Báo cáo / biểu mẫu
- `ReportService`, `DocxTemplateEngine`, `Infrastructure/Documents/**`: chỉ sửa chỗ đọc trạng thái/trường đã đổi tên để biểu mẫu vẫn ra đúng số liệu; B-07 (ô trống) điền được bằng trường mới thì điền, ghi lại những ô còn trống.

### 4. Frontend
- `app/evaluations/**`, `components/evaluations/**`, `services/evaluationService.ts`, `app/collective-evaluations/**`: trang "Việc cần xử lý" (work-queue) là trang chính; trang hồ sơ hiển thị tiến trình 9 bước (bước tắt hiện mờ "Không áp dụng trong kỳ"), nút theo `actions`; hộp nhập lý do khi trả lại/mở lại; gửi `version` ở mọi hành động ghi; 409 → thông báo + tải lại (T-24).
- Route cố định (task 11 tạo mục menu trỏ tới): **`/work-queue`** (Việc cần xử lý), **`/periods`** (Kỳ đánh giá), hồ sơ `/evaluations/[recordId]`.
- Trang cấu hình kỳ (`app/periods/**`): chọn preset, bật/tắt bước, thời hạn, danh sách người được đánh giá (thêm tay / theo Phòng / import).
- Không có chuỗi mã vai trò; chỉ `hasPermission` cho menu, còn nút trong hồ sơ theo `actions`.

## Phạm vi file (RULES mục 8.2)
Chủ sở hữu: entity `EvaluationPeriod`, `EvaluationRecord`, `EvaluationTask`, `EvaluationRecordHistory`, `EvaluationMeeting*`, `CollectiveEvaluationRecord`, enum trong `DomainEnums.cs` liên quan đánh giá, `Domain/Evaluation/**`, `EvaluationService`, `CollectiveEvaluationService`, `EvaluationWorkflowService`, `PeriodService`, `EvaluationController`, `CollectiveEvaluationController`, `ReportService`, `Infrastructure/Documents/**`, `DocxTemplateEngine`; frontend như mục 4 (gồm `app/work-queue/**`, `app/periods/**`). **Không** sửa `AppSidebar.tsx` (task 11 thêm menu theo route cố định ở mục 4); import người được đánh giá chỉ thêm định nghĩa backend, không sửa `app/imports/**`.
Seed dữ liệu mẫu đánh giá trong `DataSeeder` phải theo trạng thái mới → được sửa **phần seed dữ liệu đánh giá** của `DataSeeder` (task 09 đã xong, không còn song song).

## Test
- Unit: máy trạng thái (mọi cặp trạng thái × hành động × cấu hình bật/tắt); `PeriodSettings` validate; `EvaluationScoring` cho kết quả **bằng đúng** trước khi tách (dùng dữ liệu các test cũ + TC-1/TC-2 trong trích xuất HD03 **chỉ để so sánh**, không sửa công thức nếu lệch — ghi lệch vào "Phát hiện thêm").
- Tích hợp: W1–W10.

## Tiêu chí hoàn thành
- W1–W10 pass trên PostgreSQL thật; `npm run build` pass.
- Báo cáo: bảng endpoint mới; bảng trường dữ liệu mới/đổi; danh sách tham số với giá trị gốc; kết luận B-03; danh sách ô biểu mẫu còn trống (B-07).
