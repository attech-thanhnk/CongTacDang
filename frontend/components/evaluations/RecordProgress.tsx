"use client";

import { formatDate } from "@/lib/dateFormat";
import React from "react";
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

/** Số ngày từ hôm nay (giờ máy) tới thời hạn dạng yyyy-MM-dd; null khi không đọc được. */
function daysUntil(deadline: string): number | null {
  const [y, m, d] = deadline.slice(0, 10).split("-").map(Number);
  if (!y || !m || !d) return null;
  const now = new Date();
  const today = Date.UTC(now.getFullYear(), now.getMonth(), now.getDate());
  return Math.round((Date.UTC(y, m - 1, d) - today) / 86_400_000);
}

/** "Hạn dd/MM/yyyy (còn N ngày / hôm nay / quá hạn N ngày)" và cờ trễ hạn. */
function dueOf(item: RecordStepProgressDto): { text: string; late: boolean } | null {
  if (!item.deadline) return null;
  const days = daysUntil(item.deadline);
  const late = item.overdue || (days !== null && days < 0);
  const rest = days === null ? "" : days > 0 ? ` (còn ${days} ngày)` : days === 0 ? " (hôm nay)" : ` (quá hạn ${-days} ngày)`;
  return { text: `Hạn ${formatDate(item.deadline)}${rest}`, late };
}

/** "Chờ: <ai>" của bước đang làm; bước cấp trên thì ghi rõ cấp trên thực hiện. */
function waitingText(item: RecordStepProgressDto): string {
  const actor = actorOf(item);
  if (!actor) return "";
  return item.mode === "External" ? actor : `Chờ: ${actor}`;
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

/**
 * Nơi duy nhất hiện bước hiện tại, người đang chờ và hạn của hồ sơ.
 * Màn rộng: thanh đủ các bước; màn hẹp: bước hiện tại + số thứ tự và dải tiến độ.
 */
export function RecordProgress({ progress, onSelect }: Props) {
  const steps = progress.filter((item) => item.state !== "skipped");
  const currentIndex = steps.findIndex((p) => p.state === "current");
  const current = currentIndex >= 0 ? steps[currentIndex] : null;
  const due = current ? dueOf(current) : null;
  const waiting = current ? waitingText(current) : "";
  const doneCount = steps.filter((p) => p.state === "done").length;

  return (
    <>
      <ol className={styles.steps} aria-label="Tiến trình hồ sơ">
        {steps.map((item, index) => {
          const isCurrent = item.state === "current";
          const done = item.state === "done";
          const label = describeStep(item);
          const content = (
            <>
              <div className={styles.c}>{done ? <i className="bi bi-check-lg" aria-hidden="true" /> : index + 1}</div>
              <span className={styles.stepName}>
                {item.name}
                {item.mode === "External" && <i className="bi bi-box-arrow-up-right ms-1" aria-hidden="true" />}
                {isCurrent && waiting && <em className={styles.stepMeta}>{waiting}</em>}
                {isCurrent && due && <em className={`${styles.stepMeta} ${due.late ? styles.late : ""} num`}>{due.text}</em>}
                {!isCurrent && !done && item.overdue && <em className={`${styles.stepMeta} ${styles.late}`}>Quá hạn</em>}
              </span>
            </>
          );
          return (
            <li
              key={item.step}
              className={`${styles.step} ${done ? styles.done : isCurrent ? styles.now : ""}`}
              aria-current={isCurrent ? "step" : undefined}
            >
              {(done || isCurrent) && onSelect ? (
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

      <div className={styles.stepsCompact}>
        {current ? (
          <button type="button" className={styles.compactBtn} onClick={onSelect ? () => onSelect(current) : undefined} disabled={!onSelect}>
            <span className={styles.compactHead}>
              <span className={`${styles.compactNo} num`}>Bước {currentIndex + 1}/{steps.length}</span>
              <strong>{current.name}</strong>
            </span>
            {(waiting || due) && (
              <span className={styles.compactMeta}>
                {waiting && <span>{waiting}</span>}
                {due && <span className={`num ${due.late ? styles.late : ""}`}>{due.text}</span>}
              </span>
            )}
          </button>
        ) : (
          <div className={styles.compactHead}>
            <span className={`${styles.compactNo} num`}>{doneCount}/{steps.length} bước</span>
            <strong>{doneCount === steps.length ? "Đã hoàn thành" : "Chưa bắt đầu"}</strong>
          </div>
        )}
        <div className={styles.compactBar} aria-hidden="true">
          {steps.map((item) => (
            <span key={item.step} className={item.state === "done" ? styles.segDone : item.state === "current" ? styles.segNow : undefined} />
          ))}
        </div>
      </div>
    </>
  );
}

export default RecordProgress;
