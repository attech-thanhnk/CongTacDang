# Thiết kế luồng đánh giá 5 bước và cấu hình kỳ

> **Trạng thái:** thiết kế kỹ thuật để triển khai (task 12). Thứ tự bước và tác nhân theo bản trích xuất HD03 (`docs/nghiep-vu/hd03-trich-xuat.md`, mục 6) — **chưa xác nhận**; vì vậy bước nào bật/tắt, ai làm, tham số bao nhiêu đều là **cấu hình**, không cứng trong code.
> **Không thay đổi công thức tính điểm và quy tắc xếp loại hiện có** (B-04 vẫn `deferred`).

## 1. Danh mục bước (mã cố định trong code)

| Mã bước | Nội dung | Người thực hiện (quyền) | Mẫu | Bắt buộc |
|---|---|---|---|---|
| `B1_REGISTER` | Cá nhân đăng ký sản phẩm/nhiệm vụ | chủ hồ sơ (`evaluation.self`) | 01 | tắt được |
| `B1_APPROVE` | Duyệt / trả lại danh mục sản phẩm | `evaluation.tasks.approve` | 01 | tắt được |
| `B2_SELF_SCORE` | Tự chấm điểm, đề xuất mức | chủ hồ sơ (`evaluation.self`) | 02, 09A/09B, 09C, 9D | **luôn bật** |
| `B2_CELL_CONFIRM` | Chi bộ xác nhận / trả lại phiếu tự chấm | `evaluation.cell.confirm` | 09x "Xác nhận của Chi bộ" | tắt được |
| `B3A_COLLECTIVE` | Ghi nhận đề xuất của tập thể lãnh đạo (kết quả phiếu kín) | `evaluation.collective.record` | 11, 12, 13 | tắt được |
| `B3B_APPRAISAL` | Thẩm định, đề xuất mức | `evaluation.appraise` | 10, 03, 19, 20 | **luôn bật** |
| `B3C_DIRECTOR` | Nhận xét, đề xuất của cấp trực tiếp sử dụng | `evaluation.director.review` | 10 | tắt được |
| `B4_DECISION` | Ghi nhận quyết định mức xếp loại | `evaluation.decide` / `evaluation.decide.external` (theo `ApprovalAuthority`) | 12, 13, 14, 15A/15B | **luôn bật** |
| `B5_PUBLISH` | Công bố, khóa hồ sơ | `evaluation.publish` | 16 | **luôn bật** |

Thứ tự cố định: `B1_REGISTER → B1_APPROVE → B2_SELF_SCORE → B2_CELL_CONFIRM → B3A_COLLECTIVE → B3B_APPRAISAL → B3C_DIRECTOR → B4_DECISION → B5_PUBLISH`. Bước bị tắt trong kỳ được bỏ qua khi tính bước kế tiếp.

## 2. Trạng thái hồ sơ cá nhân

Trạng thái = **bước đang chờ** (thay enum `RecordStatus` hiện tại):

| Trạng thái | Nghĩa |
|---|---|
| `AwaitingRegistration` | Chờ cá nhân đăng ký sản phẩm |
| `AwaitingTaskApproval` | Chờ duyệt danh mục |
| `AwaitingSelfScore` | Chờ tự chấm |
| `AwaitingCellConfirm` | Chờ Chi bộ xác nhận |
| `AwaitingCollective` | Chờ ghi nhận đề xuất tập thể |
| `AwaitingAppraisal` | Chờ thẩm định |
| `AwaitingDirectorReview` | Chờ cấp trực tiếp sử dụng |
| `AwaitingDecision` | Chờ quyết định |
| `AwaitingPublish` | Chờ công bố |
| `Published` | Đã công bố — **khóa**, mọi sửa đổi phải qua `Reopen` |

Chuyển trạng thái (máy trạng thái trong `Domain`, thuần, có unit test):
- **Hoàn thành bước** → trạng thái của bước bật kế tiếp.
- **Trả lại** (chỉ từ `B1_APPROVE`, `B2_CELL_CONFIRM`, `B3B_APPRAISAL`) → về bước của chủ hồ sơ tương ứng (`AwaitingRegistration` hoặc `AwaitingSelfScore`); **bắt buộc lý do**.
- **Mở lại** (`evaluation.reopen`, từ `Published`) → về bước người mở lại chọn (không sớm hơn `AwaitingSelfScore`); **bắt buộc lý do**; giữ dữ liệu các bước sau để đối chiếu (ghi lịch sử).
- Mọi chuyển trạng thái ghi `EvaluationRecordHistory`: người làm, thời điểm, bước, hành động (`complete/return/reopen`), lý do, ảnh chụp điểm/mức trước–sau.
- Chuyển không hợp lệ → 409 với thông báo "Hồ sơ đang ở bước …, không thể …".
- Mọi hành động dùng `version` (xmin) — thiếu `version` với hành động ghi → 400 (đóng T-24).

## 3. Kỳ đánh giá

### 3.1 Trạng thái kỳ (thay `PeriodStatus` hiện tại)
`Draft → Open → Locked → Closed`
- `Draft`: cấu hình bước, tham số, danh sách người được đánh giá. Chưa ai thao tác hồ sơ.
- `Open`: các bước chạy theo trạng thái từng hồ sơ.
- `Locked` ("điểm chốt dữ liệu", HD03 II.2): chỉ các bước từ `B3B_APPRAISAL` trở đi được thao tác; chủ hồ sơ không sửa được.
- `Closed`: toàn bộ hồ sơ đã `Published`; chỉ `Reopen`.
- Chuyển kỳ chỉ tiến, trừ `Locked → Open` (có lý do). Cấu hình bước **không sửa được khi kỳ đã `Open`**, trừ thời hạn.
- Nhiều kỳ có thể tồn tại; "kỳ đang hoạt động" = kỳ `Open`/`Locked` mới nhất (giữ unique index hiện có nếu phù hợp).

