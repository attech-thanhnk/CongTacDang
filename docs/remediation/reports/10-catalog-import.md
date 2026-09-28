# Báo cáo: Task 10 — Danh mục tổ chức & khung import dữ liệu

- **Branch:** `feat/catalog-import` (fast-forward từ `8a64c85` trước khi sửa file đầu tiên)
- **Commit cuối:** commit chứa báo cáo này (commit code cuối: `d39a54e`)
- **Ngày:** 2026-09-28

## Kết quả theo mã lỗi

| Mã | Trạng thái | Tóm tắt thay đổi | File chính |
|---|---|---|---|
| T-62 | done | CRUD Phòng/đơn vị (`GET/POST /departments`, `GET/PUT/DELETE /departments/{id}`); Chi bộ rà lại cùng ràng buộc. Mã duy nhất không phân biệt hoa thường (lưu chữ hoa, kiểm tra cả bản ghi đã xóa mềm vì unique index giữ mã) → 409; ngừng hoạt động/hoạt động lại qua `PUT` (`isActive`); xóa bị chặn (409, nêu số cán bộ và số hồ sơ đánh giá, hướng dẫn chuyển cán bộ / chuyển "Ngừng hoạt động") khi còn cán bộ, hồ sơ đánh giá cá nhân, hồ sơ tập thể hoặc biên bản (Chi bộ). Thêm `SortOrder` cho cả hai; bỏ `AdministrativeDepartment.HeadId` (không nơi nào dùng). `SecretaryId/DeputySecretaryId` giữ, ghi chú chỉ để hiển thị biểu mẫu. Đọc: mọi người đã đăng nhập; ghi: `[RequirePermission(PermissionCodes.CatalogManage)]`. Lỗi nghiệp vụ ném `ValidationException/NotFoundException/ConflictException`, middleware trả `ApiResponse`. Frontend `app/catalog` + `services/catalogService.ts`. | `OrganizationService.cs`, `OrganizationController.cs`, `Organizations.cs`, `SpecificRepositories.cs` (chỉ `IOrganizationRepository`/`OrganizationRepository`), `DTOs/CatalogDtos.cs`, `DtoModels.cs` (chỉ `BranchDto`/`DepartmentDto`, bỏ `Create/UpdateBranchDto`) |
| T-64 | done (phần của task 10) | Khung import: `IImportDefinition<TRow>`, bộ xử lý không generic, đọc/ghi Excel ClosedXML (sheet "Dữ liệu" + "Hướng dẫn", data validation, ô dữ liệu định dạng Text), giới hạn `.xlsx` ≤ 5 MB ≤ 2.000 dòng, bỏ dòng trống, cắt khoảng trắng, chống formula injection ở tệp kết quả; phiên xem trước trong bộ nhớ 30 phút gắn người tạo, xác nhận một lần; **phân tích lại ngay trước khi ghi** trong transaction `IUnitOfWork` (có dòng lỗi → 400, không ghi dòng nào); 1 bản ghi audit tóm tắt/lần xác nhận; tệp kết quả chỉ trong bộ nhớ, tải một lần, hết hạn 30 phút, chỉ người tạo tải được. 3 loại: `departments`, `party-cells`, `users`. Quyền: `system.import` + quyền dữ liệu của loại. Frontend `app/imports` (loại lấy từ API, cột động) + `services/importService.ts`; 2 mục menu cuối `AppSidebar`. Phần gán vai trò / người được đánh giá của T-64 thuộc task 13/12. | `Application/Imports/**`, `Infrastructure/Imports/**`, `ImportController.cs`, `Api/Extensions/ImportExtensions.cs`, `DTOs/ImportDtos.cs`, `Program.cs` (1 dòng) |

**L6 — chọn "báo lỗi dòng"**: tên đăng nhập đã có (không phân biệt hoa thường, **kể cả tài khoản đã xóa mềm**) → dòng lỗi "đã có tài khoản…", không cho xác nhận. Lý do: (1) sửa tài khoản là việc của `UserService`/`UserAccountService` (task 08 đang viết lại: xóa cache quyền, `SecurityStamp`, nhật ký) — import tự sửa entity sẽ bỏ qua các bước đó; (2) đổi Phòng/Chi bộ của tài khoản đã có làm thay đổi phạm vi quyền (task 09) — không nên xảy ra ngầm khi nhập lại một tệp; (3) import cán bộ dùng cho khởi tạo, nhập lại nhầm tệp không được gây tác dụng phụ. Unique index `Username` giữ cả bản ghi đã xóa nên tên đã xóa cũng phải báo lỗi (nếu không sẽ 409 chung khi ghi).

