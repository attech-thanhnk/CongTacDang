"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { Card, PageHeader } from "@/components/common";
import { AccountFormModal } from "@/components/admin/AccountFormModal";
import { AccountStatusBadge } from "@/components/admin/AccountStatusBadge";
import { NoAccess } from "@/components/admin/NoAccess";
import { Pagination } from "@/components/admin/Pagination";
import { TemporaryCredential, TemporaryPasswordModal } from "@/components/admin/TemporaryPasswordModal";
import { errorMessage, errorTitle, formatDateTime } from "@/components/admin/adminUtils";
import { useCatalogOptions } from "@/components/admin/useCatalogOptions";
import { AccountListItem, CreateAccountPayload, PagedResult, userService } from "@/services/userService";

const PAGE_SIZE = 20;

type StatusFilter = "" | "active" | "inactive";

interface Filters {
  q: string;
  departmentId: string;
  partyCellId: string;
  status: StatusFilter;
}

const EMPTY_FILTERS: Filters = { q: "", departmentId: "", partyCellId: "", status: "" };

/** Quản trị tài khoản: danh sách phân trang phía máy chủ, tìm kiếm, lọc; tạo tài khoản. */
export default function AdminUsersPage() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canRead = hasPermission("system.users.read");
  const canManage = hasPermission("system.users.manage");
  const { departments, partyCells } = useCatalogOptions();

  const [draft, setDraft] = useState<Filters>(EMPTY_FILTERS);
  const [filters, setFilters] = useState<Filters>(EMPTY_FILTERS);
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<PagedResult<AccountListItem> | null>(null);
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const [saving, setSaving] = useState(false);
  const [credential, setCredential] = useState<TemporaryCredential | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await userService.search({
        page,
        pageSize: PAGE_SIZE,
        q: filters.q,
        departmentId: filters.departmentId || undefined,
        partyCellId: filters.partyCellId || undefined,
        isActive: filters.status === "" ? undefined : filters.status === "active",
      });
      setResult(data);
    } catch (err) {
      toast.error(errorMessage(err, "Không tải được danh sách tài khoản."), errorTitle(err));
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, filters]);

  useEffect(() => {
    if (canRead) load();
  }, [canRead, load]);

  const applyFilters = (event?: React.FormEvent) => {
    event?.preventDefault();
    setPage(1);
    setFilters({ ...draft });
  };

  const clearFilters = () => {
    setDraft(EMPTY_FILTERS);
    setFilters(EMPTY_FILTERS);
    setPage(1);
  };

  const handleCreate = async (payload: CreateAccountPayload) => {
    setSaving(true);
    try {
      const created = await userService.create(payload);
      setCreating(false);
      setCredential({
        title: "Đã tạo tài khoản",
        fullName: payload.fullName,
        username: created.username,
        temporaryPassword: created.temporaryPassword,
      });
      await load();
    } catch (err) {
      toast.error(errorMessage(err, "Không tạo được tài khoản."), errorTitle(err), 8000);
    } finally {
      setSaving(false);
    }
  };

  if (!canRead) return <NoAccess title="Tài khoản" permissionName="Xem tài khoản, hồ sơ cán bộ" />;

  const items = result?.items ?? [];

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Tài khoản"
        subTitle="Tài khoản đăng nhập và hồ sơ cán bộ trong phạm vi bạn được xem."
        actions={
          canManage ? (
            <button type="button" className="btn btn-sm btn-primary" onClick={() => setCreating(true)}>
              <i className="bi bi-person-plus me-1" />
              Tạo tài khoản
            </button>
          ) : undefined
        }
      />

      <div className="page-body">
        <Card className="mb-3">
          <form className="row g-2 align-items-end p-3" onSubmit={applyFilters}>
            <div className="col-12 col-md-4">
              <label className="form-label small fw-semibold text-secondary mb-1">Tìm kiếm</label>
              <input
                className="form-control form-control-sm"
                placeholder="Tên đăng nhập, họ tên, email, số thẻ Đảng…"
                value={draft.q}
                onChange={(e) => setDraft({ ...draft, q: e.target.value })}
              />
            </div>
            <div className="col-6 col-md-2">
              <label className="form-label small fw-semibold text-secondary mb-1">Phòng / đơn vị</label>
              <select className="form-select form-select-sm" value={draft.departmentId} onChange={(e) => setDraft({ ...draft, departmentId: e.target.value })}>
                <option value="">Tất cả</option>
                {departments.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="col-6 col-md-2">
              <label className="form-label small fw-semibold text-secondary mb-1">Chi bộ</label>
              <select className="form-select form-select-sm" value={draft.partyCellId} onChange={(e) => setDraft({ ...draft, partyCellId: e.target.value })}>
                <option value="">Tất cả</option>
                {partyCells.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="col-6 col-md-2">
              <label className="form-label small fw-semibold text-secondary mb-1">Trạng thái</label>
              <select
                className="form-select form-select-sm"
                value={draft.status}
                onChange={(e) => setDraft({ ...draft, status: e.target.value as StatusFilter })}
              >
                <option value="">Tất cả</option>
                <option value="active">Đang hoạt động</option>
                <option value="inactive">Vô hiệu</option>
              </select>
            </div>
            <div className="col-6 col-md-2 d-flex gap-1">
              <button type="submit" className="btn btn-sm btn-primary flex-fill" disabled={loading}>
                <i className="bi bi-search me-1" />
                Lọc
              </button>
              <button type="button" className="btn btn-sm btn-outline-secondary" title="Xóa bộ lọc" onClick={clearFilters}>
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
                  <th className="ps-3">Cán bộ</th>
                  <th>Chức danh</th>
                  <th>Phòng / đơn vị</th>
                  <th>Chi bộ</th>
                  <th>Trạng thái</th>
                  <th>Đăng nhập cuối</th>
                  <th className="pe-3 text-end" />
                </tr>
              </thead>
              <tbody>
                {loading && !result ? (
                  <tr>
                    <td colSpan={7} className="text-center py-5 text-secondary">
                      <span className="spinner-border spinner-border-sm me-2" />
                      Đang tải…
                    </td>
                  </tr>
                ) : items.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="text-center py-5 text-secondary">
                      <i className="bi bi-inbox fs-3 d-block mb-2" />
                      Không có tài khoản phù hợp.
                    </td>
                  </tr>
                ) : (
                  items.map((account) => (
                    <tr key={account.id} style={{ opacity: loading ? 0.6 : 1 }}>
                      <td className="ps-3">
                        <Link href={`/admin/users/${account.id}`} className="fw-semibold text-decoration-none">
                          {account.fullName}
                        </Link>
                        <div className="small text-secondary">{account.username}</div>
                      </td>
                      <td className="small">{account.positionTitle || "—"}</td>
                      <td className="small">{account.departmentName || "—"}</td>
                      <td className="small">{account.partyCellName || "—"}</td>
                      <td>
                        <AccountStatusBadge account={account} />
                      </td>
                      <td className="small text-nowrap">{account.lastLoginAt ? formatDateTime(account.lastLoginAt) : "Chưa đăng nhập"}</td>
                      <td className="pe-3 text-end">
                        <Link href={`/admin/users/${account.id}`} className="btn btn-sm btn-outline-primary">
                          Chi tiết
                        </Link>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
          {result && (
            <Pagination
              page={result.page}
              totalPages={result.totalPages}
              totalCount={result.totalCount}
              pageSize={result.pageSize}
              disabled={loading}
              onPageChange={setPage}
            />
          )}
        </Card>
      </div>

      {creating && (
        <AccountFormModal
          departments={departments}
          partyCells={partyCells}
          saving={saving}
          onClose={() => setCreating(false)}
          onCreate={handleCreate}
        />
      )}
      {credential && <TemporaryPasswordModal credential={credential} onClose={() => setCredential(null)} />}
    </div>
  );
}
