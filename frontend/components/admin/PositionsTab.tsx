"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useToast } from "@/contexts/ToastContext";
import { RowActions } from "@/components/common/RowActions";
import { Card, Pagination, usePagination } from "@/components/common";
import {
  APPROVAL_AUTHORITY_CODE_LABELS,
  ApprovalAuthorityCode,
  catalogService,
  Position,
  POSITION_SIDE_LABELS,
  PositionSide,
  WorkflowProfileOption,
} from "@/services/catalogService";
import { FormLayout, formLayoutService } from "@/services/formLayoutService";
import { AdminModal } from "./AdminModal";
import { useFormDirty } from "./useFormDirty";
import { errorMessage, errorTitle } from "./adminUtils";

interface FormState {
  id?: string;
  name: string;
  side: PositionSide;
  statCode: string;
  defaultApprovalAuthority: "" | ApprovalAuthorityCode;
  defaultWorkflowProfileCode: string;
  isLeadership: boolean;
  isHead: boolean;
  formLayoutId: string;
  sortOrder: string;
  isActive: boolean;
}

const EMPTY_FORM: FormState = {
  name: "",
  side: "Administrative",
  statCode: "",
  defaultApprovalAuthority: "",
  defaultWorkflowProfileCode: "",
  isLeadership: false,
  isHead: false,
  formLayoutId: "",
  sortOrder: "0",
  isActive: true,
};

