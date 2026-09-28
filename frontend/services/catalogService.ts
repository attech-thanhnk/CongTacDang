import { request } from "./apiClient";

/** Loại danh mục tổ chức. */
export type CatalogKind = "departments" | "branches";

/** Một mục danh mục (Phòng/đơn vị hoặc Chi bộ). */
export interface CatalogItem {
  id: string;
  code: string;
  name: string;
  description?: string;
  sortOrder: number;
  isActive: boolean;
  memberCount: number;
}

/** Dữ liệu thêm mới / cập nhật; trường bỏ trống (undefined) khi cập nhật được giữ nguyên. */
export interface SaveCatalogItemPayload {
  code?: string;
  name?: string;
  description?: string;
  sortOrder?: number;
  isActive?: boolean;
}

const base = (kind: CatalogKind) => `/organizations/${kind}`;

/** Dịch vụ danh mục tổ chức (API `/api/organizations/departments|branches`). */
export const catalogService = {
  /** Danh sách (mọi người đã đăng nhập). */
  list(kind: CatalogKind): Promise<CatalogItem[]> {
    return request<CatalogItem[]>(base(kind));
  },

  /** Thêm mới (quyền "Quản lý danh mục"). */
  create(kind: CatalogKind, payload: SaveCatalogItemPayload): Promise<CatalogItem> {
    return request<CatalogItem>(base(kind), { method: "POST", body: JSON.stringify(payload) });
  },

  /** Cập nhật, kể cả ngừng hoạt động / hoạt động lại. */
  update(kind: CatalogKind, id: string, payload: SaveCatalogItemPayload): Promise<CatalogItem> {
    return request<CatalogItem>(`${base(kind)}/${id}`, { method: "PUT", body: JSON.stringify(payload) });
  },

  /** Xóa; máy chủ trả 409 kèm lý do khi còn cán bộ hoặc hồ sơ đánh giá. */
  remove(kind: CatalogKind, id: string): Promise<unknown> {
    return request(`${base(kind)}/${id}`, { method: "DELETE" });
  },
};
