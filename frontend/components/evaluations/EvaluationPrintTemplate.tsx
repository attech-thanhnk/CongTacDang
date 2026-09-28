/**
 * Loại văn bản hiển thị trong EvaluationPdfModal.
 *
 * Bản in HTML (dựng lại biểu mẫu và tính lại số liệu phía trình duyệt, in bằng window.print) đã được bỏ (T-38):
 * mọi bản xem/tải PDF nay lấy từ máy chủ, sinh từ cùng template Word/Excel và dữ liệu đã lưu.
 */
export type PrintTemplateType =
  | "mau01"
  | "mau02"
  | "mau09"
  | "mau10"
  | "mau13"
  | "mau14"
  | "mau15"
  | "individual";
