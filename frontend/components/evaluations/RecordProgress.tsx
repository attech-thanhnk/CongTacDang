"use client";

import React from "react";
import { RecordStepProgressDto } from "@/services/evaluationService";

interface Props {
  progress: RecordStepProgressDto[];
}

const formatDate = (value?: string | null) =>
  value ? new Intl.DateTimeFormat("vi-VN", { dateStyle: "short" }).format(new Date(value)) : null;

/**
 * Tiến trình 9 bước của hồ sơ. Trạng thái từng bước (xong / đang chờ / chưa tới / không áp dụng) do máy chủ tính;
 * bước bị tắt trong kỳ hiện mờ với chú thích "Không áp dụng trong kỳ".
 */
export function RecordProgress({ progress }: Props) {
  return (
    <ol className="list-unstyled d-flex flex-wrap gap-2 mb-0" aria-label="Tiến trình hồ sơ">
      {progress.map((item, index) => {
        const skipped = item.state === "skipped";
        const current = item.state === "current";
        const done = item.state === "done";
        const color = skipped ? "#94a3b8" : done ? "#047857" : current ? "#1d4ed8" : "#475569";
        const background = skipped ? "#f8fafc" : done ? "#ecfdf5" : current ? "#eff6ff" : "#ffffff";
        const icon = skipped ? "bi-slash-circle" : done ? "bi-check-circle-fill" : current ? "bi-arrow-right-circle-fill" : "bi-circle";
        return (
          <li
            key={item.step}
            className="border rounded-3 px-2 py-2"
            style={{
              minWidth: 150,
              flex: "1 1 150px",
              background,
              borderColor: current ? "#93c5fd" : "#e2e8f0",
              opacity: skipped ? 0.6 : 1,
            }}
            aria-current={current ? "step" : undefined}
          >
            <div className="d-flex align-items-center gap-2" style={{ color, fontSize: 12, fontWeight: 600 }}>
              <i className={`bi ${icon}`} />
              <span>
                {index + 1}. {item.name}
              </span>
            </div>
            <div className="small mt-1" style={{ color: skipped ? "#94a3b8" : "#64748b", fontSize: 11 }}>
              {skipped
                ? "Không áp dụng trong kỳ"
                : done
                  ? "Đã hoàn thành"
                  : current
                    ? "Đang chờ xử lý"
                    : "Chưa tới"}
              {!skipped && item.deadline && (
                <span className={item.overdue ? "text-danger fw-semibold" : ""}>
                  {" · Hạn "}
                  {formatDate(item.deadline)}
                  {item.overdue ? " (quá hạn)" : ""}
                </span>
              )}
            </div>
          </li>
        );
      })}
    </ol>
  );
}

export default RecordProgress;
