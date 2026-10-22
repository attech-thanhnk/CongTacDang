"use client";

import React, { useState, Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { Button } from "@/components/common";
import { useOrganizationInfo } from "@/services/organizationSettingsService";
import { AuthBrand } from "./AuthBrand";
import styles from "./auth.module.css";
import { errorMessage } from "@/components/admin/adminUtils";

/** Chỉ quay lại đường dẫn nội bộ (chặn chuyển hướng ra ngoài như "//host" hay "https://…"), bỏ qua trang đăng nhập. */
function safeReturnUrl(value: string | null): string {
  if (!value || !value.startsWith("/") || value.startsWith("//") || value.startsWith("/\\")) return "/evaluations";
  if (value === "/login" || value.startsWith("/login?")) return "/evaluations";
  return value;
}

function LoginForm() {
  const { login } = useAuth();
  const org = useOrganizationInfo();
  const router = useRouter();
  const searchParams = useSearchParams();
  const returnUrl = safeReturnUrl(searchParams.get("returnUrl"));

  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);
    setIsSubmitting(true);

    try {
      await login(username.trim(), password);
      router.replace(returnUrl);
    } catch (err: unknown) {
      setErrorMsg(errorMessage(err, "Tên đăng nhập hoặc mật khẩu không chính xác."));
    } finally {
      setIsSubmitting(false);
    }
  };

  const footer = org ? [org.companyName, org.parentCompanyName].filter(Boolean).join(" · ") : "";

  return (
    <div className={styles.card}>
      <AuthBrand />

      {errorMsg && (
        <div className="alert alert-danger py-2 px-3 mb-3 d-flex align-items-center gap-2" role="alert">
          <i className="bi bi-exclamation-octagon-fill flex-shrink-0" />
          <span>{errorMsg}</span>
        </div>
      )}

      <form onSubmit={handleSubmit} className={styles.form}>
        <div>
          <label className="form-label" htmlFor="login-username">Tên đăng nhập</label>
          <input
            id="login-username"
            type="text"
            required
            autoFocus
            autoComplete="username"
            placeholder="Tên đăng nhập hoặc email"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            className="form-control"
          />
        </div>

        <div>
          <label className="form-label" htmlFor="login-password">Mật khẩu</label>
          <input
            id="login-password"
            type="password"
            required
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="form-control"
          />
        </div>

        <div className="form-check mb-0">
          <input className="form-check-input" type="checkbox" id="rememberMe" defaultChecked />
          <label className="form-check-label text-secondary" htmlFor="rememberMe">
            Ghi nhớ phiên làm việc trên thiết bị này
          </label>
        </div>

        <Button type="submit" variant="primary" loading={isSubmitting} loadingText="Đang xác thực..." className={styles.submit}>
          Đăng nhập
        </Button>
      </form>

      {footer && <div className={styles.foot}>{footer}</div>}
    </div>
  );
}

export default function LoginPage() {
  return (
    <div className={styles.screen}>
      <Suspense fallback={<div className="spinner-border text-primary" role="status" />}>
        <LoginForm />
      </Suspense>
    </div>
  );
}
