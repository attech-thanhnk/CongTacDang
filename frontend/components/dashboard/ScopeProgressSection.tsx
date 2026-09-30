"use client";

import React, { useMemo } from "react";
import Link from "next/link";
import type { EvaluationRecordDto } from "@/services/evaluationService";
import type { BadgeTone } from "@/components/common/StatusBadge";
import styles from "./dashboard.module.css";

/** Tông theo trạng thái, cùng quy ước với RecordStatusBadge. */
const STATUS_TONES: Record<string, BadgeTone> = {
  AwaitingRegistration: "info",
  AwaitingSelfScore: "info",
  Published: "success",
};

function toneOf(status: string): BadgeTone {
  return STATUS_TONES[status] ?? (status.startsWith("Awaiting") ? "warning" : "neutral");
}

/** Tiến độ hồ sơ trong phạm vi được xem: số theo trạng thái và bảng theo Chi bộ. */
export function ScopeProgressSection({ records, loading }: { records: EvaluationRecordDto[]; loading: boolean }) {
  const statusCounts = useMemo(() => {
    const map = new Map<string, { name: string; count: number; order: number }>();
    for (const r of records) {
      const entry = map.get(r.status) || { name: r.statusDisplayName, count: 0, order: r.progress.findIndex((s) => s.state === "current") };
      entry.count++;
      map.set(r.status, entry);
    }
    // Sắp theo thứ tự bước; đã công bố (không còn bước hiện hành) xếp cuối.
    return Array.from(map.entries())
      .map(([status, v]) => ({ status, ...v }))
      .sort((a, b) => (a.order < 0 ? 99 : a.order) - (b.order < 0 ? 99 : b.order));
  }, [records]);

  const returnedCount = useMemo(() => records.filter((r) => !!r.returnReason).length, [records]);

  const byCell = useMemo(() => {
    const map = new Map<string, { total: number; published: number }>();
    for (const r of records) {
      const key = r.partyCellName || "Chưa thuộc Chi bộ";
      const entry = map.get(key) || { total: 0, published: 0 };
      entry.total++;
      if (r.status === "Published") entry.published++;
      map.set(key, entry);
    }
    return Array.from(map.entries()).sort(([a], [b]) => a.localeCompare(b, "vi"));
  }, [records]);

  return (
    <section className={styles.section}>
      <div className={styles.sectionHeader}>
        <h2 className={styles.sectionTitle}>
          Tiến độ trong phạm vi
          <span className="badge text-bg-light border">{records.length} hồ sơ</span>
        </h2>
        <Link href="/evaluations" className="btn btn-outline-secondary btn-sm">
          Danh sách hồ sơ
        </Link>
      </div>

      {loading ? (
        <div className={styles.empty}>
          <span className="spinner-border spinner-border-sm me-2" />
          Đang tải...
        </div>
      ) : records.length === 0 ? (
        <div className={styles.empty}>Chưa có hồ sơ nào trong phạm vi của bạn.</div>
      ) : (
        <div className={styles.sectionBody}>
          <div className={styles.tiles}>
            {statusCounts.map((s) => (
              <div key={s.status} className={`${styles.tile} ${styles[`tone-${toneOf(s.status)}`]}`}>
                <div className={styles.tileValue}>{s.count}</div>
                <div className={styles.tileLabel}>{s.name}</div>
              </div>
            ))}
            {returnedCount > 0 && (
              <div className={`${styles.tile} ${styles["tone-danger"]}`}>
                <div className={styles.tileValue}>{returnedCount}</div>
                <div className={styles.tileLabel}>Bị trả lại</div>
              </div>
            )}
          </div>

          <div className="table-responsive">
            <table className={`table table-sm align-middle mb-0 ${styles.table}`}>
              <thead>
                <tr>
                  <th>Chi bộ</th>
                  <th className="text-center">Số hồ sơ</th>
                  <th className="text-center">Đã công bố</th>
                  <th style={{ width: "30%" }}>Tỷ lệ</th>
                </tr>
              </thead>
              <tbody>
                {byCell.map(([name, v]) => {
                  const percent = v.total ? Math.round((v.published / v.total) * 100) : 0;
                  return (
                    <tr key={name}>
                      <td>{name}</td>
                      <td className="text-center">{v.total}</td>
                      <td className="text-center">{v.published}</td>
                      <td>
                        <div className="d-flex align-items-center gap-2">
                          <div className={`${styles.bar} flex-grow-1`}>
                            <div className={styles.barFill} style={{ width: `${percent}%` }} />
                          </div>
                          <span className={styles.muted}>{percent}%</span>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </section>
  );
}

export default ScopeProgressSection;
