# Báo cáo: Task 13 — Import gán vai trò & kịch bản go-live đầu-cuối

- **Branch:** `feat/import-assignments` (fast-forward tới `473665f` — đầu `chore/wave4-integration` — trước khi sửa file đầu tiên)
- **Commit cuối:** commit chứa báo cáo này (commit code cuối: `8b78477`)
- **Ngày:** 2026-09-28

## Kết quả theo mã lỗi

| Mã | Trạng thái | Tóm tắt thay đổi | File chính |
|---|---|---|---|
| T-64 (phần gán vai trò) | done | Loại import `role-assignments` ("Gán vai trò") trên khung task 10. Cột: Tên đăng nhập, Tên vai trò, Loại phạm vi (`Toàn công ty` \| `Phòng` \| `Chi bộ`, có data validation), Mã Phòng/Chi bộ, Từ ngày, Đến ngày, Ghi chú. Kiểm tra lúc xem trước **và** ngay trước khi ghi: tài khoản tồn tại (chưa xóa), vai trò tồn tại (so tên không phân biệt hoa thường, chuẩn hóa NFC + khoảng trắng), Phòng/Chi bộ tồn tại và đang hoạt động, vai trò có quyền "không áp dụng phạm vi" chỉ gán `Toàn công ty` (thông báo nêu **tên** quyền), không tự gán cho người đang nhập (chốt chặn), trùng bản gán đang/sắp hiệu lực cùng người–vai trò–phạm vi chồng lấn thời gian → lỗi dòng; trùng trong tệp → lỗi dòng; ngày sai định dạng, "Đến ngày" < "Từ ngày", "Đến ngày" đã qua, ghi chú > 1000 ký tự → lỗi dòng. Ghi từng dòng qua `IRoleAssignmentService.AssignAsync` (đủ chốt chặn của task 09: cần `system.assignments.manage` Global, không tự gán, xóa cache quyền) trong transaction của khung — một dòng bị service từ chối → rollback cả tệp. Quyền: `system.import` + `system.assignments.manage`. Frontend không sửa: `app/imports` tự hiện loại mới qua `GET /api/imports/kinds` (đã kiểm: trang không mã cứng loại import). | `Application/Imports/Definitions/RoleAssignmentImportDefinition.cs`, `Application/Imports/Definitions/RoleAssignmentImportLookup.cs` (interface tra cứu), `Infrastructure/Imports/RoleAssignmentImportLookup.cs`, `Api/Extensions/ImportExtensions.cs` (2 dòng đăng ký) |
| T-68 | partial | `GoLiveScenarioTests` chạy đủ 5 bước của task file trên PostgreSQL thật từ CSDL trống (collection `golive`, CSDL tạm riêng). **Còn lại:** (1) bước 1 "khởi tạo quản trị ban đầu theo cấu hình seed" được **giả lập** trong test vì hệ thống chưa có cơ chế này (xem "Cần phối hợp" + "Phát hiện thêm"); (2) assert `grants` của `/api/auth/me` nằm ở test riêng, **đang skip** vì nền hiện tại chưa có trường này (chờ tích hợp Đợt 4) — bước 3 đã kiểm bằng `permissions` + `effective-permissions`; (3) nối tiếp với kỳ đánh giá (task 12) chưa làm. | `backend/tests/CongTacDang.IntegrationTests/GoLiveScenarioTests.cs`, `RoleAssignmentImportIntegrationTests.cs` |

**Quy ước ngày tháng (quyết định trong task):** "Từ ngày"/"Đến ngày" là **ngày theo giờ Việt Nam (UTC+7, cố định)**. Từ ngày D → hiệu lực từ 00:00 ngày D (+7). Đến ngày D **tính cả ngày D** → `ValidTo` (không bao gồm) = 00:00 ngày D+1 (+7). Trống "Từ ngày" = ngay khi xác nhận; trống "Đến ngày" = không thời hạn. Nhận `dd/mm/yyyy`, `d/m/yyyy`, `yyyy-mm-dd`, `d-m-yyyy`, `d.m.yyyy` và ô kiểu ngày của Excel. Lý do cố định +7: máy chủ Docker thường chạy UTC, trong mã chưa có quy ước múi giờ nào khác.

