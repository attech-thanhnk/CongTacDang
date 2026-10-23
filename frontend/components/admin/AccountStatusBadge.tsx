"use client";

import React from "react";
import type { AccountListItem } from "@/services/userService";
import { Badge, BadgeTone } from "@/components/common/StatusBadge";
import { formatDateTime } from "./adminUtils";

const TEMP_PASSWORD_HINT = "Phải đổi mật khẩu ở lần đăng nhập kế tiếp";

/**
 * Trạng thái tài khoản: hoạt động / bị khóa tạm (sai mật khẩu) / vô hiệu (quản trị khóa); kèm cờ mật khẩu tạm.
 * `compact`: chỉ một nhãn theo trạng thái quan trọng nhất, phần còn lại vào tooltip — dùng trong bảng.
 */
export function AccountStatusBadge({ account, compact = false }: { account: AccountListItem; compact?: boolean }) {
  const lockTitle = account.lockoutEnd ? `Khóa đến ${formatDateTime(account.lockoutEnd)}` : undefined;

  if (compact) {
    let tone: BadgeTone;
    let label: string;
    const details: string[] = [];
    if (!account.isActive) {
      tone = "neutral";
      label = "Vô hiệu";
      details.push("Tài khoản bị vô hiệu");
    } else if (account.isLockedOut) {
      tone = "warning";
      label = "Bị khóa tạm";
      details.push(lockTitle ?? "Tài khoản bị khóa tạm");
    } else if (account.mustChangePassword) {
      tone = "info";
      label = "Mật khẩu tạm";
      details.push("Đang hoạt động", TEMP_PASSWORD_HINT);
    } else {
      tone = "success";
      label = "Hoạt động";
      details.push("Đang hoạt động");
    }
    // Khi nhãn chính là khóa/vô hiệu, cờ mật khẩu tạm còn lại hiện bằng chấm nhỏ.
    const extraTempPassword = account.mustChangePassword && (!account.isActive || account.isLockedOut);
    if (extraTempPassword) details.push("Mật khẩu tạm — " + TEMP_PASSWORD_HINT.toLowerCase());

    return (
      <span className="d-inline-flex align-items-center gap-1 text-nowrap" title={details.join("\n")}>
        <Badge tone={tone}>
          {label}
        </Badge>
        {extraTempPassword && (
          <span
            role="img"
            aria-label="Mật khẩu tạm"
            style={{ width: 8, height: 8, borderRadius: "50%", background: "var(--info)", display: "inline-block" }}
          />
        )}
      </span>
    );
  }

  return (
    <span className="d-inline-flex flex-wrap gap-1">
      {!account.isActive ? (
        <Badge tone="neutral">Vô hiệu</Badge>
      ) : account.isLockedOut ? (
        <Badge tone="warning" title={lockTitle}>
          Bị khóa tạm
        </Badge>
      ) : (
        <Badge tone="success">Hoạt động</Badge>
      )}
      {account.mustChangePassword && (
        <Badge tone="info" title={TEMP_PASSWORD_HINT}>
          Mật khẩu tạm
        </Badge>
      )}
    </span>
  );
}

export default AccountStatusBadge;
