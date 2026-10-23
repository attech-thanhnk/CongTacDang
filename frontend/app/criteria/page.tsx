"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { Pagination } from "@/components/common/Pagination";
import { usePagination } from "@/components/common/usePagination";
import { Badge, BadgeTone } from "@/components/common/StatusBadge";
import { RowActions } from "@/components/common/RowActions";
import { AdminModal } from "@/components/admin/AdminModal";
import { NoAccess } from "@/components/admin/NoAccess";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { CriteriaSetListItem, criteriaService } from "@/services/criteriaService";
import { errorMessage, errorStatus } from "@/components/admin/adminUtils";

const STATUS_TONE: Record<string, BadgeTone> = {
  Draft: "warning",
  Published: "success",
  Archived: "neutral",
};

const EMPTY_FORM = { code: "", name: "", notes: "" };

/** Xem: criteria.manage hoặc period.manage (để chọn bộ cho kỳ); ghi: criteria.manage. */
export default function CriteriaSetsPage() {
  const { hasPermission } = useAuth();
  const canManage = hasPermission("criteria.manage");
  const canView = canManage || hasPermission("period.manage");
  const { toast, confirm } = useToast();
  const router = useRouter();
  const [sets, setSets] = useState<CriteriaSetListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState(EMPTY_FORM);
  const [showCreate, setShowCreate] = useState(false);

  const load = useCallback(async () => {
    if (!canView) return;
    try {
      setSets(await criteriaService.list());
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Không tải được danh sách bộ tiêu chí."));
    } finally {
      setLoading(false);
    }
  }, [canView, toast]);

  useEffect(() => {
    load();
  }, [load]);

  const run = async (work: () => Promise<unknown>, success: string) => {
    setBusy(true);
    try {
      await work();
      toast.success(success);
      await load();
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Không thực hiện được thao tác."));
      if (errorStatus(err) === 409) await load();
    } finally {
      setBusy(false);
    }
  };

  const create = async (event: React.FormEvent) => {
    event.preventDefault();
    setBusy(true);
    try {
      const created = await criteriaService.create({ ...form, code: form.code.trim(), name: form.name.trim() });
      toast.success("Đã tạo bản nháp.");
      router.push(`/criteria/${created.id}`);
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Không tạo được bộ tiêu chí."));
    } finally {
      setBusy(false);
    }
  };

  const clone = (set: CriteriaSetListItem) =>
    run(async () => {
      const created = await criteriaService.clone(set.id);
      router.push(`/criteria/${created.id}`);
    }, "Đã nhân bản thành bản nháp mới.");

  const paging = usePagination(sets, { storageKey: "criteriaSets" });

  if (!canView) {
    return <NoAccess title="Bộ tiêu chí và thang điểm" permissionName="Quản lý bộ tiêu chí" />;
  }

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Bộ tiêu chí và thang điểm"
        actions={
          canManage ? (
            <button type="button" className="btn btn-primary" onClick={() => { setForm(EMPTY_FORM); setShowCreate(true); }}>
              <i className="bi bi-plus-lg me-1" />Tạo bộ tiêu chí
            </button>
          ) : undefined
        }
      />
      <div className="page-body">
        <section className="card">
          <div className="card-body p-0">
            {loading ? (
              <div className="text-secondary p-3 loading-delay"><span className="spinner-border spinner-border-sm me-2" />Đang tải...</div>
            ) : sets.length === 0 ? (
              <EmptyState title="Chưa có bộ tiêu chí" />
            ) : (
              <>
              <div className="table-responsive">
                <table className="table table-hover align-middle mb-0 table-fit">
                  <thead>
                    <tr>
                      <th>Bộ tiêu chí</th><th>Trạng thái</th><th className="d-none d-md-table-cell">Nội dung</th><th className="d-none d-md-table-cell">Kỳ đang dùng</th><th className="text-end pe-3">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {paging.pageItems.map((s) => (
                      <tr key={s.id}>
                        <td>
                          <Link href={`/criteria/${s.id}`} className="fw-semibold">{s.name}</Link>
                          <div className="small text-secondary">{s.code}</div>
                        </td>
                        <td className="text-nowrap"><Badge tone={STATUS_TONE[s.status] ?? "neutral"}>{s.statusDisplayName}</Badge></td>
                        <td className="num d-none d-md-table-cell">{s.generalItemCount} tiêu chí con · {s.axisCount} trục · {s.weightFrameCount} khung</td>
                        <td className="d-none d-md-table-cell">{s.usedByPeriods.length > 0 ? s.usedByPeriods.join(", ") : "—"}</td>
                        <td className="text-end text-nowrap pe-3">
                          <RowActions
                            itemLabel={s.name}
                            disabled={busy}
                            primary={[
                              canManage && s.status === "Draft"
                                ? { key: "edit", label: "Sửa", icon: "bi-pencil", onClick: () => router.push(`/criteria/${s.id}`) }
                                : { key: "view", label: "Xem", icon: "bi-eye", onClick: () => router.push(`/criteria/${s.id}`) },
                            ]}
                            menu={[
                              { key: "clone", label: "Nhân bản", icon: "bi-copy", hidden: !canManage, onClick: () => clone(s) },
                              {
                                key: "archive", label: "Lưu trữ", icon: "bi-archive", danger: true, hidden: !canManage || s.status !== "Published",
                                onClick: () => confirm({ title: "Lưu trữ bộ tiêu chí", message: `Kỳ đã dùng "${s.name}" không bị ảnh hưởng; bộ không chọn được cho kỳ mới.`, confirmText: "Lưu trữ", isDanger: true, onConfirm: () => run(() => criteriaService.archive(s.id, s.version), "Đã lưu trữ bộ tiêu chí.") }),
                              },
                              {
                                key: "delete", label: "Xóa bản nháp", icon: "bi-trash", danger: true, hidden: !canManage || s.status !== "Draft",
                                onClick: () => confirm({ title: "Xóa bản nháp", message: `Xóa hẳn bản nháp "${s.name}"?`, confirmText: "Xóa", isDanger: true, onConfirm: () => run(() => criteriaService.remove(s.id, s.version), "Đã xóa bản nháp.") }),
                              },
                            ]}
                          />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <Pagination
                page={paging.page}
                pageSize={paging.pageSize}
                total={paging.total}
                onPageChange={paging.setPage}
                onPageSizeChange={paging.setPageSize}
              />
              </>
            )}
          </div>
        </section>
      </div>

      {showCreate && (
        <AdminModal
          title="Tạo bộ tiêu chí"
          onClose={() => setShowCreate(false)}
          closeDisabled={busy}
          footer={
            <>
              <button type="button" className="btn btn-outline-secondary" disabled={busy} onClick={() => setShowCreate(false)}>Hủy</button>
              <button type="submit" form="criteria-create-form" className="btn btn-primary" disabled={busy || !form.code.trim() || !form.name.trim()}>Tạo bản nháp</button>
            </>
          }
        >
          <form id="criteria-create-form" onSubmit={create} className="row g-3">
            <div className="col-12">
              <label className="form-label" htmlFor="cs-code">Mã bộ</label>
              <input id="cs-code" className="form-control" required maxLength={50} autoFocus placeholder="VD: TC-2027-V2" value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} />
            </div>
            <div className="col-12">
              <label className="form-label" htmlFor="cs-name">Tên bộ</label>
              <input id="cs-name" className="form-control" required maxLength={200} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
            </div>
            <div className="col-12">
              <label className="form-label" htmlFor="cs-notes">Ghi chú</label>
              <textarea id="cs-notes" className="form-control" rows={2} maxLength={4000} value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} />
            </div>
          </form>
        </AdminModal>
      )}
    </div>
  );
}
