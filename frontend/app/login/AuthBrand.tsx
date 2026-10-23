"use client";

import React from "react";
import { useOrganizationInfo } from "@/services/organizationSettingsService";
import styles from "./auth.module.css";

/** Biểu tượng Đảng bộ (sao trong vòng đỏ son) và tên đơn vị lấy từ cài đặt đơn vị — cùng cách hiển thị với thanh bên. */
export function AuthBrand({ title }: { title?: string }) {
  const org = useOrganizationInfo();
  const main = title ?? (org?.shortName || org?.systemName || "");
  const sub = title
    ? ""
    : org
    ? org.shortName && org.systemName.includes(org.shortName)
      ? org.systemName.replace(org.shortName, "").trim()
      : org.systemName !== main
        ? org.systemName
        : ""
    : "";

  return (
    <div className={styles.brand}>
      <div className={styles.emblem} aria-hidden="true">
        <svg viewBox="0 0 24 24">
          <path d="M12 2.5l2.7 6.1 6.6.5-5 4.4 1.5 6.5L12 16.6 6.2 20l1.5-6.5-5-4.4 6.6-.5z" />
        </svg>
      </div>
      <div>
        {sub && <div className={styles.t1}>{sub}</div>}
        <div className={styles.t2}>{main || " "}</div>
      </div>
    </div>
  );
}

export default AuthBrand;
