"use client";

import React, { useEffect, useMemo, useState } from "react";
import { CatalogItem, treeLabel } from "@/services/catalogService";
import { criteriaService, WeightFrameOptions } from "@/services/criteriaService";
import {
  AccountListItem,
  APPROVAL_AUTHORITY_LABELS,
  ApprovalAuthority,
  CreateAccountPayload,
  EMPTY_GUID,
  UpdateAccountPayload,
} from "@/services/userService";
import { AdminModal } from "./AdminModal";
import { usePermissionScope } from "@/services/authService";
import { toDateInput } from "./adminUtils";
import { DateField } from "@/components/evaluations/DateField";

interface FormState {
  username: string;
  fullName: string;
  /** `yyyy-MM-dd`; "" = chưa nhập. */
  dateOfBirth: string;
  email: string;
  phoneNumber: string;
  partyCardNumber: string;
  positionTitle: string;
  departmentId: string;
  partyCellId: string;
  /** Mã khung tỷ trọng mặc định; "" = chưa chọn. */
  weightFrameCode: string;
  /** Chỉ khi tạo: "" = suy ra từ chức vụ; 1/2 = đặt tay. */
  approvalAuthority: "" | ApprovalAuthority;
  approvalAuthorityReason: string;
}

function initialState(account?: AccountListItem): FormState {
  return {
    username: account?.username ?? "",
    fullName: account?.fullName ?? "",
    dateOfBirth: account?.dateOfBirth ?? "",
    email: account?.email ?? "",
    phoneNumber: account?.phoneNumber ?? "",
    partyCardNumber: account?.partyCardNumber ?? "",
    positionTitle: account?.positionTitle ?? "",
    departmentId: account?.departmentId ?? "",
    partyCellId: account?.partyCellId ?? "",
    weightFrameCode: account?.weightFrameCode ?? "",
    approvalAuthority: "",
    approvalAuthorityReason: "",
  };
}

/** Chỉ hiện mục đang hoạt động, cộng mục hiện tại của tài khoản (kể cả đã ngừng) để không mất giá trị. */
function selectable(items: CatalogItem[], currentId: string) {
  return items.filter((item) => item.isActive || item.id === currentId);
}

export interface AccountFormModalProps {
  /** Không truyền → tạo mới. */
  account?: AccountListItem;
  departments: CatalogItem[];
  partyCells: CatalogItem[];
  saving: boolean;
  onClose: () => void;
  onCreate?: (payload: CreateAccountPayload) => void;
  onUpdate?: (payload: UpdateAccountPayload) => void;
}

