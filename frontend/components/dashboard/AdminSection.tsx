"use client";

import React from "react";
import Link from "next/link";
import styles from "./dashboard.module.css";

export interface AdminShortcut {
  href: string;
  label: string;
  icon: string;
  /** Số đếm nhỏ cạnh tên; null khi không đếm. */
  count: number | null;
}

/** Lối tắt quản trị theo quyền: tài khoản, vai trò, người thực hiện, nhập dữ liệu. */
export function AdminSection({ items }: { items: AdminShortcut[] }) {
  if (items.length === 0) return null;
  return (
    <section className={styles.block}>
      <div className={styles.blockH}>
        <h2>Quản trị</h2>
      </div>
      <div className={styles.shortcuts}>
        {items.map((item) => (
          <Link key={item.href} href={item.href} className={styles.shortcut}>
            <i className={`bi ${item.icon}`} aria-hidden="true" />
            <span>{item.label}</span>
            {item.count !== null && <span className={`${styles.count} num`}>{item.count}</span>}
            <i className={`bi bi-chevron-right ${styles.shortcutGo}`} aria-hidden="true" />
          </Link>
        ))}
      </div>
    </section>
  );
}

export default AdminSection;
