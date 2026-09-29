# Task 14 — Mô hình tổ chức động: cây đơn vị, danh mục chức vụ, kiêm nhiệm (Đợt 6)

- **Agent:** F · **Branch:** `feat/dynamic-org`
- **Mã:** T-71, T-72, T-73
- **Báo cáo:** `docs/remediation/reports/14-dynamic-org.md`
- **Đọc trước:** `CLAUDE.md`, `RULES.md` mục 7.3 + 8, `docs/thiet-ke/phan-quyen.md`, `docs/nghiep-vu/hd03-trich-xuat.md` mục 1.4–1.6 và mã chức danh M1–M26 (mục 7).
- Chạy song song với task 15 (luồng theo nhóm đối tượng) — tôn trọng phạm vi file.

## Mục tiêu
Mọi cơ cấu tổ chức Đảng và chính quyền của ATTECH (bất kể bao nhiêu cấp, chức danh gì, ai kiêm nhiệm gì) khai báo được **bằng giao diện / import**, không sửa code. Không còn enum chức vụ cứng.

## 1. Cây đơn vị (T-71)
- `PartyCell` (bên Đảng) và `AdministrativeDepartment` (bên chính quyền) thêm `ParentId` (tự tham chiếu, null = gốc), `UnitTypeId`, `Path` (materialized path, ví dụ `/<id gốc>/<id con>/`, cập nhật khi đổi cha; chặn tạo vòng).
- Danh mục **loại đơn vị** `org_unit_types` (tên, bên Đảng/chính quyền, thứ tự) — quản trị tự thêm (ví dụ: Đảng ủy, Đảng bộ bộ phận, Chi bộ; Công ty, Đơn vị, Phòng, Trung tâm, Xưởng, Đội).
- **Phạm vi gán vai trò bao trùm cây con:** bản gán phạm vi `Department(d)` / `PartyCell(c)` áp dụng cho `d`/`c` **và mọi đơn vị con**. Sửa `AuthorizationGuard.Can`, `ScopeFilter` (dựng điều kiện theo `Path` hoặc danh sách id con đã mở rộng — chọn cách hiệu quả, ghi lý do) và tài liệu `docs/thiet-ke/phan-quyen.md` mục 4.
- Tên `ScopeType.Department`/`PartyCell` giữ nguyên (hợp đồng API); tên hiển thị đổi thành "Đơn vị chính quyền" / "Tổ chức Đảng".
- Xóa đơn vị: chặn khi còn đơn vị con, cán bộ, hồ sơ, bản gán đang hiệu lực (409 kèm số lượng).

## 2. Danh mục chức vụ và kiêm nhiệm (T-72)
- Bảng `positions`: `Name`, `Side` (Đảng / Chính quyền / Đoàn thể / Khác), `StatCode` (tùy chọn, `M1`…`M26` theo Mẫu 15A/15B), `DefaultApprovalAuthority` (tùy chọn: CoSo/CapTren), `IsLeadership`, `SortOrder`, `IsActive`. Quản trị thêm/sửa/ngừng dùng.
- Bảng `member_positions`: `UserId`, `PositionId`, `PartyCellId?` hoặc `DepartmentId?` (đơn vị giữ chức vụ), `IsPrimary`, `ValidFrom`, `ValidTo`, `Note`. Một người nhiều chức vụ ở nhiều đơn vị (kiêm nhiệm).
- **Gỡ enum** `PartyRole`, `AdministrativePosition` khỏi `PartyMemberProfile` và mọi nơi dùng; `PositionTitle` giữ làm chức danh hiển thị trên văn bản (mặc định = chức vụ chính).
- `PartyMemberProfile.PartyCellId` / `DepartmentId` giữ nghĩa "nơi sinh hoạt Đảng / đơn vị công tác chính".
- **Thẩm quyền phê duyệt suy ra:** nếu hồ sơ cán bộ không đặt tay, `ApprovalAuthority` = `CapTren` khi có ít nhất một chức vụ đang hiệu lực có `DefaultApprovalAuthority = CapTren`, ngược lại `CoSo` (HD03 tr.6: "cấp ủy cấp trên quyết định cuối cùng"). Cho phép đặt tay (ghi đè) có lý do.
- **Mã thống kê của người** (Mẫu 15A/15B): mã `StatCode` nhỏ nhất (thứ tự đứng trước) trong các chức vụ đang hiệu lực (HD03 tr.74: "ưu tiên nhóm chức danh có thứ tự đứng trước"). Cập nhật `ReportService` Mẫu 15A/15B dùng mã này thay cho suy luận từ enum cũ.
- Seed danh mục chức vụ mặc định theo HD03 (M1–M26 + chức vụ thường gặp ở ATTECH: Bí thư/Phó Bí thư Đảng ủy, UV BTV, Đảng ủy viên, UV UBKT, Bí thư/Phó Bí thư Chi bộ, Chi ủy viên, Đảng viên; Chủ tịch, Giám đốc, Phó Giám đốc, Kiểm soát viên, Kế toán trưởng, Trưởng/Phó phòng, Trưởng/Phó trung tâm, Quản đốc/Phó quản đốc, Chuyên viên, Nhân viên) — **chỉ khi danh mục trống**, ghi rõ trong báo cáo là mặc định chờ xác nhận.

