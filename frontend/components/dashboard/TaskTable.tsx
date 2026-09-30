"use client";

import React from "react";
import Link from "next/link";
import type { WorkQueueItemDto, WorkflowStepCode } from "@/services/evaluationService";
import styles from "./dashboard.module.css";
import { deadlineState, initialsOf, shortDate, taskActionLabel } from "./taskUtils";

export interface TaskRow extends WorkQueueItemDto {
  step: WorkflowStepCode;
}

/** Hạn của việc: quá hạn đỏ, sắp hạn vàng; bị trả lại ghi thêm. */
function DeadlineCell({ task }: { task: TaskRow }) {
  const state = deadlineState(task.deadline, task.overdue);
  const date = shortDate(task.deadline);
  const cls = state === "overdue" ? styles.late : state === "soon" ? styles.soon : styles.muted;
  return (
    <td className={`${styles.nowrap} num`}>
      <span className={cls}>
        {date || "—"}
        {state === "overdue" && " · quá hạn"}
      </span>
      {task.returnReason && (
        <>
          <span className={styles.muted}> · </span>
          <span className={styles.late}>bị trả lại</span>
        </>
      )}
    </td>
  );
}

/**
 * Bảng việc cần xử lý (Tổng quan và trang Việc cần xử lý): cán bộ, hạn, nút ghi đúng tên việc.
 * Việc thuộc kỳ khác kỳ đang chọn hiện thêm tên kỳ cạnh tên cán bộ.
 */
export function TaskTable({ tasks, periodId }: { tasks: TaskRow[]; periodId?: string }) {
  return (
    <div className={styles.tw}>
      <table className={styles.table}>
        <thead>
          <tr>
            <th>Cán bộ</th>
            <th>Hạn</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {tasks.map((task) => (
            <tr key={`${task.recordId}-${task.step}`}>
              <td>
                <div className={styles.person}>
                  <div className="initials">{initialsOf(task.fullName)}</div>
                  <div>
                    <Link href={`/evaluations/${task.recordId}`} className={styles.name}>
                      {task.fullName}
                    </Link>
                    {task.isOwnRecord && <span className={styles.hint}>Hồ sơ của tôi</span>}
                    {periodId && task.periodId !== periodId && <span className={styles.hint}>{task.periodName}</span>}
                    <span className={styles.sub}>
                      {[task.departmentName, task.partyCellName].filter(Boolean).join(" · ") || "—"}
                    </span>
                    {task.returnReason && <span className={styles.reason}>{task.returnReason}</span>}
                  </div>
                </div>
              </td>
              <DeadlineCell task={task} />
              <td className={styles.right}>
                <Link
                  href={`/evaluations/${task.recordId}`}
                  className={`btn btn-sm ${task.returnReason ? "btn-outline-secondary" : "btn-primary"}`}
                >
                  {taskActionLabel(task)}
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default TaskTable;
