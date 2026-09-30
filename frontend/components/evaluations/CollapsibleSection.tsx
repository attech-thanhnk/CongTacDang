"use client";

import React, { useId, useState } from "react";
import styles from "./record.module.css";

interface Props {
  title: React.ReactNode;
  /** Nhãn nhỏ cạnh tiêu đề (vd. "Chưa tới", "Cấp trên thực hiện"). */
  tag?: React.ReactNode;
  defaultOpen?: boolean;
  /** Nội dung sát mép khối (bảng, lưới số liệu) thay vì có lề. */
  flush?: boolean;
  children: React.ReactNode;
}

/** Khối dữ liệu có tiêu đề, bấm tiêu đề để mở/thu gọn. */
export function CollapsibleSection({ title, tag, defaultOpen = true, flush = false, children }: Props) {
  const [open, setOpen] = useState(defaultOpen);
  const id = useId();
  return (
    <section className={`${styles.block} ${open ? styles.open : ""}`}>
      <h2 className={styles.toggleH}>
        <button type="button" className={styles.toggle} aria-expanded={open} aria-controls={id} onClick={() => setOpen(!open)}>
          <i className={`bi ${open ? "bi-chevron-down" : "bi-chevron-right"} ${styles.chev}`} aria-hidden="true" />
          <span>{title}</span>
          {tag && <span className={styles.end}>{tag}</span>}
        </button>
      </h2>
      {open && (
        <div id={id} className={flush ? undefined : styles.body}>
          {children}
        </div>
      )}
    </section>
  );
}

export default CollapsibleSection;
