"use client";

import React, { useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import type { ScopeProgressDto } from "@/services/dashboardService";
import styles from "./dashboard.module.css";
import { STEP_STATUS, dayMonth, stepPalette } from "./stepUtils";

/** Số dòng tổ chức Đảng hiện sẵn; còn lại mở bằng nút. */
const VISIBLE_CELLS = 10;

const listLink = (status: string) => `/evaluations?status=${encodeURIComponent(status)}`;

/** Tiến độ hồ sơ trong phạm vi: đếm theo bước hiện tại, quá hạn / sắp đến hạn, danh sách chậm hạn, thanh theo tổ chức Đảng. */
export function ScopeProgressSection({ scope }: { scope: ScopeProgressDto }) {
  const router = useRouter();
  const [showAll, setShowAll] = useState(false);
  const colors = useMemo(() => stepPalette(scope.steps.map((s) => s.step)), [scope.steps]);
  const groups = showAll ? scope.groups : scope.groups.slice(0, VISIBLE_CELLS);

  return (
    <section className={styles.block} aria-label="Tiến độ trong phạm vi">
      <div className={styles.blockH}>
        <h2>
          Tiến độ trong phạm vi <span className={styles.count}>{scope.total}</span>
        </h2>
        {scope.overdue > 0 && (
          <Link href={listLink("__overdue")} className={`tag t-bad ${styles.flagTag}`}>
            Quá hạn <b className="num">{scope.overdue}</b>
          </Link>
        )}
        {scope.dueSoon > 0 && (
          <span className={`tag t-wait ${styles.flagTag}`}>
            Đến hạn trong {scope.dueSoonWorkingDays} ngày làm việc <b className="num">{scope.dueSoon}</b>
          </span>
        )}
        <Link href="/evaluations" className={styles.link}>
          Danh sách hồ sơ
        </Link>
      </div>

      <div className={styles.stepCounts}>
        {scope.steps.map((s) => (
          <Link
            key={s.step}
            href={listLink(STEP_STATUS[s.step] ?? "")}
            className={styles.scopeItem}
            style={{ "--c": colors[s.step] } as React.CSSProperties}
          >
            {s.name} <b className="num">{s.count}</b>
            {s.overdue > 0 && <span className={`${styles.late} num`}>({s.overdue} quá hạn)</span>}
          </Link>
        ))}
      </div>

      {scope.late.length > 0 && (
        <div className={styles.lateList}>
          <div className={styles.groupH}>
            <h3>
              Chậm hạn <span className={styles.count}>{scope.overdue}</span>
            </h3>
            {scope.overdue > scope.late.length && (
              <Link href={listLink("__overdue")} className={styles.link}>
                Xem tất cả
              </Link>
            )}
          </div>
          <div className={styles.tw}>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th>Cán bộ</th>
                  <th>Bước</th>
                  <th>Người phải làm</th>
                  <th className={styles.right}>Quá hạn</th>
                </tr>
              </thead>
              <tbody>
                {scope.late.map((r) => (
                  <tr key={r.recordId} className={styles.rowLink} onClick={() => router.push(`/evaluations/${r.recordId}`)}>
                    <td>
                      <Link href={`/evaluations/${r.recordId}`} className={styles.name}>
                        {r.fullName}
                      </Link>
                    </td>
                    <td>{r.stepName}</td>
                    <td className={styles.muted}>{r.performer}</td>
                    <td className={`${styles.right} ${styles.late} ${styles.nowrap} num`} title={`Hạn ${dayMonth(r.deadline)}`}>
                      {r.daysOverdue} ngày
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {scope.groups.length > 1 && (
        <div className={styles.cells}>
          <div className={`${styles.cellRow} ${styles.cellRow4} ${styles.cellHead}`}>
            <span>Tổ chức Đảng</span>
            <span className={styles.right}>Hồ sơ</span>
            <span className={styles.right}>Quá hạn</span>
            <span>Theo bước</span>
          </div>
          {groups.map((g) => {
            const summary = g.steps.map((s) => `${s.name} ${s.count}`).join(", ");
            const label = g.name || "Chưa thuộc tổ chức Đảng";
            return (
              <Link
                key={g.partyCellId ?? "none"}
                href={g.name ? `/evaluations?partyCell=${encodeURIComponent(g.name)}` : "/evaluations"}
                className={`${styles.cellRow} ${styles.cellRow4}`}
                title={`${label}: ${summary}`}
              >
                <span className={`${styles.cellName} ${g.name ? "" : styles.muted}`}>{label}</span>
                <span className={`${styles.right} num`}>{g.total}</span>
                <span className={`${styles.right} num ${g.overdue > 0 ? styles.late : styles.muted}`}>{g.overdue > 0 ? g.overdue : "–"}</span>
                <span className={styles.seg} role="img" aria-label={summary}>
                  {g.steps.map((s) => (
                    <i key={s.step} title={`${s.name} ${s.count}`} style={{ width: `${(s.count / g.total) * 100}%`, background: colors[s.step] }} />
                  ))}
                </span>
              </Link>
            );
          })}
          {scope.groups.length > VISIBLE_CELLS && (
            <button type="button" className={styles.moreBtn} onClick={() => setShowAll((v) => !v)} aria-expanded={showAll}>
              {showAll ? "Thu gọn" : `Hiện tất cả ${scope.groups.length} tổ chức`}
              <i className={`fa-solid ${showAll ? "fa-chevron-up" : "fa-chevron-down"} ms-1`} aria-hidden="true" />
            </button>
          )}
        </div>
      )}
    </section>
  );
}

export default ScopeProgressSection;
