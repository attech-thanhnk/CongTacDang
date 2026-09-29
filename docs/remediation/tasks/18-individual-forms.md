# Task 18 — Biểu mẫu cá nhân phải nộp: 09B, 09C, 9D (Đợt 8)

- **Agent:** J · **Branch:** `feat/individual-forms`
- **Mã:** T-82, T-83
- **Báo cáo:** `docs/remediation/reports/18-individual-forms.md`
- **Đọc trước:** `CLAUDE.md`, `RULES.md` 7.3 + 8, `docs/nghiep-vu/hd03-trich-xuat.md` mục 7 (danh mục mẫu, cột "Q3/2026"), mục 8; **biểu mẫu gốc** `docs/2.03-HD.TVDU (HD DGXL CAN BO QUY III-2026) (Bieu mau).docx` (Mẫu 09B, 09C, 9D) và PDF HD03 (tr.53–65); `docs/bieu-mau.md`; `Infrastructure/Documents/**`, `DocxTemplateEngine`, `WordTemplateStore`, `TemplateData` (tag `ORG_*`).
- Song song với task 19 (biểu mẫu tập thể & báo cáo) và task 20 (sau công bố) — tôn trọng phạm vi file.

## Vấn đề
Quý III/2026 phải nộp Mẫu 09B (phiếu tự chấm), 09C (bản tự đánh giá tự luận, tối đa 2 trang A4, Chi bộ xác nhận có đóng dấu), 9D (phụ lục kết quả nhiệm vụ theo trục T1–T6). Hệ thống chưa xuất các mẫu này; 09C và 9D chưa có chỗ nhập nội dung.

## Việc
1. **Dữ liệu (T-82):**
   - Hồ sơ đánh giá thêm nội dung **09C** theo đúng các mục của biểu mẫu gốc (đọc file .docx gốc để lấy đúng tên mục — ví dụ ưu điểm, hạn chế/khuyết điểm, nguyên nhân, phương hướng khắc phục… — không đoán), lưu `jsonb` theo mã mục để sau này đổi mẫu không cần đổi schema; kiểm tra độ dài hợp lý.
   - Nội dung **9D**: các dòng kết quả nhiệm vụ theo trục (mã trục lấy từ bộ tiêu chí của kỳ), đúng các cột của biểu mẫu gốc.
   - Nhập trong bước tự chấm (`B2_SELF_SCORE`) cùng form hiện có: chủ hồ sơ sửa được tới khi nộp; trả lại → sửa tiếp; kỳ khóa/đã công bố → chỉ đọc. Lịch sử hồ sơ ghi thay đổi.
   - Bộ tiêu chí/kỳ quyết định mẫu nào áp dụng: 09B khi `SelfScoreForm = 09B`, 09A khi `09A` (09A cũng xuất được nếu dữ liệu có); 09C, 9D áp dụng cho mọi hồ sơ (theo trích xuất mục 7) — đưa thành cờ cấu hình trong bộ tiêu chí (`requiredForms`) thay vì cứng.
2. **Xuất (T-83):** template Word **dựng từ đúng biểu mẫu gốc** (sao chép bố cục từ file .docx gốc, gắn content control/tag theo quy ước `docs/bieu-mau.md`, dùng tag `ORG_*` cho tên đơn vị) cho 09B, 09C, 9D (và 09A nếu làm được cùng lúc). Endpoint trong **controller mới** `RecordFormsController` (route `api/reports/docx/record/{recordId}/{formCode}`), `?format=pdf` như các mẫu hiện có, kiểm quyền `Export` trên hồ sơ qua guard. Template mới đăng ký vào danh mục quản lý biểu mẫu (task 17) để thay được qua giao diện.
3. **Giao diện:** trang chi tiết hồ sơ — nhóm nút xuất theo `requiredForms` của kỳ (bỏ nút cứng Mẫu 01/02/10 nếu không áp dụng cho kỳ; vẫn giữ khi áp dụng); form nhập 09C (các mục văn bản) và 9D (bảng theo trục) trong bước tự chấm.
4. Tài liệu `docs/bieu-mau.md`: tag của 09B/09C/9D.

## Phạm vi file
Chủ sở hữu: trường 09C/9D trên `EvaluationRecord` + cấu hình, phần tự chấm trong `EvaluationWorkflowService` (lưu 09C/9D), `requiredForms` trong bộ tiêu chí (`Domain/Evaluation/CriteriaSet*`), `Infrastructure/Documents/Forms/Mau09*`, `Mau9D*` (mới), `RecordFormsController` (mới), template mới trong `Infrastructure/Templates/Word/`, đăng ký template/DI trong `DocumentExtensions` (chỉ thêm dòng), frontend `components/evaluations/SelfScoreForm.tsx`, component mới cho 09C/9D, `app/evaluations/[recordId]/page.tsx` (khối nút xuất + form), `services/evaluationService.ts` (thêm hàm).
**Không** sửa: `ExportReportController`, `ReportService`, `CollectiveEvaluation*`, trang tập thể, trang báo cáo (task 19); thực thể/trang kiến nghị, kế hoạch, công khai, thông báo (task 20).

## Test
- Unit: validate 09C/9D; dữ liệu tag.
- Tích hợp: nhập 09C + 9D khi tự chấm → nộp → xuất 09B/09C/9D ra .docx hợp lệ, không còn tag thừa, có nội dung đã nhập và tên đơn vị từ cài đặt; người ngoài phạm vi → 403; trả lại → sửa được; đã công bố → không sửa được.

## Tiêu chí hoàn thành
Build 0 warning, test (integration 0 skip), `tsc`, `npm run build`. Không tạo migration (người điều phối gộp `InitialCreate`). Báo cáo có ảnh cấu trúc mục 09C/9D lấy từ biểu mẫu gốc.
