# CongTacDang — Hệ thống đánh giá cán bộ Đảng bộ ATTECH

Số hóa quy trình đánh giá, xếp loại cán bộ quản lý hằng quý (5 bước, 16 biểu mẫu) theo Hướng dẫn 03-HD/TVĐU.
Triển khai on-premise trong mạng LAN nội bộ. Tài liệu gốc trong `docs/`.

## Stack
- **Backend:** ASP.NET Core .NET 10, Clean Architecture, EF Core + Npgsql (PostgreSQL 16), JWT trong HttpOnly cookie.
- **Frontend:** Next.js 14 App Router + TypeScript (strict), axios; `/api/*` được Next.js rewrites chuyển tới backend (BFF).
- **Xuất biểu mẫu:** OpenXML (`DocxTemplateEngine`, template trong `Infrastructure/Templates/Word`) và ClosedXML (Excel).
- **Hạ tầng:** Docker Compose trong `docker/`.

## Cấu trúc
```
backend/CongTacDang.slnx
backend/src/
  CongTacDang.Domain/          Entities, Enums — không phụ thuộc project nào
  CongTacDang.Application/     Services nghiệp vụ, DTOs, interfaces (repository, storage, jwt), Security (roles/permissions)
  CongTacDang.Infrastructure/  DbContext, Repositories, DataSeeder, file storage, report/docx
  CongTacDang.Api/             Controllers, Middlewares, JwtService, Program.cs
frontend/
  app/          Các trang (evaluations, collective-evaluations, attachments, users, reports, audit, forms, login)
  components/   evaluations/Step1..Step5, attachments, layout, common
  services/     apiClient.ts + service theo module
  contexts/     AuthContext, LayoutContext, ToastContext
docker/         Dockerfile.backend, Dockerfile.frontend, docker-compose.yml
docs/remediation/  Danh sách lỗi, quy tắc và nhiệm vụ khắc phục (xem RULES.md)
```

## Lệnh thường dùng
```bash
dotnet build backend/CongTacDang.slnx
dotnet test  backend/CongTacDang.slnx
dotnet run --project backend/src/CongTacDang.Api
cd frontend && npm run dev          # cổng 3001
cd frontend && npx tsc --noEmit
cd frontend && npm run build
docker compose -f docker/docker-compose.yml config
```

## Quy ước
- Comment, thông báo lỗi và tài liệu viết bằng tiếng Việt; tên định danh trong code bằng tiếng Anh.
- API trả về `ApiResponse` / `ApiResponse<T>` (`Application/Common/Models`).
- Phân quyền: policy theo claim `perm` (`AppPermissions`) + role (`AppRoles`); kiểm tra phạm vi dữ liệu (Chi bộ, chủ hồ sơ) trong Application service.
- Entity hỗ trợ xóa mềm (`ISoftDeletable`) và audit (`IAuditableEntity`); audit log được ghi tự động trong `CongTacDangDbContext.SaveChangesAsync`.
- Không commit secret, file build (`publish*/`, `.next*/`, `.vs/`) hay file upload.
