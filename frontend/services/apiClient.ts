const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000/api";

export interface ApiResponse<T = any> {
  success: boolean;
  message?: string;
  data?: T;
  errors?: string[];
  timestamp?: string;
}

export async function request<T>(endpoint: string, options?: RequestInit): Promise<T> {
  const url = `${API_BASE_URL}${endpoint.startsWith("/") ? endpoint : `/${endpoint}`}`;
  const response = await fetch(url, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...(options?.headers || {}),
    },
  });

  if (!response.ok) {
    let errorMsg = `Lỗi yêu cầu máy chủ (${response.status})`;
    try {
      const errJson = await response.json();
      if (errJson && errJson.message) {
        errorMsg = errJson.message;
      }
    } catch {
      const errText = await response.text();
      if (errText) errorMsg = errText;
    }
    throw new Error(errorMsg);
  }

  const payload = await response.json();

  // Tu dong giai nen neu ket qua tra ve dong goi trong ApiResponse<T>
  if (payload && typeof payload === "object" && "success" in payload && "data" in payload) {
    if (!payload.success) {
      throw new Error(payload.message || "Yêu cầu không thành công.");
    }
    return payload.data as T;
  }

  return payload as T;
}

export { API_BASE_URL };
