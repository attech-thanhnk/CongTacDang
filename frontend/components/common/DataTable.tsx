"use client";

import React from "react";
import EmptyState from "./EmptyState";

export interface DataTableColumn<T> {
  header: React.ReactNode;
  width?: string;
  align?: "left" | "center" | "right";
  className?: string;
  render: (item: T, index: number) => React.ReactNode;
}

export interface DataTableProps<T> {
  columns: DataTableColumn<T>[];
  data: T[];
  keyExtractor: (item: T, index: number) => string | number;
  loading?: boolean;
  searchTerm?: string;
  onSearchChange?: (term: string) => void;
  searchPlaceholder?: string;
  filters?: React.ReactNode;
  totalCountText?: string;
  emptyTitle?: string;
  emptyDescription?: string;
  emptyIcon?: string;
  emptyAction?: React.ReactNode;
  className?: string;
  onRowClick?: (item: T) => void;
  selectedRowKey?: string | number | null;
}

export function DataTable<T>({
  columns,
  data,
  keyExtractor,
  loading = false,
  searchTerm,
  onSearchChange,
  searchPlaceholder = "Tìm kiếm...",
  filters,
  totalCountText,
  emptyTitle = "Chưa có dữ liệu",
  emptyDescription = "Không tìm thấy bản ghi nào phù hợp.",
  emptyIcon = "bi-inbox",
  emptyAction,
  className = "",
  onRowClick,
  selectedRowKey,
}: DataTableProps<T>) {
  const hasToolbar = onSearchChange !== undefined || filters !== undefined || totalCountText !== undefined;

  return (
    <div className={`space-y-3 ${className}`}>
      {/* Toolbar: Tìm kiếm, Bộ lọc, Số lượng */}
      {hasToolbar && (
        <div className="d-flex flex-column flex-sm-row justify-content-between align-items-center gap-2">
          <div className="d-flex align-items-center gap-2 w-100 w-sm-auto flex-wrap">
            {filters}
            {totalCountText && (
              <span className="small text-secondary text-nowrap">
                {totalCountText}
              </span>
            )}
          </div>

          {onSearchChange && (
            <div className="w-100 w-sm-auto">
              <input
                type="text"
                placeholder={searchPlaceholder}
                value={searchTerm || ""}
                onChange={(e) => onSearchChange(e.target.value)}
                className="form-control form-control-sm"
                style={{ minWidth: "220px", maxWidth: "280px" }}
              />
            </div>
          )}
        </div>
      )}

      {/* Table Container */}
      <div className="table-responsive">
        <table className="table table-sm table-hover align-middle mb-0">
          <thead className="text-center">
            <tr>
              {columns.map((col, idx) => (
                <th
                  key={idx}
                  style={{ width: col.width }}
                  className={`text-${col.align || "left"} ${col.className || ""}`}
                >
                  {col.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={columns.length} className="text-center py-5 text-muted">
                  <div className="d-flex flex-column align-items-center justify-content-center gap-2">
                    <span className="spinner-border spinner-border-sm text-primary" role="status"></span>
                    <span className="small">Đang tải dữ liệu...</span>
                  </div>
                </td>
              </tr>
            ) : data.length === 0 ? (
              <tr>
                <td colSpan={columns.length} className="p-0">
                  <EmptyState
                    icon={emptyIcon}
                    title={emptyTitle}
                    description={emptyDescription}
                    action={emptyAction}
                  />
                </td>
              </tr>
            ) : (
              data.map((item, rowIdx) => {
                const key = keyExtractor(item, rowIdx);
                const isSelected = selectedRowKey !== undefined && selectedRowKey !== null && selectedRowKey === key;

                return (
                  <tr
                    key={key}
                    onClick={() => onRowClick && onRowClick(item)}
                    style={{
                      cursor: onRowClick ? "pointer" : "default",
                      backgroundColor: isSelected ? "#eff6ff" : "transparent",
                    }}
                  >
                    {columns.map((col, colIdx) => (
                      <td
                        key={colIdx}
                        className={`text-${col.align || "left"} ${col.className || ""}`}
                      >
                        {col.render(item, rowIdx)}
                      </td>
                    ))}
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default DataTable;
