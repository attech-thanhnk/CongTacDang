"use client";

import React, { useEffect, useLayoutEffect, useRef, useState, Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { Button } from "@/components/common";
import { LoginPanelPosition, resolveApiUrl, useOrganizationInfo } from "@/services/organizationSettingsService";
import { useAppVersion, versionLabel } from "@/services/versionService";
import { AuthBrand } from "./AuthBrand";
import styles from "./auth.module.css";
import { errorMessage } from "@/components/admin/adminUtils";

/** Chỉ quay lại đường dẫn nội bộ (chặn chuyển hướng ra ngoài như "//host" hay "https://…"), bỏ qua trang đăng nhập. */
function safeReturnUrl(value: string | null): string {
  if (!value || !value.startsWith("/") || value.startsWith("//") || value.startsWith("/\\")) return "/evaluations";
  if (value === "/login" || value.startsWith("/login?")) return "/evaluations";
  return value;
}

const SUCCESS_DELAY_MS = 600;

function prefersReducedMotion() {
  return typeof window !== "undefined" && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

function LoginForm() {
  const { login } = useAuth();
  const { toast } = useToast();
  const org = useOrganizationInfo();
  const version = useAppVersion();
  const router = useRouter();
  const searchParams = useSearchParams();
  const returnUrl = safeReturnUrl(searchParams.get("returnUrl"));
  const passwordRef = useRef<HTMLInputElement>(null);

  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [succeeded, setSucceeded] = useState(false);
  const [shaking, setShaking] = useState(false);
  const [entered, setEntered] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);

    try {
      const session = await login(username.trim(), password);
      // Phải đổi mật khẩu: AuthGuard tự chuyển sang trang đổi mật khẩu.
      if (session.mustChangePassword) return;
      setSucceeded(true);
      setTimeout(() => router.replace(returnUrl), prefersReducedMotion() ? 0 : SUCCESS_DELAY_MS);
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Tên đăng nhập hoặc mật khẩu không chính xác."), "Đăng nhập không thành công");
      setPassword("");
      setShaking(true);
      passwordRef.current?.focus();
    } finally {
      setIsSubmitting(false);
    }
  };

  // Gỡ lớp hiệu ứng khi chạy xong để lắc báo lỗi không phát lại hiệu ứng vào trang.
  const handleAnimationEnd = (e: React.AnimationEvent) => {
    if (e.animationName.includes("cardIn")) setEntered(true);
    if (e.animationName.includes("shake")) setShaking(false);
  };

  const footer = org ? [org.companyName, org.parentCompanyName].filter(Boolean).join(" · ") : "";
  const cardClass = [styles.card, entered ? "" : styles.enter, shaking ? styles.shake : "", succeeded ? styles.leave : ""]
    .filter(Boolean)
    .join(" ");

  return (
    <div className={cardClass} onAnimationEnd={handleAnimationEnd}>
      <AuthBrand title="Đăng nhập" />

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
            ref={passwordRef}
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

        {succeeded ? (
          <Button type="button" variant="success" disabled className={`${styles.submit} ${styles.done}`}>
            Đăng nhập thành công
          </Button>
        ) : (
          <Button type="submit" variant="primary" loading={isSubmitting} loadingText="Đang xác thực..." className={styles.submit}>
            Đăng nhập
          </Button>
        )}
      </form>

      {footer && <div className={styles.foot}>{footer}</div>}
      {version && <div className={styles.version} title={`Dựng lúc ${version.buildTime}`}>{versionLabel(version)}</div>}
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
