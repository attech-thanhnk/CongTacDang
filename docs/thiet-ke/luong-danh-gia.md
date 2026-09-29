# Thiết kế luồng đánh giá 5 bước và cấu hình kỳ

> **Trạng thái:** thiết kế kỹ thuật để triển khai (task 12; luồng theo nhóm đối tượng — task 15). Thứ tự bước và tác nhân theo bản trích xuất HD03 (`docs/nghiep-vu/hd03-trich-xuat.md`, mục 1.4, 1.6, 6, Phụ lục III) — **chưa xác nhận**; vì vậy bước nào áp dụng cho nhóm đối tượng nào, do ai (trong hệ thống hay cấp trên) thực hiện, tham số bao nhiêu đều là **cấu hình**, không cứng trong code.
> **Không thay đổi công thức tính điểm và quy tắc xếp loại hiện có** (B-04 vẫn `deferred`).

## 1. Danh mục bước (mã cố định trong code)

| Mã bước | Nội dung | Quyền thực hiện mặc định (khi nội bộ) | Mẫu | Ràng buộc chế độ |
|---|---|---|---|---|
| `B1_REGISTER` | Cá nhân đăng ký sản phẩm/nhiệm vụ | chủ hồ sơ (`evaluation.self`, cố định) | 01 | Nội bộ / Không áp dụng |
| `B1_APPROVE` | Duyệt / trả lại danh mục sản phẩm | `evaluation.tasks.approve` | 01 | mọi chế độ (cần B1_REGISTER áp dụng) |
| `B2_SELF_SCORE` | Tự chấm điểm, đề xuất mức | chủ hồ sơ (`evaluation.self`, cố định) | 02, 09A/09B, 09C, 9D | **chỉ Nội bộ** |
| `B2_CELL_CONFIRM` | Chi bộ xác nhận / trả lại phiếu tự chấm | `evaluation.cell.confirm` | 09x "Xác nhận của Chi bộ" | mọi chế độ |
| `B3A_COLLECTIVE` | Ghi nhận đề xuất của tập thể lãnh đạo (kết quả phiếu kín) | `evaluation.collective.record` | 11, 12, 13 | mọi chế độ |
| `B3B_APPRAISAL` | Thẩm định, đề xuất mức | `evaluation.appraise` | 10, 03, 19, 20 | mọi chế độ |
| `B3C_DIRECTOR` | Nhận xét, đề xuất của cấp trực tiếp sử dụng (hoặc lãnh đạo đơn vị — ví dụ 3) | `evaluation.director.review` | 10 | mọi chế độ |
| `B4_DECISION` | Ghi nhận quyết định mức xếp loại | `evaluation.decide` | 12, 13, 14, 15A/15B | Nội bộ / Cấp trên (**không** "Không áp dụng") |
| `B5_PUBLISH` | Công bố, khóa hồ sơ | `evaluation.publish` | 16 | **chỉ Nội bộ** |

Thứ tự cố định: `B1_REGISTER → B1_APPROVE → B2_SELF_SCORE → B2_CELL_CONFIRM → B3A_COLLECTIVE → B3B_APPRAISAL → B3C_DIRECTOR → B4_DECISION → B5_PUBLISH`.

### 1.1 Chế độ bước và hồ sơ luồng (task 15)

Mỗi bước trong một **hồ sơ luồng** (nhóm đối tượng) có một chế độ:

| Chế độ | Ý nghĩa | Ai thao tác |
|---|---|---|
| `Internal` (Nội bộ) | Bước làm trong hệ thống | người có **quyền thực hiện** cấu hình cho bước (mã trong `PermissionCodes`, module `evaluation`, trừ `self`/`read`/`reopen`); bước của chủ hồ sơ luôn là `evaluation.self` |
| `External` (Cấp trên thực hiện) | Bước do cấp trên / cơ quan ngoài hệ thống làm | người có `evaluation.external.record` **ghi nhận kết quả**: cơ quan, số/ngày văn bản, nhận xét, mức đề xuất/quyết định (bắt buộc với B3a/B3b/B3c/B4), điểm, tệp đính kèm (tùy chọn). Không trả lại được |
| `Off` (Không áp dụng) | Bước không áp dụng cho nhóm này | — (bỏ qua khi tính bước kế tiếp) |

