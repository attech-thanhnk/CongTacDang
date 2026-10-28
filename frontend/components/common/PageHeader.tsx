"use client";

import React, { useLayoutEffect } from "react";
import { createPortal } from "react-dom";
import { useActionsSlot, usePageTitleSetter } from "@/contexts/PageTitleContext";
import { BackButton } from "./BackButton";
import { usePhone } from "./usePhone";

export interface PageHeaderProps {
  title: string;
  subTitle?: string;
  badge?: React.ReactNode;
  actions?: React.ReactNode;
  /** Bộ lọc/ô tìm của trang: ở thân trang, không lên thanh trên. */
  toolbar?: React.ReactNode;
  className?: string;
}

/** Tiêu đề trang đăng ký lên thanh trên (h1 duy nhất); nút thao tác chính cũng lên thanh trên (điện thoại: giữ ở thân trang). */
export const PageHeader: React.FC<PageHeaderProps> = ({
  title,
  subTitle,
  badge,
  actions,
  toolbar,
  className = "",
}) => {
  const setTitle = usePageTitleSetter();
  const slot = useActionsSlot();
  const phone = usePhone();

  useLayoutEffect(() => {
    if (!setTitle) return;
    setTitle(title);
    return () => setTitle((current) => (current === title ? null : current));
  }, [setTitle, title]);

  const inBar = !!setTitle;
  const back = inBar ? <BackButton /> : null;
  const toTopBar = inBar && !!slot && !phone && !!actions;

  return (
    <>
      {toTopBar && createPortal(<div className="page-header-actions">{actions}</div>, slot)}
      <div className={`page-header-bar ${className}`}>
        {back}
        {(!inBar || subTitle || badge) && (
          <div className="page-header-content">
            <div className="page-header-title-row">
              {!inBar && <h1>{title}</h1>}
              {badge && <div>{badge}</div>}
            </div>
            {subTitle && <div className="page-subtitle">{subTitle}</div>}
          </div>
        )}

        {toolbar && <div className="page-header-actions">{toolbar}</div>}
        {actions && !toTopBar && <div className="page-header-actions">{actions}</div>}
      </div>
    </>
  );
};

export default PageHeader;
