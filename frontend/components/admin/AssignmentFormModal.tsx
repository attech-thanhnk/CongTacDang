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
  const [localError, setLocalError] = useState<string | null>(null);

  const role = fixedRole ?? roles.find((r) => r.id === roleId);
  const globalOnlyPermissions = useMemo(
    () => (role ? role.permissionCodes.filter((code) => globalOnlyCodes.has(code)) : []),
    [role, globalOnlyCodes]
  );
  const mustBeGlobal = globalOnlyPermissions.length > 0;
  const scopeOptions = (scopeType === "Department" ? departments : partyCells).filter((item) => item.isActive);

  const handleSubmit = (event: React.FormEvent) => {
    event.preventDefault();
    setLocalError(null);
    if (validFrom && validTo && validTo < validFrom) {
      setLocalError("Ngày kết thúc phải bằng hoặc sau ngày bắt đầu.");
      return;
    }
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
    if (!user) {
      setLocalError("Hãy chọn người được gán.");
      return;
    }
    if (!role) {
      setLocalError("Hãy chọn vai trò.");
      return;
    }
    const effectiveScope: ScopeType = mustBeGlobal ? "Global" : scopeType;
    if (effectiveScope !== "Global" && !scopeId) {
      setLocalError(`Hãy chọn ${effectiveScope === "Department" ? "Phòng / đơn vị" : "Chi bộ"} áp dụng.`);
      return;
    }
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
    <AdminModal title={isEdit ? "Sửa thời hạn bản gán" : "Gán vai trò"} onClose={onClose} closeDisabled={saving} maxWidth={600}>
      <form onSubmit={handleSubmit}>
        {isEdit ? (
          <div className="small mb-3 p-2 rounded border bg-light">
            <div>
              <strong>{assignment.fullName}</strong> <span className="text-secondary">({assignment.username})</span>
            </div>
            <div>
              Vai trò <strong>{assignment.roleName}</strong> — phạm vi <strong>{assignment.scopeName}</strong>
            </div>
            <div className="text-secondary">Không đổi được người, vai trò, phạm vi; cần khác → kết thúc bản gán này và tạo bản gán mới.</div>
          </div>
        ) : (
          <>
            <div className="mb-2">
              <label className="form-label small fw-semibold mb-1">
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
              <label className="form-label small fw-semibold mb-1">
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
              {role?.description && <div className="form-text">{role.description}</div>}
            </div>
            <div className="row g-2 mb-2">
              <div className="col-12 col-md-5">
                <label className="form-label small fw-semibold mb-1">Loại phạm vi</label>
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
                <label className="form-label small fw-semibold mb-1">Đối tượng phạm vi</label>
                {mustBeGlobal || scopeType === "Global" ? (
                  <div className="form-control form-control-sm bg-light text-secondary">Toàn công ty</div>
                ) : (
                  <>
                    <select className="form-select form-select-sm" value={scopeId} required onChange={(e) => setScopeId(e.target.value)}>
                      <option value="">— Chọn {scopeType === "Department" ? "đơn vị chính quyền" : "tổ chức Đảng"} —</option>
                      {scopeOptions.map((item) => (
                        <option key={item.id} value={item.id}>
                          {treeLabel(item)} ({item.code})
                        </option>
                      ))}
                    </select>
                    <div className="form-text">Phạm vi gồm đơn vị được chọn và mọi đơn vị cấp dưới của nó.</div>
                  </>
                )}
              </div>
              {mustBeGlobal && (
                <div className="col-12 form-text mt-0">
                  Vai trò có quyền chỉ áp dụng Toàn công ty ({globalOnlyPermissions.length} quyền) nên chỉ gán được phạm vi Toàn công ty.
                </div>
              )}
            </div>
          </>
        )}

        <div className="row g-2 mb-2">
          <div className="col-6">
            <label className="form-label small fw-semibold mb-1">Hiệu lực từ ngày</label>
            <input type="date" className="form-control form-control-sm" value={validFrom} onChange={(e) => setValidFrom(e.target.value)} />
            <div className="form-text">{isEdit ? "Để trống = giữ nguyên." : "Để trống = ngay bây giờ."}</div>
          </div>
          <div className="col-6">
            <label className="form-label small fw-semibold mb-1">Đến hết ngày</label>
            <input type="date" className="form-control form-control-sm" value={validTo} onChange={(e) => setValidTo(e.target.value)} />
            <div className="form-text">Để trống = không thời hạn.</div>
          </div>
        </div>
        <div className="mb-2">
          <label className="form-label small fw-semibold mb-1">Ghi chú</label>
          <textarea
            className="form-control form-control-sm"
            rows={2}
            maxLength={1000}
            value={note}
            placeholder="VD: theo Quyết định số …"
            onChange={(e) => setNote(e.target.value)}
          />
        </div>
        {localError && <div className="alert alert-danger small py-2 mb-2">{localError}</div>}
        <div className="d-flex justify-content-end gap-2 mt-3">
          <button type="button" className="btn btn-sm btn-light" onClick={onClose} disabled={saving}>
            Hủy
          </button>
          <button type="submit" className="btn btn-sm btn-primary" disabled={saving}>
            {saving && <span className="spinner-border spinner-border-sm me-1" />}
            {isEdit ? "Lưu" : "Gán vai trò"}
          </button>
        </div>
      </form>
    </AdminModal>
  );
}

export default AssignmentFormModal;
