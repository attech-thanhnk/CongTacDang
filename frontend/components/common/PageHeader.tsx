"use client";

import React, { useLayoutEffect } from "react";
import { usePageTitleSetter } from "@/contexts/PageTitleContext";

export interface PageHeaderProps {
  title: string;
  subTitle?: string;
  badge?: React.ReactNode;
  actions?: React.ReactNode;
  className?: string;
}

/** Tiêu đề trang đăng ký lên thanh trên (h1 duy nhất); thân trang chỉ giữ phụ đề, huy hiệu và nút thao tác. */
export const PageHeader: React.FC<PageHeaderProps> = ({
  title,
  subTitle,
  badge,
  actions,
  className = "",
}) => {
  const setTitle = usePageTitleSetter();

  useLayoutEffect(() => {
    if (!setTitle) return;
    setTitle(title);
    return () => setTitle((current) => (current === title ? null : current));
  }, [setTitle, title]);

  const inBar = !!setTitle;
  if (inBar && !subTitle && !badge && !actions) return null;

  return (
    <div className={`page-header-bar ${className}`}>
      {(!inBar || subTitle || badge) && (
        <div className="page-header-content">
          <div className="page-header-title-row">
            {!inBar && <h1>{title}</h1>}
            {badge && <div>{badge}</div>}
          </div>
          {subTitle && <div className="page-subtitle">{subTitle}</div>}
        </div>
      )}

      {actions && <div className="page-header-actions">{actions}</div>}
    </div>
  );
};

export default PageHeader;
