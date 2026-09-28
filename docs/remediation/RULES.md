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

## 7. Đợt 2 (task 04, 05, 06)

Mục 3 (bảng phân chia file) và mục 5 (migration) ở trên áp dụng cho đợt 1. Đợt 2 dùng quy định sau.

### 7.1 Thứ tự chạy
- **04 ║ 06** chạy song song. **05** chạy sau khi 04 đã merge vào `main`.
- Mỗi agent làm trong **worktree riêng** của branch ghi trong task file. Kiểm tra `git branch --show-current` trước khi sửa file đầu tiên; sai branch → dừng và báo.

### 7.2 Phân chia file

| Vùng | Chủ sở hữu |
|---|---|
| Cơ chế quyền theo đối tượng (mới), `EvaluationService`/`CollectiveEvaluationService` (chỉ phần kiểm tra quyền) | 04 |
| `AttachmentService.cs`, `AttachmentController.cs`, `TaskAttachment.cs` | 04 → sau đó 05 |
| `ExportReportController.cs`, `UserController.cs`, `OrganizationController.cs`, `UserService.cs` | 04 |
| `DataSeeder.cs` — phần đồng bộ quyền của role | 04 |
| `DataSeeder.cs` — khối DDL `ExecuteSqlRaw` | 06 |
| `frontend/app/evaluations/page.tsx`, `EvaluationStepNav.tsx` — chỉ mã quyền | 04 |
| `frontend/services/evaluationService.ts`, `components/evaluations/Step*.tsx`, `EvaluationPeriodHeader.tsx`, `app/collective-evaluations/page.tsx` — chỉ luồng `version`/409 | 06 |
| `DocxTemplateEngine.cs`, `ReportService.cs`, `Infrastructure/Templates/**`, `EvaluationPrintTemplate.tsx`, `EvaluationPdfModal.tsx`, file storage | 05 |
| `HostingExtensions.cs`, `Program.cs` (logging), `docker/**`, `docs/deployment.md`, `.gitattributes` | 06 (05 được thêm LibreOffice vào `Dockerfile.backend` và mục cài đặt vào `deployment.md`) |
| `backend/tests/**` | mỗi task thêm file test riêng, không sửa file test của task khác |

### 7.3 Môi trường
- **Không** chạy ứng dụng hay migration vào CSDL dùng chung (`192.168.22.159`). `user-secrets` của máy đang trỏ tới CSDL thử nghiệm của người điều phối — nếu cần chạy app, dùng biến môi trường trỏ tới CSDL khác hoặc không chạy.
- **Không** dùng cổng 5000 và 3001 (người điều phối đang chạy). Cần chạy thử thì dùng 5100–5199 / 3100–3199.
- **Không** tạo EF migration; thay đổi schema liệt kê trong báo cáo, người điều phối sinh migration sau khi merge.
- **Không** tự đặt luật nghiệp vụ mới (xem `docs/nghiep-vu/` — chưa xác nhận, không dùng làm căn cứ).

## 8. Chương trình nền tảng chuẩn (Đợt 3–5, task 07–13)

Mục 8 thay mục 3, 5, 7.1, 7.2 cho task 07–13. Mục 7.3 (môi trường) vẫn áp dụng.

### 8.1 Căn cứ và thứ tự
- Căn cứ thiết kế: `docs/thiet-ke/phan-quyen.md`, `docs/thiet-ke/luong-danh-gia.md`. Task file chi tiết hơn thiết kế → theo task file; mâu thuẫn → dừng, ghi "Cần phối hợp".
- Riêng task 12 **được** hiện thực thứ tự bước và tác nhân theo `docs/thiet-ke/luong-danh-gia.md` (đã được duyệt làm căn cứ kỹ thuật), nhưng **không** đổi công thức điểm/ngưỡng xếp loại.

