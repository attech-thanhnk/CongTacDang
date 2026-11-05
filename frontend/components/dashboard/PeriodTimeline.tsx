"use client";

import React from "react";
import type { PeriodTimelineDto } from "@/services/dashboardService";
import styles from "./dashboard.module.css";
import { dayMonth, deadlineRange } from "./stepUtils";

const STATE_CLASS = { done: styles.tlDone, current: styles.tlCurrent, pending: styles.tlPending } as const;

/** Lịch kỳ: các bước kèm hạn trên một dải ngang; bước đang diễn ra tô nổi và gắn mốc hôm nay. */
export function PeriodTimeline({ timeline, today }: { timeline: PeriodTimelineDto | null | undefined; today: string }) {
  if (!timeline || timeline.steps.length === 0) return null;
  return (
    <section className={styles.block} aria-label="Lịch kỳ">
      <div className={styles.blockH}>
        <h2>Lịch kỳ</h2>
        <span className={styles.muted}>{timeline.periodName}</span>
        <span className={`${styles.todayMark} num`}>Hôm nay {dayMonth(today)}</span>
      </div>
      <ol className={styles.timeline}>
        {timeline.steps.map((s) => (
          <li key={s.step} className={`${styles.tlStep} ${STATE_CLASS[s.state] ?? ""}`} aria-current={s.state === "current" ? "step" : undefined}>
            <span className={styles.tlBar} aria-hidden="true" />
            <span className={styles.tlName} title={s.name}>
              {s.name}
            </span>
            <span className={`${styles.tlDate} num`}>
              {deadlineRange(s.deadlineFrom, s.deadlineTo) || "—"}
              {s.state === "current" && <span className={styles.tlNow}>Hôm nay</span>}
            </span>
          </li>
        ))}
      </ol>
    </section>
  );
}

export default PeriodTimeline;
