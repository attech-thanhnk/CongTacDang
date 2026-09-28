"use client";

import React from "react";
import type { PermissionModule } from "@/services/roleService";

export interface PermissionMatrixProps {
  modules: PermissionModule[];
  selected: Set<string>;
  onChange: (next: Set<string>) => void;
  readOnly?: boolean;
  /** Mã không được bỏ chọn (vd. 2 quyền quản trị của vai trò bảo vệ). */
  lockedCodes?: Set<string>;
}

/** Ma trận quyền nhóm theo phân hệ: mô tả từng quyền, đánh dấu quyền "không áp dụng phạm vi". */
export function PermissionMatrix({ modules, selected, onChange, readOnly, lockedCodes }: PermissionMatrixProps) {
  const toggle = (code: string, checked: boolean) => {
    const next = new Set(selected);
    if (checked) next.add(code);
    else if (!lockedCodes?.has(code)) next.delete(code);
    onChange(next);
  };

  const toggleModule = (module: PermissionModule, checked: boolean) => {
    const next = new Set(selected);
    module.permissions.forEach((p) => {
      if (checked) next.add(p.code);
      else if (!lockedCodes?.has(p.code)) next.delete(p.code);
    });
    onChange(next);
  };

  return (
    <div className="d-flex flex-column gap-3">
      {modules.map((module) => {
        const count = module.permissions.filter((p) => selected.has(p.code)).length;
        const all = count === module.permissions.length;
        return (
          <div key={module.module} className="border rounded">
            <div className="d-flex justify-content-between align-items-center px-3 py-2 bg-light border-bottom">
              <span className="fw-semibold">
                {module.moduleName}{" "}
                <span className="small text-secondary fw-normal">
                  ({count}/{module.permissions.length})
                </span>
              </span>
              {!readOnly && (
                <button type="button" className="btn btn-sm btn-link p-0 text-decoration-none" onClick={() => toggleModule(module, !all)}>
                  {all ? "Bỏ chọn cả nhóm" : "Chọn cả nhóm"}
                </button>
              )}
            </div>
            <ul className="list-unstyled mb-0">
              {module.permissions.map((permission) => {
                const id = `perm-${permission.code}`;
                const locked = lockedCodes?.has(permission.code) ?? false;
                return (
                  <li key={permission.code} className="px-3 py-2 border-bottom">
                    <div className="form-check mb-0">
                      <input
                        id={id}
                        type="checkbox"
                        className="form-check-input"
                        checked={selected.has(permission.code)}
                        disabled={readOnly || (locked && selected.has(permission.code))}
                        onChange={(e) => toggle(permission.code, e.target.checked)}
                      />
                      <label className="form-check-label w-100" htmlFor={id}>
                        <span className="fw-semibold text-dark">{permission.name}</span>{" "}
                        <code className="small text-secondary">{permission.code}</code>
                        {!permission.appliesScope && (
                          <span
                            className="badge bg-warning-subtle text-warning-emphasis border border-warning-subtle ms-2"
                            title="Quyền này không áp dụng phạm vi: vai trò chứa quyền này chỉ gán được phạm vi Toàn công ty."
                          >
                            Chỉ Toàn công ty
                          </span>
                        )}
                        {locked && (
                          <span className="badge bg-secondary-subtle text-secondary border ms-2" title="Vai trò được bảo vệ phải giữ quyền này">
                            <i className="bi bi-lock-fill me-1" />
                            Bắt buộc
                          </span>
                        )}
                        <div className="small text-secondary">{permission.description}</div>
                      </label>
                    </div>
                  </li>
                );
              })}
            </ul>
          </div>
        );
      })}
    </div>
  );
}

export default PermissionMatrix;
