# Thiết kế luồng đánh giá 5 bước và cấu hình kỳ

> **Trạng thái:** thiết kế kỹ thuật để triển khai (task 12; luồng theo diện quyết định — task 15). Thứ tự bước và tác nhân theo bản trích xuất HD03 (`docs/nghiep-vu/hd03-trich-xuat.md`, mục 1.4, 1.6, 6, Phụ lục III) — **chưa xác nhận**; vì vậy bước nào áp dụng cho diện quyết định nào, do ai (trong hệ thống hay cấp trên) thực hiện, tham số bao nhiêu đều là **cấu hình**, không cứng trong code.
> **Cấu trúc công thức tính điểm giữ nguyên** (B-04 vẫn `deferred`); nội dung chấm (tiêu chí, trục kết quả phân loại sản phẩm, khung tỷ trọng, thang quy đổi, ngưỡng mức, tham số, làm tròn) lấy từ **bộ tiêu chí** của kỳ (mục 3.5, task 16).

## 1. Danh mục bước (mã cố định trong code)

| Mã bước | Nội dung | Quyền thực hiện mặc định (khi nội bộ) | Mẫu | Ràng buộc chế độ |
|---|---|---|---|---|
| `B1_REGISTER` | Cá nhân đăng ký sản phẩm/nhiệm vụ | chủ hồ sơ (`evaluation.self`, cố định) | 01 | **chỉ Nội bộ** (luôn áp dụng) |
| `B1_UNIT_SUBMIT` | Trưởng đơn vị gửi / trả lại danh mục (ký ô "Người trực tiếp sử dụng") | `evaluation.tasks.forward` | 01 | mọi chế độ; tự bỏ qua khi chủ hồ sơ là Trưởng đơn vị |
| `B1_HR_REVIEW` | Cơ quan tổ chức cán bộ rà soát, trình / trả lại danh mục (ký ô "Cơ quan tổ chức cán bộ") | `evaluation.tasks.review` | 01 | mọi chế độ |
| `B1_APPROVE` | Duyệt / trả lại danh mục sản phẩm (ký ô "Cấp có thẩm quyền phê duyệt") | `evaluation.tasks.approve` | 01 | mọi chế độ |
| `B2_SELF_SCORE` | Tự chấm điểm, đề xuất mức | chủ hồ sơ (`evaluation.self`, cố định) | 02, 09A, 09C, 9D | **chỉ Nội bộ** |
| `B2_CELL_CONFIRM` | Chi bộ xác nhận / trả lại phiếu tự chấm | `evaluation.cell.confirm` | 9D "Xác nhận của Chi bộ" | mọi chế độ |
| `B3A_COLLECTIVE` | Ghi nhận đề xuất của tập thể lãnh đạo (kết quả phiếu kín) | `evaluation.collective.record` | 11, 12, 13, 07, 08 | mọi chế độ |
| `B3B_APPRAISAL` | Thẩm định, đề xuất mức | `evaluation.appraise` | 10, 03, 19, 20 | mọi chế độ |
| `B3C_DIRECTOR` | Nhận xét, đề xuất của cấp trực tiếp sử dụng (hoặc lãnh đạo đơn vị — ví dụ 3) | `evaluation.director.review` | 10 | mọi chế độ |
| `B4_DECISION` | Ghi nhận quyết định mức xếp loại (kèm đề xuất về công tác cán bộ — cột 13 Mẫu 14, tùy chọn) | `evaluation.decide` | 12, 13, 14, 15A/15B | Nội bộ / Cấp trên (**không** "Không áp dụng") |
| `B5_PUBLISH` | Công bố và lưu trữ kết quả (khóa hồ sơ) | `evaluation.publish` | 16 | **chỉ Nội bộ** |

Mẫu cá nhân áp dụng cho hồ sơ (nút xuất trên trang hồ sơ, phần nhập 09C/9D ở `B2_SELF_SCORE`) theo **bộ tiêu chí của kỳ**
(`requiredForms`, mục 3.5) — không cố định theo bước.

Thứ tự cố định: `B1_REGISTER → B1_UNIT_SUBMIT → B1_HR_REVIEW → B1_APPROVE → B2_SELF_SCORE → B2_CELL_CONFIRM → B3A_COLLECTIVE → B3B_APPRAISAL → B3C_DIRECTOR → B4_DECISION → B5_PUBLISH`.

### 1.1 Chế độ bước và diện quyết định (task 15)

Mỗi bước trong một **diện quyết định** (diện quyết định) có một chế độ:

| Chế độ | Ý nghĩa | Ai thao tác |
|---|---|---|
| `Internal` (Nội bộ) | Bước làm trong hệ thống | người có **quyền thực hiện** cấu hình cho bước (mã trong `PermissionCodes`, module `evaluation`, trừ `self`/`read`/`reopen`); bước của chủ hồ sơ luôn là `evaluation.self` |
| `External` (Cấp trên thực hiện) | Bước do cấp trên / cơ quan ngoài hệ thống làm | người có `evaluation.external.record` **ghi nhận kết quả**: cơ quan, số/ngày văn bản, nhận xét, mức đề xuất/quyết định (bắt buộc với B3a/B3b/B3c/B4), điểm, văn bản đính kèm (tùy chọn — người ghi nhận tải lên rồi gắn vào kết quả; ai xem được hồ sơ thì xem được văn bản, chỉ người có `evaluation.external.record` trên hồ sơ sửa/thay được). Không trả lại được |
| `Off` (Không áp dụng) | Bước không áp dụng cho nhóm này | — (bỏ qua khi tính bước kế tiếp) |

- Bước bắt buộc (không `Off`): `B2_SELF_SCORE`, `B4_DECISION`, `B5_PUBLISH`. Không `External`: `B1_REGISTER`, `B2_SELF_SCORE`, `B5_PUBLISH`.
- **Danh mục do cấp trên duyệt** (`B1_APPROVE` = `External`): bước này không trả lại được, nên khi hồ sơ ở `AwaitingTaskApproval` chủ hồ sơ (`evaluation.self`) hoặc người có `evaluation.tasks.proxy` trên hồ sơ (không phải chủ hồ sơ) **cập nhật danh mục** cho khớp kết quả cấp trên duyệt: `POST /api/evaluations/records/{id}/tasks/update` `{ version, tasks }` — sửa, thêm, xóa sản phẩm với cùng kiểm tra như lúc nộp (số việc, tổng trọng số). Hồ sơ giữ nguyên bước; lịch sử ghi `TaskChange` "Cập nhật danh mục theo kết quả duyệt: <tên>". API `actions` trả `UpdateTasks` (bước `B1_APPROVE`); kỳ khóa dữ liệu chặn như các bước trước thẩm định. Sau đó người có `evaluation.external.record` ghi nhận `B1_APPROVE` như thường; đã ghi nhận thì không cập nhật được nữa (sửa tiếp qua mục 4b).
- Trước khi ghi nhận B4 (nội bộ hoặc cấp trên) hồ sơ qua hai cổng theo thứ tự: hiệu chuẩn (Mẫu 20) rồi kiểm soát (Mẫu 19), theo chế độ cổng của diện quyết định (mục 3.2). Hồ sơ vẫn vào `AwaitingDecision` bình thường. Sau hiệu chuẩn, mức/điểm hiển thị ở Mẫu 14, 16, QĐ, 11 và nhãn bước mức hiệu lực lấy giá trị sau hiệu chuẩn (`ReviewedGrade`/`ReviewedScore`).
- Mỗi hồ sơ đánh giá lưu `WorkflowProfileCode` (ảnh chụp khi thêm vào kỳ; mặc định theo `ApprovalAuthority`). Máy trạng thái, API `actions`, `work-queue`, kiểm tra quyền dùng **cấu hình bước của diện quyết định của hồ sơ**; không còn cấu hình bước cấp kỳ.
- Kết quả ghi nhận của bước cấp trên lưu ở `evaluation_external_results` (mỗi hồ sơ × bước một dòng; ghi lại sau khi mở lại hồ sơ thì cập nhật dòng cũ) và được chép vào các trường của bước trên hồ sơ (mức/nhận xét/điểm; "người thực hiện" = tên cơ quan cấp trên) để báo cáo, biểu mẫu dùng chung một nguồn.
- **Xung đột lợi ích**: mọi bước không phải của chủ hồ sơ (kể cả khi quyền thực hiện được cấu hình khác mặc định, và ghi nhận kết quả của cấp trên) không áp dụng trên hồ sơ của chính mình — kiểm tra trong `EvaluationWorkflowService` trước `IAuthorizationGuard`. Ngoại lệ duy nhất: ghi nhận đề xuất tập thể (B3a, nội bộ) khi cổng `MINUTES = Required` và diện quyết định không phải Trưởng đơn vị đề xuất — mức buộc theo kết quả kiểm phiếu của biên bản, người nhập (thư ký là thành viên cuộc họp) chỉ chép biên bản nên được nhập cả hồ sơ của mình; trả lại ở bước này vẫn bị chặn.

