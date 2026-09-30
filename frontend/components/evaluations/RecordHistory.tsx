"use client";

import React, { useMemo, useState } from "react";
import { EvaluationRecordHistoryDto, gradeLabel } from "@/services/evaluationService";
import { formatDateTime } from "@/lib/dateFormat";

const RECENT = 5;

/** Lịch sử hồ sơ dạng dòng thời gian gọn: mới nhất trước, mặc định 5 mục gần nhất. */
export function RecordHistory({ history }: { history: EvaluationRecordHistoryDto[] }) {
  const [showAll, setShowAll] = useState(false);
  const sorted = useMemo(
    () => [...history].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()),
    [history]
  );
  const visible = showAll ? sorted : sorted.slice(0, RECENT);

  return (
    <section className="card border-0 shadow-sm">
      <div className="card-body">
        <h2 className="h6 mb-3">Lịch sử hồ sơ</h2>
        {sorted.length === 0 ? (
          <div className="text-secondary" style={{ fontSize: 14 }}>Chưa có mốc nào.</div>
        ) : (
          <>
            <ol className="list-unstyled mb-0 position-relative ps-3" style={{ borderLeft: "2px solid #e2e8f0", marginLeft: 6 }}>
              {visible.map((h) => {
                const negative = h.action === "Return" || h.action === "Reopen";
                return (
                  <li key={h.id} className="position-relative pb-3" style={{ fontSize: 14 }}>
                    <span
                      className="position-absolute rounded-circle"
                      style={{ left: -22, top: 5, width: 10, height: 10, background: negative ? "#dc2626" : "#3b82f6", border: "2px solid #fff" }}
                      aria-hidden="true"
                    />
                    <div className="fw-semibold">
                      {h.actionName}
                      {h.stepName ? <span className="fw-normal text-secondary"> · {h.stepName}</span> : null}
                    </div>
                    <div className="text-secondary" style={{ fontSize: 13 }}>
                      {formatDateTime(h.createdAt)} · {h.actorName}
                    </div>
                    {h.reason && <div className={negative ? "text-danger" : undefined}>Lý do: {h.reason}</div>}
                    {h.gradeBefore !== h.gradeAfter && (
                      <div className="text-secondary" style={{ fontSize: 13 }}>Mức: {gradeLabel(h.gradeBefore)} → {gradeLabel(h.gradeAfter)}</div>
                    )}
                    {h.scoreBefore !== h.scoreAfter && (
                      <div className="text-secondary" style={{ fontSize: 13 }}>Điểm: {h.scoreBefore ?? "—"} → {h.scoreAfter ?? "—"}</div>
                    )}
                  </li>
                );
              })}
            </ol>
            {sorted.length > RECENT && (
              <button type="button" className="btn btn-link btn-sm p-0" onClick={() => setShowAll(!showAll)}>
                {showAll ? "Thu gọn" : `Xem tất cả (${sorted.length})`}
              </button>
            )}
          </>
        )}
      </div>
    </section>
  );
}

export default RecordHistory;
