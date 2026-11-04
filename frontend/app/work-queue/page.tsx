"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { usePeriod } from "@/contexts/PeriodContext";
import { useToast } from "@/contexts/ToastContext";
import { PublishManyItemResultDto, WorkQueueDto, evaluationService } from "@/services/evaluationService";
import { commonDeadline, normalizeText, queueGroups } from "@/components/dashboard/taskUtils";
import { GroupDeadline, TaskTable } from "@/components/dashboard/TaskTable";
import styles from "@/components/dashboard/dashboard.module.css";
import { errorMessage } from "@/components/admin/adminUtils";

/** Nhóm công bố cho chọn nhiều hồ sơ để công bố một lần. */
const PUBLISH_STEP = "B5_PUBLISH";

/** Hiện ô tìm khi danh sách dài hơn số này. */
const SEARCH_THRESHOLD = 10;

/** Gồm mọi kỳ đang mở (không lọc theo kỳ đang chọn); việc thuộc kỳ khác ghi thêm tên kỳ. */
export default function WorkQueuePage() {
  const { periodId } = usePeriod();
  const [queue, setQueue] = useState<WorkQueueDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [keyword, setKeyword] = useState("");
  const { toast, confirm } = useToast();
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [publishing, setPublishing] = useState(false);
  const [failed, setFailed] = useState<PublishManyItemResultDto[]>([]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await evaluationService.getWorkQueue();
      setQueue(data);
      // Bỏ chọn hồ sơ không còn chờ công bố.
      const waiting = new Set(data.groups.filter((g) => g.step === PUBLISH_STEP).flatMap((g) => g.items.map((i) => i.recordId)));
      setSelected((prev) => new Set(Array.from(prev).filter((id) => waiting.has(id))));
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

  const toggle = (recordId: string) =>
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(recordId)) next.delete(recordId);
      else next.add(recordId);
      return next;
    });

  const publishSelected = (items: { recordId: string; version: number }[]) =>
    confirm({
      title: "Công bố kết quả",
      message: `Công bố ${items.length} hồ sơ đã chọn?`,
      confirmText: "Công bố",
      isDanger: false,
      onConfirm: async () => {
        setPublishing(true);
        try {
          const result = await evaluationService.publishMany(items);
          setFailed(result.items.filter((i) => !i.succeeded));
          if (result.publishedCount > 0) toast.success(`Đã công bố ${result.publishedCount} hồ sơ.`);
          if (result.failedCount > 0) toast.error(`${result.failedCount} hồ sơ công bố không thành công.`);
          await load();
        } catch (err: unknown) {
          toast.error(errorMessage(err, "Công bố không thành công."));
        } finally {
          setPublishing(false);
        }
      },
    });

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
            const bulk = group.step === PUBLISH_STEP;
            const picked = bulk ? group.items.filter((i) => selected.has(i.recordId)) : [];
            const allPicked = bulk && picked.length === group.items.length;
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
                {bulk && (
                  <div className={styles.bulkBar}>
                    <label className="d-flex align-items-center gap-2 mb-0">
                      <input
                        type="checkbox"
                        className={`form-check-input ${styles.pick}`}
                        checked={allPicked}
                        onChange={() => setSelected(allPicked ? new Set() : new Set(group.items.map((i) => i.recordId)))}
                      />
                      Chọn tất cả
                    </label>
                    <button
                      type="button"
                      className="btn btn-primary btn-sm ms-auto"
                      disabled={picked.length === 0 || publishing}
                      onClick={() => publishSelected(picked.map((i) => ({ recordId: i.recordId, version: i.version })))}
                    >
                      {publishing && <span className="spinner-border spinner-border-sm me-1" />}
                      Công bố ({picked.length})
                    </button>
                  </div>
                )}
                {bulk && failed.length > 0 && (
                  <div className={`${styles.bulkFailed} text-danger`} role="alert">
                    {failed.length} hồ sơ công bố không thành công:
                    <ul>
                      {failed.map((f) => (
                        <li key={f.recordId}>
                          {f.fullName || "Hồ sơ"}: {f.message}
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
                <TaskTable
                  tasks={group.items}
                  periodId={periodId}
                  pageKey="workQueue"
                  resetKeys={[keyword]}
                  showDeadline={!common}
                  selection={bulk ? { selected, onToggle: toggle } : undefined}
                />
              </section>
            );
          })
        )}
      </div>
    </div>
  );
}