/** Hộp thoại tạo / sửa thông tin tài khoản. Tên đăng nhập không đổi được sau khi tạo. */
export function AccountFormModal({ account, departments, partyCells, saving, onClose, onCreate, onUpdate }: AccountFormModalProps) {
  const [form, setForm] = useState<FormState>(() => initialState(account));
  const isEdit = !!account;
  const [frames, setFrames] = useState<WeightFrameOptions | null>(null);
  const [framesError, setFramesError] = useState(false);

  // Danh sách khung tỷ trọng lấy từ bộ tiêu chí đang dùng gần nhất.
  useEffect(() => {
    let active = true;
    criteriaService
      .weightFrames()
      .then((value) => {
        if (active) setFrames(value);
      })
      .catch(() => {
        if (active) setFramesError(true);
      });
    return () => {
      active = false;
    };
  }, []);

  // Ngày sinh: không ở tương lai, không quá 100 năm trước (máy chủ kiểm tra lại).
  const [minBirthDate, maxBirthDate] = useMemo(() => {
    const today = new Date();
    const past = new Date(today);
    past.setFullYear(today.getFullYear() - 100);
    return [toDateInput(past.toISOString()), toDateInput(today.toISOString())];
  }, []);

  const frameOptions = frames?.frames ?? [];
  const currentFrameMissing = !!form.weightFrameCode && !frameOptions.some((f) => f.code === form.weightFrameCode);
  const set = <K extends keyof FormState>(key: K, value: FormState[K]) => setForm((prev) => ({ ...prev, [key]: value }));

  // Quản trị theo phạm vi: tài khoản phải thuộc phạm vi được giao — kiểm tra trước khi gửi.
  const scope = usePermissionScope("system.users.manage");
  const departmentIds = new Set(scope?.departmentIds ?? []);
  const partyCellIds = new Set(scope?.partyCellIds ?? []);
  const scopedDepartments = !scope || scope.isGlobal || departmentIds.size === 0
    ? departments : departments.filter((d) => departmentIds.has(d.id) || d.id === form.departmentId);
  const scopedPartyCells = !scope || scope.isGlobal || partyCellIds.size === 0
    ? partyCells : partyCells.filter((c) => partyCellIds.has(c.id) || c.id === form.partyCellId);
  const outOfScope = !!scope && !scope.isGlobal
    && !(form.departmentId && departmentIds.has(form.departmentId))
    && !(form.partyCellId && partyCellIds.has(form.partyCellId));

  // Sửa: chỉ bật Lưu khi có thay đổi so với lúc mở.
  const [initial] = useState(() => JSON.stringify(initialState(account)));
  const dirty = !isEdit || JSON.stringify(form) !== initial;

  const handleSubmit = (event: React.FormEvent) => {
    event.preventDefault();
    if (outOfScope || !dirty) return;
    if (isEdit) {
      onUpdate?.({
        fullName: form.fullName.trim(),
        dateOfBirth: form.dateOfBirth,
        email: form.email.trim(),
        phoneNumber: form.phoneNumber.trim(),
        partyCardNumber: form.partyCardNumber.trim(),
        positionTitle: form.positionTitle.trim(),
        departmentId: form.departmentId || EMPTY_GUID,
        partyCellId: form.partyCellId || EMPTY_GUID,
        weightFrameCode: form.weightFrameCode,
      });
    } else {
      onCreate?.({
        username: form.username.trim().toLowerCase(),
        fullName: form.fullName.trim(),
        dateOfBirth: form.dateOfBirth || undefined,
        email: form.email.trim() || undefined,
        phoneNumber: form.phoneNumber.trim() || undefined,
        partyCardNumber: form.partyCardNumber.trim() || undefined,
        positionTitle: form.positionTitle.trim() || undefined,
        departmentId: form.departmentId || undefined,
        partyCellId: form.partyCellId || undefined,
        weightFrameCode: form.weightFrameCode || undefined,
        approvalAuthority: form.approvalAuthority || undefined,
        approvalAuthorityReason: form.approvalAuthority ? form.approvalAuthorityReason.trim() || undefined : undefined,
      });
    }
  };

  return (
    <AdminModal
      title={isEdit ? `Sửa tài khoản ${account.username}` : "Tạo tài khoản"}
      onClose={onClose}
      closeDisabled={saving}
      maxWidth={640}
      footer={
        <>
          <button type="button" className="btn btn-outline-secondary" onClick={onClose} disabled={saving}>
            Hủy
          </button>
          <button type="submit" form="account-form" className="btn btn-primary" disabled={saving || outOfScope || !dirty}>
            {saving && <span className="spinner-border spinner-border-sm me-1" />}
            {isEdit ? "Lưu" : "Tạo tài khoản"}
          </button>
        </>
      }
    >
      <form id="account-form" onSubmit={handleSubmit}>
        <div className="row g-2">
          <div className="col-12 col-md-6">
            <label className="form-label">
              Tên đăng nhập {!isEdit && <span className="text-danger">*</span>}
            </label>
            <input
              className="form-control form-control-sm"
              value={form.username}
              disabled={isEdit}
              required={!isEdit}
              pattern="[a-zA-Z0-9._\-]{3,50}"
              title="3–50 ký tự: chữ cái không dấu, số, dấu chấm, gạch dưới, gạch ngang"
              autoComplete="off"
              onChange={(e) => set("username", e.target.value)}
            />
          </div>
          <div className="col-12 col-md-6">
            <label className="form-label">
              Họ và tên <span className="text-danger">*</span>
            </label>
            <input className="form-control form-control-sm" value={form.fullName} required maxLength={200} onChange={(e) => set("fullName", e.target.value)} />
          </div>
          <div className="col-12 col-md-6">
            <label className="form-label" htmlFor="account-date-of-birth">Ngày sinh</label>
            <DateField
              id="account-date-of-birth"
              value={form.dateOfBirth}
              min={minBirthDate}
              max={maxBirthDate}
              onChange={(value) => set("dateOfBirth", value)}
            />
          </div>
          <div className="col-12 col-md-6">
            <label className="form-label">Email</label>
            <input type="email" className="form-control form-control-sm" value={form.email} onChange={(e) => set("email", e.target.value)} />
          </div>
          <div className="col-12 col-md-6">
            <label className="form-label">Số điện thoại</label>
            <input className="form-control form-control-sm" value={form.phoneNumber} onChange={(e) => set("phoneNumber", e.target.value)} />
          </div>
          <div className="col-12 col-md-6">
            <label className="form-label">Chức danh</label>
            <input className="form-control form-control-sm" value={form.positionTitle} onChange={(e) => set("positionTitle", e.target.value)} />
          </div>
          <div className="col-12 col-md-6">
            <label className="form-label">Số thẻ Đảng viên</label>
            <input className="form-control form-control-sm" value={form.partyCardNumber} onChange={(e) => set("partyCardNumber", e.target.value)} />
          </div>
          <div className="col-12 col-md-6">
            <label className="form-label">Đơn vị công tác chính</label>
            <select className="form-select form-select-sm" value={form.departmentId} onChange={(e) => set("departmentId", e.target.value)}>
              <option value="">— Chưa gán —</option>
              {selectable(scopedDepartments, form.departmentId).map((item) => (
                <option key={item.id} value={item.id}>
                  {treeLabel(item)}
                  {item.isActive ? "" : " (ngừng hoạt động)"}
                </option>
              ))}
            </select>
          </div>
          <div className="col-12 col-md-6">
            <label className="form-label">Nơi sinh hoạt Đảng</label>
            <select className="form-select form-select-sm" value={form.partyCellId} onChange={(e) => set("partyCellId", e.target.value)}>
              <option value="">— Chưa gán —</option>
              {selectable(scopedPartyCells, form.partyCellId).map((item) => (
                <option key={item.id} value={item.id}>
                  {treeLabel(item)}
                  {item.isActive ? "" : " (ngừng hoạt động)"}
                </option>
              ))}
            </select>
          </div>
          <div className="col-12 col-md-6">
            <label className="form-label">Khung tỷ trọng mặc định</label>
            <select className="form-select form-select-sm" value={form.weightFrameCode} onChange={(e) => set("weightFrameCode", e.target.value)}>
              <option value="">— Mặc định của bộ tiêu chí —</option>
              {frameOptions.map((frame) => (
                <option key={frame.code} value={frame.code}>
                  {frame.code} — {frame.name}
                </option>
              ))}
              {currentFrameMissing && (
                <option value={form.weightFrameCode}>{form.weightFrameCode} (không có trong bộ tiêu chí hiện hành)</option>
              )}
            </select>
            {framesError && <div className="form-text">Tải danh sách khung tỷ trọng không thành công.</div>}
          </div>
          {isEdit ? (
            <div className="col-12 form-text mt-1">
              Chức vụ và cấp quyết định xếp loại sửa ở thẻ &quot;Chức vụ&quot;.
            </div>
          ) : (
            <>
              <div className="col-12 col-md-6">
                <label className="form-label">Cấp quyết định xếp loại</label>
                <select
                  className="form-select form-select-sm"
                  value={form.approvalAuthority}
                  onChange={(e) => set("approvalAuthority", e.target.value === "" ? "" : Number(e.target.value) === 2 ? 2 : 1)}
                >
                  <option value="">Theo chức vụ</option>
                  {([1, 2] as ApprovalAuthority[]).map((value) => (
                    <option key={value} value={value}>
                      {APPROVAL_AUTHORITY_LABELS[value]}
                    </option>
                  ))}
                </select>
              </div>
              {form.approvalAuthority !== "" && (
                <div className="col-12 col-md-6">
                  <label className="form-label">Lý do</label>
                  <input
                    className="form-control form-control-sm"
                    value={form.approvalAuthorityReason}
                    required
                    maxLength={1000}
                    onChange={(e) => set("approvalAuthorityReason", e.target.value)}
                  />
                </div>
              )}
            </>
          )}
        </div>
        {outOfScope && (
          <div className="small text-danger mt-2">
            Chọn đơn vị công tác hoặc nơi sinh hoạt Đảng thuộc phạm vi bạn được giao quản lý tài khoản.
          </div>
        )}
      </form>
    </AdminModal>
  );
}

export default AccountFormModal;
