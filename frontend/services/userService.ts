import { request } from "./apiClient";

export interface UserProfile {
  id: string;
  fullName: string;
  userName: string;
  partyRole: string;
  adminTitle: string;
  partyBranchName?: string;
  adminDeptName?: string;
  jobGroup?: string;
  roles?: string[];
}

export interface CadreItem {
  id: string;
  fullName: string;
  userName?: string;
  partyCardNumber?: string;
  partyRole?: string;
  adminTitle?: string;
  branchName?: string;
  partyCellName?: string;
  departmentName?: string;
  isPartyMember?: boolean;
  isActive: boolean;
}

export interface CreateUserPayload {
  fullName: string;
  partyCardNumber?: string | null;
  adminTitle?: string | null;
  partyCellId?: string | null;
}

export interface RoleItem {
  code: string;
  name: string;
  description: string;
}

export const userService = {
  async getProfile(): Promise<UserProfile> {
    return request<UserProfile>("/users/profile");
  },

  async getUsers(): Promise<CadreItem[]> {
    return request<CadreItem[]>("/users/list");
  },

  async createUser(payload: CreateUserPayload): Promise<{ message: string; id: string }> {
    return request<{ message: string; id: string }>("/users/create", {
      method: "POST",
      body: JSON.stringify(payload),
    });
  },

  async updateUser(id: string, payload: Partial<CreateUserPayload>): Promise<any> {
    return request(`/users/${id}`, {
      method: "PUT",
      body: JSON.stringify(payload),
    });
  },

  async deleteUser(id: string): Promise<{ message: string }> {
    return request<{ message: string }>(`/users/${id}`, {
      method: "DELETE",
    });
  },

  async getRoles(): Promise<RoleItem[]> {
    return request<RoleItem[]>("/users/roles");
  },
};
