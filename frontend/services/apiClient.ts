import axios, { AxiosInstance, AxiosResponse, AxiosError } from "axios";

/**
 * Cấu hình Endpoint Web API.
 * Mặc định sử dụng relative path `/api` được chuyển tiếp (reverse-proxy) an toàn qua Next.js rewrites,
 * giúp loại bỏ hoàn toàn các vấn đề CORS, bảo toàn Cookie HttpOnly và độc lập môi trường triển khai.
 */
export const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || "/api";

export interface ApiResponse<T = any> {
  success: boolean;
  message?: string;
  data?: T;
  errors?: string[];
  timestamp?: string;
  /** Mã lỗi máy đọc được (ví dụ PASSWORD_CHANGE_REQUIRED) */
  code?: string;
}

/** Mã lỗi backend trả khi tài khoản còn dùng mật khẩu tạm (403). */
export const PASSWORD_CHANGE_REQUIRED = "PASSWORD_CHANGE_REQUIRED";

/** Chuyển tới trang đổi mật khẩu (không refresh, không đăng xuất). */
function redirectToChangePassword() {
  if (typeof window === "undefined") return;
  if (window.location.pathname !== "/change-password") {
    window.location.assign("/change-password");
  }
}

/**
 * Lớp ngoại lệ tiêu chuẩn cho các yêu cầu Web API trong hệ thống.
 * Bảo toàn mã trạng thái HTTP, danh sách lỗi chi tiết (RFC 7807 / Validation) và dữ liệu phản hồi từ máy chủ.
 */
export class ApiError extends Error {
  public status?: number;
  public errors?: string[];
  public data?: any;

  constructor(message: string, status?: number, errors?: string[], data?: any) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.errors = errors;
    this.data = data;
  }
}

/**
 * Khởi tạo Axios client chuyên dụng cho hệ thống Công tác Đảng
 */
export const apiClient: AxiosInstance = axios.create({
  baseURL: API_BASE_URL,
  timeout: 30000,
  withCredentials: true,
  headers: {
    "Content-Type": "application/json",
  },
});

// Đảm bảo thông tin xác thực (HttpOnly Cookie) luôn được đính kèm vào mọi yêu cầu
apiClient.interceptors.request.use(
  (config) => {
    config.withCredentials = true;
    return config;
  },
  (error) => Promise.reject(error)
);

// Quản lý hàng đợi các yêu cầu đồng thời khi đang làm mới Token
let isRefreshing = false;
let failedQueue: Array<{
  resolve: (value?: unknown) => void;
  reject: (reason?: unknown) => void;
}> = [];

const processQueue = (error: any = null) => {
  failedQueue.forEach((promise) => {
    if (error) {
      promise.reject(error);
    } else {
      promise.resolve();
    }
  });
  failedQueue = [];
};

