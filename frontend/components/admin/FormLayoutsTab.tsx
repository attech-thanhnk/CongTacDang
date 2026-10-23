"use client";

import React, { useCallback, useEffect, useState } from "react";
import { useToast } from "@/contexts/ToastContext";
import { RowActions } from "@/components/common/RowActions";
import { Card } from "@/components/common";
import { FormLayout, formLayoutService, SaveFormLayoutPayload } from "@/services/formLayoutService";
import { AdminModal } from "./AdminModal";
import { useFormDirty } from "./useFormDirty";
import { errorMessage, errorTitle } from "./adminUtils";

interface FormState {
  id?: string;
  name: string;
  description: string;
  singleLinePositions: boolean;
  showDateLineOn9D: boolean;
  emptyPositionText: string;
  isDefault: boolean;
  sortOrder: string;
}

const EMPTY_FORM: FormState = {
  name: "",
  description: "",
  singleLinePositions: false,
  showDateLineOn9D: false,
  emptyPositionText: "",
  isDefault: false,
  sortOrder: "0",
};

/** Tóm tắt cách in của một kiểu (cột "Trình bày"). */
function summary(l: FormLayout): string[] {
  return [
    l.singleLinePositions ? "Chức vụ một dòng (9D)" : "Chức vụ 3 dòng",
    l.showDateLineOn9D ? "9D có dòng ngày" : "9D không có dòng ngày",
    l.emptyPositionText ? `Chức vụ trống ghi "${l.emptyPositionText}"` : "Chức vụ trống để dòng chấm",
  ];
}

