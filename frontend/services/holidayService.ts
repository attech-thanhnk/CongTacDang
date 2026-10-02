import { request } from "./apiClient";

/** Một ngày nghỉ trong danh mục (lễ, Tết, nghỉ bù…); `date` dạng yyyy-MM-dd. */
export interface Holiday {
  id: string;
  date: string;
  name: string;
}

/** Thêm: có `endDate` thì thêm mọi ngày trong khoảng `date`–`endDate`. Sửa: chỉ dùng `date` và `name`. */
export interface SaveHolidayPayload {
  date: string;
  endDate?: string | null;
  name: string;
}

export const holidayService = {
  listByYear(year: number): Promise<Holiday[]> {
    return request<Holiday[]>(`/holidays?year=${year}`);
  },

  create(payload: SaveHolidayPayload): Promise<Holiday[]> {
    return request<Holiday[]>("/holidays", { method: "POST", body: JSON.stringify(payload) });
  },

  update(id: string, payload: SaveHolidayPayload): Promise<Holiday> {
    return request<Holiday>(`/holidays/${id}`, { method: "PUT", body: JSON.stringify(payload) });
  },

  remove(id: string): Promise<unknown> {
    return request(`/holidays/${id}`, { method: "DELETE" });
  },
};
