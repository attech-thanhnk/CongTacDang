import { request } from "./apiClient";
import { DownloadedFile, fetchFile, saveBlob } from "./download";
import { organizationSettingsService } from "./organizationSettingsService";

/** Nhóm trên trang Báo cáo: hồ sơ nộp theo HD03 (mục V.1) hoặc báo cáo nội bộ (không mang số mẫu HD03). */
export type ReportGroup = "hd03" | "internal";

/**
 * Cách xuất của một báo cáo:
 * - `period`: theo kỳ + phạm vi tổ chức Đảng (chọn trên trang Báo cáo);
 * - `none`: không cần chọn kỳ (danh sách cán bộ);
 * - `collective` / `meeting`: xuất từ từng hồ sơ tập thể / biên bản trên trang "Đánh giá tập thể & Hội nghị".
 */
export type ReportScope = "period" | "none" | "collective" | "meeting";

export interface ReportDefinition {
  id: string;
  /** Số mẫu HD03 ("Mẫu 14") hoặc nhãn báo cáo nội bộ. */
  code: string;
  /** Tên đúng nguyên văn biểu mẫu HD03. */
  title: string;
  group: ReportGroup;
  scope: ReportScope;
  /** Định dạng gốc theo HD03 V.1 (Excel, trừ 07, 09C, 12, 13, 16 là Word). */
  fileType: "XLSX" | "DOCX";
  endpoint?: string;
  /** Mã mẫu trong tên tệp theo quy cách HD03 ("Mau 16_…_Quy III-2026"). */
  fileCode: string;
  /** Phần đầu tên tệp thay cho "Mau <fileCode>" (văn bản không mang số mẫu, ví dụ Quyết định xếp loại). */
  filePrefix?: string;
}

export const REPORTS: ReportDefinition[] = [
  {
    id: "mau-07", code: "Mẫu 07", group: "hd03", scope: "collective", fileType: "DOCX", fileCode: "07",
    title: "Báo cáo tự đánh giá, xếp loại chất lượng của tập thể Đảng ủy (Chi ủy, Chi bộ)",
  },
  {
    id: "mau-08", code: "Mẫu 08", group: "hd03", scope: "collective", fileType: "XLSX", fileCode: "08",
    title: "Báo cáo tổng hợp kết quả thực hiện các nhiệm vụ của cơ quan, đơn vị",
  },
  {
    id: "mau-11", code: "Mẫu 11", group: "hd03", scope: "period", fileType: "DOCX", fileCode: "11",
    title: "Phiếu đánh giá, xếp loại cán bộ", endpoint: "/reports/docx/mau-11",
  },
  {
    id: "mau-12", code: "Mẫu 12", group: "hd03", scope: "meeting", fileType: "DOCX", fileCode: "12",
    title: "Biên bản Hội nghị tập thể lãnh đạo, quản lý hoặc Hội nghị Đảng ủy/Chi ủy cơ sở về việc đánh giá, xếp loại chất lượng cán bộ quý",
  },
  {
    id: "mau-13", code: "Mẫu 13", group: "hd03", scope: "meeting", fileType: "DOCX", fileCode: "13",
    title: "Biên bản kiểm phiếu Hội nghị tập thể lãnh đạo, quản lý hoặc Hội nghị Đảng ủy/Chi ủy cơ sở về việc đánh giá, xếp loại chất lượng cán bộ quý",
  },
  {
    id: "mau-14", code: "Mẫu 14", group: "hd03", scope: "period", fileType: "XLSX", fileCode: "14",
    title: "Danh sách đánh giá và đề xuất xếp loại quý … năm … đối với cán bộ thuộc diện … quyết định, phê duyệt mức xếp loại",
    endpoint: "/reports/form-14",
  },
  {
    id: "mau-15a", code: "Mẫu 15A", group: "hd03", scope: "period", fileType: "XLSX", fileCode: "15A",
    title: "Tổng hợp kết quả đánh giá, xếp loại cán bộ quý … năm … (Đối tượng đề nghị BTVĐUTCT quyết định, phê duyệt mức xếp loại)",
    endpoint: "/reports/form-15a",
  },
  {
    id: "mau-15b", code: "Mẫu 15B", group: "hd03", scope: "period", fileType: "XLSX", fileCode: "15B",
    title: "Tổng hợp kết quả đánh giá, xếp loại cán bộ quý … năm … (Đối tượng thuộc diện Đảng ủy/Chi ủy cơ sở quyết định, phê duyệt mức xếp loại)",
    endpoint: "/reports/form-15b",
  },
  {
    id: "mau-16", code: "Mẫu 16", group: "hd03", scope: "period", fileType: "DOCX", fileCode: "16",
    title: "Báo cáo về kết quả đánh giá, xếp loại chất lượng cán bộ quý … năm …",
    endpoint: "/reports/docx/mau-16",
  },
  {
    id: "decision", code: "Quyết định", group: "hd03", scope: "period", fileType: "DOCX", fileCode: "QD",
    filePrefix: "Quyet dinh xep loai",
    title: "Quyết định xếp loại cán bộ quý … năm … thuộc thẩm quyền quyết định của Đảng ủy (Chi ủy), kèm Phụ lục kết quả",
    endpoint: "/reports/docx/decision",
  },
  {
    id: "cadres", code: "Nội bộ", group: "internal", scope: "none", fileType: "XLSX", fileCode: "DanhSachCanBo",
    title: "Danh sách cán bộ (nội bộ)", endpoint: "/reports/cadres",
  },
  {
    id: "rating-summary", code: "Nội bộ", group: "internal", scope: "period", fileType: "XLSX", fileCode: "BangTongHop_XepLoaiCanBo",
    title: "Bảng tổng hợp xếp loại cán bộ", endpoint: "/reports/internal/rating-summary",
  },
  {
    id: "excellent-quota", code: "Nội bộ", group: "internal", scope: "period", fileType: "XLSX", fileCode: "KiemSoatTyLeXuatSac",
    title: "Báo cáo nội bộ — Kiểm soát tỷ lệ Hoàn thành xuất sắc nhiệm vụ theo tổ chức Đảng",
    endpoint: "/reports/internal/excellent-quota",
  },
];

