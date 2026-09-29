"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useToast } from "@/contexts/ToastContext";
import { Card } from "@/components/common";
import {
  CatalogItem,
  CatalogKind,
  catalogService,
  OrgSide,
  OrgUnitType,
  SaveCatalogItemPayload,
  subtreeIds,
  treeLabel,
} from "@/services/catalogService";
import { EMPTY_GUID } from "@/services/userService";
import { AdminModal } from "./AdminModal";
import { errorMessage, errorTitle } from "./adminUtils";

interface FormState {
  id?: string;
  code: string;
  name: string;
  description: string;
  sortOrder: string;
  isActive: boolean;
  parentId: string;
  unitTypeId: string;
}

export interface OrgTreeTabProps {
  kind: CatalogKind;
  /** Tên đơn vị hiển thị: "đơn vị" / "tổ chức Đảng". */
  unit: string;
  canManage: boolean;
  unitTypes: OrgUnitType[];
}

/** Cây đơn vị một bên (đơn vị chính quyền / tổ chức Đảng): xem, thêm gốc/con, sửa (kể cả đổi cha), ngừng hoạt động, xóa. */
export function OrgTreeTab({ kind, unit, canManage, unitTypes }: OrgTreeTabProps) {
  const { toast, confirm } = useToast();
  const side: OrgSide = kind === "branches" ? "Party" : "Administrative";
  const [items, setItems] = useState<CatalogItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set());
  const [form, setForm] = useState<FormState | null>(null);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await catalogService.list(kind));
    } catch (err) {
      toast.error(errorMessage(err, "Không tải được danh mục."), errorTitle(err));
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [kind]);

  useEffect(() => {
    load();
  }, [load]);

  const typesOfSide = useMemo(() => unitTypes.filter((t) => t.side === side), [unitTypes, side]);

  const visible = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (term) {
      return items.filter(
        (i) => i.code.toLowerCase().includes(term) || i.name.toLowerCase().includes(term) || (i.description || "").toLowerCase().includes(term)
      );
    }
    // Ẩn nút có tổ tiên đang thu gọn.
    return items.filter((i) => !Array.from(collapsed).some((id) => id !== i.id && i.path.includes(`/${id}/`)));
  }, [items, search, collapsed]);

  const toggle = (id: string) =>
    setCollapsed((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const openCreate = (parent?: CatalogItem) =>
    setForm({ code: "", name: "", description: "", sortOrder: "0", isActive: true, parentId: parent?.id ?? "", unitTypeId: "" });

  const openEdit = (item: CatalogItem) =>
    setForm({
      id: item.id,
      code: item.code,
      name: item.name,
      description: item.description || "",
      sortOrder: String(item.sortOrder ?? 0),
      isActive: item.isActive,
      parentId: item.parentId ?? "",
      unitTypeId: item.unitTypeId ?? "",
    });

  // Ô chọn cha: không được chọn chính nút đang sửa và con cháu của nó (tránh vòng).
  const parentOptions = useMemo(() => {
    if (!form?.id) return items;
    const excluded = subtreeIds(items, form.id);
    return items.filter((i) => !excluded.has(i.id));
  }, [items, form?.id]);

  const handleSave = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!form) return;
    const sortOrder = Number.parseInt(form.sortOrder || "0", 10);
    if (Number.isNaN(sortOrder) || sortOrder < 0) {
      toast.error("Thứ tự hiển thị phải là số nguyên không âm.");
      return;
    }
    const payload: SaveCatalogItemPayload = {
      code: form.code.trim(),
      name: form.name.trim(),
      description: form.description.trim(),
      sortOrder,
      isActive: form.isActive,
      parentId: form.parentId || (form.id ? EMPTY_GUID : undefined),
      unitTypeId: form.unitTypeId || (form.id ? EMPTY_GUID : undefined),
    };
    setSaving(true);
    try {
      if (form.id) {
        await catalogService.update(kind, form.id, payload);
        toast.success(`Đã cập nhật ${unit} "${payload.name}".`);
      } else {
        await catalogService.create(kind, payload);
        toast.success(`Đã thêm ${unit} "${payload.name}".`);
      }
      setForm(null);
      await load();
    } catch (err) {
      toast.error(errorMessage(err, "Không lưu được."), errorTitle(err), 8000);
    } finally {
      setSaving(false);
    }
  };

  const toggleActive = async (item: CatalogItem) => {
    try {
      await catalogService.update(kind, item.id, { isActive: !item.isActive });
      toast.success(item.isActive ? `Đã ngừng hoạt động "${item.name}".` : `Đã cho "${item.name}" hoạt động trở lại.`);
      await load();
    } catch (err) {
      toast.error(errorMessage(err, "Không cập nhật được trạng thái."), errorTitle(err), 8000);
    }
  };

  const handleDelete = (item: CatalogItem) =>
    confirm({
      title: `Xóa ${unit}`,
      message: `Xóa ${unit} "${item.name}" (${item.code})? Không thể xóa khi còn ${unit} cấp dưới, cán bộ, hồ sơ đánh giá, bản gán vai trò hoặc chức vụ đang hiệu lực — khi đó hãy chuyển sang "Ngừng hoạt động".`,
      confirmText: "Xóa",
      isDanger: true,
      onConfirm: async () => {
        try {
          await catalogService.remove(kind, item.id);
          toast.success(`Đã xóa ${unit} "${item.name}".`);
          await load();
        } catch (err) {
          toast.error(errorMessage(err, "Không xóa được."), errorTitle(err), 10000);
        }
      },
    });

  const selectableTypes = typesOfSide.filter((t) => t.isActive || t.id === form?.unitTypeId);

  return (
    <>
      <div className="d-flex flex-wrap gap-2 mb-3">
        <input
          className="form-control form-control-sm"
          style={{ maxWidth: 320 }}
          placeholder="Tìm theo mã, tên, mô tả…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => setCollapsed(new Set())} disabled={!!search}>
          <i className="bi bi-arrows-expand me-1" />
          Mở rộng
        </button>
        <button
          type="button"
          className="btn btn-sm btn-outline-secondary"
          onClick={() => setCollapsed(new Set(items.filter((i) => i.childCount > 0).map((i) => i.id)))}
          disabled={!!search}
        >
          <i className="bi bi-arrows-collapse me-1" />
          Thu gọn
        </button>
        <button type="button" className="btn btn-sm btn-outline-secondary" onClick={load} disabled={loading}>
          <i className="bi bi-arrow-clockwise me-1" />
          Làm mới
        </button>
        {canManage && (
          <button type="button" className="btn btn-sm btn-primary ms-auto" onClick={() => openCreate()}>
            <i className="bi bi-plus-lg me-1" />
            Thêm {unit} gốc
          </button>
        )}
      </div>

      <Card>
        <div className="table-responsive">
          <table className="table table-hover align-middle mb-0" style={{ fontSize: 13 }}>
            <thead style={{ background: "var(--bg-base)" }}>
              <tr>
                <th className="ps-3">Tên</th>
                <th style={{ width: 140 }}>Mã</th>
                <th style={{ width: 140 }}>Loại</th>
                <th className="text-center" style={{ width: 80 }}>Cán bộ</th>
                <th style={{ width: 130 }}>Trạng thái</th>
                {canManage && <th className="pe-3 text-end" style={{ width: 170 }}>Thao tác</th>}
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={6} className="text-center py-5 text-secondary">
                    <span className="spinner-border spinner-border-sm me-2" />
                    Đang tải…
                  </td>
                </tr>
              ) : visible.length === 0 ? (
                <tr>
                  <td colSpan={6} className="text-center py-5 text-secondary">
                    <i className="bi bi-inbox fs-3 d-block mb-2" />
                    Chưa có {unit} nào.
                  </td>
                </tr>
              ) : (
                visible.map((item) => (
                  <tr key={item.id} style={{ opacity: item.isActive ? 1 : 0.6 }}>
                    <td className="ps-3">
                      <div className="d-flex align-items-center" style={{ paddingLeft: search ? 0 : item.depth * 20 }}>
                        {!search && item.childCount > 0 ? (
                          <button
                            type="button"
                            className="btn btn-sm btn-link p-0 me-1 text-secondary"
                            onClick={() => toggle(item.id)}
                            title={collapsed.has(item.id) ? "Mở rộng" : "Thu gọn"}
                          >
                            <i className={`bi ${collapsed.has(item.id) ? "bi-chevron-right" : "bi-chevron-down"}`} />
                          </button>
                        ) : (
                          <span className="me-1" style={{ display: "inline-block", width: 14 }} />
                        )}
                        <span className="fw-semibold text-dark">{item.name}</span>
                        {item.childCount > 0 && <span className="badge bg-light text-secondary border ms-2">{item.childCount} cấp dưới</span>}
                      </div>
                      {search && item.parentName && <div className="small text-secondary">thuộc {item.parentName}</div>}
                    </td>
                    <td>
                      <code className="fw-semibold">{item.code}</code>
                    </td>
                    <td className="text-secondary small">{item.unitTypeName || "—"}</td>
                    <td className="text-center">{item.memberCount}</td>
                    <td>
                      {item.isActive ? (
                        <span className="badge bg-success-subtle text-success border border-success-subtle">Hoạt động</span>
                      ) : (
                        <span className="badge bg-secondary-subtle text-secondary border">Ngừng hoạt động</span>
                      )}
                    </td>
                    {canManage && (
                      <td className="pe-3 text-end text-nowrap">
                        <button type="button" className="btn btn-sm btn-link p-1" title={`Thêm ${unit} cấp dưới`} onClick={() => openCreate(item)}>
                          <i className="bi bi-diagram-2" />
                        </button>
                        <button type="button" className="btn btn-sm btn-link p-1" title="Sửa / đổi cấp trên" onClick={() => openEdit(item)}>
                          <i className="bi bi-pencil-square" />
                        </button>
                        <button
                          type="button"
                          className="btn btn-sm btn-link p-1 text-warning"
                          title={item.isActive ? "Ngừng hoạt động" : "Hoạt động lại"}
                          onClick={() => toggleActive(item)}
                        >
                          <i className={`bi ${item.isActive ? "bi-pause-circle" : "bi-play-circle"}`} />
                        </button>
                        <button type="button" className="btn btn-sm btn-link p-1 text-danger" title="Xóa" onClick={() => handleDelete(item)}>
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
        <AdminModal title={form.id ? `Sửa ${unit}` : `Thêm ${unit}`} onClose={() => setForm(null)} closeDisabled={saving} maxWidth={560}>
          <form onSubmit={handleSave}>
            <div className="row g-2">
              <div className="col-12 col-md-5">
                <label className="form-label small fw-semibold mb-1">
                  Mã {kind === "departments" && <span className="text-danger">*</span>}
                </label>
                <input
                  className="form-control form-control-sm"
                  value={form.code}
                  maxLength={50}
                  required={kind === "departments"}
                  placeholder={kind === "departments" ? "VD: PH-KH" : "Để trống để hệ thống tự sinh"}
                  onChange={(e) => setForm({ ...form, code: e.target.value.toUpperCase() })}
                />
              </div>
              <div className="col-12 col-md-7">
                <label className="form-label small fw-semibold mb-1">
                  Tên <span className="text-danger">*</span>
                </label>
                <input
                  className="form-control form-control-sm"
                  value={form.name}
                  maxLength={200}
                  required
                  onChange={(e) => setForm({ ...form, name: e.target.value })}
                />
              </div>
              <div className="col-12 col-md-7">
                <label className="form-label small fw-semibold mb-1">Cấp trên trực tiếp</label>
                <select className="form-select form-select-sm" value={form.parentId} onChange={(e) => setForm({ ...form, parentId: e.target.value })}>
                  <option value="">— Không có (đơn vị gốc) —</option>
                  {parentOptions.map((item) => (
                    <option key={item.id} value={item.id}>
                      {treeLabel(item)}
                      {item.isActive ? "" : " (ngừng hoạt động)"}
                    </option>
                  ))}
                </select>
                {form.id && <div className="form-text">Không chọn được chính nó hoặc cấp dưới của nó.</div>}
              </div>
              <div className="col-12 col-md-5">
                <label className="form-label small fw-semibold mb-1">Loại</label>
                <select className="form-select form-select-sm" value={form.unitTypeId} onChange={(e) => setForm({ ...form, unitTypeId: e.target.value })}>
                  <option value="">— Chưa phân loại —</option>
                  {selectableTypes.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.name}
                      {t.isActive ? "" : " (ngừng dùng)"}
                    </option>
                  ))}
                </select>
              </div>
              <div className="col-12">
                <label className="form-label small fw-semibold mb-1">Mô tả</label>
                <textarea
                  className="form-control form-control-sm"
                  rows={2}
                  value={form.description}
                  onChange={(e) => setForm({ ...form, description: e.target.value })}
                />
              </div>
              <div className="col-5">
                <label className="form-label small fw-semibold mb-1">Thứ tự trong cùng cấp</label>
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
                    id={`org-active-${kind}`}
                    type="checkbox"
                    className="form-check-input"
                    checked={form.isActive}
                    onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
                  />
                  <label className="form-check-label small" htmlFor={`org-active-${kind}`}>
                    Đang hoạt động
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

export default OrgTreeTab;
