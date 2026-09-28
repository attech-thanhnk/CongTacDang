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
