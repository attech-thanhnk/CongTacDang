"use client";

import React from "react";

export interface EmptyStateProps {
  icon?: string;
  title: string;
  /** Một câu hướng dẫn ngắn dưới tiêu đề. */
  description?: React.ReactNode;
  /** Nút hành động; nơi dùng tự kiểm tra quyền trước khi truyền vào. */
  action?: React.ReactNode;
  /** Thu nhỏ khoảng đệm khi đặt trong khối đã có tiêu đề riêng. */
  compact?: boolean;
  className?: string;
}

export const EmptyState: React.FC<EmptyStateProps> = ({
  icon = "bi-inbox",
  title,
  description,
  action,
  compact = false,
  className = "",
}) => {
  return (
    <div
      className={`d-flex flex-column align-items-center justify-content-center text-center ${compact ? "px-3 py-4" : "p-5"} ${className}`}
    >
      <div
        className="rounded-circle d-flex align-items-center justify-content-center mb-3"
        aria-hidden="true"
        style={{
          width: compact ? "44px" : "56px",
          height: compact ? "44px" : "56px",
          backgroundColor: "var(--tint)",
          color: "var(--ink-3)",
          fontSize: compact ? "20px" : "24px",
        }}
      >
        <i className={`bi ${icon}`}></i>
      </div>
      <h6 className="fw-semibold mb-0" style={{ fontSize: "16px", color: "var(--ink)" }}>
        {title}
      </h6>
      {description && (
        <p className="mb-0 mt-1 lh-base" style={{ fontSize: "14px", color: "var(--ink-2)", maxWidth: "28rem" }}>
          {description}
        </p>
      )}
      {action && <div className="mt-3 d-flex flex-wrap justify-content-center gap-2">{action}</div>}
    </div>
  );
};

export default EmptyState;
