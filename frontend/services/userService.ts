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

/** Một tài khoản trong danh sách quản trị (`GET /api/users`, `GET /api/users/{id}`). */
export interface AccountListItem {
  id: string;
  username: string;
  fullName: string;
  email: string;
  phoneNumber: string;
  partyCardNumber?: string | null;
  isPartyMember: boolean;
  positionTitle: string;
  /** Mã khung tỷ trọng A-B-C-D mặc định (theo bộ tiêu chí); null = chưa chọn (dùng khung mặc định của bộ). */
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
  /** Người xem quản lý được tài khoản này (phạm vi bao trùm cây đơn vị — máy chủ tính; chỉ có ở chi tiết). */
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

/** Bộ lọc danh sách tài khoản. */
export interface AccountSearchParams {
  page?: number;
  pageSize?: number;
  q?: string;
  departmentId?: string;
  partyCellId?: string;
  isActive?: boolean;
}

/** Dữ liệu tạo tài khoản (`POST /api/users`). */
export interface CreateAccountPayload {
  username: string;
  fullName: string;
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
 * Dữ liệu cập nhật tài khoản (`PUT /api/users/{id}`). Không gửi = giữ nguyên;
 * chuỗi rỗng = xóa (email/điện thoại/số thẻ); `EMPTY_GUID` = bỏ gán đơn vị / tổ chức Đảng.
 * Thẩm quyền phê duyệt sửa qua `setApprovalAuthority`.
 */
export interface UpdateAccountPayload {
  fullName?: string;
  email?: string;
  phoneNumber?: string;
  partyCardNumber?: string;
  positionTitle?: string;
  departmentId?: string;
  partyCellId?: string;
  /** Mã khung tỷ trọng mặc định; chuỗi rỗng = bỏ chọn. */
  weightFrameCode?: string;
}

/** Một chức vụ của cán bộ (`GET /api/users/{id}/positions`). */
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
  validFrom: string;
  /** Mốc hết hiệu lực (không bao gồm). */
  validTo?: string | null;
  isEffective: boolean;
  note?: string | null;
}

/** Thêm/sửa chức vụ của cán bộ. */
export interface SaveMemberPositionPayload {
  positionId?: string;
  partyCellId?: string;
  departmentId?: string;
  isPrimary?: boolean;
  validFrom?: string;
  validTo?: string;
  clearValidTo?: boolean;
  note?: string;
}

/** Thẩm quyền phê duyệt của cán bộ. */
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

/** Dịch vụ quản trị tài khoản (API task 08). */
export const userService = {
  /** Danh sách tài khoản có phân trang, tìm kiếm, lọc (trong phạm vi được xem). */
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

  /** Chi tiết một tài khoản. */
  get(id: string): Promise<AccountListItem> {
    return request<AccountListItem>(`/users/${id}`);
  },

  /** Tạo tài khoản; trả tên đăng nhập + mật khẩu tạm (chỉ hiển thị một lần). */
  create(payload: CreateAccountPayload): Promise<CreatedAccount> {
    return request<CreatedAccount>("/users", { method: "POST", body: JSON.stringify(payload) });
  },

  /** Cập nhật thông tin tài khoản. */
  update(id: string, payload: UpdateAccountPayload): Promise<AccountListItem> {
    return request<AccountListItem>(`/users/${id}`, { method: "PUT", body: JSON.stringify(payload) });
  },

  /** Mở lại tài khoản đã vô hiệu hóa. */
  activate(id: string): Promise<void> {
    return request<void>(`/users/${id}/activate`, { method: "POST" });
  },

  /** Vô hiệu hóa tài khoản (409 nếu tự khóa mình / quản trị viên cuối cùng). */
  deactivate(id: string): Promise<void> {
    return request<void>(`/users/${id}/deactivate`, { method: "POST" });
  },

  /** Mở khóa đăng nhập tạm thời (không đổi mật khẩu). */
  unlock(id: string): Promise<void> {
    return request<void>(`/users/${id}/unlock`, { method: "POST" });
  },

  /** Đặt lại mật khẩu tạm (chỉ hiển thị một lần). */
  resetPassword(id: string): Promise<ResetPasswordResult> {
    return request<ResetPasswordResult>(`/users/${id}/reset-password`, { method: "POST" });
  },

  /** Xóa (mềm) tài khoản (409 như vô hiệu hóa). */
  remove(id: string): Promise<void> {
    return request<void>(`/users/${id}`, { method: "DELETE" });
  },

  /** Chức vụ (đang và đã giữ) của cán bộ. */
  listPositions(userId: string): Promise<MemberPosition[]> {
    return request<MemberPosition[]>(`/users/${userId}/positions`);
  },

  /** Thêm chức vụ (chính hoặc kiêm nhiệm). */
  addPosition(userId: string, payload: SaveMemberPositionPayload): Promise<MemberPosition> {
    return request<MemberPosition>(`/users/${userId}/positions`, { method: "POST", body: JSON.stringify(payload) });
  },

  /** Sửa chức vụ. */
  updatePosition(userId: string, id: string, payload: SaveMemberPositionPayload): Promise<MemberPosition> {
    return request<MemberPosition>(`/users/${userId}/positions/${id}`, { method: "PUT", body: JSON.stringify(payload) });
  },

  /** Kết thúc chức vụ ngay bây giờ. */
  endPosition(userId: string, id: string): Promise<MemberPosition> {
    return request<MemberPosition>(`/users/${userId}/positions/${id}/end`, { method: "POST" });
  },

  /** Xóa bản ghi chức vụ nhập nhầm. */
  removePosition(userId: string, id: string): Promise<void> {
    return request<void>(`/users/${userId}/positions/${id}`, { method: "DELETE" });
  },

  /** Thẩm quyền phê duyệt: đang áp dụng, suy ra, đặt tay. */
  getApprovalAuthority(userId: string): Promise<ApprovalAuthorityInfo> {
    return request<ApprovalAuthorityInfo>(`/users/${userId}/approval-authority`);
  },

  /** Đặt tay (kèm lý do) hoặc bỏ đặt tay (`override` rỗng). */
  setApprovalAuthority(userId: string, override: "CoSo" | "CapTren" | "", reason?: string): Promise<ApprovalAuthorityInfo> {
    return request<ApprovalAuthorityInfo>(`/users/${userId}/approval-authority`, {
      method: "PUT",
      body: JSON.stringify({ override: override || null, reason }),
    });
  },
};
