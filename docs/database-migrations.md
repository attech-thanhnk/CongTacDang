# Quản lý migration CSDL

API dùng EF Core Migrations với `Database:AutoMigrate`. Mặc định migration tự động bật ở Development và tắt ở Production. Khi bật, ứng dụng gọi `MigrateAsync` (tự tạo CSDL nếu chưa có); lỗi kết nối được ghi log mức Critical và làm ứng dụng dừng.

Migration là **nguồn schema duy nhất**: không dùng `EnsureCreated`, seeder không chạy DDL. Hệ thống không hỗ trợ nâng cấp CSDL tạo từ phiên bản trước khi có migration — triển khai luôn bắt đầu từ CSDL trống.

Lịch sử migration được gộp thành **một** migration `InitialCreate` sinh từ model hiện tại (chưa có CSDL triển khai thật nên không giữ bước chuyển dữ liệu nào; gộp lại lần gần nhất sau đợt 8 — nội dung Mẫu 09C/9D và phần tự luận theo trục trên hồ sơ (`SelfAssessment`, `TaskResults`, `AxisNotes` jsonb — `AxisNotes` đã bỏ, xem mục "Migration có xóa dữ liệu"), mục Mẫu 07 (`collective_evaluation_records.Sections`) và Mẫu 12/13 (`evaluation_meetings.Details`, jsonb mặc định `'{}'`), phiếu "Chưa đánh giá, xếp loại" (`VotesNotRated`), cột 13 Mẫu 14 (`evaluation_records.CadreWorkProposal`), bản nháp báo cáo (`report_drafts`, unique `(PeriodId, PartyCellId, FormCode)` `NULLS NOT DISTINCT`), kiến nghị (`evaluation_appeals`) và kế hoạch 30-60-90 ngày (`improvement_plans`); đợt 7 — thêm bộ tiêu chí theo phiên bản (`criteria_sets`, ảnh chụp bộ trong kỳ, điểm theo mã tiêu chí/trục, khung tỷ trọng), thông tin đơn vị (`organization_settings`) và phiên bản file mẫu Word (`word_template_versions`); đợt 6: cây đơn vị, loại đơn vị, chức vụ/kiêm nhiệm, diện quyết định, kết quả của cấp trên). Ngoài phần EF sinh, `InitialCreate` tạo thêm bằng SQL index unique `IX_party_member_profiles_Username_lower` trên `lower("Username")` (tên đăng nhập duy nhất không phân biệt hoa thường) — index biểu thức không khai báo được trong model nên `has-pending-model-changes` không thấy. Migration mới sau này thêm nối tiếp như bình thường.

Test tích hợp dựng CSDL tạm `ctd_it_*` (tạo từ `template0`) bằng chính migration (`MigrateAsync`), không dùng `EnsureCreated`.

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

## Migration có xóa dữ liệu

- `20261023095959_BoChamTheoTruc09B` — bỏ cách chấm theo trục (Mẫu 09B), hệ thống chỉ còn chấm theo sản phẩm đăng ký. **Xóa hẳn**
  các kỳ gắn bộ tiêu chí 09B (theo `CriteriaSetId` hoặc `selfScoreForm` trong ảnh chụp bộ của kỳ) cùng mọi dữ liệu phụ thuộc: hồ sơ,
  nhiệm vụ, lịch sử, kết quả cấp trên, kiến nghị, kế hoạch 30-60-90 ngày, đề nghị điều chỉnh việc, biên bản hội nghị và kết quả
  kiểm phiếu, hồ sơ tập thể và dòng của hồ sơ, bản nháp báo cáo. Tệp đính kèm của các dữ liệu này chuyển `OwnerType = 'Unlinked'`
  để tác vụ dọn tệp chưa gắn (`UnlinkedAttachmentCleanupService`) xóa bản ghi và tệp sau thời gian giữ. Xóa bộ tiêu chí 09B và
  bản ghi phiên bản file mẫu `MAU_09B` (tệp trong kho, thư mục `word-templates/MAU_09B/`, không tự xóa — xóa tay nếu cần), bỏ `selfScoreForm` khỏi ảnh chụp bộ của các kỳ còn lại, rồi xóa cột `criteria_sets.SelfScoreForm`,
  `evaluation_records.AxisScores`/`AxisNotes`/`SelfScoreForm`, `form_layouts.HeaderStyle`/`ShowCellConfirmation`. `Down` chỉ thêm
  lại cột, không khôi phục dữ liệu — cần dữ liệu cũ thì khôi phục bản sao lưu **trước** khi nâng cấp. Sao lưu CSDL và thư mục tệp
  trước khi chạy bản có migration này.

## Khôi phục bản sao lưu

Khi `Database:AutoMigrate=true`, khôi phục bản sao lưu của phiên bản cũ rồi khởi động bản mới sẽ tự nâng cấp schema; bản sao lưu mới hơn mã nguồn thì mã nguồn không chạy được. `docker/restore.sh` so sánh `__EFMigrationsHistory` trong bản sao lưu với migration của mã nguồn trước khi ghi đè — xem `docs/deployment.md`, mục "Khôi phục sao lưu".
