# Quản lý migration CSDL

API dùng EF Core Migrations với `Database:AutoMigrate`. Mặc định migration tự động bật ở Development và tắt ở Production. Khi bật, ứng dụng kiểm tra kết nối, baseline schema cũ nếu cần rồi gọi `MigrateAsync`; lỗi kết nối được ghi log mức Critical và làm ứng dụng dừng.

## CSDL mới

```bash
dotnet ef database update -p backend/src/CongTacDang.Infrastructure -s backend/src/CongTacDang.Api
```

Hoặc để API tự áp dụng khi `Database:AutoMigrate=true`.

## Baseline CSDL cũ

Schema được tạo trước đây bằng `EnsureCreated` không có `__EFMigrationsHistory`. Khi phát hiện các bảng nghiệp vụ đã tồn tại nhưng lịch sử migration chưa có bản ghi, API tạo bảng lịch sử và ghi `InitialCreate` là đã áp dụng, sau đó mới chạy các migration tiếp theo. API không chạy lại `InitialCreate` trên dữ liệu hiện hữu.

Có thể baseline thủ công sau khi kiểm tra schema tương thích:

```sql
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" varchar(150) NOT NULL,
    "ProductVersion" varchar(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260928071949_InitialCreate', '10.0.12')
ON CONFLICT ("MigrationId") DO NOTHING;
```

Không thực hiện baseline nếu schema chưa có đầy đủ các bảng và cột của `InitialCreate`; hãy dùng CSDL mới hoặc bổ sung migration chuyển đổi riêng.

## Kiểm tra

```bash
dotnet ef migrations script -p backend/src/CongTacDang.Infrastructure -s backend/src/CongTacDang.Api
```
