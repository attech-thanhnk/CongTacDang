# Quản lý migration CSDL

API dùng EF Core Migrations với `Database:AutoMigrate`. Mặc định migration tự động bật ở Development và tắt ở Production. Khi bật, ứng dụng gọi `MigrateAsync` (tự tạo CSDL nếu chưa có); lỗi kết nối được ghi log mức Critical và làm ứng dụng dừng.

Migration là **nguồn schema duy nhất**: không dùng `EnsureCreated`, seeder không chạy DDL. Hệ thống không hỗ trợ nâng cấp CSDL tạo từ phiên bản trước khi có migration — triển khai luôn bắt đầu từ CSDL trống.

Lịch sử migration được gộp thành **một** migration `InitialCreate` sinh từ model hiện tại (chưa có CSDL triển khai thật nên không giữ bước chuyển dữ liệu nào). Ngoài phần EF sinh, `InitialCreate` tạo thêm bằng SQL index unique `IX_party_member_profiles_Username_lower` trên `lower("Username")` (tên đăng nhập duy nhất không phân biệt hoa thường) — index biểu thức không khai báo được trong model nên `has-pending-model-changes` không thấy. Migration mới sau này thêm nối tiếp như bình thường.

Test tích hợp dựng CSDL tạm `ctd_it_*` bằng chính migration (`MigrateAsync`), không dùng `EnsureCreated`.

## Lệnh `dotnet ef`

`dotnet ef` dùng `DesignTimeDbContextFactory`, đọc chuỗi kết nối từ biến môi trường `ConnectionStrings__Default` (không đọc `user-secrets`). Ví dụ với chuỗi kết nối lưu trong `user-secrets`:

```bash
export ConnectionStrings__Default="$(cd backend/src/CongTacDang.Api && dotnet user-secrets list | sed -n 's/^ConnectionStrings:Default = //p')"
```

## CSDL mới

```bash
dotnet ef database update -p backend/src/CongTacDang.Infrastructure -s backend/src/CongTacDang.Api
```

Hoặc để API tự áp dụng khi `Database:AutoMigrate=true`.

## Thêm migration

```bash
dotnet ef migrations add <TenMigration> -p backend/src/CongTacDang.Infrastructure -s backend/src/CongTacDang.Api -o Data/Migrations
dotnet ef migrations has-pending-model-changes -p backend/src/CongTacDang.Infrastructure -s backend/src/CongTacDang.Api
```

Đọc lại file migration sinh ra trước khi commit (EF cảnh báo khi có thao tác có thể mất dữ liệu).

## Kiểm tra

```bash
dotnet ef migrations script -p backend/src/CongTacDang.Infrastructure -s backend/src/CongTacDang.Api
```