## 2. Trạng thái hồ sơ cá nhân

Trạng thái = **bước đang chờ** (thay enum `RecordStatus` hiện tại):

| Trạng thái | Nghĩa |
|---|---|
| `AwaitingRegistration` | Chờ cá nhân đăng ký sản phẩm |
| `AwaitingUnitSubmit` | Chờ gửi danh mục |
| `AwaitingTaskReview` | Chờ rà soát, trình duyệt |
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
- **Hoàn thành bước** (làm trong hệ thống, hoặc ghi nhận kết quả của cấp trên) → trạng thái của bước áp dụng kế tiếp theo diện quyết định của hồ sơ.
- **Trả lại** (chỉ từ `B1_UNIT_SUBMIT`, `B1_HR_REVIEW`, `B1_APPROVE`, `B2_CELL_CONFIRM`, `B3B_APPRAISAL` khi bước ở chế độ Nội bộ) → về bước của chủ hồ sơ tương ứng (`AwaitingRegistration` hoặc `AwaitingSelfScore`); **bắt buộc lý do**.
- **Đổi diện quyết định** (`period.manage`, bắt buộc lý do, ghi lịch sử `ChangeProfile`): chỉ khi kỳ chưa đóng và hồ sơ **chưa qua** bước nào mà hai diện quyết định khác nhau (chế độ hoặc quyền thực hiện); bước đang chờ không còn áp dụng → chuyển tới bước áp dụng kế tiếp.
- **Không đánh giá (Chưa xếp loại)** — người thuộc diện chưa xếp loại trong quý (đào tạo, nghỉ ốm, thai sản từ 02 tháng; mới bổ nhiệm dưới 01 tháng; đang bị kiểm tra dấu hiệu vi phạm) không đăng ký, không tự chấm, không đi các bước:
  - Đánh dấu: `POST /api/evaluations/records/{id}/not-rated` `{ version, reason }` — `period.manage` trên hồ sơ (`IAuthorizationGuard.Ensure`), không phải hồ sơ của chính mình, **bắt buộc lý do**. Chỉ khi kỳ `Draft`/`Open` và hồ sơ chưa có quyết định (`DecisionRecordedAt` trống, trạng thái không sau `AwaitingDecision`).
  - Khi đánh dấu (`NotRatedRules.Mark`): lưu `NotRatedReason`, `NotRatedAt`, `NotRatedById/ByName` và trạng thái lúc đánh dấu (`NotRatedFromStatus`); đặt quyết định "Chưa xếp loại" (`FinalGrade = ChuaXepLoai`, `DecisionRecordedAt/By` = người đánh dấu) và chuyển thẳng sang `AwaitingPublish` để công bố cùng mọi người. Mức và điểm hiệu lực của hồ sơ là "Chưa xếp loại" / 0, bỏ qua mức đề xuất trước đó. Lịch sử ghi `MarkNotRated` kèm lý do.
  - Bỏ đánh dấu (đánh dấu nhầm): `POST …/not-rated/cancel` `{ version, reason }` — cùng quyền, bắt buộc lý do, chỉ khi kỳ `Draft`/`Open` và hồ sơ chưa công bố. Xóa các trường đã đặt, hồ sơ về trạng thái trước khi đánh dấu (khớp lại bước áp dụng; thường là bước đầu). Lịch sử ghi `UnmarkNotRated`.
  - Hồ sơ đã đánh dấu: tiến trình hiện các bước chưa làm là "bỏ qua", bước quyết định "xong" bởi người đánh dấu; không có trong "Việc cần xử lý" của các bước trước công bố; kiểm tra kẹt luồng không xét các bước trước công bố, cổng và khung tỷ trọng; danh sách người được đánh giá không tính hạn. Báo cáo (Quyết định, Mẫu 14, 15A/15B, 16) tính vào cột Chưa xếp loại vì đã có quyết định; Quyết định ghi lý do ở cột ghi chú; trần Xuất sắc không tính người này. Công bố được dù mức là "Chưa xếp loại". Mở lại hồ sơ đã công bố thì bỏ dấu cùng quyết định đi kèm.
- Hành động không khớp chế độ bước (gọi API bước nội bộ khi bước do cấp trên thực hiện, ghi nhận kết quả khi bước nội bộ, bước không áp dụng) → 409.
- **Mở lại** (`evaluation.reopen`, từ `Published`) → về bước người mở lại chọn (không sớm hơn `AwaitingSelfScore`); **bắt buộc lý do**; giữ dữ liệu các bước sau để đối chiếu (ghi lịch sử).
- Mọi chuyển trạng thái ghi `EvaluationRecordHistory`: người làm, thời điểm, bước, hành động (`complete/return/reopen`), lý do, ảnh chụp điểm/mức trước–sau.
- Chuyển không hợp lệ → 409 với thông báo "Hồ sơ đang ở bước …, không thể …".
- Mọi hành động dùng `version` (xmin) — thiếu `version` với hành động ghi → 400 (đóng T-24).
- **Minh chứng của chủ hồ sơ** theo trạng thái (`EvidenceRules`): trước khi nộp phiếu tự chấm (gồm khi được trả lại / mở lại về bước của
  chủ hồ sơ) thêm, thay, gỡ tự do; đã nộp — chỉ bổ sung tệp mới; `Published` hoặc kỳ không `Open` — khóa, bổ sung qua kiến nghị
  (chi tiết: `phan-quyen.md` mục 4).

## 3. Kỳ đánh giá

