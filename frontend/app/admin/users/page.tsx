"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { Card, PageHeader, Pagination, usePageSize } from "@/components/common";
import { AccountFormModal } from "@/components/admin/AccountFormModal";
import { AccountStatusBadge } from "@/components/admin/AccountStatusBadge";
import { NoAccess } from "@/components/admin/NoAccess";
import { TemporaryCredential, TemporaryPasswordModal } from "@/components/admin/TemporaryPasswordModal";
import { errorMessage, errorTitle, formatDateTime } from "@/components/admin/adminUtils";
import { useCatalogOptions } from "@/components/admin/useCatalogOptions";
import { AccountListItem, CreateAccountPayload, PagedResult, userService } from "@/services/userService";
import { treeLabel } from "@/services/catalogService";
import styles from "./users.module.css";

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
  const [pageSize, setPageSize] = usePageSize("admin.users");
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
        pageSize,
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
  }, [page, pageSize, filters]);

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

  if (!canRead) return <NoAccess title="Người dùng" permissionName="Xem tài khoản, hồ sơ cán bộ" />;

  const items = result?.items ?? [];

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Người dùng"
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
        <Card>
          <div className="card-header">
            <form className={`row g-2 align-items-end ${styles.filters}`} onSubmit={applyFilters}>
              <div className="col-12 col-md-6 col-xl-4">
                <label className="form-label" htmlFor="users-q">
                  Tìm kiếm
                </label>
                <input
                  id="users-q"
                  className="form-control form-control-sm"
                  placeholder="Tên đăng nhập, họ tên, email, số thẻ Đảng…"
                  value={draft.q}
                  onChange={(e) => setDraft({ ...draft, q: e.target.value })}
                />
              </div>
              <div className="col-12 col-sm-6 col-md-3 col-xl-2">
                <label className="form-label" htmlFor="users-dept">
                  Đơn vị
                </label>
                <select
                  id="users-dept"
                  className="form-select form-select-sm"
                  value={draft.departmentId}
                  onChange={(e) => setDraft({ ...draft, departmentId: e.target.value })}
                >
                  <option value="">Tất cả</option>
                  {departments.map((d) => (
                    <option key={d.id} value={d.id}>
                      {treeLabel(d)}
                    </option>
                  ))}
                </select>
              </div>
              <div className="col-12 col-sm-6 col-md-3 col-xl-2">
                <label className="form-label" htmlFor="users-cell">
                  Tổ chức Đảng
                </label>
                <select
                  id="users-cell"
                  className="form-select form-select-sm"
                  value={draft.partyCellId}
                  onChange={(e) => setDraft({ ...draft, partyCellId: e.target.value })}
                >
                  <option value="">Tất cả</option>
                  {partyCells.map((c) => (
                    <option key={c.id} value={c.id}>
                      {treeLabel(c)}
                    </option>
                  ))}
                </select>
              </div>
              <div className="col-6 col-md-3 col-xl-2">
                <label className="form-label" htmlFor="users-status">
                  Trạng thái
                </label>
                <select
                  id="users-status"
                  className="form-select form-select-sm"
                  value={draft.status}
                  onChange={(e) => setDraft({ ...draft, status: e.target.value as StatusFilter })}
                >
                  <option value="">Tất cả</option>
                  <option value="active">Đang hoạt động</option>
                  <option value="inactive">Vô hiệu</option>
                </select>
              </div>
              <div className="col-6 col-md-3 col-xl-2 d-flex gap-1">
                <button type="submit" className="btn btn-sm btn-primary flex-fill" disabled={loading}>
                  <i className="bi bi-search me-1" />
                  Lọc
                </button>
                <button
                  type="button"
                  className={`btn btn-sm btn-outline-secondary ${styles.clearBtn}`}
                  title="Xóa bộ lọc"
                  aria-label="Xóa bộ lọc"
                  onClick={clearFilters}
                >
                  <i className="bi bi-x-lg" />
                </button>
              </div>
            </form>
          </div>

          {/* Màn rộng: bảng. */}
          <div className="table-responsive d-none d-md-block">
            <table className={`table table-hover align-middle mb-0 ${styles.table}`}>
              <thead>
                <tr>
                  <th className={styles.colName}>Cán bộ</th>
                  <th className={styles.colPosition}>Chức danh</th>
                  <th className={styles.colUnit}>Đơn vị</th>
                  <th className={styles.colUnit}>Tổ chức Đảng</th>
                  <th className={styles.colStatus}>Trạng thái</th>
                  <th className={styles.colLogin}>Đăng nhập cuối</th>
                  <th className={`text-end ${styles.colAction}`}>
                    <span className="visually-hidden">Thao tác</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {loading && !result ? (
                  <tr>
                    <td colSpan={7} className="text-center py-5 text-secondary loading-delay">
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
                      <td className={styles.colName}>
                        <Link href={`/admin/users/${account.id}`} className="fw-semibold text-decoration-none">
                          {account.fullName}
                        </Link>
                        <div className={styles.username}>{account.username}</div>
                      </td>
                      <td>{account.positionTitle || "—"}</td>
                      <td>{account.departmentName || "—"}</td>
                      <td>{account.partyCellName || "—"}</td>
                      <td className={styles.colStatus}>
                        <AccountStatusBadge account={account} compact />
                      </td>
                      <td className={styles.colLogin}>
                        {account.lastLoginAt ? formatDateTime(account.lastLoginAt) : <span className="text-secondary">Chưa đăng nhập</span>}
                      </td>
                      <td className={`text-end ${styles.colAction}`}>
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

          {/* Điện thoại: mỗi tài khoản một thẻ để tên và đơn vị không bị bóp. */}
          <div className="d-md-none">
            {loading && !result ? (
              <div className={`${styles.empty} loading-delay`}>
                <span className="spinner-border spinner-border-sm me-2" />
                Đang tải…
              </div>
            ) : items.length === 0 ? (
              <div className={styles.empty}>
                <i className="bi bi-inbox fs-3 d-block mb-2" />
                Không có tài khoản phù hợp.
              </div>
            ) : (
              <ul className={styles.cards} style={{ opacity: loading ? 0.6 : 1 }}>
                {items.map((account) => (
                  <li key={account.id} className={styles.cardItem}>
                    <div className={styles.cardHead}>
                      <div className={styles.cardName}>
                        <Link href={`/admin/users/${account.id}`} className="text-decoration-none">
                          {account.fullName}
                        </Link>
                        <div className={styles.username}>{account.username}</div>
                      </div>
                      <AccountStatusBadge account={account} compact />
                    </div>
                    {(account.positionTitle || account.departmentName) && (
                      <div className={styles.cardMeta}>
                        <i className="bi bi-briefcase" aria-hidden="true" />
                        {[account.positionTitle, account.departmentName].filter(Boolean).join(" · ")}
                      </div>
                    )}
                    {account.partyCellName && (
                      <div className={styles.cardMeta}>
                        <i className="bi bi-flag" aria-hidden="true" />
                        {account.partyCellName}
                      </div>
                    )}
                    <div className={styles.cardFoot}>
                      <span className={styles.lastLogin}>
                        {account.lastLoginAt ? `Đăng nhập ${formatDateTime(account.lastLoginAt)}` : "Chưa đăng nhập"}
                      </span>
                      <Link href={`/admin/users/${account.id}`} className="btn btn-sm btn-outline-primary">
                        Chi tiết
                      </Link>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>
          {result && (
            <Pagination
              page={result.page}
              pageSize={result.pageSize}
              total={result.totalCount}
              disabled={loading}
              onPageChange={setPage}
              onPageSizeChange={(size) => {
                setPageSize(size);
                setPage(1);
              }}
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
