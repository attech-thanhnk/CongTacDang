"use client";

import React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import type { WorkQueueItemDto, WorkflowStepCode } from "@/services/evaluationService";
import { Pagination } from "@/components/common/Pagination";
import { usePagination } from "@/components/common/usePagination";
import styles from "./dashboard.module.css";
import { deadlineState, initialsOf, shortDate, taskActionLabel } from "./taskUtils";

export interface TaskRow extends WorkQueueItemDto {
  step: WorkflowStepCode;
}

/** Hạn của việc: quá hạn đỏ, sắp hạn vàng. */
function Deadline({ task }: { task: TaskRow }) {
  const state = deadlineState(task.deadline, task.overdue);
  const date = shortDate(task.deadline);
  const cls = state === "overdue" ? styles.late : state === "soon" ? styles.soon : styles.muted;
  return (
    <span className={cls}>
      {date || "—"}
      {state === "overdue" && " · quá hạn"}
    </span>
  );
}

/** Bảng việc cần xử lý; việc thuộc kỳ khác kỳ đang chọn hiện thêm tên kỳ. */
export function TaskTable({
  tasks,
  periodId,
  pageKey,
  resetKeys,
  showDeadline = true,
}: {
  tasks: TaskRow[];
  periodId?: string;
  /** Có thì phân trang (khóa ghi nhớ số dòng/trang); bỏ trống thì hiện mọi dòng. */
  pageKey?: string;
  resetKeys?: readonly unknown[];
  /** Tắt khi mọi việc cùng một hạn và hạn đã ghi ở tiêu đề nhóm. */
  showDeadline?: boolean;
}) {
  const paging = usePagination(tasks, { storageKey: pageKey, resetKeys });
  const rows = pageKey ? paging.pageItems : tasks;
  const router = useRouter();
  return (
    <>
    <div className={styles.tw}>
      <table className={`${styles.table} ${styles.taskTable}`}>
        {showDeadline && (
          <thead className={styles.deskOnly}>
            <tr>
              <th>Cán bộ</th>
              <th>Hạn</th>
              <th />
            </tr>
          </thead>
        )}
        <tbody>
          {rows.map((task) => (
            <tr key={`${task.recordId}-${task.step}`} className={styles.rowLink} onClick={() => router.push(`/evaluations/${task.recordId}`)}>
              <td>
                <div className={styles.person}>
                  <div className="initials">{initialsOf(task.fullName)}</div>
                  <div>
                    <Link href={`/evaluations/${task.recordId}`} className={styles.name} onClick={(e) => e.stopPropagation()}>
                      {task.fullName}
                    </Link>
                    {task.isOwnRecord && <span className={styles.hint}>Hồ sơ của tôi</span>}
                    {periodId && task.periodId !== periodId && <span className={styles.hint}>{task.periodName}</span>}
                    <span className={styles.sub}>
                      {[task.departmentName, task.partyCellName].filter(Boolean).join(" · ") || "—"}
                    </span>
                    {task.returnReason && <span className={styles.reason}>Bị trả lại: {task.returnReason}</span>}
                    {showDeadline && (
                      <span className={`${styles.sub} ${styles.mobileOnly} num`}>
                        Hạn <Deadline task={task} />
                      </span>
                    )}
                  </div>
                </div>
              </td>
              {showDeadline && (
                <td className={`${styles.nowrap} ${styles.deskOnly} num`}>
                  <Deadline task={task} />
                </td>
              )}
              <td className={styles.right}>
                <Link
                  href={`/evaluations/${task.recordId}`}
                  className="btn btn-sm btn-primary"
                  onClick={(e) => e.stopPropagation()}
                >
                  {taskActionLabel(task)}
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
    {pageKey && (
      <Pagination
        page={paging.page}
        pageSize={paging.pageSize}
        total={paging.total}
        onPageChange={paging.setPage}
        onPageSizeChange={paging.setPageSize}
      />
    )}
    </>
  );
}

export default TaskTable;
