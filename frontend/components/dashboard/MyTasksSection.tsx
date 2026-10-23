"use client";

import React from "react";
import Link from "next/link";
import type { WorkQueueDto } from "@/services/evaluationService";
import styles from "./dashboard.module.css";
import { commonDeadline, queueGroups } from "./taskUtils";
import { GroupDeadline, TaskTable } from "./TaskTable";

/** Số dòng mỗi nhóm khi có nhiều nhóm; chỉ một nhóm thì hiện nhiều hơn. */
const ROWS_PER_GROUP = 3;
const ROWS_SINGLE_GROUP = 8;

/** Việc đang chờ người dùng ở mọi kỳ đang mở, nhóm theo loại việc; phần còn lại xem ở trang Việc cần xử lý. */
export function MyTasksSection({ queue, loading, periodId }: { queue: WorkQueueDto | null; loading: boolean; periodId?: string }) {
  const groups = queueGroups(queue);
  const total = groups.reduce((sum, g) => sum + g.items.length, 0);
  const limit = groups.length > 1 ? ROWS_PER_GROUP : ROWS_SINGLE_GROUP;
  const truncated = groups.some((g) => g.items.length > limit);

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

  if (total === 0) {
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
    <section className={styles.block} aria-label="Việc cần xử lý">
      <div className={styles.blockH}>
        <h2>
          Việc cần xử lý <span className={styles.count}>{total}</span>
        </h2>
        {truncated && (
          <Link href="/work-queue" className={styles.link}>
            Xem tất cả
          </Link>
        )}
      </div>
      {groups.map((group) => {
        const common = commonDeadline(group.items);
        const hidden = group.items.length - limit;
        return (
          <div key={group.step} className={styles.taskGroup}>
            <div className={styles.groupH}>
              <h3>
                {group.stepName} <span className={styles.count}>{group.items.length}</span>
              </h3>
              {common && <GroupDeadline deadline={common.deadline} overdue={common.overdue} />}
            </div>
            <TaskTable tasks={group.items.slice(0, limit)} periodId={periodId} showDeadline={!common} />
            {hidden > 0 && (
              <Link href={`/work-queue#${group.step}`} className={styles.moreBtn}>
                Xem thêm {hidden} việc
              </Link>
            )}
          </div>
        );
      })}
    </section>
  );
}

export default MyTasksSection;
