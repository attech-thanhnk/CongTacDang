"use client";

import React, { useEffect, useState, Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { Button } from "@/components/common";
import { resolveApiUrl, useOrganizationInfo } from "@/services/organizationSettingsService";
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

const PANEL_CLASS = { center: styles.panelCenter, left: styles.panelLeft, right: styles.panelRight } as const;

export default function LoginPage() {
  const org = useOrganizationInfo();
  const imageUrl = org?.loginBackgroundUrl ? resolveApiUrl(org.loginBackgroundUrl) : null;
  const [loaded, setLoaded] = useState<{ url: string; width: number } | null>(null);

  // Tải trước ảnh rồi mới hiện dần; lấy chiều rộng gốc để không phóng ảnh quá cỡ.
  useEffect(() => {
    if (!imageUrl) return;
    let active = true;
    const img = new window.Image();
    img.onload = () => {
      if (active) setLoaded({ url: imageUrl, width: img.naturalWidth });
    };
    img.src = imageUrl;
    return () => {
      active = false;
    };
  }, [imageUrl]);

  const ready = !!imageUrl && loaded?.url === imageUrl;
  const screenClass = imageUrl
    ? `${styles.screen} ${styles.hasImage} ${PANEL_CLASS[org?.loginPanelPosition ?? "center"] ?? styles.panelCenter}`
    : styles.screen;

  return (
    <div className={screenClass}>
      {imageUrl && (
        <>
          <div
            className={`${styles.backdrop} ${ready ? styles.backdropVisible : ""}`}
            style={{ backgroundImage: `url("${imageUrl}")` }}
            aria-hidden="true"
          />
          <div className={`${styles.photo} ${ready ? styles.backdropVisible : ""}`} aria-hidden="true">
            {ready && <img src={imageUrl} alt="" style={{ maxWidth: `min(100vw, ${Math.round(loaded.width * 1.3)}px)` }} />}
          </div>
        </>
      )}
      <Suspense fallback={<div className="spinner-border text-primary" role="status" />}>
        <LoginForm />
      </Suspense>
    </div>
  );
}
