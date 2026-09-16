import { request } from "./apiClient";

export interface BranchItem {
  id: string;
  code: string;
  name: string;
  description?: string;
  memberCount?: number;
  members?: any[];
}

export interface DepartmentItem {
  id: string;
  code: string;
  name: string;
  description?: string;
}

export const organizationService = {
  async getBranches(): Promise<BranchItem[]> {
    return request<BranchItem[]>("/organizations/branches");
  },

  async createBranch(payload: { code?: string; name: string; description?: string }): Promise<BranchItem> {
    return request<BranchItem>("/organizations/branches", {
      method: "POST",
      body: JSON.stringify(payload),
    });
  },

  async updateBranch(id: string, payload: { name: string; description?: string }): Promise<BranchItem> {
    return request<BranchItem>(`/organizations/branches/${id}`, {
      method: "PUT",
      body: JSON.stringify(payload),
    });
  },

  async deleteBranch(id: string): Promise<any> {
    return request(`/organizations/branches/${id}`, {
      method: "DELETE",
    });
  },

  async getDepartments(): Promise<DepartmentItem[]> {
    return request<DepartmentItem[]>("/organizations/departments");
  },
};
