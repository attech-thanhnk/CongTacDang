import { request } from "./apiClient";
import { DownloadedFile, fetchFile, saveBlob } from "./download";
import { organizationSettingsService } from "./organizationSettingsService";

/** Nhóm trên trang Báo cáo: hồ sơ phải nộp hoặc báo cáo nội bộ (không mang số mẫu). */
export type ReportGroup = "hd03" | "internal";

/** `period`: theo kỳ + phạm vi; `none`: không cần kỳ; `collective`/`meeting`: xuất từ hồ sơ tập thể / biên bản. */
export type ReportScope = "period" | "none" | "collective" | "meeting";

export interface ReportDefinition {
  id: string;
  /** Số mẫu ("Mẫu 14") hoặc nhãn báo cáo nội bộ. */
  code: string;
  /** Nguyên văn tên biểu mẫu gốc. */
  title: string;
  group: ReportGroup;
  scope: ReportScope;
  /** Định dạng gốc của biểu mẫu. */
  fileType: "XLSX" | "DOCX";
  endpoint?: string;
  /** Mã mẫu trong tên tệp ("Mau 16_…_Quy III-2026"). */
  fileCode: string;
  /** Thay cho "Mau <fileCode>" với văn bản không mang số mẫu. */
  filePrefix?: string;
}

export const REPORTS: ReportDefinition[] = [
  {
    id: "mau-06", code: "Mẫu 06", group: "hd03", scope: "collective", fileType: "DOCX", fileCode: "06",
    title: "Báo cáo kết quả tập thể / lĩnh vực phục vụ liên kết trách nhiệm cán bộ lãnh đạo",
  },
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
    title: "Phiếu đánh giá, xếp loại cán bộ (phiếu kín)", endpoint: "/reports/docx/mau-11",
  },
  {
    id: "mau-12", code: "Mẫu 12", group: "hd03", scope: "meeting", fileType: "DOCX", fileCode: "12",
    title: "Biên bản hội nghị đánh giá, xếp loại chất lượng cán bộ quý",
  },
  {
    id: "mau-13", code: "Mẫu 13", group: "hd03", scope: "meeting", fileType: "DOCX", fileCode: "13",
    title: "Biên bản kiểm phiếu hội nghị đánh giá, xếp loại chất lượng cán bộ quý",
  },
  {
    id: "mau-14", code: "Mẫu 14", group: "hd03", scope: "period", fileType: "XLSX", fileCode: "14",
    title: "Danh sách đánh giá và đề xuất xếp loại",
    endpoint: "/reports/form-14",
  },
  {
    id: "mau-15a", code: "Mẫu 15A", group: "hd03", scope: "period", fileType: "XLSX", fileCode: "15A",
    title: "Tổng hợp kết quả đánh giá, xếp loại cán bộ (đối tượng đề nghị cấp trên quyết định)",
    endpoint: "/reports/form-15a",
  },
  {
    id: "mau-15b", code: "Mẫu 15B", group: "hd03", scope: "period", fileType: "XLSX", fileCode: "15B",
    title: "Tổng hợp kết quả đánh giá, xếp loại cán bộ (đối tượng thuộc diện Đảng ủy/Chi ủy cơ sở quyết định)",
    endpoint: "/reports/form-15b",
  },
  {
    id: "mau-16", code: "Mẫu 16", group: "hd03", scope: "period", fileType: "DOCX", fileCode: "16",
    title: "Báo cáo về kết quả đánh giá, xếp loại chất lượng cán bộ quý",
    endpoint: "/reports/docx/mau-16",
  },
  {
    id: "decision", code: "Quyết định", group: "hd03", scope: "period", fileType: "DOCX", fileCode: "QD",
    filePrefix: "Quyet dinh xep loai",
    title: "Quyết định xếp loại cán bộ quý của Đảng ủy (Chi ủy), kèm Phụ lục kết quả",
    endpoint: "/reports/docx/decision",
  },
  {
    id: "cadres", code: "Nội bộ", group: "internal", scope: "none", fileType: "XLSX", fileCode: "DanhSachCanBo",
    title: "Danh sách cán bộ", endpoint: "/reports/cadres",
  },
  {
    id: "rating-summary", code: "Nội bộ", group: "internal", scope: "period", fileType: "XLSX", fileCode: "BangTongHop_XepLoaiCanBo",
    title: "Bảng tổng hợp xếp loại cán bộ", endpoint: "/reports/internal/rating-summary",
  },
  {
    id: "excellent-quota", code: "Nội bộ", group: "internal", scope: "period", fileType: "XLSX", fileCode: "KiemSoatTyLeXuatSac",
    title: "Kiểm soát tỷ lệ Hoàn thành xuất sắc nhiệm vụ theo tổ chức Đảng",
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
  /** Số Xuất sắc tối đa của nhóm chức danh (không in lên biểu mẫu); null với dòng "Tổng cộng". */
  maxExcellentAllowed?: number | null;
  /** Nhóm vượt trần Xuất sắc (cảnh báo). */
  isExceedingQuota?: boolean;
}

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