/** Danh mục kiểu trình bày biểu mẫu tự đánh giá (Mẫu 09C, 9D); gắn cho chức vụ ở tab Chức vụ. */
export function FormLayoutsTab({ canManage }: { canManage: boolean }) {
  const { toast, confirm } = useToast();
  const [layouts, setLayouts] = useState<FormLayout[]>([]);
  const [loading, setLoading] = useState(true);
  const [form, setForm] = useState<FormState | null>(null);
  const formDirty = useFormDirty(form);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setLayouts(await formLayoutService.list());
    } catch (err) {
      toast.error(errorMessage(err, "Không tải được danh mục kiểu trình bày."), errorTitle(err));
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const openEdit = (l: FormLayout, copy = false) =>
    setForm({
      id: copy ? undefined : l.id,
      name: copy ? `${l.name} (bản sao)` : l.name,
      description: l.description ?? "",
      singleLinePositions: l.singleLinePositions,
      showDateLineOn9D: l.showDateLineOn9D,
      emptyPositionText: l.emptyPositionText ?? "",
      isDefault: copy ? false : l.isDefault,
      sortOrder: String(l.sortOrder),
    });

  const handleSave = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!form) return;
    const sortOrder = Number.parseInt(form.sortOrder || "0", 10);
    if (Number.isNaN(sortOrder) || sortOrder < 0) {
      toast.error("Thứ tự phải là số nguyên không âm.");
      return;
    }
    const payload: SaveFormLayoutPayload = {
      name: form.name.trim(),
      description: form.description.trim() || null,
      singleLinePositions: form.singleLinePositions,
      showDateLineOn9D: form.showDateLineOn9D,
      emptyPositionText: form.emptyPositionText.trim() || null,
      isDefault: form.isDefault,
      sortOrder,
    };
    setSaving(true);
    try {
      if (form.id) await formLayoutService.update(form.id, payload);
      else await formLayoutService.create(payload);
      toast.success(form.id ? `Đã cập nhật kiểu "${payload.name}".` : `Đã thêm kiểu "${payload.name}".`);
      setForm(null);
      await load();
    } catch (err) {
      toast.error(errorMessage(err, "Không lưu được kiểu trình bày."), errorTitle(err), 8000);
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = (l: FormLayout) =>
    confirm({
      title: "Xóa kiểu trình bày",
      message: `Xóa kiểu "${l.name}"? Kiểu đang gắn cho chức vụ hoặc cán bộ thì không xóa được.`,
      confirmText: "Xóa",
      isDanger: true,
      onConfirm: async () => {
        try {
          await formLayoutService.remove(l.id);
          toast.success(`Đã xóa kiểu "${l.name}".`);
          await load();
        } catch (err) {
          toast.error(errorMessage(err, "Không xóa được kiểu trình bày."), errorTitle(err), 10000);
        }
      },
    });

  const check = (id: string, label: string, key: "singleLinePositions" | "showDateLineOn9D" | "isDefault") =>
    form && (
      <div className="form-check">
        <input id={id} type="checkbox" className="form-check-input" checked={form[key]} onChange={(e) => setForm({ ...form, [key]: e.target.checked })} />
        <label className="form-check-label small" htmlFor={id}>
          {label}
        </label>
      </div>
    );

  return (
    <>
      <Card>
        {canManage && (
          <div className="card-header d-flex flex-wrap align-items-center gap-2">
            <button type="button" className="btn btn-sm btn-primary ms-auto" onClick={() => setForm({ ...EMPTY_FORM })}>
              <i className="bi bi-plus-lg me-1" />
              Thêm kiểu
            </button>
          </div>
        )}
        <div className="table-responsive">
          <table className="table table-hover align-middle mb-0">
            <thead>
              <tr>
                <th className="ps-3">Kiểu trình bày</th>
                <th>Trình bày</th>
                <th className="text-center" style={{ width: 80 }}>Thứ tự</th>
                <th className="text-center" style={{ width: 130 }}>Áp dụng</th>
                {canManage && <th className="pe-3 text-end" style={{ width: 130 }}>Thao tác</th>}
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={5} className="text-center py-4 text-secondary loading-delay">
                    <span className="spinner-border spinner-border-sm me-2" />
                    Đang tải…
                  </td>
                </tr>
              ) : layouts.length === 0 ? (
                <tr>
                  <td colSpan={5} className="text-center py-4 text-secondary">
                    Chưa có kiểu trình bày nào.
                  </td>
                </tr>
              ) : (
                layouts.map((l) => (
                  <tr key={l.id}>
                    <td className="ps-3">
                      <span className="fw-semibold text-dark">{l.name}</span>
                      {l.isDefault && <span className="tag t-ok ms-2">Mặc định</span>}
                      {l.description && <div className="small text-secondary">{l.description}</div>}
                    </td>
                    <td className="small">
                      {summary(l).map((line) => (
                        <div key={line}>{line}</div>
                      ))}
                    </td>
                    <td className="text-center">{l.sortOrder}</td>
                    <td className="text-center small">
                      {l.positionCount} chức vụ
                      {l.memberCount > 0 && <div>{l.memberCount} cán bộ đặt riêng</div>}
                    </td>
                    {canManage && (
                      <td className="pe-3 text-end text-nowrap">
                        <RowActions
                          itemLabel={l.name}
                          primary={[{ key: "edit", label: "Sửa", icon: "bi-pencil", onClick: () => openEdit(l) }]}
                          menu={[
                            { key: "clone", label: "Nhân bản", icon: "bi-copy", onClick: () => openEdit(l, true) },
                            { key: "delete", label: "Xóa", icon: "bi-trash", danger: true, onClick: () => handleDelete(l) },
                          ]}
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
        <AdminModal title={form.id ? "Sửa kiểu trình bày" : "Thêm kiểu trình bày"} onClose={() => setForm(null)} closeDisabled={saving} maxWidth={560}
          footer={
            <>
            <button type="button" className="btn btn-outline-secondary" onClick={() => setForm(null)} disabled={saving}>
              Hủy
            </button>
            <button type="submit" form="form-layout-form" className="btn btn-primary" disabled={saving || !formDirty}>
              {saving && <span className="spinner-border spinner-border-sm me-1" />}
              Lưu
            </button>
            </>
          }
        >
          <form id="form-layout-form" onSubmit={handleSave}>
            <div className="row g-2">
              <div className="col-12">
                <label className="form-label">
                  Tên kiểu <span className="text-danger">*</span>
                </label>
                <input
                  className="form-control form-control-sm"
                  value={form.name}
                  maxLength={200}
                  required
                  onChange={(e) => setForm({ ...form, name: e.target.value })}
                />
              </div>
              <div className="col-12">
                <label className="form-label">Mô tả</label>
                <input
                  className="form-control form-control-sm"
                  value={form.description}
                  maxLength={500}
                  placeholder="VD: Giám đốc, Phó Giám đốc"
                  onChange={(e) => setForm({ ...form, description: e.target.value })}
                />
              </div>
              <div className="col-12">
                {check("layout-single-line", "Mẫu 9D ghi chức vụ trên một dòng", "singleLinePositions")}
                {check("layout-date-line", "Mẫu 9D có dòng địa danh, ngày tháng", "showDateLineOn9D")}
              </div>
              <div className="col-8">
                <label className="form-label">Chức vụ trống ghi là</label>
                <input
                  className="form-control form-control-sm"
                  value={form.emptyPositionText}
                  maxLength={50}
                  placeholder="Để trống: giữ dòng chấm"
                  onChange={(e) => setForm({ ...form, emptyPositionText: e.target.value })}
                />
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
              <div className="col-12">{check("layout-default", "Kiểu mặc định (cán bộ có chức vụ chưa gắn kiểu)", "isDefault")}</div>
            </div>
          </form>
        </AdminModal>
      )}
    </>
  );
}

export default FormLayoutsTab;
