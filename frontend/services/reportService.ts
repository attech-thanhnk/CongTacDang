import { apiClient, API_BASE_URL } from "./apiClient";
import { organizationSettingsService } from "./organizationSettingsService";

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
    // {SHORT_NAME}: tên viết tắt của đơn vị (Quản trị → Thông tin đơn vị).
    fileName: "DanhSach_CanBo_{SHORT_NAME}.xlsx",
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
    description: "Báo cáo thống kê số lượng cán bộ, đảng viên đang sinh hoạt tại các Chi bộ trực thuộc Đảng bộ.",
    format: "Bảng tính Excel (.xlsx)",
    fileType: "XLSX",
    endpoint: "/reports/form-15",
    fileName: "Mau_15_ThongKeChiBo_Q3_2026.xlsx",
    previewRoute: "/users?tab=branches",
    lastUpdated: "16/09/2026",
  },
  {
    id: "rep-04",
    code: "BC-KS-15A",
    title: "Mẫu 15A - Kiểm soát trần 20% toàn Đảng bộ",
    category: "Tổng hợp Đảng bộ",
    description: "Tổng hợp số lượng và tỷ lệ đề xuất Hoàn thành xuất sắc nhiệm vụ ở cấp Đảng ủy Công ty.",
    format: "Bảng tính Excel (.xlsx)",
    fileType: "XLSX",
    endpoint: "/reports/form-15a",
    fileName: "Mau_15A_KiemSoatTran20_ToanDangBo.xlsx",
    previewRoute: "/evaluations",
    lastUpdated: "18/09/2026",
  },
  {
    id: "rep-05",
    code: "BC-KS-15B",
    title: "Mẫu 15B - Kiểm soát trần 20% theo Chi bộ",
    category: "Tổ chức Cơ sở Đảng",
    description: "Kiểm soát tỷ lệ Hoàn thành xuất sắc nhiệm vụ chi tiết theo từng Chi bộ.",
    format: "Bảng tính Excel (.xlsx)",
    fileType: "XLSX",
    endpoint: "/reports/form-15b",
    fileName: "Mau_15B_KiemSoatTran20_TheoChiBo.xlsx",
    previewRoute: "/evaluations",
    lastUpdated: "18/09/2026",
  },
  {
    id: "rep-06",
    code: "BC-TH-16",
    title: "Mẫu 16 - Tổng hợp kết quả xếp loại theo nhóm chức vụ",
    category: "Tổng hợp Đảng bộ",
    description: "Tổng hợp số lượng cán bộ theo mức xếp loại và nhóm chức vụ trong kỳ đánh giá.",
    format: "Bảng tính Excel (.xlsx)",
    fileType: "XLSX",
    endpoint: "/reports/form-16",
    fileName: "Mau_16_TongHopKetQuaXepLoai.xlsx",
    previewRoute: "/evaluations",
    lastUpdated: "18/09/2026",
  },
];

/** Định dạng tệp xuất: gốc (Word/Excel) hoặc PDF do máy chủ chuyển. */
export type ReportFileFormat = "original" | "pdf";

const withQuery = (endpoint: string, params: Record<string, string | undefined>): string => {
  const query = Object.entries(params)
    .filter(([, value]) => !!value)
    .map(([key, value]) => `${key}=${encodeURIComponent(value as string)}`)
    .join("&");
  if (!query) return endpoint;
  return endpoint.includes("?") ? `${endpoint}&${query}` : `${endpoint}?${query}`;
};

/** Thay `{SHORT_NAME}` trong tên tệp bằng tên viết tắt của đơn vị (không tải được → bỏ phần này). */
async function resolveFileName(fileName: string): Promise<string> {
  if (!fileName.includes(SHORT_NAME_TOKEN)) return fileName;
  const shortName = await organizationSettingsService
    .getPublic()
    .then((info) => info.shortName)
    .catch(() => "");
  return shortName ? fileName.replace(SHORT_NAME_TOKEN, shortName) : fileName.replace(`_${SHORT_NAME_TOKEN}`, "");
}

const SHORT_NAME_TOKEN = "{SHORT_NAME}";

