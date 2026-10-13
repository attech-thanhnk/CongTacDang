import { request } from "./apiClient";

/** Loại phạm vi của bản gán vai trò. */
export type ScopeType = "Global" | "Department" | "PartyCell";

export const SCOPE_TYPE_LABELS: Record<ScopeType, string> = {
  Global: "Toàn công ty",
  Department: "Đơn vị chính quyền",
  PartyCell: "Tổ chức Đảng",
};

/** Trạng thái bản gán tại thời điểm tra cứu. */
export type AssignmentStatus = "Active" | "Future" | "Expired";

/** Một mã quyền trong danh mục. */
export interface PermissionDefinition {
  code: string;
  name: string;
  module: string;
  description: string;
  /** false: quyền chỉ có nghĩa khi gán phạm vi Toàn công ty. */
  appliesScope: boolean;
  sortOrder: number;
}

/** Nhóm quyền theo phân hệ (`GET /api/admin/permissions`). */
export interface PermissionModule {
  module: string;
  moduleName: string;
  permissions: PermissionDefinition[];
}

/** Vai trò (`GET /api/admin/roles`). */
export interface AdminRole {
  id: string;
  name: string;
  description: string;
  /** Không xóa được, không gỡ được 2 quyền quản trị. */
  isProtected: boolean;
  isSystem: boolean;
  permissionCodes: string[];
  /** Số bản gán đang hoặc sắp hiệu lực. */
  assignmentCount: number;
}

export interface SaveRolePayload {
  name: string;
  description?: string;
}

/** Bản gán vai trò. */
export interface RoleAssignment {
  id: string;
  userId: string;
  username: string;
  fullName: string;
  roleId: string;
  roleName: string;
  scopeType: ScopeType;
  scopeId?: string | null;
  scopeName: string;
  validFrom: string;
  /** Không bao gồm; null = không thời hạn. */
  validTo?: string | null;
  note?: string | null;
  status: AssignmentStatus;
}

export interface AssignmentQuery {
  userId?: string;
  roleId?: string;
  scopeType?: ScopeType;
  scopeId?: string;
  /** Có giá trị → chỉ bản gán hiệu lực tại thời điểm này; bỏ trống → gồm cả lịch sử. */
  activeOn?: string;
}

export interface CreateAssignmentPayload {
  userId: string;
  roleId: string;
  scopeType: ScopeType;
  scopeId?: string;
  validFrom?: string;
  validTo?: string;
  note?: string;
}

export interface UpdateAssignmentPayload {
  /** Bỏ trống = giữ nguyên. */
  validFrom?: string;
  /** Bỏ trống = không thời hạn. */
  validTo?: string;
  note?: string;
}

/** Nguồn cấp một quyền: phạm vi + vai trò + bản gán. */
export interface EffectiveGrantSource {
  scopeType: ScopeType;
  scopeId?: string | null;
  scopeName: string;
  roleName: string;
  assignmentId: string;
}

export interface EffectivePermissionItem {
  code: string;
  name: string;
  module: string;
  sources: EffectiveGrantSource[];
}

/** "Người này làm được gì" (`GET /api/admin/users/{id}/effective-permissions`). */
export interface UserEffectivePermissions {
  userId: string;
  isActive: boolean;
  permissions: EffectivePermissionItem[];
  assignments: RoleAssignment[];
}

function toQuery(params: Record<string, string | undefined>): string {
  const search = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value) search.set(key, value);
  });
  const query = search.toString();
  return query ? `?${query}` : "";
}

