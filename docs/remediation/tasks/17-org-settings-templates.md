# Task 17 — Cài đặt đơn vị và quản lý biểu mẫu trên giao diện (Đợt 7)

- **Agent:** I · **Branch:** `feat/org-settings-templates`
- **Mã:** T-80, T-81
- **Báo cáo:** `docs/remediation/reports/17-org-settings-templates.md`
- **Đọc trước:** `CLAUDE.md`, `RULES.md` 7.3 + 8, `docs/bieu-mau.md`, `Infrastructure/Documents/**`, `Infrastructure/Services/ReportService.cs`, `DocxTemplateEngine.cs`, `docs/remediation/reports/05-files-docs.md`.
- Chạy song song với task 16 (bộ tiêu chí) — tôn trọng phạm vi file.

## Vấn đề
- Tên đơn vị ghi cứng trong code (ví dụ `"ĐẢNG BỘ CÔNG TY TNHH KỸ THUẬT QUẢN LÝ BAY"`, `"…ATTECH"`, tên file `DanhSach_CanBo_ATTECH.xlsx`).
- File mẫu Word nằm trên máy chủ, muốn thay phải chép file tay; không có phiên bản, không kiểm tra tag.

## 1. Cài đặt đơn vị (T-80)
- Bảng cài đặt (một bản ghi hoặc key–value có kiểu, chọn và ghi lý do): tên Đảng bộ (dòng tiêu đề trái biểu mẫu), tên tổ chức Đảng cấp trên, tên đầy đủ công ty, tên viết tắt (dùng trong tên file), địa danh (dòng "…, ngày … tháng … năm …"), tên người/đơn vị lập báo cáo mặc định nếu biểu mẫu có, và mọi chuỗi đơn vị khác đang ghi cứng — **rà toàn bộ** `backend/src` (`ReportService`, `Documents/**`, `DataSeeder`, controller tên file, `HostingExtensions` Swagger title…) và `frontend` (tiêu đề trang, `layout.tsx`, trang in) để đưa vào cài đặt hoặc bỏ tên riêng.
- Quyền mới `system.settings.manage`. API `GET /api/settings/organization` (mọi người đã đăng nhập đọc được phần hiển thị), `PUT` (quyền trên). Cache, xóa khi sửa.
- Seed giá trị mặc định = đúng chuỗi đang ghi cứng hiện nay (để biểu mẫu không đổi), chỉ khi chưa có.
- Excel (Mẫu 14–16, danh sách cán bộ) và Word dùng cài đặt; tên file tải xuống dùng tên viết tắt.

## 2. Quản lý file mẫu Word (T-81)
- Quyền mới `system.templates.manage`. Trang quản trị liệt kê từng biểu mẫu (mã, tên, phiên bản đang dùng, người/lúc cập nhật): tải file đang dùng, tải file mẫu gốc, **tải lên phiên bản mới**, kích hoạt lại phiên bản cũ, xem lịch sử.
- Khi tải lên: chỉ `.docx`, kiểm tra magic bytes, mở được bằng OpenXML, **kiểm tra tag**: liệt kê tag có trong file, báo tag không nhận diện (lỗi) và tag bắt buộc bị thiếu (cảnh báo) theo danh mục tag của từng mẫu (lấy từ `TemplateData`/`Forms/*Data.cs`); thử sinh tài liệu với dữ liệu giả trước khi cho kích hoạt.
- Lưu file qua `IFileStorageService` (không ghi vào thư mục ứng dụng), metadata trong CSDL; `WordTemplateStore` đọc phiên bản đang kích hoạt từ CSDL, không có thì dùng file gốc đi kèm ứng dụng.
- Tài liệu `docs/bieu-mau.md`: danh mục tag từng mẫu, cách sửa template trong Word mà không làm vỡ tag.

## 3. Giao diện
- `app/admin/settings/**` (thông tin đơn vị), `app/admin/templates/**` (biểu mẫu). Thêm mục menu trong nhóm Quản trị theo quyền.

## Phạm vi file
Chủ sở hữu: entity/cấu hình cài đặt và template mới, `WordTemplateStore`, `DocxTemplateEngine`, `TemplateData` (phần thông tin đơn vị/tag chung), `ReportService` (tiêu đề, tên đơn vị, tên file — không phần điểm), controller/service cài đặt + template, `HostingExtensions` (chuỗi tên riêng), `PermissionCodes` (thêm 2 mã), `DataSeeder` (cài đặt mặc định + gán 2 quyền cho vai trò Quản trị hệ thống), `docs/bieu-mau.md`, frontend `app/admin/settings/**`, `app/admin/templates/**`, `components/layout/AppSidebar.tsx` (chỉ thêm mục), `app/layout.tsx` (tiêu đề), service FE mới.
**Không** sửa: `Documents/Forms/Mau01Data.cs`, `Mau02Data.cs`, `Mau10Data.cs` (nội dung điểm — task 16; nếu cần thêm tag đơn vị thì thêm ở `TemplateData` dùng chung), `Domain/Evaluation/**`, `app/criteria/**`, `app/periods/**`, `components/evaluations/**`.

## Test
- Unit: validate tag (thừa/thiếu), chọn phiên bản đang kích hoạt, fallback file gốc.
- Tích hợp: đổi tên Đảng bộ → Excel Mẫu 14 và Word Mẫu 02 xuất ra có tên mới; tải lên template thiếu tag bắt buộc → cảnh báo, tag lạ → từ chối; tải bản mới → xuất dùng bản mới; kích hoạt lại bản cũ → dùng bản cũ; người không có quyền → 403.

## Tiêu chí hoàn thành
- Grep `KỸ THUẬT QUẢN LÝ BAY|ATTECH` trong `backend/src` chỉ còn trong giá trị seed mặc định của cài đặt (và dữ liệu mẫu), không còn trong logic xuất biểu mẫu / tên file.
- Build 0 warning, test (integration 0 skip), `tsc`, `npm run build`. Không tạo migration.
- Báo cáo: danh sách chuỗi đã đưa vào cài đặt, danh mục tag từng mẫu, API mới.
