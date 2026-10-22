"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { usePeriod } from "@/contexts/PeriodContext";
import { WorkQueueDto, evaluationService } from "@/services/evaluationService";
import { normalizeText } from "@/components/dashboard/taskUtils";
import { TaskTable } from "@/components/dashboard/TaskTable";
import styles from "@/components/dashboard/dashboard.module.css";
import { errorMessage } from "@/components/admin/adminUtils";

/** Hiện ô tìm khi danh sách dài hơn số này. */
const SEARCH_THRESHOLD = 10;

/**
 * Việc cần xử lý: hồ sơ đang chờ chính người dùng ở bước họ có quyền, trong phạm vi được gán (máy chủ lọc).
 * Gồm mọi kỳ đang mở (không lọc theo kỳ đang chọn); việc thuộc kỳ khác ghi thêm tên kỳ.
 */
export default function WorkQueuePage() {
  const { periodId } = usePeriod();
  const [queue, setQueue] = useState<WorkQueueDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [keyword, setKeyword] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setQueue(await evaluationService.getWorkQueue());
    } catch (err: unknown) {
      setError(errorMessage(err, "Không tải được danh sách việc cần xử lý."));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const showSearch = (queue?.total ?? 0) > SEARCH_THRESHOLD;

  const groups = useMemo(() => {
    if (!queue) return [];
    const term = showSearch ? normalizeText(keyword) : "";
    return queue.groups
      .map((g) => ({
        ...g,
        items: (term ? g.items.filter((item) => normalizeText(item.fullName).includes(term)) : g.items).map((item) => ({
          ...item,
          step: g.step,
        })),
      }))
      .filter((g) => g.items.length > 0);
  }, [queue, keyword, showSearch]);

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Việc cần xử lý"
        badge={queue ? <span className={`${styles.count} fs-5`}>{queue.total}</span> : undefined}
        actions={
          <>
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
            <button type="button" className="btn btn-outline-secondary btn-sm" onClick={() => load()} aria-label="Tải lại" title="Tải lại">
              <i className="bi bi-arrow-clockwise" />
            </button>
          </>
        }
      />

      <div className="page-body">
        {error && <div className="alert alert-danger mb-0">{error}</div>}
        {loading ? (
          <div className="text-secondary loading-delay">
            <span className="spinner-border spinner-border-sm me-2" />
            Đang tải...
          </div>
        ) : !queue || queue.total === 0 ? (
          <EmptyState icon="bi-check2-circle" title="Không có việc nào đang chờ bạn" />
        ) : groups.length === 0 ? (
          <div className={styles.muted}>Không tìm thấy cán bộ phù hợp.</div>
        ) : (
          groups.map((group) => (
            <section key={group.step} className={styles.block}>
              <div className={styles.blockH}>
                <h2>
                  {group.stepName} <span className={styles.count}>{group.items.length}</span>
                </h2>
              </div>
              <TaskTable tasks={group.items} periodId={periodId} pageKey="workQueue" resetKeys={[keyword]} />
            </section>
          ))
        )}
      </div>
    </div>
  );
}
