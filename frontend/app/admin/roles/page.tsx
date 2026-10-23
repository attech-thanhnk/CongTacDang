"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { Card, PageHeader } from "@/components/common";
import { RowActions } from "@/components/common/RowActions";
import { NoAccess } from "@/components/admin/NoAccess";
import { RoleFormModal } from "@/components/admin/RoleFormModal";
import { errorMessage, errorTitle } from "@/components/admin/adminUtils";
import { AdminRole, roleService, SaveRolePayload } from "@/services/roleService";

/** Danh sách vai trò: tạo, đổi tên/mô tả, xóa. Ma trận quyền ở trang chi tiết. */
export default function AdminRolesPage() {
  const router = useRouter();
  const { hasPermissionIn } = useAuth();
  const { toast, confirm } = useToast();
  const canManage = hasPermissionIn("system.roles.manage", "Global");

  const [roles, setRoles] = useState<AdminRole[]>([]);
  const [loading, setLoading] = useState(true);
  const [form, setForm] = useState<{ role?: AdminRole } | null>(null);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRoles(await roleService.listRoles());
    } catch (err) {
      toast.error(errorMessage(err, "Không tải được danh sách vai trò."), errorTitle(err));
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (canManage) load();
  }, [canManage, load]);

  const handleSave = async (payload: SaveRolePayload) => {
    setSaving(true);
    try {
      if (form?.role) {
        await roleService.updateRole(form.role.id, payload);
        toast.success(`Đã cập nhật vai trò "${payload.name}".`);
        setForm(null);
        await load();
      } else {
        const created = await roleService.createRole(payload);
        toast.success(`Đã tạo vai trò "${created.name}". Hãy chọn quyền cho vai trò.`);
        setForm(null);
        router.push(`/admin/roles/${created.id}`);
      }
    } catch (err) {
      toast.error(errorMessage(err, "Không lưu được vai trò."), errorTitle(err), 8000);
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = (role: AdminRole) =>
    confirm({
      title: "Xóa vai trò",
      message:
        role.assignmentCount > 0
          ? `Vai trò "${role.name}" đang có ${role.assignmentCount} phân công đang hoặc sắp hiệu lực. Hãy kết thúc các phân công trước khi xóa. Vẫn thử xóa?`
          : `Xóa vai trò "${role.name}"?`,
      confirmText: "Xóa",
      isDanger: true,
      onConfirm: async () => {
        try {
          await roleService.deleteRole(role.id);
          toast.success(`Đã xóa vai trò "${role.name}".`);
          await load();
        } catch (err) {
          // 409: vai trò bảo vệ, hoặc "đang có N bản gán" — hiện đúng thông báo máy chủ.
          toast.error(errorMessage(err, "Không xóa được vai trò."), errorTitle(err), 10000);
        }
      },
    });

  if (!canManage) return <NoAccess title="Vai trò" permissionName="Quản lý vai trò" />;

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Vai trò"
        actions={
          <button type="button" className="btn btn-primary" onClick={() => setForm({})}>
            <i className="bi bi-plus-lg me-1" />
            Tạo vai trò
          </button>
        }
      />
      <div className="page-body">
        <Card>
          <div className="table-responsive">
            <table className="table table-hover align-middle mb-0 table-fit">
              <thead>
                <tr>
                  <th className="ps-3">Vai trò</th>
                  <th className="d-none d-md-table-cell">Mô tả</th>
                  <th className="text-center d-none d-md-table-cell">Số quyền</th>
                  <th className="text-center d-none d-md-table-cell">Phân công đang hiệu lực</th>
                  <th className="pe-3 text-end">Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {loading ? (
                  <tr>
                    <td colSpan={5} className="text-center py-5 text-secondary loading-delay">
                      <span className="spinner-border spinner-border-sm me-2" />
                      Đang tải…
                    </td>
                  </tr>
                ) : roles.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="text-center py-5 text-secondary">
                      Chưa có vai trò nào.
                    </td>
                  </tr>
                ) : (
                  roles.map((role) => (
                    <tr key={role.id}>
                      <td className="ps-3">
                        <Link href={`/admin/roles/${role.id}`} className="fw-semibold text-decoration-none">
                          {role.name}
                        </Link>
                        {role.isProtected && (
                          <span className="status-badge tone-neutral ms-2" title="Không xóa được, không gỡ được quyền quản trị">
                            <i className="bi bi-lock-fill me-1" />
                            Bảo vệ
                          </span>
                        )}
                      </td>
                      <td className="small text-secondary d-none d-md-table-cell">{role.description || "—"}</td>
                      <td className="text-center d-none d-md-table-cell">{role.permissionCodes.length}</td>
                      <td className="text-center d-none d-md-table-cell">{role.assignmentCount}</td>
                      <td className="pe-3 text-end text-nowrap">
                        <RowActions
                          itemLabel={role.name}
                          primary={[
                            {
                              key: "permissions",
                              label: "Phân quyền",
                              title: "Quyền và người được gán",
                              icon: "bi-shield-check",
                              onClick: () => router.push(`/admin/roles/${role.id}`),
                            },
                          ]}
                          menu={[
                            { key: "edit", label: "Sửa", icon: "bi-pencil", onClick: () => setForm({ role }) },
                            { key: "delete", label: "Xóa", icon: "bi-trash", danger: true, hidden: role.isProtected, onClick: () => handleDelete(role) },
                          ]}
                        />
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </Card>
      </div>

      {form && <RoleFormModal role={form.role} saving={saving} onClose={() => setForm(null)} onSubmit={handleSave} />}
    </div>
  );
}