- Bước bắt buộc (không `Off`): `B2_SELF_SCORE`, `B4_DECISION`, `B5_PUBLISH`. Không `External`: `B1_REGISTER`, `B2_SELF_SCORE`, `B5_PUBLISH`.
- Mỗi hồ sơ đánh giá lưu `WorkflowProfileCode` (ảnh chụp khi thêm vào kỳ; mặc định theo `ApprovalAuthority`). Máy trạng thái, API `actions`, `work-queue`, kiểm tra quyền dùng **cấu hình bước của hồ sơ luồng của hồ sơ**; không còn cấu hình bước cấp kỳ.
- Kết quả ghi nhận của bước cấp trên lưu ở `evaluation_external_results` (mỗi hồ sơ × bước một dòng; ghi lại sau khi mở lại hồ sơ thì cập nhật dòng cũ) và được chép vào các trường của bước trên hồ sơ (mức/nhận xét/điểm; "người thực hiện" = tên cơ quan cấp trên) để báo cáo, biểu mẫu dùng chung một nguồn.
- **Xung đột lợi ích**: mọi bước không phải của chủ hồ sơ (kể cả khi quyền thực hiện được cấu hình khác mặc định, và ghi nhận kết quả của cấp trên) không áp dụng trên hồ sơ của chính mình — kiểm tra trong `EvaluationWorkflowService` trước `IAuthorizationGuard`.

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
- **Hoàn thành bước** (làm trong hệ thống, hoặc ghi nhận kết quả của cấp trên) → trạng thái của bước áp dụng kế tiếp theo hồ sơ luồng của hồ sơ.
- **Trả lại** (chỉ từ `B1_APPROVE`, `B2_CELL_CONFIRM`, `B3B_APPRAISAL` khi bước ở chế độ Nội bộ) → về bước của chủ hồ sơ tương ứng (`AwaitingRegistration` hoặc `AwaitingSelfScore`); **bắt buộc lý do**.
- **Đổi hồ sơ luồng** (`period.manage`, bắt buộc lý do, ghi lịch sử `ChangeProfile`): chỉ khi kỳ chưa đóng và hồ sơ **chưa qua** bước nào mà hai hồ sơ luồng khác nhau (chế độ hoặc quyền thực hiện); bước đang chờ không còn áp dụng → chuyển tới bước áp dụng kế tiếp.
- Hành động không khớp chế độ bước (gọi API bước nội bộ khi bước do cấp trên thực hiện, ghi nhận kết quả khi bước nội bộ, bước không áp dụng) → 409.
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

### 3.2 Cấu hình theo kỳ (`EvaluationPeriod.Settings`, cột `jsonb`, schema version 2)
```json
{
  "schemaVersion": 2,
  "profiles": [
    {
      "code": "co-so",
      "name": "Diện Đảng ủy cơ sở",
      "description": "PL III ví dụ 1 …",
      "steps": {
        "B1_REGISTER":     { "mode": "Internal", "permission": null, "deadline": "2026-10-05" },
        "B1_APPROVE":      { "mode": "Internal", "permission": "evaluation.tasks.approve", "deadline": null },
        "B2_SELF_SCORE":   { "mode": "Internal", "permission": null, "deadline": "2026-12-11" },
        "B2_CELL_CONFIRM": { "mode": "Internal", "permission": "evaluation.cell.confirm", "deadline": null },
        "B3A_COLLECTIVE":  { "mode": "Internal", "permission": "evaluation.collective.record", "deadline": "2026-12-11" },
        "B3B_APPRAISAL":   { "mode": "Internal", "permission": "evaluation.appraise", "deadline": "2026-12-13" },
        "B3C_DIRECTOR":    { "mode": "Internal", "permission": "evaluation.director.review", "deadline": "2026-12-14" },
        "B4_DECISION":     { "mode": "Internal", "permission": "evaluation.decide", "deadline": "2026-12-14" },
        "B5_PUBLISH":      { "mode": "Internal", "permission": "evaluation.publish", "deadline": null }
      }
    },
    { "code": "cap-tren", "name": "Diện BTV Đảng ủy Tổng công ty", "steps": { "B3B_APPRAISAL": { "mode": "External" }, "…": "…" } }
  ],
  "defaultProfiles": { "CoSo": "co-so", "CapTren": "cap-tren" },
  "enforceDeadlines": false,
  "selfScoreForm": "09B",
  "parameters": { }
}
```
- `profiles[].steps[].mode`: `Internal` | `External` | `Off` (mục 1.1). `permission` chỉ có nghĩa khi `Internal` và bước không phải của chủ hồ sơ; bỏ trống → quyền mặc định của bước. Máy chủ kiểm tra mã quyền thuộc danh mục (module `evaluation`, trừ `self`, `read`, `reopen`).
- `defaultProfiles`: hồ sơ luồng gán khi thêm người vào kỳ, theo `ApprovalAuthority` ảnh chụp (`CoSo`, `CapTren` — cả hai bắt buộc).
- `deadline` theo từng bước **của từng hồ sơ luồng**: hiển thị và cảnh báo; chỉ chặn khi `enforceDeadlines = true`.
- `selfScoreForm`: `09A` (có Mẫu 01/02 — mọi hồ sơ luồng phải áp dụng `B1_REGISTER`) hoặc `09B` (chấm trực tiếp 6 trục, Q3/2026).
- `parameters`: hằng số nghiệp vụ (giá trị mặc định = đúng giá trị code cũ — xem báo cáo task 12).
- Cấu hình **cũ** (schema 1, bước cấp kỳ) không còn được hỗ trợ (chưa có CSDL cần giữ — người điều phối sinh lại `InitialCreate`).
- Kỳ `Draft`: sửa mọi thứ (thêm/sửa/xóa hồ sơ luồng — không xóa được hồ sơ luồng đang được hồ sơ đánh giá dùng, 409). Kỳ `Open`/`Locked`: chỉ sửa thời hạn.

