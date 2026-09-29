# Task 19 — Biểu mẫu tập thể, biên bản, báo cáo tổng hợp đúng HD03 (Đợt 8)

- **Agent:** K · **Branch:** `feat/collective-forms-reports`
- **Mã:** T-84, T-85
- **Báo cáo:** `docs/remediation/reports/19-collective-forms-reports.md`
- **Đọc trước:** `CLAUDE.md`, `RULES.md` 7.3 + 8, `docs/nghiep-vu/hd03-trich-xuat.md` mục 5 (tập thể), 6 (hồ sơ báo cáo V.1), 7 (danh mục mẫu); **biểu mẫu gốc** `docs/2.03-HD.TVDU (HD DGXL CAN BO QUY III-2026) (Bieu mau).docx` (Mẫu 07, 08, 12, 14, 15A, 15B, 16) và PDF HD03 (tr.45–49, 68–78); `docs/bieu-mau.md`; `ReportService`, `ExportReportController`, `Infrastructure/Documents/**`, `CollectiveEvaluationService`.
- Song song với task 18 (biểu mẫu cá nhân) và task 20 (sau công bố) — tôn trọng phạm vi file.

## Vấn đề
- Chưa xuất Mẫu 07, 08 (tập thể tự đánh giá), 12 (biên bản hội nghị), 16 (báo cáo Word của cấp ủy) dù Quý III/2026 bắt buộc.
- Trang Báo cáo đặt tên lệch HD03: "Mẫu 14" mang tên khác; "Mẫu 15" là "thống kê cơ cấu tổ chức & sĩ số" (không có trong HD03); "Mẫu 16" là bảng Excel trong khi HD03 Mẫu 16 là báo cáo Word.

## Việc
1. **Xuất tập thể & biên bản (T-84):** template Word **dựng từ đúng biểu mẫu gốc** (bố cục lấy từ file .docx gốc, tag theo `docs/bieu-mau.md`, tag `ORG_*`) cho Mẫu 07, 08 (từ hồ sơ tập thể), 12 (từ biên bản hội nghị — cả hội nghị tập thể lãnh đạo B3a và hội nghị cấp ủy B4). Kiểm tra dữ liệu hiện có đủ các mục của biểu mẫu chưa; thiếu mục nào → bổ sung trường (jsonb theo mã mục) và ô nhập trên trang Tập thể & Hội nghị. Nút xuất (Word/PDF) ngay trên từng hồ sơ tập thể / biên bản. Quyền theo guard hiện có (`collective.manage`/`meeting.read`, phạm vi).
2. **Báo cáo tổng hợp (T-85):**
   - Mẫu 14, 15A, 15B: đặt lại tên, tiêu đề, cột **đúng nguyên văn biểu mẫu gốc**; rà nội dung so với biểu mẫu (mã chức danh M1–M26 từ task 14, nhóm theo thẩm quyền), sửa chỗ lệch.
   - Mẫu 16: **báo cáo Word** theo biểu mẫu gốc, số liệu tổng hợp tự động từ kết quả kỳ (số lượng/tỷ lệ theo mức, theo nhóm chức danh…), các phần nhận xét/đánh giá chung là ô nhập tay trước khi xuất (lưu nháp theo kỳ + phạm vi tổ chức Đảng).
   - Bỏ báo cáo không có trong HD03 (`form-15` "thống kê cơ cấu tổ chức", `form-16` Excel) — hoặc nếu có giá trị nội bộ thì đặt tên rõ "Báo cáo nội bộ — …", không mang số mẫu HD03. Danh sách cán bộ (`cadres`) giữ, đặt tên "Danh sách cán bộ (nội bộ)".
   - Trang `app/reports`: nhóm "Hồ sơ nộp theo HD03" (14, 15A, 15B, 16, 12, 13…) và "Báo cáo nội bộ"; chọn kỳ + phạm vi tổ chức Đảng; mỗi mẫu ghi đúng số và tên HD03.
3. Tài liệu `docs/bieu-mau.md`: tag các mẫu mới; bảng "mẫu nào xuất ở đâu".

## Phạm vi file
Chủ sở hữu: `ExportReportController`, `ReportService`, `ReportAccessService`, `Infrastructure/Documents/Forms/Mau07*`, `Mau08*`, `Mau12*`, `Mau16*` (mới), `Mau13*`, template mới trong `Templates/Word/`, `CollectiveEvaluationRecord`, `EvaluationMeeting` (trường bổ sung), `CollectiveEvaluationService`, `CollectiveEvaluationController`, đăng ký DI/template trong `DocumentExtensions` (chỉ thêm dòng), frontend `app/reports/**`, `app/collective-evaluations/**`, `services/reportService.ts`, component mới cho tập thể/báo cáo.
**Không** sửa: `EvaluationRecord` trường 09C/9D, `SelfScoreForm`, trang chi tiết hồ sơ, `RecordFormsController` (task 18); kiến nghị, kế hoạch, công khai, thông báo (task 20).

## Test
- Tích hợp: xuất 07/08/12/16 ra .docx hợp lệ, có số liệu đúng (dựng kỳ có vài hồ sơ đã công bố ở các mức khác nhau → kiểm số đếm/tỷ lệ trên Mẫu 16, 14, 15A/15B), tên đơn vị từ cài đặt; phạm vi: Bí thư Chi bộ chỉ xuất được của Chi bộ mình; báo cáo cũ đã bỏ → 404.

## Tiêu chí hoàn thành
Build 0 warning, test (integration 0 skip), `tsc`, `npm run build`. Không tạo migration. Báo cáo: bảng mẫu HD03 → endpoint → trang, và các chỗ đã sửa cho khớp nguyên văn biểu mẫu.
