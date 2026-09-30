"use client";

import React, { useState } from "react";
import { authService } from "@/services/authService";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";

/** Form đổi mật khẩu dùng chung: trang đổi mật khẩu bắt buộc (lần đăng nhập đầu) và mục Bảo mật của trang Tài khoản. */
export function ChangePasswordForm({
  className,
  submitClassName = "btn btn-primary",
  onSuccess,
}: {
  className?: string;
  submitClassName?: string;
  onSuccess?: () => void;
}) {
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
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
      onSuccess?.();
    } catch (error: any) {
      toast.error(error.message || "Không thể đổi mật khẩu.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className={className}>
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
      <div>
        <button className={submitClassName} type="submit" disabled={submitting}>
          {submitting ? "Đang cập nhật..." : "Đổi mật khẩu"}
        </button>
      </div>
    </form>
  );
}

export default ChangePasswordForm;
