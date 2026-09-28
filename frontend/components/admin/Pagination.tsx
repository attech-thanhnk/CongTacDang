"use client";

import React from "react";

export interface PaginationProps {
  page: number;
  totalPages: number;
  totalCount: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  disabled?: boolean;
}

/** Thanh phân trang cho danh sách phân trang phía máy chủ. */
export function Pagination({ page, totalPages, totalCount, pageSize, onPageChange, disabled }: PaginationProps) {
  const from = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, totalCount);
  return (
    <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 px-3 py-2 border-top small text-secondary">
      <span>
        {from}–{to} / {totalCount} bản ghi
      </span>
      <div className="btn-group btn-group-sm">
        <button type="button" className="btn btn-outline-secondary" disabled={disabled || page <= 1} onClick={() => onPageChange(1)} aria-label="Trang đầu">
          <i className="bi bi-chevron-double-left" />
        </button>
        <button type="button" className="btn btn-outline-secondary" disabled={disabled || page <= 1} onClick={() => onPageChange(page - 1)} aria-label="Trang trước">
          <i className="bi bi-chevron-left" />
        </button>
        <span className="btn btn-outline-secondary disabled">
          Trang {totalPages === 0 ? 0 : page}/{totalPages}
        </span>
        <button type="button" className="btn btn-outline-secondary" disabled={disabled || page >= totalPages} onClick={() => onPageChange(page + 1)} aria-label="Trang sau">
          <i className="bi bi-chevron-right" />
        </button>
        <button type="button" className="btn btn-outline-secondary" disabled={disabled || page >= totalPages} onClick={() => onPageChange(totalPages)} aria-label="Trang cuối">
          <i className="bi bi-chevron-double-right" />
        </button>
      </div>
    </div>
  );
}

export default Pagination;
