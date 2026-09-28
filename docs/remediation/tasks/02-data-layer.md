# Task 02 — Dữ liệu & Persistence

- **Branch:** `fix/data-layer`
- **Mã lỗi:** T-13, T-14, T-15, T-16, T-23, phần kiểm tra DB của T-19
- **Báo cáo:** `docs/remediation/reports/02-data-layer.md`
- Tuân thủ `docs/remediation/RULES.md`. Task duy nhất được tạo EF migration.

## Bối cảnh phối hợp
Task 01 chạy song song và sẽ: thêm cột `MustChangePassword`, `FailedLoginCount`, `LockoutEnd` (PartyMemberProfile) và `TokenHash` (RefreshToken); map `DbUpdateConcurrencyException` → 409 trong middleware. **Không tự làm lại** những việc này; migration của các cột đó sẽ được sinh sau khi merge.

## T-16 Đồng bộ phiên bản gói (làm trước tiên)
- Nâng EF Core, EF Core Design, Npgsql.EntityFrameworkCore.PostgreSQL, JwtBearer lên bản **10.x ổn định mới nhất**; Swashbuckle lên bản tương thích .NET 10 (hoặc chuyển sang `Microsoft.AspNetCore.OpenApi` nếu hợp lý — ghi rõ lựa chọn).
- Cân nhắc `backend/Directory.Packages.props` (Central Package Management).
- Sửa mọi breaking change; build sạch.

## T-13 EF Core Migrations
- Thay `EnsureCreatedAsync()` bằng Migrations; thư mục `Infrastructure/Data/Migrations`. Thêm `IDesignTimeDbContextFactory` nếu cần để `dotnet ef` chạy được mà không cần DB thật.
- Tạo migration `InitialCreate` từ model hiện tại.
- **Baseline cho DB đang chạy** (tạo bằng EnsureCreated, chưa có `__EFMigrationsHistory`): khi khởi động, nếu phát hiện bảng nghiệp vụ đã tồn tại mà chưa có lịch sử migration → tạo bảng lịch sử và ghi `InitialCreate` là đã áp dụng, sau đó mới Migrate. Mô tả trong `docs/database-migrations.md` (kèm cách làm thủ công bằng SQL).
- Khởi động: chỉ tự `Migrate` khi `Database:AutoMigrate=true` (mặc định true ở Development, false ở Production); không kết nối được DB → log và **dừng ứng dụng**, không nuốt exception.
- Tách điều phối seed: dữ liệu tham chiếu (role, permission) chạy mọi môi trường; dữ liệu mẫu chỉ khi `Database:SeedSampleData=true`. **Không sửa nội dung seed tài khoản/mật khẩu** (thuộc task 01) — chỉ tách lời gọi; nếu phải tách hàm trong `DataSeeder`, giữ nguyên thân hàm.
- Logic đặt trong `Api/Extensions/PersistenceExtensions.cs`.

## T-14 Unit of Work & transaction
- Thêm `IUnitOfWork` (Application) + cài đặt (Infrastructure): `SaveChangesAsync`, `ExecuteInTransactionAsync(Func<Task>)` dùng execution strategy của Npgsql.
- Áp dụng tối thiểu, **không viết lại toàn bộ repository**:
  - `EvaluationService.SetActivePeriodAsync` (đang update từng kỳ trong vòng lặp) → một transaction.
  - Rà `EvaluationService`, `CollectiveEvaluationService`, `UserService`, `RoleService`, `OrganizationService`: phương thức nào ghi ≥ 2 lần qua repository → bọc transaction. Liệt kê trong báo cáo.
  - Không đụng `AuthService`, `AttachmentService`, `RefreshTokenRepository` (task 01).
- Thêm unique partial index: chỉ một `EvaluationPeriod` có `IsActive = true`.

## T-15 Concurrency
- Optimistic concurrency bằng cột hệ thống `xmin` của PostgreSQL (theo cách Npgsql bản hiện tại khuyến nghị) cho: `EvaluationRecord`, `EvaluationTask`, `EvaluationPeriod`, `CollectiveEvaluationRecord`, `EvaluationMeeting`.
- Đưa `version` vào DTO đọc và DTO cập nhật tương ứng; khi update gán làm `OriginalValue` để người lưu sau nhận 409. Chỉ để `DbUpdateConcurrencyException` nổi lên (middleware do task 01 map).
- Báo cáo: danh sách API mà frontend cần gửi kèm `version`. **Không sửa frontend** trong task này.

## T-23 + T-19 (DB) Hiệu năng & health check
- `AsNoTracking` cho truy vấn chỉ đọc trong `SpecificRepositories.cs` (cẩn thận với chỗ đọc rồi sửa).
- Index cho cột lọc thường dùng còn thiếu (`MemberId`, `PartyCellId`, `PeriodId`, `Status`, …) — giải thích từng index.
- Health check: `AddDbContextCheck<CongTacDangDbContext>()`.

## Kiểm tra thêm
- `dotnet ef migrations script` sinh SQL hợp lệ.
- Nếu có Postgres (local hoặc `docker run postgres:16-alpine`): migrate trên DB trống **và** trên DB tạo bằng EnsureCreated từ `main` (baseline). Ghi kết quả vào báo cáo.
