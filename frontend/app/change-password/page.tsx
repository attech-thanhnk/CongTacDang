"use client";

import React, { useState } from "react";
import { useRouter } from "next/navigation";
import { authService } from "@/services/authService";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";

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
    <div className="container py-5" style={{ maxWidth: "620px" }}>
      <div className="card border-0 shadow-sm">
        <div className="card-body p-4 p-md-5">
          <h1 className="h4 fw-bold mb-2">Đổi mật khẩu</h1>
          <p className="text-secondary small mb-4">
            Nếu bạn đang dùng mật khẩu tạm do quản trị cấp, hãy đổi mật khẩu để tiếp tục sử dụng hệ thống.
            Mật khẩu mới phải có tối thiểu 8 ký tự (hoặc theo chính sách của đơn vị), gồm cả chữ và số.
            Sau khi đổi, các phiên đăng nhập khác của bạn sẽ bị đăng xuất.
          </p>
          <form onSubmit={handleSubmit} className="d-grid gap-3">
            <label className="small fw-semibold">
              Mật khẩu hiện tại
              <input className="form-control mt-1" type="password" required value={currentPassword} onChange={(event) => setCurrentPassword(event.target.value)} />
            </label>
            <label className="small fw-semibold">
              Mật khẩu mới
              <input className="form-control mt-1" type="password" minLength={8} required value={newPassword} onChange={(event) => setNewPassword(event.target.value)} />
            </label>
            <label className="small fw-semibold">
              Xác nhận mật khẩu mới
              <input className="form-control mt-1" type="password" minLength={8} required value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} />
            </label>
            <button className="btn btn-primary" type="submit" disabled={submitting}>
              {submitting ? "Đang cập nhật..." : "Đổi mật khẩu"}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
