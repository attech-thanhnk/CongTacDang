"use client";

import React from "react";

export interface AdminModalProps {
  title: React.ReactNode;
  onClose: () => void;
  children: React.ReactNode;
  footer?: React.ReactNode;
  maxWidth?: number;
  /** Không cho đóng bằng nút X (vd. khi đang lưu). */
  closeDisabled?: boolean;
}

/** Hộp thoại Bootstrap dùng chung cho các trang quản trị. */
export function AdminModal({ title, onClose, children, footer, maxWidth = 560, closeDisabled }: AdminModalProps) {
  return (
    <div className="modal show d-block bg-dark bg-opacity-50" tabIndex={-1} role="dialog" aria-modal="true">
      <div className="modal-dialog modal-dialog-centered modal-dialog-scrollable" style={{ maxWidth }}>
        <div className="modal-content shadow border-0">
          <div className="modal-header py-2 px-3">
            <h6 className="modal-title fw-bold mb-0">{title}</h6>
            <button type="button" className="btn-close" aria-label="Đóng" onClick={onClose} disabled={closeDisabled} />
          </div>
          <div className="modal-body p-3">{children}</div>
          {footer && <div className="modal-footer py-2 px-3">{footer}</div>}
        </div>
      </div>
    </div>
  );
}

export default AdminModal;
