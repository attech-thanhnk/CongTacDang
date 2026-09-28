"use client";

import React, { useMemo } from "react";
import type { EffectivePermissionItem, UserEffectivePermissions } from "@/services/roleService";

/** Tên phân hệ dự phòng khi không tải được danh mục quyền (người xem không có quyền quản lý vai trò). */
const FALLBACK_MODULE_NAMES: Record<string, string> = {
  system: "Quản trị hệ thống",
  catalog: "Danh mục",
  period: "Kỳ đánh giá",
  evaluation: "Đánh giá cá nhân",
  collective: "Đánh giá tập thể",
  meeting: "Hội nghị, kiểm phiếu",
  report: "Báo cáo",
  attachment: "Văn bản, tài liệu",
};

export interface EffectivePermissionsPanelProps {
  data: UserEffectivePermissions | null;
  loading: boolean;
  error: string | null;
  /** Tên phân hệ từ danh mục quyền (nếu tải được). */
  moduleNames?: Record<string, string>;
}

/** "Người này làm được gì": mỗi quyền kèm phạm vi và vai trò nguồn. */
export function EffectivePermissionsPanel({ data, loading, error, moduleNames }: EffectivePermissionsPanelProps) {
  const groups = useMemo(() => {
    const map = new Map<string, EffectivePermissionItem[]>();
    (data?.permissions ?? []).forEach((permission) => {
      const list = map.get(permission.module) ?? [];
      list.push(permission);
      map.set(permission.module, list);
    });
    return Array.from(map.entries());
  }, [data]);

  if (loading) {
    return (
      <div className="text-center py-4 text-secondary small">
        <span className="spinner-border spinner-border-sm me-2" />
        Đang tính quyền hiệu lực…
      </div>
    );
  }
  if (error) return <div className="alert alert-danger small m-3">{error}</div>;
  if (!data) return null;

  return (
    <div>
      {!data.isActive && (
        <div className="alert alert-warning small m-3">
          Tài khoản đang bị vô hiệu hóa nên hiện <strong>không có quyền nào</strong>, dù vẫn còn bản gán vai trò.
        </div>
      )}
      {data.permissions.length === 0 ? (
        <div className="text-center py-4 text-secondary small">
          Người này chưa có quyền nào (chưa được gán vai trò đang hiệu lực).
        </div>
      ) : (
        <div className="table-responsive">
          <table className="table align-middle mb-0" style={{ fontSize: 13 }}>
            <thead style={{ background: "var(--bg-base)" }}>
              <tr>
                <th className="ps-3" style={{ width: "36%" }}>Quyền</th>
                <th>Phạm vi</th>
                <th className="pe-3">Vai trò nguồn</th>
              </tr>
            </thead>
            <tbody>
              {groups.map(([module, permissions]) => (
                <React.Fragment key={module}>
                  <tr className="table-light">
                    <td colSpan={3} className="ps-3 fw-semibold small text-uppercase text-secondary">
                      {moduleNames?.[module] ?? FALLBACK_MODULE_NAMES[module] ?? module}
                    </td>
                  </tr>
                  {permissions.map((permission) => (
                    <tr key={permission.code}>
                      <td className="ps-3">
                        <div className="fw-semibold text-dark">{permission.name}</div>
                        <code className="small text-secondary">{permission.code}</code>
                      </td>
                      <td>
                        {permission.sources.map((source) => (
                          <div key={`${source.assignmentId}-${source.scopeId ?? "global"}`} className="text-nowrap">
                            <i
                              className={`bi ${source.scopeType === "Global" ? "bi-globe2" : source.scopeType === "Department" ? "bi-building" : "bi-flag"} me-1 text-secondary`}
                            />
                            {source.scopeName}
                          </div>
                        ))}
                      </td>
                      <td className="pe-3">
                        {permission.sources.map((source) => (
                          <div key={`${source.assignmentId}-role`}>{source.roleName}</div>
                        ))}
                      </td>
                    </tr>
                  ))}
                </React.Fragment>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

export default EffectivePermissionsPanel;
