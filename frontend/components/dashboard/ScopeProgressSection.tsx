"use client";

import React, { useMemo, useState } from "react";
import Link from "next/link";
import type { EvaluationRecordDto } from "@/services/evaluationService";
import styles from "./dashboard.module.css";

/** Trạng thái thuộc giai đoạn cá nhân đăng ký / tự chấm. */
const SELF_STAGE = new Set(["AwaitingRegistration", "AwaitingTaskApproval", "AwaitingSelfScore"]);

type Stage = "self" | "review" | "returned" | "published";

/** Thứ tự, nhãn và màu giai đoạn — ô số liệu dùng làm chú giải cho thanh tiến độ; "đang tự chấm" là phần nền chưa tô. */
const STAGES: { key: Stage; label: string; color: string; href: string }[] = [
  { key: "self", label: "Đang tự chấm", color: "var(--rule-strong)", href: "/evaluations" },
  { key: "review", label: "Đang xét duyệt", color: "var(--wait)", href: "/evaluations" },
  { key: "returned", label: "Bị trả lại", color: "var(--bad)", href: "/evaluations?status=__returned" },
  { key: "published", label: "Đã công bố", color: "var(--ok)", href: "/evaluations?status=Published" },
];

const stageOf = (r: EvaluationRecordDto): Stage =>
  r.status === "Published" ? "published" : r.returnReason ? "returned" : SELF_STAGE.has(r.status) ? "self" : "review";

type Counts = Record<Stage, number> & { total: number };
const emptyCounts = (): Counts => ({ self: 0, review: 0, returned: 0, published: 0, total: 0 });

/** Số dòng tổ chức Đảng hiện sẵn; còn lại mở bằng nút. */
const VISIBLE_CELLS = 10;

/** Hồ sơ trong phạm vi: số theo giai đoạn, và (khi phạm vi gồm nhiều tổ chức Đảng) thanh tiến độ từng tổ chức. */
export function ScopeProgressSection({ records, loading }: { records: EvaluationRecordDto[]; loading: boolean }) {
  const [showAll, setShowAll] = useState(false);
  const { totals, byCell } = useMemo(() => {
    const totals = emptyCounts();
    const map = new Map<string, Counts>();
    for (const r of records) {
      const stage = stageOf(r);
      totals[stage]++;
      totals.total++;
      const key = r.partyCellName || "";
      const entry = map.get(key) ?? emptyCounts();
      entry[stage]++;
      entry.total++;
      map.set(key, entry);
    }
    // Hồ sơ chưa gắn tổ chức Đảng xếp cuối.
    const byCell = Array.from(map.entries()).sort(([a], [b]) => (!a ? 1 : !b ? -1 : a.localeCompare(b, "vi")));
    return { totals, byCell };
  }, [records]);

  return (
    <section className={styles.block}>
      <div className={styles.blockH}>
        <h2>Hồ sơ trong phạm vi</h2>
        <Link href="/evaluations" className={styles.link}>
          Danh sách hồ sơ
        </Link>
      </div>

      <div className={styles.stats}>
        {STAGES.map((s) => (
          <Link key={s.key} href={s.href} className={styles.stat} style={{ "--c": s.color } as React.CSSProperties}>
            <div className={styles.statV}>{loading ? "…" : totals[s.key]}</div>
            <div className={styles.statL}>{s.label}</div>
          </Link>
        ))}
      </div>

      {!loading && records.length === 0 && <div className={`${styles.empty} ${styles.topRule}`}>Chưa có hồ sơ trong phạm vi</div>}

      {!loading && byCell.length > 1 && (
        <div className={styles.cells}>
          <div className={`${styles.cellRow} ${styles.cellHead}`} aria-hidden="true">
            <span>Tổ chức Đảng</span>
            <span className={styles.right}>Hồ sơ</span>
            <span>Tiến độ</span>
          </div>
          {(showAll ? byCell : byCell.slice(0, VISIBLE_CELLS)).map(([name, c]) => {
            const summary = STAGES.filter((s) => c[s.key] > 0)
              .map((s) => `${s.label} ${c[s.key]}`)
              .join(", ");
            return (
              <Link
                key={name || "none"}
                href={name ? `/evaluations?partyCell=${encodeURIComponent(name)}` : "/evaluations"}
                className={styles.cellRow}
                title={`${name || "Chưa thuộc tổ chức Đảng"}: ${summary}`}
              >
                <span className={`${styles.cellName} ${name ? "" : styles.muted}`}>{name || "Chưa thuộc tổ chức Đảng"}</span>
                <span className={`${styles.right} num`}>{c.total}</span>
                <span className={styles.seg} role="img" aria-label={summary}>
                  {STAGES.map((s) =>
                    s.key !== "self" && c[s.key] > 0 ? <i key={s.key} style={{ width: `${(c[s.key] / c.total) * 100}%`, background: s.color }} /> : null
                  )}
                </span>
              </Link>
            );
          })}
          {byCell.length > VISIBLE_CELLS && (
            <button type="button" className={styles.moreBtn} onClick={() => setShowAll((v) => !v)} aria-expanded={showAll}>
              {showAll ? "Thu gọn" : `Hiện tất cả ${byCell.length} tổ chức`}
              <i className={`bi ${showAll ? "bi-chevron-up" : "bi-chevron-down"} ms-1`} aria-hidden="true" />
            </button>
          )}
        </div>
      )}
    </section>
  );
}

export default ScopeProgressSection;
