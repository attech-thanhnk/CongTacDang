"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { Card } from "@/components/common";
import { auditService, LOGIN_RESULT_LABELS, LoginEventDto, LoginResult } from "@/services/auditService";
import type { PagedResult } from "@/services/userService";
import { Pagination } from "./Pagination";
import { PickedUser, UserPicker } from "./UserPicker";
import { endOfDayExclusiveIso, errorMessage, errorTitle, formatDateTime, startOfDayIso } from "./adminUtils";

const PAGE_SIZE = 50;

const RESULT_CLASS: Record<LoginResult, string> = {
  Success: "bg-success-subtle text-success border border-success-subtle",
  InvalidPassword: "bg-danger-subtle text-danger border border-danger-subtle",
  UnknownUser: "bg-warning-subtle text-warning-emphasis border border-warning-subtle",
  LockedOut: "bg-warning-subtle text-warning-emphasis border border-warning-subtle",
  Disabled: "bg-secondary-subtle text-secondary border",
};

interface Filters {
  user: PickedUser | null;
  result: LoginResult | "";
  from: string;
  to: string;
}

const EMPTY: Filters = { user: null, result: "", from: "", to: "" };

/** Tab "Nhật ký đăng nhập": lọc theo người, kết quả, khoảng thời gian; phân trang phía máy chủ. */
export function LoginEventsTab() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canPickUser = hasPermission("system.users.read");
  const [draft, setDraft] = useState<Filters>(EMPTY);
  const [filters, setFilters] = useState<Filters>(EMPTY);
  const [page, setPage] = useState(1);
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
          pageSize: PAGE_SIZE,
        })
      );
    } catch (err) {
      toast.error(errorMessage(err, "Không tải được nhật ký đăng nhập."), errorTitle(err));
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filters, page]);

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

  return (
    <>
      <Card className="mb-3">
        <form className="row g-2 align-items-end p-3" onSubmit={apply}>
          {canPickUser && (
            <div className="col-12 col-md-4">
              <label className="form-label small fw-semibold text-secondary mb-1">Tài khoản</label>
              <UserPicker value={draft.user} activeOnly={false} onChange={(user) => setDraft({ ...draft, user })} />
            </div>
          )}
          <div className="col-6 col-md-2">
            <label className="form-label small fw-semibold text-secondary mb-1">Kết quả</label>
            <select
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
            <label className="form-label small fw-semibold text-secondary mb-1">Từ ngày</label>
            <input type="date" className="form-control form-control-sm" value={draft.from} onChange={(e) => setDraft({ ...draft, from: e.target.value })} />
          </div>
          <div className="col-6 col-md-2">
            <label className="form-label small fw-semibold text-secondary mb-1">Đến hết ngày</label>
            <input type="date" className="form-control form-control-sm" value={draft.to} onChange={(e) => setDraft({ ...draft, to: e.target.value })} />
          </div>
          <div className="col-6 col-md-2 d-flex gap-1">
            <button type="submit" className="btn btn-sm btn-primary flex-fill" disabled={loading}>
              <i className="bi bi-search me-1" />
              Lọc
            </button>
            <button
              type="button"
              className="btn btn-sm btn-outline-secondary"
              title="Xóa bộ lọc"
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
      </Card>

      <Card>
        <div className="table-responsive">
          <table className="table table-hover align-middle mb-0" style={{ fontSize: 13 }}>
            <thead style={{ background: "var(--bg-base)" }}>
              <tr>
                <th className="ps-3">Thời điểm</th>
                <th>Tên đăng nhập</th>
                <th>Kết quả</th>
                <th>Địa chỉ IP</th>
                <th className="pe-3">Trình duyệt</th>
              </tr>
            </thead>
            <tbody>
              {loading && !data ? (
                <tr>
                  <td colSpan={5} className="text-center py-5 text-secondary">
                    <span className="spinner-border spinner-border-sm me-2" />
                    Đang tải…
                  </td>
                </tr>
              ) : items.length === 0 ? (
                <tr>
                  <td colSpan={5} className="text-center py-5 text-secondary">
                    <i className="bi bi-inbox fs-3 d-block mb-2" />
                    Không có lần đăng nhập phù hợp.
                  </td>
                </tr>
              ) : (
                items.map((event) => (
                  <tr key={event.id} style={{ opacity: loading ? 0.6 : 1 }}>
                    <td className="ps-3 text-nowrap">{formatDateTime(event.createdAt)}</td>
                    <td>
                      {event.userId && canPickUser ? (
                        <Link href={`/admin/users/${event.userId}`} className="text-decoration-none">
                          {event.usernameAttempted}
                        </Link>
                      ) : (
                        event.usernameAttempted
                      )}
                    </td>
                    <td>
                      <span className={`badge ${RESULT_CLASS[event.result] ?? "bg-light text-dark border"}`}>
                        {LOGIN_RESULT_LABELS[event.result] ?? event.result}
                      </span>
                    </td>
                    <td className="small">{event.ipAddress || "—"}</td>
                    <td className="pe-3 small text-secondary text-truncate" style={{ maxWidth: 280 }} title={event.userAgent ?? undefined}>
                      {event.userAgent || "—"}
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
            totalPages={data.totalPages}
            totalCount={data.totalCount}
            pageSize={data.pageSize}
            disabled={loading}
            onPageChange={setPage}
          />
        )}
      </Card>
    </>
  );
}

export default LoginEventsTab;
