"use client";

import React from "react";
import Link from "next/link";
import type { WorkQueueDto } from "@/services/evaluationService";
import styles from "./dashboard.module.css";
import { flattenQueue } from "./taskUtils";
import { TaskTable } from "./TaskTable";

const MAX_ROWS = 8;

/** Việc đang chờ người dùng ở mọi kỳ đang mở, quá hạn lên đầu; không có việc thì chỉ một dòng. */
export function MyTasksSection({ queue, loading, periodId }: { queue: WorkQueueDto | null; loading: boolean; periodId?: string }) {
  const tasks = flattenQueue(queue);

  if (loading) {
    return (
      <section className={styles.block}>
        <div className={`${styles.empty} loading-delay`}>
          <span className="spinner-border spinner-border-sm me-2" />
          Đang tải...
        </div>
      </section>
    );
  }

  if (tasks.length === 0) {
    return (
      <section className={styles.block} aria-label="Việc cần xử lý">
        <div className={styles.allDone}>
          <i className="bi bi-check2-circle" aria-hidden="true" />
          Không có việc nào đang chờ bạn
        </div>
      </section>
    );
  }

  return (
    <section className={styles.block}>
      <div className={styles.blockH}>
        <h2>
          Việc cần xử lý <span className={styles.count}>{tasks.length}</span>
        </h2>
        {tasks.length > MAX_ROWS && (
          <Link href="/work-queue" className={styles.link}>
            Xem tất cả
          </Link>
        )}
      </div>
      <TaskTable tasks={tasks.slice(0, MAX_ROWS)} periodId={periodId} />
    </section>
  );
}

export default MyTasksSection;
