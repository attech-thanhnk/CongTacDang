"use client";

import React from "react";

/** Các kích thước trang cho phép chọn; phần tử đầu là mặc định. */
export const PAGE_SIZE_OPTIONS = [20, 50, 100] as const;
export const DEFAULT_PAGE_SIZE: number = PAGE_SIZE_OPTIONS[0];

export interface PaginationProps {
  /** Trang hiện tại, bắt đầu từ 1. */
  page: number;
  pageSize: number;
  total: number;
  onPageChange: (page: number) => void;
  /** Bỏ trống thì không hiện ô chọn số dòng/trang. */
  onPageSizeChange?: (pageSize: number) => void;
  disabled?: boolean;
  className?: string;
}

type PageToken = number | "gap-left" | "gap-right";

/** Dãy số trang gọn: trang đầu, cuối, lân cận trang hiện tại; chỗ bị lược hiện dấu "…". */
function pageWindow(page: number, totalPages: number): PageToken[] {
  if (totalPages <= 7) return Array.from({ length: totalPages }, (_, i) => i + 1);
  const start = Math.max(2, Math.min(page - 1, totalPages - 4));
  const end = Math.min(totalPages - 1, Math.max(page + 1, 5));
  const tokens: PageToken[] = [1];
  if (start > 2) tokens.push("gap-left");
  for (let p = start; p <= end; p++) tokens.push(p);
  if (end < totalPages - 1) tokens.push("gap-right");
  tokens.push(totalPages);
  return tokens;
}

/** Thanh phân trang chuẩn đặt dưới bảng — dùng chung cho phân trang phía máy chủ và phía trình duyệt. */
export function Pagination({ page, pageSize, total, onPageChange, onPageSizeChange, disabled, className = "" }: PaginationProps) {
  if (total <= DEFAULT_PAGE_SIZE && pageSize === DEFAULT_PAGE_SIZE) return null;

  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const current = Math.min(Math.max(1, page), totalPages);
  const from = total === 0 ? 0 : (current - 1) * pageSize + 1;
  const to = Math.min(current * pageSize, total);
  const go = (p: number) => {
    if (!disabled && p >= 1 && p <= totalPages && p !== current) onPageChange(p);
  };
  const atFirst = disabled || current <= 1;
  const atLast = disabled || current >= totalPages;

  return (
    <div className={`d-flex flex-wrap justify-content-between align-items-center gap-2 px-3 py-2 border-top small text-secondary ${className}`}>
      <span className="text-nowrap">
        {from}–{to} / {total}
      </span>
      <div className="d-flex flex-wrap align-items-center gap-2 ms-auto">
        {totalPages > 1 && (
          <nav aria-label="Phân trang">
            <ul className="pagination pagination-sm mb-0 flex-wrap">
              <li className={`page-item ${atFirst ? "disabled" : ""}`}>
                <button type="button" className="page-link" onClick={() => go(1)} disabled={atFirst} aria-label="Trang đầu">
                  <i className="bi bi-chevron-double-left" />
                </button>
              </li>
              <li className={`page-item ${atFirst ? "disabled" : ""}`}>
                <button type="button" className="page-link" onClick={() => go(current - 1)} disabled={atFirst} aria-label="Trang trước">
                  <i className="bi bi-chevron-left" />
                </button>
              </li>
              {pageWindow(current, totalPages).map((token) =>
                typeof token === "number" ? (
                  <li
                    key={token}
                    className={`page-item ${token === current ? "active" : "d-none d-sm-block"} ${disabled ? "disabled" : ""}`}
                    aria-current={token === current ? "page" : undefined}
                  >
                    <button type="button" className="page-link" onClick={() => go(token)} disabled={disabled}>
                      {token}
                    </button>
                  </li>
                ) : (
                  <li key={token} className="page-item disabled d-none d-sm-block">
                    <span className="page-link">…</span>
                  </li>
                )
              )}
              <li className={`page-item ${atLast ? "disabled" : ""}`}>
                <button type="button" className="page-link" onClick={() => go(current + 1)} disabled={atLast} aria-label="Trang sau">
                  <i className="bi bi-chevron-right" />
                </button>
              </li>
              <li className={`page-item ${atLast ? "disabled" : ""}`}>
                <button type="button" className="page-link" onClick={() => go(totalPages)} disabled={atLast} aria-label="Trang cuối">
                  <i className="bi bi-chevron-double-right" />
                </button>
              </li>
            </ul>
          </nav>
        )}
        {onPageSizeChange && (
          <label className="d-flex align-items-center gap-1 mb-0 text-nowrap">
            <select
              className="form-select form-select-sm w-auto"
              value={pageSize}
              disabled={disabled}
              onChange={(e) => onPageSizeChange(Number(e.target.value))}
              aria-label="Số dòng mỗi trang"
            >
              {PAGE_SIZE_OPTIONS.map((size) => (
                <option key={size} value={size}>
                  {size}
                </option>
              ))}
            </select>
            dòng/trang
          </label>
        )}
      </div>
    </div>
  );
}

export default Pagination;
