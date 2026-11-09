"use client";

import { useCatalogOptions } from "@/components/admin/useCatalogOptions";
import { nameFilterOptions, subtreeNames } from "@/services/catalogService";
import React, { useMemo, useState } from "react";
import { Pagination } from "@/components/common/Pagination";
import { usePagination } from "@/components/common/usePagination";
import Link from "next/link";
import { formatDate } from "@/lib/dateFormat";
import { PeriodParticipantDto } from "@/services/evaluationService";
import styles from "./periods.module.css";

/** Còn từ ngần này ngày trở xuống thì báo "sắp đến hạn". */
const DUE_SOON_DAYS = 3;

const STATUS_ORDER = [
  "AwaitingRegistration",
  "AwaitingUnitSubmit",
  "AwaitingTaskReview",
  "AwaitingTaskApproval",
  "AwaitingSelfScore",
  "AwaitingCellConfirm",
  "AwaitingCollective",
  "AwaitingAppraisal",
  "AwaitingDirectorReview",
  "AwaitingDecision",
  "AwaitingPublish",
  "Published",
];
const statusRank = (status: string) => {
  const index = STATUS_ORDER.indexOf(status);
  return index < 0 ? STATUS_ORDER.length : index;
};

const isOverdue = (p: PeriodParticipantDto) => (p.daysOverdue ?? 0) > 0;
const isDueSoon = (p: PeriodParticipantDto) => p.daysLeft != null && p.daysLeft <= DUE_SOON_DAYS;

/** Thứ tự ưu tiên đôn đốc: quá hạn lâu nhất → sắp đến hạn gần nhất → còn hạn → không có hạn → đã công bố. */
const urgency = (p: PeriodParticipantDto): number => {
  if (!p.currentStep) return 1_000_000;
  if (p.daysOverdue != null) return -p.daysOverdue;
  if (p.daysLeft != null) return p.daysLeft;
  return 100_000;
};

interface Props {
  participants: PeriodParticipantDto[];
}

