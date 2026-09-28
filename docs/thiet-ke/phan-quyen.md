# Thiết kế phân quyền động có phạm vi

> **Trạng thái:** thiết kế kỹ thuật đã chốt để triển khai (task 07, 09, 11, 12, 13).
> Phần **cấu hình mặc định** (mục 6) là đề xuất theo bản trích xuất HD03 **chưa xác nhận** — nghiệp vụ duyệt và sửa được qua giao diện, không cần sửa code.

## 1. Nguyên tắc

1. **Code không biết tên vai trò.** Mọi kiểm tra quyền dùng *mã quyền* (permission code). Không còn `IsInRole(...)`, `HasRole(...)`, `AppRoles.*` trong logic (backend lẫn frontend). Ngoại lệ duy nhất: seeder tạo vai trò quản trị ban đầu.
2. **Mã quyền do code định nghĩa** (danh mục cố định, mục 3) vì mỗi quyền phải có code thực thi. **Vai trò, quyền của vai trò, gán vai trò cho người** do quản trị cấu hình trong CSDL.
3. **Mỗi lần gán vai trò có phạm vi và thời hạn:** ai — vai trò gì — ở đâu (toàn công ty / một Phòng / một Chi bộ) — từ ngày, đến ngày.
4. **Quyền được tính lại ở mỗi request** từ CSDL (có cache, xóa cache khi thay đổi). JWT chỉ chứa danh tính, không chứa vai trò/quyền → thay đổi có hiệu lực ngay ở request kế tiếp.
5. **Mọi kiểm tra quyền theo đối tượng đi qua một chỗ** (`IAuthorizationGuard`). Truy vấn danh sách dùng bộ lọc phạm vi của guard, không tự viết điều kiện.
6. **Quản trị kỹ thuật tách khỏi nghiệp vụ:** vai trò quản trị mặc định không có quyền xem nội dung đánh giá (Mẫu 18: IT là đầu mối quản trị dữ liệu).

## 2. Mô hình dữ liệu

| Bảng | Trường chính | Ghi chú |
|---|---|---|
| `permissions` | `Code` (unique), `Name`, `Module`, `Description`, `SortOrder` | Seed từ `PermissionCodes`; quản trị không thêm/xóa được. Mã có trong CSDL nhưng không còn trong code → bỏ qua khi tính quyền, ghi log cảnh báo. |
| `roles` | `Id`, `Name` (unique trong bản ghi chưa xóa), `Description`, `IsProtected`, audit, xóa mềm | **Bỏ cột `Code` khỏi logic** (giữ cột nếu cần cho seed, không dùng để phân quyền). `IsProtected`: không xóa được, không gỡ được quyền `system.roles.manage`/`system.assignments.manage`. |
| `role_permissions` | `RoleId`, `PermissionId` | Nhiều-nhiều. |
| `user_role_assignments` | `Id`, `UserId`, `RoleId`, `ScopeType`, `ScopeId?`, `ValidFrom`, `ValidTo?`, `Note`, audit, xóa mềm | **Thay bảng nhiều-nhiều user↔role hiện tại.** `ScopeType ∈ {Global, Department, PartyCell}`; `ScopeId` bắt buộc khi khác `Global`, null khi `Global`. |

Trường bổ sung (task 07):
- `PartyMemberProfile.ApprovalAuthority` (enum `ApprovalAuthority { CoSo = 1, CapTren = 2 }`) thay `IsApprovedByAttech` (true → `CoSo`, false → `CapTren`).
- `EvaluationRecord.ApprovalAuthority` — ảnh chụp từ hồ sơ khi tạo hồ sơ đánh giá (cùng kiểu với `PartyCellId`, `DepartmentId` đã có).
- `PartyMemberProfile.SecurityStamp` (string, đổi khi đổi/đặt lại mật khẩu, khóa, xóa) — task 08 dùng.

## 3. Danh mục mã quyền (`Application/Common/Security/PermissionCodes.cs`)

