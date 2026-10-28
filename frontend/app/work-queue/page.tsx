"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { usePeriod } from "@/contexts/PeriodContext";
import { useToast } from "@/contexts/ToastContext";
import { WorkQueueDto, evaluationService } from "@/services/evaluationService";
import { commonDeadline, normalizeText, queueGroups } from "@/components/dashboard/taskUtils";
import { GroupDeadline, TaskTable } from "@/components/dashboard/TaskTable";
import styles from "@/components/dashboard/dashboard.module.css";
import { errorMessage } from "@/components/admin/adminUtils";

/** Hiện ô tìm khi danh sách dài hơn số này. */
const SEARCH_THRESHOLD = 10;

/** Gồm mọi kỳ đang mở (không lọc theo kỳ đang chọn); việc thuộc kỳ khác ghi thêm tên kỳ. */
export default function WorkQueuePage() {
  const { periodId } = usePeriod();
  const [queue, setQueue] = useState<WorkQueueDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [keyword, setKeyword] = useState("");
  const { toast } = useToast();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setQueue(await evaluationService.getWorkQueue());
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Tải danh sách việc không thành công."));
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const showSearch = (queue?.total ?? 0) > SEARCH_THRESHOLD;

  const groups = useMemo(() => {
    const term = showSearch ? normalizeText(keyword) : "";
    return queueGroups(queue)
      .map((g) => (term ? { ...g, items: g.items.filter((item) => normalizeText(item.fullName).includes(term)) } : g))
      .filter((g) => g.items.length > 0);
  }, [queue, keyword, showSearch]);

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Việc cần xử lý"
        actions={
          showSearch ? (
            <input
              type="search"
              className={`form-control form-control-sm ${styles.search}`}
              placeholder="Tìm theo tên cán bộ"
              aria-label="Tìm theo tên cán bộ"
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
            />
          ) : undefined
        }
      />

      <div className="page-body">
        {loading ? (
          <div className="text-secondary loading-delay">
            <span className="spinner-border spinner-border-sm me-2" />
            Đang tải...
          </div>
        ) : groups.length === 0 && !keyword ? (
          <EmptyState card
            icon="bi-check2-circle"
            title="Không có việc nào đang chờ bạn"
            description="Khi có hồ sơ cần bạn xử lý, chúng sẽ hiện ở đây."
          />
        ) : groups.length === 0 ? (
          <EmptyState card icon="bi-search" title="Không tìm thấy cán bộ phù hợp" description="Thử từ khóa khác." compact />
        ) : (
          groups.map((group, index) => {
            // Cả nhóm cùng một hạn thì ghi hạn một lần ở tiêu đề.
            const common = commonDeadline(group.items);
            return (
              <section key={group.step} id={group.step} className={styles.block}>
                <div className={styles.blockH}>
                  <h2>
                    {group.stepName} <span className={styles.count}>{group.items.length}</span>
                  </h2>
                  {common && <GroupDeadline deadline={common.deadline} overdue={common.overdue} />}
                  {index === 0 && (
                    <button type="button" className="btn btn-outline-secondary btn-sm ms-auto" onClick={() => load()} aria-label="Làm mới" title="Làm mới">
                      <i className="bi bi-arrow-clockwise" />
                    </button>
                  )}
                </div>
                <TaskTable tasks={group.items} periodId={periodId} pageKey="workQueue" resetKeys={[keyword]} showDeadline={!common} />
              </section>
            );
          })
        )}
      </div>
    </div>
  );
}