// Xử lý và chuẩn hóa dữ liệu phản hồi, bắt lỗi HTTP tập trung
apiClient.interceptors.response.use(
  (response: AxiosResponse) => {
    const payload = response.data;
    // Kiểm tra cấu trúc ApiResponse bọc chuẩn từ ASP.NET Core
    if (payload && typeof payload === "object" && "success" in payload) {
      if (!payload.success) {
        return Promise.reject(
          new ApiError(payload.message || "Yêu cầu không thành công.", response.status, payload.errors, payload)
        );
      }
      return "data" in payload ? payload.data : payload;
    }
    return payload;
  },
  async (error: AxiosError<any>) => {
    let message = "Không thể kết nối đến máy chủ Web API.";
    let status: number | undefined = undefined;
    let errors: string[] | undefined = undefined;
    const resData = error.response?.data;
    const originalRequest = error.config as any;

    if (error.response) {
      status = error.response.status;

      // Trích xuất thông báo lỗi chính xác từ backend (RFC 7807 ProblemDetails hoặc ApiResponse)
      let parsedData = resData;
      if (typeof Blob !== "undefined" && resData instanceof Blob) {
        try {
          const text = await resData.text();
          parsedData = JSON.parse(text);
        } catch {
          // Bỏ qua nếu không phải chuỗi JSON
        }
      }

      if (parsedData && typeof parsedData === "object") {
        if (parsedData.message) {
          message = parsedData.message;
        } else if (parsedData.detail) {
          message = parsedData.detail;
        } else if (parsedData.title) {
          message = parsedData.title;
        }

        if (Array.isArray(parsedData.errors)) {
          errors = parsedData.errors;
        } else if (parsedData.errors && typeof parsedData.errors === "object") {
          errors = Object.values(parsedData.errors).flat() as string[];
        }

        if (errors && errors.length > 0) {
          const validErrors = errors.filter((e) => typeof e === "string" && e.trim().length > 0);
          if (validErrors.length > 0) {
            message = validErrors.join("; ");
          }
        }
      } else if (typeof parsedData === "string" && parsedData.trim().length > 0) {
        message = parsedData;
      }

      // Xử lý mã trạng thái 401 Unauthorized — Tự động Refresh Token ngầm
      if (status === 401) {
        const isAuthEndpoint =
          originalRequest?.url?.includes("/auth/login") ||
          originalRequest?.url?.includes("/auth/refresh-token") ||
          originalRequest?.url?.includes("/auth/logout");

        // Nếu là endpoint xác thực hoặc request đã thử retry rồi mà vẫn 401 -> Hết phiên thật sự
        if (isAuthEndpoint || originalRequest?._retry) {
          if (!isAuthEndpoint && typeof window !== "undefined") {
            window.dispatchEvent(new CustomEvent("auth:session-expired"));
          }
          return Promise.reject(
            new ApiError(resData?.message || "Phiên làm việc đã hết hạn hoặc chưa được xác thực.", status, errors, resData)
          );
        }

        // Nếu đang có 1 request khác tiến hành làm mới token -> Xếp hàng đợi kết quả
        if (isRefreshing) {
          return new Promise((resolve, reject) => {
            failedQueue.push({ resolve, reject });
          })
            .then(() => apiClient(originalRequest))
            .catch((err) => Promise.reject(err));
        }

        originalRequest._retry = true;
        isRefreshing = true;

        return new Promise((resolve, reject) => {
          axios
            .post(`${API_BASE_URL}/auth/refresh-token`, {}, { withCredentials: true })
            .then(() => {
              processQueue(null);
              resolve(apiClient(originalRequest));
            })
            .catch((refreshErr) => {
              processQueue(refreshErr);
              if (typeof window !== "undefined") {
                window.dispatchEvent(new CustomEvent("auth:session-expired"));
              }
              reject(
                new ApiError("Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại.", 401, undefined, refreshErr?.response?.data)
              );
            })
            .finally(() => {
              isRefreshing = false;
            });
        });
      } else if (status === 403) {
        if (parsedData && typeof parsedData === "object" && parsedData.code === PASSWORD_CHANGE_REQUIRED) {
          // Máy chủ chặn vì còn mật khẩu tạm: chuyển tới trang đổi mật khẩu, không làm mới phiên, không đăng xuất.
          redirectToChangePassword();
          return Promise.reject(
            new ApiError(message || "Bạn cần đổi mật khẩu tạm trước khi tiếp tục.", status, errors, parsedData)
          );
        }
        message = message || "Cán bộ không có thẩm quyền thực hiện thao tác này.";
      } else if (status === 404) {
        message = message || "Không tìm thấy dữ liệu yêu cầu trên hệ thống.";
      } else if (status >= 500) {
        message = message || "Lỗi xử lý nội bộ tại máy chủ. Vui lòng liên hệ Quản trị viên.";
      }
    } else if (error.code === "ECONNABORTED") {
      message = "Yêu cầu máy chủ quá thời gian chờ (Timeout). Vui lòng thử lại.";
    } else if (error.message) {
      message = error.message;
    }

    return Promise.reject(new ApiError(message, status, errors, resData));
  }
);

/**
 * Hàm gọi API tổng quát tương thích linh hoạt cho các service tầng ứng dụng
 */
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
