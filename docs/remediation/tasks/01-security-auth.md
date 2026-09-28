# Task 01 — Bảo mật & Xác thực

- **Branch:** `fix/security-auth`
- **Mã lỗi:** T-01, T-02, T-03, T-04, T-08, T-09, T-10, T-11, T-12, T-17, và project test (một phần T-21)
- **Báo cáo:** `docs/remediation/reports/01-security-auth.md`
- Tuân thủ `docs/remediation/RULES.md`. Không tạo EF migration.

## T-01 Path traversal
- `AttachmentService.UploadAttachmentAsync`: `formCode` chỉ chấp nhận `^[A-Za-z0-9_-]{1,20}$`, sai → lỗi validation (400).
- `LocalFileStorageService`: gom logic dựng đường dẫn vào 1 hàm; dùng `Path.GetFullPath` và kiểm tra kết quả nằm trong `_storageRoot` (so sánh có dấu phân cách cuối, không phân biệt hoa thường trên Windows). Áp dụng cho Save/Get/Delete/Exists; vi phạm → ném exception.

## T-02 Stored XSS qua file đính kèm
- Bỏ ContentType do client gửi; suy ra từ đuôi file và **kiểm tra magic bytes**: PDF `%PDF`, PNG `89 50 4E 47`, JPEG `FF D8 FF`, DOCX/XLSX `50 4B 03 04`. Không khớp → từ chối.
- `AttachmentController`: header `X-Content-Type-Options: nosniff` cho download/view; `/view` chỉ trả `inline` cho pdf/png/jpeg, loại khác trả `attachment`; dùng `ContentDispositionHeaderValue` + `FileNameStar` thay vì tự ghép chuỗi.
- `frontend/components/attachments/DocumentViewerModal.tsx`: chỉ hiển thị iframe / `window.open(blobUrl)` khi MIME thuộc whitelist (pdf, png, jpeg); loại khác chỉ cho tải về.

## T-03 Đổi mật khẩu & mật khẩu mặc định
- Thêm `PartyMemberProfile.MustChangePassword` (bool). Seed và `UserService` tạo tài khoản mới → `true`.
- `POST /api/auth/change-password` (mật khẩu cũ, mới): tối thiểu 8 ký tự, có chữ và số, khác mật khẩu cũ; thành công → đặt `MustChangePassword=false`, thu hồi các refresh token khác của user.
- API admin đặt lại mật khẩu (quyền quản trị người dùng): sinh mật khẩu tạm ngẫu nhiên, trả về một lần, `MustChangePassword=true`.
- Login và `/api/auth/me` trả `mustChangePassword`.
- Frontend: trang `/change-password`; `AuthGuard` bắt buộc chuyển tới đó khi `mustChangePassword=true`; admin có nút đặt lại mật khẩu (nếu trang users cần sửa, chỉ thêm tối thiểu).

## T-04 Chống dò mật khẩu
- Rate limiter có sẵn của ASP.NET Core (`AddRateLimiter`) cho `/api/auth/login` và `/api/auth/refresh-token`, phân vùng theo IP; logic đặt trong `Api/Extensions/SecurityExtensions.cs`.
- Khóa tạm: thêm `FailedLoginCount`, `LockoutEnd` vào `PartyMemberProfile`; sai 5 lần → khóa 15 phút; đăng nhập đúng → reset.
- Username không tồn tại vẫn gọi `BCrypt.Verify` với hash giả để thời gian phản hồi đồng đều.

## T-08 + T-10 Exception → HTTP status
- Tạo `Application/Common/Exceptions/`: `AppException` (base), `ValidationException`→400, `ForbiddenException`→403, `NotFoundException`→404, `ConflictException`→409.
- Thay các `throw new UnauthorizedAccessException(...)` dùng để **kiểm tra quyền** trong Application service bằng `ForbiddenException` (trong `EvaluationService`/`CollectiveEvaluationService` chỉ được thay dòng `throw`). Giữ 401 cho lỗi xác thực.
- `GlobalExceptionMiddleware`:
  - `AppException` → status tương ứng + message.
  - Giữ tương thích tạm: `ArgumentException`→400, `KeyNotFoundException`→404.
  - `DbUpdateConcurrencyException` → 409 "Dữ liệu đã được người khác cập nhật, vui lòng tải lại." (task 02 dựa vào mapping này).
  - `DbUpdateException` với `PostgresException.SqlState == "23505"` → 409.
  - `InvalidOperationException` và mọi exception khác → 500, message chung; chỉ Development mới kèm chi tiết.
- Rà `frontend/services/apiClient.ts` và các trang: không còn chỗ nào dựa vào 401 để hiểu là thiếu quyền; 403 hiển thị thông báo, không đăng xuất.

## T-09 Refresh token
- Lưu SHA-256 (hex) của token vào cột `TokenHash` (unique index) thay cho token gốc; cookie vẫn chứa token gốc.
- `RefreshTokenRepository.RotateAsync(oldToken, newToken)`: revoke cũ + thêm mới trong **một** `SaveChanges`.
- Grace period 30 giây: token đã bị thay thế trong vòng 30 giây (nhiều tab refresh cùng lúc) → trả 401 nhưng **không** thu hồi toàn bộ phiên. Quá 30 giây → coi là tái sử dụng, thu hồi toàn bộ.
- Cookie `refresh_token` đặt `Path=/api/auth` (nhớ cả lúc xóa cookie). Kiểm tra `frontend/middleware.ts` (thuộc task 03) đang đọc cookie `refresh_token` để quyết định chuyển hướng — nếu đổi Path làm hỏng logic đó, ghi vào "Cần phối hợp" và đề xuất cách xử lý.

## T-11, T-12, T-17
- T-11: bỏ `catch (Exception)` trả `ex.Message` trong `UploadFile`; để middleware xử lý.
- T-12: `GetDownloadUrlAsync` trả URL tới endpoint có thật (`/api/attachments/{id}/download`), điều chỉnh interface/chữ ký nếu cần.
- T-17: tính SHA-256 theo stream (`IncrementalHash`) khi ghi file, không `ToArray()` cả file.

## Project test (một phần T-21)
- Tạo `backend/tests/CongTacDang.UnitTests` (xUnit), thêm vào `backend/CongTacDang.slnx`.
- Test tối thiểu: path traversal bị chặn; magic bytes; mapping exception → status; rotation + grace period; lockout.

## Kiểm tra thêm
- Thử thủ công (hoặc test) upload `formCode=../../x` → 400; file `.pdf` chứa HTML → bị từ chối.
