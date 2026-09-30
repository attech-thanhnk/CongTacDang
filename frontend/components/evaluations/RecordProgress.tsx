"use client";

import { formatDate } from "@/lib/dateFormat";
import React from "react";
import { RecordStepProgressDto } from "@/services/evaluationService";
import styles from "./record.module.css";

interface Props {
  progress: RecordStepProgressDto[];
}

/** Mô tả đầy đủ trạng thái bước (hiện khi rê chuột và cho trình đọc màn hình). */
function describe(item: RecordStepProgressDto): string {
  const external = item.mode === "External";
  const state =
    item.state === "done"
      ? external ? "Đã ghi nhận kết quả của cấp trên" : "Đã hoàn thành"
      : item.state === "current"
        ? external ? "Do cấp trên thực hiện — chờ ghi nhận kết quả" : "Đang chờ xử lý"
        : external ? "Do cấp trên thực hiện" : "Chưa tới";
  const deadline = item.deadline ? ` · Hạn ${formatDate(item.deadline)}${item.overdue ? " (quá hạn)" : ""}` : "";
  return `${item.name}: ${state}${deadline}`;
}

/**
 * Tiến trình các bước của hồ sơ (vòng tròn nối nhau): bước xong tô đầy, bước hiện tại sáng kèm hạn (đỏ khi quá hạn);
 * bước không áp dụng cho hồ sơ luồng thì ẩn. Trạng thái do máy chủ tính.
 */
export function RecordProgress({ progress }: Props) {
  const steps = progress.filter((item) => item.state !== "skipped");
  return (
    <ol className={styles.steps} aria-label="Tiến trình hồ sơ">
      {steps.map((item, index) => {
        const current = item.state === "current";
        const done = item.state === "done";
        const label = describe(item);
        return (
          <li
            key={item.step}
            className={`${styles.step} ${done ? styles.done : current ? styles.now : ""}`}
            title={label}
            aria-label={label}
            aria-current={current ? "step" : undefined}
          >
            <div className={styles.c}>{done ? <i className="bi bi-check-lg" aria-hidden="true" /> : index + 1}</div>
            <span className={styles.stepName}>
              {item.name}
              {item.mode === "External" && <i className="bi bi-box-arrow-up-right ms-1" aria-hidden="true" />}
              {current && item.deadline && (
                <em className={`${styles.stepDue} ${item.overdue ? styles.late : ""} num`}>
                  hạn {formatDate(item.deadline)}
                </em>
              )}
              {!current && !done && item.overdue && <em className={`${styles.stepDue} ${styles.late}`}>quá hạn</em>}
            </span>
          </li>
        );
      })}
    </ol>
  );
}

export default RecordProgress;
