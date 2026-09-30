"use client";

import React, { useMemo, useState } from "react";
import { EvaluationRecordHistoryDto, gradeLabel } from "@/services/evaluationService";
import { formatDateTime } from "@/lib/dateFormat";
import styles from "./record.module.css";

const RECENT = 5;

/** Lịch sử hồ sơ dạng dòng thời gian có đường nối: mới nhất trước, mặc định 5 mục gần nhất. */
export function RecordHistory({ history }: { history: EvaluationRecordHistoryDto[] }) {
  const [showAll, setShowAll] = useState(false);
  const sorted = useMemo(
    () => [...history].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()),
    [history]
  );
  const visible = showAll ? sorted : sorted.slice(0, RECENT);

  return (
    <section className={styles.block}>
      <div className={styles.blockH}>
        <h2>Lịch sử</h2>
      </div>
      {sorted.length === 0 ? (
        <div className={`${styles.body} ${styles.small}`}>Chưa có mốc nào.</div>
      ) : (
        <>
          <ol className={styles.timeline}>
            {visible.map((h) => {
              const negative = h.action === "Return" || h.action === "Reopen";
              return (
                <li key={h.id} className={negative ? styles.negative : undefined}>
                  <div>
                    <b>
                      {h.actionName}
                      {h.stepName ? ` · ${h.stepName}` : ""}
                    </b>
                    <small className="num">
                      {formatDateTime(h.createdAt)} · {h.actorName}
                    </small>
                    {h.reason && <small className={negative ? styles.late : undefined}>Lý do: {h.reason}</small>}
                    {h.gradeBefore !== h.gradeAfter && (
                      <small>Mức: {gradeLabel(h.gradeBefore)} → {gradeLabel(h.gradeAfter)}</small>
                    )}
                    {h.scoreBefore !== h.scoreAfter && (
                      <small className="num">Điểm: {h.scoreBefore ?? "—"} → {h.scoreAfter ?? "—"}</small>
                    )}
                  </div>
                </li>
              );
            })}
          </ol>
          {sorted.length > RECENT && (
            <div className={styles.timelineMore}>
              <button type="button" className={styles.linkBtn} onClick={() => setShowAll(!showAll)}>
                {showAll ? "Thu gọn" : `Xem tất cả (${sorted.length})`}
              </button>
            </div>
          )}
        </>
      )}
    </section>
  );
}

export default RecordHistory;