## Luồng

| # | Luồng | Trạng thái | Test |
|---|---|---|---|
| L1 | Quản lý Phòng: thêm/sửa/ngừng hoạt động/xóa; mã duy nhất; không xóa được khi còn cán bộ hoặc hồ sơ (409 nêu số lượng) | pass | `CatalogImportIntegrationTests.L1_Departments_Crud_UniqueCode_Deactivate_DeleteBlockedWhileInUse` |
| L2 | Quản lý Chi bộ (như L1) | pass | `L2_PartyCells_Crud_DeleteBlockedWhileInUse` |
| L3 | Mẫu → tải lên → xem trước từng dòng → xác nhận → ghi 1 transaction → kết quả; có lỗi → không xác nhận, không ghi dòng nào | pass | `L3_L4_ImportDepartmentsAndPartyCells_TemplatePreviewCommit_CreateAndUpdateByCode`, `L3_OneErrorRow_BlocksCommit_AndNothingIsWritten`; unit `Commit_OnlyByCreator_Once_AndNotWhenRowsHaveErrors` |
| L4 | Import Phòng, Chi bộ theo mã: có → "update", chưa có → "create" | pass | `L3_L4_ImportDepartmentsAndPartyCells_…`; unit `Catalog_ExistingCode_IsUpdate_NewCode_IsCreate_CaseInsensitive` |
| L5 | Import cán bộ + tài khoản qua `IUserAccountService`, tham chiếu Phòng/Chi bộ bằng mã, tệp tài khoản tải 1 lần; đăng nhập bằng mật khẩu trong tệp → bị buộc đổi mật khẩu; tải lần 2 → 404 | pass | `L5_L6_ImportUsers_CreatesAccounts_ResultFileOnce_LoginForcesPasswordChange_ReimportIsRowError`; unit `Users_Commit_CreatesAccountsViaService_AndReturnsPasswordFile`, `ResultStore_TakeOnce_OwnerOnly_AndExpires` |
| L6 | Import lại cùng tệp cán bộ → lỗi dòng, tài khoản cũ không đổi | pass | `L5_L6_…` (phần cuối); unit `Users_InvalidReferences_Duplicates_AndBadValues_AreRowErrors` |

Unit test theo yêu cầu (`ImportFrameworkTests`, 24 test): thiếu cột (`ReadRows_MissingRequiredColumn_Throws400WithColumnName`), sai mã tham chiếu / Phòng đã xóa / Chi bộ ngừng hoạt động, trùng trong tệp, trùng CSDL (`Users_InvalidReferences_…`, `Catalog_DuplicateInFile_DeletedCode_BadValues_AreRowErrors`), formula injection (`SanitizeCellText_PrefixesFormulaTriggers` ×7, `ResultFile_EscapesFormulaInjection_AndStoresTextNotFormulas`), file mẫu (`Template_HasDataAndGuideSheets_…`), cắt khoảng trắng/bỏ dòng trống (`ReadRows_TrimsValues_…`), > 2.000 dòng, không phải xlsx, lọc loại theo quyền (`Kinds_AreFilteredByImportAndDataPermissions`), phiên hết hạn 30 phút.

## Cổng kiểm tra

