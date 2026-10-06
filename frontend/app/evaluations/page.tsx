"use client";

import React, { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { GradeBadge } from "@/components/common/StatusBadge";
import { RecordTag } from "@/components/dashboard/RecordTag";
import { initialsOf } from "@/components/dashboard/taskUtils";
import styles from "@/components/dashboard/dashboard.module.css";
import { useAuth } from "@/contexts/AuthContext";
import { usePeriod } from "@/contexts/PeriodContext";
import { BranchQuotaCheckDto, EvaluationRecordDto, evaluationService } from "@/services/evaluationService";

/** Thứ tự trạng thái theo luồng (để sắp xếp và xếp nút lọc). */
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

/** Hồ sơ quá hạn: bước hiện tại đã quá thời hạn. */
const isOverdue = (r: EvaluationRecordDto) => r.progress?.some((p) => p.state === "current" && p.overdue) ?? false;
const displayGrade = (r: EvaluationRecordDto) => (r.finalGrade && r.finalGrade !== "ChuaXepLoai" ? r.finalGrade : r.appraisalProposedGrade);

type SortKey = "name" | "status";
const RETURNED = "__returned";
const OVERDUE = "__overdue";

/**
 * Danh sách hồ sơ đánh giá của kỳ đang chọn: hồ sơ của tôi (nếu có) và các hồ sơ trong phạm vi được xem
 * (máy chủ lọc theo phạm vi). Thao tác trên hồ sơ thực hiện ở trang hồ sơ theo `actions`.
 */
export default function EvaluationsPage() {
  const { hasPermission } = useAuth();
  const router = useRouter();
  const canRead = hasPermission("evaluation.read");
  const { periods, periodId, loading: periodsLoading } = usePeriod();
  const [myRecord, setMyRecord] = useState<EvaluationRecordDto | null>(null);
  const [records, setRecords] = useState<EvaluationRecordDto[]>([]);
  const [quotas, setQuotas] = useState<BranchQuotaCheckDto[]>([]);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [department, setDepartment] = useState("");
  const [partyCell, setPartyCell] = useState("");
  const [sort, setSort] = useState<{ key: SortKey; desc: boolean }>({ key: "name", desc: false });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!periodId) {
      setMyRecord(null);
      setRecords([]);
      setQuotas([]);
      setLoading(periodsLoading);
      return;
    }
    let mounted = true;
    setLoading(true);
    setError(null);
    setStatusFilter("");
    setDepartment("");
    setPartyCell("");
    Promise.all([
      evaluationService.getMyRecord(periodId).catch(() => null),
      canRead ? evaluationService.getRecordsByPeriod(periodId) : Promise.resolve([] as EvaluationRecordDto[]),
      canRead ? evaluationService.checkBranchQuotas(periodId).catch(() => []) : Promise.resolve([] as BranchQuotaCheckDto[]),
    ])
      .then(([mine, list, quota]) => {
        if (!mounted) return;
        setMyRecord(mine);
        setRecords(list);
        setQuotas(quota.filter((q) => q.totalCadres > 0));
      })
      .catch((err: any) => mounted && setError(err?.message || "Không tải được hồ sơ đánh giá."))
      .finally(() => mounted && setLoading(false));
    return () => {
      mounted = false;
    };
  }, [periodId, periodsLoading, canRead]);

  const departments = useMemo(
    () => Array.from(new Set(records.map((r) => r.departmentName).filter((v): v is string => !!v))).sort((a, b) => a.localeCompare(b, "vi")),
    [records]
  );
  const partyCells = useMemo(
    () => Array.from(new Set(records.map((r) => r.partyCellName).filter((v): v is string => !!v))).sort((a, b) => a.localeCompare(b, "vi")),
    [records]
  );

  /** Lọc theo Phòng, Chi bộ, ô tìm (chưa lọc trạng thái) — dùng để đếm số lượng trên nút lọc trạng thái. */
  const baseFiltered = useMemo(() => {
    const term = search.trim().toLowerCase();
    return records.filter(
      (r) =>
        (!department || r.departmentName === department) &&
        (!partyCell || r.partyCellName === partyCell) &&
        (!term || [r.fullName, r.departmentName, r.partyCellName, r.statusDisplayName].some((v) => v?.toLowerCase().includes(term)))
    );
  }, [records, search, department, partyCell]);

  const statusChips = useMemo(() => {
    const map = new Map<string, { label: string; count: number }>();
    baseFiltered.forEach((r) => {
      const item = map.get(r.status) ?? { label: r.statusDisplayName, count: 0 };
      item.count += 1;
      map.set(r.status, item);
    });
    return Array.from(map.entries())
      .sort(([a], [b]) => statusRank(a) - statusRank(b))
      .map(([status, item]) => ({ status, ...item }));
  }, [baseFiltered]);
  const returnedCount = baseFiltered.filter((r) => !!r.returnReason).length;
  const overdueCount = baseFiltered.filter(isOverdue).length;

  const filtered = useMemo(() => {
    const list = baseFiltered.filter((r) =>
      !statusFilter ? true : statusFilter === RETURNED ? !!r.returnReason : statusFilter === OVERDUE ? isOverdue(r) : r.status === statusFilter
    );
    const direction = sort.desc ? -1 : 1;
    return [...list].sort((a, b) => {
      const byName = a.fullName.localeCompare(b.fullName, "vi");
      if (sort.key === "status") return (statusRank(a.status) - statusRank(b.status)) * direction || byName;
      return byName * direction;
    });
  }, [baseFiltered, statusFilter, sort]);

  const toggleSort = (key: SortKey) => setSort((s) => (s.key === key ? { key, desc: !s.desc } : { key, desc: false }));
  const sortIcon = (key: SortKey) =>
    sort.key === key ? <i className={`bi ${sort.desc ? "bi-sort-down" : "bi-sort-up"} ms-1`} aria-hidden="true" /> : null;
  const ariaSort = (key: SortKey) => (sort.key === key ? (sort.desc ? "descending" : "ascending") : undefined);

  const chip = (value: string, label: string, count: number, danger = false) => {
    const active = statusFilter === value;
    return (
      <button
        key={value || "all"}
        type="button"
        className={`btn btn-sm ${active ? (danger ? "btn-danger" : "btn-primary") : danger ? "btn-outline-danger" : "btn-outline-secondary"}`}
        aria-pressed={active}
        onClick={() => setStatusFilter(active ? "" : value)}
      >
        {label} <span className="num">{count}</span>
      </button>
    );
  };

  const open = (id: string) => router.push(`/evaluations/${id}`);

  return (
    <div className="page-wrapper">
      <PageHeader title="Hồ sơ đánh giá" />

      <div className="page-body">
        {error && <div className="alert alert-danger mb-0">{error}</div>}
        {!periodsLoading && periods.length === 0 && <EmptyState title="Chưa có kỳ đánh giá đang diễn ra" />}

        {myRecord && (
          <section className={styles.block}>
            <div className={styles.blockH}>
              <div className={styles.person}>
                <div className="initials">{initialsOf(myRecord.fullName)}</div>
                <div>
                  <Link href={`/evaluations/${myRecord.id}`} className={styles.name}>Hồ sơ của tôi</Link>
                  <span className={styles.sub}>{[myRecord.positionTitle, myRecord.partyCellName].filter(Boolean).join(" · ")}</span>
                  {myRecord.returnReason && <span className={styles.reason}>{myRecord.returnReason}</span>}
                </div>
              </div>
              <div className={styles.tools}>
                <RecordTag status={myRecord.status} label={myRecord.statusDisplayName} returned={!!myRecord.returnReason} overdue={isOverdue(myRecord)} />
                <Link href={`/evaluations/${myRecord.id}`} className="btn btn-primary btn-sm">Mở hồ sơ</Link>
              </div>
            </div>
          </section>
        )}

        {canRead && (
          <section className={styles.block}>
            <div className={styles.blockH}>
              <h2>
                Hồ sơ trong phạm vi <span className={styles.count}>{filtered.length}</span>
              </h2>
              <div className={styles.tools}>
                <select className="form-select form-select-sm" aria-label="Lọc theo Phòng" value={department} onChange={(e) => setDepartment(e.target.value)}>
                  <option value="">Tất cả Phòng</option>
                  {departments.map((d) => <option key={d} value={d}>{d}</option>)}
                </select>
                <select className="form-select form-select-sm" aria-label="Lọc theo Chi bộ" value={partyCell} onChange={(e) => setPartyCell(e.target.value)}>
                  <option value="">Tất cả Chi bộ</option>
                  {partyCells.map((c) => <option key={c} value={c}>{c}</option>)}
                </select>
                <input
                  type="search"
                  className="form-control form-control-sm"
                  placeholder="Tìm tên, Phòng, Chi bộ, trạng thái"
                  aria-label="Tìm hồ sơ"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                />
              </div>
            </div>
            {records.length > 0 && (
              <div className={styles.chips} role="group" aria-label="Lọc theo trạng thái">
                {chip("", "Tất cả", baseFiltered.length)}
                {statusChips.map((s) => chip(s.status, s.label, s.count))}
                {returnedCount > 0 && chip(RETURNED, "Bị trả lại", returnedCount, true)}
                {overdueCount > 0 && chip(OVERDUE, "Quá hạn", overdueCount, true)}
              </div>
            )}
            {loading ? (
              <div className={`${styles.empty} loading-delay`}><span className="spinner-border spinner-border-sm me-2" />Đang tải...</div>
            ) : filtered.length === 0 ? (
              <EmptyState title="Không có hồ sơ" />
            ) : (
              <div className={styles.tw}>
                <table className={`${styles.table} ${styles.wide}`}>
                  <thead>
                    <tr>
                      <th aria-sort={ariaSort("name")}>
                        <button type="button" className={styles.sortBtn} onClick={() => toggleSort("name")}>Cán bộ{sortIcon("name")}</button>
                      </th>
                      <th>Phòng / Chi bộ</th>
                      <th aria-sort={ariaSort("status")}>
                        <button type="button" className={styles.sortBtn} onClick={() => toggleSort("status")}>Trạng thái{sortIcon("status")}</button>
                      </th>
                      <th className={styles.right}>Tự chấm</th>
                      <th>Mức</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filtered.map((r) => (
                      <tr key={r.id} className={styles.rowLink} onClick={() => open(r.id)}>
                        <td>
                          <div className={styles.person}>
                            <div className="initials">{initialsOf(r.fullName)}</div>
                            <div>
                              <Link href={`/evaluations/${r.id}`} className={styles.name} onClick={(e) => e.stopPropagation()}>{r.fullName}</Link>
                              {r.positionTitle && <span className={styles.sub}>{r.positionTitle}</span>}
                            </div>
                          </div>
                        </td>
                        <td>
                          {r.departmentName || "—"}
                          {r.partyCellName && <span className={styles.sub}>{r.partyCellName}</span>}
                        </td>
                        <td><RecordTag status={r.status} label={r.statusDisplayName} returned={!!r.returnReason} overdue={isOverdue(r)} /></td>
                        <td className={`${styles.right} num`}>{r.selfScoredAt ? r.totalSelfScore : "—"}</td>
                        <td><GradeBadge grade={displayGrade(r)} /></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        )}

        {canRead && quotas.length > 0 && (
          <section className={styles.block}>
            <div className={styles.blockH}><h2>Trần Hoàn thành xuất sắc theo Chi bộ (Mẫu 15)</h2></div>
            <div className={styles.tw}>
              <table className={`${styles.table} ${styles.mid}`}>
                <thead>
                  <tr>
                    <th>Chi bộ</th>
                    <th className={styles.right}>Số hồ sơ</th>
                    <th className={styles.right}>HT tốt trở lên</th>
                    <th className={styles.right}>Tối đa XS</th>
                    <th className={styles.right}>Đề xuất XS</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {quotas.map((q) => (
                    <tr key={q.branchId}>
                      <td>{q.branchName}</td>
                      <td className={`${styles.right} num`}>{q.totalCadres}</td>
                      <td className={`${styles.right} num`}>{q.goodOrBetterCount}</td>
                      <td className={`${styles.right} num`}>{q.maxExcellentAllowed}</td>
                      <td className={`${styles.right} num`}>{q.proposedExcellentCount}</td>
                      <td className={styles.right}>
                        {q.isExceedingQuota ? <span className="tag t-bad">Vượt trần</span> : <span className="tag t-ok">Đạt</span>}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        )}
      </div>
    </div>
  );
}
