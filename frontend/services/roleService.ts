import { request } from "./apiClient";

/** Thông tin quyền hạn nguyên tử */
export interface PermissionItem {
  /** Mã định danh quyền (task 09: bằng mã quyền) */
  id: string;
  /** Mã quyền (VD: evaluation.read) */
  code: string;
  /** Tên hiển thị quyền */
  name: string;
  /** Phân hệ */
  resource: string;
  /** Không còn dùng (giữ tương thích giao diện cũ) */
  action: string;
  /** Mô tả chi tiết quyền */
  description: string;
}

/** Thông tin vai trò kèm danh sách quyền hạn */
export interface RoleDetailItem {
  /** Mã định danh vai trò */
  id: string;
  /** Không còn dùng để phân quyền (task 09); giữ tương thích giao diện cũ */
  code: string;
  /** Tên hiển thị vai trò */
  name: string;
  /** Mô tả vai trò */
  description: string;
  /** Vai trò được bảo vệ (không xóa, không gỡ quyền quản trị) */
  isSystem: boolean;
  /** Danh sách quyền hạn được gán */
  permissions: PermissionItem[];
}

/** Vai trò theo API quản trị mới (GET /api/admin/roles) */
interface AdminRoleResponse {
  id: string;
  name: string;
  description: string;
  isProtected: boolean;
  isSystem: boolean;
  permissionCodes: string[];
  assignmentCount: number;
}

/** Nhóm quyền theo phân hệ (GET /api/admin/permissions) */
interface PermissionModuleResponse {
  module: string;
  moduleName: string;
  permissions: { code: string; name: string; module: string; description: string; appliesScope: boolean }[];
}

// Lớp chuyển đổi tối thiểu để trang /users cũ không vỡ sau task 09 — task 11 viết lại theo API mới.
export const roleService = {
  /** Lấy toàn bộ danh sách vai trò kèm quyền hạn */
  async getAdminRoles(): Promise<RoleDetailItem[]> {
    const [roles, permissions] = await Promise.all([
      request<AdminRoleResponse[]>("/admin/roles"),
      roleService.getAdminPermissions(),
    ]);
    const byCode = new Map(permissions.map((p) => [p.code, p]));
    return roles.map((r) => ({
      id: r.id,
      code: "",
      name: r.name,
      description: r.description,
      isSystem: r.isProtected,
      permissions: r.permissionCodes.map(
        (code) => byCode.get(code) ?? { id: code, code, name: code, resource: "", action: "", description: "" }
      ),
    }));
  },

  /** Lấy danh mục quyền (phẳng) */
  async getAdminPermissions(): Promise<PermissionItem[]> {
    const modules = await request<PermissionModuleResponse[]>("/admin/permissions");
    return modules.flatMap((m) =>
      m.permissions.map((p) => ({
        id: p.code,
        code: p.code,
        name: p.name,
        resource: m.moduleName,
        action: "",
        description: p.description,
      }))
    );
  },

  /** Cập nhật danh sách quyền hạn cho một vai trò */
  async updateRolePermissions(roleId: string, permissionCodes: string[]): Promise<unknown> {
    return request(`/admin/roles/${roleId}/permissions`, {
      method: "PUT",
      body: JSON.stringify({ permissionCodes }),
    });
  },

  /** Đặt các vai trò phạm vi Toàn công ty cho cán bộ (theo Id vai trò) */
  async assignUserRoles(userId: string, roleIds: string[]): Promise<unknown> {
    return request(`/admin/users/${userId}/roles`, {
      method: "POST",
      body: JSON.stringify({ roleIds }),
    });
  },
};
