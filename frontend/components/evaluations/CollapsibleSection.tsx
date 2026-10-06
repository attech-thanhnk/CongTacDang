"use client";

import React, { useEffect, useId, useState } from "react";
import styles from "./record.module.css";

/** Sự kiện yêu cầu mở khối theo `anchorId` (dùng khi điều hướng tới khối từ nơi khác trên trang). */
const OPEN_EVENT = "collapsible-section:open";

interface Props {
  title: React.ReactNode;
  /** Nhãn nhỏ cạnh tiêu đề (vd. "Chưa tới", "Cấp trên thực hiện"). */
  tag?: React.ReactNode;
  defaultOpen?: boolean;
  /** Nội dung sát mép khối (bảng, lưới số liệu) thay vì có lề. */
  flush?: boolean;
  /** Id của khối để cuộn tới / mở từ bên ngoài (`revealSection`). */
  anchorId?: string;
  children: React.ReactNode;
}

/**
 * Mở khối có `anchorId` (nếu đang thu gọn), cuộn mượt tới khối và đặt focus vào tiêu đề.
 * Trả về false khi không có khối này trên trang.
 */
export function revealSection(anchorId: string): boolean {
  const element = document.getElementById(anchorId);
  if (!element) return false;
  window.dispatchEvent(new CustomEvent<string>(OPEN_EVENT, { detail: anchorId }));
  // Chờ khối dựng lại nội dung rồi mới cuộn để vị trí đúng.
  requestAnimationFrame(() =>
    requestAnimationFrame(() => {
      element.scrollIntoView({ behavior: "smooth", block: "start" });
      element.querySelector<HTMLElement>("button")?.focus({ preventScroll: true });
    }),
  );
  return true;
}

/** Khối dữ liệu có tiêu đề, bấm tiêu đề để mở/thu gọn. */
export function CollapsibleSection({ title, tag, defaultOpen = true, flush = false, anchorId, children }: Props) {
  const [open, setOpen] = useState(defaultOpen);
  const id = useId();

  useEffect(() => {
    if (!anchorId) return;
    const handler = (event: Event) => {
      if ((event as CustomEvent<string>).detail === anchorId) setOpen(true);
    };
    window.addEventListener(OPEN_EVENT, handler);
    return () => window.removeEventListener(OPEN_EVENT, handler);
  }, [anchorId]);

  return (
    <section id={anchorId} className={`${styles.block} ${styles.anchor} ${open ? styles.open : ""}`}>
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
