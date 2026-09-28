# Task 11 — Giao diện quản trị tài khoản, vai trò, phân quyền (Đợt 5)

- **Agent:** C · **Branch:** `feat/admin-ui` (tạo từ `main` **sau khi** task 08, 09, 10 đã merge)
- **Mã:** T-65, phần giao diện của T-56, T-59, T-55
- **Báo cáo:** `docs/remediation/reports/11-admin-ui.md`
- **Đọc trước:** `CLAUDE.md`, `RULES.md` (mục 8), `docs/thiet-ke/phan-quyen.md`, báo cáo task 08, 09, 10 (bảng endpoint).

## Mục tiêu
Quản trị viên làm được mọi luồng tài khoản và phân quyền **chỉ bằng giao diện**, không cần gọi API tay. Thay trang `app/users/page.tsx` (> 1.000 dòng, gộp nhiều chức năng) bằng các trang tách riêng.

## Luồng phải chạy đúng (kiểm bằng tay trên trình duyệt + ghi kết quả trong báo cáo)

| # | Luồng |
|---|---|
| U1 | Tạo tài khoản → hộp thoại hiện **tên đăng nhập + mật khẩu tạm** kèm nút sao chép, cảnh báo "chỉ hiển thị một lần" → đóng là mất |
| U2 | Danh sách tài khoản: tìm kiếm, lọc Phòng/Chi bộ/trạng thái, phân trang phía máy chủ; cột trạng thái (hoạt động / bị khóa tạm / vô hiệu), lần đăng nhập cuối |
| U3 | Sửa thông tin, khóa/mở, mở khóa đăng nhập, đặt lại mật khẩu (như U1), xóa — mỗi thao tác nguy hiểm có hộp xác nhận; lỗi 409 chốt chặn hiển thị đúng thông báo máy chủ |
| U4 | Vai trò: danh sách, tạo, đổi tên/mô tả, xóa (409 khi đang được gán → hiện số bản gán); **ma trận quyền** nhóm theo module, có mô tả từng quyền, đánh dấu quyền "không áp dụng phạm vi" |
| U5 | Gán vai trò: từ trang người dùng hoặc trang vai trò → chọn vai trò, loại phạm vi (Toàn công ty / Phòng / Chi bộ) và đối tượng phạm vi (danh sách lấy từ danh mục), từ ngày – đến ngày, ghi chú; kết thúc / xóa bản gán; lịch sử bản gán đã hết hạn |
| U6 | "Người này làm được gì": bảng quyền + phạm vi + vai trò nguồn |
| U7 | Nhật ký: tab Nhật ký thao tác (trang `audit` hiện có) + tab **Nhật ký đăng nhập** (lọc người, kết quả, thời gian) |
| U8 | Menu và nút chỉ hiện theo quyền; không có chuỗi mã vai trò nào trong `frontend/` |

## Việc cần làm
- Trang mới: `app/admin/users/page.tsx`, `app/admin/users/[id]/page.tsx` (thông tin + bản gán + quyền hiệu lực), `app/admin/roles/page.tsx`, `app/admin/roles/[id]/page.tsx` (ma trận quyền + người đang được gán). Component dùng chung trong `components/admin/**`.
- `app/users/page.tsx` → chuyển hướng sang `/admin/users` (danh mục Chi bộ đã ở `/catalog` của task 10).
- `AuthContext`: nhận `permissions` + `grants` từ `/api/auth/me`; `hasPermission(code)` giữ chữ ký; thêm `hasPermissionIn(code, scopeType, scopeId?)`. **Xóa** `hasRole`.
- `AppSidebar`: thêm "Việc cần xử lý" (`/work-queue`, hiện khi có bất kỳ quyền `evaluation.*` trừ `evaluation.read`) và "Kỳ đánh giá" (`/periods`, quyền `period.manage`) — route do task 12 tạo song song; nhóm "Quản trị" (Tài khoản, Vai trò, Danh mục, Nhập dữ liệu, Nhật ký) theo quyền; bỏ hiển thị `user.roles[0]` (hiện tên vai trò đầu tiên từ `grants`, hoặc chức danh).
- `services/userService.ts`, `roleService.ts`, `auditService.ts`: viết lại theo endpoint mới; kiểu dữ liệu TypeScript chặt (không `any`).
- Theo UI hiện có (Bootstrap). **Không** đưa thêm thư viện UI (T-22 vẫn `deferred`).

## Phạm vi file (RULES mục 8.2)
Chủ sở hữu: `app/admin/**`, `components/admin/**`, `app/users/**`, `app/audit/**`, `contexts/AuthContext.tsx`, `components/layout/AppSidebar.tsx`, `components/layout/AppHeader.tsx`, `services/userService.ts`, `services/roleService.ts`, `services/auditService.ts`.
**Không** sửa backend. Thiếu API → ghi "Cần phối hợp" (người điều phối giao lại cho task 08/09).
Không sửa `app/evaluations/**`, `components/evaluations/**` (task 12).

## Kiểm tra
- `npx tsc --noEmit`, `npm run build` pass.
- `grep -rnE "hasRole|QUAN_TRI_HE_THONG|BAN_THUONG_VU|BI_THU_CHI_BO|TO_THAM_DINH|DANG_UY_CO_SO|CAN_BO" frontend/app frontend/components frontend/contexts frontend/services` → rỗng.
- Chạy app (cổng 3100–3199, backend 5100–5199, CSDL riêng) và đi qua U1–U8; mỗi luồng ghi pass/fail + ảnh chụp nếu có thể. Không chạy được → ghi rõ, **không** báo đã qua.