| Bước | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build backend/CongTacDang.slnx` | pass | 0 warning, 0 error |
| `dotnet test` — unit | pass | 120 test: 119 pass, 1 skip (`PdfConversionTests.Mau01…` — thiếu LibreOffice, skip sẵn có), 0 fail |
| `dotnet test` — integration (có `CONGTACDANG_TEST_PG`, máy chủ 159) | pass | 18 test **đã chạy**: 18 pass, 0 skip, 0 fail (13 cũ + 5 mới của task 10) |
| `npx tsc --noEmit` | pass | |
| `npm run build` | pass | Không thêm cảnh báo ESLint (`next lint` trên `app/catalog`, `app/imports`, `services` sạch) |
| Chạy app / thử giao diện thủ công | không chạy | Theo 7.3 không chạy app vào CSDL dùng chung; giao diện chỉ kiểm tra bằng `tsc` + `build` |

Số lần gọi `/api/auth/login` của test task 10: **2** (client quản trị dùng chung cả lớp + 1 lần đăng nhập bằng mật khẩu tạm). Tổng test tích hợp hiện tại: 8/10 lần mỗi phút.

## Thay đổi schema (cần migration)

| Bảng | Thay đổi | Ghi chú |
|---|---|---|
| `administrative_departments` | thêm `SortOrder integer NOT NULL` (default 0) | dữ liệu cũ nhận 0 |
| `administrative_departments` | **xóa** `HeadId uuid NULL` | không có FK/index; không nơi nào đọc/ghi. Người đứng đầu xác định bằng gán vai trò phạm vi Phòng (task 09) |
| `party_cells` | thêm `SortOrder integer NOT NULL` (default 0) | dữ liệu cũ nhận 0 |

Không thêm bảng/index. `SecretaryId`, `DeputySecretaryId` giữ nguyên.

## Key config mới

Không có. Giới hạn (5 MB, 2.000 dòng, 30 phút) là hằng số `ImportLimits`.

## Thay đổi hành vi API / breaking change

| Phương thức | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/organizations/departments` | đăng nhập | Danh sách Phòng (thêm `sortOrder`, `isActive`); sắp theo `sortOrder`, rồi mã |
| GET | `/api/organizations/departments/{id}` | đăng nhập | Chi tiết (mới) |
| POST | `/api/organizations/departments` | `catalog.manage` | Thêm (mới). Body `SaveCatalogItemDto { code, name, description?, sortOrder?, isActive? }` |
| PUT | `/api/organizations/departments/{id}` | `catalog.manage` | Sửa (mới); trường null giữ nguyên |
| DELETE | `/api/organizations/departments/{id}` | `catalog.manage` | Xóa mềm (mới); 409 khi còn tham chiếu |
| GET | `/api/organizations/branches[/{id}]` | đăng nhập | Như cũ + `sortOrder`, `isActive`; **không còn cần `branches.read`** |
| POST/PUT/DELETE | `/api/organizations/branches[/{id}]` | `catalog.manage` | Body đổi sang `SaveCatalogItemDto` (tương thích body cũ `{code,name,description}` / `{name,description}`); `PUT` nhận thêm `code`, `sortOrder`, `isActive`; **xóa khi còn cán bộ: 400 → 409**, thêm chặn khi còn hồ sơ |
| GET | `/api/imports/kinds` | `system.import` | Các loại người dùng được dùng: `[{ kind, displayName, description, columns[] }]` (thiếu quyền dữ liệu → loại bị ẩn) |
| GET | `/api/imports/{kind}/template` | `system.import` + quyền loại | Tệp mẫu `.xlsx` |
| POST | `/api/imports/{kind}/preview` | `system.import` + quyền loại | multipart `file` → `{ sessionId, kind, fileName, expiresAt, canCommit, columns[], rows: [{ rowNumber, action: create\|update\|error, errors[], data{key:value} }], summary{ total, create, update, error } }`. Tệp sai định dạng/thiếu cột/quá giới hạn → 400 |
| POST | `/api/imports/{sessionId}/commit` | `system.import` + quyền loại, chỉ người tạo phiên | `{ created, updated, resultFileToken?, resultFileExpiresAt? }`. Phiên hết hạn/đã xác nhận → 404; người khác → 403; còn dòng lỗi (phân tích lại) → 400, không ghi |
| GET | `/api/imports/results/{token}` | `system.import`, chỉ người tạo | Tệp kết quả, `Cache-Control: no-store`; lần 2 / hết hạn / người khác → 404 |

Khác: `IOrganizationService` đổi chữ ký Create/Update sang `SaveCatalogItemDto`; `OrganizationService` nhận thêm `IUnitOfWork`. Trang `app/users` (tab Chi bộ cũ) vẫn chạy với API mới.

### Mô tả cột 3 file mẫu

Tiêu đề khớp không phân biệt hoa thường, bỏ dấu `*` cuối; cột thừa bị bỏ qua; cột không bắt buộc có thể không có trong tệp. Cột có "giá trị hợp lệ" được gắn data validation dạng danh sách trong file mẫu và được chuẩn hóa về đúng cách viết.

**`departments` — Danh mục Phòng/đơn vị** (quyền `catalog.manage`), **`party-cells` — Danh mục Chi bộ** (quyền `catalog.manage`): cùng cấu trúc, nhãn "Phòng" / "Chi bộ".

| Cột | Khóa | Bắt buộc | Quy tắc |
|---|---|---|---|
| Mã Phòng / Mã Chi bộ | `code` | có | Không khoảng trắng, ≤ 50 ký tự, lưu chữ hoa. Có (chưa xóa) → cập nhật; chưa có → tạo mới; thuộc bản ghi đã xóa → lỗi; trùng trong tệp → lỗi |
| Tên Phòng / Tên Chi bộ | `name` | có | ≤ 200 ký tự |
| Mô tả | `description` | không | Trống khi cập nhật → giữ nguyên |
| Thứ tự hiển thị | `sortOrder` | không | Số nguyên ≥ 0; trống: tạo mới 0, cập nhật giữ nguyên |
| Trạng thái | `status` | không | `Hoạt động` \| `Ngừng hoạt động`; trống: tạo mới Hoạt động, cập nhật giữ nguyên |

