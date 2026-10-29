"use client";

import React, { useMemo, useState } from "react";
import { CatalogItem, treeLabel } from "@/services/catalogService";
import {
  AdminRole,
  CreateAssignmentPayload,
  RoleAssignment,
  SCOPE_TYPE_LABELS,
  ScopeType,
  UpdateAssignmentPayload,
} from "@/services/roleService";
import { AdminModal } from "./AdminModal";
import { UserPicker, PickedUser } from "./UserPicker";
import { endOfDayExclusiveIso, exclusiveIsoToLastDayInput, startOfDayIso, toDateInput } from "./adminUtils";
import { DateField } from "@/components/evaluations/DateField";

export interface AssignmentFormModalProps {
  /** Sửa bản gán (chỉ thời hạn + ghi chú). Không truyền → tạo mới. */
  assignment?: RoleAssignment;
  /** Tạo từ trang người dùng: người được gán cố định. */
  fixedUser?: PickedUser;
  /** Tạo từ trang vai trò: vai trò cố định. */
  fixedRole?: AdminRole;
  /** Danh sách vai trò để chọn (khi không cố định vai trò). */
  roles: AdminRole[];
  /** Mã quyền "không áp dụng phạm vi" — vai trò chứa quyền này chỉ gán được Toàn công ty. */
  globalOnlyCodes: Set<string>;
  departments: CatalogItem[];
  partyCells: CatalogItem[];
  saving: boolean;
  onClose: () => void;
  onCreate?: (payload: CreateAssignmentPayload) => void;
  onUpdate?: (payload: UpdateAssignmentPayload) => void;
}

const SCOPE_TYPES: ScopeType[] = ["Global", "Department", "PartyCell"];

