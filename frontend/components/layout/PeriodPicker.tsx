"use client";

import React from "react";
import { usePathname } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { usePeriod } from "@/contexts/PeriodContext";

/** Ô chọn kỳ đánh giá đang làm việc, đặt ở thanh trên. */
export function PeriodPicker() {
  const pathname = usePathname();
  const { user } = useAuth();
  const { periods, periodId, setPeriodId } = usePeriod();

  // Người chỉ làm quản trị hệ thống không làm việc theo kỳ.
  const onlySystemWork =
    !!user && !user.permissions.some((code) => /^(evaluation|period|criteria)\./.test(code));
  if (!user || user.mustChangePassword || pathname === "/change-password" || onlySystemWork) return null;

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
          <option key={p.id} value={p.id} title={p.name}>
            Quý {["I", "II", "III", "IV"][p.quarter - 1] ?? p.quarter}/{p.year}
            {p.status === "Open" ? "" : ` (${p.statusDisplayName})`}
          </option>
        ))}
      </select>
    </div>
  );
}

export default PeriodPicker;
