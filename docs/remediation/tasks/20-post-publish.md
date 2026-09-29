# Task 20 — Sau công bố: công khai kết quả, kiến nghị, kế hoạch 30-60-90, nhắc việc (Đợt 8)

- **Agent:** L · **Branch:** `feat/post-publish`
- **Mã:** T-86, T-87, T-88, T-89
- **Báo cáo:** `docs/remediation/reports/20-post-publish.md`
- **Đọc trước:** `CLAUDE.md`, `RULES.md` 7.3 + 8, `docs/nghiep-vu/hd03-trich-xuat.md` mục 2.2 (quyền được biết, dữ liệu nhạy cảm, "công khai kết quả không đồng nghĩa công bố toàn bộ hồ sơ"), mục 6 (Bước 5; Khiếu nại, kiến nghị PL II III.2), mục 7 (Mẫu 17); biểu mẫu gốc `docs/2.03-HD.TVDU (HD DGXL CAN BO QUY III-2026) (Bieu mau).docx` (Mẫu 17) và PDF HD03 tr.79; `docs/thiet-ke/phan-quyen.md`, `luong-danh-gia.md`.
- Song song với task 18, 19 — tôn trọng phạm vi file.

## 1. Công khai kết quả (T-86)
- Trang `app/results/**` "Kết quả đánh giá": sau khi hồ sơ `Published`, người có quyền mới `evaluation.results.view` (phạm vi) xem **danh sách kết quả** trong phạm vi: họ tên, chức danh, đơn vị, **mức xếp loại chính thức** (và điểm nếu bộ tiêu chí cho phép — thêm cờ cấu hình `publishScores` trong bộ tiêu chí hoặc cài đặt kỳ; mặc định chỉ mức). **Không** hiện chi tiết hồ sơ, minh chứng, ý kiến (HD03 mục 2.2). Lọc theo kỳ, đơn vị, mức.
- Phạm vi công khai do **bản gán vai trò** quyết định (Global = toàn công ty; đơn vị/tổ chức Đảng = chỉ trong đơn vị đó và đơn vị con), không cứng trong code. Seed: gán `evaluation.results.view` cho vai trò mặc định "Người được đánh giá"; dữ liệu mẫu gán phạm vi Global. Ghi rõ trong báo cáo là mặc định chờ xác nhận.

## 2. Kiến nghị sau công bố (T-87)
- Entity `EvaluationAppeal`: hồ sơ, người gửi (chủ hồ sơ), nội dung, tệp đính kèm (gắn đối tượng `Appeal`), trạng thái `Submitted → UnderReview → Accepted | Rejected`, người xử lý, trả lời (bắt buộc nêu căn cứ), thời điểm.
- Chủ hồ sơ gửi khi hồ sơ `Published` (một hồ sơ có thể nhiều kiến nghị, không trùng khi đang xử lý). Hồ sơ hiển thị nhãn "Đang xem xét kiến nghị" (HD03: kết quả được ghi chú đang xem xét).
- Người xử lý: quyền mới `evaluation.appeal.resolve` theo phạm vi; **xung đột lợi ích**: người đã tham gia thẩm định/quyết định hồ sơ đó bị chặn nếu kiến nghị liên quan tới họ — tối thiểu: không xử lý kiến nghị của chính mình; thêm mã vào `ConflictOfInterestCodes`. Chấp nhận → gợi ý (không tự động) mở lại hồ sơ bằng `Reopen` hiện có, liên kết kiến nghị trong lịch sử.
- Work-queue hiện nhóm "Kiến nghị chờ xử lý". Trang chi tiết hồ sơ: khối "Kiến nghị" (component riêng).

## 3. Kế hoạch hỗ trợ, khắc phục 30-60-90 ngày — Mẫu 17 (T-88)
- Entity `ImprovementPlan` gắn hồ sơ có mức chính thức thuộc nhóm bắt buộc (theo Mẫu 17: "Bắt buộc áp dụng đối với cán bộ xếp loại Hoàn thành nhiệm vụ - Mức C hoặc Không hoàn thành nhiệm vụ - Mức D" — danh sách mức bắt buộc là tham số trong bộ tiêu chí, mặc định {HoanThanh, KhongHoanThanh}, không cứng). Nội dung theo đúng các mục của Mẫu 17 gốc (mục tiêu 30/60/90 ngày, người hỗ trợ, kết quả…), jsonb theo mã mục.
- Ai lập/duyệt: quyền mới `evaluation.improvement.manage` (phạm vi — thủ trưởng đơn vị); chủ hồ sơ xem và xác nhận. Readiness/cảnh báo: hồ sơ bắt buộc có kế hoạch mà chưa có → hiện trên work-queue của người có quyền.
- Xuất Word Mẫu 17 từ template **dựng theo biểu mẫu gốc** (tag `ORG_*`), endpoint trong controller mới của task này.

## 4. Nhắc việc trong ứng dụng (T-89)
- Biểu tượng chuông ở header: số việc đang chờ (work-queue), bước sắp tới hạn (≤ 2 ngày, theo hạn của hồ sơ luồng) và quá hạn, kiến nghị mới, kế hoạch cần lập. Tính khi tải (API `GET /api/notifications/summary`), không cần email/đẩy thời gian thực.

## Phạm vi file
Chủ sở hữu: entity/cấu hình/service/controller mới `EvaluationAppeal`, `ImprovementPlan`, kết quả công khai, thông báo; `PermissionCodes` (thêm 4 mã), `ConflictOfInterestCodes` (thêm), `DataSeeder` (quyền cho vai trò mặc định), `WorkQueue` phía service (thêm nhóm — sửa gọn), cờ `publishScores`/điều kiện kế hoạch trong bộ tiêu chí (chỉ thêm tham số), template `Mau_17*`, frontend `app/results/**`, component mới `components/evaluations/AppealPanel.tsx`, `ImprovementPlanPanel.tsx`, `components/layout/NotificationBell.tsx`, chèn **tối thiểu** vào `app/evaluations/[recordId]/page.tsx` (chỉ render 2 panel), `AppHeader.tsx` (chuông), `AppSidebar.tsx` (mục "Kết quả đánh giá"), `app/work-queue/**` (nhóm mới), service FE mới.
**Không** sửa: 09C/9D, `RecordFormsController`, `SelfScoreForm` (task 18); `ReportService`, `ExportReportController`, trang báo cáo/tập thể (task 19).

## Test
- Tích hợp: công khai chỉ hiện mức (không lộ chi tiết), theo phạm vi; kiến nghị: gửi → xử lý bởi người có quyền khác chủ hồ sơ → trả lời bắt buộc căn cứ → nhãn "đang xem xét" bật/tắt đúng; người có xung đột bị chặn; kế hoạch: hồ sơ mức C/D bắt buộc có kế hoạch, lập → xuất Mẫu 17; chuông trả số đúng.

## Tiêu chí hoàn thành
Build 0 warning, test (integration 0 skip), `tsc`, `npm run build`. Không tạo migration. Báo cáo ghi các lựa chọn mặc định chờ nghiệp vụ xác nhận.