| Đợt | Task (agent) | Điều kiện bắt đầu |
|---|---|---|
| 3 | 07 (B) | `main` hiện tại |
| 4 | 08 (A) ║ 09 (B) ║ 10 (D) | 07 đã merge |
| 5 | 11 (C) ║ 12 (E) ║ 13 (D) | 08, 09, 10 đã merge |

Tạo branch từ `main` **tại thời điểm bắt đầu** đợt; kiểm tra `git branch --show-current` trước khi sửa file đầu tiên.

### 8.2 Phân chia file
Mỗi task file có mục "Phạm vi file" — đó là danh sách chủ sở hữu. Quy tắc chung:
- **Entity mới** cấu hình trong `Infrastructure/Data/Configurations/<Entity>Configuration.cs` (task 07 bật `ApplyConfigurationsFromAssembly`); không sửa `OnModelCreating` trừ khi gỡ cấu hình cũ của entity mình sở hữu.
- **DTO mới** trong file riêng `Application/DTOs/<Module>Dtos.cs`; chỉ sửa DTO cũ thuộc module mình.
- **Repository mới** trong file riêng; `SpecificRepositories.cs` chỉ sửa đúng lớp repository thuộc module mình.
- **`Program.cs`**: mỗi task đăng ký service trong extension riêng (`AccountExtensions`/`SecurityExtensions` — 08, `AuthorizationExtensions` — 07/09, `ImportExtensions` — 10, `EvaluationExtensions` — 12) và chỉ thêm 1 dòng gọi.
- **`AppSidebar.tsx`**: Đợt 4 — 09 chỉ đổi mã quyền, 10 chỉ thêm mục cuối danh sách. Đợt 5 — chỉ 11 sửa.
- **`DataSeeder.cs`**: 07 → 09 (vai trò, quyền, gán). Đợt 5 chỉ 12 sửa phần seed dữ liệu đánh giá.
- **Test**: mỗi task tạo file test riêng (tên nêu trong task file); không sửa file test của task khác. Được sửa **kỳ vọng** của test cũ chỉ khi task file yêu cầu đổi hành vi đó, ghi rõ trong báo cáo.
- Cần sửa file ngoài phạm vi → **không sửa**, ghi "Cần phối hợp".

### 8.3 Migration
- Agent **không** tạo migration. Người điều phối sau mỗi đợt: merge → `dotnet ef migrations add Wave<N>` → `has-pending-model-changes` sạch → cổng kiểm tra.
- Test tích hợp dùng `EnsureCreated` trên CSDL tạm nên chạy được trước khi có migration.

### 8.4 Cổng kiểm tra (bổ sung mục 4)
```bash
dotnet build backend/CongTacDang.slnx
dotnet test  backend/CongTacDang.slnx          # unit + integration (integration skip nếu thiếu CONGTACDANG_TEST_PG)
cd frontend && npx tsc --noEmit && npm run build
```
- Test tích hợp chạy trên máy chủ PostgreSQL `192.168.22.159`: `CONGTACDANG_TEST_PG` trỏ tới CSDL quản trị `postgres` bằng tài khoản có quyền `CREATEDB` (người điều phối cấp, không ghi vào file nào trong repo). Test chỉ tạo/xóa CSDL tên `ctd_it_*`; **tuyệt đối không** đọc/ghi `congtacdang_test` hay CSDL khác. Mục 7.3 (không chạy app/migration vào CSDL dùng chung) vẫn giữ nguyên.
- Báo cáo ghi số test tích hợp **đã chạy** và **đã skip** — skip không được tính là pass.
- Mỗi luồng (L/U/W…) trong task file phải xuất hiện trong báo cáo với trạng thái pass / fail / không chạy + tên test.

### 8.5 Ngôn ngữ và thông báo
- Thông báo lỗi trả người dùng: tiếng Việt, nói rõ **vì sao** và **cần làm gì** (ví dụ "Không thể xóa Phòng Kỹ thuật vì còn 12 cán bộ. Hãy chuyển cán bộ sang phòng khác trước.").
- 403 do thiếu quyền nêu tên quyền hiển thị (không nêu mã kỹ thuật).
