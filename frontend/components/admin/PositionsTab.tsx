"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useToast } from "@/contexts/ToastContext";
import { Card } from "@/components/common";
import {
  APPROVAL_AUTHORITY_CODE_LABELS,
  ApprovalAuthorityCode,
  catalogService,
  Position,
  POSITION_SIDE_LABELS,
  PositionSide,
} from "@/services/catalogService";
import { AdminModal } from "./AdminModal";
import { errorMessage, errorTitle } from "./adminUtils";

interface FormState {
  id?: string;
  name: string;
  side: PositionSide;
  statCode: string;
  defaultApprovalAuthority: "" | ApprovalAuthorityCode;
  isLeadership: boolean;
  sortOrder: string;
  isActive: boolean;
}

const EMPTY_FORM: FormState = {
  name: "",
  side: "Administrative",
  statCode: "",
  defaultApprovalAuthority: "",
  isLeadership: false,
  sortOrder: "0",
  isActive: true,
};

/** Danh mục chức vụ: bên, mã chức danh Mẫu 15A/15B, thẩm quyền mặc định — không có chức vụ cố định trong code. */
export function PositionsTab({ canManage }: { canManage: boolean }) {
  const { toast, confirm } = useToast();
  const [positions, setPositions] = useState<Position[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [sideFilter, setSideFilter] = useState<"" | PositionSide>("");
  const [form, setForm] = useState<FormState | null>(null);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setPositions(await catalogService.listPositions());
    } catch (err) {
      toast.error(errorMessage(err, "Không tải được danh mục chức vụ."), errorTitle(err));
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

  const openEdit = (p: Position) =>
    setForm({
      id: p.id,
      name: p.name,
      side: p.side,
      statCode: p.statCode ?? "",
      defaultApprovalAuthority: p.defaultApprovalAuthority ?? "",
      isLeadership: p.isLeadership,
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
      isLeadership: form.isLeadership,
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
      toast.error(errorMessage(err, "Không lưu được chức vụ."), errorTitle(err), 8000);
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
          toast.error(errorMessage(err, "Không xóa được chức vụ."), errorTitle(err), 10000);
        }
      },
    });

  return (
    <>
      <div className="alert alert-info small py-2">
        Mã chức danh (M1–M26) dùng để thống kê Mẫu 15A/15B: mỗi cán bộ được tính một lần theo mã nhỏ nhất trong các chức vụ đang giữ.
        Thẩm quyền mặc định dùng để suy ra cấp quyết định xếp loại: có ít nhất một chức vụ &quot;cấp trên quyết định&quot; → cấp trên quyết định.
        Danh mục mặc định theo HD03 đang chờ nghiệp vụ xác nhận.
      </div>
      <div className="d-flex flex-wrap gap-2 mb-3">
        <input
          className="form-control form-control-sm"
          style={{ maxWidth: 300 }}
          placeholder="Tìm theo tên, mã chức danh…"
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
      <Card>
        <div className="table-responsive">
          <table className="table table-hover align-middle mb-0" style={{ fontSize: 13 }}>
            <thead style={{ background: "var(--bg-base)" }}>
              <tr>
                <th className="ps-3">Chức vụ</th>
                <th style={{ width: 120 }}>Bên</th>
                <th style={{ width: 90 }}>Mã</th>
                <th style={{ width: 210 }}>Thẩm quyền mặc định</th>
                <th className="text-center" style={{ width: 90 }}>Đang giữ</th>
                <th style={{ width: 110 }}>Trạng thái</th>
                {canManage && <th className="pe-3 text-end" style={{ width: 100 }}>Thao tác</th>}
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={7} className="text-center py-4 text-secondary">
                    <span className="spinner-border spinner-border-sm me-2" />
                    Đang tải…
                  </td>
                </tr>
              ) : filtered.length === 0 ? (
                <tr>
                  <td colSpan={7} className="text-center py-4 text-secondary">
                    Không có chức vụ phù hợp.
                  </td>
                </tr>
              ) : (
                filtered.map((p) => (
                  <tr key={p.id} style={{ opacity: p.isActive ? 1 : 0.6 }}>
                    <td className="ps-3">
                      <span className="fw-semibold text-dark">{p.name}</span>
                      {p.isLeadership && <span className="badge bg-light text-secondary border ms-2">Lãnh đạo, quản lý</span>}
                    </td>
                    <td>{POSITION_SIDE_LABELS[p.side] ?? p.side}</td>
                    <td>{p.statCode ? <code>{p.statCode}</code> : "—"}</td>
                    <td className="small">
                      {p.defaultApprovalAuthority ? APPROVAL_AUTHORITY_CODE_LABELS[p.defaultApprovalAuthority] : <span className="text-secondary">Không xác định</span>}
                    </td>
                    <td className="text-center">{p.holderCount}</td>
                    <td>
                      {p.isActive ? (
                        <span className="badge bg-success-subtle text-success border border-success-subtle">Đang dùng</span>
                      ) : (
                        <span className="badge bg-secondary-subtle text-secondary border">Ngừng dùng</span>
                      )}
                    </td>
                    {canManage && (
                      <td className="pe-3 text-end text-nowrap">
                        <button type="button" className="btn btn-sm btn-link p-1" title="Sửa" onClick={() => openEdit(p)}>
                          <i className="bi bi-pencil-square" />
                        </button>
                        <button type="button" className="btn btn-sm btn-link p-1 text-danger" title="Xóa" onClick={() => handleDelete(p)}>
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
        <AdminModal title={form.id ? "Sửa chức vụ" : "Thêm chức vụ"} onClose={() => setForm(null)} closeDisabled={saving} maxWidth={520}>
          <form onSubmit={handleSave}>
            <div className="row g-2">
              <div className="col-12">
                <label className="form-label small fw-semibold mb-1">
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
                <label className="form-label small fw-semibold mb-1">Bên</label>
                <select className="form-select form-select-sm" value={form.side} onChange={(e) => setForm({ ...form, side: e.target.value as PositionSide })}>
                  {(Object.keys(POSITION_SIDE_LABELS) as PositionSide[]).map((s) => (
                    <option key={s} value={s}>
                      {POSITION_SIDE_LABELS[s]}
                    </option>
                  ))}
                </select>
              </div>
              <div className="col-6">
                <label className="form-label small fw-semibold mb-1">Mã chức danh (15A/15B)</label>
                <input
                  className="form-control form-control-sm"
                  value={form.statCode}
                  maxLength={5}
                  placeholder="VD: M26 (để trống nếu không thống kê)"
                  onChange={(e) => setForm({ ...form, statCode: e.target.value.toUpperCase() })}
                />
              </div>
              <div className="col-12">
                <label className="form-label small fw-semibold mb-1">Thẩm quyền quyết định xếp loại mặc định</label>
                <select
                  className="form-select form-select-sm"
                  value={form.defaultApprovalAuthority}
                  onChange={(e) => setForm({ ...form, defaultApprovalAuthority: e.target.value as "" | ApprovalAuthorityCode })}
                >
                  <option value="">Không xác định (không ảnh hưởng suy ra)</option>
                  {(Object.keys(APPROVAL_AUTHORITY_CODE_LABELS) as ApprovalAuthorityCode[]).map((a) => (
                    <option key={a} value={a}>
                      {APPROVAL_AUTHORITY_CODE_LABELS[a]}
                    </option>
                  ))}
                </select>
              </div>
              <div className="col-4">
                <label className="form-label small fw-semibold mb-1">Thứ tự</label>
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

export default PositionsTab;