**Không sửa / không tạo bản gán đã có:** mỗi dòng là một bản gán mới; sửa thời hạn / thu hồi làm ở màn hình Gán vai trò (task 11). Nhập lại cùng tệp → các dòng trùng bản gán đang hiệu lực bị báo lỗi, không ghi gì.

## Luồng

| # | Luồng | Trạng thái | Test |
|---|---|---|---|
| 1 | Loại import: mã, quyền, cột, danh sách giá trị hợp lệ | pass | unit `RoleAssignmentImportTests.Definition_Kind_Permissions_AndColumns`; tích hợp `RoleAssignmentImportIntegrationTests.Kind_RequiresImportAndAssignmentsManage_TemplateDownloads` (thiếu `system.assignments.manage` → loại bị ẩn, file mẫu 403 nêu tên quyền "Gán vai trò"; đủ quyền → file mẫu tải được) |
| 2 | Dòng hợp lệ Global/Phòng/Chi bộ, tên vai trò không phân biệt hoa thường, ngày giờ VN → UTC, ghi qua `AssignAsync` | pass | unit `ValidRows_AreCreate_DatesAreVietnamDays_AndCommitCallsAssignService` |
| 3 | Validate: người/vai trò/phạm vi không tồn tại, tài khoản đã xóa, Chi bộ ngừng hoạt động, tự gán, vai trò chỉ-Global gán theo phạm vi, Global kèm mã, thiếu mã, loại phạm vi sai, ngày sai/ngược/đã qua, ghi chú dài, dòng trống | pass | unit `ReferencesAndGuardrails_AreRowErrors` |
| 4 | Trùng bản gán đang/sắp hiệu lực → lỗi dòng; nối tiếp sau hạn → hợp lệ; trùng trong tệp → lỗi; khác phạm vi/vai trò → hợp lệ | pass | unit `DuplicateWithEffectiveAssignment_OrWithinFile_IsRowError_NonOverlappingIsAllowed` |
| 5 | 1 transaction: service từ chối giữa chừng → ngoại lệ lan ra (khung rollback); dữ liệu đổi sau xem trước → phân tích lại → 400 "Không dòng nào được ghi", không có bản gán nào; tệp sửa → ghi được, `CreatedBy` + audit theo người nhập | pass | unit `Commit_ServiceRejection_Propagates_SoFrameworkRollsBack`; tích hợp `Commit_ReanalyzesAndWritesNothing_WhenAnAssignmentAppearedAfterPreview` |
| G1 | Go-live bước 1: CSDL trống (0 cán bộ/Phòng/Chi bộ, có vai trò bảo vệ) → quản trị ban đầu → đăng nhập → `mustChangePassword` + API bị 403 `PASSWORD_CHANGE_REQUIRED` → đổi mật khẩu → thấy đủ 4 loại import | pass (khởi tạo quản trị **giả lập**) | `GoLiveScenarioTests.GoLive_FromEmptyDatabase_ImportCatalogCadresAssignments_ThenPermissionsFollowAssignments` |
| G2 | Go-live bước 2: import Phòng → Chi bộ → 3 cán bộ (tải tệp mật khẩu tạm) → tệp gán lỗi (tự gán, quản trị theo Phòng, trùng trong tệp) không cho xác nhận, không ghi → tệp gán đúng 7 dòng; 1 bản audit tóm tắt | pass | như trên |
| G3 | Go-live bước 3: cán bộ đăng nhập bằng mật khẩu tạm trong tệp → bị chặn tới khi đổi → đổi → `/api/auth/me.permissions` đúng 7 mã theo tệp; `effective-permissions` đúng phạm vi (Chi bộ Kỹ thuật / Phòng Kỹ thuật / Toàn công ty), ghi chú và `validTo` theo quy ước ngày | pass | như trên |
| G3b | `/api/auth/me.grants` trả mã + phạm vi (`scopeType`, `scopeId`, `scopeName`) | **skip** — chờ tích hợp Đợt 4 | `RoleAssignmentImportIntegrationTests.GoLive_Step3_AuthMe_ReturnsGrantsWithScope` (tự chạy khi `/me` có `grants`) |
| G4 | Go-live bước 4: `GET /api/reports/cadres` 200 → quản trị xóa bản gán "Cấp ủy viên Đảng ủy" → request kế tiếp (cùng phiên) 403 nêu "Xuất báo cáo tổng hợp"; bản gán khác giữ nguyên | pass | `GoLive_FromEmptyDatabase_…` |
| G5 | Go-live bước 5: quản trị không có mã `evaluation.*`; `records/{id}`, `records/{id}/history`, `records?periodId` → 403; chủ hồ sơ xem được | pass (kỳ + hồ sơ tạo trực tiếp trong CSDL) | `GoLive_FromEmptyDatabase_…` |
| G6 | Nối tiếp: tạo kỳ → thêm người được đánh giá → một hồ sơ tới `Published` | không chạy — chờ task 12 | — |

