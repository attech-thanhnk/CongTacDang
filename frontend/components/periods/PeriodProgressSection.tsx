"use client";

import React, { useMemo, useState } from "react";
import Link from "next/link";
import { formatDate } from "@/lib/dateFormat";
import { PeriodParticipantDto } from "@/services/evaluationService";

/** Còn từ ngần này ngày trở xuống thì báo "sắp đến hạn". */
const DUE_SOON_DAYS = 3;

const STATUS_ORDER = [
  "AwaitingRegistration",
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
  const [onlyUrgent, setOnlyUrgent] = useState(false);

  const departments = useMemo(
    () => Array.from(new Set(participants.map((p) => p.departmentName).filter((v): v is string => !!v))).sort((a, b) => a.localeCompare(b, "vi")),
    [participants]
  );
  const cells = useMemo(
    () => Array.from(new Set(participants.map((p) => p.partyCellName).filter((v): v is string => !!v))).sort((a, b) => a.localeCompare(b, "vi")),
    [participants]
  );

  /** Lọc theo Phòng, Chi bộ (chưa lọc bước) — để đếm số hồ sơ từng bước. */
  const base = useMemo(
    () => participants.filter((p) => (!department || p.departmentName === department) && (!cell || p.partyCellName === cell)),
    [participants, department, cell]
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
    const list = base.filter((p) => (!status || p.status === status) && (!onlyUrgent || isOverdue(p) || isDueSoon(p)));
    return [...list].sort((a, b) => urgency(a) - urgency(b) || a.fullName.localeCompare(b.fullName, "vi"));
  }, [base, status, onlyUrgent]);

  return (
    <section className="card">
      <div className="card-header d-flex flex-wrap justify-content-between align-items-center gap-2">
        <h2 className="mb-0">Tiến độ <span className="text-secondary fw-medium num">{rows.length}</span></h2>
        <div className="d-flex flex-wrap align-items-center gap-2">
          <select className="form-select form-select-sm w-auto" aria-label="Lọc theo Phòng" value={department} onChange={(e) => setDepartment(e.target.value)}>
            <option value="">Tất cả Phòng</option>
            {departments.map((d) => <option key={d} value={d}>{d}</option>)}
          </select>
          <select className="form-select form-select-sm w-auto" aria-label="Lọc theo Chi bộ" value={cell} onChange={(e) => setCell(e.target.value)}>
            <option value="">Tất cả Chi bộ</option>
            {cells.map((c) => <option key={c} value={c}>{c}</option>)}
          </select>
          <select className="form-select form-select-sm w-auto" aria-label="Lọc theo bước" value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="">Tất cả các bước</option>
            {steps.map((s) => <option key={s.key} value={s.key}>{s.label}</option>)}
          </select>
          <div className="form-check mb-0">
            <input className="form-check-input" type="checkbox" id="progress-urgent" checked={onlyUrgent} onChange={(e) => setOnlyUrgent(e.target.checked)} />
            <label className="form-check-label" htmlFor="progress-urgent">Chỉ hồ sơ quá hạn / sắp đến hạn</label>
          </div>
        </div>
      </div>

      {base.length > 0 && (
        <div className="card-body border-bottom d-flex flex-wrap align-items-center gap-2" role="group" aria-label="Số hồ sơ theo bước">
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
          {(overdueCount > 0 || dueSoonCount > 0) && (
            <span className="ms-auto d-flex gap-2">
              {overdueCount > 0 && <span className="tag t-bad">Quá hạn <span className="num">{overdueCount}</span></span>}
              {dueSoonCount > 0 && <span className="tag t-wait">Sắp đến hạn <span className="num">{dueSoonCount}</span></span>}
            </span>
          )}
        </div>
      )}

      {rows.length === 0 ? (
        <div className="card-body text-secondary">Không có hồ sơ.</div>
      ) : (
        <div className="table-responsive">
          <table className="table table-hover align-middle mb-0">
            <thead>
              <tr><th>Cán bộ</th><th>Phòng</th><th>Chi bộ</th><th>Bước đang chờ</th><th>Hạn</th><th>Tình trạng hạn</th></tr>
            </thead>
            <tbody>
              {rows.map((p) => (
                <tr key={p.recordId}>
                  <td><Link href={`/evaluations/${p.recordId}`} className="fw-semibold">{p.fullName}</Link></td>
                  <td>{p.departmentName || "—"}</td>
                  <td>{p.partyCellName || "—"}</td>
                  <td>{p.currentStep ? p.statusDisplayName : <span className="tag t-ok">{p.statusDisplayName}</span>}</td>
                  <td className="num">{p.currentStep ? formatDate(p.currentStepDeadline) : ""}</td>
                  <td><DeadlineState participant={p} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function DeadlineState({ participant: p }: { participant: PeriodParticipantDto }) {
  if (!p.currentStep) return null;
  if (isOverdue(p)) return <span className="tag t-bad">Quá hạn <span className="num">{p.daysOverdue}</span> ngày</span>;
  if (isDueSoon(p))
    return <span className="tag t-wait">{p.daysLeft === 0 ? "Hạn hôm nay" : <>Còn <span className="num">{p.daysLeft}</span> ngày</>}</span>;
  return null;
}
