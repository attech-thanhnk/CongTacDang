"use client";

import React, { useState } from "react";
import { useRouter } from "next/navigation";
import { authService } from "@/services/authService";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { AuthBrand } from "../login/AuthBrand";
import styles from "../login/auth.module.css";

export default function ChangePasswordPage() {
  const router = useRouter();
  const { refreshUser } = useAuth();
  const { toast } = useToast();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (newPassword !== confirmPassword) {
      toast.error("Mật khẩu xác nhận không khớp.");
      return;
    }

    setSubmitting(true);
    try {
      await authService.changePassword(currentPassword, newPassword);
      await refreshUser();
      toast.success("Đổi mật khẩu thành công. Các phiên đăng nhập khác của bạn đã bị đăng xuất.");
      router.replace("/evaluations");
    } catch (error: any) {
      toast.error(error.message || "Không thể đổi mật khẩu.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className={styles.inline}>
      <div className={styles.card}>
        <AuthBrand />
        <h1 className={styles.title}>Đổi mật khẩu</h1>
        <form onSubmit={handleSubmit} className={styles.form}>
          <div>
            <label className="form-label" htmlFor="cp-current">Mật khẩu hiện tại</label>
            <input id="cp-current" className="form-control" type="password" autoComplete="current-password" required value={currentPassword} onChange={(event) => setCurrentPassword(event.target.value)} />
          </div>
          <div>
            <label className="form-label" htmlFor="cp-new">Mật khẩu mới</label>
            <input id="cp-new" className="form-control" type="password" autoComplete="new-password" minLength={8} required
              placeholder="Tối thiểu 8 ký tự, gồm chữ và số" value={newPassword} onChange={(event) => setNewPassword(event.target.value)} />
          </div>
          <div>
            <label className="form-label" htmlFor="cp-confirm">Xác nhận mật khẩu mới</label>
            <input id="cp-confirm" className="form-control" type="password" autoComplete="new-password" minLength={8} required value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} />
          </div>
          <button className={`btn btn-primary ${styles.submit}`} type="submit" disabled={submitting}>
            {submitting ? "Đang cập nhật..." : "Đổi mật khẩu"}
          </button>
        </form>
      </div>
    </div>
  );
}
