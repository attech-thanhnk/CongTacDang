"use client";

import React, { useEffect, useMemo, useState } from "react";

export interface PickerRow {
  id: string;
  name: string;
  detail?: string;
  /** Nhóm lọc (ví dụ nhóm đối tượng); để trống nếu không lọc. */
  group?: string;
}

interface Props {
  isOpen: boolean;
  title: string;
  confirmText: string;
  rows: PickerRow[];
  loading?: boolean;
  busy?: boolean;
  /** Hiện ô nhập nhóm chức danh/phạm vi. */
  askGroupName?: boolean;
  groupNameRequired?: boolean;
  emptyText: string;
  onCancel: () => void;
  onConfirm: (recordIds: string[], groupName: string) => void;
}

/** Chọn hồ sơ để lập biên bản/phiếu mới; chỉ bật nút khi đã chọn ít nhất một hồ sơ. */
export function RecordPickerDialog({ isOpen, title, confirmText, rows, loading, busy, askGroupName, groupNameRequired, emptyText, onCancel, onConfirm }: Props) {
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [group, setGroup] = useState("");
  const [groupName, setGroupName] = useState("");

  useEffect(() => {
    if (isOpen) {
      setSelected(new Set());
      setGroup("");
      setGroupName("");
    }
  }, [isOpen]);

  const groups = useMemo(() => Array.from(new Set(rows.map((r) => r.group).filter((g): g is string => !!g))), [rows]);
  const visible = useMemo(() => rows.filter((r) => !group || r.group === group), [rows, group]);

  if (!isOpen) return null;

  const toggle = (id: string) =>
    setSelected((s) => {
      const next = new Set(s);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  const allVisible = visible.length > 0 && visible.every((r) => selected.has(r.id));
  const toggleAll = () =>
    setSelected((s) => {
      const next = new Set(s);
      visible.forEach((r) => (allVisible ? next.delete(r.id) : next.add(r.id)));
      return next;
    });
  const missingName = !!groupNameRequired && groupName.trim() === "";

  return (
    <div className="modal fade show d-block" tabIndex={-1} role="dialog" aria-modal="true" style={{ backgroundColor: "rgba(15, 23, 42, 0.55)", zIndex: 1080 }}>
      <div className="modal-dialog modal-dialog-centered modal-dialog-scrollable" style={{ maxWidth: 640 }}>
        <div className="modal-content border-0 shadow-lg" style={{ borderRadius: 14 }}>
          <div className="modal-header px-4 py-3">
            <h2 className="modal-title fs-6 fw-bold mb-0">{title}</h2>
            <button type="button" className="btn-close" aria-label="Đóng" onClick={onCancel} disabled={busy} />
          </div>
          <div className="modal-body px-4 py-3 d-flex flex-column gap-2">
            {askGroupName && (
              <div>
                <label className="form-label small mb-1" htmlFor="picker-group-name">Nhóm chức danh / phạm vi{groupNameRequired ? " *" : ""}</label>
                <input id="picker-group-name" className="form-control form-control-sm" maxLength={300} value={groupName} onChange={(e) => setGroupName(e.target.value)} />
              </div>
            )}
            {groups.length > 1 && (
              <select className="form-select form-select-sm" aria-label="Lọc theo nhóm đối tượng" value={group} onChange={(e) => setGroup(e.target.value)}>
                <option value="">Mọi nhóm đối tượng</option>
                {groups.map((g) => <option key={g} value={g}>{g}</option>)}
              </select>
            )}
            {loading ? (
              <div className="text-secondary small loading-delay"><span className="spinner-border spinner-border-sm me-2" />Đang tải...</div>
            ) : visible.length === 0 ? (
              <div className="text-secondary small py-3 text-center">{emptyText}</div>
            ) : (
              <div className="list-group">
                <label className="list-group-item d-flex gap-2 align-items-center small fw-semibold">
                  <input type="checkbox" className="form-check-input mt-0" checked={allVisible} onChange={toggleAll} />
                  Chọn tất cả ({visible.length})
                </label>
                {visible.map((r) => (
                  <label key={r.id} className="list-group-item d-flex gap-2 align-items-start small">
                    <input type="checkbox" className="form-check-input mt-1" checked={selected.has(r.id)} onChange={() => toggle(r.id)} />
                    <span>
                      <span className="fw-semibold">{r.name}</span>
                      {r.detail && <span className="d-block text-secondary">{r.detail}</span>}
                    </span>
                  </label>
                ))}
              </div>
            )}
          </div>
          <div className="modal-footer px-4 py-3 d-flex justify-content-between">
            <span className="small text-secondary">Đã chọn {selected.size}</span>
            <div className="d-flex gap-2">
              <button type="button" className="btn btn-outline-secondary" disabled={busy} onClick={onCancel}>Hủy</button>
              <button type="button" className="btn btn-primary" disabled={busy || selected.size === 0 || missingName}
                onClick={() => onConfirm(Array.from(selected), groupName.trim())}>
                {busy ? "Đang xử lý..." : confirmText}
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

export default RecordPickerDialog;
