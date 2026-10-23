import { request } from "./apiClient";

/** Kết quả phân trang phía máy chủ (`PagedResult<T>`). */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/** Cấp có thẩm quyền quyết định xếp loại: 1 = Đảng ủy cơ sở, 2 = cấp trên. */
export type ApprovalAuthority = 1 | 2;

export const APPROVAL_AUTHORITY_LABELS: Record<ApprovalAuthority, string> = {
  1: "Đảng ủy cơ sở quyết định",
  2: "Cấp trên quyết định",
};

/** Tài khoản trong danh sách quản trị. */
export interface AccountListItem {
  id: string;
  username: string;
  fullName: string;
  /** Ngày sinh `yyyy-MM-dd`; null = chưa nhập. */
  dateOfBirth?: string | null;
  email: string;
  phoneNumber: string;
  partyCardNumber?: string | null;
  isPartyMember: boolean;
  positionTitle: string;
  /** null = dùng khung mặc định của bộ tiêu chí. */
  weightFrameCode?: string | null;
  departmentId?: string | null;
  departmentName?: string | null;
  partyCellId?: string | null;
  partyCellName?: string | null;
  /** Thẩm quyền đang áp dụng (đặt tay hoặc suy ra từ chức vụ). */
  approvalAuthority: ApprovalAuthority;
  /** Thẩm quyền đặt tay (null = suy ra từ chức vụ). */
  approvalAuthorityOverride?: ApprovalAuthority | null;
  approvalAuthorityOverrideReason?: string | null;
  /** Người xem quản lý được tài khoản này (máy chủ tính; chỉ có ở chi tiết). */
  canManage?: boolean;
  /** false: quản trị đã vô hiệu hóa tài khoản. */
  isActive: boolean;
  /** Đang bị khóa tạm do nhập sai mật khẩu nhiều lần. */
  isLockedOut: boolean;
  lockoutEnd?: string | null;
  failedLoginCount: number;
  mustChangePassword: boolean;
  lastLoginAt?: string | null;
  createdAt: string;
}

export interface AccountSearchParams {
  page?: number;
  pageSize?: number;
  q?: string;
  departmentId?: string;
  partyCellId?: string;
  isActive?: boolean;
}

export interface CreateAccountPayload {
  username: string;
  fullName: string;
  /** Ngày sinh `yyyy-MM-dd`. */
  dateOfBirth?: string;
  email?: string;
  phoneNumber?: string;
  partyCardNumber?: string;
  positionTitle?: string;
  departmentId?: string;
  partyCellId?: string;
  /** Đặt tay thẩm quyền; không gửi = suy ra từ chức vụ. */
  approvalAuthority?: ApprovalAuthority;
  approvalAuthorityReason?: string;
  /** Mã khung tỷ trọng mặc định (danh sách từ `GET /api/criteria-sets/weight-frames`). */
  weightFrameCode?: string;
}

/**
 * Không gửi = giữ nguyên; chuỗi rỗng = xóa (email/điện thoại/số thẻ); `EMPTY_GUID` = bỏ gán đơn vị / tổ chức Đảng.
 */
export interface UpdateAccountPayload {
  fullName?: string;
  /** Ngày sinh `yyyy-MM-dd`; chuỗi rỗng = xóa. */
  dateOfBirth?: string;
  email?: string;
  phoneNumber?: string;
  partyCardNumber?: string;
  positionTitle?: string;
  departmentId?: string;
  partyCellId?: string;
  /** Mã khung tỷ trọng mặc định; chuỗi rỗng = bỏ chọn. */
  weightFrameCode?: string;
}

export interface MemberPosition {
  id: string;
  userId: string;
  positionId: string;
  positionName: string;
  side: string;
  statCode?: string | null;
  defaultApprovalAuthority?: string | null;
  partyCellId?: string | null;
  partyCellName?: string | null;
  departmentId?: string | null;
  departmentName?: string | null;
  isPrimary: boolean;
  /** Giữ dạng "Quyền" (đánh giá như chính chức vụ). */
  isActing: boolean;
  /** Chức danh hiển thị ("Quyền …" khi giữ dạng quyền). */
  displayName: string;
  validFrom: string;
  /** Mốc hết hiệu lực (không bao gồm). */
  validTo?: string | null;
  isEffective: boolean;
  note?: string | null;
}

export interface SaveMemberPositionPayload {
  positionId?: string;
  partyCellId?: string;
  departmentId?: string;
  isPrimary?: boolean;
  isActing?: boolean;
  validFrom?: string;
  validTo?: string;
  clearValidTo?: boolean;
  note?: string;
}

export interface ApprovalAuthorityInfo {
  effective: "CoSo" | "CapTren";
  derived: "CoSo" | "CapTren";
  override?: "CoSo" | "CapTren" | null;
  overrideReason?: string | null;
  capTrenPositions: string[];
  statCode?: string | null;
}

