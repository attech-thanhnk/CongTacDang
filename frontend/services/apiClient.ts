import axios, { AxiosInstance, AxiosResponse } from "axios";

export const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000/api";

export interface ApiResponse<T = any> {
  success: boolean;
  message?: string;
  data?: T;
  errors?: string[];
  timestamp?: string;
}

// Cấu hình Axios client (hỗ trợ BFF qua HttpOnly Cookie)
export const apiClient: AxiosInstance = axios.create({
  baseURL: API_BASE_URL,
  timeout: 30000,
  withCredentials: true,
  headers: {
    "Content-Type": "application/json",
  },
});

// Tự động đính kèm thông tin xác thực (cookie) vào request
apiClient.interceptors.request.use(
  (config) => {
    config.withCredentials = true;
    return config;
  },
  (error) => Promise.reject(error)
);

// Chuẩn hóa ApiResponse<T> và bắt lỗi HTTP tập trung
apiClient.interceptors.response.use(
  (response: AxiosResponse) => {
    const payload = response.data;
    if (payload && typeof payload === "object" && "success" in payload && "data" in payload) {
      if (!payload.success) {
        return Promise.reject(new Error(payload.message || "Yêu cầu không thành công."));
      }
      return payload.data;
    }
    return payload;
  },
  (error) => {
    let message = "Không thể kết nối đến máy chủ Web API.";
    if (error.response) {
      const status = error.response.status;
      const resData = error.response.data;
      if (resData && typeof resData === "object" && resData.message) {
        message = resData.message;
      } else if (typeof resData === "string" && resData.length > 0) {
        message = resData;
      } else {
        message = `Lỗi yêu cầu máy chủ (HTTP ${status})`;
      }
    } else if (error.message) {
      message = error.message;
    }
    return Promise.reject(new Error(message));
  }
);

// Wrapper tương thích Fetch API cho các services hiện hữu
export async function request<T>(endpoint: string, options?: RequestInit): Promise<T> {
  const method = (options?.method || "GET").toLowerCase();
  const headers = options?.headers as Record<string, string> | undefined;
  let data: any = undefined;

  if (options?.body) {
    if (typeof options.body === "string") {
      try {
        data = JSON.parse(options.body);
      } catch {
        data = options.body;
      }
    } else {
      data = options.body;
    }
  }

  const res = await apiClient.request({
    url: endpoint,
    method,
    headers,
    data,
  });

  return res as unknown as T;
}
