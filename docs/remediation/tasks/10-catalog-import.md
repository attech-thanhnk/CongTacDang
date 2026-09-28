# Task 10 — Danh mục tổ chức & khung import dữ liệu (Đợt 4)

- **Agent:** D · **Branch:** `feat/catalog-import` (tạo từ `main` **sau khi** task 07 đã merge)
- **Mã:** T-62, T-64
- **Báo cáo:** `docs/remediation/reports/10-catalog-import.md`
- **Đọc trước:** `CLAUDE.md`, `RULES.md` (mục 8), `docs/thiet-ke/phan-quyen.md` (mục 3), báo cáo task 07 (`IUserAccountService`).

## Luồng phải chạy đúng

| # | Luồng | Kết quả mong đợi |
|---|---|---|
| L1 | Quản lý Phòng/đơn vị | Thêm/sửa/ngừng hoạt động/xóa Phòng; mã duy nhất; không xóa được Phòng còn cán bộ hoặc còn hồ sơ đánh giá (409, nêu số lượng) |
| L2 | Quản lý Chi bộ | Như L1 cho Chi bộ (API đã có — rà lại cho đúng các ràng buộc trên) |
| L3 | Import chuẩn | **Tải file mẫu → tải file lên → xem trước từng dòng (hợp lệ / lỗi + lý do) → xác nhận → ghi toàn bộ trong 1 transaction → báo cáo kết quả**. Có lỗi ở bất kỳ dòng nào → không cho xác nhận (không import một phần) |
| L4 | Import Phòng, Chi bộ | Theo mã: mã đã có → cập nhật (hiển thị "cập nhật" ở bước xem trước), chưa có → tạo mới |
| L5 | Import cán bộ + tạo tài khoản | Tạo tài khoản qua `IUserAccountService`; tham chiếu Phòng/Chi bộ bằng **mã**; kết thúc → tải **một lần** file Excel danh sách `tên đăng nhập + mật khẩu tạm` (hết hạn sau 30 phút hoặc sau lần tải đầu, không lưu mật khẩu rõ trên đĩa quá thời hạn) |
| L6 | Import lại cùng file | Cán bộ đã có (trùng tên đăng nhập) → báo lỗi dòng hoặc cập nhật thông tin **không gồm mật khẩu** (chọn một, ghi lý do trong báo cáo) |

## Việc cần làm

### 1. Danh mục (T-62)
- `AdministrativeDepartment`: CRUD đầy đủ qua `OrganizationService`/`OrganizationController`: `GET/POST /api/organizations/departments`, `PUT/DELETE /api/organizations/departments/{id}`. Thêm `SortOrder`. Bỏ `HeadId` khỏi logic (người đứng đầu xác định bằng gán vai trò phạm vi Phòng — task 09); giữ cột nếu xóa gây phức tạp, ghi trong báo cáo.
- Chi bộ: rà `branches` CRUD — thêm ràng buộc xóa như L1. `SecretaryId`/`DeputySecretaryId`: giữ để hiển thị trên biểu mẫu, **không** dùng để phân quyền.
- Đọc danh mục: mọi người đã đăng nhập. Ghi: `catalog.manage`. Chuyển mọi attribute trong `OrganizationController` sang mã mới.