/** Hộp thoại gán vai trò: vai trò, loại phạm vi, đối tượng phạm vi, thời hạn, ghi chú. */
export function AssignmentFormModal({
  assignment,
  fixedUser,
  fixedRole,
  roles,
  globalOnlyCodes,
  departments,
  partyCells,
  saving,
  onClose,
  onCreate,
  onUpdate,
}: AssignmentFormModalProps) {
  const isEdit = !!assignment;
  const [user, setUser] = useState<PickedUser | null>(fixedUser ?? null);
  const [roleId, setRoleId] = useState(fixedRole?.id ?? "");
  const [scopeType, setScopeType] = useState<ScopeType>("Global");
  const [scopeId, setScopeId] = useState("");
  const [validFrom, setValidFrom] = useState(isEdit ? toDateInput(assignment.validFrom) : "");
  const [validTo, setValidTo] = useState(isEdit ? exclusiveIsoToLastDayInput(assignment.validTo) : "");
  const [note, setNote] = useState(assignment?.note ?? "");

  const role = fixedRole ?? roles.find((r) => r.id === roleId);
  const globalOnlyPermissions = useMemo(
    () => (role ? role.permissionCodes.filter((code) => globalOnlyCodes.has(code)) : []),
    [role, globalOnlyCodes]
  );
  const mustBeGlobal = globalOnlyPermissions.length > 0;
  const scopeOptions = (scopeType === "Department" ? departments : partyCells).filter((item) => item.isActive);
  const effectiveScope: ScopeType = mustBeGlobal ? "Global" : scopeType;
  const dateError = !!validFrom && !!validTo && validTo < validFrom;
  const changed = isEdit
    ? validFrom !== toDateInput(assignment.validFrom) ||
      validTo !== exclusiveIsoToLastDayInput(assignment.validTo) ||
      note.trim() !== (assignment.note ?? "").trim()
    : !!user && !!role && (effectiveScope === "Global" || !!scopeId);
  const canSubmit = changed && !dateError && !saving;

  const handleSubmit = (event: React.FormEvent) => {
    event.preventDefault();
    if (!canSubmit) return;
    if (isEdit) {
      // Ngày không đổi → gửi lại đúng mốc cũ (không làm tròn về 0 giờ).
      const fromUnchanged = validFrom === toDateInput(assignment.validFrom);
      const toUnchanged = validTo === exclusiveIsoToLastDayInput(assignment.validTo);
      onUpdate?.({
        validFrom: fromUnchanged ? undefined : startOfDayIso(validFrom),
        validTo: toUnchanged ? assignment.validTo ?? undefined : endOfDayExclusiveIso(validTo),
        note: note.trim(),
      });
      return;
    }
    if (!user || !role) return;
    onCreate?.({
      userId: user.id,
      roleId: role.id,
      scopeType: effectiveScope,
      scopeId: effectiveScope === "Global" ? undefined : scopeId,
      validFrom: startOfDayIso(validFrom),
      validTo: endOfDayExclusiveIso(validTo),
      note: note.trim() || undefined,
    });
  };

  return (
    <AdminModal
      title={isEdit ? "Sửa thời hạn phân công" : "Phân công vai trò"}
      onClose={onClose}
      closeDisabled={saving}
      maxWidth={600}
      footer={
        <>
          <button type="button" className="btn btn-outline-secondary" onClick={onClose} disabled={saving}>
            Hủy
          </button>
          <button type="submit" form="assignment-form" className="btn btn-primary" disabled={!canSubmit}>
            {saving && <span className="spinner-border spinner-border-sm me-1" />}
            {isEdit ? "Lưu" : "Phân công"}
          </button>
        </>
      }
    >
      <form id="assignment-form" onSubmit={handleSubmit}>
        {isEdit ? (
          <div className="small mb-3 p-2 rounded border bg-light">
            <div>
              <strong>{assignment.fullName}</strong> <span className="text-secondary">({assignment.username})</span>
            </div>
            <div>
              Vai trò <strong>{assignment.roleName}</strong> — phạm vi <strong>{assignment.scopeName}</strong>
            </div>
          </div>
        ) : (
          <>
            <div className="mb-2">
              <label className="form-label">
                Người được gán <span className="text-danger">*</span>
              </label>
              {fixedUser ? (
                <div className="border rounded px-2 py-1 small">
                  <strong>{fixedUser.fullName}</strong> <span className="text-secondary">({fixedUser.username})</span>
                </div>
              ) : (
                <UserPicker value={user} onChange={setUser} />
              )}
            </div>
            <div className="mb-2">
              <label className="form-label">
                Vai trò <span className="text-danger">*</span>
              </label>
              {fixedRole ? (
                <div className="border rounded px-2 py-1 small fw-semibold">{fixedRole.name}</div>
              ) : (
                <select className="form-select form-select-sm" value={roleId} required onChange={(e) => setRoleId(e.target.value)}>
                  <option value="">— Chọn vai trò —</option>
                  {roles.map((r) => (
                    <option key={r.id} value={r.id}>
                      {r.name}
                    </option>
                  ))}
                </select>
              )}
            </div>
            <div className="row g-2 mb-2">
              <div className="col-12 col-md-5">
                <label className="form-label">Loại phạm vi</label>
                <select
                  className="form-select form-select-sm"
                  value={mustBeGlobal ? "Global" : scopeType}
                  disabled={mustBeGlobal}
                  onChange={(e) => {
                    setScopeType(e.target.value as ScopeType);
                    setScopeId("");
                  }}
                >
                  {SCOPE_TYPES.map((type) => (
                    <option key={type} value={type}>
                      {SCOPE_TYPE_LABELS[type]}
                    </option>
                  ))}
                </select>
              </div>
              <div className="col-12 col-md-7">
                <label className="form-label">Đối tượng phạm vi</label>
                {mustBeGlobal || scopeType === "Global" ? (
                  <div className="form-control form-control-sm bg-light text-secondary">Toàn công ty</div>
                ) : (
                  <select className="form-select form-select-sm" value={scopeId} required onChange={(e) => setScopeId(e.target.value)}>
                    <option value="">— Chọn {scopeType === "Department" ? "đơn vị chính quyền" : "tổ chức Đảng"} —</option>
                    {scopeOptions.map((item) => (
                      <option key={item.id} value={item.id}>
                        {treeLabel(item)} ({item.code})
                      </option>
                    ))}
                  </select>
                )}
              </div>
            </div>
          </>
        )}

        <div className="row g-2 mb-2">
          <div className="col-6">
            <label className="form-label">Hiệu lực từ ngày</label>
            <DateField value={validFrom} onChange={setValidFrom} aria-label="Hiệu lực từ ngày" />
          </div>
          <div className="col-6">
            <label className="form-label">Đến hết ngày</label>
            <DateField value={validTo} onChange={setValidTo} invalid={!!dateError} aria-label="Đến hết ngày" />
            {dateError ? (
              <div className="invalid-feedback d-block">Phải bằng hoặc sau ngày bắt đầu.</div>
            ) : null}
          </div>
        </div>
        <div className="mb-2">
          <label className="form-label">Ghi chú</label>
          <textarea
            className="form-control form-control-sm"
            rows={2}
            maxLength={1000}
            value={note}
            onChange={(e) => setNote(e.target.value)}
          />
        </div>
      </form>
    </AdminModal>
  );
}

export default AssignmentFormModal;
