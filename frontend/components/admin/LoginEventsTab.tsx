"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { Card, Pagination, usePageSize } from "@/components/common";
import { auditService, LOGIN_RESULT_LABELS, LoginEventDto, LoginResult } from "@/services/auditService";
import type { PagedResult } from "@/services/userService";
import { PickedUser, UserPicker } from "./UserPicker";
import styles from "./LoginEventsTab.module.css";
import { endOfDayExclusiveIso, errorMessage, errorTitle, formatDateTime, startOfDayIso } from "./adminUtils";

const RESULT_CLASS: Record<LoginResult, string> = {
  Success: "tag t-ok",
  InvalidPassword: "tag t-bad",
  UnknownUser: "tag t-wait",
  LockedOut: "tag t-wait",
  Disabled: "status-badge tone-neutral",
};

interface Filters {
  user: PickedUser | null;
  result: LoginResult | "";
  from: string;
  to: string;
}

const EMPTY: Filters = { user: null, result: "", from: "", to: "" };

/** Tên trình duyệt và hệ điều hành rút từ user-agent; chuỗi đầy đủ xem khi rê chuột. */
function describeAgent(ua?: string | null): string {
  if (!ua) return "";
  const browser = /Edg\//.test(ua)
    ? "Edge"
    : /OPR\//.test(ua)
      ? "Opera"
      : /Firefox\//.test(ua)
        ? "Firefox"
        : /Chrome\//.test(ua)
          ? "Chrome"
          : /Safari\//.test(ua)
            ? "Safari"
            : "";
  const os = /Windows/.test(ua)
    ? "Windows"
    : /Android/.test(ua)
      ? "Android"
      : /iPhone|iPad|iOS/.test(ua)
        ? "iOS"
        : /Mac OS X/.test(ua)
          ? "macOS"
          : /Linux/.test(ua)
            ? "Linux"
            : "";
  const text = [browser, os].filter(Boolean).join(" · ");
  return text || "Trình duyệt khác";
}

