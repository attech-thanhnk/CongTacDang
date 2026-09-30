"use client";

import { formatDate } from "@/lib/dateFormat";
import React from "react";
import { RecordStepProgressDto } from "@/services/evaluationService";

interface Props {
  progress: RecordStepProgressDto[];
}

/** Mô tả đầy đủ trạng thái bước (hiện khi rê chuột và cho trình đọc màn hình). */
function describe(item: RecordStepProgressDto): string {
  const external = item.mode === "External";
  const state =
    item.state === "skipped"
      ? "Không áp dụng cho nhóm này"
      : item.state === "done"
        ? external ? "Đã ghi nhận kết quả của cấp trên" : "Đã hoàn thành"
        : item.state === "current"
          ? external ? "Do cấp trên thực hiện — chờ ghi nhận kết quả" : "Đang chờ xử lý"
          : external ? "Do cấp trên thực hiện" : "Chưa tới";
  const deadline = item.state !== "skipped" && item.deadline ? ` · Hạn ${formatDate(item.deadline)}${item.overdue ? " (quá hạn)" : ""}` : "";
  return `${item.name}: ${state}${deadline}`;
}

/**
 * Tiến trình các bước của hồ sơ theo hồ sơ luồng của hồ sơ, dạng gọn một hàng. Trạng thái từng bước và chế độ bước
 * do máy chủ tính; bước đang xử lý hiện thêm thời hạn.
 */
export function RecordProgress({ progress }: Props) {
  return (
    <ol className="list-unstyled d-flex flex-wrap align-items-stretch gap-1 mb-0" aria-label="Tiến trình hồ sơ">
      {progress.map((item, index) => {
        const skipped = item.state === "skipped";
        const current = item.state === "current";
        const done = item.state === "done";
        const external = item.mode === "External";
        const color = skipped ? "#94a3b8" : done ? "#047857" : current ? "#1d4ed8" : "#475569";
        const background = skipped ? "transparent" : done ? "#ecfdf5" : current ? "#dbeafe" : "#f8fafc";
        const icon = skipped ? "bi-slash-circle" : done ? "bi-check-circle-fill" : current ? "bi-arrow-right-circle-fill" : "bi-circle";
        const label = describe(item);
        return (
          <li
            key={item.step}
            className="rounded-3 px-2 py-1 d-flex flex-column justify-content-center"
            style={{
              flex: current ? "1 1 200px" : "0 1 auto",
              background,
              border: `1px solid ${current ? "#60a5fa" : skipped ? "transparent" : "#e2e8f0"}`,
              opacity: skipped ? 0.55 : 1,
              fontSize: 13,
            }}
            title={label}
            aria-label={label}
            aria-current={current ? "step" : undefined}
          >
            <div className="d-flex align-items-center gap-1 text-nowrap" style={{ color, fontWeight: current ? 700 : 600 }}>
              <i className={`bi ${icon}`} aria-hidden="true" />
              <span>{index + 1}. {item.name}</span>
              {external && !skipped && <i className="bi bi-box-arrow-up-right ms-1" aria-hidden="true" />}
            </div>
            {current && item.deadline && (
              <div className={item.overdue ? "text-danger fw-semibold" : "text-secondary"} style={{ fontSize: 13 }}>
                Hạn {formatDate(item.deadline)}{item.overdue ? " (quá hạn)" : ""}
              </div>
            )}
            {!current && !skipped && item.overdue && (
              <div className="text-danger fw-semibold" style={{ fontSize: 13 }}>Quá hạn</div>
            )}
          </li>
        );
      })}
    </ol>
  );
}

export default RecordProgress;
