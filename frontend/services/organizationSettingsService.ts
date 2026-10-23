import { useEffect, useState } from "react";
import { API_BASE_URL, apiClient, request } from "./apiClient";

export type LoginPanelPosition = "center" | "left" | "right";

/** Thông tin đơn vị dùng trên biểu mẫu, báo cáo, giao diện. */
export interface OrganizationSettings {
  partyCommitteeName: string;
  superiorPartyName: string;
  companyName: string;
  parentCompanyName: string;
  shortName: string;
  location: string;
  appraisalUnitName: string;
  systemName: string;
  loginPanelPosition: LoginPanelPosition;
  loginBackgroundUrl?: string | null;
  loginBackgroundFileName?: string | null;
  updatedAt?: string | null;
  updatedByName?: string | null;
}

export type SaveOrganizationSettingsPayload = Omit<
  OrganizationSettings,
  "updatedAt" | "updatedByName" | "loginBackgroundUrl" | "loginBackgroundFileName"
>;

/** Phần hiển thị trước khi đăng nhập. */
export interface PublicOrganizationInfo {
  systemName: string;
  companyName: string;
  parentCompanyName: string;
  shortName: string;
  loginBackgroundUrl?: string | null;
  loginPanelPosition?: LoginPanelPosition;
}

/** Khớp giới hạn máy chủ. */
export const LOGIN_BACKGROUND_MAX_BYTES = 5 * 1024 * 1024;
export const LOGIN_BACKGROUND_ACCEPT = "image/jpeg,image/png,image/webp";

const CHANGED_EVENT = "organization-settings:changed";
let publicCache: Promise<PublicOrganizationInfo> | null = null;

function notifyChanged() {
  publicCache = null;
  if (typeof window !== "undefined") window.dispatchEvent(new CustomEvent(CHANGED_EVENT));
}

/** URL máy chủ trả về dạng "/api/..."; đổi sang gốc API đang cấu hình. */
export function resolveApiUrl(url: string): string {
  return url.startsWith("/api/") ? `${API_BASE_URL.replace(/\/$/, "")}${url.slice(4)}` : url;
}

export const organizationSettingsService = {
  get(): Promise<OrganizationSettings> {
    return request<OrganizationSettings>("/settings/organization");
  },

  /** Báo các thành phần đang hiển thị tải lại. */
  async update(payload: SaveOrganizationSettingsPayload): Promise<OrganizationSettings> {
    const saved = await request<OrganizationSettings>("/settings/organization", {
      method: "PUT",
      body: JSON.stringify(payload),
    });
    notifyChanged();
    return saved;
  },

  async uploadLoginBackground(file: File): Promise<OrganizationSettings> {
    const form = new FormData();
    form.append("file", file);
    const saved = (await apiClient.post("/settings/organization/login-background", form, {
      headers: { "Content-Type": "multipart/form-data" },
      timeout: 120000,
    })) as unknown as OrganizationSettings;
    notifyChanged();
    return saved;
  },

  async removeLoginBackground(): Promise<OrganizationSettings> {
    const saved = await request<OrganizationSettings>("/settings/organization/login-background", { method: "DELETE" });
    notifyChanged();
    return saved;
  },

  /** Không cần đăng nhập; mọi thành phần dùng chung một lần gọi. */
  getPublic(): Promise<PublicOrganizationInfo> {
    if (!publicCache) {
      publicCache = request<PublicOrganizationInfo>("/settings/organization/public").catch((err) => {
        publicCache = null;
        throw err;
      });
    }
    return publicCache;
  },
};

/** null khi chưa tải xong hoặc lỗi — nơi dùng hiển thị chữ chung, không ghi cứng tên đơn vị. */
export function useOrganizationInfo(): PublicOrganizationInfo | null {
  const [info, setInfo] = useState<PublicOrganizationInfo | null>(null);

  useEffect(() => {
    let active = true;
    const load = () =>
      organizationSettingsService
        .getPublic()
        .then((value) => {
          if (active) setInfo(value);
        })
        .catch(() => {
          // Không chặn giao diện khi chưa tải được thông tin đơn vị.
        });
    load();
    window.addEventListener(CHANGED_EVENT, load);
    return () => {
      active = false;
      window.removeEventListener(CHANGED_EVENT, load);
    };
  }, []);

  return info;
}
