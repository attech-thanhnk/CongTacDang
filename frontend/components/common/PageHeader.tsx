"use client";

import React from "react";

export interface PageHeaderProps {
  title: string;
  subTitle?: string;
  badge?: React.ReactNode;
  actions?: React.ReactNode;
  className?: string;
}

export const PageHeader: React.FC<PageHeaderProps> = ({
  title,
  subTitle,
  badge,
  actions,
  className = "",
}) => {
  return (
    <div className={`page-header-bar ${className}`}>
      <div>
        <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
          <h1>{title}</h1>
          {badge && <div>{badge}</div>}
        </div>
        {subTitle && (
          <div className="page-subtitle">{subTitle}</div>
        )}
      </div>

      {actions && (
        <div style={{ display: "flex", alignItems: "center", gap: "8px", flexWrap: "wrap" }}>
          {actions}
        </div>
      )}
    </div>
  );
};

export default PageHeader;