/** Định dạng tệp xuất: gốc (Word/Excel) hoặc PDF do máy chủ chuyển. */
export type ReportFileFormat = "original" | "pdf";

/** Phần nhập tay của Mẫu 16 (để trống → giữ chữ mẫu của biểu mẫu). */
export interface Form16DraftContent {
  documentNumber?: string | null;
  recipient?: string | null;
  workingRules?: string | null;
  meetingDate?: string | null;
  organizer?: string | null;
  proposer?: string | null;
  proposal1?: string | null;
  proposal2?: string | null;
  proposal3?: string | null;
  signerName?: string | null;
}

/** Một dòng số liệu Mẫu 16 theo nhóm chức danh. */
export interface Form16SummaryRow {
  statCode?: string | null;
  subject: string;
  total: number;
  excellent: number;
  good: number;
  satisfactory: number;
  unsatisfactory: number;
  notRated: number;
  excellentPercent?: number | null;
}

/** Bản nháp Mẫu 16 kèm số liệu tổng hợp tự động. */
export interface Form16Draft {
  periodId: string;
  periodName: string;
  partyCellId?: string | null;
  partyOrganizationName: string;
  version?: number | null;
  updatedAt?: string | null;
  content: Form16DraftContent;
  baseRows: Form16SummaryRow[];
  superiorRows: Form16SummaryRow[];
  suggestedMeetingDate?: string | null;
}

export const withQuery = (endpoint: string, params: Record<string, string | undefined>): string => {
  const query = Object.entries(params)
    .filter(([, value]) => !!value)
    .map(([key, value]) => `${key}=${encodeURIComponent(value as string)}`)
    .join("&");
  if (!query) return endpoint;
  return endpoint.includes("?") ? `${endpoint}&${query}` : `${endpoint}?${query}`;
};

const SHORT_NAME_TOKEN = "{SHORT_NAME}";

/** Thay `{SHORT_NAME}` trong tên tệp bằng tên viết tắt của đơn vị (không tải được → bỏ phần này). */
async function resolveFileName(fileName: string): Promise<string> {
  if (!fileName.includes(SHORT_NAME_TOKEN)) return fileName;
  const shortName = await organizationSettingsService
    .getPublic()
    .then((info) => info.shortName)
    .catch(() => "");
  return shortName ? fileName.replace(SHORT_NAME_TOKEN, shortName) : fileName.replace(`_${SHORT_NAME_TOKEN}`, "");
}

/** Bỏ dấu tiếng Việt cho tên tệp (quy cách HD03: viết không dấu). */
export function asciiName(value: string): string {
  return value
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/đ/g, "d")
    .replace(/Đ/g, "D")
    .replace(/[^A-Za-z0-9 -]/g, "")
    .trim();
}

/** Tên tệp theo quy cách HD03 V.1: "Mau 16_<tên viết tắt>_Quy III-2026". */
export function hd03FileName(fileCode: string, unit: string, periodLabel: string, extension: string): string {
  return scopedFileName(`Mau ${fileCode}`, unit, periodLabel, extension);
}

