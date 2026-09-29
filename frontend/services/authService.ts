import { request } from "./apiClient";

/** Một quyền kèm phạm vi áp dụng */
export interface AccessGrant {
  /** Mã quyền */
  code: string;
  /** Loại phạm vi */
  scopeType: "Global" | "Department" | "PartyCell";
  /** Id đơn vị chính quyền / tổ chức Đảng được gán (null khi Toàn công ty) */
  scopeId: string | null;
  /** Tên phạm vi hiển thị */
  scopeName: string;
}

/** Thông tin phiên làm việc của người dùng đang đăng nhập */
export interface UserSession {
  /** Mã định danh cán bộ */
  id: string;
  /** Họ và tên cán bộ */
  fullName: string;
  /** Tên tài khoản */
  userName: string;
  /** Tên các vai trò đang hiệu lực — chỉ để hiển thị, không dùng để phân quyền */
  roles: string[];
  /** Các mã quyền có ở ít nhất một phạm vi (system.users.read, evaluation.read, ...) */
  permissions: string[];
  /** Quyền kèm phạm vi (Toàn công ty / Đơn vị chính quyền / Tổ chức Đảng — gồm cả đơn vị cấp dưới) */
  grants: AccessGrant[];
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

  /**
   * Đổi mật khẩu của tài khoản hiện tại. Máy chủ cấp lại cookie cho phiên này và đăng xuất mọi phiên khác;
   * trả về thông tin phiên mới (mustChangePassword = false).
   */
  async changePassword(currentPassword: string, newPassword: string): Promise<UserSession> {
    return request<UserSession>("/auth/change-password", {
      method: "POST",
      body: JSON.stringify({ currentPassword, newPassword }),
    });
  },
};