/** Tiến độ kỳ: số hồ sơ theo bước, ai đang ở bước nào, quá hạn / sắp đến hạn. */
export function PeriodProgressSection({ participants }: Props) {
  const [department, setDepartment] = useState("");
  const [cell, setCell] = useState("");
  const [status, setStatus] = useState("");
  // Bấm nhãn đếm "Quá hạn" / "Sắp đến hạn" để lọc, bấm lại để bỏ lọc.
  const [urgent, setUrgent] = useState<"" | "overdue" | "soon">("");

  const { departments: departmentTree, partyCells: cellTree } = useCatalogOptions();
  const departments = useMemo(
    () => nameFilterOptions(departmentTree, participants.map((x) => x.departmentName).filter((v): v is string => !!v)),
    [departmentTree, participants]
  );
  const cells = useMemo(
    () => nameFilterOptions(cellTree, participants.map((x) => x.partyCellName).filter((v): v is string => !!v)),
    [cellTree, participants]
  );
  const departmentNames = useMemo(() => (department ? subtreeNames(departmentTree, department) : null), [departmentTree, department]);
  const cellNames = useMemo(() => (cell ? subtreeNames(cellTree, cell) : null), [cellTree, cell]);

  /** Lọc theo Phòng, Chi bộ (chưa lọc bước) — để đếm số hồ sơ từng bước. */
  const base = useMemo(
    () => participants.filter((p) => (!departmentNames || departmentNames.has(p.departmentName ?? "")) && (!cellNames || cellNames.has(p.partyCellName ?? ""))),
    [participants, departmentNames, cellNames]
  );

  const steps = useMemo(() => {
    const map = new Map<string, { label: string; count: number }>();
    base.forEach((p) => {
      const item = map.get(p.status) ?? { label: p.statusDisplayName, count: 0 };
      item.count += 1;
      map.set(p.status, item);
    });
    return Array.from(map.entries())
      .sort(([a], [b]) => statusRank(a) - statusRank(b))
      .map(([key, item]) => ({ key, ...item }));
  }, [base]);

  const overdueCount = base.filter(isOverdue).length;
  const dueSoonCount = base.filter(isDueSoon).length;

  const rows = useMemo(() => {
    const list = base.filter((p) =>
      (!status || p.status === status) && (urgent === "" || (urgent === "overdue" ? isOverdue(p) : isDueSoon(p))));
    // Cùng mức khẩn giữ thứ tự chức danh do máy chủ trả về.
    return [...list].sort((a, b) => urgency(a) - urgency(b));
  }, [base, status, urgent]);
  const paging = usePagination(rows, { storageKey: "period.progress", resetKeys: [department, cell, status, urgent] });

  return (
    <section className="card">
      <div className={`card-body ${styles.toolbar}`}>
        <select className="form-select form-select-sm" aria-label="Lọc theo đơn vị" value={department} onChange={(e) => setDepartment(e.target.value)}>
          <option value="">Tất cả đơn vị</option>
          {departments.map((d) => <option key={d.value} value={d.value}>{d.label}</option>)}
        </select>
        <select className="form-select form-select-sm" aria-label="Lọc theo Chi bộ" value={cell} onChange={(e) => setCell(e.target.value)}>
          <option value="">Tất cả Chi bộ</option>
          {cells.map((c) => <option key={c.value} value={c.value}>{c.label}</option>)}
        </select>
        {(overdueCount > 0 || dueSoonCount > 0 || urgent !== "") && (
          <span className={styles.toolbarEnd}>
            {(overdueCount > 0 || urgent === "overdue") && (
              <button type="button" className={`tag t-bad ${styles.tagBtn} ${urgent === "overdue" ? styles.tagOn : ""}`} aria-pressed={urgent === "overdue"}
                onClick={() => setUrgent(urgent === "overdue" ? "" : "overdue")}>
                Quá hạn <span className="num">{overdueCount}</span>
              </button>
            )}
            {(dueSoonCount > 0 || urgent === "soon") && (
              <button type="button" className={`tag t-wait ${styles.tagBtn} ${urgent === "soon" ? styles.tagOn : ""}`} aria-pressed={urgent === "soon"}
                onClick={() => setUrgent(urgent === "soon" ? "" : "soon")}>
                Sắp đến hạn <span className="num">{dueSoonCount}</span>
              </button>
            )}
          </span>
        )}
      </div>

      {(steps.length > 1 || status !== "") && (
        <div className="card-body border-top d-flex flex-wrap align-items-center gap-2" role="group" aria-label="Lọc theo bước đang chờ">
          {steps.map((s) => {
            const active = status === s.key;
            return (
              <button key={s.key} type="button" aria-pressed={active}
                className={`btn btn-sm ${active ? "btn-primary" : "btn-outline-secondary"}`}
                onClick={() => setStatus(active ? "" : s.key)}>
                {s.label} <span className="num">{s.count}</span>
              </button>
            );
          })}
        </div>
      )}

      {rows.length === 0 ? (
        <div className="card-body border-top text-secondary">Không có hồ sơ.</div>
      ) : (
        <>
        <div className="table-responsive">
          <table className={`table table-hover align-middle mb-0 ${styles.compactTable} ${styles.progressCards}`}>
            <thead>
              <tr>
                <th>Cán bộ</th>
                <th className={styles.unitCol}>Đơn vị</th>
                <th>Bước đang chờ</th>
                <th>Hạn</th>
              </tr>
            </thead>
            <tbody>
              {paging.pageItems.map((p) => (
                <tr key={p.recordId}>
                  <td className={styles.nameCell}><Link href={`/evaluations/${p.recordId}`} className="fw-semibold">{p.fullName}</Link></td>
                  <td className={styles.unitCol}>{p.departmentName || "—"}<div className={styles.unitSub}>{p.partyCellName || "—"}</div></td>
                  <td>{p.currentStep ? p.statusDisplayName : <span className="tag t-ok">{p.statusDisplayName}</span>}</td>
                  <td className="text-nowrap"><Deadline participant={p} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <Pagination
          page={paging.page}
          pageSize={paging.pageSize}
          total={paging.total}
          onPageChange={paging.setPage}
          onPageSizeChange={paging.setPageSize}
        />
        </>
      )}
    </section>
  );
}

/** "dd/MM · còn N ngày" trong một ô; quá hạn chữ đỏ, sắp đến hạn chữ vàng. */
function Deadline({ participant: p }: { participant: PeriodParticipantDto }) {
  if (!p.currentStep || !p.currentStepDeadline) return null;
  const day = formatDate(p.currentStepDeadline).slice(0, 5);
  const state = isOverdue(p)
    ? `quá hạn ${p.daysOverdue} ngày`
    : p.daysLeft === 0
      ? "hôm nay"
      : p.daysLeft != null
        ? `còn ${p.daysLeft} ngày`
        : "";
  const tone = isOverdue(p) ? styles.late : isDueSoon(p) ? styles.soon : "";
  return <span className={`num ${tone}`}>{day}{state && ` · ${state}`}</span>;
}
