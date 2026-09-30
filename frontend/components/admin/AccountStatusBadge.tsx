"use client";

import React from "react";
import type { AccountListItem } from "@/services/userService";
import { Badge } from "@/components/common/StatusBadge";
import { formatDateTime } from "./adminUtils";

/** Trạng thái tài khoản: hoạt động / bị khóa tạm (sai mật khẩu) / vô hiệu (quản trị khóa); kèm cờ mật khẩu tạm. */
export function AccountStatusBadge({ account }: { account: AccountListItem }) {
  return (
    <span className="d-inline-flex flex-wrap gap-1">
      {!account.isActive ? (
        <Badge tone="neutral">Vô hiệu</Badge>
      ) : account.isLockedOut ? (
        <Badge tone="warning" title={account.lockoutEnd ? `Khóa đến ${formatDateTime(account.lockoutEnd)}` : undefined}>
          Bị khóa tạm
        </Badge>
      ) : (
        <Badge tone="success">Hoạt động</Badge>
      )}
      {account.mustChangePassword && (
        <Badge tone="info" title="Phải đổi mật khẩu ở lần đăng nhập kế tiếp">
          Mật khẩu tạm
        </Badge>
      )}
    </span>
  );
}

export default AccountStatusBadge;
