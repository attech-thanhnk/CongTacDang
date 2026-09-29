"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { NotificationSummaryDto, postPublishService } from "@/services/postPublishService";

const KIND_ICON: Record<string, { icon: string; color: string }> = {
  overdue: { icon: "bi-exclamation-octagon-fill", color: "var(--color-danger)" },
  dueSoon: { icon: "bi-alarm-fill", color: "#d97706" },
  appeal: { icon: "bi-chat-square-text-fill", color: "var(--color-cobalt)" },
  improvementPlan: { icon: "bi-clipboard2-pulse-fill", color: "#7c3aed" },
  acknowledge: { icon: "bi-pen-fill", color: "#0f766e" },
  pending: { icon: "bi-inbox-fill", color: "var(--text-secondary)" },
};

const formatDate = (value?: string | null) =>
  value ? new Intl.DateTimeFormat("vi-VN", { dateStyle: "short" }).format(new Date(value)) : null;

/**
 * Chuông nhắc việc ở header (task 20 — T-89): số việc đang chờ, bước sắp tới hạn / quá hạn, kiến nghị mới, kế hoạch 30-60-90 ngày
 * cần lập hoặc chờ xác nhận. Tính khi tải trang và khi chuyển trang (API `GET /api/notifications/summary`), không đẩy thời gian thực.
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
  const urgent = (summary?.overdue ?? 0) > 0;

  const stats = summary
    ? [
        { label: "Quá hạn", value: summary.overdue, className: "text-danger" },
        { label: `Sắp tới hạn (≤ ${summary.dueSoonDays} ngày)`, value: summary.dueSoon, className: "text-warning" },
        { label: "Kiến nghị chờ xử lý", value: summary.appeals, className: "" },
        { label: "Kế hoạch 30-60-90 cần lập/duyệt", value: summary.improvementPlans, className: "" },
        { label: "Kế hoạch chờ bạn xác nhận", value: summary.plansToAcknowledge, className: "" },
      ].filter((s) => s.value > 0)
    : [];

  return (
    <div style={{ position: "relative" }}>
      <button
        type="button"
        onClick={() => { setOpen(!open); if (!open) load(); }}
        aria-label={`Nhắc việc: ${total} việc đang chờ`}
        title="Nhắc việc"
        style={{
          width: "34px",
          height: "34px",
          background: "transparent",
          border: "1px solid var(--border-base)",
          borderRadius: "50%",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          cursor: "pointer",
          color: "var(--text-secondary)",
          position: "relative",
        }}
      >
        <i className={`bi ${total > 0 ? "bi-bell-fill" : "bi-bell"}`} style={{ fontSize: "15px" }} />
        {total > 0 && (
          <span
            className={`badge rounded-pill ${urgent ? "text-bg-danger" : "text-bg-primary"}`}
            style={{ position: "absolute", top: "-4px", right: "-6px", fontSize: "10px" }}
          >
            {total > 99 ? "99+" : total}
          </span>
        )}
      </button>

      {open && (
        <>
          <div onClick={() => setOpen(false)} style={{ position: "fixed", inset: 0, zIndex: 1040 }} />
          <div
            role="dialog"
            aria-label="Nhắc việc"
            style={{
              position: "absolute",
              right: 0,
              top: "calc(100% + 8px)",
              width: "340px",
              maxWidth: "90vw",
              background: "var(--bg-card)",
              border: "1px solid var(--border-base)",
              borderRadius: "var(--radius-lg)",
              boxShadow: "var(--shadow-lg)",
              zIndex: 1050,
              overflow: "hidden",
            }}
          >
            <div style={{ padding: "12px 14px", borderBottom: "1px solid var(--border-subtle)", background: "var(--bg-app)" }}>
              <div className="fw-semibold small">Nhắc việc</div>
              <div className="small text-secondary">
                {total > 0 ? `${total} việc đang chờ bạn` : "Không có việc nào đang chờ bạn"}
              </div>
              {stats.length > 0 && (
                <div className="d-flex flex-wrap gap-1 mt-2">
                  {stats.map((s) => (
                    <span key={s.label} className={`badge text-bg-light border fw-normal ${s.className}`}>{s.label}: <strong>{s.value}</strong></span>
                  ))}
                </div>
              )}
            </div>
            <ul className="list-unstyled mb-0" style={{ maxHeight: "320px", overflowY: "auto" }}>
              {(summary?.items ?? []).map((item, index) => {
                const style = KIND_ICON[item.kind] ?? KIND_ICON.pending;
                return (
                  <li key={`${item.link}-${item.kind}-${index}`} style={{ borderBottom: "1px solid var(--border-subtle)" }}>
                    <Link href={item.link} onClick={() => setOpen(false)} className="d-flex gap-2 px-3 py-2 text-decoration-none" style={{ color: "var(--text-primary)" }}>
                      <i className={`bi ${style.icon}`} style={{ color: style.color, marginTop: "2px" }} />
                      <span className="small" style={{ minWidth: 0 }}>
                        <span className="d-block fw-semibold text-truncate">{item.title}</span>
                        {item.detail && <span className="d-block text-secondary text-truncate">{item.detail}</span>}
                        {item.deadline && <span className="d-block text-secondary">Hạn: {formatDate(item.deadline)}</span>}
                      </span>
                    </Link>
                  </li>
                );
              })}
            </ul>
            <div style={{ padding: "8px 14px" }}>
              <Link href="/work-queue" onClick={() => setOpen(false)} className="small">Xem tất cả việc cần xử lý</Link>
            </div>
          </div>
        </>
      )}
    </div>
  );
}

export default NotificationBell;
