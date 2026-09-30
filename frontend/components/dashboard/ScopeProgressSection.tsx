"use client";

import React, { useMemo } from "react";
import Link from "next/link";
import type { EvaluationRecordDto } from "@/services/evaluationService";
import styles from "./dashboard.module.css";

/** Trạng thái thuộc giai đoạn cá nhân đăng ký / tự chấm. */
const SELF_STAGE = new Set(["AwaitingRegistration", "AwaitingTaskApproval", "AwaitingSelfScore"]);

/** Dải số liệu trong phạm vi được xem: đang tự chấm, đang xét duyệt, bị trả lại, đã công bố. */
export function ScopeStats({ records, loading }: { records: EvaluationRecordDto[]; loading: boolean }) {
  const counts = useMemo(() => {
    let self = 0;
    let review = 0;
    let returned = 0;
    let published = 0;
    for (const r of records) {
      if (r.status === "Published") published++;
      else if (r.returnReason) returned++;
      else if (SELF_STAGE.has(r.status)) self++;
      else review++;
    }
    return { self, review, returned, published };
  }, [records]);

  const items: [string, number, string][] = [
    ["Đang tự chấm", counts.self, "var(--info)"],
    ["Đang xét duyệt", counts.review, "var(--wait)"],
    ["Bị trả lại", counts.returned, "var(--bad)"],
    ["Đã công bố", counts.published, "var(--ok)"],
  ];

  return (
    <section className={styles.block} aria-label="Số liệu hồ sơ">
      <div className={styles.stats}>
        {items.map(([label, value, color]) => (
          <Link key={label} href="/evaluations" className={styles.stat} style={{ "--c": color } as React.CSSProperties}>
            <div className={styles.statV}>{loading ? "…" : value}</div>
            <div className={styles.statL}>{label}</div>
          </Link>
        ))}
      </div>
    </section>
  );
}

/** Tiến độ theo Chi bộ: số hồ sơ, số đã công bố, thanh tỷ lệ. */
export function ScopeProgressSection({ records, loading }: { records: EvaluationRecordDto[]; loading: boolean }) {
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
    <section className={styles.block}>
      <div className={styles.blockH}>
        <h2>
          Tiến độ theo Chi bộ <span className={styles.count}>{records.length}</span>
        </h2>
        <Link href="/evaluations" className={styles.link}>
          Danh sách hồ sơ
        </Link>
      </div>

      {loading ? (
        <div className={styles.empty}>
          <span className="spinner-border spinner-border-sm me-2" />
          Đang tải...
        </div>
      ) : records.length === 0 ? (
        <div className={styles.empty}>Chưa có hồ sơ trong phạm vi</div>
      ) : (
        <div className={styles.tw}>
          <table className={styles.table}>
            <thead>
              <tr>
                <th>Chi bộ</th>
                <th className={styles.right}>Hồ sơ</th>
                <th className={styles.right}>Công bố</th>
                <th style={{ width: "38%" }}>Tiến độ</th>
              </tr>
            </thead>
            <tbody>
              {byCell.map(([name, v]) => {
                const percent = v.total ? Math.round((v.published / v.total) * 100) : 0;
                return (
                  <tr key={name}>
                    <td>{name}</td>
                    <td className={`${styles.right} num`}>{v.total}</td>
                    <td className={`${styles.right} num`}>{v.published}</td>
                    <td>
                      <div className={styles.barRow}>
                        <div className={styles.bar}>
                          <i style={{ width: `${percent}%` }} />
                        </div>
                        <span className={styles.barPct}>{percent}%</span>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

export default ScopeProgressSection;
