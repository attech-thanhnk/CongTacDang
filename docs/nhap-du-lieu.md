# Nhập dữ liệu từ Excel

Trang **Hệ thống → Nhập dữ liệu** (`/admin/imports`). Quyền `system.import` (chỉ gán phạm vi Toàn công ty) mở chức năng; mỗi loại nhập còn cần quyền dữ liệu tương ứng. Danh sách loại hiện trên trang lấy từ máy chủ theo quyền của người dùng.

## Quy trình

1. Chọn loại dữ liệu → **Tải file mẫu** (sheet `Dữ liệu` có tiêu đề cố định, cột bắt buộc có dấu `*`, danh sách chọn cho cột có giá trị cố định; sheet `Hướng dẫn` mô tả từng cột, ví dụ và giá trị hợp lệ lấy từ danh mục hiện tại).
2. Điền dữ liệu từ dòng 2, tải file lên → **Kiểm tra file**: bảng xem trước từng dòng với kết quả *Tạo mới* / *Cập nhật* / *Lỗi* kèm lý do; lọc **Chỉ dòng lỗi**.
3. **Nhập dữ liệu**: chỉ bấm được khi **không còn dòng lỗi**. Toàn bộ file ghi trong một transaction — có lỗi ở bất kỳ dòng nào thì không dòng nào được ghi (không nhập một phần). Lúc xác nhận, máy chủ kiểm tra lại với dữ liệu hiện tại; dữ liệu đã đổi làm phát sinh lỗi → từ chối, tải file lên lại để xem lỗi.
4. Nhập cán bộ có tài khoản mới → nút **Tải file mật khẩu tạm** (tên đăng nhập, họ tên, mật khẩu tạm). File chỉ tải được **một lần**, trong 30 phút, chỉ người đã nhập; mật khẩu tạm không lưu ở đâu khác (chỉ giữ trong bộ nhớ máy chủ tới khi tải hoặc hết hạn).

Giới hạn: file `.xlsx` ≤ 5 MB, ≤ 2.000 dòng dữ liệu (dòng trống bỏ qua), mỗi ô ≤ 1.000 ký tự. Phiên xem trước giữ 30 phút trong bộ nhớ máy chủ, gắn với người tải lên — chỉ người đó xác nhận được; khởi động lại máy chủ thì phải tải file lên lại. Ngày nhập dạng `dd/MM/yyyy` (hiểu theo giờ Việt Nam). Mỗi lần nhập ghi một bản ghi nhật ký tóm tắt (`Action = Import`: loại, tên file, số dòng, tạo mới, cập nhật) ngoài nhật ký tự động của từng bản ghi.

## Thứ tự nhập khuyến nghị

**Đơn vị chính quyền → Tổ chức Đảng → Cán bộ và tài khoản → Gán vai trò** (loại sau tham chiếu mã/tên đăng nhập của loại trước). Danh mục loại đơn vị, chức vụ và vai trò cần có sẵn (quản trị trên giao diện).

## Các loại nhập

### 1. Đơn vị chính quyền (`departments`) — cần `catalog.manage`

| Cột | Bắt buộc | Ghi chú |
|---|---|---|
| Mã đơn vị | Có | Không khoảng trắng; chuẩn hóa chữ hoa. Mã đã có → **Cập nhật**, chưa có → **Tạo mới**. Mã của đơn vị đã xóa → lỗi |
| Tên đơn vị | Có | ≤ 200 ký tự |
| Tên viết tắt | Không | ≤ 30 ký tự, dùng đặt tên tệp xuất. Trống: tạo mới → không có; cập nhật → giữ nguyên |
| Mã đơn vị cấp trên | Không | Mã đã có hoặc có trong cùng file (thứ tự dòng tùy ý). Trống: tạo mới → đơn vị gốc (chỉ khi cây chưa có gốc); cập nhật → giữ nguyên cấp trên |
| Loại đơn vị | Có | Tên trong danh mục loại đơn vị bên chính quyền, đang dùng |
| Thứ tự | Không | Số nguyên |
| Mô tả | Không | Trống khi cập nhật → giữ nguyên |

Kiểm tra: trùng mã trong file, cấp trên không tồn tại/đã xóa, mỗi cây một gốc, quan hệ cấp trên tạo vòng. Ghi qua `OrganizationService` (tính lại `Path`, xóa cache quyền).

### 2. Tổ chức Đảng (`party-cells`) — cần `catalog.manage`

Như đơn vị chính quyền với các cột: **Mã tổ chức Đảng**, **Tên tổ chức Đảng**, **Tên viết tắt** (tùy chọn, như đơn vị chính quyền), **Mã tổ chức cấp trên**, **Loại tổ chức** (danh mục loại đơn vị bên Đảng), Thứ tự, Mô tả.

### 3. Cán bộ và tài khoản (`users`) — cần `system.users.manage`

