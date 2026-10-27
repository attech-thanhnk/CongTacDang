"use client";

import React, { useEffect, useMemo, useState } from "react";
import { useToast } from "@/contexts/ToastContext";
import { useFormDirty } from "@/components/admin/useFormDirty";
import { organizationService, DepartmentItem } from "@/services/organizationService";
import { CROSSCHECK_FIELDS, CrosscheckDto, SaveCrosscheckRequest } from "@/services/crosscheckService";

/** Sản phẩm đăng ký của hồ sơ có thể chọn làm dòng cần xác nhận. */
export interface CrosscheckTaskOption {
  id: string;
  taskName: string;
}

interface Props {
  /** Phiếu đang sửa; bỏ trống khi lập yêu cầu mới. */
  crosscheck?: CrosscheckDto | null;
  /** Sản phẩm đăng ký của hồ sơ; bỏ trống thì chỉ nhập dòng tự do. */
  tasks?: CrosscheckTaskOption[];
  busy: boolean;
  submitLabel: string;
  onCancel: () => void;
  onSubmit: (body: SaveCrosscheckRequest) => void;
}

interface Row {
  key: string;
  taskId: string | null;
  itemName: string;
  selfData: string;
}

interface FormState {
  external: boolean;
  departmentId: string;
  agencyName: string;
  fields: string[];
  addresseeName: string;
  requestReason: string;
  rows: Row[];
}

let rowSeq = 0;
const newKey = () => `r${++rowSeq}`;

function initialState(c?: CrosscheckDto | null): FormState {
  if (!c) return { external: false, departmentId: "", agencyName: "", fields: [], addresseeName: "", requestReason: "", rows: [] };
  const external = !c.expertDepartmentId;
  return {
    external,
    departmentId: c.expertDepartmentId ?? "",
    agencyName: external ? c.expertUnitName : "",
    fields: c.fields,
    addresseeName: c.addresseeName,
    requestReason: c.requestReason,
    rows: c.items.map((i) => ({ key: newKey(), taskId: i.taskId ?? null, itemName: i.itemName, selfData: i.selfData })),
  };
}

