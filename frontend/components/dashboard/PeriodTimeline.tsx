"use client";

import React, { useState } from "react";
import { AdminModal } from "@/components/admin/AdminModal";
import type { PeriodTimelineDto } from "@/services/dashboardService";
import styles from "./dashboard.module.css";
import { dayMonth, deadlineRange, fullDate } from "./stepUtils";

const STATE_LABEL = {
  done: { text: "Đã qua", tone: "tag" },
  current: { text: "Đang diễn ra", tone: "tag t-info" },
  pending: { text: "Chưa đến", tone: "tag" },
} as const;

/** Hạn từng bước theo từng diện quyết định. */
function TimelineDetail({ timeline, onClose }: { timeline: PeriodTimelineDto; onClose: () => void }) {
  return (
    <AdminModal title={`Hạn các bước — ${timeline.periodName}`} onClose={onClose} maxWidth={Math.min(1140, 420 + timeline.profiles.length * 170)}>
      <div className="table-responsive">
        <table className="table align-middle mb-0">
          <thead>
            <tr>
              <th scope="col">Bước</th>
              {timeline.profiles.map((p) => (
                <th key={p.code} scope="col">
                  {p.name}
                  {p.isMine && <span className="tag t-info ms-2">Của bạn</span>}
                </th>
              ))}
              <th scope="col">Trạng thái</th>
            </tr>
          </thead>
          <tbody>
            {timeline.steps.map((s) => (
              <tr key={s.step} className={s.state === "current" ? "fw-semibold" : undefined} aria-current={s.state === "current" ? "step" : undefined}>
                <td className="text-nowrap">{s.name}</td>
                {timeline.profiles.map((p) => (
                  <td key={p.code} className="num text-nowrap">
                    {fullDate(s.deadlines.find((d) => d.profileCode === p.code)?.deadline) || "—"}
                  </td>
                ))}
                <td className="text-nowrap"><span className={STATE_LABEL[s.state].tone}>{STATE_LABEL[s.state].text}</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </AdminModal>
  );
}

const STATE_CLASS = { done: styles.tlDone, current: styles.tlCurrent, pending: styles.tlPending } as const;

/** Lịch kỳ: các bước kèm hạn trên một dải ngang; bước đang diễn ra tô nổi và gắn mốc hôm nay. */
export function PeriodTimeline({ timeline, today }: { timeline: PeriodTimelineDto | null | undefined; today: string }) {
  const [detail, setDetail] = useState(false);
  if (!timeline || timeline.steps.length === 0) return null;
  return (
    <section className={styles.block} aria-label="Lịch kỳ">
      <div className={styles.blockH}>
        <h2>Lịch kỳ</h2>
        <span className={styles.muted}>{timeline.periodName}</span>
        <span className={`${styles.todayMark} num`}>Hôm nay {dayMonth(today)}</span>
        <button type="button" className="btn btn-outline-secondary btn-sm" onClick={() => setDetail(true)}>Xem chi tiết</button>
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
      {detail && <TimelineDetail timeline={timeline} onClose={() => setDetail(false)} />}
    </section>
  );
}

export default PeriodTimeline;