| Cột | Bắt buộc | Ghi chú |
|---|---|---|
| Tên đăng nhập | Có | `^[a-z0-9._-]{3,50}$`, chuẩn hóa chữ thường. Chưa có → **Tạo mới** tài khoản; đã có → **Cập nhật**; thuộc tài khoản đã xóa → lỗi |
| Họ và tên | Có | ≤ 200 ký tự |
| Ngày sinh | Không | `dd/MM/yyyy`; không ở tương lai, không quá 100 năm |
| Email | Không | Đúng định dạng |
| Điện thoại | Không | ≤ 30 ký tự (cột để dạng chữ để giữ số 0 đầu) |
| Mã đơn vị | Không | Mã đơn vị chính quyền có trong danh mục, đang hoạt động |
| Mã tổ chức Đảng | Không | Mã tổ chức Đảng có trong danh mục, đang hoạt động |
| Đảng viên | Không | `Có` / `Không`. Có → bắt buộc số thẻ đảng; Không → số thẻ phải trống (khi cập nhật: xóa số thẻ). Trống → suy theo số thẻ |
| Số thẻ đảng | Không | ≤ 50 ký tự |
| Chức vụ | Không | Nhiều chức vụ phân tách bằng `;`, theo **tên** hoặc **mã thống kê** (M1–M26) trong danh mục chức vụ; mã thống kê ứng với nhiều chức vụ → lỗi, phải ghi tên. Chức vụ đầu tiên là chức vụ chính (tài khoản mới). Ghi `Quyền` (hoặc `Q.`) trước tên để giữ dạng quyền, ví dụ `Quyền Trưởng phòng` — đánh giá như chính chức vụ đó. Chức vụ Đảng giữ tại tổ chức Đảng của cán bộ, chức vụ chính quyền giữ tại đơn vị, chức vụ khác không gắn đơn vị |
| Khung tỷ trọng mặc định | Không | Mã khung A-B-C-D (ví dụ `K2`) |

Lựa chọn khi tên đăng nhập đã có: **cập nhật thông tin, không đổi mật khẩu**; ô trống giữ nguyên giá trị cũ (không xóa); chức vụ chỉ **thêm** chức vụ chưa giữ, không kết thúc chức vụ đang giữ (kết thúc chức vụ làm trên trang tài khoản). Tài khoản mới tạo qua `IUserAccountService` (mật khẩu tạm ngẫu nhiên, bắt đổi khi đăng nhập lần đầu); chức vụ qua `IPositionService` (tính lại thẩm quyền phê duyệt). Quyền `system.users.manage` được kiểm tra trên đơn vị/tổ chức Đảng của từng tài khoản (nơi hiện tại và nơi đến) — dòng ngoài phạm vi báo lỗi. Nhập nhiều tài khoản mới mất thời gian (băm mật khẩu, khoảng 0,1–0,2 giây/tài khoản).

### 4. Gán vai trò (`role-assignments`) — cần `system.assignments.manage`

| Cột | Bắt buộc | Ghi chú |
|---|---|---|
| Tên đăng nhập | Có | Tài khoản đã có, chưa xóa |
| Vai trò | Có | Tên hoặc mã vai trò |
| Loại phạm vi | Có | `Toàn công ty` / `Đơn vị` / `Tổ chức Đảng` (phạm vi gồm cả cấp dưới) |
| Mã đơn vị/tổ chức Đảng | Theo phạm vi | Bắt buộc với Đơn vị / Tổ chức Đảng (có trong danh mục, đang hoạt động); phải trống với Toàn công ty |
| Từ ngày | Không | `dd/MM/yyyy`; trống → hiệu lực ngay |
| Đến ngày | Không | `dd/MM/yyyy`, còn hiệu lực hết ngày này; trống → không thời hạn |
| Ghi chú | Không | ≤ 1.000 ký tự |

Mỗi dòng tạo một bản gán mới (luôn *Tạo mới*). Kiểm tra: không tự gán cho chính người đang nhập; vai trò có quyền chỉ áp dụng Toàn công ty chỉ gán được phạm vi Toàn công ty; trùng bản gán (cùng người – vai trò – phạm vi, thời gian chồng lấn) với bản gán đang/sắp hiệu lực hoặc với dòng khác trong file. Ghi qua `IRoleAssignmentService` (các chốt chặn của dịch vụ vẫn áp dụng).

## API

| Phương thức | Đường dẫn | Ghi chú |
|---|---|---|
| GET | `/api/imports/kinds` | Loại người dùng được dùng (mã, tên, mô tả, cột) |
| GET | `/api/imports/{kind}/template` | File mẫu `.xlsx` |
| POST | `/api/imports/{kind}/preview` | multipart `file` → `{ sessionId, rows: [{ rowNumber, action: create\|update\|error, errors, notes, data }], summary }` |
| POST | `/api/imports/sessions/{sessionId}/commit` | → `{ total, created, updated, resultFileToken? }`; người khác → 403; còn lỗi → 409 |
| GET | `/api/imports/results/{token}` | File mật khẩu tạm, tải một lần (lần sau 404) |

## Thêm một loại nhập mới

Tạo lớp kế thừa `ImportDefinition<TRow>` trong `Application/Imports/Definitions/` (khai báo `Kind`, `Name`, `Columns`, `RequiredPermissions`; viết `ParseRow`, `ValidateAsync`, `CommitRowsAsync` gọi dịch vụ nghiệp vụ hiện có), đăng ký bằng `services.AddImportDefinition<T>()` trong `Api/Extensions/ImportExtensions.cs`. Controller và trang nhập dữ liệu tự hiện loại mới.