| Mã | Tên hiển thị | Áp dụng phạm vi | Ghi chú |
|---|---|---|---|
| `system.users.read` | Xem tài khoản, hồ sơ cán bộ | có | Không gồm hồ sơ đánh giá |
| `system.users.manage` | Quản lý tài khoản | có | Tạo, sửa, khóa/mở, xóa, đặt lại mật khẩu, mở khóa đăng nhập |
| `system.roles.manage` | Quản lý vai trò | không | Tạo/sửa/xóa vai trò, chọn quyền cho vai trò |
| `system.assignments.manage` | Gán vai trò | không | Gán/thu hồi vai trò kèm phạm vi, thời hạn |
| `system.audit.read` | Xem nhật ký | không | Nhật ký thao tác + nhật ký đăng nhập |
| `system.import` | Nhập dữ liệu | không | Phải có thêm quyền quản lý loại dữ liệu được nhập |
| `catalog.manage` | Quản lý danh mục | không | Phòng/đơn vị, Chi bộ. **Xem** danh mục: mọi người đã đăng nhập |
| `period.manage` | Quản lý kỳ đánh giá | không | Tạo kỳ, cấu hình bước/thời hạn/tham số, danh sách người được đánh giá, mở/khóa kỳ |
| `evaluation.self` | Tham gia đánh giá (bản thân) | chủ hồ sơ | Đăng ký sản phẩm, tự chấm, giải trình, nộp minh chứng — **chỉ trên hồ sơ của mình** |
| `evaluation.read` | Xem hồ sơ đánh giá | có | Chủ hồ sơ **luôn** xem được hồ sơ của mình (HD03: quyền được biết) |
| `evaluation.tasks.approve` | Duyệt danh mục sản phẩm (B1) | có | |
| `evaluation.cell.confirm` | Chi bộ xác nhận phiếu tự chấm (B2) | có | |
| `evaluation.collective.record` | Ghi nhận đề xuất của tập thể lãnh đạo (B3a) | có | Ghi kết quả kiểm phiếu, không ghi phiếu từng người |
| `evaluation.appraise` | Thẩm định (B3b) | có | |
| `evaluation.director.review` | Nhận xét, đề xuất của cấp trực tiếp sử dụng (B3c) | có | |
| `evaluation.decide` | Ghi nhận quyết định của Đảng ủy cơ sở (B4) | có | Chỉ hồ sơ `ApprovalAuthority = CoSo` |
| `evaluation.decide.external` | Ghi nhận quyết định của cấp trên (B4) | có | Chỉ hồ sơ `ApprovalAuthority = CapTren` |
| `evaluation.publish` | Công bố, khóa kết quả (B5) | có | |
| `evaluation.reopen` | Mở lại hồ sơ đã khóa để đính chính | có | Bắt buộc lý do |
| `collective.manage` | Lập hồ sơ tự đánh giá tập thể (Mẫu 06–08) | có | |
| `meeting.read` | Xem biên bản hội nghị, kiểm phiếu (Mẫu 12–13) | có | |
| `meeting.manage` | Lập biên bản hội nghị, kiểm phiếu | có | |
| `report.export` | Xuất báo cáo tổng hợp | có | Mẫu 14–16, danh sách cán bộ — lọc theo phạm vi |
| `attachment.general.manage` | Quản lý văn bản chung | không | Tài liệu hướng dẫn, biểu mẫu trống (`FormCode = GENERAL`) |

"Áp dụng phạm vi = không": quyền chỉ có nghĩa khi được gán phạm vi `Global`; gán phạm vi khác → API từ chối (400).

### Ánh xạ mã cũ → mã mới (dùng cho task 07/09)

