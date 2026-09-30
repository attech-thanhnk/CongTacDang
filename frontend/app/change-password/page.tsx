"use client";

import React from "react";
import { useRouter } from "next/navigation";
import { ChangePasswordForm } from "@/components/account/ChangePasswordForm";
import { AuthBrand } from "../login/AuthBrand";
import styles from "../login/auth.module.css";

/** Đổi mật khẩu bắt buộc (mật khẩu tạm ở lần đăng nhập đầu); người dùng đã vào hệ thống đổi ở trang Tài khoản. */
export default function ChangePasswordPage() {
  const router = useRouter();

  return (
    <div className={styles.inline}>
      <div className={styles.card}>
        <AuthBrand />
        <h1 className={styles.title}>Đổi mật khẩu</h1>
        <ChangePasswordForm
          className={styles.form}
          submitClassName={`btn btn-primary ${styles.submit}`}
          onSuccess={() => router.replace("/evaluations")}
        />
      </div>
    </div>
  );
}
