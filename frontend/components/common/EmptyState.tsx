"use client";

import React from "react";

export interface EmptyStateProps {
  icon?: string;
  title: string;
  description?: string;
  action?: React.ReactNode;
  className?: string;
}

export const EmptyState: React.FC<EmptyStateProps> = ({
  icon = "bi-inbox",
  title,
  description,
  action,
  className = "",
}) => {
  return (
    <div
      className={`d-flex flex-column align-items-center justify-content-center text-center p-5 ${className}`}
    >
      <div
        className="rounded-circle d-flex align-items-center justify-content-center mb-3"
        style={{
          width: "56px",
          height: "56px",
          backgroundColor: "#f1f5f9",
          color: "#64748b",
          fontSize: "24px",
        }}
      >
        <i className={`bi ${icon}`}></i>
      </div>
      <h6 className="fw-semibold text-dark mb-1" style={{ fontSize: "14.5px" }}>
        {title}
      </h6>
      {description && (
        <p className="text-secondary small mb-3 max-w-md lh-base" style={{ fontSize: "12.5px" }}>
          {description}
        </p>
      )}
      {action && <div>{action}</div>}
    </div>
  );
};

export default EmptyState;