**Kiểu kỳ dựng sẵn** (mỗi kiểu sinh 3 hồ sơ luồng — cấu hình mặc định **chờ nghiệp vụ xác nhận**, sửa được trong giao diện kỳ):

| Hồ sơ luồng (mã) | Căn cứ | B1 đăng ký/duyệt | B2 tự chấm | B2 Chi bộ | B3a tập thể | B3b thẩm định | B3c | B4 quyết định | B5 |
|---|---|---|---|---|---|---|---|---|---|
| Diện Đảng ủy cơ sở (`co-so`) — mặc định `CoSo` | PL III ví dụ 1 | Nội bộ | Nội bộ | Nội bộ | Nội bộ (tập thể lãnh đạo Phòng) | Nội bộ (Phòng TCCB-LĐ) | Nội bộ — `evaluation.director.review` (Giám đốc) | Nội bộ — `evaluation.decide` (Đảng ủy) | Nội bộ |
| Diện BTV Đảng ủy Tổng công ty (`cap-tren`) — mặc định `CapTren` | PL III ví dụ 2 | Nội bộ | Nội bộ | Nội bộ | Nội bộ (tập thể lãnh đạo Công ty) | **Cấp trên** (Ban TCĐU) | **Cấp trên** (HĐTV) | **Cấp trên** (BTV ĐUTCT) | Nội bộ |
| Bí thư/Phó bí thư Chi bộ là nhân viên (`bi-thu-nhan-vien`) | PL III ví dụ 3 | Nội bộ | Nội bộ | Nội bộ | Nội bộ | Nội bộ | Nội bộ — **`evaluation.unit.review`** (Trưởng phòng đề xuất thay Giám đốc) | Nội bộ — `evaluation.decide` | Nội bộ |

- **"Đầy đủ"** (`full`): như bảng, `09A`.
- **"Quý III/2026 — chuyển tiếp"** (`q3-2026-transition`): như bảng nhưng `B1_REGISTER`, `B1_APPROVE` = **Không áp dụng** ở mọi hồ sơ luồng; `09B`.

### 3.3 Danh sách người được đánh giá
- Tạo `EvaluationRecord` ngay khi thêm người: kỳ + người. Thêm người = tạo hồ sơ ở bước áp dụng đầu tiên của hồ sơ luồng, **ảnh chụp** `DepartmentId`, `PartyCellId`, `JobGroup`, `ApprovalAuthority` từ hồ sơ cán bộ và `WorkflowProfileCode` (chọn khi thêm, hoặc mặc định theo `ApprovalAuthority`); `period.manage` sửa được ảnh chụp khi kỳ còn `Draft`/`Open` (có lý do, ghi lịch sử — sửa cấp quyết định **không** tự đổi hồ sơ luồng).
- Đổi hồ sơ luồng từng người / hàng loạt: mục 2.
- Chỉ người trong danh sách mới có hồ sơ; `evaluation.self` không tự tạo hồ sơ.
- Thêm theo: chọn tay, theo Phòng/Chi bộ, hoặc **import Excel** (loại `period-participants`, cột tùy chọn "Hồ sơ luồng" = mã hoặc tên; bỏ trống = mặc định).

