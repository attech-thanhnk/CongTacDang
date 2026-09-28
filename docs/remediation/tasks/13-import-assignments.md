# Task 13 — Import gán vai trò & kiểm thử luồng quản trị đầu-cuối (Đợt 5)

- **Agent:** D · **Branch:** `feat/import-assignments` (tạo từ `main` **sau khi** task 08, 09, 10 đã merge)
- **Mã:** T-64 (phần gán vai trò), T-68
- **Báo cáo:** `docs/remediation/reports/13-import-assignments.md`
- **Đọc trước:** `CLAUDE.md`, `RULES.md` (mục 8), `docs/thiet-ke/phan-quyen.md`, báo cáo task 09 (`IRoleAssignmentService`) và 10.

## 1. Import gán vai trò
- Loại import `role-assignments` trên khung task 10. Cột: tên đăng nhập, tên vai trò, loại phạm vi (`Toàn công ty|Phòng|Chi bộ`), mã Phòng/Chi bộ, từ ngày, đến ngày, ghi chú.
- Validate: người, vai trò, phạm vi tồn tại; quyền "không áp dụng phạm vi" chỉ gán `Toàn công ty`; không tự gán cho người đang import (chốt chặn); trùng bản gán đang hiệu lực (cùng người–vai trò–phạm vi) → báo lỗi dòng.
- Commit qua `IRoleAssignmentService` trong 1 transaction. Quyền: `system.import` + `system.assignments.manage`.
- Frontend: không cần sửa — trang `app/imports/**` tự hiện loại mới qua `GET /api/imports/kinds`. Nếu trang chưa dựng động được như yêu cầu task 10 → ghi "Cần phối hợp", không tự sửa.

## 2. Kịch bản go-live đầu-cuối (T-68)
Test tích hợp `GoLiveScenarioTests.cs` mô phỏng đúng thứ tự triển khai thật, từ CSDL trống:
1. Hệ thống khởi tạo tài khoản quản trị ban đầu (theo cấu hình seed) → đăng nhập → bị buộc đổi mật khẩu → đổi.
2. Import Phòng → import Chi bộ → import cán bộ (nhận file mật khẩu tạm) → import gán vai trò.
3. Một cán bộ bất kỳ đăng nhập bằng mật khẩu tạm trong file → đổi mật khẩu → `/api/auth/me` trả đúng quyền + phạm vi theo file gán.
4. Quản trị gỡ một bản gán → request kế tiếp của người đó bị 403 ở chức năng tương ứng.
5. Quản trị không xem được hồ sơ đánh giá (tách quản trị kỹ thuật).

Nếu task 12 đã merge trước khi task này kết thúc: nối tiếp kịch bản với tạo kỳ → thêm người được đánh giá → một hồ sơ đi tới `Published`. Nếu chưa: ghi "Cần phối hợp" để người điều phối bổ sung sau.

## Phạm vi file
Chủ sở hữu: `Application/Imports/Definitions/RoleAssignment*`, dòng đăng ký DI của loại import này, file test mới. Không sửa service của task 08/09, không sửa frontend.

## Tiêu chí hoàn thành
- Kịch bản go-live pass trên PostgreSQL thật.
- Báo cáo: **hướng dẫn go-live** từng bước (dạng checklist cho người vận hành) — người điều phối đưa vào `docs/deployment.md`.