/** Tab "Nhật ký đăng nhập": lọc theo người, kết quả, khoảng thời gian; phân trang phía máy chủ. */
export function LoginEventsTab() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canPickUser = hasPermission("system.users.read");
  const [draft, setDraft] = useState<Filters>(EMPTY);
  const [filters, setFilters] = useState<Filters>(EMPTY);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = usePageSize("admin.loginEvents");
  const [data, setData] = useState<PagedResult<LoginEventDto> | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setData(
        await auditService.getLoginEvents({
          userId: filters.user?.id,
          result: filters.result || undefined,
          from: startOfDayIso(filters.from),
          to: endOfDayExclusiveIso(filters.to),
          page,
          pageSize,
        })
      );
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Tải nhật ký đăng nhập không thành công."), errorTitle(err));
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filters, page, pageSize]);

  useEffect(() => {
    load();
  }, [load]);

  const apply = (event: React.FormEvent) => {
    event.preventDefault();
    if (draft.from && draft.to && draft.to < draft.from) {
      toast.error("Ngày kết thúc phải bằng hoặc sau ngày bắt đầu.", "Dữ liệu chưa hợp lệ");
      return;
    }
    setPage(1);
    setFilters({ ...draft });
  };

  const items = data?.items ?? [];
  const hasFilters = !!(filters.user || filters.result || filters.from || filters.to);

  return (
    <Card>
      <div className="card-header">
        <form className="row g-2 align-items-end" onSubmit={apply}>
          {canPickUser && (
            <div className="col-12 col-md-4">
              <label className="form-label">Tài khoản</label>
              <UserPicker value={draft.user} activeOnly={false} onChange={(user) => setDraft({ ...draft, user })} />
            </div>
          )}
          <div className="col-12 col-sm-6 col-md-2">
            <label className="form-label" htmlFor="login-result">Kết quả</label>
            <select
              id="login-result"
              className="form-select form-select-sm"
              value={draft.result}
              onChange={(e) => setDraft({ ...draft, result: e.target.value as LoginResult | "" })}
            >
              <option value="">Tất cả</option>
              {(Object.keys(LOGIN_RESULT_LABELS) as LoginResult[]).map((key) => (
                <option key={key} value={key}>
                  {LOGIN_RESULT_LABELS[key]}
                </option>
              ))}
            </select>
          </div>
          <div className="col-6 col-md-2">
            <label className="form-label" htmlFor="login-from">Từ ngày</label>
            <input id="login-from" type="date" className="form-control form-control-sm" value={draft.from} onChange={(e) => setDraft({ ...draft, from: e.target.value })} />
          </div>
          <div className="col-6 col-md-2">
            <label className="form-label" htmlFor="login-to">Đến hết ngày</label>
            <input id="login-to" type="date" className="form-control form-control-sm" value={draft.to} onChange={(e) => setDraft({ ...draft, to: e.target.value })} />
          </div>
          <div className="col-12 col-md-2 d-flex gap-1">
            <button type="submit" className="btn btn-sm btn-outline-primary flex-fill" disabled={loading}>
              <i className="bi bi-search me-1" />
              Lọc
            </button>
            <button
              type="button"
              className="btn btn-sm btn-outline-secondary"
              title="Xóa bộ lọc"
              aria-label="Xóa bộ lọc"
              onClick={() => {
                setDraft(EMPTY);
                setFilters(EMPTY);
                setPage(1);
              }}
            >
              <i className="bi bi-x-lg" />
            </button>
          </div>
        </form>
      </div>
      <div className="table-responsive">
        <table className={`table table-hover align-middle mb-0 ${styles.table}`}>
          <thead>
            <tr>
              <th className="ps-3">Thời điểm</th>
              <th>Tài khoản</th>
              <th>Kết quả</th>
              <th className="pe-3">Thiết bị</th>
            </tr>
          </thead>
          <tbody>
            {loading && !data ? (
              <tr>
                <td colSpan={4} className="text-center py-5 text-secondary loading-delay">
                  <span className="spinner-border spinner-border-sm me-2" />
                  Đang tải…
                </td>
              </tr>
            ) : items.length === 0 ? (
              <tr>
                <td colSpan={4} className="text-center py-5 text-secondary">
                  <i className="bi bi-inbox fs-3 d-block mb-2" />
                  {hasFilters ? "Không có lần đăng nhập phù hợp với bộ lọc." : "Chưa có lần đăng nhập nào được ghi lại."}
                </td>
              </tr>
            ) : (
              items.map((event) => (
                <tr key={event.id} className={styles.row} style={{ opacity: loading ? 0.6 : 1 }}>
                  <td className={`ps-3 text-nowrap num ${styles.time}`}>{formatDateTime(event.createdAt)}</td>
                  <td className={`fw-semibold ${styles.user}`}>
                    {event.userId && canPickUser ? (
                      <Link href={`/admin/users/${event.userId}`} className="text-decoration-none">
                        {event.usernameAttempted}
                      </Link>
                    ) : (
                      event.usernameAttempted
                    )}
                  </td>
                  <td className={`text-nowrap ${styles.result}`}>
                    <span className={RESULT_CLASS[event.result] ?? "status-badge tone-neutral"}>
                      {LOGIN_RESULT_LABELS[event.result] ?? event.result}
                    </span>
                  </td>
                  <td className={`pe-3 small text-secondary ${styles.device}`}>
                    <div className="text-nowrap" title={event.userAgent ?? undefined}>
                      {describeAgent(event.userAgent) || "—"}
                    </div>
                    {event.ipAddress && <div className="font-monospace" style={{ fontSize: 12 }}>IP {event.ipAddress}</div>}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
      {data && (
        <Pagination
          page={data.page}
          pageSize={data.pageSize}
          total={data.totalCount}
          disabled={loading}
          onPageChange={setPage}
          onPageSizeChange={(size) => {
            setPageSize(size);
            setPage(1);
          }}
        />
      )}
    </Card>
  );
}

export default LoginEventsTab;
