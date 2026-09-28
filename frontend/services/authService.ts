import { request } from "./apiClient";

/** Thông tin phiên làm việc của người dùng đang đăng nhập */
export interface UserSession {
  /** Mã định danh cán bộ */
  id: string;
  /** Họ và tên cán bộ */
  fullName: string;
  /** Tên tài khoản */
  userName: string;
  /** Danh sách vai trò hệ thống (CAN_BO, BI_THU_CHI_BO, ...) */
  roles: string[];
  /** Danh sách quyền hạn nguyên tử (users.read, ...) */
  permissions: string[];
  /** Bắt buộc đổi mật khẩu tạm */
  mustChangePassword: boolean;
  /** Thời điểm hết hạn phiên làm việc */
  expiresAt?: string;
}

export const authService = {
  /** Đăng nhập hệ thống — nhận session và cookie HttpOnly auth_token */
  async login(username: string, password: string): Promise<UserSession> {
    return request<UserSession>("/auth/login", {
      method: "POST",
      body: JSON.stringify({ username, password }),
    });
  },

  /** Đăng xuất hệ thống — xóa cookie trên trình duyệt */
  async logout(): Promise<void> {
    return request<void>("/auth/logout", {
      method: "POST",
    });
  },

  /** Lấy thông tin phiên đăng nhập hiện tại từ JWT claims */
  async getMe(): Promise<UserSession> {
    return request<UserSession>("/auth/me");
  },

  /** Làm mới phiên làm việc bằng Refresh Token */
  async refreshToken(): Promise<UserSession> {
    return request<UserSession>("/auth/refresh-token", {
      method: "POST",
    });
  },

  /** Đổi mật khẩu của tài khoản hiện tại */
  async changePassword(currentPassword: string, newPassword: string): Promise<void> {
    return request<void>("/auth/change-password", {
      method: "POST",
      body: JSON.stringify({ currentPassword, newPassword }),
    });
  },
};