**`users` — Cán bộ và tài khoản** (quyền `system.users.manage`):

| Cột | Khóa | Bắt buộc | Quy tắc |
|---|---|---|---|
| Tên đăng nhập | `username` | có | Không khoảng trắng, ≤ 100 ký tự; trùng trong tệp hoặc đã có tài khoản (kể cả đã xóa, không phân biệt hoa thường) → lỗi |
| Họ và tên | `fullName` | có | ≤ 200 ký tự |
| Email | `email` | không | Có `@`, không khoảng trắng, ≤ 200 |
| Số thẻ Đảng | `partyCardNumber` | không | Có giá trị → là Đảng viên (ô dạng Text giữ số 0 đầu) |
| Chức danh | `positionTitle` | không | Trống → "Cán bộ" |
| Mã Phòng | `departmentCode` | không | Phải có trong danh mục, chưa xóa, đang hoạt động |
| Mã Chi bộ | `partyCellCode` | không | Như Mã Phòng |
| Thẩm quyền phê duyệt | `approvalAuthority` | có | `CoSo` \| `CapTren` |

Tệp kết quả `tai-khoan-moi-<yyyyMMdd-HHmm>.xlsx`, sheet "Tài khoản": dòng ghi chú bảo mật, rồi `STT | Tên đăng nhập | Họ và tên | Mật khẩu tạm`.

### Hướng dẫn thêm một loại import mới (task 12, 13)

1. **Kiểu dòng**: lớp `XxxImportRow` (mutable) chứa giá trị đã parse và chỗ để ghi tham chiếu đã phân giải (Id).
2. **Định nghĩa**: lớp triển khai `IImportDefinition<XxxImportRow>` (namespace `CongTacDang.Application.Imports`), đặt trong module của task (ví dụ `Application/Imports/Definitions/` hoặc thư mục module riêng):
   - `Kind` — mã kebab-case duy nhất, dùng trong URL (ví dụ `role-assignments`, `evaluation-subjects`).
   - `DisplayName`, `Description` — hiển thị ở trang Nhập dữ liệu và sheet "Hướng dẫn".
   - `TemplateColumns` — danh sách `ImportColumn(Key, Header, Required, Description, AllowedValues?, Example?)`. Khung tự kiểm tra ô bắt buộc trống và giá trị ngoài `AllowedValues` (không phân biệt hoa thường, chuẩn hóa về cách viết trong danh sách) **trước** `ParseRow`; `Key` là khóa trong `data` của bảng xem trước.
   - `RequiredPermissions` — quyền dữ liệu (ngoài `system.import`) người dùng phải có đủ, ví dụ `PermissionCodes.SystemAssignmentsManage`, `PermissionCodes.PeriodManage`.
   - `ParseRow(source, errors)` — đọc `source.Get(key)` / `GetOrNull(key)` (đã cắt khoảng trắng), kiểm tra riêng dòng, luôn trả đối tượng; lỗi thêm vào `errors` (tiếng Việt, nêu lý do + cách sửa).
   - `ValidateAsync(rows, ct)` — kiểm tra chéo: trùng trong tệp, trùng/khớp CSDL, mã tham chiếu tồn tại. Đặt `row.Action = ImportRowAction.Create | Update` cho dòng hợp lệ, `row.AddError(...)` cho dòng lỗi (khung tự chuyển dòng có lỗi thành `Error`). Dùng truy vấn **chỉ đọc**; được gọi cả lúc xem trước và ngay trước khi ghi (trong transaction). Cần tra cứu mới → thêm interface đọc riêng trong module của task (tham khảo `IImportLookup`), không sửa `IImportLookup`.
   - `CommitAsync(rows, ct)` — ghi các dòng (đều hợp lệ) qua repository/service; **không** mở transaction, **không** gọi `SaveChanges` bắt buộc (khung đã mở transaction và gọi `IUnitOfWork.SaveChangesAsync` sau khi hàm trả về; gọi service tự lưu như `IUserAccountService` vẫn nằm trong transaction). Ném ngoại lệ → rollback toàn bộ. Trả `ImportCommitResult(created, updated, resultFile?)`; `ImportResultTable` nếu cần giao tệp kết quả (tải một lần, chỉ bộ nhớ, tự chống formula injection).
