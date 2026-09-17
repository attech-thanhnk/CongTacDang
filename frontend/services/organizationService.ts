import { request } from "./apiClient";

/** Thông tin Chi bộ Đảng */
export interface BranchItem {
  /** Mã định danh Chi bộ */
  id: string;
  /** Mã ký hiệu Chi bộ */
  code: string;
  /** Tên đầy đủ Chi bộ */
  name: string;
  /** Mô tả nhiệm vụ trọng tâm */
  description?: string;
  /** Số lượng cán bộ sinh hoạt */
  memberCount?: number;
  /** Danh sách thành viên (nếu có) */
  members?: any[];
}

/** Thông tin Phòng ban / Phân xưởng chuyên môn */
export interface DepartmentItem {
  /** Mã định danh phòng ban */
  id: string;
  /** Mã ký hiệu phòng ban */
  code: string;
  /** Tên phòng ban */
  name: string;
  /** Mô tả chức năng nhiệm vụ */
  description?: string;
}

export const organizationService = {
  /** Lấy danh sách toàn bộ Chi bộ trực thuộc */
  async getBranches(): Promise<BranchItem[]> {
    return request<BranchItem[]>("/organizations/branches");
  },

  /** Thành lập Chi bộ mới */
  async createBranch(payload: { code?: string; name: string; description?: string }): Promise<BranchItem> {
    return request<BranchItem>("/organizations/branches", {
      method: "POST",
      body: JSON.stringify(payload),
    });
  },

  /** Cập nhật thông tin Chi bộ */
  async updateBranch(id: string, payload: { name: string; description?: string }): Promise<BranchItem> {
    return request<BranchItem>(`/organizations/branches/${id}`, {
      method: "PUT",
      body: JSON.stringify(payload),
    });
  },

  /** Giải thể / Xóa Chi bộ (yêu cầu không có cán bộ sinh hoạt) */
  async deleteBranch(id: string): Promise<any> {
    return request(`/organizations/branches/${id}`, {
      method: "DELETE",
    });
  },

  /** Lấy danh sách Phòng ban chuyên môn */
  async getDepartments(): Promise<DepartmentItem[]> {
    return request<DepartmentItem[]>("/organizations/departments");
  },
};