| Cũ | Mới |
|---|---|
| `users.read` | `system.users.read` |
| `users.create`, `users.update`, `users.delete` | `system.users.manage` |
| `branches.read` | (bỏ — mọi người đã đăng nhập) |
| `branches.create/update/delete` | `catalog.manage` |
| `attachments.read` | (bỏ — quyền trên tệp = quyền trên hồ sơ gắn tệp) |
| `attachments.upload`, `attachments.delete` | `evaluation.self` (tệp của mình) / `attachment.general.manage` (văn bản chung) |
| `evaluations.read` | `evaluation.read` |
| `evaluations.register`, `evaluations.self_score` | `evaluation.self` |
| `evaluations.branch_vote` | `evaluation.cell.confirm` + `collective.manage` + `meeting.read` + `meeting.manage` (phạm vi Chi bộ) |
| `evaluations.appraise` | `evaluation.appraise` |
| `evaluations.approve` | `evaluation.decide` + `evaluation.decide.external` |
| `reports.export` | `report.export` |
| `roles.manage` | `system.roles.manage` + `system.assignments.manage` + `system.audit.read` |

## 4. Luật tính quyền (`IAuthorizationGuard`)

Đối tượng cần kiểm tra được mô tả bằng `AccessTarget`:
```
OwnerId?          // chủ hồ sơ (người được đánh giá / người tải tệp)
DepartmentId?     // Phòng của hồ sơ (ảnh chụp trên EvaluationRecord)
PartyCellId?      // Chi bộ của hồ sơ
ApprovalAuthority? // CoSo / CapTren
```

Người dùng **được** thực hiện quyền `P` trên đối tượng `T` khi:
1. Tài khoản đang hoạt động, chưa xóa, không trong trạng thái bắt buộc đổi mật khẩu (task 08 chặn trước ở middleware); **và**
2. Có ít nhất một bản gán vai trò **đang hiệu lực** (`ValidFrom ≤ now < ValidTo` hoặc `ValidTo` null, vai trò chưa xóa) mà vai trò đó có `P`, và phạm vi bao trùm `T`:
   - `Global` → mọi đối tượng;
   - `Department(d)` → `T.DepartmentId == d`;
   - `PartyCell(c)` → `T.PartyCellId == c`;
   
   **và**
3. Luật riêng theo mã (cố định trong code, lấy từ HD03):
   - `evaluation.self`: chỉ khi `T.OwnerId == user.Id` (phạm vi bỏ qua).
   - `evaluation.read`: luôn đúng khi `T.OwnerId == user.Id`.
   - `evaluation.decide`: thêm điều kiện `T.ApprovalAuthority == CoSo`; `evaluation.decide.external`: `== CapTren`.
   - **Xung đột lợi ích** (HD03 tr.4): các quyền duyệt/xác nhận/ghi nhận/thẩm định/nhận xét/quyết định/công bố/mở lại (`evaluation.tasks.approve`, `cell.confirm`, `collective.record`, `appraise`, `director.review`, `decide`, `decide.external`, `publish`, `reopen`) **không** áp dụng khi `T.OwnerId == user.Id`.

Guard cung cấp:
- `bool Can(string permission, AccessTarget target)` và `void Ensure(...)` (ném `ForbiddenException` → 403);
- `bool HasAny(string permission)` — có quyền ở bất kỳ phạm vi nào (dùng cho policy ở controller và menu);
- `ScopeFilter GetScope(string permission)` — trả `{ IsGlobal, DepartmentIds[], PartyCellIds[] }` để service dựng điều kiện `WHERE` cho truy vấn danh sách (kèm điều kiện chủ hồ sơ khi mã là `evaluation.read`).

Controller dùng `[RequirePermission(PermissionCodes.X)]` = "có X ở phạm vi nào đó"; service **bắt buộc** gọi `Ensure`/`GetScope` trên đối tượng cụ thể.

## 5. Chốt chặn quản trị

