"use client";

import React, { useState } from "react";
import type { AdminRole, SaveRolePayload } from "@/services/roleService";
import { AdminModal } from "./AdminModal";

/** Hộp thoại tạo vai trò / sửa tên, mô tả vai trò. Quyền chọn ở trang chi tiết (ma trận quyền). */
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
  const changed = name.trim() !== (role?.name ?? "") || description.trim() !== (role?.description ?? "");
  const canSave = !!name.trim() && changed && !saving;

  return (
    <AdminModal
      title={role ? "Sửa vai trò" : "Tạo vai trò"}
      onClose={onClose}
      closeDisabled={saving}
      maxWidth={480}
      footer={
        <>
          <button type="button" className="btn btn-outline-secondary" onClick={onClose} disabled={saving}>
            Hủy
          </button>
          <button type="submit" form="role-form" className="btn btn-primary" disabled={!canSave}>
            {saving && <span className="spinner-border spinner-border-sm me-1" />}
            {role ? "Lưu" : "Tạo vai trò"}
          </button>
        </>
      }
    >
      <form
        id="role-form"
        onSubmit={(e) => {
          e.preventDefault();
          if (canSave) onSubmit({ name: name.trim(), description: description.trim() });
        }}
      >
        <div className="mb-2">
          <label className="form-label" htmlFor="role-name">
            Tên vai trò <span className="text-danger">*</span>
          </label>
          <input id="role-name" className="form-control form-control-sm" value={name} required maxLength={100} autoFocus onChange={(e) => setName(e.target.value)} />
        </div>
        <div className="mb-0">
          <label className="form-label" htmlFor="role-desc">Mô tả</label>
          <textarea id="role-desc" className="form-control form-control-sm" rows={3} value={description} onChange={(e) => setDescription(e.target.value)} />
        </div>
      </form>
    </AdminModal>
  );
}

export default RoleFormModal;
