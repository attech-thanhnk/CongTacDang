"use client";

import React, { useEffect, useLayoutEffect, useState, Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { Button } from "@/components/common";
import { LoginPanelPosition, resolveApiUrl, useOrganizationInfo } from "@/services/organizationSettingsService";
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
const LOOK_KEY = "login-look";
const useIsoLayoutEffect = typeof window === "undefined" ? useEffect : useLayoutEffect;

interface Look {
  url: string | null;
  position: LoginPanelPosition;
  width?: number;
}

function readLook(): Look | undefined {
  try {
    const raw = localStorage.getItem(LOOK_KEY);
    return raw ? (JSON.parse(raw) as Look) : undefined;
  } catch {
    return undefined;
  }
}

function saveLook(look: Look) {
  try {
    localStorage.setItem(LOOK_KEY, JSON.stringify(look));
  } catch {
    // Không lưu được thì lần sau chờ API như bình thường.
  }
}

export default function LoginPage() {
  const org = useOrganizationInfo();
  // undefined = chưa biết có ảnh nền không; khi đó chưa vẽ khung để tránh nháy giao diện mặc định.
  const [look, setLook] = useState<Look | undefined>(undefined);

  // Dùng cài đặt lần trước ngay trước khi vẽ, API trả về sau sẽ cập nhật.
  useIsoLayoutEffect(() => {
    const cached = readLook();
    if (cached) setLook(cached);
  }, []);

  useEffect(() => {
    if (!org) return;
    const url = org.loginBackgroundUrl ? resolveApiUrl(org.loginBackgroundUrl) : null;
    setLook((prev) => {
      const next: Look = { url, position: org.loginPanelPosition ?? "center", width: prev?.url === url ? prev.width : undefined };
      saveLook(next);
      return next;
    });
  }, [org]);

  // API lỗi hoặc chậm: vẫn hiện khung đăng nhập mặc định.
  useEffect(() => {
    const timer = setTimeout(() => setLook((prev) => prev ?? { url: null, position: "center" }), 1500);
    return () => clearTimeout(timer);
  }, []);

  const handleImageLoad = (e: React.SyntheticEvent<HTMLImageElement>) => {
    const width = e.currentTarget.naturalWidth;
    setLook((prev) => {
      if (!prev || prev.width === width) return prev;
      const next = { ...prev, width };
      saveLook(next);
      return next;
    });
  };

  if (!look) return <div className={styles.screen} />;

  const form = (
    <Suspense fallback={<div className="spinner-border text-primary" role="status" />}>
      <LoginForm />
    </Suspense>
  );

  if (!look.url) return <div className={styles.screen}>{form}</div>;

  return (
    <div className={`${styles.screen} ${styles.hasImage} ${PANEL_CLASS[look.position] ?? styles.panelCenter}`}>
      <div className={styles.photo} aria-hidden="true">
        <img
          src={look.url}
          alt=""
          onLoad={handleImageLoad}
          className={look.width ? styles.photoVisible : undefined}
          // Không phóng ảnh quá 1,3 lần cỡ gốc.
          style={look.width ? { maxWidth: `min(100vw, ${Math.round(look.width * 1.3)}px)` } : undefined}
        />
      </div>
      {form}
    </div>
  );
}
