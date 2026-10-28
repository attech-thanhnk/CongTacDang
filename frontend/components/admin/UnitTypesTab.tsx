"use client";

import React, { useState } from "react";
import { TopBarActions } from "@/components/common/TopBarActions";
import { useToast } from "@/contexts/ToastContext";
import { RowActions } from "@/components/common/RowActions";
import { Card } from "@/components/common";
import { catalogService, ORG_SIDE_LABELS, OrgSide, OrgUnitType } from "@/services/catalogService";
import { AdminModal } from "./AdminModal";
import { useFormDirty } from "./useFormDirty";
import { errorMessage, errorTitle } from "./adminUtils";

interface FormState {
  id?: string;
  name: string;
  side: OrgSide;
  sortOrder: string;
  isActive: boolean;
}

export interface UnitTypesTabProps {
  unitTypes: OrgUnitType[];
  loading: boolean;
  canManage: boolean;
  onChanged: () => Promise<void> | void;
}

/** Danh mục loại đơn vị (Đảng ủy, Chi bộ; Công ty, Phòng, Trung tâm…) — quản trị tự thêm/sửa. */
export function UnitTypesTab({ unitTypes, loading, canManage, onChanged }: UnitTypesTabProps) {
  const { toast, confirm } = useToast();
  const [form, setForm] = useState<FormState | null>(null);
  const formDirty = useFormDirty(form);
  const [saving, setSaving] = useState(false);

  const handleSave = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!form) return;
    const sortOrder = Number.parseInt(form.sortOrder || "0", 10);
    if (Number.isNaN(sortOrder) || sortOrder < 0) {
      toast.error("Thứ tự hiển thị phải là số nguyên không âm.");
      return;
    }
    const payload = { name: form.name.trim(), side: form.side, sortOrder, isActive: form.isActive };
    setSaving(true);
    try {
      if (form.id) await catalogService.updateUnitType(form.id, payload);
      else await catalogService.createUnitType(payload);
      toast.success(form.id ? "Đã cập nhật loại đơn vị." : "Đã thêm loại đơn vị.");
      setForm(null);
      await onChanged();
    } catch (err) {
      toast.error(errorMessage(err, "Lưu loại đơn vị không thành công."), errorTitle(err), 8000);
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = (type: OrgUnitType) =>
    confirm({
      title: "Xóa loại đơn vị",
      message: `Xóa loại "${type.name}"? Loại còn đơn vị thì không xóa được: hãy chuyển sang "Ngừng dùng".`,
      confirmText: "Xóa",
      isDanger: true,
      onConfirm: async () => {
        try {
          await catalogService.removeUnitType(type.id);
          toast.success(`Đã xóa loại "${type.name}".`);
          await onChanged();
        } catch (err) {
          toast.error(errorMessage(err, "Xóa loại đơn vị không thành công."), errorTitle(err), 10000);
        }
      },
    });

  return (
    <>
      <Card>
      {canManage && (
        <TopBarActions>
          <button
            type="button"
            className="btn btn-sm btn-primary"
            onClick={() => setForm({ name: "", side: "Administrative", sortOrder: "0", isActive: true })}
          >
            <i className="bi bi-plus-lg me-1" />
            Thêm loại đơn vị
          </button>
        </TopBarActions>
      )}
        <div className="table-responsive">
          <table className="table table-hover align-middle mb-0">
            <thead>
              <tr>
                <th className="ps-3">Tên loại</th>
                <th style={{ width: 180 }}>Bên</th>
                <th className="text-center" style={{ width: 90 }}>Thứ tự</th>
                <th className="text-center" style={{ width: 110 }}>Số đơn vị</th>
                <th style={{ width: 120 }}>Trạng thái</th>
                {canManage && <th className="pe-3 text-end" style={{ width: 130 }}>Thao tác</th>}
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={6} className="text-center py-4 text-secondary loading-delay">
                    <span className="spinner-border spinner-border-sm me-2" />
                    Đang tải…
                  </td>
                </tr>
              ) : unitTypes.length === 0 ? (
                <tr>
                  <td colSpan={6} className="text-center py-4 text-secondary">
                    Chưa có loại đơn vị nào.
                  </td>
                </tr>
              ) : (
                unitTypes.map((t) => (
                  <tr key={t.id} style={{ opacity: t.isActive ? 1 : 0.6 }}>
                    <td className="ps-3 fw-semibold text-dark">{t.name}</td>
                    <td>{ORG_SIDE_LABELS[t.side] ?? t.side}</td>
                    <td className="text-center">{t.sortOrder}</td>
                    <td className="text-center">{t.unitCount}</td>
                    <td>
                      {t.isActive ? (
                        <span className="tag t-ok">Đang dùng</span>
                      ) : (
                        <span className="status-badge tone-neutral">Ngừng dùng</span>
                      )}
                    </td>
                    {canManage && (
                      <td className="pe-3 text-end text-nowrap">
                        <RowActions
                          itemLabel={t.name}
                          primary={[{ key: "edit", label: "Sửa", icon: "bi-pencil", onClick: () => setForm({ id: t.id, name: t.name, side: t.side, sortOrder: String(t.sortOrder), isActive: t.isActive }) }]}
                          menu={[{ key: "delete", label: "Xóa", icon: "bi-trash", danger: true, onClick: () => handleDelete(t) }]}
                        />
                      </td>
                    )}
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </Card>

      {form && (
        <AdminModal title={form.id ? "Sửa loại đơn vị" : "Thêm loại đơn vị"} onClose={() => setForm(null)} closeDisabled={saving} maxWidth={460}
          footer={
            <>
            <button type="button" className="btn btn-outline-secondary" onClick={() => setForm(null)} disabled={saving}>
              Hủy
            </button>
            <button type="submit" form="unit-type-form" className="btn btn-primary" disabled={saving || !formDirty}>
              {saving && <span className="spinner-border spinner-border-sm me-1" />}
              Lưu
            </button>
            </>
          }
        >
          <form id="unit-type-form" onSubmit={handleSave}>
            <div className="mb-2">
              <label className="form-label">
                Tên loại <span className="text-danger">*</span>
              </label>
              <input
                className="form-control form-control-sm"
                value={form.name}
                maxLength={100}
                required
                placeholder="VD: Phòng, Trung tâm, Chi bộ"
                onChange={(e) => setForm({ ...form, name: e.target.value })}
              />
            </div>
            <div className="mb-2">
              <label className="form-label">Bên</label>
              <select className="form-select form-select-sm" value={form.side} onChange={(e) => setForm({ ...form, side: e.target.value as OrgSide })}>
                {(Object.keys(ORG_SIDE_LABELS) as OrgSide[]).map((s) => (
                  <option key={s} value={s}>
                    {ORG_SIDE_LABELS[s]}
                  </option>
                ))}
              </select>
            </div>
            <div className="row g-2">
              <div className="col-5">
                <label className="form-label">Thứ tự</label>
                <input
                  type="number"
                  min={0}
                  className="form-control form-control-sm"
                  value={form.sortOrder}
                  onChange={(e) => setForm({ ...form, sortOrder: e.target.value })}
                />
              </div>
              <div className="col-7 d-flex align-items-end">
                <div className="form-check mb-1">
                  <input
                    id="unit-type-active"
                    type="checkbox"
                    className="form-check-input"
                    checked={form.isActive}
                    onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
                  />
                  <label className="form-check-label small" htmlFor="unit-type-active">
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

export default UnitTypesTab;