### 3.4 Kiểm tra kẹt luồng (task 15)
- `GET /api/evaluations/periods/{id}/readiness` (`period.manage`): với mỗi hồ sơ chưa công bố và mỗi bước **còn phía trước** (kể cả bước đang chờ) có chế độ Nội bộ hoặc Cấp trên, cần **ít nhất một** tài khoản đang hoạt động — không phải chủ hồ sơ — có quyền thực hiện bước (Cấp trên: `evaluation.external.record`) với phạm vi bao trùm hồ sơ. Bước của chủ hồ sơ: chính chủ hồ sơ phải còn hoạt động và có `evaluation.self`. Quyền tính bằng `IPermissionResolver` + `AuthorizationGuard.Evaluate` (không tự suy luật).
- Kết quả: `{ ready, checkedRecords, issues: [{ recordId, fullName, workflowProfileName, step, stepName, mode, permission, permissionName, scope, message }] }`.
- Mở kỳ (`Draft → Open`) khi còn cảnh báo → **409**, `code = PERIOD_NOT_READY`, `data` = kết quả kiểm tra, `errors` = danh sách thông báo. Mở bắt buộc: `{ version, force: true, reason }` (lý do bắt buộc) → lý do được ghi vào `StatusReason` của kỳ (có audit).
## 4. Bỏ phiếu kín (B3a, B4)
- Hệ thống **không** tổ chức bỏ phiếu điện tử và **không lưu phiếu của từng người**. Chỉ ghi **kết quả kiểm phiếu** (số phiếu mỗi mức, phiếu không hợp lệ, số triệu tập/có mặt) do thư ký nhập, gắn với biên bản hội nghị (Mẫu 12/13).
- Kiểm tra: tổng phiếu các mức + không hợp lệ ≤ số có mặt; không âm.
- Audit log của các bảng kết quả kiểm phiếu chỉ ghi người nhập (thư ký) — không có thông tin người bỏ phiếu → đóng B-03 theo thiết kế.


## 5. API hành động (để frontend không tự suy luật)
- `GET /api/evaluations/records/{id}/actions` → danh sách hành động người hiện tại được làm trên hồ sơ, mỗi mục: `{ action, step, label, requiresReason, reasonOptional, overdue, targetSteps? }`. Tính từ: trạng thái hồ sơ + **hồ sơ luồng của hồ sơ** (chế độ + quyền thực hiện của bước) + trạng thái kỳ + xung đột lợi ích + `IAuthorizationGuard`. Bước cấp trên thực hiện → hành động `RecordExternal` (bước ở trường `step`).
- `POST /api/evaluations/records/{id}/external/{step}` (`evaluation.external.record`) → ghi nhận kết quả của bước do cấp trên thực hiện: `{ version, authorityName, documentNumber?, documentDate?, comment?, grade?, score?, attachmentId? }`.
- Các API bước nội bộ (`tasks/approve`, `cell/confirm`, `collective`, `appraisal`, `director-review`, `decision`, `publish`, …) chỉ yêu cầu đăng nhập ở controller (quyền thực hiện cấu hình theo hồ sơ luồng); service kiểm tra chế độ bước, xung đột lợi ích và `IAuthorizationGuard.Ensure` với đúng mã quyền của bước (403 nêu tên quyền).
- `GET /api/evaluations/work-queue?periodId=` → hồ sơ đang chờ **người hiện tại** xử lý, nhóm theo bước (theo hồ sơ luồng của từng hồ sơ; mục có `mode`, `workflowProfileName`).
- Frontend hiển thị bước/nút **chỉ** theo các API trên; tiến trình hồ sơ (`progress[].mode`) hiện "Do cấp trên thực hiện — ghi nhận kết quả" / "Không áp dụng cho nhóm này".

## 6. Câu hỏi cần nghiệp vụ xác nhận (không chặn việc code)
1. B3a: có cần ghi nhận trên hệ thống không, khi Q3/2026 "chưa áp dụng Mẫu 11" nhưng IV.3a vẫn yêu cầu phiếu kín (trích xuất mục 10)?
2. ~~B3c có áp dụng cho mọi đối tượng không?~~ → cấu hình theo hồ sơ luồng (task 15). Còn chờ xác nhận: ví dụ 3 "Trưởng Phòng đề xuất" là bước riêng thay B3c (như cấu hình mặc định) hay gộp vào B3a?
3. ~~Ai nhập kết quả quyết định của BTV Đảng ủy Tổng công ty?~~ → người có `evaluation.external.record` (mặc định vai trò Văn phòng Đảng ủy). Còn chờ xác nhận: ai ghi nhận kết quả thẩm định (Ban TCĐU) và nhận xét (HĐTV) của cấp trên — cùng Văn phòng Đảng ủy hay đơn vị khác?
4. Có chặn cứng theo thời hạn không (`enforceDeadlines`)?
5. Kiến nghị/khiếu nại sau công bố: chỉ cần `Reopen` có lý do, hay cần luồng riêng?
6. Ba hồ sơ luồng dựng sẵn (mục 3.2) có đúng và đủ nhóm đối tượng của ATTECH không (ví dụ Phó Giám đốc — IV.3c dòng 4b; Kiểm soát viên — dòng 5; kiêm nhiệm hai cấp — mục 1.5)?
7. Diện BTV ĐUTCT: B2 "Chi bộ xác nhận" và B3a "tập thể lãnh đạo Công ty" có làm trong hệ thống như cấu hình mặc định không?
