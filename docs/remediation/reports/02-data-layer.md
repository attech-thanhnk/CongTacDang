# Báo cáo: Dữ liệu & Persistence

- **Branch:** `fix/security-auth`
- **Commit cuối:** `84d576d` (commit code; commit báo cáo là commit cuối của task)
- **Ngày:** 2026-09-28

## Kết quả theo mã lỗi

| Mã | Trạng thái | Tóm tắt thay đổi | File chính |
|---|---|---|---|
| T-13 | done | Thay `EnsureCreatedAsync` bằng Migrations, baseline schema cũ, dừng ứng dụng khi lỗi DB, tách seed sample và thêm design-time factory. | `Api/Extensions/PersistenceExtensions.cs`, `Infrastructure/Data/Migrations/*` |
| T-14 | done | Thêm `IUnitOfWork` dùng execution strategy Npgsql; bọc các luồng đánh giá nhiều lần ghi và cập nhật RBAC nhiều bước trong transaction. | `Application/Common/Interfaces/IUnitOfWork.cs`, `Application/Services/EvaluationService.cs`, `Infrastructure/Repositories/SpecificRepositories.cs` |
| T-15 | done | Dùng `xmin` làm optimistic concurrency token cho 5 entity; truyền `version` qua DTO và đặt `OriginalValue` trước update. | `Infrastructure/Data/CongTacDangDbContext.cs`, `Application/DTOs/DtoModels.cs` |
| T-16 | done | Đồng bộ EF Core/Design/JWT Bearer `10.0.12`, Npgsql `10.0.3`, Swashbuckle `10.2.3`; cập nhật API OpenAPI namespace tương thích. | `*.csproj`, `Api/Program.cs` |
| T-23 | done | Thêm `AsNoTracking` cho truy vấn chỉ đọc, bổ sung index lọc thường dùng và DB health check. | `Infrastructure/Repositories/SpecificRepositories.cs`, `Infrastructure/Data/CongTacDangDbContext.cs` |
| T-19 (DB) | done | `/healthz` dùng `AddDbContextCheck<CongTacDangDbContext>()`. | `Api/Extensions/PersistenceExtensions.cs` |

Các phương thức ghi từ hai lần trở lên đã được bọc transaction: `EvaluationService.SetActivePeriodAsync`, `RegisterTasksAsync`, `SubmitSelfScoreAsync`, `SubmitBranchReviewAsync`, `SubmitBranchMeetingAsync`, `SubmitAppraisalAsync`, `ApproveFinalGradeAsync`; `RoleRepository.UpdateRolePermissionsAsync` và `AssignRolesToUserAsync`. Các thao tác tạo/cập nhật chỉ có một lần lưu của `UserService`, `OrganizationService`, `CollectiveEvaluationService` không bọc thêm transaction.

## Cổng kiểm tra

| Bước | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build` | pass | 0 lỗi, 8 warning có sẵn ở `DocxTemplateEngine`/`HostingExtensions`, không phát sinh từ task 02 |
| `dotnet test` | pass | 6/6 test; lần chạy cuối dùng `--no-build` sau khi build tuần tự |
| `npx tsc --noEmit` | pass | Không có lỗi TypeScript |
| `dotnet ef migrations script --idempotent` | pass | Sinh được SQL hợp lệ; `xmin` không bị tạo như cột vật lý |
| PostgreSQL migrate DB trống/baseline | không chạy | Môi trường không có Docker và không có PostgreSQL khả dụng |

## Thay đổi schema (cần migration)

- Tạo `InitialCreate` cho schema persistence hiện tại trong `Infrastructure/Data/Migrations`.
- Thêm unique partial index `evaluation_periods(IsActive) WHERE IsActive = TRUE`.
- Thêm index cho `MemberId`, `PartyCellId`, `DepartmentId`, `Status`, `PeriodId`, `RecordId`, `AttachmentId` và các khóa lọc/sắp xếp liên quan.
- Các entity `EvaluationRecord`, `EvaluationTask`, `EvaluationPeriod`, `CollectiveEvaluationRecord`, `EvaluationMeeting` dùng cột hệ thống PostgreSQL `xmin`; không tạo cột vật lý mới.
- Migration `InitialCreate` cố ý chưa gồm các cột hardening của task 01 (`MustChangePassword`, `FailedLoginCount`, `LockoutEnd`, `TokenHash` và các trường token liên quan). Migration task 01 phải được sinh sau khi merge.

## Key config mới

| Key | Mặc định | Ý nghĩa |
|---|---|---|
| `Database:AutoMigrate` | `true` ở Development, `false` ở Production | Cho phép API tự gọi EF `MigrateAsync` khi khởi động |
| `Database:SeedSampleData` | `false` | Bật seed dữ liệu mẫu; role/permission vẫn seed mọi môi trường |

## Thay đổi hành vi API / breaking change

- Các DTO đọc `EvaluationPeriod`, `EvaluationRecord`, `EvaluationTask`, `CollectiveEvaluationRecord`, `EvaluationMeeting` có thêm trường `version`.
- Các API cập nhật hồ sơ phải gửi `version`: đăng ký nhiệm vụ khi cập nhật hồ sơ, tự chấm, chi bộ review, branch meeting từng phiếu, appraisal và approve final.
- API kỳ đánh giá `PUT /api/evaluations/periods/{id}/activate` và `PUT /api/evaluations/periods/{id}/status` nhận thêm query `version`.
- Version sai hoặc bản ghi đã bị cập nhật sẽ phát sinh `DbUpdateConcurrencyException`, được middleware task 01 ánh xạ thành HTTP 409.
- Frontend chưa được sửa theo yêu cầu task; cần truyền `version` lấy từ response trước mỗi lần ghi.

## Cần phối hợp

- Sau khi merge task 01, sinh migration `AuthHardening` cho các cột xác thực/token đã loại khỏi `InitialCreate`.
- Task 03 bổ sung các key `Database:*` vào cấu hình triển khai và `.env.example` nếu cần; code đã có mặc định an toàn.
- Cần chạy kiểm thử migrate trên PostgreSQL trống và database legacy tạo bằng `EnsureCreated` trong môi trường có PostgreSQL.

## Phát hiện thêm

- `DataSeeder` vẫn còn khối DDL tương thích schema cũ; nên loại bỏ sau khi migration task 01 được hợp nhất để migration là nguồn thay đổi schema duy nhất. Không tự sửa vì phần nội dung seed/schema này đang thuộc vùng phối hợp task 01.
