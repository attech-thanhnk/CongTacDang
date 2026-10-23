"use client";

import { formatDate } from "@/lib/dateFormat";
import React, { useEffect, useRef } from "react";
import { RecordStepProgressDto } from "@/services/evaluationService";
import styles from "./record.module.css";

interface Props {
  progress: RecordStepProgressDto[];
  /** Bấm vào bước đã xong / đang làm (điều hướng tới nội dung của bước trên trang). */
  onSelect?: (item: RecordStepProgressDto) => void;
}

const pad = (value: number) => String(value).padStart(2, "0");

/** dd/MM HH:mm theo giờ máy. */
export function formatShortDateTime(value?: string | null): string {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  return `${pad(date.getDate())}/${pad(date.getMonth() + 1)} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/** Chủ thể thực hiện bước do máy chủ mô tả (máy chủ cũ chưa có → suy từ chế độ bước). */
export function actorOf(item: RecordStepProgressDto): string {
  if (item.actor) return item.actor;
  return item.mode === "External" ? "Cấp trên thực hiện" : "";
}

/** Mô tả đầy đủ trạng thái bước (hiện khi rê chuột và cho trình đọc màn hình). */
export function describeStep(item: RecordStepProgressDto): string {
  const external = item.mode === "External";
  const actor = actorOf(item);
  const deadline = item.deadline ? ` · Hạn ${formatDate(item.deadline)}${item.overdue ? " (quá hạn)" : ""}` : "";
  let state: string;
  if (item.state === "done") {
    const at = formatShortDateTime(item.doneAt);
    const by = item.doneByName ? ` · ${item.doneByName}` : "";
    state = `Hoàn thành${at ? ` ${at}` : ""}${by}${external ? " (cấp trên thực hiện)" : ""}`;
    return `${item.name}: ${state}`;
  }
  if (item.state === "current") {
    state = actor ? `Chờ ${external ? "cấp trên thực hiện" : actor}` : "Đang chờ xử lý";
  } else {
    state = external ? "Cấp trên thực hiện" : actor ? `${actor} thực hiện` : "Chưa tới";
  }
  return `${item.name}: ${state}${deadline}`;
}

/** Trạng thái bước do máy chủ tính; bước không áp dụng cho hồ sơ luồng thì ẩn. */
export function RecordProgress({ progress, onSelect }: Props) {
  const steps = progress.filter((item) => item.state !== "skipped");
  const listRef = useRef<HTMLOListElement>(null);

  // Màn hình hẹp: cuộn ngang để bước đang làm nằm trong tầm nhìn (không cuộn cả trang).
  useEffect(() => {
    const list = listRef.current;
    const current = list?.querySelector<HTMLElement>("[aria-current='step']");
    if (!list || !current || list.scrollWidth <= list.clientWidth) return;
    list.scrollLeft = current.offsetLeft - (list.clientWidth - current.offsetWidth) / 2;
  }, [progress]);

  return (
    <ol ref={listRef} className={styles.steps} aria-label="Tiến trình hồ sơ">
      {steps.map((item, index) => {
        const current = item.state === "current";
        const done = item.state === "done";
        const label = describeStep(item);
        const content = (
          <>
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
          </>
        );
        return (
          <li
            key={item.step}
            className={`${styles.step} ${done ? styles.done : current ? styles.now : ""}`}
            aria-current={current ? "step" : undefined}
          >
            {(done || current) && onSelect ? (
              <button type="button" className={styles.stepBtn} title={label} aria-label={label} onClick={() => onSelect(item)}>
                {content}
              </button>
            ) : (
              <div className={`${styles.stepBtn} ${styles.stepIdle}`} role="group" title={label} aria-label={label}>
                {content}
              </div>
            )}
          </li>
        );
      })}
    </ol>
  );
}

export default RecordProgress;
