"use client";

import React from "react";
import { Badge, BadgeTone } from "@/components/common/StatusBadge";

const PERIOD_TONES: Record<string, BadgeTone> = {
  Draft: "neutral",
  Open: "success",
  Locked: "warning",
  Closed: "neutral",
};

/** Trạng thái kỳ đánh giá: Dự thảo / Đóng — xám, Đang mở — xanh, Khóa dữ liệu — vàng. */
export function PeriodStatusBadge({ status, label }: { status: string; label: string }) {
  return <Badge tone={PERIOD_TONES[status] ?? "neutral"}>{label}</Badge>;
}

export default PeriodStatusBadge;