## Cổng kiểm tra

| Bước | Kết quả | Ghi chú |
|---|---|---|
| `dotnet build backend/CongTacDang.slnx` | pass | 0 lỗi, 9 warning CS0618 — đều có sẵn trên nền `473665f` (`UserService`, `AuthService`, `UserController`, `SecurityTests`), không thêm warning mới |
| `dotnet test` — unit | pass | 175 test: **174 pass, 1 skip** (`PdfConversionTests.Mau01…` — thiếu LibreOffice, skip sẵn có), 0 fail; 5 test mới |
| `dotnet test` — integration (có `CONGTACDANG_TEST_PG`, máy chủ 159) | pass | 51 test: **50 chạy + pass, 1 skip** (`GoLive_Step3_AuthMe_ReturnsGrantsWithScope` — chờ `grants`), 0 fail; 4 test mới (3 chạy, 1 skip). Skip không tính là pass |
| `npx tsc --noEmit` | pass | |
| `npm run build` | pass | Không sửa frontend |
| CSDL tạm | chỉ `ctd_it_*` | Kịch bản go-live dùng collection `golive` với `ApiFactory` riêng → **thêm 1 CSDL `ctd_it_*`** mỗi lần chạy, xóa khi xong. Không đụng `congtacdang_test`. |
| Chạy app / giao diện | không chạy | Theo 7.3; frontend không đổi |

Số lần gọi `/api/auth/login` của test task 13: 6 (go-live 2, import 3, grants 1), **mỗi lần một IP giả riêng** (`distinctClientIp: true`) nên không chiếm hạn mức 10 lần/phút của test khác.

## Thay đổi schema (cần migration)
Không có.

## Key config mới
Không có. (Đề xuất key cho khởi tạo quản trị ban đầu — xem "Cần phối hợp".)

## Thay đổi hành vi API / breaking change
- `GET /api/imports/kinds` có thêm loại `role-assignments` ("Gán vai trò") cho người có `system.import` + `system.assignments.manage` (Global). `GET /api/imports/role-assignments/template`, `POST /api/imports/role-assignments/preview`, `POST /api/imports/{sessionId}/commit` như khung task 10. Kết quả xác nhận: `{ created: <số bản gán>, updated: 0 }`, không có tệp kết quả.
- Không đổi endpoint cũ.

### Mô tả cột `role-assignments`

| Cột | Khóa | Bắt buộc | Quy tắc |
|---|---|---|---|
| Tên đăng nhập | `username` | có | Tài khoản đã có, chưa xóa (không phân biệt hoa thường); không được là người đang nhập |
| Tên vai trò | `roleName` | có | Đúng tên trong Quản lý vai trò (không phân biệt hoa thường, bỏ khoảng trắng thừa) |
| Loại phạm vi | `scopeType` | có | `Toàn công ty` \| `Phòng` \| `Chi bộ`; vai trò có quyền "không áp dụng phạm vi" (quản trị, nhập dữ liệu, quản lý kỳ…) chỉ `Toàn công ty` |
| Mã Phòng/Chi bộ | `scopeCode` | khi Phòng/Chi bộ | Mã đang hoạt động trong danh mục; phải trống khi `Toàn công ty` |
| Từ ngày | `validFrom` | không | Ngày giờ VN; trống = ngay |
| Đến ngày | `validTo` | không | Tính cả ngày này; trống = không thời hạn; không được trước "Từ ngày" hay đã qua |
| Ghi chú | `note` | không | ≤ 1000 ký tự |

## Hướng dẫn go-live (checklist cho người vận hành)

> Người điều phối đưa mục này vào `docs/deployment.md`. Thứ tự dưới đây là thứ tự `GoLiveScenarioTests` kiểm tra tự động (trừ bước 0 và mục có ghi chú).

