"use client";

import React, { useEffect, useMemo, useRef, useState } from "react";
import styles from "./docWithToc.module.css";

export interface DocTocEntry {
  id: string;
  label: string;
  code?: string;
  /** 1 = mục lớn, 2 = mục con của mục lớn đứng trước. */
  level: 1 | 2;
}

interface Props {
  entries: DocTocEntry[];
  ariaLabel: string;
  /** Phần đầu mục lục, ví dụ ô tìm kiếm. */
  header?: React.ReactNode;
  /** Chỉ mở mục con của mục lớn đang đọc (hoặc mục người dùng mở). */
  collapsible?: boolean;
  /** Mở hết mọi mục con (đang tìm kiếm). */
  expandAll?: boolean;
  /** Chiều cao các thanh dính nằm trong nội dung; mọi tiêu đề có id chừa đúng chừng này khi cuộn tới. */
  stickyHeight?: number;
  children: React.ReactNode;
}

const READ_OFFSET = 32;

/** Mục đang đọc: mục cuối cùng đã cuộn qua mép trên vùng cuộn; IntersectionObserver báo thay đổi, cuộn dừng thì tính lại. */
function useActiveSection(ids: string[]) {
  const [active, setActive] = useState<string | null>(ids[0] ?? null);
  const key = ids.join("|");
  useEffect(() => {
    if (!ids.length) return;
    const root = document.querySelector<HTMLElement>(".app-main");
    const els = ids.map((id) => document.getElementById(id)).filter((e): e is HTMLElement => !!e);
    if (!els.length) return;
    const recompute = () => {
      const limit = (root ? root.getBoundingClientRect().top : 0) + READ_OFFSET;
      let current = els[0].id;
      for (const el of els) if (el.getBoundingClientRect().top <= limit) current = el.id;
      // Cuối trang: mục cuối không thể lên tới mép trên nên tính là đang đọc.
      if (root && root.scrollTop + root.clientHeight >= root.scrollHeight - 2) current = els[els.length - 1].id;
      setActive(current);
    };
    const io = new IntersectionObserver(recompute, { root, rootMargin: "0px 0px -75% 0px", threshold: [0, 1] });
    els.forEach((e) => io.observe(e));
    let timer: number | undefined;
    const onScroll = () => {
      window.clearTimeout(timer);
      timer = window.setTimeout(recompute, 100);
    };
    (root ?? window).addEventListener("scroll", onScroll, { passive: true });
    recompute();
    return () => {
      io.disconnect();
      window.clearTimeout(timer);
      (root ?? window).removeEventListener("scroll", onScroll);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);
  return active;
}

/** Nội dung có mục lục: mục lục dính trong vùng cuộn (màn rộng) hoặc nút "Mục lục" (màn hẹp). */
export function DocWithToc({ entries, ariaLabel, header, collapsible = false, expandAll = false, stickyHeight = 0, children }: Props) {
  const active = useActiveSection(useMemo(() => entries.map((e) => e.id), [entries]));
  const [open, setOpen] = useState(false);
  const [manual, setManual] = useState<Set<string>>(new Set());
  const list = useRef<HTMLUListElement>(null);

  // Mục lớn chứa mục đang đọc.
  const activeChapter = useMemo(() => {
    let chapter: string | null = null;
    for (const e of entries) {
      if (e.level === 1) chapter = e.id;
      if (e.id === active) return chapter;
    }
    return chapter;
  }, [entries, active]);

  // Neo # trên địa chỉ luôn cuộn tới đúng mục: lúc mở trang, khi đổi hash cùng trang và khi Back/Forward
  // (lần cuộn mượt đang dở bị lần cuộn mới thay thế).
  useEffect(() => {
    const go = () => {
      const id = decodeURIComponent(window.location.hash.slice(1));
      if (id) document.getElementById(id)?.scrollIntoView({ block: "start" });
    };
    window.addEventListener("hashchange", go);
    window.addEventListener("popstate", go);
    if (window.location.hash) requestAnimationFrame(go);
    return () => {
      window.removeEventListener("hashchange", go);
      window.removeEventListener("popstate", go);
    };
  }, []);

  // Đổi mục lớn đang đọc thì bỏ các lựa chọn mở/đóng tay.
  useEffect(() => setManual(new Set()), [activeChapter]);

  // Giữ mục đang đọc trong tầm nhìn của mục lục (chỉ cuộn mục lục, không cuộn trang).
  useEffect(() => {
    const box = list.current?.closest<HTMLElement>("[data-toc-scroll]");
    const link = list.current?.querySelector<HTMLElement>("[aria-current]");
    if (!box || !link || box.scrollHeight <= box.clientHeight) return;
    const top = link.offsetTop - box.offsetTop;
    if (top < box.scrollTop || top + link.offsetHeight > box.scrollTop + box.clientHeight) box.scrollTop = top - 40;
  }, [active]);

  const toggle = (id: string) =>
    setManual((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const jump = (e: React.MouseEvent, id: string) => {
    const target = document.getElementById(id);
    if (!target) return;
    e.preventDefault();
    target.scrollIntoView({ behavior: "smooth", block: "start" });
    window.history.replaceState(null, "", `#${id}`);
    setOpen(false);
  };

  let chapter: string | null = null;
  return (
    <div className={styles.root} style={{ ["--doc-sticky-h" as string]: `${stickyHeight}px` }}>
      <nav className={`${styles.toc} ${open ? styles.tocOpen : ""}`} aria-label={ariaLabel} data-toc-scroll>
        <button type="button" className={styles.tocToggle} aria-expanded={open} onClick={() => setOpen((v) => !v)}>
          <span>Mục lục</span>
          <i className="bi bi-chevron-down" aria-hidden="true" />
        </button>
        <div className={styles.tocBody}>
          {header}
          <ul ref={list}>
            {entries.map((e) => {
              if (e.level === 1) chapter = e.id;
              const chapterOpen = expandAll || (chapter === activeChapter) !== (chapter !== null && manual.has(chapter));
              if (e.level === 2 && collapsible && !chapterOpen) return null;
              const hasChildren = collapsible && e.level === 1 && !expandAll;
              const isOpen = chapterOpen;
              return (
                <li key={e.id} className={e.level === 2 ? styles.child : undefined}>
                  <a href={`#${e.id}`} className={active === e.id ? styles.active : undefined} aria-current={active === e.id ? "location" : undefined} onClick={(ev) => jump(ev, e.id)}>
                    {e.code && <span className={styles.code}>{e.code}</span>}
                    <span className={styles.label}>{e.label}</span>
                  </a>
                  {hasChildren && (
                    <button type="button" className={styles.chev} aria-label={`${isOpen ? "Thu gọn" : "Mở"} ${e.label}`} aria-expanded={isOpen} onClick={() => toggle(e.id)}>
                      <i className={`bi ${isOpen ? "bi-chevron-up" : "bi-chevron-down"}`} aria-hidden="true" />
                    </button>
                  )}
                </li>
              );
            })}
          </ul>
        </div>
      </nav>
      <div className={styles.main}>{children}</div>
    </div>
  );
}

export default DocWithToc;
