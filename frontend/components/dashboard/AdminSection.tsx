"use client";

import React from "react";
import Link from "next/link";
import styles from "./dashboard.module.css";

export interface AdminStats {
  accounts: number | null;
  departments: number;
  cells: number;
}

/** Số liệu quản trị: tài khoản, phòng/đơn vị, Chi bộ. */
export function AdminSection({ stats }: { stats: AdminStats }) {
  const items = [
    stats.accounts !== null && { href: "/admin/users", label: "Tài khoản", value: stats.accounts },
    { href: "/catalog", label: "Phòng/đơn vị", value: stats.departments },
    { href: "/catalog", label: "Chi bộ", value: stats.cells },
  ].filter(Boolean) as { href: string; label: string; value: number }[];

  return (
    <section className={styles.block}>
      <div className={styles.blockH}>
        <h2>Quản trị</h2>
      </div>
      <div className={styles.stats} style={{ gridTemplateColumns: `repeat(${items.length}, minmax(0, 1fr))` }}>
        {items.map((item) => (
          <Link key={item.label} href={item.href} className={styles.stat}>
            <div className={styles.statV}>{item.value}</div>
            <div className={styles.statL}>{item.label}</div>
          </Link>
        ))}
      </div>
    </section>
  );
}

export default AdminSection;
