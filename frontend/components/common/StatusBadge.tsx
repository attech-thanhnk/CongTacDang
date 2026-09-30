"use client";

import React from "react";
import { gradeLabel } from "@/services/evaluationService";

/** Tông màu nhãn: xanh lá = xong, xanh dương = chờ cá nhân, vàng = đang ở các bước xét duyệt, đỏ = bị trả lại/quá hạn. */
export type BadgeTone = "success" | "info" | "warning" | "danger" | "neutral";

const RECORD_TONES: Record<string, BadgeTone> = {
  AwaitingRegistration: "info",
  AwaitingTaskApproval: "warning",
  AwaitingSelfScore: "info",
  AwaitingCellConfirm: "warning",
  AwaitingCollective: "warning",
  AwaitingAppraisal: "warning",
  AwaitingDirectorReview: "warning",
  AwaitingDecision: "warning",
  AwaitingPublish: "warning",
  Published: "success",
};

export function Badge({ tone = "neutral", children, title }: { tone?: BadgeTone; children: React.ReactNode; title?: string }) {
  return (
    <span className={`status-badge tone-${tone}`} title={title}>
      {children}
    </span>
  );
}

/** Trạng thái hồ sơ đánh giá; bị trả lại hoặc quá hạn thì tô đỏ. */
export function RecordStatusBadge({
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
  const tone: BadgeTone = returned || overdue ? "danger" : RECORD_TONES[status] ?? "neutral";
  return (
    <Badge tone={tone} title={returned ? "Bị trả lại" : overdue ? "Quá hạn" : undefined}>
      {label}
      {returned && " · bị trả lại"}
      {!returned && overdue && " · quá hạn"}
    </Badge>
  );
}

const GRADE_TONES: Record<string, BadgeTone> = {
  HoanThanhXuatSac: "success",
  HoanThanhTot: "info",
  HoanThanh: "neutral",
  KhongHoanThanh: "danger",
};

/** Mức xếp loại; chưa có mức thì hiện "—". */
export function GradeBadge({ grade }: { grade?: string | null }) {
  if (!grade || grade === "ChuaXepLoai") return <span className="text-secondary">—</span>;
  return <Badge tone={GRADE_TONES[grade] ?? "neutral"}>{gradeLabel(grade)}</Badge>;
}

export default RecordStatusBadge;
