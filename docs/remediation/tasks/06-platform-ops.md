# Task 06 — Hoàn thiện nền dữ liệu, frontend & vận hành

- **Branch:** `chore/platform-ops`
- **Mã lỗi:** T-24, T-26, T-29, T-39, T-40, T-41, phần test của T-42
- **Báo cáo:** `docs/remediation/reports/06-platform-ops.md`
- Chạy song song với task 04. Tuân thủ `RULES.md` mục 7.

## T-24 Frontend gửi `version`
- Các DTO đọc đã có `version` (EvaluationPeriod/Record/Task, CollectiveEvaluationRecord, EvaluationMeeting). Frontend giữ `version` từ response và gửi lại trong mọi lệnh cập nhật tương ứng (`frontend/services/evaluationService.ts`, `components/evaluations/Step*.tsx`, `EvaluationPeriodHeader.tsx`, `app/collective-evaluations/page.tsx`).
- Khi nhận 409: hiển thị thông báo "Dữ liệu đã được người khác cập nhật" và tải lại dữ liệu; không mất dữ liệu người dùng đang nhập nếu có thể (giữ form, chỉ làm mới phần đọc).
- Sửa **tối thiểu** trong các component Step (chỉ luồng version/409); không đổi giao diện hay logic tính điểm.

## T-26 DDL cũ trong `DataSeeder`
- Xóa khối `ExecuteSqlRaw` tạo/đổi tên bảng và `ALTER TABLE … ADD COLUMN IF NOT EXISTS` — migration là nguồn schema duy nhất.
- Giữ nguyên nội dung seed dữ liệu (role, permission, người dùng mẫu). **Không** sửa phần đồng bộ quyền của role (thuộc task 04 — T-33); nếu hai phần nằm sát nhau, chỉ xóa khối DDL.

## T-29 `.gitattributes`
- Thêm `.gitattributes` (`* text=auto eol=lf`; `*.ps1`, `*.cmd`, `*.bat` → `eol=crlf`; `*.docx`, `*.xlsx`, `*.pdf`, ảnh → `binary`).
- **Không** chạy `git add --renormalize` trên toàn repo trong task này (sẽ đụng mọi file, gây xung đột với task khác) — ghi lệnh vào báo cáo để người điều phối chạy sau khi merge.

## T-39 Warning build
- `HostingExtensions.cs`: dùng `KnownIPNetworks` / `System.Net.IPNetwork` thay API obsolete.
- `DocxTemplateEngine.cs`: **không sửa** (task 05 viết lại file này). Ghi chú trong báo cáo.

## T-40 Log
- Log có cấu trúc ra console **và** file xoay vòng theo ngày (Serilog hoặc provider có sẵn — chọn một, lý do trong báo cáo), giữ tối đa N ngày theo cấu hình, thư mục log cấu hình được (mặc định ngoài thư mục publish).
- Correlation id cho mỗi request (nhận `X-Request-Id` nếu có, không thì sinh mới; trả lại trong response header; gắn vào mọi dòng log của request).
- Không log mật khẩu, token, cookie.
- Cấu hình Docker: mount thư mục log thành volume.

## T-41 Sao lưu / khôi phục
- Script `docker/backup.sh` và `docker/restore.sh` (kèm bản `.ps1` cho Windows nếu hợp lý): `pg_dump` định dạng custom + nén thư mục upload, đặt tên theo thời gian, giữ N bản gần nhất; restore từ một bản chỉ định (yêu cầu xác nhận trước khi ghi đè).
- Cập nhật `docs/deployment.md`: lịch sao lưu khuyến nghị, cách khôi phục, cách kiểm tra bản sao lưu.
- Thử script bằng `bash -n` / chạy khô; **không** chạy vào CSDL thật (RULES mục 7).

## Test (T-42)
- Test cho middleware correlation id.
- Test frontend: không bắt buộc; nếu thêm, dùng công cụ nhẹ và ghi vào CI.