export const reportService = {
  /** Lấy nội dung tệp xuất từ máy chủ (kèm HttpOnly Cookie). `format = "pdf"` để máy chủ chuyển sang PDF. */
  async fetchReportBlob(endpoint: string, format: ReportFileFormat = "original"): Promise<Blob> {
    const url = format === "pdf" ? withQuery(endpoint, { format: "pdf" }) : endpoint;
    // Chuyển PDF bằng LibreOffice có thể mất vài chục giây.
    const res: any = await apiClient.get(url, { responseType: "blob", timeout: format === "pdf" ? 120000 : undefined });
    return res instanceof Blob ? res : new Blob([res]);
  },

  /** Lưu Blob thành tệp tải về trên trình duyệt. */
  saveBlob(blob: Blob, fileName: string): void {
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.setAttribute("download", fileName);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
  },

  // Tải báo cáo kèm HttpOnly Cookie xác thực qua apiClient
  async downloadReport(endpoint: string, fileName: string, format: ReportFileFormat = "original"): Promise<void> {
    if (!endpoint) return;
    try {
      const blob = await this.fetchReportBlob(endpoint, format);
      this.saveBlob(blob, await resolveFileName(fileName));
    } catch (err: any) {
      console.error("Lỗi khi tải báo cáo:", err);
      throw err;
    }
  },

  /**
   * Xuất bảng tổng hợp kết quả đánh giá xếp loại cán bộ (Mẫu 14).
   * Không truyền `periodId`: kỳ đang hoạt động. Không truyền `branchId`: cấp cao nhận toàn Đảng bộ, Bí thư Chi bộ nhận Chi bộ mình.
   */
  async exportForm14(periodId?: string, branchId?: string): Promise<void> {
    return this.downloadReport(withQuery("/reports/form-14", { periodId, branchId }), "Mau_14_TongHopXepLoaiCanBo.xlsx");
  },

  /** Xuất bảng kiểm soát tỷ lệ trần 20% Hoàn thành xuất sắc nhiệm vụ (Mẫu 15) */
  async exportForm15(periodId?: string, branchId?: string): Promise<void> {
    return this.downloadReport(withQuery("/reports/form-15", { periodId, branchId }), "Mau_15_KiemSoatTran20_ChiBo.xlsx");
  },

  async exportForm15A(periodId?: string, branchId?: string): Promise<void> {
    return this.downloadReport(withQuery("/reports/form-15a", { periodId, branchId }), "Mau_15A_KiemSoatTran20.xlsx");
  },

  async exportForm15B(periodId?: string, branchId?: string): Promise<void> {
    return this.downloadReport(withQuery("/reports/form-15b", { periodId, branchId }), "Mau_15B_KiemSoatTran20_TheoChiBo.xlsx");
  },

  async exportForm16(periodId?: string, branchId?: string): Promise<void> {
    return this.downloadReport(withQuery("/reports/form-16", { periodId, branchId }), "Mau_16_TongHopKetQuaXepLoai.xlsx");
  },

  /** Xuất Mẫu 01: Phiếu giao / đăng ký sản phẩm chuyên môn hàng quý (.docx) */
  async exportMau01Docx(recordId: string, fullName?: string): Promise<void> {
    const safeName = fullName ? fullName.replace(/\s+/g, "_") : "CanBo";
    return this.downloadReport(`/reports/docx/mau-01/${recordId}`, `Mau_01_DangKyNhiemVu_${safeName}.docx`);
  },

  /** Xuất Mẫu 02: Phiếu tự đánh giá kết quả thực hiện sản phẩm hàng quý (.docx) */
  async exportMau02Docx(recordId: string, fullName?: string): Promise<void> {
    const safeName = fullName ? fullName.replace(/\s+/g, "_") : "CanBo";
    return this.downloadReport(`/reports/docx/mau-02/${recordId}`, `Mau_02_TuDanhGia_${safeName}.docx`);
  },

  /** Xuất Mẫu 10: Phiếu thẩm định, nhận xét, đề xuất xếp loại và ghi nhận giải trình (.docx) */
  async exportMau10Docx(recordId: string, fullName?: string): Promise<void> {
    const safeName = fullName ? fullName.replace(/\s+/g, "_") : "CanBo";
    return this.downloadReport(`/reports/docx/mau-10/${recordId}`, `Mau_10_PhieuThamDinh_${safeName}.docx`);
  },

  /** Xuất Mẫu 11: Phiếu đánh giá, xếp loại cán bộ quý bỏ phiếu kín Chi bộ (.docx) */
  async exportMau11Docx(periodId: string, branchId?: string, branchName?: string): Promise<void> {
    const query = branchId ? `?periodId=${periodId}&branchId=${branchId}` : `?periodId=${periodId}`;
    const safeBranch = branchName ? branchName.replace(/\s+/g, "_") : "ToanDangBo";
    return this.downloadReport(`/reports/docx/mau-11${query}`, `Mau_11_PhieuBoPhieu_${safeBranch}.docx`);
  },

  /** Xuất Mẫu 13: Biên bản kiểm phiếu đánh giá, xếp loại cán bộ quý (.docx) */
  async exportMau13Docx(periodId: string, branchId?: string, branchName?: string): Promise<void> {
    const query = branchId ? `?periodId=${periodId}&branchId=${branchId}` : `?periodId=${periodId}`;
    const safeBranch = branchName ? branchName.replace(/\s+/g, "_") : "ToanDangBo";
    return this.downloadReport(`/reports/docx/mau-13${query}`, `Mau_13_BienBanKiemPhieu_${safeBranch}.docx`);
  },

  printDocument(): void {
    window.print();
  },
};
