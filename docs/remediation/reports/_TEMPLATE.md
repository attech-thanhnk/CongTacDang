# Báo cáo: <tên task>

- **Branch:** `<branch>`
- **Commit cuối:** `<hash>`
- **Ngày:** YYYY-MM-DD

## Kết quả theo mã lỗi

| Mã | Trạng thái | Tóm tắt thay đổi | File chính |
|---|---|---|---|
| T-xx | done / partial / skipped | … | … |

Mỗi mục `partial` / `skipped`: nêu lý do và phần còn lại.

## Cổng kiểm tra

| Bước | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build` | pass / fail / không chạy | số warning |
| `dotnet test` | pass / fail / không chạy | số test |
| `npx tsc --noEmit` | pass / fail / không chạy | |
| Bước riêng của task | … | |

## Thay đổi schema (cần migration)
- Bảng / cột / index / kiểu dữ liệu — hoặc "Không có".

## Key config mới
| Key | Mặc định | Ý nghĩa |
|---|---|---|

## Thay đổi hành vi API / breaking change
- Endpoint, status code, trường request/response thay đổi mà frontend hoặc người dùng cần biết.

## Cần phối hợp
- Việc cần agent khác / người điều phối làm (file thuộc chủ sở hữu khác, bước sau merge…).

## Phát hiện thêm
- Vấn đề mới thấy nhưng ngoài phạm vi — đề xuất mã mới, mức độ, vị trí.