/** Danh mục chức vụ: bên, mã chức danh Mẫu 15A/15B, thẩm quyền mặc định — không có chức vụ cố định trong code. */
export function PositionsTab({ canManage }: { canManage: boolean }) {
  const { toast, confirm } = useToast();
  const [positions, setPositions] = useState<Position[]>([]);
  const [layouts, setLayouts] = useState<FormLayout[]>([]);
  const [profiles, setProfiles] = useState<WorkflowProfileOption[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [sideFilter, setSideFilter] = useState<"" | PositionSide>("");
  const [form, setForm] = useState<FormState | null>(null);
  const formDirty = useFormDirty(form);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [items, layoutItems, profileItems] = await Promise.all([
        catalogService.listPositions(),
        formLayoutService.list(),
        catalogService.listWorkflowProfileOptions(),
      ]);
      setPositions(items);
      setLayouts(layoutItems);
      setProfiles(profileItems);
    } catch (err) {
      toast.error(errorMessage(err, "Tải danh mục chức vụ không thành công."), errorTitle(err));
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase();
    return positions.filter(
      (p) =>
        (!sideFilter || p.side === sideFilter) &&
        (!term || p.name.toLowerCase().includes(term) || (p.statCode || "").toLowerCase().includes(term))
    );
  }, [positions, search, sideFilter]);
  const paging = usePagination(filtered, { storageKey: "catalog.positions", resetKeys: [search, sideFilter] });

  const profileName = (code: string) => profiles.find((x) => x.code === code)?.name ?? code;

  const openEdit = (p: Position) =>
    setForm({
      id: p.id,
      name: p.name,
      side: p.side,
      statCode: p.statCode ?? "",
      defaultApprovalAuthority: p.defaultApprovalAuthority ?? "",
      defaultWorkflowProfileCode: p.defaultWorkflowProfileCode ?? "",
      isLeadership: p.isLeadership,
      isHead: p.isHead,
      formLayoutId: p.formLayoutId ?? "",
      sortOrder: String(p.sortOrder),
      isActive: p.isActive,
    });

  const handleSave = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!form) return;
    const sortOrder = Number.parseInt(form.sortOrder || "0", 10);
    if (Number.isNaN(sortOrder) || sortOrder < 0) {
      toast.error("Thứ tự hiển thị phải là số nguyên không âm.");
      return;
    }
    const payload = {
      name: form.name.trim(),
      side: form.side,
      statCode: form.statCode.trim(),
      defaultApprovalAuthority: form.defaultApprovalAuthority,
      defaultWorkflowProfileCode: form.defaultWorkflowProfileCode,
      isLeadership: form.isLeadership,
      isHead: form.isHead,
      formLayoutId: form.formLayoutId,
      sortOrder,
      isActive: form.isActive,
    };
    setSaving(true);
    try {
      if (form.id) await catalogService.updatePosition(form.id, payload);
      else await catalogService.createPosition(payload);
      toast.success(form.id ? `Đã cập nhật chức vụ "${payload.name}".` : `Đã thêm chức vụ "${payload.name}".`);
      setForm(null);
      await load();
    } catch (err) {
      toast.error(errorMessage(err, "Lưu chức vụ không thành công."), errorTitle(err), 8000);
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = (p: Position) =>
    confirm({
      title: "Xóa chức vụ",
      message: `Xóa chức vụ "${p.name}"? Chức vụ đã từng gán cho cán bộ không xóa được (cần giữ lịch sử) — khi đó hãy chuyển sang "Ngừng dùng".`,
      confirmText: "Xóa",
      isDanger: true,
      onConfirm: async () => {
        try {
          await catalogService.removePosition(p.id);
          toast.success(`Đã xóa chức vụ "${p.name}".`);
          await load();
        } catch (err) {
          toast.error(errorMessage(err, "Xóa chức vụ không thành công."), errorTitle(err), 10000);
        }
      },
    });

  return (
    <>
      <Card>
      <div className="card-header d-flex flex-wrap align-items-center gap-2">
        <input
          className="form-control form-control-sm"
          style={{ maxWidth: 300 }}
          placeholder="Tìm kiếm"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <select
          className="form-select form-select-sm"
          style={{ maxWidth: 180 }}
          value={sideFilter}
          onChange={(e) => setSideFilter(e.target.value as "" | PositionSide)}
        >
          <option value="">Mọi bên</option>
          {(Object.keys(POSITION_SIDE_LABELS) as PositionSide[]).map((s) => (
            <option key={s} value={s}>
              {POSITION_SIDE_LABELS[s]}
            </option>
          ))}
        </select>
        {canManage && (
          <button type="button" className="btn btn-sm btn-primary ms-auto" onClick={() => setForm({ ...EMPTY_FORM })}>
            <i className="bi bi-plus-lg me-1" />
            Thêm chức vụ
          </button>
        )}
      </div>
        <div className="table-responsive">
          <table className="table table-hover align-middle mb-0">
            <thead>
              <tr>
                <th className="ps-3">Chức vụ</th>
                <th style={{ width: 120 }}>Bên</th>
                <th style={{ width: 90 }}>Mã</th>
                <th style={{ width: 210 }}>Thẩm quyền mặc định</th>
                <th style={{ width: 170 }}>Kiểu trình bày</th>
                <th className="text-center" style={{ width: 90 }}>Đang giữ</th>
                <th style={{ width: 110 }}>Trạng thái</th>
                {canManage && <th className="pe-3 text-end" style={{ width: 130 }}>Thao tác</th>}
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={8} className="text-center py-4 text-secondary loading-delay">
                    <span className="spinner-border spinner-border-sm me-2" />
                    Đang tải…
                  </td>
                </tr>
              ) : filtered.length === 0 ? (
                <tr>
                  <td colSpan={8} className="text-center py-4 text-secondary">
                    Không có chức vụ phù hợp.
                  </td>
                </tr>
              ) : (
                paging.pageItems.map((p) => (
                  <tr key={p.id} style={{ opacity: p.isActive ? 1 : 0.6 }}>
                    <td className="ps-3">
                      <span className="fw-semibold text-dark">{p.name}</span>
                      {p.isLeadership && <span className="status-badge tone-neutral ms-2">Lãnh đạo, quản lý</span>}
                      {p.isHead && <span className="status-badge tone-neutral ms-2">Người đứng đầu</span>}
                    </td>
                    <td>{POSITION_SIDE_LABELS[p.side] ?? p.side}</td>
                    <td>{p.statCode ? <code>{p.statCode}</code> : "—"}</td>
                    <td className="small">
                      {p.defaultApprovalAuthority ? APPROVAL_AUTHORITY_CODE_LABELS[p.defaultApprovalAuthority] : <span className="text-secondary">Không xác định</span>}
                      {p.defaultWorkflowProfileCode && (
                        <div className="text-secondary">Diện quyết định: {profileName(p.defaultWorkflowProfileCode)}</div>
                      )}
                    </td>
                    <td className="small">{p.formLayoutName ?? <span className="text-secondary">Mặc định</span>}</td>
                    <td className="text-center">{p.holderCount}</td>
                    <td>
                      {p.isActive ? (
                        <span className="tag t-ok">Đang dùng</span>
                      ) : (
                        <span className="status-badge tone-neutral">Ngừng dùng</span>
                      )}
                    </td>
                    {canManage && (
                      <td className="pe-3 text-end text-nowrap">
                        <RowActions itemLabel={p.name} primary={[{ key: "edit", label: "Sửa", icon: "bi-pencil", onClick: () => openEdit(p) }]} menu={[{ key: "delete", label: "Xóa", icon: "bi-trash", danger: true, onClick: () => handleDelete(p) }]} />
                      </td>
                    )}
                  </tr>
                ))
              )}
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
      </Card>

      {form && (
        <AdminModal title={form.id ? "Sửa chức vụ" : "Thêm chức vụ"} onClose={() => setForm(null)} closeDisabled={saving} maxWidth={520}
          footer={
            <>
            <button type="button" className="btn btn-outline-secondary" onClick={() => setForm(null)} disabled={saving}>
              Hủy
            </button>
            <button type="submit" form="position-form" className="btn btn-primary" disabled={saving || !formDirty}>
              {saving && <span className="spinner-border spinner-border-sm me-1" />}
              Lưu
            </button>
            </>
          }
        >
          <form id="position-form" onSubmit={handleSave}>
            <div className="row g-2">
              <div className="col-12">
                <label className="form-label">
                  Tên chức vụ <span className="text-danger">*</span>
                </label>
                <input
                  className="form-control form-control-sm"
                  value={form.name}
                  maxLength={200}
                  required
                  onChange={(e) => setForm({ ...form, name: e.target.value })}
                />
              </div>
              <div className="col-6">
                <label className="form-label">Bên</label>
                <select className="form-select form-select-sm" value={form.side} onChange={(e) => setForm({ ...form, side: e.target.value as PositionSide })}>
                  {(Object.keys(POSITION_SIDE_LABELS) as PositionSide[]).map((s) => (
                    <option key={s} value={s}>
                      {POSITION_SIDE_LABELS[s]}
                    </option>
                  ))}
                </select>
              </div>
              <div className="col-6">
                <label className="form-label">Mã chức danh</label>
                <input
                  className="form-control form-control-sm"
                  value={form.statCode}
                  maxLength={5}
                  placeholder="VD: M26 (để trống nếu không thống kê)"
                  onChange={(e) => setForm({ ...form, statCode: e.target.value.toUpperCase() })}
                />
              </div>
              <div className="col-12">
                <label className="form-label">Thẩm quyền quyết định xếp loại mặc định</label>
                <select
                  className="form-select form-select-sm"
                  value={form.defaultApprovalAuthority}
                  onChange={(e) => setForm({ ...form, defaultApprovalAuthority: e.target.value as "" | ApprovalAuthorityCode })}
                >
                  <option value="">Không xác định</option>
                  {(Object.keys(APPROVAL_AUTHORITY_CODE_LABELS) as ApprovalAuthorityCode[]).map((a) => (
                    <option key={a} value={a}>
                      {APPROVAL_AUTHORITY_CODE_LABELS[a]}
                    </option>
                  ))}
                </select>
              </div>
              <div className="col-12">
                <label className="form-label" htmlFor="pos-workflow-profile">Diện quyết định khi thêm vào kỳ</label>
                <select
                  id="pos-workflow-profile"
                  className="form-select form-select-sm"
                  value={form.defaultWorkflowProfileCode}
                  onChange={(e) => setForm({ ...form, defaultWorkflowProfileCode: e.target.value })}
                >
                  <option value="">Theo thẩm quyền quyết định</option>
                  {profiles.map((x) => (
                    <option key={x.code} value={x.code}>
                      {x.name}
                    </option>
                  ))}
                  {form.defaultWorkflowProfileCode && !profiles.some((x) => x.code === form.defaultWorkflowProfileCode) && (
                    <option value={form.defaultWorkflowProfileCode}>{form.defaultWorkflowProfileCode}</option>
                  )}
                </select>
              </div>
              <div className="col-12">
                <label className="form-label">Kiểu trình bày biểu mẫu tự đánh giá</label>
                <select className="form-select form-select-sm" value={form.formLayoutId} onChange={(e) => setForm({ ...form, formLayoutId: e.target.value })}>
                  <option value="">Kiểu mặc định</option>
                  {layouts.map((l) => (
                    <option key={l.id} value={l.id}>
                      {l.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="col-4">
                <label className="form-label">Thứ tự</label>
                <input
                  type="number"
                  min={0}
                  className="form-control form-control-sm"
                  value={form.sortOrder}
                  onChange={(e) => setForm({ ...form, sortOrder: e.target.value })}
                />
              </div>
              <div className="col-8 d-flex flex-column justify-content-end">
                <div className="form-check">
                  <input
                    id="position-leadership"
                    type="checkbox"
                    className="form-check-input"
                    checked={form.isLeadership}
                    onChange={(e) => setForm({ ...form, isLeadership: e.target.checked })}
                  />
                  <label className="form-check-label small" htmlFor="position-leadership">
                    Chức vụ lãnh đạo, quản lý
                  </label>
                </div>
                <div className="form-check">
                  <input
                    id="position-head"
                    type="checkbox"
                    className="form-check-input"
                    checked={form.isHead}
                    onChange={(e) => setForm({ ...form, isHead: e.target.checked })}
                  />
                  <label className="form-check-label small" htmlFor="position-head">
                    Người đứng đầu (không tính vào trần tỷ lệ Hoàn thành xuất sắc)
                  </label>
                </div>
                <div className="form-check">
                  <input
                    id="position-active"
                    type="checkbox"
                    className="form-check-input"
                    checked={form.isActive}
                    onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
                  />
                  <label className="form-check-label small" htmlFor="position-active">
                    Đang dùng
                  </label>
                </div>
              </div>
            </div>
          </form>
        </AdminModal>
      )}
    </>
  );
}

export default PositionsTab;
