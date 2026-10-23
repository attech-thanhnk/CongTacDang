"use client";

import React from "react";
import { RowActions } from "@/components/common/RowActions";
import Link from "next/link";
import { Pagination } from "@/components/common/Pagination";
import { usePagination } from "@/components/common/usePagination";
import type { AssignmentStatus, RoleAssignment } from "@/services/roleService";
import { formatDateTime } from "./adminUtils";

const STATUS_META: Record<AssignmentStatus, { label: string; className: string }> = {
  Active: { label: "Đang hiệu lực", className: "tone-success" },
  Future: { label: "Sắp hiệu lực", className: "tone-info" },
  Expired: { label: "Đã hết hạn", className: "tone-neutral" },
};

const SCOPE_ICON: Record<RoleAssignment["scopeType"], string> = {
  Global: "bi-globe2",
  Department: "bi-building",
  PartyCell: "bi-flag",
};

export interface AssignmentTableProps {
  assignments: RoleAssignment[];
  /** Hiện cột người được gán (trang vai trò). */
  showUser?: boolean;
  /** Hiện cột vai trò (trang người dùng). */
  showRole?: boolean;
  /** Có quyền sửa / kết thúc / xóa. */
  canManage: boolean;
  emptyText: string;
  onEdit?: (assignment: RoleAssignment) => void;
  onEnd?: (assignment: RoleAssignment) => void;
  onDelete?: (assignment: RoleAssignment) => void;
  /** Có thì phân trang (khóa ghi nhớ số dòng/trang); bỏ trống thì hiện mọi dòng. */
  pageKey?: string;
  resetKeys?: readonly unknown[];
}

/** Bảng bản gán vai trò (phạm vi, thời hạn, trạng thái, ghi chú). */
export function AssignmentTable({
  assignments,
  showUser,
  showRole,
  canManage,
  emptyText,
  onEdit,
  onEnd,
  onDelete,
  pageKey,
  resetKeys,
}: AssignmentTableProps) {
  const columnCount = 4 + (showUser ? 1 : 0) + (showRole ? 1 : 0) + (canManage ? 1 : 0);
  const paging = usePagination(assignments, { storageKey: pageKey, resetKeys });
  const rows = pageKey ? paging.pageItems : assignments;
  return (
    <>
    <div className="table-responsive">
      <table className="table table-hover align-middle mb-0">
        <thead>
          <tr>
            {showUser && <th className="ps-3">Người được gán</th>}
            {showRole && <th className={showUser ? "" : "ps-3"}>Vai trò</th>}
            <th>Phạm vi</th>
            <th>Thời hạn</th>
            <th>Trạng thái</th>
            <th>Ghi chú</th>
            {canManage && <th className="pe-3 text-end">Thao tác</th>}
          </tr>
        </thead>
        <tbody>
          {assignments.length === 0 ? (
            <tr>
              <td colSpan={columnCount} className="text-center py-4 text-secondary">
                {emptyText}
              </td>
            </tr>
          ) : (
            rows.map((a) => {
              const status = STATUS_META[a.status] ?? STATUS_META.Active;
              return (
                <tr key={a.id} style={{ opacity: a.status === "Expired" ? 0.7 : 1 }}>
                  {showUser && (
                    <td className="ps-3">
                      <Link href={`/admin/users/${a.userId}`} className="fw-semibold text-decoration-none">
                        {a.fullName}
                      </Link>
                      <div className="small text-secondary">{a.username}</div>
                    </td>
                  )}
                  {showRole && (
                    <td className={showUser ? "fw-semibold" : "ps-3 fw-semibold"}>{a.roleName}</td>
                  )}
                  <td className="text-nowrap">
                    <i className={`bi ${SCOPE_ICON[a.scopeType] ?? "bi-globe2"} me-1 text-secondary`} />
                    {a.scopeName}
                  </td>
                  <td className="small text-nowrap">
                    <div>Từ {formatDateTime(a.validFrom)}</div>
                    <div className="text-secondary">{a.validTo ? `Đến ${formatDateTime(a.validTo)}` : "Không thời hạn"}</div>
                  </td>
                  <td>
                    <span className={`status-badge ${status.className}`}>{status.label}</span>
                  </td>
                  <td className="small text-secondary" style={{ maxWidth: 220 }}>
                    {a.note || "—"}
                  </td>
                  {canManage && (
                    <td className="pe-3 text-end text-nowrap">
                      <RowActions
                        itemLabel={showUser ? a.fullName : a.roleName}
                        iconOnly
                        primary={[{ key: "edit", label: "Sửa thời hạn, ghi chú", icon: "bi-pencil", hidden: a.status === "Expired" || !onEdit, onClick: () => onEdit?.(a) }]}
                        menu={[
                          { key: "end", label: "Kết thúc ngay", icon: "bi-stop-circle", danger: true, hidden: a.status !== "Active" || !onEnd, onClick: () => onEnd?.(a) },
                          { key: "delete", label: "Xóa phân công", icon: "bi-trash", danger: true, hidden: !onDelete, onClick: () => onDelete?.(a) },
                        ]}
                      />
                    </td>
                  )}
                </tr>
              );
            })
          )}
        </tbody>
      </table>
    </div>
    {pageKey && (
      <Pagination
        page={paging.page}
        pageSize={paging.pageSize}
        total={paging.total}
        onPageChange={paging.setPage}
        onPageSizeChange={paging.setPageSize}
      />
    )}
    </>
  );
}

export default AssignmentTable;
