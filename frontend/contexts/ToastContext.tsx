"use client";

import React, { createContext, useContext, useState, useCallback, ReactNode } from "react";
import toastStyles from "./toast.module.css";

export type ToastType = "success" | "error" | "warning" | "info";

const TOAST_ICON: Record<ToastType, string> = {
  success: "bi-check-lg",
  error: "bi-exclamation-lg",
  warning: "bi-exclamation-lg",
  info: "bi-info-lg",
};

export interface ToastItem {
  id: string;
  type: ToastType;
  message: string;
  title?: string;
  duration?: number;
}

export interface ConfirmOptions {
  title?: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  isDanger?: boolean;
  onConfirm: () => void | Promise<void>;
}

interface ToastContextType {
  toast: {
    success: (message: string, title?: string, duration?: number) => void;
    error: (message: string, title?: string, duration?: number) => void;
    warning: (message: string, title?: string, duration?: number) => void;
    info: (message: string, title?: string, duration?: number) => void;
  };
  confirm: (options: ConfirmOptions) => void;
}

const ToastContext = createContext<ToastContextType | undefined>(undefined);

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const [confirmModal, setConfirmModal] = useState<ConfirmOptions | null>(null);
  const [confirmLoading, setConfirmLoading] = useState(false);

  const removeToast = useCallback((id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const addToast = useCallback(
    (type: ToastType, message: string, title?: string, duration = 3500) => {
      const id = `${Date.now()}-${Math.random().toString(36).substring(2, 9)}`;
      const newToast: ToastItem = { id, type, message, title, duration };

      setToasts((prev) => [...prev, newToast]);

      if (duration > 0) {
        setTimeout(() => {
          removeToast(id);
        }, duration);
      }
    },
    [removeToast]
  );

  const toast = {
    success: useCallback(
      (message: string, title?: string, duration?: number) =>
        addToast("success", message, title || "Thành công", duration),
      [addToast]
    ),
    error: useCallback(
      (message: string, title?: string, duration?: number) =>
        addToast("error", message, title || "Lỗi thao tác", duration),
      [addToast]
    ),
    warning: useCallback(
      (message: string, title?: string, duration?: number) =>
        addToast("warning", message, title || "Cảnh báo", duration),
      [addToast]
    ),
    info: useCallback(
      (message: string, title?: string, duration?: number) =>
        addToast("info", message, title || "Thông tin", duration),
      [addToast]
    ),
  };

  const confirm = useCallback((options: ConfirmOptions) => {
    setConfirmModal(options);
  }, []);

  const handleConfirmSubmit = async () => {
    if (!confirmModal) return;
    setConfirmLoading(true);
    try {
      await confirmModal.onConfirm();
      setConfirmModal(null);
    } catch {
      // lỗi xử lý trong onConfirm
    } finally {
      setConfirmLoading(false);
    }
  };

  return (
    <ToastContext.Provider value={{ toast, confirm }}>
      {children}

      {/* Toast góc trên phải */}
      <div className={toastStyles.stack} aria-live="polite">
        {toasts.map((t) => (
          <div key={t.id} className={`${toastStyles.toast} ${toastStyles[t.type] ?? ""}`} role={t.type === "error" ? "alert" : "status"}>
            <span className={toastStyles.icon} aria-hidden="true">
              <i className={`bi ${TOAST_ICON[t.type]}`} />
            </span>
            <div className={toastStyles.body}>
              {t.title && <div className={toastStyles.title}>{t.title}</div>}
              <div className={toastStyles.message}>{t.message}</div>
            </div>
            <button type="button" onClick={() => removeToast(t.id)} className={toastStyles.close} aria-label="Đóng">
              <i className="bi bi-x-lg" />
            </button>
          </div>
        ))}
      </div>

      {/* Hộp xác nhận */}
      {confirmModal && (
        <>
          <div
            className="position-fixed top-0 start-0 w-100 h-100 bg-dark bg-opacity-50"
            style={{ zIndex: 1080, backdropFilter: "blur(2px)" }}
            onClick={() => !confirmLoading && setConfirmModal(null)}
          />
          <div
            className="position-fixed top-50 start-50 translate-middle w-100 p-3"
            style={{ zIndex: 1085, maxWidth: "420px" }}
          >
            <div className="card shadow-lg border rounded-3 bg-white" style={{ borderColor: "#e2e8f0" }}>
              <div className="card-body p-4">
                <div className="d-flex align-items-center gap-2.5 mb-2.5">
                  <div
                    className="rounded-circle d-flex align-items-center justify-content-center shrink-0"
                    style={{
                      width: "36px",
                      height: "36px",
                      backgroundColor: confirmModal.isDanger !== false ? "#fef2f2" : "#eff6ff",
                      color: confirmModal.isDanger !== false ? "#b91c1c" : "#1e40af",
                    }}
                  >
                    <i
                      className={`bi ${
                        confirmModal.isDanger !== false ? "bi-exclamation-triangle" : "bi-question-circle"
                      } fs-5`}
                    ></i>
                  </div>
                  <h5 className="fw-bold text-dark mb-0 fs-6">
                    {confirmModal.title || "Xác nhận thao tác"}
                  </h5>
                </div>

                <p className="text-secondary small mb-4 lh-base" style={{ fontSize: "13px" }}>
                  {confirmModal.message}
                </p>

                <div className="d-flex justify-content-end gap-2">
                  <button
                    type="button"
                    disabled={confirmLoading}
                    onClick={() => setConfirmModal(null)}
                    className="btn btn-sm btn-outline-secondary px-3"
                  >
                    {confirmModal.cancelText || "Hủy bỏ"}
                  </button>
                  <button
                    type="button"
                    disabled={confirmLoading}
                    onClick={handleConfirmSubmit}
                    className={`btn btn-sm ${
                      confirmModal.isDanger !== false ? "btn-danger" : "btn-primary"
                    } px-3 fw-medium`}
                  >
                    {confirmLoading ? (
                      <>
                        <span className="spinner-border spinner-border-sm me-1" role="status"></span>
                        <span>Đang xử lý...</span>
                      </>
                    ) : (
                      confirmModal.confirmText || "Đồng ý"
                    )}
                  </button>
                </div>
              </div>
            </div>
          </div>
        </>
      )}
    </ToastContext.Provider>
  );
}

export function useToast() {
  const context = useContext(ToastContext);
  if (!context) {
    throw new Error("useToast must be used within a ToastProvider");
  }
  return context;
}
