"use client";

import React, { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { GradeBadge, RecordStatusBadge } from "@/components/common/StatusBadge";
import { useAuth } from "@/contexts/AuthContext";
import {
  BranchQuotaCheckDto,
  EvaluationPeriodDto,
  EvaluationRecordDto,
  evaluationService,
} from "@/services/evaluationService";

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
 * Danh sách hồ sơ đánh giá: hồ sơ của tôi (nếu có trong danh sách được đánh giá của kỳ) và các hồ sơ trong phạm vi
 * được xem (máy chủ lọc theo phạm vi). Thao tác trên hồ sơ thực hiện ở trang hồ sơ theo `actions`.
 */
export default function EvaluationsPage() {
  const { hasPermission } = useAuth();
  const router = useRouter();
  const canRead = hasPermission("evaluation.read");
  const [periods, setPeriods] = useState<EvaluationPeriodDto[]>([]);
  const [periodId, setPeriodId] = useState("");
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
    evaluationService
      .getPeriods()
      .then((list) => {
        const visible = list.filter((p) => p.status !== "Draft");
        setPeriods(visible);
        const active = visible.find((p) => p.isActive) || visible[0];
        if (active) setPeriodId(active.id);
        else setLoading(false);
      })
      .catch((err: any) => {
        setError(err?.message || "Không tải được danh sách kỳ đánh giá.");
        setLoading(false);
      });
  }, []);

  useEffect(() => {
    if (!periodId) return;
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
        setMyRecord(mine);
        setRecords(list);
        setQuotas(quota.filter((q) => q.totalCadres > 0));
      })
      .catch((err: any) => setError(err?.message || "Không tải được hồ sơ đánh giá."))
      .finally(() => setLoading(false));
  }, [periodId, canRead]);

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
        className={`btn btn-sm rounded-pill ${active ? (danger ? "btn-danger" : "btn-primary") : danger ? "btn-outline-danger" : "btn-outline-secondary"}`}
        aria-pressed={active}
        onClick={() => setStatusFilter(active ? "" : value)}
      >
        {label} <span className="fw-semibold">({count})</span>
      </button>
    );
  };

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Hồ sơ đánh giá"
        actions={
          <div className="d-flex flex-wrap gap-2">
            <select className="form-select form-select-sm w-auto" aria-label="Kỳ đánh giá" value={periodId} onChange={(e) => setPeriodId(e.target.value)}>
              {periods.map((p) => (
                <option key={p.id} value={p.id}>{p.name} ({p.statusDisplayName})</option>
              ))}
            </select>
            <Link href="/work-queue" className="btn btn-primary btn-sm text-nowrap"><i className="bi bi-list-check me-1" />Việc cần xử lý</Link>
          </div>
        }
      />

      <div className="page-body d-flex flex-column gap-3">
        {error && <div className="alert alert-danger mb-0">{error}</div>}
        {!loading && periods.length === 0 && <EmptyState title="Chưa có kỳ đánh giá đang diễn ra" />}

        {myRecord && (
          <section className="card" style={{ boxShadow: "inset 4px 0 0 #3b82f6" }}>
            <div className="card-body d-flex flex-wrap justify-content-between align-items-center gap-3">
              <div className="d-flex flex-column gap-1">
                <div className="fw-semibold" style={{ fontSize: 16 }}>
                  <i className="bi bi-person-badge me-2 text-primary" />Hồ sơ của tôi
                </div>
                <div>
                  <RecordStatusBadge status={myRecord.status} label={myRecord.statusDisplayName} returned={!!myRecord.returnReason} overdue={isOverdue(myRecord)} />
                </div>
                {myRecord.returnReason && <div className="text-danger" style={{ fontSize: 14 }}>Lý do trả lại: {myRecord.returnReason}</div>}
              </div>
              <Link href={`/evaluations/${myRecord.id}`} className="btn btn-primary">
                <i className="bi bi-box-arrow-in-right me-1" />Mở hồ sơ của tôi
              </Link>
            </div>
          </section>
        )}

        {canRead && (
          <section className="card border-0 shadow-sm">
            <div className="card-header bg-white d-flex flex-column gap-2">
              <div className="d-flex flex-wrap justify-content-between align-items-center gap-2">
                <h2 className="h6 mb-0">Hồ sơ trong phạm vi ({filtered.length})</h2>
                <div className="d-flex flex-wrap gap-2">
                  <select className="form-select form-select-sm w-auto" aria-label="Lọc theo Phòng" value={department} onChange={(e) => setDepartment(e.target.value)}>
                    <option value="">Tất cả Phòng</option>
                    {departments.map((d) => <option key={d} value={d}>{d}</option>)}
                  </select>
                  <select className="form-select form-select-sm w-auto" aria-label="Lọc theo Chi bộ" value={partyCell} onChange={(e) => setPartyCell(e.target.value)}>
                    <option value="">Tất cả Chi bộ</option>
                    {partyCells.map((c) => <option key={c} value={c}>{c}</option>)}
                  </select>
                  <input className="form-control form-control-sm" style={{ maxWidth: 260 }} placeholder="Tìm theo tên, Phòng, Chi bộ, trạng thái" value={search} onChange={(e) => setSearch(e.target.value)} />
                </div>
              </div>
              {records.length > 0 && (
                <div className="d-flex flex-wrap gap-2" role="group" aria-label="Lọc theo trạng thái">
                  {chip("", "Tất cả", baseFiltered.length)}
                  {statusChips.map((s) => chip(s.status, s.label, s.count))}
                  {returnedCount > 0 && chip(RETURNED, "Bị trả lại", returnedCount, true)}
                  {overdueCount > 0 && chip(OVERDUE, "Quá hạn", overdueCount, true)}
                </div>
              )}
            </div>
            <div className="card-body p-0">
              {loading ? (
                <div className="text-secondary p-3"><span className="spinner-border spinner-border-sm me-2" />Đang tải...</div>
              ) : filtered.length === 0 ? (
                <EmptyState title="Không có hồ sơ" />
              ) : (
                <div className="table-responsive">
                  <table className="table table-hover align-middle mb-0" style={{ fontSize: 14 }}>
                    <thead>
                      <tr className="text-secondary" style={{ fontSize: 13 }}>
                        <th className="ps-3" aria-sort={ariaSort("name")}>
                          <button type="button" className="btn btn-link btn-sm p-0 text-reset text-decoration-none fw-semibold" onClick={() => toggleSort("name")}>
                            Cán bộ{sortIcon("name")}
                          </button>
                        </th>
                        <th>Phòng</th>
                        <th>Chi bộ</th>
                        <th aria-sort={ariaSort("status")}>
                          <button type="button" className="btn btn-link btn-sm p-0 text-reset text-decoration-none fw-semibold" onClick={() => toggleSort("status")}>
                            Trạng thái{sortIcon("status")}
                          </button>
                        </th>
                        <th>Tự chấm</th>
                        <th>Mức</th>
                        <th />
                      </tr>
                    </thead>
                    <tbody>
                      {filtered.map((r) => (
                        <tr key={r.id} role="link" style={{ cursor: "pointer" }} onClick={() => router.push(`/evaluations/${r.id}`)}>
                          <td className="ps-3 fw-semibold">{r.fullName}</td>
                          <td>{r.departmentName || "—"}</td>
                          <td>{r.partyCellName || "—"}</td>
                          <td><RecordStatusBadge status={r.status} label={r.statusDisplayName} returned={!!r.returnReason} overdue={isOverdue(r)} /></td>
                          <td>{r.totalSelfScore || "—"}</td>
                          <td><GradeBadge grade={displayGrade(r)} /></td>
                          <td className="text-end pe-3">
                            <Link href={`/evaluations/${r.id}`} className="btn btn-outline-primary btn-sm" onClick={(e) => e.stopPropagation()}>Xem</Link>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </section>
        )}

        {canRead && quotas.length > 0 && (
          <section className="card border-0 shadow-sm">
            <div className="card-header bg-white"><h2 className="h6 mb-0">Kiểm soát trần Hoàn thành xuất sắc theo Chi bộ (Mẫu 15)</h2></div>
            <div className="card-body p-0">
              <div className="table-responsive"><table className="table table-sm mb-0" style={{ fontSize: 14 }}>
                <thead><tr className="text-secondary" style={{ fontSize: 13 }}><th className="ps-3">Chi bộ</th><th>Số hồ sơ</th><th>HT tốt trở lên</th><th>Tối đa XS</th><th>Đề xuất XS</th><th /></tr></thead>
                <tbody>
                  {quotas.map((q) => (
                    <tr key={q.branchId}>
                      <td className="ps-3">{q.branchName}</td>
                      <td>{q.totalCadres}</td>
                      <td>{q.goodOrBetterCount}</td>
                      <td>{q.maxExcellentAllowed}</td>
                      <td>{q.proposedExcellentCount}</td>
                      <td>{q.isExceedingQuota ? <span className="badge text-bg-danger">Vượt trần</span> : <span className="badge text-bg-success">Đạt</span>}</td>
                    </tr>
                  ))}
                </tbody>
              </table></div>
            </div>
          </section>
        )}
      </div>
    </div>
  );
}
