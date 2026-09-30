"use client";

import React from "react";
import { recordTone } from "@/components/common/StatusBadge";
import styles from "./dashboard.module.css";

const TONE_CLASS: Record<string, string> = {
  success: "t-ok",
  info: "t-info",
  warning: "t-wait",
  danger: "t-bad",
};

/** Nhãn trạng thái hồ sơ dạng .tag; bị trả lại hoặc quá hạn thì tô đỏ. */
export function RecordTag({
  status,
  label,
  returned,
  overdue,
}: {
  status: string;
  label: string;
  returned?: boolean;
  overdue?: boolean;
}) {
  const tone = returned || overdue ? "danger" : recordTone(status);
  return (
    <span className={`tag ${TONE_CLASS[tone] ?? styles.tNeutral}`}>
      {label}
      {returned && " · bị trả lại"}
      {!returned && overdue && " · quá hạn"}
    </span>
  );
}

export default RecordTag;
