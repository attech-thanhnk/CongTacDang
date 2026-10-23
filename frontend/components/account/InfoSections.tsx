"use client";

import React from "react";
import styles from "./InfoSections.module.css";

export interface InfoItem {
  label: string;
  /** null/undefined/chuỗi rỗng → hiện "—" nhạt. */
  value: React.ReactNode;
  /** Chiếm cả hàng (giá trị dài). */
  wide?: boolean;
}

export interface InfoSection {
  title: string;
  icon?: string;
  items: InfoItem[];
}

function isEmpty(value: React.ReactNode): boolean {
  return value === null || value === undefined || value === false || (typeof value === "string" && value.trim() === "");
}

/** Thông tin hồ sơ theo nhóm, mỗi nhóm là lưới 2 cột nhãn trên – giá trị dưới. */
export function InfoSections({ sections, className = "" }: { sections: InfoSection[]; className?: string }) {
  return (
    <div className={`${styles.sections} ${className}`}>
      {sections.map((section) => (
        <section key={section.title} className={styles.section}>
          <h3 className={styles.title}>
            {section.icon && <i className={`bi ${section.icon}`} aria-hidden="true" />}
            {section.title}
          </h3>
          <dl className={styles.grid}>
            {section.items.map((item) => (
              <div key={item.label} className={`${styles.item} ${item.wide ? styles.wide : ""}`}>
                <dt>{item.label}</dt>
                <dd>{isEmpty(item.value) ? <span className={styles.empty}>—</span> : item.value}</dd>
              </div>
            ))}
          </dl>
        </section>
      ))}
    </div>
  );
}

export default InfoSections;
