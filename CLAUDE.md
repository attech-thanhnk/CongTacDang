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
  CongTacDang.Application/     Services nghiệp vụ, Accounts, DTOs, interfaces,
                               Common/Security (PermissionCodes, IPermissionResolver, IAuthorizationGuard)
  CongTacDang.Infrastructure/  DbContext, Data/Configurations, Data/Migrations, Repositories, DataSeeder,
                               file storage, Documents/report/docx
  CongTacDang.Api/             Controllers, Authorization (RequirePermission), Middlewares, Extensions, Program.cs
backend/tests/
  CongTacDang.UnitTests/
  CongTacDang.IntegrationTests/  API thật trên PostgreSQL (CSDL tạm ctd_it_*)
frontend/
  app/          Trang: work-queue, evaluations, periods, collective-evaluations, attachments, reports, forms,
                catalog, criteria, admin (users, roles, settings, templates), audit, login, change-password
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
  - Vai trò, quyền của vai trò và bản gán (người + vai trò + phạm vi Global / đơn vị chính quyền / tổ chức Đảng + thời hạn) lưu trong CSDL, quản trị qua giao diện. Phạm vi đơn vị **bao trùm mọi đơn vị con** trong cây.
  - Quyền được tính lại mỗi request qua `IPermissionResolver` (cache, xóa sau commit) — JWT chỉ chứa danh tính + `sstamp`.
  - Controller: `[RequirePermission]` / `[RequireAnyPermission]` (có quyền ở phạm vi nào đó). Service **bắt buộc** kiểm tra trên đối tượng bằng `IAuthorizationGuard.Ensure`, lọc danh sách bằng `GetScope`.
  - Frontend: `hasPermission` / `hasPermissionIn` từ `AuthContext`; nút thao tác hồ sơ đánh giá hiển thị theo API `actions`, không tự suy luật.
- **Tổ chức:** đơn vị chính quyền (`AdministrativeDepartment`) và tổ chức Đảng (`PartyCell`) là hai cây nhiều cấp (`ParentId`, `Path`, loại đơn vị trong `org_unit_types`); chức vụ là danh mục `positions` (mã thống kê M1–M26), cán bộ giữ nhiều chức vụ qua `member_positions` (kiêm nhiệm); thẩm quyền phê duyệt (CoSo/CapTren) suy ra từ chức vụ, ghi đè được có lý do. Không dùng enum chức vụ.
- **Luồng đánh giá:** bước và trạng thái theo `docs/thiet-ke/luong-danh-gia.md`. Cấu hình kỳ (`EvaluationPeriod.Settings`) gồm các **hồ sơ luồng** theo nhóm đối tượng: mỗi bước `Internal` (kèm mã quyền thực hiện) / `External` (cấp trên thực hiện, ghi nhận kết quả) / `Off`; mỗi hồ sơ đánh giá gắn một hồ sơ luồng. Kiểm tra kẹt luồng (`readiness`) trước khi mở kỳ.
- **Chấm điểm:** nội dung chấm (nhóm/tiêu chí con, trục, khung tỷ trọng, ngưỡng mức xếp loại, làm tròn, trần Xuất sắc, ngưỡng giải trình) nằm trong **bộ tiêu chí** (`criteria_sets`, có phiên bản, bản Published bất biến); kỳ chọn một bộ và chụp lại khi mở kỳ. Điểm hồ sơ lưu theo mã tiêu chí/trục (jsonb). Chỉ **cấu trúc** công thức A/B/C/D nằm trong code (`EvaluationScoring`) — không đổi khi chưa được nghiệp vụ xác nhận.
- **Thông tin đơn vị** (tên Đảng bộ, công ty, tên viết tắt, địa danh) lấy từ cài đặt đơn vị; **file mẫu Word** quản lý phiên bản qua giao diện (kiểm tra tag khi tải lên). Không ghi cứng tên đơn vị trong code.
- **CSDL:** migration là nguồn schema duy nhất; entity mới cấu hình trong `Infrastructure/Data/Configurations/`.
- Entity hỗ trợ xóa mềm (`ISoftDeletable`) và audit (`IAuditableEntity`); audit log được ghi tự động trong `CongTacDangDbContext.SaveChangesAsync`.
- Máy chủ PostgreSQL `192.168.22.159` dùng chung: test chỉ tạo/xóa CSDL `ctd_it_*`, không đụng CSDL khác.
- Không commit secret, file build (`publish*/`, `.next*/`, `.vs/`) hay file upload.
