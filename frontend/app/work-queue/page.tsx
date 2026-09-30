"use client";

import { formatDate } from "@/lib/dateFormat";
import React, { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { RecordStatusBadge } from "@/components/common/StatusBadge";
import { EvaluationPeriodDto, WorkQueueDto, evaluationService } from "@/services/evaluationService";
import { deadlineState, normalizeText } from "@/components/dashboard/taskUtils";
import styles from "@/components/dashboard/dashboard.module.css";

/** Hiện ô tìm khi danh sách dài hơn số này. */
const SEARCH_THRESHOLD = 10;

/**
 * Việc cần xử lý: hồ sơ đang chờ chính người dùng ở bước họ có quyền, trong phạm vi được gán (máy chủ lọc).
 * Mặc định gồm mọi kỳ đang mở/khóa dữ liệu; chọn kỳ để lọc.
 */
export default function WorkQueuePage() {
  const [periods, setPeriods] = useState<EvaluationPeriodDto[]>([]);
  const [periodId, setPeriodId] = useState("");
  const [queue, setQueue] = useState<WorkQueueDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [keyword, setKeyword] = useState("");

  useEffect(() => {
    evaluationService.getPeriods().then(setPeriods).catch(() => setPeriods([]));
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setQueue(await evaluationService.getWorkQueue(periodId || undefined));
    } catch (err: any) {
      setError(err?.message || "Không tải được danh sách việc cần xử lý.");
    } finally {
      setLoading(false);
    }
  }, [periodId]);

  useEffect(() => {
    load();
  }, [load]);

  const selectablePeriods = periods.filter((p) => p.status !== "Draft");
  const showSearch = (queue?.total ?? 0) > SEARCH_THRESHOLD;
  const showPeriod = !periodId;

  const groups = useMemo(() => {
    if (!queue) return [];
    const term = showSearch ? normalizeText(keyword) : "";
    return queue.groups
      .map((g) => ({
        ...g,
        items: term ? g.items.filter((item) => normalizeText(item.fullName).includes(term)) : g.items,
      }))
      .filter((g) => g.items.length > 0);
  }, [queue, keyword, showSearch]);

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Việc cần xử lý"
        badge={queue ? <span className="badge text-bg-primary">{queue.total}</span> : undefined}
        actions={
          <div className="d-flex flex-wrap gap-2">
            {showSearch && (
              <input
                type="search"
                className={`form-control form-control-sm ${styles.search}`}
                placeholder="Tìm theo tên cán bộ"
                aria-label="Tìm theo tên cán bộ"
                value={keyword}
                onChange={(e) => setKeyword(e.target.value)}
              />
            )}
            <select className="form-select form-select-sm w-auto" aria-label="Kỳ đánh giá" value={periodId} onChange={(e) => setPeriodId(e.target.value)}>
              <option value="">Tất cả kỳ đang mở</option>
              {selectablePeriods.map((p) => (
                <option key={p.id} value={p.id}>{p.name} ({p.statusDisplayName})</option>
              ))}
            </select>
            <button type="button" className="btn btn-outline-secondary btn-sm" onClick={() => load()} aria-label="Tải lại">
              <i className="bi bi-arrow-clockwise" />
            </button>
          </div>
        }
      />

      <div className="page-body d-flex flex-column gap-3">
        {error && <div className="alert alert-danger mb-0">{error}</div>}
        {loading ? (
          <div className="text-secondary"><span className="spinner-border spinner-border-sm me-2" />Đang tải...</div>
        ) : !queue || queue.total === 0 ? (
          <EmptyState icon="bi-check2-circle" title="Không có việc nào đang chờ bạn" />
        ) : groups.length === 0 ? (
          <div className={styles.muted}>Không tìm thấy cán bộ phù hợp.</div>
        ) : (
          groups.map((group) => (
            <section key={group.step} className={styles.section}>
              <div className={styles.sectionHeader}>
                <h2 className={styles.sectionTitle}>
                  {group.stepName}
                  <span className={styles.groupCount}>{group.items.length}</span>
                </h2>
              </div>
              <div className="table-responsive">
                <table className={`table table-hover align-middle mb-0 ${styles.table}`}>
                  <thead>
                    <tr className={styles.muted}>
                      <th className="ps-3">Cán bộ</th>
                      <th>Trạng thái</th>
                      <th>Hạn</th>
                      <th />
                    </tr>
                  </thead>
                  <tbody>
                    {group.items.map((item) => {
                      const state = deadlineState(item.deadline, item.overdue);
                      return (
                        <tr key={item.recordId} className={state === "overdue" ? "table-danger" : undefined}>
                          <td className="ps-3">
                            <div className="fw-semibold">
                              {item.fullName}
                              {item.isOwnRecord && <span className="badge text-bg-info ms-2">Hồ sơ của tôi</span>}
                            </div>
                            <div className={styles.muted}>
                              {[item.departmentName, item.partyCellName, showPeriod ? item.periodName : null].filter(Boolean).join(" · ") || "—"}
                            </div>
                            {item.returnReason && <div className={styles.reason}>Bị trả lại: {item.returnReason}</div>}
                          </td>
                          <td>
                            <RecordStatusBadge status={item.status} label={item.statusDisplayName} returned={!!item.returnReason} />
                          </td>
                          <td
                            className={
                              state === "overdue" ? "text-danger fw-semibold" : state === "soon" ? "text-warning-emphasis fw-semibold" : undefined
                            }
                          >
                            {formatDate(item.deadline)}
                            {state === "overdue" && <div>Quá hạn</div>}
                            {state === "soon" && <div>Sắp hạn</div>}
                          </td>
                          <td className="text-end pe-3">
                            <Link href={`/evaluations/${item.recordId}`} className="btn btn-primary">
                              {item.mode === "External" ? "Ghi nhận" : "Xử lý"}
                            </Link>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </section>
          ))
        )}
      </div>
    </div>
  );
}
