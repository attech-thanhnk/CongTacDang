"use client";

import React, { useState, Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { Button } from "@/components/common";

function LoginForm() {
  const { login } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const returnUrl = searchParams.get("returnUrl") || "/evaluations";

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
    } catch (err: any) {
      setErrorMsg(err.message || "Tên đăng nhập hoặc mật khẩu không chính xác.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div
      className="card border w-100 shadow-sm"
      style={{ maxWidth: "400px", borderRadius: "12px", borderColor: "#e2e8f0" }}
    >
      <div className="p-4 pb-3 border-bottom text-center bg-white" style={{ borderColor: "#e2e8f0" }}>
        <h1 className="fw-bold text-primary text-uppercase mb-0" style={{ fontSize: "17px", letterSpacing: "0.5px" }}>
          Đảng bộ ATTECH
        </h1>
      </div>

      <div className="card-body p-4 bg-white">
        {errorMsg && (
          <div
            className="alert alert-danger py-2 px-3 small mb-3 d-flex align-items-center gap-2"
            style={{ fontSize: "12px", borderRadius: "6px" }}
          >
            <i className="bi bi-exclamation-octagon-fill text-danger flex-shrink-0"></i>
            <span>{errorMsg}</span>
          </div>
        )}

        <form onSubmit={handleSubmit}>
          <div className="mb-3">
            <label className="form-label small fw-semibold text-secondary mb-1">
              Tên đăng nhập
            </label>
            <div className="input-group input-group-sm">
              <span className="input-group-text bg-light text-muted border-end-0">
                <i className="bi bi-person"></i>
              </span>
              <input
                type="text"
                required
                autoFocus
                autoComplete="username"
                placeholder="Tên đăng nhập hoặc email"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                className="form-control border-start-0 ps-0"
              />
            </div>
          </div>

          <div className="mb-3">
            <label className="form-label small fw-semibold text-secondary mb-1">
              Mật khẩu
            </label>
            <div className="input-group input-group-sm">
              <span className="input-group-text bg-light text-muted border-end-0">
                <i className="bi bi-lock"></i>
              </span>
              <input
                type="password"
                required
                autoComplete="current-password"
                placeholder="Mật khẩu"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="form-control border-start-0 ps-0"
              />
            </div>
          </div>

          <div className="form-check mb-3">
            <input className="form-check-input" type="checkbox" id="rememberMe" defaultChecked />
            <label className="form-check-label text-secondary small" htmlFor="rememberMe">
              Ghi nhớ phiên làm việc trên thiết bị này
            </label>
          </div>

          <Button
            type="submit"
            variant="primary"
            loading={isSubmitting}
            loadingText="Đang xác thực..."
            icon="bi-box-arrow-in-right"
            className="w-100 py-2 shadow-sm"
          >
            Đăng nhập hệ thống
          </Button>
        </form>

        <div className="text-center mt-4 pt-2 border-top text-muted" style={{ fontSize: "11px", borderColor: "#f1f5f9" }}>
          Công ty TNHH Kỹ thuật Quản lý bay • Tổng công ty Quản lý bay Việt Nam
        </div>
      </div>
    </div>
  );
}

export default function LoginPage() {
  return (
    <div
      className="d-flex align-items-center justify-content-center min-vh-100 px-3 py-5"
      style={{
        backgroundColor: "#f8fafc",
        backgroundImage: "radial-gradient(#cbd5e1 0.75px, transparent 0.75px)",
        backgroundSize: "16px 16px",
      }}
    >
      <Suspense fallback={<div className="spinner-border text-primary" role="status"></div>}>
        <LoginForm />
      </Suspense>
    </div>
  );
}
