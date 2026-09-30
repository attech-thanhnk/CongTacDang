"use client";

import React from "react";
import Link from "next/link";
import type { WorkQueueDto } from "@/services/evaluationService";
import { formatDate } from "@/lib/dateFormat";
import { RecordStatusBadge } from "@/components/common/StatusBadge";
import styles from "./dashboard.module.css";
import { deadlineState, flattenQueue } from "./taskUtils";

const MAX_ROWS = 8;

/** Việc đang chờ người dùng (work-queue), mỗi việc một dòng, quá hạn lên đầu. */
export function MyTasksSection({ queue, loading }: { queue: WorkQueueDto | null; loading: boolean }) {
  const tasks = flattenQueue(queue);
  const total = queue?.total ?? 0;

  return (
    <section className={styles.sectionPrimary}>
      <div className={styles.sectionHeader}>
        <h2 className={styles.sectionTitle}>
          <span className={total > 0 ? styles.bigCount : styles.bigCountZero}>{loading ? "…" : total}</span>
          Việc cần xử lý của tôi
        </h2>
        {total > 0 && (
          <Link href="/work-queue" className="btn btn-outline-primary btn-sm">
            Xem tất cả
          </Link>
        )}
      </div>

      {loading ? (
        <div className={styles.empty}>
          <span className="spinner-border spinner-border-sm me-2" />
          Đang tải...
        </div>
      ) : tasks.length === 0 ? (
        <div className={styles.empty}>
          <i className="bi bi-check2-circle text-success me-2" />
          Không có việc nào đang chờ bạn
        </div>
      ) : (
        <>
          <ul className={styles.taskList}>
            {tasks.slice(0, MAX_ROWS).map((task) => {
              const state = deadlineState(task.deadline, task.overdue);
              return (
                <li key={`${task.recordId}-${task.stepName}`} className={state === "overdue" ? styles.taskRowOverdue : styles.taskRow}>
                  <div className={styles.taskMain}>
                    <div className={styles.taskName}>{task.isOwnRecord ? "Hồ sơ của tôi" : task.fullName}</div>
                    <div className={styles.taskMeta}>
                      {task.stepName}
                      {task.departmentName ? ` · ${task.departmentName}` : ""}
                    </div>
                    {task.returnReason && <div className={styles.reason}>Bị trả lại: {task.returnReason}</div>}
                  </div>
                  <div className={styles.taskStep}>
                    <RecordStatusBadge status={task.status} label={task.statusDisplayName} returned={!!task.returnReason} />
                  </div>
                  <div
                    className={
                      state === "overdue" ? styles.deadlineOverdue : state === "soon" ? styles.deadlineSoon : styles.deadline
                    }
                  >
                    {task.deadline ? `Hạn ${formatDate(task.deadline)}` : "Không hạn"}
                  </div>
                  <Link href={`/evaluations/${task.recordId}`} className="btn btn-primary btn-sm">
                    {task.mode === "External" ? "Ghi nhận" : "Xử lý"}
                  </Link>
                </li>
              );
            })}
          </ul>
          {tasks.length > MAX_ROWS && (
            <div className={`${styles.sectionBody} border-top`}>
              <Link href="/work-queue" className="fw-semibold text-decoration-none">
                Xem thêm {tasks.length - MAX_ROWS} việc
              </Link>
            </div>
          )}
        </>
      )}
    </section>
  );
}

export default MyTasksSection;