/** Dịch vụ quản trị vai trò, quyền và bản gán (API task 09). */
export const roleService = {
  listRoles(): Promise<AdminRole[]> {
    return request<AdminRole[]>("/admin/roles");
  },

  getRole(id: string): Promise<AdminRole> {
    return request<AdminRole>(`/admin/roles/${id}`);
  },

  createRole(payload: SaveRolePayload & { permissionCodes?: string[] }): Promise<AdminRole> {
    return request<AdminRole>("/admin/roles", { method: "POST", body: JSON.stringify(payload) });
  },

  updateRole(id: string, payload: SaveRolePayload): Promise<AdminRole> {
    return request<AdminRole>(`/admin/roles/${id}`, { method: "PUT", body: JSON.stringify(payload) });
  },

  /** Đặt lại toàn bộ quyền của vai trò. */
  setRolePermissions(id: string, permissionCodes: string[]): Promise<AdminRole> {
    return request<AdminRole>(`/admin/roles/${id}/permissions`, {
      method: "PUT",
      body: JSON.stringify({ permissionCodes }),
    });
  },

  /** Xóa vai trò (409 khi vai trò bảo vệ hoặc còn bản gán). */
  deleteRole(id: string): Promise<void> {
    return request<void>(`/admin/roles/${id}`, { method: "DELETE" });
  },

  /** Danh mục quyền nhóm theo phân hệ. */
  listPermissions(): Promise<PermissionModule[]> {
    return request<PermissionModule[]>("/admin/permissions");
  },

  listAssignments(query: AssignmentQuery = {}): Promise<RoleAssignment[]> {
    return request<RoleAssignment[]>(
      `/admin/assignments${toQuery({
        userId: query.userId,
        roleId: query.roleId,
        scopeType: query.scopeType,
        scopeId: query.scopeId,
        activeOn: query.activeOn,
      })}`
    );
  },

  createAssignment(payload: CreateAssignmentPayload): Promise<RoleAssignment> {
    return request<RoleAssignment>("/admin/assignments", { method: "POST", body: JSON.stringify(payload) });
  },

  updateAssignment(id: string, payload: UpdateAssignmentPayload): Promise<RoleAssignment> {
    return request<RoleAssignment>(`/admin/assignments/${id}`, { method: "PUT", body: JSON.stringify(payload) });
  },

  /** Kết thúc bản gán ngay (validTo = bây giờ). */
  endAssignment(id: string): Promise<RoleAssignment> {
    return request<RoleAssignment>(`/admin/assignments/${id}/end`, { method: "POST" });
  },

  deleteAssignment(id: string): Promise<void> {
    return request<void>(`/admin/assignments/${id}`, { method: "DELETE" });
  },

  getEffectivePermissions(userId: string): Promise<UserEffectivePermissions> {
    return request<UserEffectivePermissions>(`/admin/users/${userId}/effective-permissions`);
  },

  /** Người thực hiện các việc tại một đơn vị; bỏ trống cả hai = Toàn công ty. */
  getPerformers(unit: { departmentId?: string; partyCellId?: string }): Promise<UnitPerformers> {
    return request<UnitPerformers>(
      `/admin/performers${toQuery({ departmentId: unit.departmentId, partyCellId: unit.partyCellId })}`
    );
  },

  /** Bản gán cần xem lại (không còn gắn với nơi giao / giao tạm sắp hết hạn); có `userId` = riêng một người. */
  getPerformerReview(userId?: string): Promise<PerformerReviewItem[]> {
    return request<PerformerReviewItem[]>(`/admin/performers/review${toQuery({ userId })}`);
  },
};

/** Một người làm được việc tại đơn vị, qua một bản gán vai trò. */
export interface Performer {
  userId: string;
  fullName: string;
  username: string;
  roleName: string;
  /** Loại phạm vi của bản gán. */
  scopeType: ScopeType;
  scopeId?: string | null;
  /** Nơi gán ("Toàn công ty" hoặc tên đơn vị). */
  scopeText: string;
  assignmentId: string;
  /** Gán tại đúng đơn vị đang xem (gỡ được tại màn hình này). */
  assignedHere: boolean;
  validTo?: string | null;
  /** Chức vụ người này giữ tại nơi giao (hoặc đơn vị cấp trên), "Cán bộ của đơn vị", hoặc null nếu không liên quan. */
  positionAtScope?: string | null;
}

/** Người giữ chức vụ tại đơn vị đang xem. */
export interface PositionHolder {
  userId: string;
  fullName: string;
  username: string;
  positionName: string;
}

/** Bản gán cần xem lại (`GET /api/admin/performers/review`). */
export interface PerformerReviewItem {
  assignmentId: string;
  userId: string;
  fullName: string;
  username: string;
  roleName: string;
  scopeType: ScopeType;
  scopeId?: string | null;
  scopeText: string;
  validTo?: string | null;
  reasonCode: "NoPositionAtScope" | "ExpiringSoon";
  reason: string;
}

/** Vai trò chọn được khi thêm người cho một việc. */
export interface PerformerRoleOption {
  id: string;
  name: string;
  description?: string | null;
}

/** Một việc trong quy trình đánh giá và người làm được việc đó. */
export interface PerformerJob {
  permission: string;
  permissionName: string;
  inUse: boolean;
  people: Performer[];
  roles: PerformerRoleOption[];
}

/** Người thực hiện theo đơn vị (`GET /api/admin/performers`). */
export interface UnitPerformers {
  scopeType: ScopeType;
  scopeId?: string | null;
  scopeName: string;
  scopeActive: boolean;
  /** Người giữ chức vụ tại đúng đơn vị đang xem (rỗng khi Toàn công ty). */
  holders: PositionHolder[];
  jobs: PerformerJob[];
}
