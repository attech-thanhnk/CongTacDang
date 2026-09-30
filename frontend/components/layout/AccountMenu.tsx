"use client";

import React, { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useAuth } from "@/contexts/AuthContext";
import { userService } from "@/services/userService";
import { useToast } from "@/contexts/ToastContext";
import styles from "./SidebarAccount.module.css";

export function initialsOf(fullName?: string): string {
  if (!fullName) return "?";
  return fullName
    .split(" ")
    .filter(Boolean)
    .slice(-2)
    .map((w) => w[0])
    .join("")
    .toUpperCase();
}

/** Khu tài khoản cuối thanh bên: bấm mở menu (Tài khoản, Đăng xuất). */
export function AccountMenu({ onNavigate }: { onNavigate?: () => void }) {
  const { user, logout } = useAuth();
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const [title, setTitle] = useState("");

  // Dòng dưới tên: chức vụ của cán bộ (không phải vai trò trong hệ thống).
  useEffect(() => {
    if (!user || user.mustChangePassword) return;
    let alive = true;
    userService.getMyProfile().then((p) => alive && setTitle(p.adminTitle || "")).catch(() => {});
    return () => {
      alive = false;
    };
  }, [user]);
  const rootRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") setOpen(false);
    };
    document.addEventListener("mousedown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  if (!user) return null;

  const go = () => {
    setOpen(false);
    onNavigate?.();
  };

  const handleLogout = async () => {
    setOpen(false);
    try {
      await logout();
    } catch (err: any) {
      toast.error(err?.message || "Đăng xuất thất bại.");
    }
  };

  return (
    <div className={styles.root} ref={rootRef}>
      {open && (
        <div className={styles.menu} role="menu">
          <div className={styles.menuHead}>
            <b className="text-truncate">{user.fullName}</b>
            <small className="text-truncate">{user.userName}</small>
          </div>
          <Link href="/account" role="menuitem" className={styles.item} onClick={go}>
            <i className="bi bi-person-gear" />
            Tài khoản
          </Link>
          <div className={styles.sep} />
          <button type="button" role="menuitem" className={`${styles.item} ${styles.danger}`} onClick={handleLogout}>
            <i className="bi bi-box-arrow-right" />
            Đăng xuất
          </button>
        </div>
      )}
      <button
        type="button"
        className={styles.trigger}
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => setOpen((v) => !v)}
      >
        <span className="initials">{initialsOf(user.fullName)}</span>
        <span className={styles.who}>
          <b className="text-truncate">{user.fullName}</b>
          {title && <small className="text-truncate" title={title}>{title}</small>}
        </span>
        <i className={`bi bi-chevron-expand ${styles.chev}`} />
      </button>
    </div>
  );
}

export default AccountMenu;
