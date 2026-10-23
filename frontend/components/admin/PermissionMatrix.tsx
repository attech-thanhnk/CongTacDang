"use client";

import React from "react";
import type { PermissionDefinition, PermissionModule } from "@/services/roleService";
import styles from "./PermissionMatrix.module.css";

export interface PermissionMatrixProps {
  modules: PermissionModule[];
  selected: Set<string>;
  onChange: (next: Set<string>) => void;
  readOnly?: boolean;
  /** Mã không được bỏ chọn (vd. 2 quyền quản trị của vai trò bảo vệ). */
  lockedCodes?: Set<string>;
  /** Quyền đã lưu: dòng khác với bản đã lưu được tô để thấy thay đổi chưa lưu. */
  original?: Set<string>;
  /** Chỉ đọc: hiện cả quyền chưa cấp (mặc định chỉ hiện quyền đã cấp). */
  showUngranted?: boolean;
}

/** Ma trận quyền nhóm theo phân hệ: mô tả từng quyền, đánh dấu quyền "không áp dụng phạm vi". */
export function PermissionMatrix({ modules, selected, onChange, readOnly, lockedCodes, original, showUngranted }: PermissionMatrixProps) {
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

  // Chỉ đọc: nhóm không có quyền nào gom thành một dòng để danh sách ngắn gọn.
  const collapseEmpty = readOnly && !showUngranted;
  const emptyModules = collapseEmpty ? modules.filter((m) => !m.permissions.some((p) => selected.has(p.code))) : [];
  const shownModules = collapseEmpty ? modules.filter((m) => !emptyModules.includes(m)) : modules;

  const renderModule = (module: PermissionModule) => {
    const count = module.permissions.filter((p) => selected.has(p.code)).length;
    const total = module.permissions.length;
    const all = count === total;
    const visible = collapseEmpty ? module.permissions.filter((p) => selected.has(p.code)) : module.permissions;
    return (
      <section key={module.module} className={styles.module} aria-label={module.moduleName}>
        <div className={styles.moduleHead}>
          <h3 className={styles.moduleTitle}>
            {module.moduleName}
            <span className={`${styles.count} ${all ? styles.countFull : ""}`} title={`Đã chọn ${count} trên ${total} quyền`}>
              {count}/{total}
            </span>
          </h3>
          {!readOnly && (
            <button type="button" className={`btn btn-sm btn-link ${styles.moduleToggle}`} onClick={() => toggleModule(module, !all)}>
              {all ? "Bỏ chọn cả nhóm" : "Chọn cả nhóm"}
            </button>
          )}
        </div>
        <ul className={styles.list}>
          {visible.map((permission) =>
            readOnly ? (
              <ReadOnlyItem key={permission.code} permission={permission} granted={selected.has(permission.code)} locked={lockedCodes?.has(permission.code) ?? false} />
            ) : (
              <EditItem
                key={permission.code}
                permission={permission}
                checked={selected.has(permission.code)}
                locked={lockedCodes?.has(permission.code) ?? false}
                changed={original ? original.has(permission.code) !== selected.has(permission.code) : false}
                onToggle={toggle}
              />
            )
          )}
        </ul>
      </section>
    );
  };

  return (
    <div className={styles.modules}>
      {shownModules.length > 0 && <div className={`${styles.modules} ${readOnly ? styles.readGrid : ""}`}>{shownModules.map(renderModule)}</div>}
      {readOnly && shownModules.length === 0 && <div className={styles.emptyModules}>Vai trò chưa có quyền nào.</div>}
      {emptyModules.length > 0 && (
        <div className={styles.emptyModules}>
          <span>Chưa cấp quyền nào trong nhóm:</span>
          {emptyModules.map((m) => (
            <span key={m.module} className={styles.emptyTag}>
              {m.moduleName} <span className="num">0/{m.permissions.length}</span>
            </span>
          ))}
        </div>
      )}
    </div>
  );
}

function PermissionText({ permission, locked }: { permission: PermissionDefinition; locked: boolean }) {
  return (
    <span className={styles.body}>
      <span className={styles.titleLine}>
        <span className={styles.name}>{permission.name}</span>
        {!permission.appliesScope && (
          <span className="tag t-wait" title="Quyền này không áp dụng phạm vi: vai trò chứa quyền này chỉ gán được phạm vi Toàn công ty.">
            Chỉ Toàn công ty
          </span>
        )}
        {locked && (
          <span className="status-badge tone-neutral" title="Vai trò được bảo vệ phải giữ quyền này">
            <i className="bi bi-lock-fill me-1" />
            Bắt buộc
          </span>
        )}
      </span>
      <span className={`d-block ${styles.desc}`}>
        {permission.description}
        {permission.description && " "}
        <code className={styles.code}>{permission.code}</code>
      </span>
    </span>
  );
}

function ReadOnlyItem({ permission, granted, locked }: { permission: PermissionDefinition; granted: boolean; locked: boolean }) {
  return (
    <li className={`${styles.item} ${granted ? "" : styles.off}`}>
      <div className={`${styles.row} ${styles.rowLocked}`}>
        <span className={`${styles.mark} ${granted ? styles.markOn : styles.markOff}`} role="img" aria-label={granted ? "Có quyền" : "Chưa cấp"} title={granted ? "Có quyền" : "Chưa cấp"}>
          <i className={`bi ${granted ? "bi-check-lg" : "bi-dash"}`} />
        </span>
        <PermissionText permission={permission} locked={locked && granted} />
      </div>
    </li>
  );
}

function EditItem({
  permission,
  checked,
  locked,
  changed,
  onToggle,
}: {
  permission: PermissionDefinition;
  checked: boolean;
  locked: boolean;
  changed: boolean;
  onToggle: (code: string, checked: boolean) => void;
}) {
  const id = `perm-${permission.code}`;
  const fixed = locked && checked;
  return (
    <li className={styles.item}>
      <label htmlFor={id} className={`${styles.row} ${fixed ? styles.rowLocked : ""} ${changed ? styles.changed : ""}`}>
        <input
          id={id}
          type="checkbox"
          className={`form-check-input ${styles.check}`}
          checked={checked}
          disabled={fixed}
          onChange={(e) => onToggle(permission.code, e.target.checked)}
        />
        <PermissionText permission={permission} locked={locked} />
      </label>
    </li>
  );
}

export default PermissionMatrix;
