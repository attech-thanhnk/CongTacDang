# Quy tắc chung cho agent khắc phục

Áp dụng cho **mọi** task trong `docs/remediation/tasks/`. Nếu task file mâu thuẫn với file này, task file thắng.

## 1. Quy trình
1. Đọc `CLAUDE.md`, file này, task file được giao, và các mục tương ứng trong `FINDINGS.md`.
2. Làm việc trên **branch riêng** ghi trong task file (worktree riêng nếu chạy song song). Không làm trên `main`.
3. Commit nhỏ theo từng mã lỗi, message dạng `fix(T-01): chặn path traversal khi lưu file`. Không push, không force, không rewrite lịch sử.
4. Chạy **cổng kiểm tra** (mục 4) trước khi kết thúc.
5. Ghi báo cáo vào `docs/remediation/reports/<tên task>.md` theo `reports/_TEMPLATE.md`, commit báo cáo là commit cuối.

## 2. Phạm vi
- **Chỉ xử lý các mã lỗi được giao.** Thấy vấn đề khác → ghi vào mục "Phát hiện thêm" trong báo cáo, không tự sửa.
- **Không đổi logic nghiệp vụ** (quy tắc chấm điểm, luồng 5 bước, trần xếp loại, phạm vi xem hồ sơ). Các mục `B-xx` là `deferred`.
- **Không sửa** `docs/remediation/FINDINGS.md`, `RULES.md`, task file của agent khác.
- Không thêm thư viện ngoài nếu thư viện có sẵn trong .NET / Next.js làm được.

## 3. Phân chia file (tránh xung đột khi merge)

| Vùng | Chủ sở hữu | Ghi chú |
|---|---|---|
| `Application/Services/AuthService.cs`, `AttachmentService.cs`, `UserService.cs` (phần mật khẩu) | 01 | |
| `Api/Controllers/AuthController.cs`, `AttachmentController.cs` | 01 | |
| `Api/Middlewares/GlobalExceptionMiddleware.cs`, `Application/Common/Exceptions/*` (mới) | 01 | |
| `Infrastructure/Services/LocalFileStorageService.cs`, `Repositories/RefreshTokenRepository.cs` | 01 | |
| `Infrastructure/Data/DataSeeder.cs` — nội dung seed tài khoản/mật khẩu | 01 | 02 chỉ sửa phần điều phối gọi seeder |
| Entity `PartyMemberProfile`, `RefreshToken` | 01 | |
| `backend/tests/**`, thêm project test vào `CongTacDang.slnx` | 01 | Agent duy nhất tạo project test |
| `frontend/services/apiClient.ts`, `components/attachments/*`, `contexts/AuthContext.tsx`, `components/layout/AuthGuard.tsx`, trang `/change-password` | 01 | |
| `backend/src/**/*.csproj`, `Directory.Packages.props` | 02 | 01 được tạo csproj cho project test |
| `Infrastructure/Data/Migrations/**`, `CongTacDangDbContext.cs` | 02 | 01 chỉ sửa khối cấu hình RefreshToken/PartyMemberProfile |
| `GenericRepository.cs`, `SpecificRepositories.cs`, `IRepository.cs`, `IUnitOfWork` (mới) | 02 | |
| `EvaluationService.cs`, `CollectiveEvaluationService.cs`, `RoleService.cs`, `OrganizationService.cs` | 02 | 01 chỉ thay dòng `throw` (T-08) |
| Entity đánh giá (`EvaluationRecord/Task/Period`, `CollectiveEvaluationRecord`, `EvaluationMeeting`), `DtoModels.cs` | 02 | |
| `appsettings*.json`, `docker/**`, `.gitignore`, `.github/**` | 03 | |
| `frontend/next.config.mjs`, `frontend/middleware.ts` | 03 | |
| `docs/deployment.md` | 03 | |

**`Program.cs` là file dùng chung.** Mỗi agent đặt logic vào file extension riêng và chỉ thêm/sửa vài dòng gọi trong `Program.cs`:
- 01 → `Api/Extensions/SecurityExtensions.cs` (rate limiter, …)
- 02 → `Api/Extensions/PersistenceExtensions.cs` (DbContext, repository, UoW, migrate/seed)
- 03 → `Api/Extensions/HostingExtensions.cs` (config validation, Swagger, ForwardedHeaders, security headers)

Cần sửa file thuộc agent khác → **không sửa**, ghi vào mục "Cần phối hợp" trong báo cáo.

## 4. Cổng kiểm tra (bắt buộc)
```bash
dotnet build backend/CongTacDang.slnx      # 0 error, không thêm warning mới
dotnet test  backend/CongTacDang.slnx      # pass (nếu đã có project test)
cd frontend && npx tsc --noEmit            # pass
```
Task file có thể thêm bước kiểm tra riêng. Bước nào không chạy được (thiếu Docker, thiếu Postgres…) → ghi rõ trong báo cáo, **không** báo là đã qua.

## 5. Migration
- **Chỉ task 02** tạo EF migration.
- Task khác thay đổi schema → chỉ sửa entity/mapping và liệt kê ở mục "Thay đổi schema" trong báo cáo. Người điều phối sinh migration sau khi merge.

## 6. Cấu hình
- Không ghi secret thật vào file nào.
- Key config mới: dùng giá trị mặc định an toàn trong code, liệt kê ở mục "Key config mới" để task 03 / người điều phối bổ sung vào `appsettings` và `.env.example`.