## 3. Import và giao diện (T-73)
- Import `departments`, `party-cells`: thêm cột **mã đơn vị cha**, **loại đơn vị**; thứ tự trong file tùy ý (xử lý cha trước con trong cùng lô; báo lỗi vòng/thiếu cha).
- Import mới `positions` (danh mục chức vụ) và `member-positions` (tên đăng nhập, chức vụ, mã đơn vị, chính/kiêm nhiệm, từ ngày, đến ngày).
- Import `users`: bỏ cột chức vụ enum; chức vụ nhập qua `member-positions`.
- Frontend (`app/catalog/**`): cây đơn vị hai bên (thêm con, đổi cha bằng chọn, sửa, xóa), tab Loại đơn vị, tab Chức vụ. Trang chi tiết tài khoản (`app/admin/users/[id]`): mục Chức vụ (thêm/kết thúc/xóa), hiển thị thẩm quyền suy ra + ghi đè. Ô chọn phạm vi khi gán vai trò hiển thị dạng cây.

## Phạm vi file
Chủ sở hữu: entity `PartyCell`, `AdministrativeDepartment`, `PartyMemberProfile` (trường tổ chức/chức vụ), `Position`, `MemberPosition`, `OrgUnitType`, enum liên quan trong `DomainEnums.cs` (trừ enum đánh giá), `OrganizationService/Controller`, service/controller chức vụ mới, `AuthorizationGuard` + `ScopeFilter` + `PermissionResolver` (chỉ phần phạm vi cây), `UserAccountService` (phần chức vụ/thẩm quyền), `ReportService` (phần Mẫu 15A/15B mã chức danh), `Application/Imports/Definitions/*` của tổ chức/cán bộ/chức vụ, `DataSeeder` (phần tổ chức, chức vụ, tài khoản mẫu), frontend `app/catalog/**`, `app/admin/users/**`, `components/admin/**`, `services/catalogService.ts`, `services/userService.ts`.
**Không** sửa: `EvaluationWorkflowService`, `PeriodService`, `RecordStateMachine`, `EvaluationPeriod`/`EvaluationRecord` (trừ nếu gỡ enum bắt buộc — khi đó chỉ sửa đúng chỗ tham chiếu), `app/periods/**`, `app/evaluations/**`, `app/work-queue/**` (task 15).

## Test
- Unit: phạm vi cây (gán ở cha → quyền ở cháu; không lan sang nhánh anh em), chống vòng, suy ra thẩm quyền, mã thống kê nhỏ nhất.
- Tích hợp: dựng cây 3 cấp bằng import → gán vai trò ở nút giữa → người đó thấy/thao tác hồ sơ ở mọi nút con, không thấy nhánh khác; kiêm nhiệm 2 chức vụ (một CapTren) → hồ sơ mới tạo có `ApprovalAuthority = CapTren`; Mẫu 15B phân nhóm theo mã chức danh.

## Tiêu chí hoàn thành
- Không còn `PartyRole`, `AdministrativePosition` trong `backend/src`, `frontend`.
- Build 0 warning, test (integration 0 skip), `tsc`, `npm run build` pass. Không tạo migration (người điều phối gộp lại `InitialCreate`).
- Báo cáo: mô hình dữ liệu mới, cột các file import, danh mục chức vụ mặc định, thay đổi API.
