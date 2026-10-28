"use client";

import React from "react";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { getPreviousPath } from "@/lib/navHistory";
import { buildMenu, findMenuTrail } from "@/components/layout/menu";

/** Nút "← <mục cha>" ở dòng đầu trang chi tiết; trang không phải chi tiết thì không hiện. */
export function BackButton() {
  const { user, hasPermission } = useAuth();
  const pathname = usePathname();
  const router = useRouter();
  const trail = user ? findMenuTrail(buildMenu(user, hasPermission), pathname) : null;
  if (!trail?.isDetail) return null;

  // Vừa từ danh sách cha sang thì quay lại bằng lịch sử để giữ bộ lọc và trang đang xem.
  const goBack = () => {
    if (getPreviousPath() === trail.item.href) router.back();
    else router.push(trail.item.href);
  };

  return (
    <button type="button" className="back-btn" onClick={goBack} aria-label={`Quay lại ${trail.item.title}`}>
      <i className="bi bi-arrow-left" aria-hidden="true" />
      <span>{trail.item.title}</span>
    </button>
  );
}

export default BackButton;
