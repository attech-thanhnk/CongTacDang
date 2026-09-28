# Khắc phục kỹ thuật — hướng dẫn điều phối

| File | Vai trò |
|---|---|
| `FINDINGS.md` | Nguồn duy nhất: mã lỗi, mức độ, owner, trạng thái. Chỉ người điều phối sửa. |
| `RULES.md` | Quy tắc chung: branch, phạm vi, phân chia file, cổng kiểm tra, migration, cấu hình. |
| `tasks/0x-*.md` | Nhiệm vụ của từng agent. |
| `reports/_TEMPLATE.md` | Mẫu báo cáo; agent ghi `reports/0x-*.md` trên branch của mình. |

## 1. Chuẩn bị (một lần)
```bash
git add -A && git commit -m "wip: snapshot before remediation"   # commit phần đang dở
git add CLAUDE.md docs/remediation && git commit -m "docs: remediation plan"
git worktree add ../CongTacDang-01 -b fix/security-auth
git worktree add ../CongTacDang-02 -b fix/data-layer
git worktree add ../CongTacDang-03 -b chore/infra-ops
```

## 2. Chạy agent — 2 đợt
Task 02 là nền (nâng gói, UoW, migrations) mà task 01 xây lên, nên chạy theo 2 đợt để tránh xung đột:

| Đợt | Task | Ghi chú |
|---|---|---|
| 1 | 02 ║ 03 song song | Gần như không đụng file của nhau |
| — | Tích hợp đợt 1 vào `main` | Người điều phối |
| 2 | 01 | Trước khi chạy: `cd ../CongTacDang-01 && git merge main` |

Có thể chạy cả 3 song song để nhanh hơn; khi đó merge theo thứ tự **02 → 01 → 03** và chấp nhận sửa lỗi compile/conflict giữa 01 và 02 lúc tích hợp.

Mỗi worktree cần `cd frontend && npm ci` trước khi chạy agent.
Mở phiên Claude Code trong thư mục worktree tương ứng, dán prompt:

```
Bạn là agent thực hiện task 0X. Đọc CLAUDE.md, docs/remediation/RULES.md,
docs/remediation/tasks/0X-<tên>.md và các mã lỗi tương ứng trong docs/remediation/FINDINGS.md.
Thực hiện toàn bộ task trên branch hiện tại, qua cổng kiểm tra, ghi báo cáo vào
docs/remediation/reports/0X-<tên>.md theo reports/_TEMPLATE.md, rồi commit. Không push.
```

## 3. Tích hợp
1. Đọc 3 báo cáo; mục `partial`/`skipped`/"Cần phối hợp" → quyết định xử lý.
2. Đợt 1: merge **02 → 03** vào `main`. Đợt 2: merge **01**.
3. Sau đợt 2, sinh migration cho thay đổi schema của task 01:
   `dotnet ef migrations add AuthHardening -p backend/src/CongTacDang.Infrastructure -s backend/src/CongTacDang.Api -o Data/Migrations`
4. Bổ sung key config mới vào `appsettings` / `docker/.env.example`.
5. Chạy cổng kiểm tra trên bản đã merge.
6. Cập nhật cột Trạng thái trong `FINDINGS.md`.
7. Dọn worktree: `git worktree remove ../CongTacDang-0X`.

---

# Chương trình nền tảng chuẩn (Đợt 3–5)

Mục tiêu: tài khoản, phân quyền động có phạm vi, danh mục, import và luồng đánh giá 5 bước **chạy đúng từ đầu đến cuối**, có test tích hợp chứng minh. Thiết kế: `docs/thiet-ke/phan-quyen.md`, `docs/thiet-ke/luong-danh-gia.md`. Quy tắc: `RULES.md` mục 8.

## Giao việc cho 5 agent

| Agent | Vai trò | Task |
|---|---|---|
| A | Tài khoản & phiên | 08 |
| B | Phân quyền | 07 → 09 |
| C | Giao diện quản trị | 11 |
| D | Danh mục & import | 10 → 13 |
| E | Luồng đánh giá | 12 |

```
Đợt 3:  B:07 ──merge──┐
Đợt 4:                ├─ A:08 ║ B:09 ║ D:10 ──merge 09→08→10──┐
Đợt 5:                                                      ├─ C:11 ║ E:12 ║ D:13 ──merge 12→11→13
```

## Mỗi đợt
1. Tạo worktree/branch từ `main` mới nhất cho từng task của đợt (`git worktree add ../CongTacDang-<task> -b <branch>`), `cd frontend && npm ci`.
2. Prompt cho agent:
   ```
   Bạn là agent <X>, thực hiện task <NN>. Đọc CLAUDE.md, docs/remediation/RULES.md (mục 8),
   docs/remediation/tasks/<NN>-<tên>.md và các tài liệu mà task file yêu cầu đọc trước.
   Làm toàn bộ task trên branch hiện tại, qua cổng kiểm tra (RULES 8.4), ghi báo cáo vào
   docs/remediation/reports/<NN>-<tên>.md theo reports/_TEMPLATE.md (thêm bảng luồng pass/fail), rồi commit. Không push.
   ```
3. Tích hợp: đọc báo cáo (mục `partial`, "Cần phối hợp") → merge theo thứ tự trong sơ đồ → sinh migration `Wave<N>` → chạy cổng kiểm tra **có** `CONGTACDANG_TEST_PG` → cập nhật `FINDINGS.md`.
4. Việc "Cần phối hợp" nhỏ: người điều phối sửa khi tích hợp; lớn: giao lại cho agent chủ sở hữu trước khi mở đợt sau.

## Việc của người điều phối trước Đợt 3
- Trên máy chủ `192.168.22.159`: tạo tài khoản PostgreSQL có quyền `CREATEDB` cho test tích hợp; đặt biến môi trường `CONGTACDANG_TEST_PG` (chuỗi kết nối tới CSDL `postgres` bằng tài khoản đó) trên máy chạy agent. Không ghi chuỗi này vào repo.
- Gửi `docs/thiet-ke/phan-quyen.md` mục 6 và `docs/thiet-ke/luong-danh-gia.md` mục 6 cho TCCB-LĐ xác nhận — **không chặn** việc code, kết quả chỉ làm đổi cấu hình mặc định.
