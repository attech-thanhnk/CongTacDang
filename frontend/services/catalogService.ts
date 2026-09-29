import { request } from "./apiClient";

/** Loại danh mục tổ chức: đơn vị chính quyền (`departments`) hoặc tổ chức Đảng (`branches`). */
export type CatalogKind = "departments" | "branches";

/** Một nút cây đơn vị (đơn vị chính quyền hoặc tổ chức Đảng) — danh sách phẳng, máy chủ sắp sẵn theo cây. */
export interface CatalogItem {
  id: string;
  code: string;
  name: string;
  description?: string;
  sortOrder: number;
  isActive: boolean;
  memberCount: number;
  /** Đơn vị cha (null = gốc). */
  parentId?: string | null;
  parentName?: string | null;
  unitTypeId?: string | null;
  unitTypeName?: string | null;
  /** Độ sâu trong cây (gốc = 0). */
  depth: number;
  /** Đường dẫn vật hóa `/id gốc/…/id/`. */
  path: string;
  /** Số đơn vị con trực tiếp. */
  childCount: number;
}

/**
 * Dữ liệu thêm mới / cập nhật; trường bỏ trống (undefined) khi cập nhật được giữ nguyên.
 * `parentId`: `EMPTY_GUID` = chuyển thành gốc; `unitTypeId`: `EMPTY_GUID` = bỏ loại.
 */
export interface SaveCatalogItemPayload {
  code?: string;
  name?: string;
  description?: string;
  sortOrder?: number;
  isActive?: boolean;
  parentId?: string;
  unitTypeId?: string;
}

/** Bên của loại đơn vị. */
export type OrgSide = "Party" | "Administrative";

export const ORG_SIDE_LABELS: Record<OrgSide, string> = {
  Party: "Tổ chức Đảng",
  Administrative: "Đơn vị chính quyền",
};

/** Loại đơn vị (Đảng ủy, Chi bộ, Công ty, Phòng…). */
export interface OrgUnitType {
  id: string;
  name: string;
  side: OrgSide;
  sortOrder: number;
  isActive: boolean;
  unitCount: number;
}

export interface SaveOrgUnitTypePayload {
  name?: string;
  side?: OrgSide;
  sortOrder?: number;
  isActive?: boolean;
}

/** Bên của chức vụ. */
export type PositionSide = "Party" | "Administrative" | "MassOrganization" | "Other";

export const POSITION_SIDE_LABELS: Record<PositionSide, string> = {
  Party: "Đảng",
  Administrative: "Chính quyền",
  MassOrganization: "Đoàn thể",
  Other: "Khác",
};

/** Thẩm quyền phê duyệt dạng chuỗi (API chức vụ). */
export type ApprovalAuthorityCode = "CoSo" | "CapTren";

export const APPROVAL_AUTHORITY_CODE_LABELS: Record<ApprovalAuthorityCode, string> = {
  CoSo: "Đảng ủy cơ sở quyết định",
  CapTren: "Cấp ủy cấp trên quyết định",
};

/** Chức vụ trong danh mục. */
export interface Position {
  id: string;
  name: string;
  side: PositionSide;
  /** Mã chức danh Mẫu 15A/15B (M1…M26). */
  statCode?: string | null;
  defaultApprovalAuthority?: ApprovalAuthorityCode | null;
  isLeadership: boolean;
  sortOrder: number;
  isActive: boolean;
  holderCount: number;
}

/** Thêm/sửa chức vụ; `statCode`/`defaultApprovalAuthority` rỗng = bỏ giá trị. */
export interface SavePositionPayload {
  name?: string;
  side?: PositionSide;
  statCode?: string;
  defaultApprovalAuthority?: string;
  isLeadership?: boolean;
  sortOrder?: number;
  isActive?: boolean;
}

const base = (kind: CatalogKind) => `/organizations/${kind}`;

/** Dịch vụ danh mục tổ chức (API `/api/organizations/*`, `/api/positions`). */
export const catalogService = {
  /** Danh sách (mọi người đã đăng nhập), sắp theo cây. */
  list(kind: CatalogKind): Promise<CatalogItem[]> {
    return request<CatalogItem[]>(base(kind));
  },

  /** Thêm mới (quyền "Quản lý danh mục"). */
  create(kind: CatalogKind, payload: SaveCatalogItemPayload): Promise<CatalogItem> {
    return request<CatalogItem>(base(kind), { method: "POST", body: JSON.stringify(payload) });
  },

  /** Cập nhật, kể cả đổi cha, ngừng hoạt động / hoạt động lại. */
  update(kind: CatalogKind, id: string, payload: SaveCatalogItemPayload): Promise<CatalogItem> {
    return request<CatalogItem>(`${base(kind)}/${id}`, { method: "PUT", body: JSON.stringify(payload) });
  },

  /** Xóa; máy chủ trả 409 kèm lý do khi còn đơn vị con, cán bộ, hồ sơ, bản gán, chức vụ. */
  remove(kind: CatalogKind, id: string): Promise<unknown> {
    return request(`${base(kind)}/${id}`, { method: "DELETE" });
  },

  /** Danh mục loại đơn vị. */
  listUnitTypes(): Promise<OrgUnitType[]> {
    return request<OrgUnitType[]>("/organizations/unit-types");
  },

  createUnitType(payload: SaveOrgUnitTypePayload): Promise<OrgUnitType> {
    return request<OrgUnitType>("/organizations/unit-types", { method: "POST", body: JSON.stringify(payload) });
  },

  updateUnitType(id: string, payload: SaveOrgUnitTypePayload): Promise<OrgUnitType> {
    return request<OrgUnitType>(`/organizations/unit-types/${id}`, { method: "PUT", body: JSON.stringify(payload) });
  },

  removeUnitType(id: string): Promise<unknown> {
    return request(`/organizations/unit-types/${id}`, { method: "DELETE" });
  },

  /** Danh mục chức vụ. */
  listPositions(): Promise<Position[]> {
    return request<Position[]>("/positions");
  },

  createPosition(payload: SavePositionPayload): Promise<Position> {
    return request<Position>("/positions", { method: "POST", body: JSON.stringify(payload) });
  },

  updatePosition(id: string, payload: SavePositionPayload): Promise<Position> {
    return request<Position>(`/positions/${id}`, { method: "PUT", body: JSON.stringify(payload) });
  },

  removePosition(id: string): Promise<unknown> {
    return request(`/positions/${id}`, { method: "DELETE" });
  },
};

/** Nhãn hiển thị một nút cây trong ô chọn: thụt lề theo độ sâu. */
export function treeLabel(item: CatalogItem): string {
  return `${"   ".repeat(item.depth)}${item.depth > 0 ? "└ " : ""}${item.name}`;
}

/** Id của nút và mọi con cháu (dựa trên `path`). */
export function subtreeIds(items: CatalogItem[], rootId: string): Set<string> {
  return new Set(items.filter((item) => item.id === rootId || item.path.includes(`/${rootId}/`)).map((item) => item.id));
}
