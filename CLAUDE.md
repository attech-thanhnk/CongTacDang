# CongTacDang — Hệ thống đánh giá cán bộ Đảng bộ ATTECH

Số hóa quy trình đánh giá, xếp loại cán bộ quản lý hằng quý (5 bước, 16 biểu mẫu) theo Hướng dẫn 03-HD/TVĐU.
Triển khai on-premise trong mạng LAN nội bộ. Tài liệu gốc trong `docs/`.

## Stack
- **Backend:** ASP.NET Core .NET 10, Clean Architecture, EF Core + Npgsql (PostgreSQL 16), JWT trong HttpOnly cookie.
- **Frontend:** Next.js 14 App Router + TypeScript (strict), axios; `/api/*` được Next.js rewrites chuyển tới backend (BFF).
- **Xuất biểu mẫu:** OpenXML (`DocxTemplateEngine`, template trong `Infrastructure/Templates/Word`), ClosedXML (Excel), LibreOffice headless (PDF).
- **Hạ tầng:** Docker Compose trong `docker/`. File đính kèm lưu trên đĩa (`LocalFileStorageService`).

## Cấu trúc
```
backend/CongTacDang.slnx
backend/src/
  CongTacDang.Domain/          Entities, Enums, máy trạng thái đánh giá — không phụ thuộc project nào
  CongTacDang.Application/     Services nghiệp vụ, Accounts, Imports (khung import), DTOs, interfaces,
                               Common/Security (PermissionCodes, IPermissionResolver, IAuthorizationGuard)
  CongTacDang.Infrastructure/  DbContext, Data/Configurations, Data/Migrations, Repositories, DataSeeder,
                               file storage, Imports (Excel), Documents/report/docx
  CongTacDang.Api/             Controllers, Authorization (RequirePermission), Middlewares, Extensions, Program.cs
backend/tests/
  CongTacDang.UnitTests/
  CongTacDang.IntegrationTests/  API thật trên PostgreSQL (CSDL tạm ctd_it_*)
frontend/
  app/          Trang: work-queue, evaluations, periods, collective-evaluations, attachments, reports, forms,
                catalog, imports, admin (users, roles), audit, login, change-password
  components/   admin, evaluations, attachments, layout, common
  services/     apiClient.ts + service theo module
  contexts/     AuthContext (permissions + grants), LayoutContext, ToastContext
docker/         Dockerfile.backend, Dockerfile.frontend, docker-compose.yml, backup/restore
docs/thiet-ke/     Thiết kế phân quyền động và luồng đánh giá (căn cứ kỹ thuật)
docs/remediation/  Danh sách lỗi (FINDINGS.md), quy tắc (RULES.md), task và báo cáo
```

## Lệnh thường dùng
```bash
dotnet build backend/CongTacDang.slnx
dotnet test  backend/CongTacDang.slnx     # test tích hợp tự skip nếu thiếu CONGTACDANG_TEST_PG
CONGTACDANG_TEST_PG='Host=192.168.22.159;Port=5432;Database=postgres;Username=ctd_it;Password=<...>' \
  dotnet test backend/CongTacDang.slnx    # chạy cả test tích hợp (chỉ tạo/xóa CSDL ctd_it_*)
dotnet run --project backend/src/CongTacDang.Api
dotnet ef migrations add <Ten> -p backend/src/CongTacDang.Infrastructure -s backend/src/CongTacDang.Api -o Data/Migrations
cd frontend && npm run dev          # cổng 3001
cd frontend && npx tsc --noEmit
cd frontend && npm run build
docker compose -f docker/docker-compose.yml config
```

## Quy ước
- Comment, thông báo lỗi và tài liệu viết bằng tiếng Việt; tên định danh trong code bằng tiếng Anh.
- API trả về `ApiResponse` / `ApiResponse<T>` (`Application/Common/Models`).
- **Phân quyền** (chi tiết: `docs/thiet-ke/phan-quyen.md`):
  - Chỉ dùng mã quyền trong `PermissionCodes`. **Không** kiểm tra theo tên vai trò (không `IsInRole`, không mã vai trò trong code/frontend).
  - Vai trò, quyền của vai trò và bản gán (người + vai trò + phạm vi Global/Department/PartyCell + thời hạn) lưu trong CSDL, quản trị qua giao diện.
  - Quyền được tính lại mỗi request qua `IPermissionResolver` (cache, xóa sau commit) — JWT chỉ chứa danh tính + `sstamp`.
  - Controller: `[RequirePermission]` / `[RequireAnyPermission]` (có quyền ở phạm vi nào đó). Service **bắt buộc** kiểm tra trên đối tượng bằng `IAuthorizationGuard.Ensure`, lọc danh sách bằng `GetScope`.
  - Frontend: `hasPermission` / `hasPermissionIn` từ `AuthContext`; nút thao tác hồ sơ đánh giá hiển thị theo API `actions`, không tự suy luật.
- **Luồng đánh giá:** bước và trạng thái theo `docs/thiet-ke/luong-danh-gia.md`; bật/tắt bước và tham số nằm trong cấu hình kỳ (`EvaluationPeriod.Settings`). Không đổi công thức điểm khi chưa được nghiệp vụ xác nhận.
- **CSDL:** migration là nguồn schema duy nhất; entity mới cấu hình trong `Infrastructure/Data/Configurations/`.
- Entity hỗ trợ xóa mềm (`ISoftDeletable`) và audit (`IAuditableEntity`); audit log được ghi tự động trong `CongTacDangDbContext.SaveChangesAsync`.
- Máy chủ PostgreSQL `192.168.22.159` dùng chung: test chỉ tạo/xóa CSDL `ctd_it_*`, không đụng CSDL khác.
- Không commit secret, file build (`publish*/`, `.next*/`, `.vs/`) hay file upload.
