"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { Card, PageHeader } from "@/components/common";
import { catalogService, CatalogItem, CatalogKind, SaveCatalogItemPayload } from "@/services/catalogService";

const TABS: { kind: CatalogKind; label: string; unit: string; icon: string }[] = [
  { kind: "departments", label: "Phòng / đơn vị", unit: "Phòng", icon: "bi-building" },
  { kind: "branches", label: "Chi bộ", unit: "Chi bộ", icon: "bi-flag" },
];

interface FormState {
  id?: string;
  code: string;
  name: string;
  description: string;
  sortOrder: string;
  isActive: boolean;
}

const EMPTY_FORM: FormState = { code: "", name: "", description: "", sortOrder: "0", isActive: true };

/** Trang quản lý danh mục tổ chức: Phòng/đơn vị và Chi bộ. Xem: mọi người; sửa: quyền "Quản lý danh mục". */
export default function CatalogPage() {
  const { hasPermission } = useAuth();
  const { toast, confirm } = useToast();
  const canManage = hasPermission("catalog.manage");

  const [tab, setTab] = useState<CatalogKind>("departments");
  const [items, setItems] = useState<CatalogItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [form, setForm] = useState<FormState | null>(null);
  const [saving, setSaving] = useState(false);

  const current = TABS.find((t) => t.kind === tab)!;

  const load = useCallback(async (kind: CatalogKind) => {
    setLoading(true);
    try {
      setItems(await catalogService.list(kind));
    } catch (err: any) {
      toast.error(err?.message || "Không tải được danh mục.");
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    load(tab);
  }, [tab, load]);

  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return items;
    return items.filter(
      (i) => i.code.toLowerCase().includes(term) || i.name.toLowerCase().includes(term) || (i.description || "").toLowerCase().includes(term)
    );
  }, [items, search]);

  const openCreate = () => setForm({ ...EMPTY_FORM });
  const openEdit = (item: CatalogItem) =>
    setForm({
      id: item.id,
      code: item.code,
      name: item.name,
      description: item.description || "",
      sortOrder: String(item.sortOrder ?? 0),
      isActive: item.isActive,
    });

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
    };
    setSaving(true);
    try {
      if (form.id) {
        await catalogService.update(tab, form.id, payload);
        toast.success(`Đã cập nhật ${current.unit} "${payload.name}".`);
      } else {
        await catalogService.create(tab, payload);
        toast.success(`Đã thêm ${current.unit} "${payload.name}".`);
      }
      setForm(null);
      await load(tab);
    } catch (err: any) {
      toast.error(err?.message || "Không lưu được.", undefined, 8000);
    } finally {
      setSaving(false);
    }
  };

  const toggleActive = async (item: CatalogItem) => {
    try {
      await catalogService.update(tab, item.id, { isActive: !item.isActive });
      toast.success(item.isActive ? `Đã ngừng hoạt động "${item.name}".` : `Đã cho "${item.name}" hoạt động trở lại.`);
      await load(tab);
    } catch (err: any) {
      toast.error(err?.message || "Không cập nhật được trạng thái.", undefined, 8000);
    }
  };

  const handleDelete = (item: CatalogItem) => {
    confirm({
      title: `Xóa ${current.unit}`,
      message: `Xóa ${current.unit} "${item.name}" (${item.code})? Không thể xóa khi còn cán bộ hoặc hồ sơ đánh giá — khi đó hãy chuyển sang "Ngừng hoạt động".`,
      confirmText: "Xóa",
      isDanger: true,
      onConfirm: async () => {
        try {
          await catalogService.remove(tab, item.id);
          toast.success(`Đã xóa ${current.unit} "${item.name}".`);
          await load(tab);
        } catch (err: any) {
          // 409: máy chủ nêu số cán bộ / hồ sơ còn tham chiếu và cách xử lý.
          toast.error(err?.message || "Không xóa được.", err?.status === 409 ? "Không thể xóa" : undefined, 10000);
        }
      },
    });
  };

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Danh mục tổ chức"
        subTitle={canManage ? "Quản lý Phòng/đơn vị chuyên môn và Chi bộ." : "Danh mục Phòng/đơn vị và Chi bộ (chỉ xem)."}
        actions={
          canManage ? (
            <button type="button" className="btn btn-sm btn-primary" onClick={openCreate}>
              <i className="bi bi-plus-lg me-1" />
              Thêm {current.unit}
            </button>
          ) : undefined
        }
      />

      <div className="page-body">
        <ul className="nav nav-tabs mb-3">
          {TABS.map((t) => (
            <li className="nav-item" key={t.kind}>
              <button
                type="button"
                className={`nav-link ${tab === t.kind ? "active fw-semibold" : ""}`}
                onClick={() => {
                  setTab(t.kind);
                  setSearch("");
                }}
              >
                <i className={`bi ${t.icon} me-1`} />
                {t.label}
              </button>
            </li>
          ))}
        </ul>

        <div className="d-flex gap-2 mb-3">
          <input
            className="form-control form-control-sm"
            style={{ maxWidth: 360 }}
            placeholder="Tìm theo mã, tên, mô tả…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => load(tab)} disabled={loading}>
            <i className="bi bi-arrow-clockwise me-1" />
            Làm mới
          </button>
        </div>

        <Card>
          <div className="table-responsive">
            <table className="table table-hover align-middle mb-0" style={{ fontSize: 13 }}>
              <thead style={{ background: "var(--bg-base)" }}>
                <tr>
                  <th className="ps-3" style={{ width: 70 }}>Thứ tự</th>
                  <th style={{ width: 140 }}>Mã</th>
                  <th>Tên</th>
                  <th>Mô tả</th>
                  <th className="text-center" style={{ width: 90 }}>Cán bộ</th>
                  <th style={{ width: 140 }}>Trạng thái</th>
                  {canManage && <th className="pe-3 text-end" style={{ width: 150 }}>Thao tác</th>}
                </tr>
              </thead>
              <tbody>
                {loading ? (
                  <tr>
                    <td colSpan={7} className="text-center py-5 text-secondary">
                      <span className="spinner-border spinner-border-sm me-2" />
                      Đang tải…
                    </td>
                  </tr>
                ) : filtered.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="text-center py-5 text-secondary">
                      <i className="bi bi-inbox fs-3 d-block mb-2" />
                      Chưa có {current.unit} nào.
                    </td>
                  </tr>
                ) : (
                  filtered.map((item) => (
                    <tr key={item.id} style={{ opacity: item.isActive ? 1 : 0.6 }}>
                      <td className="ps-3 text-secondary">{item.sortOrder}</td>
                      <td>
                        <code className="fw-semibold">{item.code}</code>
                      </td>
                      <td className="fw-semibold text-dark">{item.name}</td>
                      <td className="text-secondary small">{item.description || "—"}</td>
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
                          <button type="button" className="btn btn-sm btn-link p-1" title="Sửa" onClick={() => openEdit(item)}>
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
      </div>

      {form && (
        <div className="modal show d-block bg-dark bg-opacity-50" tabIndex={-1}>
          <div className="modal-dialog modal-dialog-centered" style={{ maxWidth: 480 }}>
            <div className="modal-content shadow border-0">
              <div className="modal-header py-2 px-3">
                <h6 className="modal-title fw-bold mb-0">
                  {form.id ? `Sửa ${current.unit}` : `Thêm ${current.unit}`}
                </h6>
                <button type="button" className="btn-close" aria-label="Đóng" onClick={() => setForm(null)} />
              </div>
              <form onSubmit={handleSave}>
                <div className="modal-body p-3">
                  <div className="mb-2">
                    <label className="form-label small fw-semibold mb-1">
                      Mã {current.unit} {tab === "departments" && <span className="text-danger">*</span>}
                    </label>
                    <input
                      className="form-control form-control-sm"
                      value={form.code}
                      maxLength={50}
                      required={tab === "departments"}
                      placeholder={tab === "departments" ? "VD: PH-KH" : "Để trống để hệ thống tự sinh"}
                      onChange={(e) => setForm({ ...form, code: e.target.value.toUpperCase() })}
                    />
                    <div className="form-text">Duy nhất, không chứa khoảng trắng; dùng để tham chiếu khi nhập dữ liệu từ Excel.</div>
                  </div>
                  <div className="mb-2">
                    <label className="form-label small fw-semibold mb-1">
                      Tên {current.unit} <span className="text-danger">*</span>
                    </label>
                    <input
                      className="form-control form-control-sm"
                      value={form.name}
                      maxLength={200}
                      required
                      onChange={(e) => setForm({ ...form, name: e.target.value })}
                    />
                  </div>
                  <div className="mb-2">
                    <label className="form-label small fw-semibold mb-1">Mô tả</label>
                    <textarea
                      className="form-control form-control-sm"
                      rows={2}
                      value={form.description}
                      onChange={(e) => setForm({ ...form, description: e.target.value })}
                    />
                  </div>
                  <div className="row g-2">
                    <div className="col-5">
                      <label className="form-label small fw-semibold mb-1">Thứ tự hiển thị</label>
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
                          id="catalog-active"
                          type="checkbox"
                          className="form-check-input"
                          checked={form.isActive}
                          onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
                        />
                        <label className="form-check-label small" htmlFor="catalog-active">
                          Đang hoạt động
                        </label>
                      </div>
                    </div>
                  </div>
                </div>
                <div className="modal-footer py-2 px-3">
                  <button type="button" className="btn btn-sm btn-light" onClick={() => setForm(null)} disabled={saving}>
                    Hủy
                  </button>
                  <button type="submit" className="btn btn-sm btn-primary" disabled={saving}>
                    {saving && <span className="spinner-border spinner-border-sm me-1" />}
                    Lưu
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
