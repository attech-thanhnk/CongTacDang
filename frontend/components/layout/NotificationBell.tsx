"use client";

import { formatDate } from "@/lib/dateFormat";
import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { NotificationSummaryDto, postPublishService } from "@/services/postPublishService";

const KIND_ICON: Record<string, { icon: string; color: string }> = {
  overdue: { icon: "bi-exclamation-octagon", color: "var(--bad)" },
  dueSoon: { icon: "bi-alarm", color: "var(--wait)" },
  appealUnassigned: { icon: "bi-person-exclamation", color: "var(--bad)" },
  appeal: { icon: "bi-chat-square-text", color: "var(--info)" },
  improvementPlan: { icon: "bi-clipboard2-pulse", color: "var(--info)" },
  acknowledge: { icon: "bi-pen", color: "var(--ok)" },
  pending: { icon: "bi-inbox", color: "var(--ink-2)" },
};

/**
 * Chuông nhắc việc ở thanh trên (task 20 — T-89): số việc đang chờ, bước sắp tới hạn / quá hạn, kiến nghị mới, kế hoạch 30-60-90 ngày
 * cần lập hoặc chờ xác nhận. Tính khi tải trang và khi chuyển trang (API `GET /api/notifications/summary`), không đẩy thời gian thực.
 * Mỗi mục dẫn tới hồ sơ (`/evaluations/{id}`) hoặc trang liên quan do máy chủ trả về.
 */
export function NotificationBell() {
  const pathname = usePathname();
  const [summary, setSummary] = useState<NotificationSummaryDto | null>(null);
  const [open, setOpen] = useState(false);

  const load = useCallback(async () => {
    try {
      setSummary(await postPublishService.getNotificationSummary());
    } catch {
      setSummary(null);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load, pathname]);

  const total = summary?.total ?? 0;

  const stats = summary
    ? [
        { label: "Quá hạn", value: summary.overdue, tone: "bad" },
        { label: `Sắp tới hạn (≤ ${summary.dueSoonDays} ngày)`, value: summary.dueSoon, tone: "wait" },
        { label: "Kiến nghị", value: summary.appeals, tone: "info" },
        { label: "Kế hoạch cần lập/duyệt", value: summary.improvementPlans, tone: "info" },
        { label: "Kế hoạch chờ xác nhận", value: summary.plansToAcknowledge, tone: "info" },
      ].filter((s) => s.value > 0)
    : [];

  return (
    <div className="bell">
      <button
        type="button"
        className="bell-btn"
        onClick={() => {
          setOpen(!open);
          if (!open) load();
        }}
        aria-label={`Nhắc việc: ${total} việc đang chờ`}
        aria-expanded={open}
        title="Nhắc việc"
      >
        <i className="bi bi-bell" />
        {total > 0 && <span className="bell-count">{total > 99 ? "99+" : total}</span>}
      </button>

      {open && (
        <>
          <div className="bell-backdrop" onClick={() => setOpen(false)} />
          <div className="bell-panel" role="dialog" aria-label="Nhắc việc">
            <div className="bell-head">
              <b>Nhắc việc</b>
              {stats.length > 0 && (
                <div className="bell-stats">
                  {stats.map((s) => (
                    <span key={s.label} className={`tag t-${s.tone}`}>
                      {s.label}: {s.value}
                    </span>
                  ))}
                </div>
              )}
            </div>
            <ul className="bell-list">
              {(summary?.items ?? []).length === 0 && <li className="bell-empty">Không có việc nào</li>}
              {(summary?.items ?? []).map((item, index) => {
                const style = KIND_ICON[item.kind] ?? KIND_ICON.pending;
                return (
                  <li key={`${item.link}-${item.kind}-${index}`}>
                    <Link href={item.link || "/work-queue"} onClick={() => setOpen(false)} className="bell-item">
                      <i className={`bi ${style.icon}`} style={{ color: style.color }} />
                      <span className="bell-item-text">
                        <b className="text-truncate">{item.title}</b>
                        {item.detail && <small className="text-truncate">{item.detail}</small>}
                        {item.deadline && <small>Hạn {formatDate(item.deadline)}</small>}
                      </span>
                    </Link>
                  </li>
                );
              })}
            </ul>
            <Link href="/work-queue" onClick={() => setOpen(false)} className="bell-all">
              Xem tất cả
            </Link>
          </div>
        </>
      )}
    </div>
  );
}

export default NotificationBell;
