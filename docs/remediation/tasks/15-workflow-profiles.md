# Task 15 — Luồng theo nhóm đối tượng, bước do cấp trên thực hiện, người thực hiện cấu hình được (Đợt 6)

- **Agent:** G · **Branch:** `feat/workflow-profiles`
- **Mã:** T-74, T-75, T-76
- **Báo cáo:** `docs/remediation/reports/15-workflow-profiles.md`
- **Đọc trước:** `CLAUDE.md`, `RULES.md` mục 7.3 + 8, `docs/thiet-ke/luong-danh-gia.md`, `docs/thiet-ke/phan-quyen.md` mục 3–4, `docs/nghiep-vu/hd03-trich-xuat.md` mục 1.4, 1.6, 6 (đặc biệt Phụ lục III ví dụ 1, 2, 3), `docs/remediation/reports/12-evaluation-workflow.md`.
- Chạy song song với task 14 (mô hình tổ chức) — tôn trọng phạm vi file.

## Vấn đề
Hiện cả kỳ dùng một luồng. Hồ sơ diện BTV Đảng ủy Tổng công ty (Ban Giám đốc) đi luồng nội bộ và **kẹt** ở bước cấp trực tiếp sử dụng (chỉ Giám đốc có quyền, lại không được tự làm). HD03 có nhiều biến thể: ví dụ 2 (thẩm định, nhận xét, quyết định đều ở cấp trên), ví dụ 3 (Trưởng phòng đề xuất thay Giám đốc; không có bước 3c).

## 1. Hồ sơ luồng (workflow profile) trong cấu hình kỳ (T-74)
- `PeriodSettings` thêm `profiles`: danh sách hồ sơ luồng, mỗi cái: `code`, `name`, `steps` — mỗi bước: `mode` ∈ `Internal | External | Off`, `permission` (mã quyền thực hiện khi `Internal`; mặc định theo bảng thiết kế hiện tại; phải thuộc `PermissionCodes`), `deadline?`.
  - `External`: bước do cơ quan ngoài hệ thống làm; người có quyền mới `evaluation.external.record` **ghi nhận kết quả** (cơ quan, số/ngày văn bản, nhận xét, mức đề xuất/quyết định, tệp đính kèm tùy chọn). Gộp `evaluation.decide.external` hiện có vào cơ chế này (B4 `External` = ghi nhận quyết định cấp trên); cập nhật `PermissionCodes`, seed vai trò mặc định, thiết kế.
  - Bước bắt buộc (`B2_SELF_SCORE`, `B4_DECISION`, `B5_PUBLISH`) không được `Off`; `B2_SELF_SCORE`, `B5_PUBLISH` không được `External`.
- Mỗi hồ sơ đánh giá lưu `WorkflowProfileCode` (ảnh chụp khi thêm người vào kỳ). Chọn mặc định theo `ApprovalAuthority` (`CoSo` → profile cơ sở, `CapTren` → profile cấp trên); `period.manage` đổi được từng người (có lý do, khi hồ sơ chưa qua bước bị ảnh hưởng). Cho phép import cột profile trong import người được đánh giá.
- Máy trạng thái, `actions`, `work-queue`, kiểm tra quyền dùng **cấu hình bước của profile của hồ sơ** thay cho cấu hình cả kỳ. Bỏ cấu hình bước cấp kỳ cũ (không giữ tương thích).
- Preset khi tạo kỳ (mỗi preset sinh sẵn các profile):
  - **Diện Đảng ủy cơ sở** — theo Phụ lục III ví dụ 1: B3A tập thể lãnh đạo Phòng, B3B Phòng TCCB-LĐ, B3C Giám đốc, B4 Đảng ủy (nội bộ).
  - **Diện BTV Đảng ủy Tổng công ty** — theo ví dụ 2: B3A tập thể lãnh đạo Công ty (nội bộ), B3B/B3C/B4 `External`.
  - **Bí thư/Phó bí thư Chi bộ là nhân viên** — theo ví dụ 3: B3C do Trưởng phòng thực hiện thay Giám đốc: tạo quyền mới `evaluation.unit.review` ("Lãnh đạo đơn vị đề xuất", gán cho vai trò Lãnh đạo Phòng mặc định) và đặt làm quyền thực hiện B3C của profile này (Internal).
  - Hai kiểu kỳ: "Đầy đủ" và "Quý III/2026 — chuyển tiếp" (tắt B1 ở mọi profile).
  Tất cả là **cấu hình mặc định chờ nghiệp vụ xác nhận**; sửa được trong giao diện kỳ.

