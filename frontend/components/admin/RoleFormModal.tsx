"use client";

import React, { useState } from "react";
import type { AdminRole, SaveRolePayload } from "@/services/roleService";
import { AdminModal } from "./AdminModal";

/** Hộp thoại tạo vai trò / đổi tên, mô tả vai trò. Quyền chọn ở trang chi tiết (ma trận quyền). */
export function RoleFormModal({
  role,
  saving,
  onClose,
  onSubmit,
}: {
  role?: AdminRole;
  saving: boolean;
  onClose: () => void;
  onSubmit: (payload: SaveRolePayload) => void;
}) {
  const [name, setName] = useState(role?.name ?? "");
  const [description, setDescription] = useState(role?.description ?? "");

  return (
    <AdminModal title={role ? `Sửa vai trò "${role.name}"` : "Tạo vai trò"} onClose={onClose} closeDisabled={saving} maxWidth={480}>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          onSubmit({ name: name.trim(), description: description.trim() });
        }}
      >
        <div className="mb-2">
          <label className="form-label small fw-semibold mb-1">
            Tên vai trò <span className="text-danger">*</span>
          </label>
          <input className="form-control form-control-sm" value={name} required maxLength={100} onChange={(e) => setName(e.target.value)} />
          <div className="form-text">Không trùng tên vai trò khác (không phân biệt hoa thường).</div>
        </div>
        <div className="mb-2">
          <label className="form-label small fw-semibold mb-1">Mô tả</label>
          <textarea className="form-control form-control-sm" rows={3} value={description} onChange={(e) => setDescription(e.target.value)} />
        </div>
        {!role && <div className="small text-secondary">Sau khi tạo, chọn quyền cho vai trò ở trang chi tiết.</div>}
        <div className="d-flex justify-content-end gap-2 mt-3">
          <button type="button" className="btn btn-sm btn-light" onClick={onClose} disabled={saving}>
            Hủy
          </button>
          <button type="submit" className="btn btn-sm btn-primary" disabled={saving}>
            {saving && <span className="spinner-border spinner-border-sm me-1" />}
            Lưu
          </button>
        </div>
      </form>
    </AdminModal>
  );
}

export default RoleFormModal;
