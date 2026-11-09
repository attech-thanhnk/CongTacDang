"use client";

import React from "react";
import { useAuth } from "@/contexts/AuthContext";
import { PageHeader } from "@/components/common";
import { HolidaysTab } from "@/components/admin/HolidaysTab";

/** Ngày nghỉ cho người quản lý kỳ, không cần quyền sửa các danh mục khác. */
export default function HolidaysPage() {
  const { hasPermission, hasPermissionIn } = useAuth();
  const canManage = hasPermission("catalog.manage") || hasPermissionIn("period.manage", "Global");

  return (
    <div className="page-wrapper">
      <PageHeader title="Ngày nghỉ" />
      <div className="page-body">
        <HolidaysTab canManage={canManage} />
      </div>
    </div>
  );
}
