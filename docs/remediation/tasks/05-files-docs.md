# Task 05 — Nền tảng file & sinh biểu mẫu

- **Branch:** `feat/files-docs`
- **Mã lỗi:** T-36, T-37, T-38, phần còn lại của T-07, phần test của T-42
- **Báo cáo:** `docs/remediation/reports/05-files-docs.md`
- **Chạy SAU khi task 04 đã merge** (dùng cơ chế quyền của task 04). Tuân thủ `RULES.md` mục 7.

## Nguyên tắc
Xây **năng lực nền**, không đổi nội dung nghiệp vụ của biểu mẫu. Nội dung/bố cục từng mẫu, công thức tính điểm và danh sách mẫu cần có sẽ được chốt sau. Mục tiêu: khi nghiệp vụ chốt, thêm hoặc sửa một mẫu chỉ là **thêm file template + một lớp dữ liệu**, không sửa bộ máy.

## T-36 Mô hình file
- Một file gắn với **đối tượng tổng quát** (`OwnerType` + `OwnerId`, ví dụ `EvaluationTask`, `EvaluationRecord`, `General`), cho phép **nhiều file** trên một đối tượng. Giữ tương thích dữ liệu cũ (`EvaluationTask.AttachmentId`, `RecordId`) — mô tả cách chuyển đổi trong báo cáo; không xóa cột cũ trong task này.
- **Phiên bản:** thay file = tạo phiên bản mới, giữ bản cũ (đánh dấu không hiện hành), lưu người và thời điểm. Xóa = xóa mềm.
- Kiểm tra quyền qua cơ chế của task 04 cho mọi thao tác.
- Lưu trữ: giữ `IFileStorageService` + `LocalFileStorageService`. **Xóa** `MinioFileStorageService` (không dùng, không ký SigV4) và các cấu hình MinIO còn lại trong code; ghi đề xuất trong báo cáo nếu sau này cần object storage.
- API: danh sách file của một đối tượng, tải phiên bản hiện hành, xem lịch sử phiên bản, tải một phiên bản cụ thể.

## T-37 Bộ sinh Word
- Thay cơ chế thay chuỗi `{{TAG}}` theo run bằng cách không bị Word tách run: ưu tiên **Content Controls** (SDT, định danh bằng Tag) qua OpenXML SDK có sẵn; hoặc một thư viện templating .NET mã nguồn mở, chạy offline, giấy phép phù hợp (ghi rõ lựa chọn và giấy phép).
- Template là **file .docx thật** trong `Infrastructure/Templates/Word/`, không sinh phôi bằng code. Bỏ các hàm `CreateMasterMau*Template`. Chuyển 5 template hiện có (01, 02, 10, 11, 13) sang cơ chế mới, giữ **nguyên bố cục và nội dung** hiện tại.
- Hỗ trợ: trường đơn, bảng lặp dòng, khối điều kiện ẩn/hiện.
- Kiến trúc: mỗi mẫu = một lớp "dữ liệu mẫu" (DTO) được dựng từ dữ liệu đã lưu + một file template. Chữ in cứng (giờ họp, khối, tên đơn vị…) lấy từ dữ liệu hoặc cấu hình, không nằm trong code.
- Tài liệu `docs/bieu-mau.md`: cách thêm một mẫu mới (tạo template, đặt Tag, viết lớp dữ liệu).

## T-38 Một đường hiển thị & xuất PDF
- **Một nguồn số liệu:** mọi con số trên biểu mẫu lấy từ giá trị đã lưu (ví dụ điểm nhiệm vụ = `SelfScore` đã lưu), không tính lại trong tầng xuất. Sửa chỗ Mẫu 02 tính lại theo trọng số Khung 2.
- **PDF phía máy chủ, chạy trong mạng nội bộ:** chuyển DOCX → PDF bằng LibreOffice headless (gọi tiến trình `soffice --headless --convert-to pdf`, có timeout, thư mục tạm riêng, dọn file sau khi xong). Đường dẫn `soffice` lấy từ cấu hình; thiếu LibreOffice → trả lỗi rõ ràng, không làm hỏng xuất Word.
- Thêm LibreOffice (kèm font tiếng Việt, ví dụ Times New Roman tương thích) vào `docker/Dockerfile.backend`; ghi yêu cầu cài đặt khi chạy không dùng Docker vào `docs/deployment.md`.
- API xuất nhận tham số định dạng (`docx` | `pdf`).
- Frontend: thay bản in HTML (`EvaluationPrintTemplate.tsx`, `EvaluationPdfModal.tsx` dùng `window.print`) bằng xem/tải PDF từ máy chủ. Giữ các nút hiện có, chỉ đổi nguồn.

## Test (T-42)
- Test điền template: trường đơn, bảng lặp, trường bị Word tách run (tạo tài liệu mẫu có run bị tách).
- Test phiên bản file (thay, xem lịch sử, xóa mềm) và kiểm tra quyền.
- Test chuyển PDF: bỏ qua (skip có lý do) khi máy không có LibreOffice.

## Kiểm tra thêm
- Schema mới chỉ sửa entity + mapping, **không** tạo migration; liệt kê trong báo cáo.
- Xuất thử đủ 5 mẫu hiện có ra DOCX (và PDF nếu máy có LibreOffice), so sánh với bản xuất cũ, ghi khác biệt vào báo cáo.
