import { API_BASE_URL } from "./apiClient";

export interface ReportItem {
  id: string;
  code: string;
  title: string;
  category: string;
  description: string;
  format: string;
  fileType: string;
  endpoint: string;
  fileName: string;
  previewRoute?: string;
  lastUpdated: string;
}

export const REPORT_LIST: ReportItem[] = [
  {
    id: "rep-01",
    code: "BC-NS-01",
    title: "Danh sách Cán bộ Lãnh đạo, Quản lý & Đảng viên ATTECH",
    category: "Nhân sự",
    description: "Kết xuất toàn bộ hồ sơ cán bộ lãnh đạo, quản lý 2 vai: Đảng viên, Chi bộ, Chức vụ Đảng, Đơn vị chuyên môn, Chức danh quản lý.",
    format: "Bảng tính Excel (.xlsx)",
    fileType: "XLSX",
    endpoint: "/reports/cadres",
    fileName: "Danh_Sach_Can_Bo_ATTECH.xlsx",
    previewRoute: "/users",
    lastUpdated: "16/09/2026",
  },
  {
    id: "rep-02",
    code: "Mẫu số 14",
    title: "Danh sách Đánh giá và Đề xuất Xếp loại Quý",
    category: "Biểu mẫu",
    description: "Bảng tổng hợp điểm chấm chung (30đ), điểm chuyên môn (70đ), tập thể đề xuất, thẩm định đề xuất và xếp loại chính thức.",
    format: "Bảng tính Excel (.xlsx)",
    fileType: "XLSX",
    endpoint: "/reports/form-14",
    fileName: "Mau_14_De_Xuat_Xep_Loai_Q3_2026.xlsx",
    previewRoute: "/forms",
    lastUpdated: "15/09/2026",
  },
  {
    id: "rep-03",
    code: "Mẫu số 15",
    title: "Bảng Tổng hợp Tỷ lệ Cán bộ Hoàn thành Xuất sắc (Trần 20%)",
    category: "Tổng hợp",
    description: "Báo cáo kiểm soát tỷ lệ không vượt quá 20% số cán bộ hoàn thành tốt nhiệm vụ của 4 Chi bộ trực thuộc ATTECH.",
    format: "Bảng tính Excel (.xlsx)",
    fileType: "XLSX",
    endpoint: "/reports/form-15",
    fileName: "Mau_15_Tong_Hop_Tran_20_Q3_2026.xlsx",
    previewRoute: "/forms",
    lastUpdated: "14/09/2026",
  },
  {
    id: "rep-04",
    code: "Mẫu số 01",
    title: "Phiếu Đăng ký Sản phẩm, Công việc Chuyên môn (Mẫu trắng)",
    category: "Biểu mẫu",
    description: "Biểu mẫu chuẩn A4 in ấn phát hành đầu quý để cán bộ đăng ký các đầu việc trọng tâm bảo đảm tổng 70 điểm.",
    format: "Văn bản in ấn (Khổ A4)",
    fileType: "A4 / PDF",
    endpoint: "",
    fileName: "Mau_01_Phieu_Dang_Ky_Giao_Viec.pdf",
    previewRoute: "/forms",
    lastUpdated: "10/09/2026",
  },
];

export const reportService = {
  downloadReport(endpoint: string, fileName: string): void {
    if (!endpoint) return;
    const fullUrl = `${API_BASE_URL}${endpoint.startsWith("/") ? endpoint : `/${endpoint}`}`;
    const link = document.createElement("a");
    link.href = fullUrl;
    link.setAttribute("download", fileName);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  },

  printDocument(): void {
    window.print();
  },
};