### 2. Khung import (T-64)
- `Application/Imports/`: interface `IImportDefinition<TRow>` — `Kind`, `TemplateColumns` (tên cột, bắt buộc, mô tả, danh sách giá trị hợp lệ), `ParseRow`, `ValidateAsync(rows)` (kiểm tra chéo: trùng trong file, tham chiếu mã tồn tại), `CommitAsync(rows)` (trong transaction qua `IUnitOfWork`), quyền yêu cầu.
- `Infrastructure/Imports/`: đọc/ghi Excel bằng ClosedXML. File mẫu: sheet dữ liệu (header cố định, data validation cho cột có danh sách giá trị) + sheet "Hướng dẫn". Chỉ nhận `.xlsx` ≤ 5 MB, ≤ 2.000 dòng; bỏ dòng trống cuối; cắt khoảng trắng; **chống formula injection** khi ghi file kết quả (giá trị bắt đầu `= + - @` → thêm `'`).
- Phiên import: sau bước tải lên, lưu kết quả phân tích trong bộ nhớ/thư mục tạm (theo `ImportSessionId`, gắn người tạo, hết hạn 30 phút); chỉ người tạo được xác nhận.
- Đăng ký loại import qua DI (`services.AddImportDefinition<T>()`); thêm loại mới **không cần sửa** controller hay frontend.
- API (`ImportController`): `GET /api/imports/kinds` (các loại người hiện tại được dùng: mã, tên hiển thị, mô tả), `GET /api/imports/{kind}/template`, `POST /api/imports/{kind}/preview` (multipart) → `{ sessionId, rows: [{ rowNumber, action: create|update|error, errors[] , data }] , summary }`, `POST /api/imports/{sessionId}/commit` → `{ created, updated, resultFileToken? }`, `GET /api/imports/results/{token}` (tải 1 lần).
- Quyền: `system.import` **và** quyền dữ liệu tương ứng (`catalog.manage` cho Phòng/Chi bộ; `system.users.manage` cho cán bộ).
- Ba loại import: `departments`, `party-cells`, `users` (cột: tên đăng nhập, họ tên, email, số thẻ Đảng, chức danh, mã Phòng, mã Chi bộ, thẩm quyền phê duyệt `CoSo|CapTren`).
- Ghi audit: mỗi lần commit ghi 1 bản ghi audit tóm tắt (loại, số dòng, người thực hiện) ngoài audit tự động từng entity.

### 3. Frontend
- Trang `app/catalog/**`: tab Phòng/đơn vị, tab Chi bộ (bảng, thêm/sửa/xóa, thông báo lỗi 409 rõ ràng). `services/catalogService.ts`.
- Trang `app/imports/**`: **danh sách loại lấy từ `GET /api/imports/kinds`**, bảng xem trước dựng cột động từ dữ liệu trả về (task 12, 13 thêm loại import mà không sửa trang này). Chọn loại → tải mẫu → tải file → bảng xem trước (lọc dòng lỗi, tô màu) → xác nhận → kết quả + nút tải file tài khoản (cảnh báo "chỉ tải được một lần"). `services/importService.ts`.
- Menu: **chỉ thêm** 2 mục mới vào cuối danh sách trong `AppSidebar.tsx` (task 09 cũng sửa file này — giữ thay đổi tối thiểu để merge dễ).
- **Không** sửa `app/users/page.tsx` (tab Chi bộ cũ ở đó sẽ bị task 11 gỡ).

## Phạm vi file (RULES mục 8.2)
Chủ sở hữu: `OrganizationService`, `OrganizationController`, entity `AdministrativeDepartment`, `PartyCell`, `Application/Imports/**`, `Infrastructure/Imports/**`, `ImportController`, `Api/Extensions/ImportExtensions.cs`; frontend `app/catalog/**`, `app/imports/**`, `services/catalogService.ts`, `services/importService.ts`.
Không sửa `UserService`/`UserAccountService` (task 08) — chỉ gọi `IUserAccountService`.

## Test
- Unit: parse/validate từng loại (thiếu cột, sai mã tham chiếu, trùng trong file, trùng CSDL, formula injection).
- Tích hợp: L1–L6; import cán bộ xong → đăng nhập được bằng mật khẩu trong file kết quả và bị buộc đổi mật khẩu; file kết quả tải lần 2 → 404; lỗi 1 dòng → không dòng nào được ghi.

## Tiêu chí hoàn thành
- L1–L6 pass. Báo cáo có mô tả cột của 3 file mẫu và hướng dẫn thêm một loại import mới (task 12, 13 dùng).
