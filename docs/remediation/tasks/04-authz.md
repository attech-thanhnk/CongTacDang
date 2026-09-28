# Task 04 — Phân quyền theo đối tượng & bảo mật còn lại

- **Branch:** `fix/authz`
- **Mã lỗi:** T-30, T-31, T-32, T-33, T-34, T-35, phần test của T-42
- **Báo cáo:** `docs/remediation/reports/04-authz.md`
- Tuân thủ `docs/remediation/RULES.md` (đặc biệt mục 7 — Đợt 2).

## Nguyên tắc
Đây là việc **kỹ thuật**: xây một cơ chế kiểm tra quyền theo đối tượng dùng chung và áp dụng nó. **Không** tự đặt ra luật nghiệp vụ mới về "ai được xem gì". Phạm vi áp dụng đúng bằng quy tắc đang có trong `EvaluationService.CanReadRecord` (chủ hồ sơ; cùng Chi bộ với quyền `branch_vote`; `appraise`/`approve`/quản trị xem rộng hơn như hiện tại). Luật chi tiết sẽ được chốt sau (B-02) — cơ chế phải cho phép đổi luật ở **một chỗ**.

## T-32 Cơ chế kiểm tra quyền theo đối tượng (làm trước)
- Dùng resource-based authorization của ASP.NET Core (`IAuthorizationService` + `AuthorizationHandler<TRequirement, TResource>`) hoặc một `IAccessPolicy` ở tầng Application — chọn một, ghi lý do trong báo cáo.
- Thao tác tối thiểu: `Read`, `Update`, `Delete`, `Export`. Đối tượng: `EvaluationRecord`, `TaskAttachment`, `PartyMemberProfile` (hồ sơ người dùng), báo cáo theo Chi bộ/kỳ.
- Chuyển logic `CanReadRecord` và các kiểm tra phạm vi rải rác trong `EvaluationService`/`CollectiveEvaluationService` sang cơ chế này **mà không đổi kết quả** (thêm test chứng minh hành vi cũ giữ nguyên).
- `GET /api/users/profile?username=`: chỉ trả hồ sơ của chính mình, trừ người có `users.read` ở phạm vi quản trị. `GET /api/organizations/departments`: thêm policy phù hợp (`branches.read`).

## T-30 Xuất báo cáo
- `/api/reports/docx/mau-01|02|10/{recordId}`: kiểm tra quyền `Export` trên hồ sơ (≈ quyền `Read`).
- `/api/reports/docx/mau-11|13`: chỉ người có quyền trên Chi bộ đó (cùng Chi bộ + `branch_vote`, hoặc cấp cao như hiện tại).
- Excel 14/15/15A/15B/16: giới hạn theo phạm vi người yêu cầu (cấp cao: toàn bộ; Bí thư Chi bộ: Chi bộ mình); **ghi rõ** trong báo cáo nếu quyết định giữ nguyên cho một vai trò nào đó.

## T-31 File đính kèm
- Thêm `UploadedById` (Guid, FK người dùng) — giữ `UploadedBy` (tên) để hiển thị. Dữ liệu cũ: để null.
- Quyền trên file = quyền trên đối tượng liên quan: có `RecordId` → theo hồ sơ; không có → chỉ người tải lên và cấp quản trị. File "văn bản chung" (FormCode `GENERAL`, không gắn hồ sơ) do quản trị tải lên: mọi người đã đăng nhập được đọc.
- Áp dụng cho: danh sách (lọc theo quyền), xem, tải, xóa.
- **Không** thay đổi mô hình file (nhiều file, phiên bản…) — đó là task 05.

## T-33 Seeder ghi đè phân quyền
- Seeder chỉ tạo role/permission khi **chưa có**; không gán lại quyền cho role đã tồn tại. Permission mới xuất hiện trong code: tạo bản ghi permission, không tự gán vào role (ghi log cảnh báo).
- Có thể giữ một lệnh/flag tường minh (ví dụ `Database:ResetRolePermissions=true`) để đặt lại về mặc định — mặc định `false`.

## T-34 Quyền không tồn tại ở frontend
- Thay `evaluations.branch_review` bằng quyền thật (`evaluations.branch_vote`) ở `frontend/app/evaluations/page.tsx` và `components/evaluations/EvaluationStepNav.tsx`. Rà toàn bộ `frontend/` tìm mã quyền không có trong `AppPermissions.cs`.

## T-35 `sign.ps1`
- Xóa `backend/sign.ps1` (không cần ký file build cho triển khai nội bộ). Ghi trong báo cáo hướng dẫn gỡ chứng chỉ `CN=CongTacDangLocal` khỏi kho Trusted Root nếu đã từng chạy. **Không** tự chạy lệnh gỡ chứng chỉ trên máy.

## Test (T-42)
- Unit test cho cơ chế quyền: chủ hồ sơ / cùng Chi bộ / khác Chi bộ / cấp cao, với từng thao tác.
- Test hành vi cũ của `CanReadRecord` được giữ nguyên.

## Kiểm tra thêm
- Thay đổi schema (`UploadedById`) chỉ sửa entity + mapping; **không** tạo migration (RULES mục 5), liệt kê trong báo cáo.
