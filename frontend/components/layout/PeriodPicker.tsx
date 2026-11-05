"use client";

import React from "react";
import { usePathname } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { usePeriod } from "@/contexts/PeriodContext";

/** Trang xem dữ liệu theo kỳ đang chọn; trang khác không cần ô chọn kỳ. */
const PERIOD_PAGES = [
  "/",
  "/work-queue",
  "/evaluations",
  "/collective-evaluations",
  "/crosschecks",
  "/quality-control",
  "/results",
  "/reports",
];

/** Ô chọn kỳ đánh giá đang làm việc, đặt ở thanh trên. */
export function PeriodPicker() {
  const pathname = usePathname();
  const { user } = useAuth();
  const { periods, periodId, setPeriodId } = usePeriod();

  // Người chỉ làm quản trị hệ thống không làm việc theo kỳ.
  const onlySystemWork =
    !!user && !user.permissions.some((code) => /^(evaluation|period|criteria)\./.test(code));
  // Trang chi tiết hồ sơ theo kỳ của chính hồ sơ nên chỉ khớp đúng đường dẫn danh sách.
  if (!user || user.mustChangePassword || onlySystemWork || !PERIOD_PAGES.includes(pathname ?? "")) return null;

  return (
    <div className="hdr-period">
      <span aria-hidden="true">Kỳ:</span>
      <select
        id="global-period"
        aria-label="Kỳ đánh giá"
        value={periodId}
        onChange={(e) => setPeriodId(e.target.value)}
        disabled={periods.length === 0}
      >
        {periods.length === 0 && <option value="">Chưa có kỳ</option>}
        {periods.map((p) => (
          <option key={p.id} value={p.id}>
            {p.name}
            {p.status === "Open" ? "" : ` (${p.statusDisplayName})`}
          </option>
        ))}
      </select>
    </div>
  );
}

export default PeriodPicker;
