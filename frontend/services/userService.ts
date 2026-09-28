import { request } from "./apiClient";

/** Thông tin hồ sơ cán bộ đang đăng nhập */
export interface UserProfile {
  /** Mã định danh cán bộ */
  id: string;
  /** Họ và tên đầy đủ */
  fullName: string;
  /** Tên tài khoản */
  userName: string;
  /** Chức vụ công tác Đảng */
  partyRole: string;
  /** Chức danh quản lý chuyên môn chính quyền */
  adminTitle: string;
  /** Tên Chi bộ sinh hoạt */
  partyBranchName?: string;
  /** Tên đơn vị / Phòng ban chuyên môn */
  adminDeptName?: string;
  /** Nhóm chức danh công tác */
  jobGroup?: string;
  /** Danh sách vai trò hệ thống */
  roles?: string[];
}

/** Thông tin cán bộ hiển thị trên danh sách quản trị */
export interface CadreItem {
  /** Mã định danh cán bộ */
  id: string;
  /** Họ và tên đầy đủ */
  fullName: string;
  /** Tên tài khoản */
  userName?: string;
  /** Số thẻ Đảng viên */
  partyCardNumber?: string;
  /** Chức vụ công tác Đảng */
  partyRole?: string;
  /** Chức danh quản lý chuyên môn */
  adminTitle?: string;
  /** Tên Chi bộ sinh hoạt */
  branchName?: string;
  /** Tên Chi bộ (bí danh) */
  partyCellName?: string;
  /** Mã Chi bộ sinh hoạt */
  partyCellId?: string;
  branchId?: string;
  /** Tên Phòng ban chuyên môn */
  departmentName?: string;
  /** Đã kết nạp Đảng viên hay chưa */
  isPartyMember?: boolean;
  /** Trạng thái hồ sơ */
  isActive: boolean;
}

/** Dữ liệu gửi lên khi tạo mới hồ sơ cán bộ */
export interface CreateUserPayload {
  /** Họ và tên cán bộ (bắt buộc) */
  fullName: string;
  /** Số thẻ Đảng viên */
  partyCardNumber?: string | null;
  /** Chức danh quản lý chính quyền */
  adminTitle?: string | null;
  /** Mã Chi bộ sinh hoạt */
  partyCellId?: string | null;
}

/** Vai trò người dùng */
export interface RoleItem {
  /** Mã định danh vai trò */
  code: string;
  /** Tên hiển thị vai trò */
  name: string;
  /** Mô tả thẩm quyền của vai trò */
  description: string;
}

export const userService = {
  /** Lấy thông tin hồ sơ của tài khoản đang đăng nhập */
  async getProfile(): Promise<UserProfile> {
    return request<UserProfile>("/users/profile");
  },

  /** Lấy danh sách toàn bộ cán bộ lãnh đạo, quản lý */
  async getUsers(): Promise<CadreItem[]> {
    return request<CadreItem[]>("/users/list");
  },

  /** Tiếp nhận hồ sơ cán bộ mới vào hệ thống */
  async createUser(payload: CreateUserPayload): Promise<{ message: string; id: string }> {
    return request<{ message: string; id: string }>("/users/create", {
      method: "POST",
      body: JSON.stringify(payload),
    });
  },

  /** Cập nhật thông tin hồ sơ cán bộ */
  async updateUser(id: string, payload: Partial<CreateUserPayload>): Promise<any> {
    return request(`/users/${id}`, {
      method: "PUT",
      body: JSON.stringify(payload),
    });
  },

  /** Xóa hồ sơ cán bộ khỏi hệ thống */
  async deleteUser(id: string): Promise<{ message: string }> {
    return request<{ message: string }>(`/users/${id}`, {
      method: "DELETE",
    });
  },

  /** Đặt lại mật khẩu tạm, trả mật khẩu đúng một lần cho quản trị viên */
  async resetPassword(id: string): Promise<{ userId: string; userName: string; temporaryPassword: string; mustChangePassword: boolean }> {
    return request(`/users/${id}/reset-password`, { method: "POST" });
  },

  /** Lấy danh mục các vai trò hệ thống */
  async getRoles(): Promise<RoleItem[]> {
    return request<RoleItem[]>("/users/roles");
  },
};
