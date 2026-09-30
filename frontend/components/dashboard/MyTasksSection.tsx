"use client";

import React from "react";
import Link from "next/link";
import type { WorkQueueDto } from "@/services/evaluationService";
import styles from "./dashboard.module.css";
import { flattenQueue } from "./taskUtils";
import { TaskTable } from "./TaskTable";

const MAX_ROWS = 8;

/** Việc đang chờ người dùng ở mọi kỳ đang mở, quá hạn lên đầu. */
export function MyTasksSection({ queue, loading, periodId }: { queue: WorkQueueDto | null; loading: boolean; periodId?: string }) {
  const tasks = flattenQueue(queue);
  const total = queue?.total ?? 0;

  return (
    <section className={styles.block}>
      <div className={styles.blockH}>
        <h2>
          Việc cần xử lý <span className={styles.count}>{loading ? "…" : total}</span>
        </h2>
        {total > 0 && (
          <Link href="/work-queue" className={styles.link}>
            {tasks.length > MAX_ROWS ? `Xem tất cả (${tasks.length})` : "Xem tất cả"}
          </Link>
        )}
      </div>

      {loading ? (
        <div className={styles.empty}>
          <span className="spinner-border spinner-border-sm me-2" />
          Đang tải...
        </div>
      ) : tasks.length === 0 ? (
        <div className={styles.empty}>Không có việc nào đang chờ bạn</div>
      ) : (
        <TaskTable tasks={tasks.slice(0, MAX_ROWS)} periodId={periodId} />
      )}
    </section>
  );
}

export default MyTasksSection;
