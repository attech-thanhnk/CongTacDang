"use client";

import React, { useState } from "react";
import { useToast } from "@/contexts/ToastContext";
import { Card } from "@/components/common";
import { catalogService, ORG_SIDE_LABELS, OrgSide, OrgUnitType } from "@/services/catalogService";
import { AdminModal } from "./AdminModal";
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
      toast.error(errorMessage(err, "Không lưu được loại đơn vị."), errorTitle(err), 8000);
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = (type: OrgUnitType) =>
    confirm({
      title: "Xóa loại đơn vị",
      message: `Xóa loại "${type.name}"? Không xóa được khi còn đơn vị thuộc loại này — khi đó hãy chuyển sang "Ngừng dùng".`,
      confirmText: "Xóa",
      isDanger: true,
      onConfirm: async () => {
        try {
          await catalogService.removeUnitType(type.id);
          toast.success(`Đã xóa loại "${type.name}".`);
          await onChanged();
        } catch (err) {
          toast.error(errorMessage(err, "Không xóa được loại đơn vị."), errorTitle(err), 10000);
        }
      },
    });

  return (
    <>
      {canManage && (
        <div className="d-flex mb-3">
          <button
            type="button"
            className="btn btn-sm btn-primary ms-auto"
            onClick={() => setForm({ name: "", side: "Administrative", sortOrder: "0", isActive: true })}
          >
            <i className="bi bi-plus-lg me-1" />
            Thêm loại đơn vị
          </button>
        </div>
      )}
      <Card>
        <div className="table-responsive">
          <table className="table table-hover align-middle mb-0" style={{ fontSize: 13 }}>
            <thead style={{ background: "var(--bg-base)" }}>
              <tr>
                <th className="ps-3">Tên loại</th>
                <th style={{ width: 180 }}>Bên</th>
                <th className="text-center" style={{ width: 90 }}>Thứ tự</th>
                <th className="text-center" style={{ width: 110 }}>Số đơn vị</th>
                <th style={{ width: 120 }}>Trạng thái</th>
                {canManage && <th className="pe-3 text-end" style={{ width: 100 }}>Thao tác</th>}
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={6} className="text-center py-4 text-secondary">
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
                        <span className="badge bg-success-subtle text-success border border-success-subtle">Đang dùng</span>
                      ) : (
                        <span className="badge bg-secondary-subtle text-secondary border">Ngừng dùng</span>
                      )}
                    </td>
                    {canManage && (
                      <td className="pe-3 text-end text-nowrap">
                        <button
                          type="button"
                          className="btn btn-sm btn-link p-1"
                          title="Sửa"
                          onClick={() => setForm({ id: t.id, name: t.name, side: t.side, sortOrder: String(t.sortOrder), isActive: t.isActive })}
                        >
                          <i className="bi bi-pencil-square" />
                        </button>
                        <button type="button" className="btn btn-sm btn-link p-1 text-danger" title="Xóa" onClick={() => handleDelete(t)}>
                          <i className="bi bi-trash" />
                        </button>
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
        <AdminModal title={form.id ? "Sửa loại đơn vị" : "Thêm loại đơn vị"} onClose={() => setForm(null)} closeDisabled={saving} maxWidth={460}>
          <form onSubmit={handleSave}>
            <div className="mb-2">
              <label className="form-label small fw-semibold mb-1">
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
              <label className="form-label small fw-semibold mb-1">Bên</label>
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
                <label className="form-label small fw-semibold mb-1">Thứ tự</label>
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
            <div className="d-flex justify-content-end gap-2 mt-3">
              <button type="button" className="btn btn-sm btn-light" onClick={() => setForm(null)} disabled={saving}>
                Hủy
              </button>
              <button type="submit" className="btn btn-sm btn-primary" disabled={saving}>
                {saving && <span className="spinner-border spinner-border-sm me-1" />}
                Lưu
              </button>
            </div>
          </form>
        </AdminModal>
      )}
    </>
  );
}

export default UnitTypesTab;