### 3.1 Trạng thái kỳ (thay `PeriodStatus` hiện tại)
`Draft → Open → Locked → Closed`
- `Draft`: cấu hình bước, chọn bộ tiêu chí, danh sách người được đánh giá. Chưa ai thao tác hồ sơ.
- `Open`: các bước chạy theo trạng thái từng hồ sơ.
- `Locked` ("điểm chốt dữ liệu", HD03 II.2): chỉ các bước từ `B3B_APPRAISAL` trở đi được thao tác; chủ hồ sơ không sửa được.
  - **Mở riêng hồ sơ** (ngoại lệ theo từng hồ sơ, không mở lại cả kỳ): trả lại (`ReturnTasks`/`ReturnByCell`/`ReturnByAppraiser`) hoặc mở lại (kể cả theo kiến nghị; mở lại khi kỳ `Closed` đưa kỳ về `Locked`) về bước trước `B3B_APPRAISAL` khi kỳ `Locked` **bắt buộc** chọn hạn `unlockUntil` (sau hiện tại, tối đa 60 ngày; giao diện gợi ý 7 ngày). Hồ sơ lưu `UnlockedUntil` + người/lý do; trong hạn, các bước trước thẩm định **của hồ sơ đó** không bị khóa kỳ chặn (`actions`, `work-queue`, thực thi), hồ sơ khác vẫn khóa. Hết hạn → chặn lại ("Thời hạn mở riêng hồ sơ đã hết"); gia hạn bằng `POST records/{id}/unlock/extend` (lý do + hạn mới; `evaluation.reopen` hoặc quyền thực hiện bước thẩm định; không trên hồ sơ của chính mình). Hồ sơ tới bước được thao tác khi kỳ khóa (hoặc công bố) → xóa mở riêng. Lịch sử ghi `GrantUnlock` / `ClearUnlock`. `actions` trả `requiresUnlockDeadline`, `unlockDeadlineSteps` (mở lại), `currentUnlockedUntil`; hồ sơ trả `unlockedUntil`, `unlockActive`.
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
      },
      "gates": {
        "MINUTES":     { "mode": "Required" },
        "CROSSCHECK":  { "mode": "Optional" },
        "CALIBRATION": { "mode": "Required" },
        "CONTROL":     { "mode": "Required" }
      }
    },
    { "code": "cap-tren", "name": "Diện BTV Đảng ủy Tổng công ty", "steps": { "B3B_APPRAISAL": { "mode": "External" }, "…": "…" } }
  ],
  "defaultProfiles": { "CoSo": "co-so", "CapTren": "cap-tren" },
  "enforceDeadlines": false
}
```
- `profiles[].steps[].mode`: `Internal` | `External` | `Off` (mục 1.1). `permission` chỉ có nghĩa khi `Internal` và bước không phải của chủ hồ sơ; bỏ trống → quyền mặc định của bước. Máy chủ kiểm tra mã quyền thuộc danh mục (module `evaluation`, trừ `self`, `read`, `reopen` và 7 quyền phiếu Mẫu 03, 19, 20).
- `profiles[].gates` (từ schema 3): cổng kiểm soát quanh bước quyết định, khóa = mã cổng (`WorkflowGates`). `CALIBRATION` (biên bản hiệu chuẩn Mẫu 20) và `CONTROL` (phiếu kiểm soát Mẫu 19): `Required` (chặn hành động B4 cho tới khi cổng được đáp ứng) | `Recorded` (không chặn, chỉ cảnh báo và ghi nhận thiếu) | `Off`. `CROSSCHECK` (đối soát chuyên ngành Mẫu 03): `Optional` | `Off`. Cổng thiếu được điền mặc định theo chế độ bước `B4_DECISION` của diện quyết định: `Required` khi bước làm trong hệ thống, `Recorded` khi cấp trên thực hiện (`CROSSCHECK` luôn `Optional`). Kỳ mới tạo thì `CONTROL` (Mẫu 19) luôn đặt `Recorded`; `CALIBRATION` (Mẫu 20) theo quy tắc trên. Mã cổng lạ hoặc chế độ không thuộc tập cho phép của cổng bị từ chối khi lưu. Cùng quy tắc với cấu hình bước: chỉ đổi được khi kỳ `Draft` (kỳ `Open`/`Locked` chỉ sửa được thời hạn). Cổng `CROSSCHECK` đã áp dụng: còn phiếu đối soát chưa xác nhận, chưa hủy thì không hoàn thành được thẩm định (B3b, kể cả ghi nhận kết quả cấp trên); `Off` ẩn tính năng. Cổng `CALIBRATION`, `CONTROL` chặn hành động B4 (`decision` và `external/B4_DECISION`, hiệu chuẩn xét trước kiểm soát): `Required` → 409 nếu hồ sơ chưa thuộc biên bản Mẫu 20 đã chốt / phiếu Mẫu 19 đã xác nhận Đủ điều kiện còn hiệu lực (hết hiệu lực khi hồ sơ trong biên bản/phiếu đổi bước, điểm hoặc mức sau lúc chốt/xác nhận); `Recorded` → cho qua, ghi "Ghi nhận thiếu" vào lịch sử hồ sơ và trả cảnh báo ở `actions`; `Off` → không truy vấn. Mẫu 19 chỉ gửi xác nhận/xác nhận được khi cổng `CALIBRATION` của diện quyết định là `Required` thì mọi hồ sơ trong phiếu đã thuộc biên bản đã chốt (409).
- Cổng `MINUTES` (biên bản họp tập thể lãnh đạo Mẫu 12/13, xét ở bước 3a): `Required` | `Recorded` | `Off`; mặc định `Required` khi `B3A_COLLECTIVE` làm trong hệ thống, còn lại `Off`. `Required` → ghi nhận B3a (cả tập thể lẫn Trưởng đơn vị đề xuất) phải gắn biên bản của kỳ, bước B3a, bao trùm hồ sơ, có kết quả kiểm phiếu của hồ sơ (tổng phiếu > 0), thiếu → 400. Tập thể: mức ghi nhận phải là mức đạt quá nửa số có mặt. Trưởng đơn vị (diện Bí thư/Phó bí thư Chi bộ là nhân viên): họp cùng biên bản rồi đề xuất, được khác kết quả kiểm phiếu nhưng bắt buộc ghi lý do (`comment`). Readiness: cổng `Required` mà không tài khoản nào (kể cả chủ hồ sơ) có `meeting.manage` bao trùm hồ sơ → cảnh báo ở bước `B3A_COLLECTIVE`. DTO hồ sơ trả `collectiveMinutesMode`.
- `defaultProfiles`: diện quyết định gán khi thêm người vào kỳ, theo `ApprovalAuthority` ảnh chụp (`CoSo`, `CapTren` — cả hai bắt buộc).
- `deadline` theo từng bước **của từng diện quyết định**: hiển thị và cảnh báo; chỉ chặn khi `enforceDeadlines = true`.
- **Quy tắc điền thời hạn:** mỗi bước có thể kèm `rule` = `{ "kind": "WorkingDayOfQuarter" | "BeforeDayOfLastMonth" | "DayOfLastMonth" | "WorkingDaysBeforeQuarter", "value": N }` ("ngày làm việc thứ N đầu quý" / "trước ngày D tháng cuối quý" = ngày làm việc cuối cùng trước ngày D / "ngày D tháng cuối quý", rơi vào ngày nghỉ thì dời sang ngày làm việc kế tiếp / "ngày làm việc thứ N trước đầu quý", 1 = ngày làm việc cuối quý trước) và danh mục **Ngày nghỉ** chung (bảng `holidays`, API `/api/holidays`, quản trị ở Danh mục → Ngày nghỉ, quyền `catalog.manage`; thứ Bảy, Chủ nhật đã loại sẵn). Quy tắc chỉ là công cụ điền ngày: khi tạo kỳ máy chủ điền sẵn `deadline` theo quy tắc mặc định; `PUT` kỳ kèm `applyDeadlineRules = true` thì máy chủ ghi `deadline` cho các bước có quy tắc (`PeriodSettings.ApplyDeadlineRules`); mọi lúc khác `deadline` chỉ là dữ liệu, sửa tự do, không bị tính lại ngầm. Quy tắc mặc định theo kế hoạch đánh giá của Phòng TCCB-LĐ (`PeriodSettings.Default`). Chi bộ xác nhận cùng ngày với tự chấm; công bố không có hạn. Ngày rơi vào cuối tuần hoặc ngày nghỉ thì chuyển sang ngày làm việc kế tiếp.
  - Diện cơ sở (và diện Bí thư/Phó bí thư Chi bộ là nhân viên): đăng ký (ngày làm việc cuối quý trước -5) và duyệt (-1) trước đầu quý; tự chấm và Chi bộ xác nhận ngày 9; họp tập thể (diện Bí thư/Phó bí thư Chi bộ là nhân viên: Trưởng đơn vị đề xuất) ngày 10; thẩm định ngày 11; nhận xét ngày 14 (diện Bí thư/Phó bí thư Chi bộ là nhân viên không có); quyết định ngày 14.
  - Diện cấp trên: đăng ký ngày làm việc thứ 1 đầu quý, duyệt ngày làm việc thứ 5; tự chấm 9; tập thể 14; thẩm định 15; nhận xét 17; quyết định 18.
  - Diện Phó Giám đốc: đăng ký ngày làm việc thứ 1, duyệt ngày làm việc thứ 5; tự chấm 9; tập thể 14; không có thẩm định riêng; nhận xét 14; quyết định 18.
  Kỳ đã mở vẫn sửa được `rule` và `deadline`.
- Cấu hình kỳ không có `parameters` (task 16): mọi hằng số nghiệp vụ thuộc **bộ tiêu chí** của kỳ (mục 3.5). Khóa cũ trong JSON bị bỏ qua khi đọc. Điểm tính trên danh mục sản phẩm đăng ký nên `B1_REGISTER` luôn áp dụng ở mọi diện quyết định (`WorkflowSteps.Mandatory`, cùng `B2_SELF_SCORE`, `B4_DECISION`, `B5_PUBLISH`); `B1_APPROVE` tùy chọn chế độ. Đổi bộ tiêu chí không làm đổi cấu hình kỳ.
- Đọc được cấu hình schema 2 (chưa có `gates`): khi đọc được nâng lên schema 3 và điền cổng mặc định, không cần migration dữ liệu.
- Cấu hình **cũ** (schema 1, bước cấp kỳ) không còn được hỗ trợ (chưa có CSDL cần giữ — người điều phối sinh lại `InitialCreate`).
- Kỳ `Draft`: sửa mọi thứ (thêm/sửa/xóa diện quyết định — không xóa được diện quyết định đang được hồ sơ đánh giá dùng, 409). Kỳ `Open`/`Locked`: chỉ sửa thời hạn.

**Tạo kỳ** (`POST /api/evaluations/periods`): năm, quý, tên, thời gian và **bộ tiêu chí đã xuất bản (bắt buộc)**; không có "kiểu kỳ". Kỳ mới nhận 4 diện quyết định mặc định (`PeriodSettings.Default`) — cấu hình mặc định **chờ nghiệp vụ xác nhận**, sửa được trong giao diện kỳ:

| Diện quyết định (mã) | Căn cứ | B1 đăng ký/duyệt | B2 tự chấm | B2 Chi bộ | B3a tập thể | B3b thẩm định | B3c | B4 quyết định | B5 |
|---|---|---|---|---|---|---|---|---|---|
| Diện Đảng ủy cơ sở (`co-so`) — mặc định `CoSo` | PL III ví dụ 1; kế hoạch đánh giá quý | Nội bộ — duyệt: `evaluation.director.review` (Giám đốc; Trưởng phòng chỉ ký Mẫu 01 của Phó phòng khi nộp) | Nội bộ | Nội bộ | Nội bộ (tập thể lãnh đạo Phòng) | Nội bộ (Phòng TCCB-LĐ) | Nội bộ — `evaluation.director.review` (Giám đốc) | Nội bộ — `evaluation.decide` (Đảng ủy) | Nội bộ |
| Diện BTV Đảng ủy Tổng công ty (`cap-tren`) — mặc định `CapTren` | PL III ví dụ 2; Chủ tịch, Giám đốc, Kiểm soát viên | Đăng ký nội bộ; **duyệt: Cấp trên** (HĐTV Tổng công ty) | Nội bộ | Nội bộ | Nội bộ (tập thể lãnh đạo Công ty) | **Cấp trên** (Ban TCĐU) | **Cấp trên** (HĐTV) | **Cấp trên** (BTV ĐUTCT) | Nội bộ |
| Bí thư/Phó bí thư Chi bộ là nhân viên (`bi-thu-nhan-vien`) | PL III ví dụ 3; kế hoạch đánh giá quý | Nội bộ — duyệt: `evaluation.director.review` (Giám đốc) | Nội bộ | Nội bộ | Nội bộ — **`evaluation.unit.review`**: Trưởng bộ phận/đơn vị đề xuất mức trên cơ sở ý kiến các Phó, không họp bỏ phiếu, không gắn biên bản/kiểm phiếu (bước mang tên "Trưởng đơn vị đề xuất"; `WorkflowProfile.IsUnitLeaderProposal`) | Nội bộ (Phòng TCCB-LĐ) | **Không áp dụng** | Nội bộ — `evaluation.decide` | Nội bộ |
| Phó Giám đốc (`pho-giam-doc`) | Kế hoạch đánh giá quý của Phòng TCCB-LĐ | Nội bộ — duyệt: `evaluation.director.review` (Chủ tịch) | Nội bộ | Nội bộ | Nội bộ (tập thể lãnh đạo Công ty) | **Không áp dụng** (Ban TCĐU thẩm định ở cấp trên sau bước nhận xét — ghi nhận cùng bước quyết định) | Nội bộ — Chủ tịch nhận xét, đề xuất (`evaluation.director.review`) | **Cấp trên** (BTV ĐUTCT) | Nội bộ |

**Bước 1 gồm bốn bước.** Chủ hồ sơ nộp danh mục → Trưởng đơn vị gửi (`B1_UNIT_SUBMIT`, vai trò Lãnh đạo đơn vị) → Phòng TCCB-LĐ rà soát, trình (`B1_HR_REVIEW`, vai trò Cơ quan thẩm định; ý kiến Phó Giám đốc phụ trách ghi ở khối "Ý kiến trước phê duyệt" trong lúc này) → người có thẩm quyền duyệt (`B1_APPROVE`). Trả lại ở bước nào cũng về chủ hồ sơ, bắt buộc lý do, xóa tên người đã gửi, đã rà soát, đã duyệt. Mặc định: `co-so`, `bi-thu-nhan-vien` bật cả hai bước; `pho-giam-doc` tắt bước gửi, bật bước rà soát; `cap-tren` tắt cả hai. Hạn mặc định trước bước duyệt lần lượt 2 và 1 ngày làm việc.

**Bỏ qua bước gửi.** Khi hồ sơ tới `B1_UNIT_SUBMIT` (Nội bộ) mà chủ hồ sơ có quyền của bước ở phạm vi gần hồ sơ hơn mọi người khác (`StepPerformerRule.OwnerIsNearest`: chủ hồ sơ là Trưởng đơn vị, hoặc là người duy nhất có quyền), bước tự hoàn thành, lịch sử ghi "Bỏ qua bước gửi danh mục: chủ hồ sơ là Trưởng đơn vị." với người thực hiện "Hệ thống"; Mẫu 01 để dòng chấm ở ô "Người trực tiếp sử dụng". Kiểm tra kẹt luồng coi bước này là có người.

**Kỳ cũ.** Cấu hình kỳ lưu trước khi có hai bước mới (thiếu khóa `B1_UNIT_SUBMIT`, `B1_HR_REVIEW`) đọc hai bước là `Off` (`WorkflowSteps.OffWhenMissing`): hồ sơ nộp xong tới thẳng bước duyệt như trước. Thứ tự trạng thái so bằng `WorkflowSteps.IsBefore`, không so số của enum (hai trạng thái mới mang số 11, 12).

Duyệt danh mục (`B1_APPROVE`) của diện `co-so`, `bi-thu-nhan-vien` dùng quyền nhận xét của cấp trực tiếp sử dụng: việc giao Giám đốc (phạm vi các đơn vị trực thuộc, bao trùm đơn vị con), Chủ tịch (phạm vi Công ty) vẫn làm được. Duyệt điều chỉnh việc trong quý theo cùng quyền của bước này.

**Ý kiến trước phê duyệt.** Khối này nằm trong trang chi tiết hồ sơ, không phải là một bước. Nó ghi lại bốn loại ý kiến: kết quả họp rà soát danh mục, kết quả rà soát hợp lệ, ý kiến Phó Giám đốc phụ trách về danh mục, ý kiến Phó Giám đốc phụ trách về đề xuất mức. Nó không chặn bước nào. Cần quyền `evaluation.opinion.record`; chủ hồ sơ không ghi được trên hồ sơ của mình. Loại ý kiến người ghi chọn được suy ra từ quyền trên hồ sơ (`kinds`, mặc định trước). Việc "Ghi ý kiến" (`RECORD_OPINIONS`) giao Phó Giám đốc phụ trách gần hồ sơ nhất (người có `opinion.record` nhưng không có quyền của TCCB-LĐ, lãnh đạo đơn vị, thư ký) khi hồ sơ chờ duyệt danh mục hoặc chờ nhận xét và người đó chưa ghi loại ý kiến tương ứng. Bảng `record_opinions`, API `/api/evaluations/records/{id}/opinions`.


### 3.3 Danh sách người được đánh giá
- Tạo `EvaluationRecord` ngay khi thêm người: kỳ + người. Thêm người = tạo hồ sơ ở bước áp dụng đầu tiên của diện quyết định, **ảnh chụp** `DepartmentId`, `PartyCellId`, `WeightFrameCode` (khung tỷ trọng mặc định của cán bộ; cán bộ chưa chọn → khung mặc định của bộ tiêu chí của kỳ), `ApprovalAuthority` từ hồ sơ cán bộ và `WorkflowProfileCode` (chọn khi thêm, hoặc mặc định theo `ApprovalAuthority`); `period.manage` sửa được ảnh chụp khi kỳ còn `Draft`/`Open` (có lý do, ghi lịch sử — sửa cấp quyết định **không** tự đổi diện quyết định).
- Đổi diện quyết định từng người / hàng loạt: mục 2.
- Chỉ người trong danh sách mới có hồ sơ; `evaluation.self` không tự tạo hồ sơ.
- Thêm theo: chọn tay hoặc theo Phòng/Chi bộ, có thể chỉ định diện quyết định (mã; bỏ trống = mặc định theo cấp quyết định).
- **Theo dõi tiến độ:** `GET /api/evaluations/periods/{id}/participants` (`period.manage`) trả thêm cho từng hồ sơ `currentStep` / `currentStepName` (bước đang chờ, `WorkflowSteps.StepOf(status)`), `currentStepDeadline` (= `deadline` của bước đó trong diện quyết định của hồ sơ), `daysOverdue` (≥ 1 khi hôm nay sau ngày hạn) và `daysLeft` (≥ 0, 0 = hạn hôm nay); "hôm nay" theo giờ Việt Nam (`EvaluationMapping.Today`). Hồ sơ `Published` không còn bước chờ → các trường này `null`. Trang kỳ (tab Tiến độ, khi kỳ `Open`/`Locked`) báo "sắp đến hạn" khi `daysLeft ≤ 3`.

### 3.4 Kiểm tra kẹt luồng (task 15)
- `GET /api/evaluations/periods/{id}/readiness` (`period.manage`): với mỗi hồ sơ chưa công bố và mỗi bước **còn phía trước** (kể cả bước đang chờ) có chế độ Nội bộ hoặc Cấp trên, cần **ít nhất một** tài khoản đang hoạt động — không phải chủ hồ sơ — có quyền thực hiện bước (Cấp trên: `evaluation.external.record`) với phạm vi bao trùm hồ sơ. Bước của chủ hồ sơ: chính chủ hồ sơ phải còn hoạt động và có `evaluation.self`. Quyền tính bằng `IPermissionResolver` + `AuthorizationGuard.Evaluate` (không tự suy luật).
- Kết quả: `{ ready, checkedRecords, issues: [{ recordId, fullName, workflowProfileName, step, stepName, mode, permission, permissionName, scope, message }] }`.
- Hồ sơ có khung tỷ trọng không thuộc bộ tiêu chí của kỳ → cảnh báo ở bước `B2_SELF_SCORE` (sửa ảnh chụp khung hoặc khung mặc định của cán bộ).
- Cổng bắt buộc (mục 3.2): với diện quyết định có `gates.CALIBRATION = Required` cần tài khoản đang hoạt động có `evaluation.calibration.manage` **và** tài khoản khác có `evaluation.calibration.conclude`; có `gates.CONTROL = Required` cần `evaluation.control.perform` **và** tài khoản khác có `evaluation.control.confirm`; cả hai bao trùm hồ sơ và không phải chủ hồ sơ. Thiếu → cảnh báo ở bước `B4_DECISION` (cùng cấu trúc `issues`, `permission` là quyền còn thiếu); mở kỳ vẫn cần `force` + lý do.
- Mở kỳ (`Draft → Open`) khi còn cảnh báo → **409**, `code = PERIOD_NOT_READY`, `data` = kết quả kiểm tra, `errors` = danh sách thông báo. Mở bắt buộc: `{ version, force: true, reason }` (lý do bắt buộc) → lý do được ghi vào `StatusReason` của kỳ (có audit).
### 3.5 Bộ tiêu chí và thang điểm (task 16)
- Bảng `criteria_sets`: mã, tên, trạng thái `Draft | Published | Archived`, nội dung `jsonb` có `schemaVersion`: nhóm tiêu chí chung + tiêu chí con (`Binary` "Đảm bảo/Không đảm bảo" hoặc `Range`), K/AD có lý do và quy tắc xử lý, trục kết quả (mã, tên, nội dung — phân loại sản phẩm đăng ký), khung tỷ trọng A/B/C/D, thang quy đổi, 4 mức xếp loại (ngưỡng + điều kiện), tham số (số sản phẩm, tổng trọng số, ngưỡng giải trình, trần Xuất sắc, làm tròn từng loại điểm…). Quản lý qua `/criteria`, quyền `criteria.manage`.
- Bộ `Published` bất biến (muốn sửa → nhân bản thành bản nháp). CSDL mới seed một bộ mặc định đã xuất bản theo bản trích xuất HD03 (mã `09A-2027`, "Bộ tiêu chí đánh giá cán bộ"; khai báo đủ biểu mẫu cá nhân, tập thể và tổng hợp) — **chờ xác nhận**.
- Kỳ `Draft` chọn một bộ `Published` (`period.manage`); bộ được **chụp nguyên** vào `EvaluationPeriod.CriteriaSnapshot` khi chọn và chụp lại khi mở kỳ. Kỳ đã mở không đổi được bộ (409); sửa/lưu trữ bộ gốc sau đó không ảnh hưởng kỳ. Mở kỳ khi chưa có bộ → 400.
- Hồ sơ lưu điểm theo mã: `GeneralScores` `{ mã tiêu chí con: { score, notApplicable, reason } }`; điểm nhiệm vụ tính từ tỷ lệ A-B-C-D × khung tỷ trọng; nhiệm vụ có `AxisCode` thuộc bộ của kỳ. Tính điểm (`EvaluationScoring`) theo ảnh chụp của kỳ.
- **Điều chỉnh tỷ lệ A-B-C-D theo hồ sơ**: tham số `parameters.frameAdjustLimit` (điểm phần trăm; bộ seed = 10; `null`/0 = không cho điều chỉnh; trường tùy chọn — nội dung cũ đọc ra `null`, không nâng `schemaVersion`; khai báo được 0–50). Ở `B1_APPROVE` (`POST records/{id}/tasks/approve`), người duyệt danh mục gửi tùy chọn `frameAdjustment: { a, b, c, d }` (phần trăm): bộ phải cho điều chỉnh, hồ sơ phải có khung trong bộ, mỗi thành phần lệch khung gốc ≤ biên độ, không âm, tổng = 100 (sai số 0,01) — sai → 400. Không gửi = giữ nguyên; gửi đúng tỷ lệ khung gốc = bỏ điều chỉnh. Lưu ở `evaluation_records.FrameA..FrameD` (0..1, `null` = theo khung gốc), ghi lịch sử "Điều chỉnh tỷ lệ A-B-C-D: …". Sửa ảnh chụp khung (`participants/{id}/snapshot`) hoặc thêm lại người vào kỳ → xóa phần điều chỉnh. **Khung hiệu lực** (`RecordWeightFrames.Effective`) = khung gốc với tỷ trọng đã điều chỉnh, dùng khi tự chấm (`ScoreTask` không đổi, chỉ nhận khung hiệu lực) và hiển thị; Mẫu 02 in `T_RESULT_PCT` = điểm SP / trọng số nên tự theo tỷ trọng mới. DTO hồ sơ trả `baseWeightFrame`, `weightFrame` (hiệu lực), `weightFrameAdjusted`, `frameAdjustLimit`.
- **Giải trình chênh lệch** (B-09): ở `B3B_APPRAISAL`, nếu |tự chấm − thẩm định| ≥ ngưỡng của bộ (hoặc làm đổi mức, nếu bộ bật) → bắt buộc nhập nội dung giải trình/căn cứ (thiếu → 400); lưu `AppraisalExplanation` (căn cứ của người thẩm định), in ở Mẫu 10 (tag `APPRAISAL_EXPLANATION`). Kết quả thẩm định do cấp trên ghi nhận (External) chưa bắt buộc giải trình — chờ nghiệp vụ.
- **Giải trình của cá nhân**: sau khi có kết quả thẩm định nội bộ (`AppraisedAt`) và trước quyết định (`DecisionRecordedAt` trống, chưa `AwaitingPublish`/`Published`, kỳ chưa đóng), chủ hồ sơ ghi ý kiến loại `MemberExplanation` qua `POST /api/evaluations/records/{id}/opinions` — được ghi vì là chủ hồ sơ, không cần `evaluation.opinion.record`; người khác không ghi loại này. Không bắt buộc, không chặn luồng. Chủ hồ sơ chỉ thấy giải trình của mình (ý kiến nội bộ khác vẫn ẩn); người có `evaluation.read` trên hồ sơ thấy giải trình. Bản mới nhất in ở Mẫu 10 (tag `MEMBER_EXPLANATION`).
- **Điểm thẩm định theo nhóm**: `POST …/appraisal` nhận `generalScore` (0..điểm tối đa nhóm tiêu chí chung) và `tasksScore` (0..tổng trọng số nhiệm vụ); máy chủ làm tròn từng nhóm theo quy tắc của nhóm và tính `AppraisalScore` = chung + nhiệm vụ theo quy tắc "tổng điểm" (`EvaluationScoring.TotalScore`), lưu `AppraisalGeneralScore`, `AppraisalTasksScore`. Giải trình chênh lệch xét theo tổng. Client cũ chỉ gửi `appraisalScore` vẫn được nhận (không có điểm nhóm); kết quả cấp trên ghi nhận chỉ có tổng (điểm nhóm để trống).
- **Thẩm định từng tiêu chí con** (người thẩm định chọn trên từng hồ sơ; mặc định theo nhóm): gửi thêm `generalItemScores` `{ mã tiêu chí con: { score, notApplicable, reason } }` (cùng dạng `GeneralScores`). Máy chủ kiểm như phiếu tự chấm nộp chính thức (`EvaluationScoring.ValidateGeneralScores`: đủ mọi tiêu chí, giới hạn điểm, nhóm Đảm bảo/Không đảm bảo, K/AD có lý do, căn cứ giảm điểm); điểm nhóm chung = `EvaluationScoring.GeneralCriteriaScore` (quy tắc K/AD và làm tròn của bộ), `generalScore` gửi kèm bị bỏ qua; `tasksScore` vẫn bắt buộc (mức 1/2/3 không quy ra điểm). Lưu jsonb `evaluation_records.AppraisalGeneralScores` (`null` = chấm theo nhóm; thẩm định lại theo nhóm hoặc ghi nhận kết quả cấp trên → xóa), DTO trả `appraisalGeneralScores`. Giải trình chênh lệch, mức đề xuất xét trên tổng như cách theo nhóm.
- **Mức thẩm định từng nhiệm vụ**: `taskLevels: [{ taskId, level 1..3, comment? }]` bắt buộc đủ mọi nhiệm vụ của hồ sơ (thiếu → 400 nêu tên việc); lưu `EvaluationTask.AppraisalLevel`, `AppraisalComment`.
- **Điều kiện xếp loại** (`GradeConditions`, Domain): có nhiệm vụ trọng tâm Mức 3 → chỉ xếp được Không hoàn thành; Hoàn thành xuất sắc cần không có nhiệm vụ trọng tâm Mức 2/3 và tỷ lệ nhiệm vụ vượt chuẩn ≥ `MinExceedStandardRatio` của mức Xuất sắc trong bộ (bộ không đặt → không xét). Nhiệm vụ chưa có loại (dữ liệu cũ) coi như không trọng tâm. **Chỉ cảnh báo, không chặn**: hồ sơ trả `gradeWarnings` (theo mức hiệu lực) và `excellentMinExceedRatio`; giao diện tính lại cùng quy tắc khi đổi mức ở bước thẩm định, nhận xét của cấp trực tiếp sử dụng, quyết định.
- **Sáu điều kiện Hoàn thành xuất sắc** (`ExcellenceConditions`, Domain; `GET /api/evaluations/records/{id}/excellence-conditions`, quyền `evaluation.read` trên hồ sơ): mỗi điều kiện Đạt / Không / Chưa xác định / Không áp dụng kèm lý do. (i) luôn Chưa xác định — người thẩm định chọn; hệ thống chỉ gợi ý sản phẩm thẩm định Mức 3 hoặc tiến độ C tự chấm dưới 90%. (ii) theo Mẫu 06 cùng kỳ có `HeadId` là cán bộ (số đúng hạn ≥ số được giao); không có Mẫu 06 mà giữ chức vụ lãnh đạo/đứng đầu → Chưa xác định; còn lại Không áp dụng. (iii) tỷ lệ vượt chuẩn. (iv) kế hoạch Mẫu 17 của hồ sơ cùng cán bộ ở kỳ (năm, quý) liền trước: không có → Đạt; chưa duyệt → Chưa xác định; có mốc chưa đạt hoặc mốc đến hạn chưa ghi kết quả → Không. (v) theo khống chế khi thẩm định. (vi) nhiệm vụ trọng tâm Mức 2/3. Chủ hồ sơ khi tự chấm gửi `selfDeclaresNoCap` (cam kết không bị khống chế, lưu `SelfDeclaresNoCap`). Người thẩm định gửi `gradeCap` (Tốt / Hoàn thành / Không hoàn thành, kèm `capReason` bắt buộc) và `conditionOverrides` `{ "I": true }` (lưu jsonb `AppraisalConditionOverrides`, không bắt buộc lý do). Chỉ cảnh báo, không chặn: mức đề xuất cao hơn mức khống chế (`gradeWarnings`), chọn không khống chế khi phiếu Mẫu 03 lĩnh vực an toàn - chất lượng có mục khiếm khuyết (`hasSafetyDefect`). In ở Mẫu 09A (`SELF_CONDITIONS`) và Mẫu 10 mục 4, 5.
- Trần tỷ lệ Xuất sắc (`ExcellentQuotaCalculator`; `GET /api/evaluations/excellent-quotas?periodId&partyCellId`, báo cáo nội bộ kiểm soát tỷ lệ, ghi chú Mẫu 15A/15B, xem trước Mẫu 16): số tối đa = làm tròn 0,5 lên 1 của (tỷ lệ của bộ, 20%–25%, mặc định 20%) × số "Hoàn thành tốt nhiệm vụ"; nhóm tính = (cấp quyết định ảnh chụp trên hồ sơ, mã chức danh M của người — mã đứng trước khi kiêm nhiệm), không chia theo Chi bộ; loại người đứng đầu (`positions.IsHead`) khỏi cả tử và mẫu; mức dùng = mức quyết định nếu đã ghi quyết định, chưa có thì `EffectiveGrade` (cấp trực tiếp sử dụng → thẩm định → tập thể → tự đề xuất đã nộp); hồ sơ lọc theo phạm vi `evaluation.read` và tổ chức Đảng chụp trên hồ sơ (không theo nơi sinh hoạt hiện tại). Chỉ cảnh báo, không chặn. Nội dung bộ cũ có trường mẫu số/làm tròn: bỏ qua khi đọc.
- **Biểu mẫu cá nhân áp dụng** (task 18, đợt 8): `requiredForms` (mã `01`, `02`, `09C`, `9D`, `10`; Mẫu 09A luôn áp dụng),
  mục Mẫu 09C (`selfAssessmentSections`: mã, tiêu đề, câu dẫn, lưu ý, số ký tự tối đa 100–20.000, bắt buộc khi nộp) — sửa trên
  trang Bộ tiêu chí. Bộ mặc định: `01, 02, 09A, 09C, 9D, 10`.
- **Tham số sau công bố** (task 20, đợt 8): `publishScores` (công khai kèm điểm, mặc định không), `improvementPlanRequiredGrades`
  (mức bắt buộc lập Mẫu 17, mặc định Mức C, D), `improvementPlanAlertDays` (cảnh báo "kế hoạch cần lập" còn xét kỳ đã đóng trong số
  ngày này, 1–3650, mặc định 90) — mục 6.

## 4. Bỏ phiếu kín (B3a, B4)
- Hệ thống **không** tổ chức bỏ phiếu điện tử và **không lưu phiếu của từng người**. Chỉ ghi **kết quả kiểm phiếu** (số phiếu mỗi mức, "Chưa đánh giá, xếp loại", phiếu không hợp lệ, số triệu tập/có mặt) do thư ký nhập, gắn với biên bản hội nghị (Mẫu 12/13).
- Kiểm tra: tổng phiếu các mức + chưa đánh giá + không hợp lệ ≤ số có mặt; không âm.
- Hồ sơ gắn vào biên bản (khi lập Mẫu 13 hoặc khi ghi nhận B3a/B4 kèm biên bản) phải cùng kỳ và thuộc đơn vị/tổ chức Đảng của biên bản
  **hoặc đơn vị, tổ chức cấp dưới** (cây con theo `Path`) — biên bản Đảng ủy bộ phận chứa được hồ sơ các Chi bộ trực thuộc; biên bản
  không gắn đơn vị (cấp Công ty) nhận mọi hồ sơ; hồ sơ ngoài cây con bị từ chối.
- Biên bản lưu thêm (jsonb `Details`, đợt 8) Tổ kiểm phiếu (người đầu là Tổ trưởng) và số phiếu phát ra / thu về / hợp lệ / không
  hợp lệ (phát ra ≤ có mặt; thu về ≤ phát ra; hợp lệ + không hợp lệ = thu về). Mẫu 13 xuất **theo từng biên bản** có kết quả kiểm
  phiếu: cán bộ tách mục I (thẩm quyền BTVĐUTCT) / mục II (Đảng ủy/Chi ủy cơ sở) theo thẩm quyền ảnh chụp trên hồ sơ.
- Audit log của các bảng kết quả kiểm phiếu chỉ ghi người nhập (thư ký) — không có thông tin người bỏ phiếu → đóng B-03 theo thiết kế.


## 4a. Sau công bố (task 20, đợt 8)
- **Công khai kết quả** (`GET /api/results`, trang Kết quả đánh giá, quyền `evaluation.results.view`): chỉ họ tên, chức danh, đơn vị,
  mức chính thức (điểm khi bộ tiêu chí bật `publishScores`), ghi chú "đang xem xét kiến nghị"; phạm vi = phạm vi bản gán, chủ hồ sơ
  luôn thấy kết quả của mình (người Chưa xếp loại chỉ chính mình thấy dòng của mình). Không lộ chi tiết hồ sơ, minh chứng, ý kiến các cấp (HD03 II.2).
- **Kiến nghị** (`EvaluationAppeal`, `Submitted → UnderReview → Accepted | Rejected`): chủ hồ sơ gửi sau công bố (`evaluation.appeal.submit`,
  không trùng khi đang xử lý, tệp gắn `EvaluationAppeal`); người có `evaluation.appeal.resolve` trong phạm vi nhận xem xét, trả lời
  bắt buộc căn cứ. Chặn xung đột lợi ích: chủ hồ sơ/người gửi và người đã thực hiện bước mà kiến nghị liên quan tới. Chấp nhận →
  "Mở lại hồ sơ theo kiến nghị" dùng `Reopen` hiện có (quyền `evaluation.reopen`), lý do dẫn chiếu kiến nghị trong lịch sử. Đã chấp nhận
  mà chưa mở lại: kết quả vẫn ghi "đang xem xét", chưa gửi kiến nghị mới, việc nằm trong "Kiến nghị chờ xử lý" của người mở lại được.
  Mở lại hồ sơ đang thuộc biên bản Mẫu 20 đã chốt: khi hồ sơ tới lại bước sau thẩm định, việc chốt lại biên bản hiện ở nhóm
  "Họp thống nhất" (`docs/thiet-ke/mau-03-19-20.md` mục 3.4).
- **Tin trả lời kiến nghị cho chủ hồ sơ** (nhóm `APPEAL_ANSWERS` "Kiến nghị đã được trả lời", chuông `kind = appealAnswered`, dẫn tới
  trang hồ sơ): khi kiến nghị được chấp nhận, không chấp nhận, hoặc đã xử lý xong (chấp nhận, đã mở lại và hồ sơ được công bố lại sau lúc
  mở lại). Mỗi hồ sơ chỉ báo kiến nghị mới nhất. Tin hết khi chủ hồ sơ mở khối kiến nghị trên trang hồ sơ
  (`POST /api/evaluations/records/{id}/appeals/seen`, chỉ chủ hồ sơ, ghi `EvaluationAppeal.OwnerSeenAt`); tin mới hơn mốc này lại hiện.
- **Kế hoạch hỗ trợ, khắc phục 30-60-90 ngày (Mẫu 17)** (`ImprovementPlan`, một kế hoạch/hồ sơ, `Draft → Approved → Acknowledged → Closed`):
  bắt buộc khi mức chính thức thuộc `improvementPlanRequiredGrades` của bộ tiêu chí; người quản lý trực tiếp có `evaluation.improvement.manage` (không
  trên hồ sơ của mình) lập, sửa khi đang lập, ghi kết quả từng mốc; thủ trưởng đơn vị có `evaluation.improvement.approve` duyệt (khác người
  lập); không còn ai ngoài chủ hồ sơ có quyền lập thì người có quyền duyệt gần nhất lập thay và người duyệt khác duyệt; chủ hồ sơ xác nhận cam kết; mốc 90 ngày ghi kết quả → đóng.
  Xuất Word/PDF Mẫu 17 từ file mẫu dựng theo biểu mẫu gốc.
- **Việc cần xử lý / chuông nhắc việc**: nhóm "Kiến nghị chờ xử lý", "Kế hoạch 30-60-90 ngày" (giao người gần nhất như các bước —
  `IStepPerformers`, khi chờ duyệt bỏ người lập; chỉ hồ sơ của kỳ đang
  mở/khóa dữ liệu và kỳ đã đóng trong `improvementPlanAlertDays` ngày gần nhất, tính từ lúc đóng kỳ), "Kế hoạch chờ bạn xác nhận",
  "Kiến nghị đã được trả lời";
  `GET /api/notifications/summary` đếm tổng việc, bước quá hạn, sắp tới hạn (`Notifications:DueSoonDays`, mặc định 2).

## 4c. Theo dõi hồ sơ tập thể (Mẫu 06, 07, 08)

`GET /api/evaluations/collective-records/tracking?periodId=` — quyền `evaluation.read` hoặc `collective.manage`, lọc theo phạm vi như danh sách hồ sơ tập thể. Trả mỗi tổ chức Đảng có hồ sơ cán bộ trong kỳ (theo `PartyCellId` chụp lúc thêm vào kỳ): số cán bộ, trạng thái Mẫu 06/07/08 (chưa lập, nháp, đã nộp), `complete`, `overdue`, `lastUpdatedAt`, kèm `deadline` của kỳ. Hạn = ngày 8 của tháng cuối quý, dời sang ngày làm việc kế tiếp nếu rơi vào cuối tuần (chưa trừ ngày nghỉ khai báo). Quá hạn = qua hạn mà chưa nộp đủ ba mẫu. Chỉ để hiển thị: không chặn bước nào của luồng cán bộ. Mã: `CollectiveTrackingBuilder`.

## 4b. Điều chỉnh việc trong quý (Mẫu 04, 05)
- Bảng `task_change_requests` (`TaskChangeRequest`, xmin, xóa mềm, audit): `Type` = `Adjust` (điều chỉnh nội dung/chỉ tiêu/thời hạn/trọng số
  một việc) / `Carryover` (chuyển tiếp sang quý sau) / `Exclude` (loại trừ, bất khả kháng) — Mẫu 04; `Urgent` (việc đột xuất) — Mẫu 05.
  `TaskId?`, `Proposal` (jsonb — các trường mới của việc, hoặc toàn bộ việc đột xuất), `Weights` (jsonb — trọng số sau điều chỉnh của mọi
  việc còn hiệu lực), `WeightsBefore`, `TaskBefore` (jsonb nullable — nội dung việc đích lúc lập đề nghị điều chỉnh, để in cột "trước" Mẫu 04 sau khi duyệt đã ghi đè việc; chụp lúc lập vì khi đó việc chưa đổi và mỗi hồ sơ chỉ có một đề nghị chờ), `Reason`, `Status` = `Pending / Approved / Rejected`, người đề nghị / quyết định, thời điểm, ý kiến.
- Áp dụng khi: kỳ **Đang mở**, hồ sơ ở `AwaitingSelfScore` (đã duyệt danh mục sản phẩm, chưa nộp tự chấm), kỳ đã có bộ tiêu chí. Mỗi hồ sơ tối đa **một** đề nghị chờ duyệt.
- Quyền: Mẫu 04 — chủ hồ sơ (`evaluation.self`); Mẫu 05 — chủ hồ sơ hoặc người duyệt danh mục sản phẩm. Duyệt / không duyệt — quyền thực hiện
  `B1_APPROVE` theo diện quyết định của hồ sơ (`WorkflowActions.PermissionFor`; bước do cấp trên thực hiện → `evaluation.external.record`),
  `IAuthorizationGuard.Ensure` với `AccessTarget.ForRecord`; chủ hồ sơ không tự duyệt; không duyệt bắt buộc lý do.
- **Đã được cấp trên phê duyệt** (khi `B1_APPROVE` của diện quyết định là `External`): chủ hồ sơ hoặc người có `evaluation.tasks.proxy` (không trên hồ sơ của mình) gửi `externalApproval: { authorityName?, documentNumber, documentDate }` kèm đề nghị (mọi loại). Cơ quan mặc định là tên đã ghi nhận ở `B1_APPROVE` (`TasksApprovedByName`); thiếu cơ quan / số / ngày → 400. Cùng điều kiện trạng thái và cùng kiểm tra, đề nghị được áp ngay như khi duyệt: `Status = Approved`, `DecidedById = null`, `DecidedByName` = cơ quan, `DecisionComment` = "Văn bản số … ngày dd/MM/yyyy" (`TaskChangeRules.IsExternallyApproved`); lịch sử ghi ở bước `B1_APPROVE`. Mẫu 04/05 in ô người phê duyệt = cơ quan kèm số, ngày văn bản. `GET …/task-changes` trả `requiresExternalApproval`, `defaultAuthorityName`, `requests[].approvedExternally`. Không gửi `externalApproval` thì vẫn là đề nghị chờ duyệt như cũ.
- Duyệt: kiểm lại trên danh mục hiện tại (`TaskChangeRules.Validate` → `EvaluationScoring.ValidateTaskRegistration`, **chỉ đếm việc còn
  hiệu lực**) rồi áp vào `evaluation_tasks` trong cùng một lần lưu: Adjust → sửa việc; Exclude/Carryover → `ChangeStatus` = `Excluded` /
  `CarriedOver`, trọng số 0, điểm 0 (việc còn trên phiếu, không chấm); Urgent → thêm việc `Kind = DotXuat`; trọng số các việc theo bảng sau
  điều chỉnh (điểm tạm tính lại theo khung tỷ trọng). Lịch sử hồ sơ: hành động `TaskChange` (gửi, duyệt — kèm thay đổi trọng số, không duyệt —
  kèm lý do). Phiên bản: gửi đề nghị dùng `version` của hồ sơ; duyệt / không duyệt dùng `version` của đề nghị.
- Tự chấm (B2): việc đã loại trừ / chuyển tiếp bị bỏ qua (điểm 0, không tính vào tỷ lệ vượt chuẩn) — công thức không đổi.
- API: `GET /api/evaluations/records/{id}/task-changes` (danh sách + cờ `canRequestChange`, `canRecordUrgent`, `requests[].canDecide`),
  `POST …/task-changes`, `POST …/task-changes/{changeId}/approve`, `POST …/task-changes/{changeId}/reject`. Việc cần xử lý / chuông: nhóm
  `TASK_CHANGES` "Duyệt điều chỉnh việc".
- Xuất Word/PDF: khối "Biểu mẫu" của hồ sơ có một phiếu cho mỗi đề nghị (Mẫu 04 cho Adjust/Carryover/Exclude, Mẫu 05 cho Urgent) — `GET /api/reports/docx/record/{id}/requests` và `…/{04|05}/{requestId}`.
- Chưa làm: tệp minh chứng (kể cả văn bản phê duyệt của cấp trên) gắn đề nghị, tạo việc chuyển tiếp ở kỳ quý sau.

## 5. API hành động (để frontend không tự suy luật)
- `GET /api/evaluations/records/{id}/actions` → danh sách hành động người hiện tại được làm trên hồ sơ, mỗi mục: `{ action, step, label, requiresReason, reasonOptional, overdue, targetSteps?, blockedBy?, blockedReason?, warnings? }`. Hành động B4 (`RecordDecision`, `RecordExternal` ở `B4_DECISION`) bị cổng chặn vẫn được trả về với `blockedBy` = `CALIBRATION_REQUIRED` hoặc `CONTROL_CHECK_REQUIRED` (cổng đầu tiên chưa đạt, hiệu chuẩn trước) và `blockedReason` tiếng Việt; cổng `Recorded` chưa đạt chỉ có `warnings`. Frontend chỉ hiển thị, không tự suy luật. Tính từ: trạng thái hồ sơ + **diện quyết định của hồ sơ** (chế độ + quyền thực hiện của bước) + trạng thái kỳ + xung đột lợi ích + `IAuthorizationGuard`. Bước cấp trên thực hiện → hành động `RecordExternal` (bước ở trường `step`). Đánh dấu / bỏ đánh dấu Chưa xếp loại → `MarkNotRated` / `UnmarkNotRated` (`requiresReason`); danh sách người được đánh giá trả thêm `notRatedReason`, `canMarkNotRated`, `canUnmarkNotRated` theo cùng điều kiện.
- `POST /api/evaluations/records/{id}/external/{step}` (`evaluation.external.record`) → ghi nhận kết quả của bước do cấp trên thực hiện: `{ version, authorityName, documentNumber?, documentDate?, comment?, grade?, score?, attachmentId?, cadreWorkProposal? }` (`cadreWorkProposal` — cột 13 Mẫu 14 — chỉ nhận ở `B4_DECISION`, bước khác → 400). Quyết định nội bộ (`decision`) nhận cùng trường.
- Các API bước nội bộ (`tasks/approve`, `cell/confirm`, `collective`, `appraisal`, `director-review`, `decision`, `publish`, …) chỉ yêu cầu đăng nhập ở controller (quyền thực hiện cấu hình theo diện quyết định); service kiểm tra chế độ bước, xung đột lợi ích và `IAuthorizationGuard.Ensure` với đúng mã quyền của bước (403 nêu tên quyền).
- `GET /api/evaluations/work-queue?periodId=` → hồ sơ đang chờ **người hiện tại** xử lý, nhóm theo bước (theo diện quyết định của từng hồ sơ; mục có `mode`, `workflowProfileName`).
- Frontend hiển thị bước/nút **chỉ** theo các API trên; tiến trình hồ sơ (`progress[].mode`) hiện "Do cấp trên thực hiện — ghi nhận kết quả" / "Không áp dụng cho nhóm này".

## 6. Câu hỏi cần nghiệp vụ xác nhận (không chặn việc code)
1. B3a: có cần ghi nhận kết quả bỏ phiếu kín trên hệ thống (phiếu Mẫu 11 xuất từ hệ thống, kết quả do thư ký nhập) không (trích xuất mục 10)?
2. ~~B3c có áp dụng cho mọi đối tượng không?~~ → cấu hình theo diện quyết định (task 15). Còn chờ xác nhận: ví dụ 3 "Trưởng Phòng đề xuất" là bước riêng thay B3c (như cấu hình mặc định) hay gộp vào B3a?
3. ~~Ai nhập kết quả quyết định của BTV Đảng ủy Tổng công ty?~~ → người có `evaluation.external.record` (mặc định vai trò Văn phòng Đảng ủy). Còn chờ xác nhận: ai ghi nhận kết quả thẩm định (Ban TCĐU) và nhận xét (HĐTV) của cấp trên — cùng Văn phòng Đảng ủy hay đơn vị khác?
4. Có chặn cứng theo thời hạn không (`enforceDeadlines`)?
5. ~~Kiến nghị/khiếu nại sau công bố: chỉ cần `Reopen` có lý do, hay cần luồng riêng?~~ → luồng kiến nghị riêng + `Reopen` (mục 4a, task 20). Còn chờ xác nhận: hạn gửi/trả lời kiến nghị (HD03 không quy định), ai xử lý (mặc định Văn phòng Đảng ủy).
6. Ba diện quyết định dựng sẵn (mục 3.2) có đúng và đủ diện quyết định của ATTECH không (ví dụ Phó Giám đốc — IV.3c dòng 4b; Kiểm soát viên — dòng 5; kiêm nhiệm hai cấp — mục 1.5)?
7. Diện BTV ĐUTCT: B2 "Chi bộ xác nhận" và B3a "tập thể lãnh đạo Công ty" có làm trong hệ thống như cấu hình mặc định không?
8. Điều kiện xếp loại theo mức thẩm định nhiệm vụ: có chặn cứng (không cho chọn mức trái điều kiện) hay chỉ cảnh báo như hiện tại? Câu "tỷ lệ nhiệm vụ trọng tâm Mức 2 từ 20%" áp dụng cho mức nào, tính trên tổng nhiệm vụ hay nhiệm vụ trọng tâm — chưa kiểm tra cho đến khi được xác nhận.
9. ~~Thẩm định trả lại: hồ sơ nộp lại đi thẳng tới thẩm định hay đi lại từ Chi bộ?~~ → **đi lại từ Chi bộ** (Chi bộ xác nhận và tập thể đề xuất lại), giữ như hiện tại (xác nhận 23/10/2026).
10. ~~Chủ hồ sơ có xem được mức/nhận xét của các bước sau trước khi công bố?~~ → **được xem**, giữ như hiện tại (xác nhận 23/10/2026).
11. Một người có được thực hiện nhiều bước trên cùng một hồ sơ (vd ghi đề xuất tập thể rồi thẩm định; người đã quyết định lần đầu quyết định lại sau khi chấp nhận kiến nghị)? Hiện chỉ chặn chủ hồ sơ và người đã quyết định xử lý kiến nghị — chưa quyết.
