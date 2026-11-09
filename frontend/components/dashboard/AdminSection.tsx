"use client";

import React from "react";
import Link from "next/link";
import type { AdminSummaryDto } from "@/services/dashboardService";
import type { AuditLogDto } from "@/services/auditService";
import { actionMeta, actorLabel, entityLabel, entityName } from "@/components/admin/auditLabels";
import { formatDateTime } from "@/components/admin/adminUtils";
import styles from "./dashboard.module.css";

/** Quản trị: việc cần xử lý (chỉ mục khác 0), mức sử dụng, hoạt động gần đây. */
export function AdminSection({ summary, logs }: { summary: AdminSummaryDto | null; logs: AuditLogDto[] | null }) {
  const items = summary?.items ?? [];
  const usage = summary?.usage;
  // Người quản lý kỳ không có usage: chỉ hiện khi thật sự có việc.
  const showSummary = !!summary && (!!usage || items.length > 0);
  const hasLogs = !!logs && logs.length > 0;
  if (!showSummary && !hasLogs) return null;

  return (
    <section className={styles.block} aria-label="Quản trị">
      {showSummary && (
        <>
          <div className={styles.blockH}>
            <h2>{usage ? "Quản trị cần xử lý" : "Kỳ đánh giá cần xử lý"}</h2>
            {usage && (
              <span className={`${styles.usage} num`}>
                {usage.recentLogins}/{usage.activeAccounts} tài khoản đã đăng nhập trong {usage.days} ngày
              </span>
            )}
          </div>
          {items.length === 0 ? (
            <div className={styles.allDone}>
              <i className="bi bi-check2-circle" aria-hidden="true" />
              Không có việc cần xử lý
            </div>
          ) : (
            <div className={styles.alerts}>
              {items.map((item) => (
                <Link key={item.code} href={item.link} className={styles.alert}>
                  <span>{item.label}</span>
                  <b className="num">{item.count}</b>
                  <i className={`bi bi-chevron-right ${styles.shortcutGo}`} aria-hidden="true" />
                </Link>
              ))}
            </div>
          )}
        </>
      )}

      {logs && hasLogs && (
        <div className={showSummary ? styles.topRule : undefined}>
          <div className={styles.groupH}>
            <h3>Hoạt động gần đây</h3>
            <Link href="/audit" className={styles.link}>
              Nhật ký
            </Link>
          </div>
          <ul className={styles.activity}>
            {logs.map((log) => {
              const name = entityName(log);
              return (
                <li key={log.id}>
                  <span className={`${styles.activityTime} num`}>{formatDateTime(log.createdAt)}</span>
                  <span className={styles.activityText}>
                    <b>{actorLabel(log.actorName)}</b> · {actionMeta(log.action).label} · {entityLabel(log.entityType)}
                    {name && <span className={styles.muted}> · {name}</span>}
                  </span>
                </li>
              );
            })}
          </ul>
        </div>
      )}
    </section>
  );
}

export default AdminSection;
