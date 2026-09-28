"use client";

import React from "react";
import type { AccountListItem } from "@/services/userService";
import { formatDateTime } from "./adminUtils";

/** Trạng thái tài khoản: hoạt động / bị khóa tạm (sai mật khẩu) / vô hiệu (quản trị khóa); kèm cờ mật khẩu tạm. */
export function AccountStatusBadge({ account }: { account: AccountListItem }) {
  return (
    <span className="d-inline-flex flex-wrap gap-1">
      {!account.isActive ? (
        <span className="badge bg-secondary-subtle text-secondary border">Vô hiệu</span>
      ) : account.isLockedOut ? (
        <span
          className="badge bg-warning-subtle text-warning-emphasis border border-warning-subtle"
          title={account.lockoutEnd ? `Khóa đến ${formatDateTime(account.lockoutEnd)}` : undefined}
        >
          Bị khóa tạm
        </span>
      ) : (
        <span className="badge bg-success-subtle text-success border border-success-subtle">Hoạt động</span>
      )}
      {account.mustChangePassword && (
        <span className="badge bg-info-subtle text-info-emphasis border border-info-subtle" title="Phải đổi mật khẩu ở lần đăng nhập kế tiếp">
          Mật khẩu tạm
        </span>
      )}
    </span>
  );
}

export default AccountStatusBadge;
