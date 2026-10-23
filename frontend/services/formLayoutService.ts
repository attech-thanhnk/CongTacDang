import { request } from "./apiClient";

/** Tiêu đề trái Mẫu 09B. */
export type FormHeaderStyle = "PartyCell" | "PartyCommittee";

export const FORM_HEADER_STYLE_LABELS: Record<FormHeaderStyle, string> = {
  PartyCell: "Đảng bộ / Chi bộ sinh hoạt",
  PartyCommittee: "Đảng bộ cấp trên / Đảng ủy",
};

/** Áp dụng cho Mẫu 09B, 09C, 9D. */
export interface FormLayout {
  id: string;
  name: string;
  description?: string | null;
  headerStyle: FormHeaderStyle;
  showCellConfirmation: boolean;
  singleLinePositions: boolean;
  showDateLineOn9D: boolean;
  /** Chữ in ở dòng chức vụ không có; null = dòng chấm. */
  emptyPositionText?: string | null;
  isDefault: boolean;
  sortOrder: number;
  positionCount: number;
  memberCount: number;
}

export interface SaveFormLayoutPayload {
  name: string;
  description?: string | null;
  headerStyle: FormHeaderStyle;
  showCellConfirmation: boolean;
  singleLinePositions: boolean;
  showDateLineOn9D: boolean;
  emptyPositionText?: string | null;
  isDefault: boolean;
  sortOrder: number;
}

export interface MemberFormLayout {
  effectiveId?: string | null;
  effectiveName?: string | null;
  derivedId?: string | null;
  derivedName?: string | null;
  overrideId?: string | null;
  /** "Tên chức vụ → tên kiểu" của các chức vụ đang giữ có gắn kiểu. */
  positionLayouts: string[];
}

export const formLayoutService = {
  list(): Promise<FormLayout[]> {
    return request<FormLayout[]>("/form-layouts");
  },

  create(payload: SaveFormLayoutPayload): Promise<FormLayout> {
    return request<FormLayout>("/form-layouts", { method: "POST", body: JSON.stringify(payload) });
  },

  update(id: string, payload: SaveFormLayoutPayload): Promise<FormLayout> {
    return request<FormLayout>(`/form-layouts/${id}`, { method: "PUT", body: JSON.stringify(payload) });
  },

  remove(id: string): Promise<unknown> {
    return request(`/form-layouts/${id}`, { method: "DELETE" });
  },

  getForMember(userId: string): Promise<MemberFormLayout> {
    return request<MemberFormLayout>(`/users/${userId}/form-layout`);
  },

  setForMember(userId: string, layoutId: string | null): Promise<MemberFormLayout> {
    return request<MemberFormLayout>(`/users/${userId}/form-layout`, { method: "PUT", body: JSON.stringify({ layoutId }) });
  },
};
