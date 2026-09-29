import { useEffect, useState } from "react";
import { request } from "./apiClient";

/** Thông tin đơn vị dùng trên biểu mẫu, báo cáo, giao diện (API `/api/settings/organization`). */
export interface OrganizationSettings {
  partyCommitteeName: string;
  superiorPartyName: string;
  companyName: string;
  parentCompanyName: string;
  shortName: string;
  location: string;
  systemName: string;
  updatedAt?: string | null;
  updatedByName?: string | null;
}

export type SaveOrganizationSettingsPayload = Omit<OrganizationSettings, "updatedAt" | "updatedByName">;

/** Phần hiển thị trước khi đăng nhập. */
export interface PublicOrganizationInfo {
  systemName: string;
  companyName: string;
  parentCompanyName: string;
  shortName: string;
}

const CHANGED_EVENT = "organization-settings:changed";
let publicCache: Promise<PublicOrganizationInfo> | null = null;

export const organizationSettingsService = {
  /** Thông tin đơn vị (cần đăng nhập). */
  get(): Promise<OrganizationSettings> {
    return request<OrganizationSettings>("/settings/organization");
  },

  /** Sửa thông tin đơn vị (quyền system.settings.manage); báo các thành phần đang hiển thị tải lại. */
  async update(payload: SaveOrganizationSettingsPayload): Promise<OrganizationSettings> {
    const saved = await request<OrganizationSettings>("/settings/organization", {
      method: "PUT",
      body: JSON.stringify(payload),
    });
    publicCache = null;
    if (typeof window !== "undefined") window.dispatchEvent(new CustomEvent(CHANGED_EVENT));
    return saved;
  },

  /** Tên hệ thống, tên công ty (không cần đăng nhập) — dùng chung một lần gọi cho mọi thành phần. */
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

/**
 * Tên hệ thống / tên công ty để hiển thị (thanh bên, tiêu đề trang, trang đăng nhập). Trả về null khi chưa tải xong
 * hoặc không tải được — nơi dùng hiển thị chữ chung, không ghi cứng tên đơn vị.
 */
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
