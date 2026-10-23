"use client";

import React, { useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { Pagination } from "@/components/common/Pagination";
import { usePagination } from "@/components/common/usePagination";
import { GradeBadge } from "@/components/common/StatusBadge";
import { RecordTag } from "@/components/dashboard/RecordTag";
import { MyRecordSection } from "@/components/dashboard/MyRecordSection";
import { initialsOf, latestGrade } from "@/components/dashboard/taskUtils";
import styles from "@/components/dashboard/dashboard.module.css";
import { useAuth } from "@/contexts/AuthContext";
import { usePeriod } from "@/contexts/PeriodContext";
import { useToast } from "@/contexts/ToastContext";
import { fmt } from "@/services/criteriaService";
import { EvaluationRecordDto, ExcellentQuotaCheckDto, evaluationService } from "@/services/evaluationService";
import { errorMessage } from "@/components/admin/adminUtils";

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

type SortKey = "name" | "status";
const RETURNED = "__returned";
const OVERDUE = "__overdue";

/** Hồ sơ của kỳ đang chọn: của tôi và trong phạm vi được xem (máy chủ lọc). */
export default function EvaluationsPage() {
  const { hasPermission } = useAuth();
  const router = useRouter();
  const canRead = hasPermission("evaluation.read");
  const { periods, periodId, loading: periodsLoading } = usePeriod();
  const [myRecord, setMyRecord] = useState<EvaluationRecordDto | null>(null);
  const [records, setRecords] = useState<EvaluationRecordDto[]>([]);
  const [quotas, setQuotas] = useState<ExcellentQuotaCheckDto | null>(null);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [department, setDepartment] = useState("");
  const [partyCell, setPartyCell] = useState("");
  const [sort, setSort] = useState<{ key: SortKey; desc: boolean }>({ key: "name", desc: false });
  const [loading, setLoading] = useState(true);
  const { toast } = useToast();
  // Bộ lọc mở sẵn từ đường dẫn (vd. bấm tổ chức Đảng ở Tổng quan); chỉ áp dụng lần nạp đầu.
  const initialFilter = useRef<{ status: string; partyCell: string } | null>(null);
  if (initialFilter.current === null && typeof window !== "undefined") {
    const q = new URLSearchParams(window.location.search);
    initialFilter.current = { status: q.get("status") ?? "", partyCell: q.get("partyCell") ?? "" };
  }

  useEffect(() => {
    if (!periodId) {
      setMyRecord(null);
      setRecords([]);
      setQuotas(null);
      setLoading(periodsLoading);
      return;
    }
    let mounted = true;
    setLoading(true);
    const initial = initialFilter.current;
    initialFilter.current = { status: "", partyCell: "" };
    setStatusFilter(initial?.status ?? "");
    setDepartment("");
    setPartyCell(initial?.partyCell ?? "");
    Promise.all([
      evaluationService.getMyRecord(periodId).catch(() => null),
      canRead ? evaluationService.getRecordsByPeriod(periodId) : Promise.resolve([] as EvaluationRecordDto[]),
      canRead ? evaluationService.checkExcellentQuotas(periodId).catch(() => null) : Promise.resolve(null),
    ])
      .then(([mine, list, quota]) => {
        if (!mounted) return;
        setMyRecord(mine);
        setRecords(list);
        setQuotas(quota);
      })
      .catch((err: unknown) => mounted && toast.error(errorMessage(err, "Tải danh sách hồ sơ không thành công.")))
      .finally(() => mounted && setLoading(false));
    return () => {
      mounted = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
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
  const paging = usePagination(filtered, {
    storageKey: "evaluations",
    resetKeys: [periodId, search, statusFilter, department, partyCell, sort],
  });

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
        {!periodsLoading && periods.length === 0 && <EmptyState
            icon="bi-calendar-x"
            title="Chưa có kỳ đánh giá đang diễn ra"
            description="Hồ sơ đánh giá hiện ở đây khi kỳ đánh giá được mở."
            action={hasPermission("period.manage") ? <Link href="/periods" className="btn btn-primary">Đến trang Kỳ đánh giá</Link> : undefined}
          />}

        {myRecord && <MyRecordSection record={myRecord} />}

        {canRead && (
          <section className={styles.block}>
            <div className={`${styles.blockH} ${styles.filters}`}>
              <input
                type="search"
                className={`form-control form-control-sm ${styles.filterSearch}`}
                placeholder="Tìm tên, Phòng, Chi bộ, trạng thái"
                aria-label="Tìm hồ sơ"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
              <select className="form-select form-select-sm" aria-label="Lọc theo Phòng" value={department} onChange={(e) => setDepartment(e.target.value)}>
                <option value="">Tất cả Phòng</option>
                {departments.map((d) => <option key={d} value={d}>{d}</option>)}
              </select>
              <select className="form-select form-select-sm" aria-label="Lọc theo Chi bộ" value={partyCell} onChange={(e) => setPartyCell(e.target.value)}>
                <option value="">Tất cả Chi bộ</option>
                {partyCell && !partyCells.includes(partyCell) && <option value={partyCell}>{partyCell}</option>}
                {partyCells.map((c) => <option key={c} value={c}>{c}</option>)}
              </select>
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
              records.length > 0 ? (
                <EmptyState
                  icon="bi-funnel"
                  title="Không có hồ sơ phù hợp"
                  description="Thử đổi bộ lọc hoặc từ khóa tìm."
                  compact
                  action={
                    <button
                      type="button"
                      className="btn btn-outline-secondary btn-sm"
                      onClick={() => { setSearch(""); setStatusFilter(""); setDepartment(""); setPartyCell(""); }}
                    >
                      Bỏ lọc
                    </button>
                  }
                />
              ) : (
                <EmptyState
                  icon="bi-folder2-open"
                  title="Chưa có hồ sơ"
                  description="Hồ sơ trong phạm vi của bạn sẽ hiện ở đây khi kỳ có người được đánh giá."
                  compact
                />
              )
            ) : (
              <>
              <div className={styles.tw}>
                <table className={`${styles.table} ${styles.wide} ${styles.recordCards}`}>
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
                      <th title="Mức quyết định; chưa có thì mức đề xuất gần nhất">Mức</th>
                    </tr>
                  </thead>
                  <tbody>
                    {paging.pageItems.map((r) => (
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
                        <td className={`${styles.right} num`}>
                          {r.selfScoredAt ? <><span className={styles.mobileOnly}>Tự chấm </span>{fmt(r.totalSelfScore)}</> : <span className={styles.deskOnly}>—</span>}
                        </td>
                        <td><GradeCell record={r} /></td>
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
        )}

        {canRead && quotas && quotas.groups.length > 0 && (
          <details className={`${styles.block} ${styles.fold}`}>
            <summary className={styles.blockH}>
              <h2>Trần Hoàn thành xuất sắc theo nhóm chức danh</h2>
              {quotas.groups.some((q) => q.isExceedingQuota) && (
                <span className="tag t-bad">Vượt trần {quotas.groups.filter((q) => q.isExceedingQuota).length} nhóm</span>
              )}
              <i className={`bi bi-chevron-down ${styles.foldIcon}`} aria-hidden="true" />
            </summary>
            <p className={`small text-secondary mb-0 ${styles.foldNote}`}>
              Tối đa {Math.round(quotas.ratio * 100)}% số &quot;Hoàn thành tốt&quot; của nhóm, làm tròn từ 0,5 trở lên thành 1; không tính người đứng đầu.
              {!quotas.isFullScope && " Chỉ gồm hồ sơ trong phạm vi bạn được xem."}
            </p>
            <div className={styles.tw}>
              <table className={`${styles.table} ${styles.mid}`}>
                <thead>
                  <tr>
                    <th>Cấp quyết định</th>
                    <th>Nhóm chức danh</th>
                    <th className={styles.right}>Số hồ sơ</th>
                    <th className={styles.right}>Người đứng đầu</th>
                    <th className={styles.right}>HT tốt</th>
                    <th className={styles.right}>Tối đa XS</th>
                    <th className={styles.right}>XS</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {quotas.groups.map((q) => (
                    <tr key={`${q.approvalAuthority}-${q.statCode ?? "none"}`}>
                      <td>{q.authorityName}</td>
                      <td>{q.statCode ? `${q.statCode} — ${q.groupName}` : q.groupName}</td>
                      <td className={`${styles.right} num`}>{q.totalCadres}</td>
                      <td className={`${styles.right} num`}>{q.headCount}</td>
                      <td className={`${styles.right} num`}>{q.goodCount}</td>
                      <td className={`${styles.right} num`}>{q.maxExcellentAllowed}</td>
                      <td className={`${styles.right} num`}>{q.excellentCount}</td>
                      <td className={styles.right}>
                        {q.isExceedingQuota ? <span className="tag t-bad">Vượt trần {q.excessCount}</span> : <span className="tag t-ok">Đạt</span>}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </details>
        )}
      </div>
    </div>
  );
}

/** Mức quyết định, chưa có thì mức đề xuất gần nhất (ghi "đề xuất"). */
function GradeCell({ record }: { record: EvaluationRecordDto }) {
  const { grade, final } = latestGrade(record);
  if (!grade) return <span className={`text-secondary ${styles.deskOnly}`}>—</span>;
  return (
    <span className={styles.nowrap}>
      <GradeBadge grade={grade} />
      {!final && <span className={styles.hint}>đề xuất</span>}
    </span>
  );
}
