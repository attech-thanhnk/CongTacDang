# Báo cáo: Bảo mật & Xác thực

- **Branch:** `fix/security-auth`
- **Commit cuối:** commit chứa báo cáo này
- **Ngày:** 2026-09-28

## Kết quả theo mã lỗi

| Mã | Trạng thái | Tóm tắt thay đổi | File chính |
|---|---|---|---|
| T-01 | done | Kiểm tra `formCode` theo allowlist và canonical path nằm trong storage root cho mọi thao tác file. | `AttachmentService.cs`, `LocalFileStorageService.cs` |
| T-02 | done | Bỏ MIME client, suy ra MIME và kiểm tra magic bytes; download/view có `nosniff`, disposition an toàn; frontend chỉ preview PDF/PNG/JPEG. | `AttachmentService.cs`, `AttachmentController.cs`, `DocumentViewerModal.tsx` |
| T-03 | done | Thêm cờ bắt buộc đổi mật khẩu, đổi mật khẩu, reset mật khẩu tạm ngẫu nhiên một lần, trả cờ trong login/me và route frontend. | `AuthService.cs`, `UserService.cs`, `AuthController.cs`, `change-password/page.tsx` |
| T-04 | done | Rate limit login/refresh theo IP; khóa sau 5 lần sai trong 15 phút; BCrypt hash giả cho username không tồn tại. | `SecurityExtensions.cs`, `AuthService.cs` |
| T-08 | done | Exception quyền nghiệp vụ trả 403; frontend không đăng xuất khi 403. | `ForbiddenException.cs`, `GlobalExceptionMiddleware.cs`, `EvaluationService.cs` |
| T-09 | partial | Token lưu SHA-256, rotation một lần lưu, grace period 30 giây và cookie path `/api/auth`. Cần task 02 đồng bộ migration hiện tại đang còn cột token cũ. | `RefreshToken.cs`, `RefreshTokenRepository.cs`, `AuthController.cs` |
| T-10 | done | Exception không xác định trả 500 và thông báo chung; concurrency/unique violation trả 409. | `GlobalExceptionMiddleware.cs` |
| T-11 | done | Bỏ catch tổng quát làm lộ `ex.Message` trong upload. | `AttachmentController.cs` |
| T-12 | done | URL tải xuống trỏ tới `/api/attachments/{id}/download`. | `IFileStorageService.cs`, `LocalFileStorageService.cs` |
| T-17 | done | Tính SHA-256 bằng `IncrementalHash` khi stream được ghi, không `ToArray()` toàn file. | `AttachmentService.cs` |
| T-21 (phần test) | done | Thêm project xUnit vào solution với test path traversal, magic bytes, exception mapping, rotation/grace và lockout. | `backend/tests/CongTacDang.UnitTests` |

## Cổng kiểm tra

| Bước | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build` | pass | 0 error, 8 warning; warning đã có ở `DocxTemplateEngine` và deprecation `KnownNetworks` của phần hosting. |
| `dotnet test` | pass | 7/7 test pass. |
| `npx tsc --noEmit` | pass | Không có lỗi TypeScript. |
| Bước riêng của task | pass | Unit test bao phủ upload `formCode=../../x` và file PDF chứa HTML/magic bytes sai; không chạy thử HTTP/PostgreSQL thật. |

## Thay đổi schema (cần migration)
- `party_member_profiles`: `MustChangePassword`, `FailedLoginCount`, `LockoutEnd`.
- `refresh_tokens`: `TokenHash` unique, `ReplacedByTokenHash`, `RevokedAt`; `Token` chỉ còn giá trị tạm trong bộ nhớ.
- Task 01 không tạo migration theo quy tắc. Migration `InitialCreate` hiện có của task 02 cần được sinh lại/điều chỉnh để khớp các cột trên.

## Key config mới
| Key | Mặc định | Ý nghĩa |
|---|---|---|
| Không có | — | Rate limit dùng giá trị an toàn cố định trong `SecurityExtensions`. |

## Thay đổi hành vi API / breaking change
- `POST /api/attachments/upload` không còn dùng `Content-Type` do client gửi; MIME được suy ra từ phần mở rộng và magic bytes.
- `formCode` chỉ chấp nhận `^[A-Za-z0-9_-]{1,20}$`.
- Login và `/api/auth/me` trả thêm `mustChangePassword`.
- Thêm `POST /api/auth/change-password` và `POST /api/users/{id}/reset-password`.
- `refresh_token` cookie có `Path=/api/auth`; xóa cookie cũng dùng path này.
- `GetDownloadUrlAsync` trả endpoint download theo attachment id.

## Cần phối hợp
- `frontend/middleware.ts` hiện đọc `refresh_token` trên các route trang. Với `Path=/api/auth`, cookie không được gửi khi truy cập route trang; cần task 03 sửa logic middleware để không phụ thuộc refresh cookie ngoài `/api/auth`.
- Task 02 cần đồng bộ migration refresh token với model hash trước khi chạy `MigrateAsync` trên CSDL mới.

## Phát hiện thêm
- Không có phát hiện mới ngoài các điểm phối hợp nêu trên.
