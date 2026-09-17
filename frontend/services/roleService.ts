import { request } from "./apiClient";

/** Thông tin quyền hạn nguyên tử */
export interface PermissionItem {
  /** Mã định danh quyền */
  id: string;
  /** Mã quyền (VD: users.read) */
  code: string;
  /** Tên hiển thị quyền */
  name: string;
  /** Tài nguyên áp dụng */
  resource: string;
  /** Hành động cho phép */
  action: string;
  /** Mô tả chi tiết quyền */
  description: string;
}

/** Thông tin vai trò kèm danh sách quyền hạn */
export interface RoleDetailItem {
  /** Mã định danh vai trò */
  id: string;
  /** Mã vai trò (CAN_BO, BI_THU_CHI_BO, ...) */
  code: string;
  /** Tên hiển thị vai trò */
  name: string;
  /** Mô tả vai trò */
  description: string;
  /** Vai trò hệ thống mặc định */
  isSystem: boolean;
  /** Danh sách quyền hạn được gán */
  permissions: PermissionItem[];
}

export const roleService = {
  /** Lấy toàn bộ danh sách vai trò kèm quyền hạn (Dynamic RBAC) */
  async getAdminRoles(): Promise<RoleDetailItem[]> {
    return request<RoleDetailItem[]>("/admin/roles");
  },

  /** Lấy danh sách 14 quyền nguyên tử trong hệ thống */
  async getAdminPermissions(): Promise<PermissionItem[]> {
    return request<PermissionItem[]>("/admin/permissions");
  },

  /** Cập nhật danh sách quyền hạn cho một vai trò */
  async updateRolePermissions(roleId: string, permissionCodes: string[]): Promise<any> {
    return request(`/admin/roles/${roleId}/permissions`, {
      method: "PUT",
      body: JSON.stringify({ permissionCodes }),
    });
  },

  /** Gán vai trò cho cán bộ */
  async assignUserRoles(userId: string, roleCodes: string[]): Promise<any> {
    return request(`/admin/users/${userId}/roles`, {
      method: "POST",
      body: JSON.stringify({ roleCodes }),
    });
  },
};