**0. Chuẩn bị (trước ngày triển khai)**
- [ ] Cấu hình production: `Database:SeedSampleData = false`, `Database:ResetRolePermissions = false`, `Jwt:Secret` riêng, chuỗi kết nối tới CSDL **mới, trống**.
- [ ] Chạy migration (hoặc `Database:AutoMigrate`) → khởi động API một lần. Seeder tạo danh mục quyền và 9 vai trò mặc định (có "Quản trị hệ thống" được bảo vệ). **Không** tạo cán bộ, Phòng, Chi bộ nào.
- [ ] Chuẩn bị 4 tệp Excel từ file mẫu tải trong trang **Nhập dữ liệu** (mỗi loại một tệp): Phòng, Chi bộ, Cán bộ và tài khoản, Gán vai trò. Tên vai trò chép đúng từ trang Quản lý vai trò. Tệp ≤ 5 MB, ≤ 2.000 dòng; tệp cán bộ nên ≤ ~150 dòng/lần (tạo mật khẩu chậm, xem báo cáo task 10).

**1. Tài khoản quản trị ban đầu**
- [ ] Tạo tài khoản quản trị ban đầu, gán vai trò "Quản trị hệ thống" phạm vi Toàn công ty, bắt buộc đổi mật khẩu. ⚠️ *Hiện chưa có cơ chế riêng (xem "Cần phối hợp" mục 1) — không dùng `SeedSampleData=true` trên CSDL thật (tạo kèm tài khoản mẫu mật khẩu `123456`, có vai trò quyết định xếp loại).*
- [ ] Đăng nhập bằng tài khoản đó → hệ thống chuyển tới **Đổi mật khẩu** (mọi chức năng khác bị chặn) → đổi mật khẩu mạnh (≥ 8 ký tự, có chữ và số).
- [ ] Khuyến nghị: tạo thêm **một quản trị thứ hai** (nhập ở bước 2c, gán ở 2d) — hệ thống không cho tự gán/thu hồi vai trò của chính mình, và luôn giữ ít nhất một quản trị.

**2. Nhập dữ liệu (trang Nhập dữ liệu, đúng thứ tự)** — mỗi tệp: tải lên → xem trước từng dòng → sửa hết dòng lỗi (có lỗi thì không xác nhận được, không dòng nào được ghi) → **Xác nhận**.
- [ ] a. **Danh mục Phòng/đơn vị** → kiểm tra số dòng "tạo mới".
- [ ] b. **Danh mục Chi bộ**.
- [ ] c. **Cán bộ và tài khoản** (tham chiếu Mã Phòng / Mã Chi bộ vừa nhập) → xác nhận → **tải ngay tệp "tài khoản mới + mật khẩu tạm"** (tải được **một lần**, hết hạn sau 30 phút, mất nếu khởi động lại API). Lưu tệp ở nơi an toàn.
- [ ] d. **Gán vai trò** (tham chiếu tên đăng nhập, tên vai trò, Mã Phòng/Chi bộ) → xác nhận. Kiểm tra tại trang Gán vai trò / "Tra cứu quyền" của vài người tiêu biểu (Bí thư Chi bộ, Lãnh đạo Phòng, Cơ quan thẩm định, Văn phòng Đảng ủy): quyền và phạm vi đúng quyết định phân công.
- [ ] Mỗi lần xác nhận có một bản ghi "Import" trong Nhật ký hệ thống; từng bản gán cũng được ghi nhật ký.

**3. Giao tài khoản cho cán bộ**
- [ ] Giao riêng từng người tên đăng nhập + mật khẩu tạm (không gửi cả tệp). Hủy tệp mật khẩu sau khi giao xong.
- [ ] Cán bộ đăng nhập lần đầu → bắt buộc đổi mật khẩu → vào được đúng các chức năng theo vai trò được gán.
- [ ] Quên/mất mật khẩu tạm: quản trị **Đặt lại mật khẩu** trong Quản lý tài khoản (không nhập lại tệp — tên đăng nhập đã có sẽ bị báo lỗi).

**4. Kiểm tra sau go-live**
- [ ] Thu hồi / xóa một bản gán thử → người đó bị chặn chức năng tương ứng **ngay request kế tiếp** (không cần đăng xuất).
- [ ] Tài khoản quản trị không xem được hồ sơ đánh giá của cán bộ (tách quản trị kỹ thuật và nghiệp vụ) — đúng thiết kế.
- [ ] Sai sót trong tệp gán: sửa thời hạn / thu hồi ở trang Gán vai trò; nhập lại tệp chỉ thêm bản gán mới (dòng trùng bản gán đang hiệu lực bị báo lỗi).

