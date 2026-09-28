"use client";

import React, { useState } from "react";
import { AdminModal } from "./AdminModal";
import { copyText } from "./adminUtils";

export interface TemporaryCredential {
  title: string;
  fullName?: string;
  username: string;
  temporaryPassword: string;
}

/**
 * Hiện tên đăng nhập + mật khẩu tạm đúng một lần (sau khi tạo tài khoản / đặt lại mật khẩu).
 * Mật khẩu chỉ nằm trong state của hộp thoại; đóng là mất, không lưu ở đâu khác.
 */
export function TemporaryPasswordModal({ credential, onClose }: { credential: TemporaryCredential; onClose: () => void }) {
  const [copied, setCopied] = useState<"username" | "password" | "both" | null>(null);
  const [failed, setFailed] = useState(false);

  const handleCopy = async (kind: "username" | "password" | "both") => {
    const text =
      kind === "username"
        ? credential.username
        : kind === "password"
          ? credential.temporaryPassword
          : `Tên đăng nhập: ${credential.username}\nMật khẩu tạm: ${credential.temporaryPassword}`;
    const ok = await copyText(text);
    setFailed(!ok);
    setCopied(ok ? kind : null);
  };

  return (
    <AdminModal
      title={credential.title}
      onClose={onClose}
      maxWidth={500}
      footer={
        <>
          <button type="button" className="btn btn-sm btn-outline-primary" onClick={() => handleCopy("both")}>
            <i className={`bi ${copied === "both" ? "bi-check2" : "bi-clipboard"} me-1`} />
            Sao chép cả hai
          </button>
          <button type="button" className="btn btn-sm btn-primary" onClick={onClose}>
            Tôi đã lưu, đóng
          </button>
        </>
      }
    >
      <div className="alert alert-warning small py-2 d-flex gap-2" role="alert">
        <i className="bi bi-exclamation-triangle-fill" />
        <div>
          Mật khẩu tạm <strong>chỉ hiển thị một lần</strong>. Đóng hộp thoại này là không xem lại được — hãy sao chép và
          giao cho cán bộ qua kênh an toàn. Cán bộ phải đổi mật khẩu ở lần đăng nhập đầu tiên.
        </div>
      </div>
      {credential.fullName && <div className="small text-secondary mb-2">Cán bộ: <strong className="text-dark">{credential.fullName}</strong></div>}
      {(
        [
          { kind: "username" as const, label: "Tên đăng nhập", value: credential.username },
          { kind: "password" as const, label: "Mật khẩu tạm", value: credential.temporaryPassword },
        ]
      ).map((field) => (
        <div className="mb-2" key={field.kind}>
          <label className="form-label small fw-semibold mb-1">{field.label}</label>
          <div className="input-group input-group-sm">
            <input
              className="form-control font-monospace"
              value={field.value}
              readOnly
              onFocus={(e) => e.currentTarget.select()}
              aria-label={field.label}
            />
            <button type="button" className="btn btn-outline-secondary" onClick={() => handleCopy(field.kind)}>
              <i className={`bi ${copied === field.kind ? "bi-check2 text-success" : "bi-clipboard"} me-1`} />
              {copied === field.kind ? "Đã sao chép" : "Sao chép"}
            </button>
          </div>
        </div>
      ))}
      {failed && (
        <div className="small text-danger mt-2">
          Trình duyệt không cho sao chép tự động. Hãy bôi đen ô và nhấn Ctrl+C.
        </div>
      )}
    </AdminModal>
  );
}

export default TemporaryPasswordModal;