/** Tên tệp "<phần đầu>_<tên viết tắt>_Quy III-2026" (không dấu) — cùng quy cách với biểu mẫu HD03. */
export function scopedFileName(prefix: string, unit: string, periodLabel: string, extension: string): string {
  const unitPart = asciiName(unit).replace(/\s+/g, "") || "DonVi";
  return `${prefix}_${unitPart}_${asciiName(periodLabel)}${extension}`;
}

/** Tên tệp báo cáo nội bộ: "<mã báo cáo>_<đơn vị>_Quy III-2026" (viết không dấu). */
export function internalFileName(fileCode: string, unit: string, periodLabel: string, extension: string): string {
  const unitPart = asciiName(unit).replace(/s+/g, "") || "DonVi";
  return `${fileCode}_${unitPart}_${asciiName(periodLabel)}${extension}`;
}

export const reportService = {
  /** Lấy tệp xuất từ máy chủ (kèm HttpOnly Cookie) và tên tệp máy chủ đặt. `format = "pdf"` để máy chủ chuyển sang PDF. */
  fetchReportFile(endpoint: string, format: ReportFileFormat = "original"): Promise<DownloadedFile> {
    const url = format === "pdf" ? withQuery(endpoint, { format: "pdf" }) : endpoint;
    // Chuyển PDF bằng LibreOffice có thể mất vài chục giây.
    return fetchFile(url, { timeout: format === "pdf" ? 120000 : undefined });
  },

  /** Chỉ lấy nội dung tệp xuất (xem trước PDF). */
  async fetchReportBlob(endpoint: string, format: ReportFileFormat = "original"): Promise<Blob> {
    return (await this.fetchReportFile(endpoint, format)).blob;
  },

  /** Tải báo cáo về máy: tên tệp theo máy chủ, không có thì dùng `fileName` (đổi đuôi khi xuất PDF). */
  async downloadReport(endpoint: string, fileName: string, format: ReportFileFormat = "original"): Promise<void> {
    if (!endpoint) return;
    const file = await this.fetchReportFile(endpoint, format);
    if (file.fileName) {
      saveBlob(file.blob, file.fileName);
      return;
    }
    const name = await resolveFileName(fileName);
    saveBlob(file.blob, format === "pdf" ? name.replace(/\.(docx|xlsx)$/i, ".pdf") : name);
  },

  /** Lập được Quyết định xếp loại chưa (lý do khi chưa). */
  getDecisionReadiness(periodId: string, branchId?: string): Promise<{ ready: boolean; message?: string | null }> {
    return request(withQuery("/reports/decision/readiness", { periodId, branchId }));
  },

  /** Bản nháp Mẫu 16 + số liệu tự động. Không truyền `branchId`: phạm vi mặc định theo quyền được giao. */
  getForm16Draft(periodId: string, branchId?: string): Promise<Form16Draft> {
    return request<Form16Draft>(withQuery("/reports/mau-16/draft", { periodId, branchId }));
  },

  /** Lưu bản nháp Mẫu 16 (kiểm tra phiên bản). */
  saveForm16Draft(periodId: string, branchId: string | undefined, version: number | null | undefined, content: Form16DraftContent): Promise<Form16Draft> {
    return request<Form16Draft>(withQuery("/reports/mau-16/draft", { periodId, branchId }), {
      method: "PUT",
      body: JSON.stringify({ version: version ?? undefined, content }),
    });
  },

  /** Xuất Mẫu 07 / 08 (Word) từ hồ sơ tập thể. */
  async exportCollective(form: "M07" | "M08", recordId: string, fileName: string, format: ReportFileFormat = "original"): Promise<void> {
    const code = form === "M07" ? "mau-07" : "mau-08";
    return this.downloadReport(`/reports/docx/${code}/${recordId}`, fileName, format);
  },

  /** Xuất Mẫu 08 bản Excel (HD03 V.1) từ hồ sơ tập thể. */
  async exportForm08Excel(recordId: string, fileName: string, format: ReportFileFormat = "original"): Promise<void> {
    return this.downloadReport(`/reports/form-08/${recordId}`, fileName, format);
  },

  /** Xuất Mẫu 12 (biên bản hội nghị) hoặc Mẫu 13 (biên bản kiểm phiếu) từ biên bản. */
  async exportMeeting(meetingId: string, form: "12" | "13", fileName: string, format: ReportFileFormat = "original"): Promise<void> {
    return this.downloadReport(`/reports/docx/mau-${form}/${meetingId}`, fileName, format);
  },
};
