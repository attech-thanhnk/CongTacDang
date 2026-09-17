import { apiClient, API_BASE_URL } from "./apiClient";

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
    code: "BC-CB-01",
    title: "Danh sách Cán bộ Lãnh đạo, Quản lý & Đảng viên",
    category: "Hồ sơ Cán bộ",
    description: "Kết xuất toàn bộ hồ sơ cán bộ lãnh đạo, quản lý 2 vai: Chi bộ sinh hoạt, Chức vụ Đảng, Đơn vị chuyên môn, Chức danh chính quyền.",
    format: "Bảng tính Excel (.xlsx)",
    fileType: "XLSX",
    endpoint: "/reports/cadres",
    fileName: "DanhSach_CanBo_ATTECH.xlsx",
    previewRoute: "/users",
    lastUpdated: "16/09/2026",
  },
  {
    id: "rep-02",
    code: "BC-TH-14",
    title: "Bảng Tổng hợp Hồ sơ Cán bộ theo Đơn vị & Chi bộ",
    category: "Tổng hợp Đảng bộ",
    description: "Bảng tổng hợp danh sách cán bộ lãnh đạo, quản lý phục vụ rà soát đánh giá định kỳ theo Hướng dẫn 03-HD/TVĐU.",
    format: "Bảng tính Excel (.xlsx)",
    fileType: "XLSX",
    endpoint: "/reports/form-14",
    fileName: "Mau_14_DanhSachHoSoCanBo_Q3_2026.xlsx",
    previewRoute: "/users",
    lastUpdated: "16/09/2026",
  },
  {
    id: "rep-03",
    code: "BC-TK-15",
    title: "Bảng Thống kê Cơ cấu Tổ chức & Sĩ số các Chi bộ",
    category: "Tổ chức Cơ sở Đảng",
    description: "Báo cáo thống kê số lượng cán bộ, đảng viên đang sinh hoạt tại các Chi bộ trực thuộc Đảng bộ Công ty ATTECH.",
    format: "Bảng tính Excel (.xlsx)",
    fileType: "XLSX",
    endpoint: "/reports/form-15",
    fileName: "Mau_15_ThongKeChiBo_Q3_2026.xlsx",
    previewRoute: "/users?tab=branches",
    lastUpdated: "16/09/2026",
  },
];

export const reportService = {
  // Tải báo cáo kèm HttpOnly Cookie xác thực qua apiClient
  async downloadReport(endpoint: string, fileName: string): Promise<void> {
    if (!endpoint) return;
    try {
      const res: any = await apiClient.get(endpoint, { responseType: "blob" });
      const actualBlob = res instanceof Blob ? res : new Blob([res]);
      const url = window.URL.createObjectURL(actualBlob);
      const link = document.createElement("a");
      link.href = url;
      link.setAttribute("download", fileName);
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);
    } catch (err: any) {
      console.error("Lỗi khi tải báo cáo:", err);
      throw err;
    }
  },

  /** Xuất bảng tổng hợp kết quả đánh giá xếp loại cán bộ (Mẫu 14) */
  async exportForm14(): Promise<void> {
    return this.downloadReport("/reports/form-14", "Mau_14_TongHopXepLoaiCanBo_Q3_2026.xlsx");
  },

  /** Xuất bảng kiểm soát tỷ lệ trần 20% Hoàn thành xuất sắc nhiệm vụ (Mẫu 15) */
  async exportForm15(): Promise<void> {
    return this.downloadReport("/reports/form-15", "Mau_15_KiemSoatTran20_ChiBo_Q3_2026.xlsx");
  },

  printDocument(): void {
    window.print();
  },
};
