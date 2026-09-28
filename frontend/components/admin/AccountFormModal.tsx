"use client";

import React, { useState } from "react";
import type { CatalogItem } from "@/services/catalogService";
import {
  AccountListItem,
  APPROVAL_AUTHORITY_LABELS,
  ApprovalAuthority,
  CreateAccountPayload,
  EMPTY_GUID,
  UpdateAccountPayload,
} from "@/services/userService";
import { AdminModal } from "./AdminModal";

interface FormState {
  username: string;
  fullName: string;
  email: string;
  phoneNumber: string;
  partyCardNumber: string;
  positionTitle: string;
  departmentId: string;
  partyCellId: string;
  approvalAuthority: ApprovalAuthority;
}

function initialState(account?: AccountListItem): FormState {
  return {
    username: account?.username ?? "",
    fullName: account?.fullName ?? "",
    email: account?.email ?? "",
    phoneNumber: account?.phoneNumber ?? "",
    partyCardNumber: account?.partyCardNumber ?? "",
    positionTitle: account?.positionTitle ?? "",
    departmentId: account?.departmentId ?? "",
    partyCellId: account?.partyCellId ?? "",
    approvalAuthority: account?.approvalAuthority ?? 1,
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
  const set = <K extends keyof FormState>(key: K, value: FormState[K]) => setForm((prev) => ({ ...prev, [key]: value }));

  const handleSubmit = (event: React.FormEvent) => {
    event.preventDefault();
    if (isEdit) {
      onUpdate?.({
        fullName: form.fullName.trim(),
        email: form.email.trim(),
        phoneNumber: form.phoneNumber.trim(),
        partyCardNumber: form.partyCardNumber.trim(),
        positionTitle: form.positionTitle.trim(),
        departmentId: form.departmentId || EMPTY_GUID,
        partyCellId: form.partyCellId || EMPTY_GUID,
        approvalAuthority: form.approvalAuthority,
      });
    } else {
      onCreate?.({
        username: form.username.trim().toLowerCase(),
        fullName: form.fullName.trim(),
        email: form.email.trim() || undefined,
        phoneNumber: form.phoneNumber.trim() || undefined,
        partyCardNumber: form.partyCardNumber.trim() || undefined,
        positionTitle: form.positionTitle.trim() || undefined,
        departmentId: form.departmentId || undefined,
        partyCellId: form.partyCellId || undefined,
        approvalAuthority: form.approvalAuthority,
      });
    }
  };

  return (
    <AdminModal
      title={isEdit ? `Sửa tài khoản ${account.username}` : "Tạo tài khoản"}
      onClose={onClose}
      closeDisabled={saving}
      maxWidth={640}
    >
      <form onSubmit={handleSubmit}>
        <div className="row g-2">
          <div className="col-12 col-md-6">
            <label className="form-label small fw-semibold mb-1">
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
            <div className="form-text">{isEdit ? "Không đổi được tên đăng nhập." : "3–50 ký tự a-z, 0-9, dấu . _ -"}</div>
          </div>
          <div className="col-12 col-md-6">
            <label className="form-label small fw-semibold mb-1">
              Họ và tên <span className="text-danger">*</span>
            </label>
            <input className="form-control form-control-sm" value={form.fullName} required maxLength={200} onChange={(e) => set("fullName", e.target.value)} />
          </div>
          <div className="col-12 col-md-6">
            <label className="form-label small fw-semibold mb-1">Email</label>
            <input type="email" className="form-control form-control-sm" value={form.email} onChange={(e) => set("email", e.target.value)} />
          </div>
          <div className="col-12 col-md-6">
            <label className="form-label small fw-semibold mb-1">Số điện thoại</label>
            <input className="form-control form-control-sm" value={form.phoneNumber} onChange={(e) => set("phoneNumber", e.target.value)} />
          </div>
          <div className="col-12 col-md-6">
            <label className="form-label small fw-semibold mb-1">Chức danh</label>
            <input className="form-control form-control-sm" value={form.positionTitle} onChange={(e) => set("positionTitle", e.target.value)} />
          </div>
          <div className="col-12 col-md-6">
            <label className="form-label small fw-semibold mb-1">Số thẻ Đảng viên</label>
            <input className="form-control form-control-sm" value={form.partyCardNumber} onChange={(e) => set("partyCardNumber", e.target.value)} />
            <div className="form-text">Có số thẻ → là Đảng viên; để trống → không phải Đảng viên.</div>
          </div>
          <div className="col-12 col-md-6">
            <label className="form-label small fw-semibold mb-1">Phòng / đơn vị</label>
            <select className="form-select form-select-sm" value={form.departmentId} onChange={(e) => set("departmentId", e.target.value)}>
              <option value="">— Chưa gán —</option>
              {selectable(departments, form.departmentId).map((item) => (
                <option key={item.id} value={item.id}>
                  {item.name}
                  {item.isActive ? "" : " (ngừng hoạt động)"}
                </option>
              ))}
            </select>
          </div>
          <div className="col-12 col-md-6">
            <label className="form-label small fw-semibold mb-1">Chi bộ sinh hoạt</label>
            <select className="form-select form-select-sm" value={form.partyCellId} onChange={(e) => set("partyCellId", e.target.value)}>
              <option value="">— Chưa gán —</option>
              {selectable(partyCells, form.partyCellId).map((item) => (
                <option key={item.id} value={item.id}>
                  {item.name}
                  {item.isActive ? "" : " (ngừng hoạt động)"}
                </option>
              ))}
            </select>
          </div>
          <div className="col-12">
            <label className="form-label small fw-semibold mb-1">Cấp quyết định xếp loại</label>
            <select
              className="form-select form-select-sm"
              value={form.approvalAuthority}
              onChange={(e) => set("approvalAuthority", Number(e.target.value) === 2 ? 2 : 1)}
            >
              {([1, 2] as ApprovalAuthority[]).map((value) => (
                <option key={value} value={value}>
                  {APPROVAL_AUTHORITY_LABELS[value]}
                </option>
              ))}
            </select>
          </div>
        </div>
        <div className="d-flex justify-content-end gap-2 mt-3">
          <button type="button" className="btn btn-sm btn-light" onClick={onClose} disabled={saving}>
            Hủy
          </button>
          <button type="submit" className="btn btn-sm btn-primary" disabled={saving}>
            {saving && <span className="spinner-border spinner-border-sm me-1" />}
            {isEdit ? "Lưu thay đổi" : "Tạo tài khoản"}
          </button>
        </div>
      </form>
    </AdminModal>
  );
}

export default AccountFormModal;