**5. Mở kỳ đánh giá** — ⚠️ chờ task 12 (luồng tạo kỳ → thêm người được đánh giá → công bố). Người có vai trò "Cơ quan thẩm định" thực hiện.

## Cần phối hợp
1. **Khởi tạo quản trị ban đầu (người điều phối / tích hợp Đợt 4, `DataSeeder` — không thuộc phạm vi file task 13):** hiện tài khoản quản trị chỉ được tạo khi `Database:SeedSampleData=true`, kèm toàn bộ dữ liệu mẫu (4 Chi bộ, 4 Phòng, 5 tài khoản mật khẩu `123456`, kỳ và hồ sơ mẫu). CSDL thật (`SeedSampleData=false`) sau lần khởi động đầu **không có tài khoản nào đăng nhập được**. Đề xuất: key `Seed:InitialAdmin:Username` / `Seed:InitialAdmin:Password` (hoặc lệnh CLI) — chỉ tạo khi bảng tài khoản trống, `MustChangePassword=true`, gán vai trò bảo vệ Global; mật khẩu lấy từ biến môi trường, không ghi vào repo. Khi có, sửa `GoLiveScenarioTests.BootstrapInitialAdministratorAsync` dùng cấu hình host thay cho ghi thẳng CSDL.
2. **`grants` của `/api/auth/me` (tích hợp Đợt 4):** sau khi thêm, test `RoleAssignmentImportIntegrationTests.GoLive_Step3_AuthMe_ReturnsGrantsWithScope` tự hết skip (dò trường `grants`) — người điều phối xác nhận test **chạy và pass** (không còn skip) trong cổng kiểm tra sau merge. Test kỳ vọng mỗi phần tử có `code`, `scopeType` (`Global`/`Department`/`PartyCell`), `scopeId`, `scopeName` như `AccessGrantDto`.
3. **Task 12:** nối tiếp `GoLiveScenarioTests` sau bước 5: người có vai trò "Cơ quan thẩm định" (đã gán cho `tran.thi.b` trong kịch bản, cần đăng nhập + đổi mật khẩu với `distinctClientIp: true`) tạo kỳ → thêm người được đánh giá (nếu có import `evaluation-subjects` thì dùng import) → một hồ sơ đi tới `Published`; thay bước tạo kỳ/hồ sơ trực tiếp trong CSDL (`SeedRecordAsync`) bằng luồng thật. Mật khẩu tạm của cả 3 cán bộ đã có trong biến `passwords` của test.
4. **Tích hợp Đợt 4 (import cán bộ ghi tất cả hoặc không):** `ImportExtensions.AddImports` có thêm 2 dòng đăng ký của task 13 (cuối hàm) — khi merge nếu file này cũng bị sửa thì giữ cả hai.
5. **`docs/deployment.md`:** đưa mục "Hướng dẫn go-live" ở trên vào (không sửa trong task này).

## Phát hiện thêm
- 🔴 **Thiếu cơ chế khởi tạo quản trị ban đầu cho CSDL thật** (xem "Cần phối hợp" 1) — chặn go-live nếu không có bước thủ công (SQL tay). Đề xuất mã mới, vị trí `Infrastructure/Data/DataSeeder.cs` (`SeedSampleUsersAsync` là nơi duy nhất tạo `admin`).
- ⚪ `RoleAssignmentService.AssignAsync` xóa cache quyền của người được gán **trước** khi transaction bên ngoài (import) commit. Một request của đúng người đó chen vào giữa có thể nạp lại cache từ dữ liệu cũ → quyền mới có hiệu lực chậm tới 5 phút (TTL). Không ảnh hưởng tính đúng khi rollback. Đề xuất: xóa cache sau commit (ví dụ `IUnitOfWork` phát sự kiện sau commit) — task 09 / người điều phối.
- ⚪ Kịch bản go-live dùng một `ApiFactory` riêng → mỗi lần chạy test tích hợp tạo thêm 1 CSDL `ctd_it_*` (tổng 3 với collection `api`, `accounts`).
