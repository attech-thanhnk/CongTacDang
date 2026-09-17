"use client";

import React, { createContext, useContext, useState, useCallback, ReactNode } from "react";

export type ToastType = "success" | "error" | "warning" | "info";

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

      {/* Vị trí hiển thị Toast (Top-Right Floating Container) */}
      <div
        className="position-fixed top-0 end-0 p-3"
        style={{ zIndex: 1090, maxWidth: "380px", width: "100%", pointerEvents: "none" }}
      >
        <div className="d-flex flex-column gap-2">
          {toasts.map((t) => {
            const isSuccess = t.type === "success";
            const isError = t.type === "error";
            const isWarning = t.type === "warning";

            const bgColor = isSuccess ? "#f0fdf4" : isError ? "#fef2f2" : isWarning ? "#fffbeb" : "#eff6ff";
            const borderColor = isSuccess ? "#bbf7d0" : isError ? "#fecaca" : isWarning ? "#fde68a" : "#bfdbfe";
            const textColor = isSuccess ? "#15803d" : isError ? "#b91c1c" : isWarning ? "#b45309" : "#1d4ed8";
            const icon = isSuccess
              ? "bi-check-circle-fill"
              : isError
              ? "bi-exclamation-octagon-fill"
              : isWarning
              ? "bi-exclamation-triangle-fill"
              : "bi-info-circle-fill";

            return (
              <div
                key={t.id}
                className="toast show shadow-sm border rounded-3 p-3 transition"
                style={{
                  backgroundColor: bgColor,
                  borderColor: borderColor,
                  pointerEvents: "auto",
                  animation: "slideInRight 0.22s ease-out",
                }}
              >
                <div className="d-flex align-items-start gap-2.5">
                  <i className={`bi ${icon} fs-5 flex-shrink-0`} style={{ color: textColor }}></i>
                  <div className="flex-grow-1">
                    {t.title && (
                      <div className="fw-semibold text-dark small" style={{ fontSize: "13px" }}>
                        {t.title}
                      </div>
                    )}
                    <div className="text-secondary small mt-0.5" style={{ fontSize: "12px", lineHeight: "1.4" }}>
                      {t.message}
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={() => removeToast(t.id)}
                    className="btn btn-sm btn-link text-muted p-0 ms-1"
                    aria-label="Đóng"
                  >
                    <i className="bi bi-x-lg" style={{ fontSize: "11px" }}></i>
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Modal Xác nhận (Confirm Dialog) */}
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