### 3.2 Cấu hình theo kỳ (`EvaluationPeriod.Settings`, cột `jsonb`, có schema version)
```json
{
  "schemaVersion": 1,
  "steps": {
    "B1_REGISTER":    { "enabled": true,  "deadline": "2026-10-05" },
    "B1_APPROVE":     { "enabled": true,  "deadline": "2026-10-07" },
    "B2_SELF_SCORE":  { "enabled": true,  "deadline": "2026-12-11" },
    "B2_CELL_CONFIRM":{ "enabled": true,  "deadline": null },
    "B3A_COLLECTIVE": { "enabled": true,  "deadline": "2026-12-11" },
    "B3B_APPRAISAL":  { "enabled": true,  "deadline": "2026-12-13" },
    "B3C_DIRECTOR":   { "enabled": true,  "deadline": "2026-12-14" },
    "B4_DECISION":    { "enabled": true,  "deadline": "2026-12-14" },
    "B5_PUBLISH":     { "enabled": true,  "deadline": null }
  },
  "enforceDeadlines": false,
  "selfScoreForm": "09B",
  "parameters": { }
}
```
- `deadline`: hiển thị và cảnh báo; chỉ chặn khi `enforceDeadlines = true`.
- `selfScoreForm`: `09A` (có Mẫu 01/02) hoặc `09B` (chấm trực tiếp 6 trục, Q3/2026) — quyết định giao diện và biểu mẫu tự chấm.
- `parameters`: mọi hằng số nghiệp vụ đang nằm rải rác trong `EvaluationService`/`CollectiveEvaluationService` (số sản phẩm tối thiểu/tối đa, tổng trọng số, ngưỡng điểm từng mức, trần % Xuất sắc, ngưỡng chênh lệch cần giải trình…) được gom vào đây. **Giá trị mặc định = đúng giá trị code đang dùng**; task 12 liệt kê từng tham số (tên, giá trị, file:dòng gốc) trong báo cáo.
- Hai mẫu cấu hình dựng sẵn khi tạo kỳ: **"Đầy đủ theo HD03"** (bật tất cả, `09A`) và **"Quý III/2026 — chuyển tiếp"** (tắt `B1_REGISTER`, `B1_APPROVE`; `09B`).

### 3.3 Danh sách người được đánh giá
- Bảng `period_participants` (hoặc tạo `EvaluationRecord` ngay khi thêm người): kỳ + người. Thêm người = tạo hồ sơ trạng thái đầu tiên, **ảnh chụp** `DepartmentId`, `PartyCellId`, `JobGroup`, `ApprovalAuthority` từ hồ sơ cán bộ tại thời điểm thêm; `period.manage` sửa được ảnh chụp khi kỳ còn `Draft`/`Open` (có lý do, ghi lịch sử).
- Chỉ người trong danh sách mới có hồ sơ; `evaluation.self` không tự tạo hồ sơ nữa.
- Thêm theo: chọn tay, theo Phòng/Chi bộ, hoặc **import Excel** (dùng khung import của task 10).

## 4. Bỏ phiếu kín (B3a, B4)
- Hệ thống **không** tổ chức bỏ phiếu điện tử và **không lưu phiếu của từng người**. Chỉ ghi **kết quả kiểm phiếu** (số phiếu mỗi mức, phiếu không hợp lệ, số triệu tập/có mặt) do thư ký nhập, gắn với biên bản hội nghị (Mẫu 12/13).
- Kiểm tra: tổng phiếu các mức + không hợp lệ ≤ số có mặt; không âm.
- Audit log của các bảng kết quả kiểm phiếu chỉ ghi người nhập (thư ký) — không có thông tin người bỏ phiếu → đóng B-03 theo thiết kế.

## 5. API hành động (để frontend không tự suy luật)
- `GET /api/evaluations/records/{id}/actions` → danh sách hành động người hiện tại được làm trên hồ sơ, mỗi mục: `{ action, step, requiresReason, reasonOptional }`. Tính từ: trạng thái hồ sơ + cấu hình kỳ + trạng thái kỳ + `IAuthorizationGuard`.
- `GET /api/evaluations/work-queue?periodId=` → hồ sơ đang chờ **người hiện tại** xử lý, nhóm theo bước (lọc bằng `GetScope`).
- Frontend hiển thị bước/nút **chỉ** theo hai API trên.

## 6. Câu hỏi cần nghiệp vụ xác nhận (không chặn việc code)
1. B3a: có cần ghi nhận trên hệ thống không, khi Q3/2026 "chưa áp dụng Mẫu 11" nhưng IV.3a vẫn yêu cầu phiếu kín (trích xuất mục 10)?
2. B3c có áp dụng cho mọi đối tượng không (PL III Ví dụ 3 không có bước này)? Nếu không → cần cấu hình bật/tắt theo nhóm đối tượng (hiện thiết kế chỉ theo kỳ).
3. Ai nhập kết quả quyết định của BTV Đảng ủy Tổng công ty cho nhóm `CapTren`?
4. Có chặn cứng theo thời hạn không (`enforceDeadlines`)?
5. Kiến nghị/khiếu nại sau công bố: chỉ cần `Reopen` có lý do, hay cần luồng riêng?