/** Nội dung yêu cầu xác nhận: đơn vị, lĩnh vực, lý do và các dòng cần xác nhận. */
export function CrosscheckRequestForm({ crosscheck, tasks = [], busy, submitLabel, onCancel, onSubmit }: Props) {
  const { toast } = useToast();
  const [form, setForm] = useState<FormState>(() => initialState(crosscheck));
  const [departments, setDepartments] = useState<DepartmentItem[]>([]);
  const dirty = useFormDirty(form);

  useEffect(() => {
    organizationService.getDepartments().then(setDepartments).catch(() => setDepartments([]));
  }, []);

  const patch = (p: Partial<FormState>) => setForm((f) => ({ ...f, ...p }));
  const setRow = (key: string, p: Partial<Row>) => setForm((f) => ({ ...f, rows: f.rows.map((r) => (r.key === key ? { ...r, ...p } : r)) }));
  const toggleField = (code: string) =>
    patch({ fields: form.fields.includes(code) ? form.fields.filter((x) => x !== code) : [...form.fields, code] });

  const usedTasks = useMemo(() => new Set(form.rows.map((r) => r.taskId).filter(Boolean)), [form.rows]);
  const freeTasks = tasks.filter((t) => !usedTasks.has(t.id));

  const submit = (event: React.FormEvent) => {
    event.preventDefault();
    if (form.external ? !form.agencyName.trim() : !form.departmentId) {
      toast.error("Hãy chọn đơn vị xác nhận.");
      return;
    }
    if (form.fields.length === 0) {
      toast.error("Hãy chọn ít nhất một lĩnh vực.");
      return;
    }
    if (!form.requestReason.trim()) {
      toast.error("Hãy nhập nội dung yêu cầu.");
      return;
    }
    if (form.rows.length === 0 || form.rows.some((r) => !r.taskId && !r.itemName.trim())) {
      toast.error("Hãy nhập tên chỉ tiêu cho mọi dòng cần xác nhận.");
      return;
    }
    onSubmit({
      version: crosscheck?.version,
      expertDepartmentId: form.external ? null : form.departmentId,
      expertAgencyName: form.external ? form.agencyName.trim() : null,
      fields: form.fields,
      addresseeName: form.addresseeName.trim(),
      requestReason: form.requestReason.trim(),
      items: form.rows.map((r) => ({ taskId: r.taskId, itemName: r.itemName.trim() || null, selfData: r.selfData.trim() || null })),
    });
  };

  return (
    <form className="border rounded p-3 d-flex flex-column gap-3" onSubmit={submit}>
      <div className="row g-2">
        <div className="col-12 col-md-6">
          <label className="form-label small mb-1" htmlFor="cc-unit">Đơn vị xác nhận</label>
          {form.external ? (
            <input id="cc-unit" className="form-control form-control-sm" maxLength={500} placeholder="Tên cơ quan ngoài hệ thống"
              value={form.agencyName} onChange={(e) => patch({ agencyName: e.target.value })} />
          ) : (
            <select id="cc-unit" className="form-select form-select-sm" value={form.departmentId} onChange={(e) => patch({ departmentId: e.target.value })}>
              <option value="">Chọn đơn vị</option>
              {departments.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          )}
          <div className="form-check mt-1">
            <input id="cc-external" type="checkbox" className="form-check-input" checked={form.external}
              onChange={(e) => patch({ external: e.target.checked })} />
            <label className="form-check-label small" htmlFor="cc-external">Cơ quan ngoài hệ thống (tôi nhập kết quả thay)</label>
          </div>
        </div>
        <div className="col-12 col-md-6">
          <label className="form-label small mb-1" htmlFor="cc-addressee">Kính gửi</label>
          <input id="cc-addressee" className="form-control form-control-sm" maxLength={500} value={form.addresseeName}
            onChange={(e) => patch({ addresseeName: e.target.value })} />
        </div>
      </div>

      <fieldset>
        <legend className="form-label small mb-1">Lĩnh vực</legend>
        <div className="d-flex flex-wrap column-gap-3">
          {CROSSCHECK_FIELDS.map((f) => (
            <div className="form-check" key={f.code}>
              <input id={`cc-f-${f.code}`} type="checkbox" className="form-check-input" checked={form.fields.includes(f.code)} onChange={() => toggleField(f.code)} />
              <label className="form-check-label small" htmlFor={`cc-f-${f.code}`}>{f.label}</label>
            </div>
          ))}
        </div>
      </fieldset>

      <div>
        <label className="form-label small mb-1" htmlFor="cc-reason">Nội dung yêu cầu</label>
        <textarea id="cc-reason" className="form-control form-control-sm" rows={3} maxLength={4000} value={form.requestReason}
          onChange={(e) => patch({ requestReason: e.target.value })} />
      </div>

      <div className="d-flex flex-column gap-2">
        <div className="d-flex justify-content-between align-items-center gap-2 flex-wrap">
          <span className="small fw-semibold">Chỉ tiêu cần xác nhận</span>
          <div className="d-flex gap-2">
            {freeTasks.length > 0 && (
              <select className="form-select form-select-sm" aria-label="Thêm từ sản phẩm đăng ký" value=""
                onChange={(e) => {
                  const t = tasks.find((x) => x.id === e.target.value);
                  if (t) patch({ rows: [...form.rows, { key: newKey(), taskId: t.id, itemName: t.taskName, selfData: "" }] });
                }}>
                <option value="">Thêm từ sản phẩm đăng ký</option>
                {freeTasks.map((t) => <option key={t.id} value={t.id}>{t.taskName}</option>)}
              </select>
            )}
            <button type="button" className="btn btn-outline-secondary btn-sm text-nowrap"
              onClick={() => patch({ rows: [...form.rows, { key: newKey(), taskId: null, itemName: "", selfData: "" }] })}>
              <i className="bi bi-plus-lg me-1" />Thêm dòng
            </button>
          </div>
        </div>
        {form.rows.map((r, index) => (
          <div key={r.key} className="border rounded p-2 row g-2 mx-0">
            <div className="col-12 col-md-5">
              <label className="form-label small mb-1" htmlFor={`cc-n-${r.key}`}>Dòng {index + 1}: tên chỉ tiêu / nhiệm vụ</label>
              <input id={`cc-n-${r.key}`} className="form-control form-control-sm" maxLength={500} value={r.itemName}
                readOnly={!!r.taskId} onChange={(e) => setRow(r.key, { itemName: e.target.value })} />
            </div>
            <div className="col-12 col-md-6">
              <label className="form-label small mb-1" htmlFor={`cc-s-${r.key}`}>Dữ liệu cá nhân</label>
              <textarea id={`cc-s-${r.key}`} className="form-control form-control-sm" rows={2} maxLength={4000} value={r.selfData}
                onChange={(e) => setRow(r.key, { selfData: e.target.value })} />
            </div>
            <div className="col-12 col-md-1 d-flex align-items-end justify-content-end">
              <button type="button" className="btn btn-outline-danger btn-sm" aria-label={`Bỏ dòng ${index + 1}`} title="Bỏ dòng"
                onClick={() => patch({ rows: form.rows.filter((x) => x.key !== r.key) })}>
                <i className="bi bi-trash" />
              </button>
            </div>
          </div>
        ))}
      </div>

      <div className="d-flex gap-2 justify-content-end">
        <button type="button" className="btn btn-outline-secondary btn-sm" disabled={busy} onClick={onCancel}>Hủy</button>
        <button type="submit" className="btn btn-primary btn-sm" disabled={busy || !dirty}>{busy ? "Đang xử lý..." : submitLabel}</button>
      </div>
    </form>
  );
}

export default CrosscheckRequestForm;