3. **Đăng ký**: gọi `services.AddImportDefinition<XxxImportDefinition>();` (extension trong `CongTacDang.Api.Extensions.ImportExtensions`, scoped) từ extension của task mình (`EvaluationExtensions` — 12; task 13 có thể thêm một dòng vào `AddImports()` hoặc extension riêng) — **không** sửa `ImportController`, `ImportService`, trang `app/imports`.
4. **Test**: unit test gọi `new ImportProcessor<XxxImportRow>(definition).AnalyzeAsync(rows, ct)` với `ImportSourceRow(rowNumber, dict)` và fake tra cứu; test tích hợp tải tệp qua `POST /api/imports/{kind}/preview` rồi `POST /api/imports/{sessionId}/commit` (xem `CatalogImportIntegrationTests.BuildFile/PreviewAsync`).
5. Không cần sửa giao diện: loại mới xuất hiện ở `GET /api/imports/kinds` cho người có đủ quyền; bảng xem trước dựng cột từ `columns`.

## Cần phối hợp

- **Người điều phối:** sinh migration cho 3 thay đổi schema ở trên (`SortOrder` ×2, xóa `HeadId`).
- **Task 03 / người điều phối (`next.config.mjs`):** proxy rewrites của Next.js mặc định timeout 30 giây. Import nhiều cán bộ chậm vì BCrypt (~0,1–0,2 giây/tài khoản → 2.000 dòng có thể vài phút); cần `experimental.proxyTimeout` lớn hơn (ví dụ 600000) hoặc khuyến cáo nhập ≤ ~150 cán bộ/tệp. Frontend đã đặt timeout axios 10 phút cho bước xác nhận.
- **Task 08 (`IUserAccountService`):** import dựa vào hành vi v0: mỗi `CreateAsync` được phép gọi nhiều lần trong **một** transaction bên ngoài (không tự mở transaction riêng, không commit riêng), ném 409 khi trùng tên, trả mật khẩu tạm và đặt `MustChangePassword = true`. Import tự kiểm tra mã Phòng/Chi bộ và trùng tên (kể cả tài khoản đã xóa mềm) ở bước xem trước. Nếu bản mới tự mở transaction (ví dụ `ExecuteInTransactionAsync` lồng) cần giữ tương thích khi đã có transaction.
- **Task 08 (rate limit đăng nhập):** test task 10 dùng 2 lần `/api/auth/login`; tổng test tích hợp hiện 8/10 mỗi phút — khi gộp 08/09 nên cho phép cấu hình giới hạn trong môi trường Testing.
- **Task 09:** `ImportService` dùng `IPermissionResolver.GetAsync(...).Has(code)` để lọc loại/kiểm tra quyền (quyền không áp dụng phạm vi → cần grant Global). Unit test dựng `EffectivePermissions(userId, grants)` + `PermissionGrant(code, ScopeType.Global, null, Guid.Empty, "test")` trong `ImportFrameworkTests.FakeResolver` — đổi chữ ký thì sửa tại đó.
- **Task 11:** gỡ tab Chi bộ cũ trong `app/users/page.tsx` (vẫn chạy với API mới); có thể gom menu "Danh mục tổ chức" / "Nhập dữ liệu" vào khu quản trị.
- `CLAUDE.md` mục Cấu trúc nên thêm `app/catalog`, `app/imports`, `Application/Imports`, `Infrastructure/Imports` (không sửa trong task này).

## Phát hiện thêm

- Unique index `party_cells.Code` / `administrative_departments.Code` gồm cả bản ghi đã xóa mềm → mã của mục đã xóa không dùng lại được. Task này báo 409/lỗi dòng rõ ràng thay vì lỗi unique chung; nếu muốn tái dùng mã cần index lọc `WHERE NOT "IsDeleted"` (task 02/người điều phối). Mức ⚪.
- Phiên xem trước và tệp kết quả lưu trong bộ nhớ tiến trình: khởi động lại API → mất (người dùng tải lại tệp; mật khẩu tạm chưa tải phải đặt lại qua quản lý tài khoản). Chạy nhiều instance cần sticky session hoặc kho dùng chung. Mức ⚪.
- Tiền tố `'` chống formula injection được ClosedXML lưu theo chuẩn Excel (ô Text + cờ `quotePrefix`, không giữ ký tự `'` trong nội dung) — Excel/LibreOffice không coi là công thức và hiển thị đúng giá trị; nếu người dùng tự xuất CSV từ tệp kết quả thì tiền tố không còn. Mức ⚪.
- `GlobalExceptionMiddleware` ghi log mức Error cho mọi ngoại lệ, kể cả 400/404/409 nghiệp vụ (ví dụ xóa Phòng còn cán bộ) → log nhiễu. Đề xuất log Warning cho `AppException`. Mức ⚪.