| Chốt | Hành vi |
|---|---|
| Không tự nâng quyền | Không tạo/sửa/thu hồi bản gán vai trò của chính mình; không sửa quyền của vai trò mình đang được gán (API 403, thông báo rõ) |
| Không tự khóa | Không khóa/xóa chính mình |
| Luôn còn quản trị | Luôn còn ≥ 1 tài khoản hoạt động có `system.roles.manage` **và** ≥ 1 có `system.assignments.manage` (phạm vi Global, đang hiệu lực). Thao tác làm vi phạm (xóa vai trò, gỡ quyền, thu hồi gán, khóa/xóa người) → 409 |
| Vai trò bảo vệ | Vai trò `IsProtected` không xóa được, không gỡ được 2 quyền quản trị nói trên |
| Kiểm tra phạm vi hợp lệ | Quyền "không áp dụng phạm vi" chỉ nằm trong vai trò được gán `Global`; `ScopeId` phải tồn tại và đang hoạt động |
| Lưu vết | Mọi thay đổi vai trò/quyền/gán ghi audit (tự động qua DbContext) với trước–sau |
| Tra cứu | API "người X làm được gì": danh sách quyền kèm phạm vi và bản gán nguồn |

## 6. Cấu hình mặc định (seed khi CSDL chưa có vai trò nào — ĐỀ XUẤT, CHỜ XÁC NHẬN)

| Vai trò | Quyền | Phạm vi gán điển hình | Căn cứ HD03 (bản trích xuất) |
|---|---|---|---|
| Người được đánh giá | `evaluation.self` | Global | IV.1, IV.2 |
| Lãnh đạo Phòng | `evaluation.read`, `evaluation.tasks.approve` | Department | IV.1; PL II mục II |
| Thư ký tập thể lãnh đạo | `evaluation.read`, `evaluation.collective.record`, `meeting.read`, `meeting.manage` | Department hoặc Global (cấp Công ty) | IV.3a; Mẫu 11–13 |
| Chi ủy / Bí thư Chi bộ | `evaluation.read`, `evaluation.cell.confirm`, `collective.manage`, `meeting.read` | PartyCell | Mẫu 09A–9D "xác nhận của Chi bộ"; Mẫu 07 |
| Cơ quan thẩm định (Phòng TCCB-LĐ) | `evaluation.read`, `evaluation.appraise`, `period.manage`, `report.export`, `system.import`, `system.users.read` | Global | IV.1 (rà soát), IV.3b |
| Cấp trực tiếp sử dụng (Giám đốc/Chủ tịch) | `evaluation.read`, `evaluation.director.review`, `report.export` | Global | IV.3c |
| Cấp ủy viên Đảng ủy | `evaluation.read`, `meeting.read`, `report.export` | Global | IV.4; Mẫu 18 |
| Văn phòng Đảng ủy (ghi nhận quyết định) | `evaluation.read`, `evaluation.decide`, `evaluation.decide.external`, `evaluation.publish`, `evaluation.reopen`, `meeting.read`, `meeting.manage`, `report.export` | Global | IV.4, IV.5 |
| Quản trị hệ thống (`IsProtected`) | `system.*`, `catalog.manage`, `attachment.general.manage` | Global | Mẫu 18 (đầu mối IT) |

Dữ liệu mẫu (khi `SeedSampleData=true`) gán vai trò cho các tài khoản mẫu theo bảng trên; **chỉ gán khi tài khoản chưa có bản gán nào** (sửa T-46).

## 7. Hiệu năng và triển khai
- Một instance API (on-premise). Cache quyền trong bộ nhớ theo `UserId`, TTL 5 phút, **xóa ngay** khi: sửa vai trò/quyền của vai trò (xóa toàn bộ), sửa bản gán của người (xóa người đó), khóa/xóa/đổi mật khẩu (task 08). Chạy nhiều instance cần cache phân tán — ngoài phạm vi, ghi trong `docs/deployment.md`.
- Mỗi request chỉ tốn 1 lần tra cache; lỡ cache: 1 truy vấn nạp bản gán + quyền của người dùng.