/** Guid rỗng — máy chủ hiểu là bỏ gán Phòng/Chi bộ. */
export const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";

/** Kết quả tạo tài khoản: mật khẩu tạm chỉ trả về đúng lần này. */
export interface CreatedAccount {
  userId: string;
  username: string;
  temporaryPassword: string;
}

/** Kết quả đặt lại mật khẩu: mật khẩu tạm chỉ trả về đúng lần này. */
export interface ResetPasswordResult {
  userId: string;
  userName: string;
  temporaryPassword: string;
  mustChangePassword: boolean;
}

function toQuery(params: Record<string, string | number | boolean | undefined>): string {
  const search = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== "") search.set(key, String(value));
  });
  const query = search.toString();
  return query ? `?${query}` : "";
}

/** Thông tin cán bộ của chính người đang đăng nhập. */
export interface MyProfile {
  id: string;
  fullName: string;
  userName: string;
  adminTitle: string;
  partyBranchName: string;
  adminDeptName: string;
}

export const userService = {
  getMyProfile(): Promise<MyProfile> {
    return request<MyProfile>("/users/profile");
  },

  /** Chỉ trong phạm vi được xem. */
  search(params: AccountSearchParams = {}): Promise<PagedResult<AccountListItem>> {
    return request<PagedResult<AccountListItem>>(
      `/users${toQuery({
        page: params.page,
        pageSize: params.pageSize,
        q: params.q?.trim() || undefined,
        departmentId: params.departmentId,
        partyCellId: params.partyCellId,
        isActive: params.isActive,
      })}`
    );
  },

  get(id: string): Promise<AccountListItem> {
    return request<AccountListItem>(`/users/${id}`);
  },

  /** Mật khẩu tạm chỉ trả về một lần. */
  create(payload: CreateAccountPayload): Promise<CreatedAccount> {
    return request<CreatedAccount>("/users", { method: "POST", body: JSON.stringify(payload) });
  },

  update(id: string, payload: UpdateAccountPayload): Promise<AccountListItem> {
    return request<AccountListItem>(`/users/${id}`, { method: "PUT", body: JSON.stringify(payload) });
  },

  activate(id: string): Promise<void> {
    return request<void>(`/users/${id}/activate`, { method: "POST" });
  },

  /** 409 nếu tự khóa mình / quản trị viên cuối cùng. */
  deactivate(id: string): Promise<void> {
    return request<void>(`/users/${id}/deactivate`, { method: "POST" });
  },

  /** Không đổi mật khẩu. */
  unlock(id: string): Promise<void> {
    return request<void>(`/users/${id}/unlock`, { method: "POST" });
  },

  /** Mật khẩu tạm chỉ hiển thị một lần. */
  resetPassword(id: string): Promise<ResetPasswordResult> {
    return request<ResetPasswordResult>(`/users/${id}/reset-password`, { method: "POST" });
  },

  /** Xóa mềm; 409 như vô hiệu hóa. */
  remove(id: string): Promise<void> {
    return request<void>(`/users/${id}`, { method: "DELETE" });
  },

  /** Gồm cả chức vụ đã thôi giữ. */
  listPositions(userId: string): Promise<MemberPosition[]> {
    return request<MemberPosition[]>(`/users/${userId}/positions`);
  },

  addPosition(userId: string, payload: SaveMemberPositionPayload): Promise<MemberPosition> {
    return request<MemberPosition>(`/users/${userId}/positions`, { method: "POST", body: JSON.stringify(payload) });
  },

  updatePosition(userId: string, id: string, payload: SaveMemberPositionPayload): Promise<MemberPosition> {
    return request<MemberPosition>(`/users/${userId}/positions/${id}`, { method: "PUT", body: JSON.stringify(payload) });
  },

  endPosition(userId: string, id: string): Promise<MemberPosition> {
    return request<MemberPosition>(`/users/${userId}/positions/${id}/end`, { method: "POST" });
  },

  removePosition(userId: string, id: string): Promise<void> {
    return request<void>(`/users/${userId}/positions/${id}`, { method: "DELETE" });
  },

  getApprovalAuthority(userId: string): Promise<ApprovalAuthorityInfo> {
    return request<ApprovalAuthorityInfo>(`/users/${userId}/approval-authority`);
  },

  /** `override` rỗng = bỏ đặt tay. */
  setApprovalAuthority(userId: string, override: "CoSo" | "CapTren" | "", reason?: string): Promise<ApprovalAuthorityInfo> {
    return request<ApprovalAuthorityInfo>(`/users/${userId}/approval-authority`, {
      method: "PUT",
      body: JSON.stringify({ override: override || null, reason }),
    });
  },
};