## 2. Kiểm tra kẹt luồng (T-75)
- `GET /api/evaluations/periods/{id}/readiness`: với mỗi hồ sơ và mỗi bước `Internal` còn phía trước, kiểm tra có **ít nhất một** tài khoản đang hoạt động (không phải chủ hồ sơ) có quyền bước đó với phạm vi bao trùm hồ sơ (dùng resolver/guard, không tự suy luật). Trả danh sách "hồ sơ X sẽ kẹt ở bước Y vì không ai có quyền Z trong phạm vi W".
- Mở kỳ (`Draft → Open`) khi còn lỗi kẹt → 409 kèm danh sách; cho phép mở bắt buộc với lý do (ghi lịch sử). Trang cấu hình kỳ hiện bảng kiểm tra này.

## 3. Giao diện (T-76)
- `app/periods/**`: sửa cấu hình theo profile (bảng bước × chế độ × quyền thực hiện × thời hạn), thêm/sửa/xóa profile, gán profile cho người được đánh giá (từng người / hàng loạt), bảng readiness.
- Trang hồ sơ: bước `External` hiển thị "Do cấp trên thực hiện — ghi nhận kết quả", form ghi nhận; bước `Off` hiển thị "Không áp dụng cho nhóm này".

## Phạm vi file
Chủ sở hữu: `Domain/Evaluation/**`, `EvaluationPeriod`, `EvaluationRecord` (trường luồng/ghi nhận ngoài), `EvaluationRecordHistory`, `EvaluationWorkflowService`, `PeriodService`, `EvaluationController`, controller kỳ, `PermissionCodes` (chỉ thêm `evaluation.external.record`, `evaluation.unit.review`, gỡ `evaluation.decide.external`), `DataSeeder` (phần vai trò mặc định liên quan 2 quyền trên + phần kỳ/hồ sơ mẫu), định nghĩa import người được đánh giá, `docs/thiet-ke/luong-danh-gia.md`; frontend `app/periods/**`, `app/evaluations/**`, `app/work-queue/**`, `components/evaluations/**`, `services/evaluationService.ts`.
**Không** sửa: cây đơn vị, chức vụ, `AuthorizationGuard`/`ScopeFilter`, `OrganizationService`, `app/catalog/**`, `app/admin/**` (task 14).

## Test
- Unit: máy trạng thái theo profile (Internal/External/Off), validate profile.
- Tích hợp: (a) hồ sơ Giám đốc (CapTren) đi tới `Published` với B3B/B3C/B4 ghi nhận ngoài; (b) hồ sơ profile "Bí thư Chi bộ là nhân viên" bỏ qua B3C của Giám đốc, Trưởng phòng đề xuất; (c) readiness phát hiện hồ sơ kẹt khi xóa bản gán của người duy nhất có quyền; mở kỳ bị 409; (d) hồ sơ cơ sở vẫn đi như cũ (W1 giữ nguyên kết quả).

## Tiêu chí hoàn thành
- Build 0 warning, test (integration 0 skip), `tsc`, `npm run build` pass. Không tạo migration.
- Báo cáo: cấu trúc `PeriodSettings` mới, bảng preset × profile × bước, endpoint mới/đổi, câu hỏi nghiệp vụ cần xác nhận.
