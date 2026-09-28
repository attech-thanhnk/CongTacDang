"use client";

import React, { useEffect, useState } from "react";
import { STEP_NAMES, WorkflowStepCode } from "@/services/evaluationService";

interface Props {
  isOpen: boolean;
  title: string;
  description?: string;
  confirmText: string;
  /** Với mở lại hồ sơ: các bước được chọn để quay về (do máy chủ trả về trong `actions`). */
  targetSteps?: WorkflowStepCode[] | null;
  busy?: boolean;
  onCancel: () => void;
  onConfirm: (reason: string, targetStep?: WorkflowStepCode) => void;
}

/** Hộp nhập lý do bắt buộc khi trả lại hoặc mở lại hồ sơ. */
export function ReasonDialog({ isOpen, title, description, confirmText, targetSteps, busy, onCancel, onConfirm }: Props) {
  const [reason, setReason] = useState("");
  const [target, setTarget] = useState<WorkflowStepCode | "">("");
  const [touched, setTouched] = useState(false);

  useEffect(() => {
    if (isOpen) {
      setReason("");
      setTouched(false);
      setTarget(targetSteps && targetSteps.length > 0 ? targetSteps[0] : "");
    }
  }, [isOpen, targetSteps]);

  if (!isOpen) return null;
  const missingReason = reason.trim().length === 0;

  return (
    <div className="modal fade show d-block" tabIndex={-1} role="dialog" aria-modal="true" style={{ backgroundColor: "rgba(15, 23, 42, 0.55)", zIndex: 1080 }}>
      <div className="modal-dialog modal-dialog-centered" style={{ maxWidth: 520 }}>
        <div className="modal-content border-0 shadow-lg" style={{ borderRadius: 14 }}>
          <div className="modal-header px-4 py-3">
            <h2 className="modal-title fs-6 fw-bold mb-0">{title}</h2>
            <button type="button" className="btn-close" aria-label="Đóng" onClick={onCancel} disabled={busy} />
          </div>
          <form
            onSubmit={(event) => {
              event.preventDefault();
              setTouched(true);
              if (missingReason) return;
              onConfirm(reason.trim(), target || undefined);
            }}
          >
            <div className="modal-body px-4 py-3">
              {description && <p className="small text-secondary">{description}</p>}
              {targetSteps && targetSteps.length > 0 && (
                <div className="mb-3">
                  <label className="form-label small fw-semibold" htmlFor="reopen-target">Quay về bước</label>
                  <select id="reopen-target" className="form-select form-select-sm" value={target} onChange={(e) => setTarget(e.target.value as WorkflowStepCode)}>
                    {targetSteps.map((step) => (
                      <option key={step} value={step}>{STEP_NAMES[step] || step}</option>
                    ))}
                  </select>
                </div>
              )}
              <label className="form-label small fw-semibold" htmlFor="reason-text">Lý do (bắt buộc)</label>
              <textarea
                id="reason-text"
                className={`form-control ${touched && missingReason ? "is-invalid" : ""}`}
                rows={4}
                maxLength={2000}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                autoFocus
              />
              {touched && missingReason && <div className="invalid-feedback">Hãy nhập lý do để người liên quan biết cần làm gì.</div>}
            </div>
            <div className="modal-footer px-4 py-3">
              <button type="button" className="btn btn-outline-secondary btn-sm" onClick={onCancel} disabled={busy}>Hủy</button>
              <button type="submit" className="btn btn-danger btn-sm" disabled={busy}>
                {busy ? "Đang xử lý..." : confirmText}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
}

export default ReasonDialog;