/** Tên tệp viết không dấu. */
export function asciiName(value: string): string {
  return value
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/đ/g, "d")
    .replace(/Đ/g, "D")
    .replace(/[^A-Za-z0-9 -]/g, "")
    .trim();
}

/** "Mau 16_<tên viết tắt>_Quy III-2026". */
export function hd03FileName(fileCode: string, unit: string, periodLabel: string, extension: string): string {
  return scopedFileName(`Mau ${fileCode}`, unit, periodLabel, extension);
}

/** "<phần đầu>_<tên viết tắt>_Quy III-2026" (không dấu). */
export function scopedFileName(prefix: string, unit: string, periodLabel: string, extension: string): string {
  const unitPart = asciiName(unit).replace(/\s+/g, "") || "DonVi";
  return `${prefix}_${unitPart}_${asciiName(periodLabel)}${extension}`;
}

/** "<mã báo cáo>_<đơn vị>_Quy III-2026" (không dấu). */
export function internalFileName(fileCode: string, unit: string, periodLabel: string, extension: string): string {
  const unitPart = asciiName(unit).replace(/\s+/g, "") || "DonVi";
  return `${fileCode}_${unitPart}_${asciiName(periodLabel)}${extension}`;
}

export const reportService = {
  /** Trả nội dung và tên tệp máy chủ đặt; `format = "pdf"` để máy chủ chuyển sang PDF. */
  fetchReportFile(endpoint: string, format: ReportFileFormat = "original"): Promise<DownloadedFile> {
    const url = format === "pdf" ? withQuery(endpoint, { format: "pdf" }) : endpoint;
    // Chuyển PDF bằng LibreOffice có thể mất vài chục giây.
    return fetchFile(url, { timeout: format === "pdf" ? 120000 : undefined });
  },

  /** Chỉ lấy nội dung tệp xuất (xem trước PDF). */
  async fetchReportBlob(endpoint: string, format: ReportFileFormat = "original"): Promise<Blob> {
    return (await this.fetchReportFile(endpoint, format)).blob;
  },

  /** Tên tệp theo máy chủ, không có thì dùng `fileName` (đổi đuôi khi xuất PDF). */
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

  /** Lập được Quyết định xếp loại chưa (kèm lý do). */
  getDecisionReadiness(periodId: string, branchId?: string): Promise<{ ready: boolean; message?: string | null }> {
    return request(withQuery("/reports/decision/readiness", { periodId, branchId }));
  },

  /** Không truyền `branchId`: phạm vi mặc định theo quyền được giao. */
  getForm16Draft(periodId: string, branchId?: string): Promise<Form16Draft> {
    return request<Form16Draft>(withQuery("/reports/mau-16/draft", { periodId, branchId }));
  },

  /** Có kiểm tra phiên bản. */
  saveForm16Draft(periodId: string, branchId: string | undefined, version: number | null | undefined, content: Form16DraftContent): Promise<Form16Draft> {
    return request<Form16Draft>(withQuery("/reports/mau-16/draft", { periodId, branchId }), {
      method: "PUT",
      body: JSON.stringify({ version: version ?? undefined, content }),
    });
  },

  async exportCollective(form: "M06" | "M07" | "M08", recordId: string, fileName: string, format: ReportFileFormat = "original"): Promise<void> {
    const code = form === "M06" ? "mau-06" : form === "M07" ? "mau-07" : "mau-08";
    return this.downloadReport(`/reports/docx/${code}/${recordId}`, fileName, format);
  },

  /** Mẫu 08 bản Excel. */
  async exportForm08Excel(recordId: string, fileName: string, format: ReportFileFormat = "original"): Promise<void> {
    return this.downloadReport(`/reports/form-08/${recordId}`, fileName, format);
  },

  /** Mẫu 03 (phiếu đối soát), 19 (phiếu kiểm soát) hoặc 20 (biên bản hiệu chuẩn) theo mã phiếu. */
  async exportQualityForm(form: "03" | "19" | "20", formId: string, fileName: string, format: ReportFileFormat = "original"): Promise<void> {
    return this.downloadReport(`/reports/docx/mau-${form}/${formId}`, fileName, format);
  },

  /** Mẫu 12 (biên bản hội nghị) hoặc Mẫu 13 (biên bản kiểm phiếu). */
  async exportMeeting(meetingId: string, form: "11" | "12" | "13", fileName: string, format: ReportFileFormat = "original"): Promise<void> {
    return this.downloadReport(form === "11" ? `/reports/docx/mau-11/meeting/${meetingId}` : `/reports/docx/mau-${form}/${meetingId}`, fileName, format);
  },
};
