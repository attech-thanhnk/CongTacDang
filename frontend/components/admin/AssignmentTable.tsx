"use client";

import React from "react";
import Link from "next/link";
import type { AssignmentStatus, RoleAssignment } from "@/services/roleService";
import { formatDateTime } from "./adminUtils";

const STATUS_META: Record<AssignmentStatus, { label: string; className: string }> = {
  Active: { label: "Đang hiệu lực", className: "bg-success-subtle text-success border border-success-subtle" },
  Future: { label: "Sắp hiệu lực", className: "bg-info-subtle text-info-emphasis border border-info-subtle" },
  Expired: { label: "Đã hết hạn", className: "bg-secondary-subtle text-secondary border" },
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
}

/** Bảng bản gán vai trò (phạm vi, thời hạn, trạng thái, ghi chú). */
export function AssignmentTable({ assignments, showUser, showRole, canManage, emptyText, onEdit, onEnd, onDelete }: AssignmentTableProps) {
  const columnCount = 4 + (showUser ? 1 : 0) + (showRole ? 1 : 0) + (canManage ? 1 : 0);
  return (
    <div className="table-responsive">
      <table className="table table-hover align-middle mb-0" style={{ fontSize: 13 }}>
        <thead style={{ background: "var(--bg-base)" }}>
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
            assignments.map((a) => {
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
                    <span className={`badge ${status.className}`}>{status.label}</span>
                  </td>
                  <td className="small text-secondary" style={{ maxWidth: 220 }}>
                    {a.note || "—"}
                  </td>
                  {canManage && (
                    <td className="pe-3 text-end text-nowrap">
                      {a.status !== "Expired" && onEdit && (
                        <button type="button" className="btn btn-sm btn-link p-1" title="Sửa thời hạn, ghi chú" onClick={() => onEdit(a)}>
                          <i className="bi bi-pencil-square" />
                        </button>
                      )}
                      {a.status === "Active" && onEnd && (
                        <button type="button" className="btn btn-sm btn-link p-1 text-warning" title="Kết thúc ngay" onClick={() => onEnd(a)}>
                          <i className="bi bi-stop-circle" />
                        </button>
                      )}
                      {onDelete && (
                        <button type="button" className="btn btn-sm btn-link p-1 text-danger" title="Xóa bản gán" onClick={() => onDelete(a)}>
                          <i className="bi bi-trash" />
                        </button>
                      )}
                    </td>
                  )}
                </tr>
              );
            })
          )}
        </tbody>
      </table>
    </div>
  );
}

export default AssignmentTable;
