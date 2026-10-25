"use client";

import React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Pagination } from "@/components/common/Pagination";
import { usePagination } from "@/components/common/usePagination";
import styles from "./dashboard.module.css";
import { TaskRow, deadlineState, initialsOf, isWorkflowStep, shortDate, taskActionLabel, taskHref } from "./taskUtils";

export type { TaskRow } from "./taskUtils";

/** Hạn của việc: quá hạn đỏ, sắp hạn vàng; không có hạn thì không hiện gì. */
function Deadline({ task }: { task: TaskRow }) {
  const date = shortDate(task.deadline);
  if (!date) return null;
  const state = deadlineState(task.deadline, task.overdue);
  const cls = state === "overdue" ? styles.late : state === "soon" ? styles.soon : styles.muted;
  return (
    <span className={`${cls} num`}>
      Hạn {date}
      {state === "overdue" && " · quá hạn"}
    </span>
  );
}

/** Hạn chung của nhóm ghi ở tiêu đề nhóm. */
export function GroupDeadline({ deadline, overdue }: { deadline: string | null; overdue: boolean }) {
  const date = shortDate(deadline);
  if (!date) return null;
  const state = deadlineState(deadline, overdue);
  return (
    <span className={`${styles.groupDue} num ${state === "overdue" ? styles.late : state === "soon" ? styles.soon : styles.muted}`}>
      Hạn {date}
      {state === "overdue" && " · quá hạn"}
    </span>
  );
}

/** Bảng việc của một nhóm; việc thuộc kỳ khác kỳ đang chọn hiện thêm tên kỳ. */
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
  const deadlineColumn = showDeadline && tasks.some((t) => !!shortDate(t.deadline));
  return (
    <>
      <div className={styles.tw}>
        <table className={`${styles.table} ${styles.taskTable}`}>
          <tbody>
            {rows.map((task) => {
              const href = taskHref(task);
              // Nhóm ngoài luồng (kiến nghị, kế hoạch, điều chỉnh việc): máy chủ ghi tình trạng cụ thể của từng việc.
              const detail = !isWorkflowStep(task.step) ? task.statusDisplayName : null;
              return (
                <tr key={`${task.recordId}-${task.step}-${task.formId ?? ""}`} className={styles.rowLink} onClick={() => router.push(href)}>
                  <td>
                    <div className={styles.person}>
                      <div className="initials">{initialsOf(task.fullName)}</div>
                      <div>
                        <Link href={href} className={styles.name} onClick={(e) => e.stopPropagation()}>
                          {task.fullName}
                        </Link>
                        {task.isOwnRecord && <span className={styles.hint}>Hồ sơ của tôi</span>}
                        {periodId && task.periodId !== periodId && <span className={styles.hint}>{task.periodName}</span>}
                        <span className={styles.sub}>
                          {[task.departmentName, task.partyCellName].filter(Boolean).join(" · ") || "—"}
                        </span>
                        {detail && <span className={styles.sub}>{detail}</span>}
                        {task.returnReason && <span className={styles.reason}>Bị trả lại: {task.returnReason}</span>}
                        {deadlineColumn && shortDate(task.deadline) && (
                          <span className={`${styles.sub} ${styles.mobileOnly}`}>
                            <Deadline task={task} />
                          </span>
                        )}
                      </div>
                    </div>
                  </td>
                  {deadlineColumn && (
                    <td className={`${styles.nowrap} ${styles.deskOnly}`}>
                      <Deadline task={task} />
                    </td>
                  )}
                  <td className={styles.right}>
                    <Link href={href} className={`btn btn-sm btn-primary ${styles.nowrap}`} onClick={(e) => e.stopPropagation()}>
                      {taskActionLabel(task)}
                    </Link>
                  </td>
                </tr>
              );
            })}
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
