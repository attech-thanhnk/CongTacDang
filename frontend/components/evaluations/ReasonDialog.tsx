"use client";

import React, { useEffect, useState } from "react";
import { RecordActionDto, STEP_NAMES, WorkflowStepCode } from "@/services/evaluationService";
import { formatDateTime } from "@/lib/dateFormat";

/** Thông tin hạn mở riêng hồ sơ (kỳ khóa dữ liệu) lấy từ hành động máy chủ trả về. */
export type UnlockDeadlineInfo = Pick<
  RecordActionDto,
  "requiresUnlockDeadline" | "unlockDeadlineSteps" | "currentUnlockedUntil" | "defaultUnlockDays" | "maxUnlockDays"
>;

interface Props {
  isOpen: boolean;
  title: string;
  description?: string;
  confirmText: string;
  /** Với mở lại hồ sơ: các bước được chọn để quay về (do máy chủ trả về trong `actions`). */
  targetSteps?: WorkflowStepCode[] | null;
  /** Hạn mở riêng hồ sơ khi kỳ khóa dữ liệu (do máy chủ báo trong `actions`). */
  unlock?: UnlockDeadlineInfo | null;
  busy?: boolean;
  onCancel: () => void;
  onConfirm: (reason: string, targetStep?: WorkflowStepCode, unlockUntil?: string) => void;
}

const pad = (n: number) => String(n).padStart(2, "0");

/** Giá trị cho ô `datetime-local` (giờ máy người dùng). */
function toLocalInput(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function defaultDeadline(days: number): string {
  const date = new Date();
  date.setDate(date.getDate() + days);
  date.setHours(17, 0, 0, 0);
  return toLocalInput(date);
}

/** Cần nhập hạn mở riêng với bước quay về đang chọn. */
function needsDeadline(unlock: UnlockDeadlineInfo | null | undefined, target: WorkflowStepCode | ""): boolean {
  if (!unlock?.requiresUnlockDeadline) return false;
  if (unlock.unlockDeadlineSteps && unlock.unlockDeadlineSteps.length > 0) return !!target && unlock.unlockDeadlineSteps.includes(target);
  return true;
}

/** Hộp nhập lý do bắt buộc khi trả lại hoặc mở lại hồ sơ (kèm hạn mở riêng khi kỳ khóa dữ liệu). */
export function ReasonDialog({ isOpen, title, description, confirmText, targetSteps, unlock, busy, onCancel, onConfirm }: Props) {
  const [reason, setReason] = useState("");
  const [target, setTarget] = useState<WorkflowStepCode | "">("");
  const [deadline, setDeadline] = useState("");
  const [touched, setTouched] = useState(false);

  const defaultDays = unlock?.defaultUnlockDays ?? 7;
  const maxDays = unlock?.maxUnlockDays ?? 60;

  useEffect(() => {
    if (isOpen) {
      setReason("");
      setTouched(false);
      setTarget(targetSteps && targetSteps.length > 0 ? targetSteps[0] : "");
      setDeadline(defaultDeadline(defaultDays));
    }
  }, [isOpen, targetSteps, defaultDays]);

  if (!isOpen) return null;
  const missingReason = reason.trim().length === 0;
  const showDeadline = needsDeadline(unlock, target);
  const deadlineDate = deadline ? new Date(deadline) : null;
  const maxDate = new Date(Date.now() + maxDays * 24 * 60 * 60 * 1000);
  const deadlineError = !showDeadline
    ? null
    : !deadlineDate || Number.isNaN(deadlineDate.getTime())
      ? "Hãy chọn hạn mở riêng hồ sơ."
      : deadlineDate.getTime() <= Date.now()
        ? "Hạn phải sau thời điểm hiện tại."
        : deadlineDate > maxDate
          ? `Hạn không được quá ${maxDays} ngày kể từ hôm nay.`
          : null;

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
              if (missingReason || deadlineError) return;
              onConfirm(reason.trim(), target || undefined, showDeadline && deadlineDate ? deadlineDate.toISOString() : undefined);
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
              {showDeadline && (
                <div className="mb-3">
                  <label className="form-label small fw-semibold" htmlFor="unlock-deadline">Mở riêng hồ sơ đến</label>
                  <input
                    id="unlock-deadline"
                    type="datetime-local"
                    className={`form-control form-control-sm ${touched && deadlineError ? "is-invalid" : ""}`}
                    value={deadline}
                    onChange={(e) => setDeadline(e.target.value)}
                  />
                  {touched && deadlineError ? (
                    <div className="invalid-feedback">{deadlineError}</div>
                  ) : (
                    <div className="form-text">
                      Kỳ đang khóa dữ liệu: chỉ hồ sơ này được thao tác lại đến hạn trên.
                      {unlock?.currentUnlockedUntil ? ` Hạn hiện tại: ${formatDateTime(unlock.currentUnlockedUntil)}.` : ""}
                    </div>
                  )}
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
