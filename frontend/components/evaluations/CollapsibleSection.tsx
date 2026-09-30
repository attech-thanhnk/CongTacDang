"use client";

import React, { useId, useState } from "react";

interface Props {
  title: React.ReactNode;
  /** Nhãn nhỏ cạnh tiêu đề (vd. "Chưa tới", "Do cấp trên thực hiện"). */
  tag?: React.ReactNode;
  defaultOpen?: boolean;
  children: React.ReactNode;
}

/** Mục dữ liệu có tiêu đề, bấm tiêu đề để mở/thu gọn. */
export function CollapsibleSection({ title, tag, defaultOpen = true, children }: Props) {
  const [open, setOpen] = useState(defaultOpen);
  const id = useId();
  return (
    <div className="border rounded-3">
      <button
        type="button"
        className="btn w-100 d-flex align-items-center gap-2 text-start px-3 py-2 border-0"
        aria-expanded={open}
        aria-controls={id}
        onClick={() => setOpen(!open)}
      >
        <i className={`bi ${open ? "bi-chevron-down" : "bi-chevron-right"} text-secondary`} aria-hidden="true" />
        <span className="fw-semibold" style={{ fontSize: 15 }}>{title}</span>
        {tag && <span className="ms-auto">{tag}</span>}
      </button>
      {open && (
        <div id={id} className="px-3 pb-3" style={{ fontSize: 14 }}>
          {children}
        </div>
      )}
    </div>
  );
}

export default CollapsibleSection;
