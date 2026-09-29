import { request } from "./apiClient";
import type { SelfAssessmentSection } from "./evaluationService";

export type { SelfAssessmentSection };

// ---------------------------------------------------------------------------
// Bộ tiêu chí và thang điểm theo phiên bản (task 16). Nội dung bộ khai báo được; cấu trúc công thức tính điểm cố định ở
// máy chủ. Hàm tính "tạm tính" ở đây chỉ để hiển thị tức thì — máy chủ tính lại và là nguồn số liệu duy nhất.
// ---------------------------------------------------------------------------

export type CriteriaScoringMode = "Binary" | "Range";
export type NotApplicableRule = "ExcludeAndRescale" | "GrantFull";
export type ScoreRoundingMode = "HalfUp" | "HalfEven" | "Truncate";
export type QuotaDenominator = "GoodOnly" | "GoodOrBetter";
export type CriteriaSetStatus = "Draft" | "Published" | "Archived";
export type RankedGrade = "HoanThanhXuatSac" | "HoanThanhTot" | "HoanThanh" | "KhongHoanThanh";

export interface CriteriaItem {
  code: string;
  text: string;
  maxScore: number;
}

export interface CriteriaGroup {
  code: string;
  name: string;
  scoringMode: CriteriaScoringMode;
  items: CriteriaItem[];
}

export interface ResultAxis {
  code: string;
  name: string;
  description?: string | null;
  maxScore: number;
  /** Tiêu đề trục in ở cột "Nội dung tiêu chí" của Mẫu 09B (trống → dựng từ tên trục). */
  formTitle?: string | null;
  /** Nội dung gợi ý in dưới tiêu đề trục trên Mẫu 09B, mỗi dòng một gạch đầu dòng (trống → nội dung áp dụng). */
  formGuidance?: string | null;
}

export interface WeightFrame {
  code: string;
  name: string;
  a: number;
  b: number;
  c: number;
  d: number;
}

export interface ConversionBand {
  minPercent: number;
  label: string;
  description?: string | null;
}

export interface GradeRule {
  grade: RankedGrade;
  minScore: number;
  minExceedStandardRatio?: number | null;
  conditions?: string | null;
}

export interface RoundingRule {
  decimals: number;
  mode: ScoreRoundingMode;
}

export interface CriteriaParameters {
  minTasks: number;
  maxTasks: number;
  totalTaskWeight: number;
  taskWeightTolerance: number;
  generalMaxScore: number;
  defaultWeightFrameCode?: string | null;
  allowNotApplicable: boolean;
  notApplicableRule: NotApplicableRule;
  deductionReasonMinPoints?: number | null;
  explanationThreshold: number;
  explanationOnGradeChange: boolean;
  excellentQuota: { ratio: number; denominator: QuotaDenominator; rounding: ScoreRoundingMode };
  rounding: { taskScore: RoundingRule; tasksTotal: RoundingRule; generalTotal: RoundingRule; total: RoundingRule };
  collectiveGeneralMaxScore: number;
  collectiveTaskMaxScore: number;
  /** Task 20: công khai kèm điểm chính thức (mặc định chỉ công khai mức). */
  publishScores?: boolean;
  /** Task 20: mức chính thức bắt buộc lập kế hoạch 30-60-90 ngày (Mẫu 17); mặc định HoanThanh, KhongHoanThanh. */
  improvementPlanRequiredGrades?: string[];
  /** Cảnh báo "kế hoạch cần lập" còn xét kỳ đã đóng trong số ngày này (1–3650, mặc định 90). */
  improvementPlanAlertDays?: number;
}

/** Nội dung bộ tiêu chí (jsonb, schemaVersion 1). */
export interface CriteriaSetContent {
  schemaVersion: number;
  generalGroups: CriteriaGroup[];
  axes: ResultAxis[];
  weightFrames: WeightFrame[];
  conversionScale: ConversionBand[];
  grades: GradeRule[];
  parameters: CriteriaParameters;
  /** Biểu mẫu cá nhân áp dụng (mã theo `RECORD_FORMS`); mẫu tự chấm 09A/09B luôn theo mẫu tự chấm của bộ. */
  requiredForms: string[];
  /** Các mục tự luận của Mẫu 09C. */
  selfAssessmentSections: SelfAssessmentSection[];
}

/** Danh mục biểu mẫu cá nhân (HD03 mục 7) — khớp `RecordFormCodes` phía máy chủ. */
export const RECORD_FORMS: { code: string; name: string }[] = [
  { code: "01", name: "Mẫu 01 — Phiếu giao / đăng ký sản phẩm, công việc chuyên môn" },
  { code: "02", name: "Mẫu 02 — Phiếu tự đánh giá kết quả thực hiện sản phẩm, công việc" },
  { code: "09A", name: "Mẫu 09A — Phiếu tự chấm điểm (theo sản phẩm Mẫu 01/02)" },
  { code: "09B", name: "Mẫu 09B — Phiếu tự chấm điểm (theo trục)" },
  { code: "09C", name: "Mẫu 09C — Bản tự đánh giá, xếp loại của cá nhân" },
  { code: "9D", name: "Mẫu 9D — Phụ lục kết quả thực hiện nhiệm vụ trong quý" },
  { code: "10", name: "Mẫu 10 — Phiếu thẩm định, nhận xét, đề xuất xếp loại" },
];

/** Giới hạn cấu hình biểu mẫu (khớp kiểm tra phía máy chủ). */
export const FORM_CONFIG_LIMITS = { sectionMin: 100, sectionMax: 20000, title: 500, guidance: 8000, alertDaysMax: 3650 } as const;

/** Ảnh chụp bộ tiêu chí trong kỳ. */
export interface CriteriaSnapshot {
  setId: string;
  code: string;
  name: string;
  selfScoreForm: "09A" | "09B" | string;
  takenAt: string;
  content: CriteriaSetContent;
}

export interface CriteriaSetListItem {
  id: string;
  version: number;
  code: string;
  name: string;
  status: CriteriaSetStatus;
  statusDisplayName: string;
  selfScoreForm: "09A" | "09B" | string;
  notes?: string | null;
  sourceSetId?: string | null;
  publishedAt?: string | null;
  archivedAt?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  generalItemCount: number;
  axisCount: number;
  weightFrameCount: number;
  usedByPeriods: string[];
}

export interface CriteriaSetDto extends CriteriaSetListItem {
  content: CriteriaSetContent;
  validationErrors: string[];
}

export interface WeightFrameOptions {
  sourceName?: string | null;
  frames: WeightFrame[];
}

/** Điểm một tiêu chí con trên hồ sơ. */
export interface GeneralItemScore {
  score: number;
  notApplicable?: boolean;
  reason?: string | null;
}

export const GRADE_LABELS: Record<RankedGrade, string> = {
  HoanThanhXuatSac: "Hoàn thành xuất sắc nhiệm vụ",
  HoanThanhTot: "Hoàn thành tốt nhiệm vụ",
  HoanThanh: "Hoàn thành nhiệm vụ",
  KhongHoanThanh: "Không hoàn thành nhiệm vụ",
};

export const RANKED_GRADES: RankedGrade[] = ["HoanThanhXuatSac", "HoanThanhTot", "HoanThanh", "KhongHoanThanh"];

export const ROUNDING_LABELS: Record<ScoreRoundingMode, string> = {
  HalfUp: "Nửa lên (0,5 → lên)",
  HalfEven: "Nửa về số chẵn",
  Truncate: "Cắt bỏ phần thừa",
};

/** Làm tròn hiển thị (tạm tính) — cùng quy tắc máy chủ. */
export function roundScore(value: number, rule: RoundingRule): number {
  const factor = Math.pow(10, Math.min(4, Math.max(0, rule.decimals)));
  const scaled = Number((value * factor).toPrecision(15));
  let rounded: number;
  if (rule.mode === "Truncate") rounded = Math.trunc(scaled);
  else if (rule.mode === "HalfEven") {
    const floor = Math.floor(scaled);
    const diff = scaled - floor;
    rounded = Math.abs(diff - 0.5) < 1e-9 ? (floor % 2 === 0 ? floor : floor + 1) : Math.round(scaled);
  } else rounded = Math.sign(scaled) * Math.round(Math.abs(scaled));
  return rounded / factor;
}

/** Tổng điểm nhóm tiêu chí chung tạm tính (có xử lý K/AD theo bộ). */
export function generalTotal(content: CriteriaSetContent, scores: Record<string, GeneralItemScore>): number {
  let achieved = 0;
  let applicable = 0;
  let notApplicable = 0;
  for (const group of content.generalGroups) {
    for (const item of group.items) {
      const s = scores[item.code];
      if (s?.notApplicable) {
        notApplicable += item.maxScore;
        continue;
      }
      applicable += item.maxScore;
      achieved += Math.min(item.maxScore, Math.max(0, Number(s?.score) || 0));
    }
  }
  let result = achieved;
  if (notApplicable > 0) {
    result = content.parameters.notApplicableRule === "GrantFull"
      ? achieved + notApplicable
      : applicable > 0 ? (achieved / applicable) * (applicable + notApplicable) : applicable + notApplicable;
  }
  return roundScore(result, content.parameters.rounding.generalTotal);
}

/** Mức theo ngưỡng điểm của bộ (không xét điều kiện). */
export function gradeByScore(content: CriteriaSetContent, score: number): RankedGrade {
  for (const grade of RANKED_GRADES) {
    const rule = content.grades.find((g) => g.grade === grade);
    if (rule && score + 1e-6 >= rule.minScore) return grade;
  }
  return "KhongHoanThanh";
}

/** Chênh lệch tự chấm – thẩm định cần giải trình (theo ngưỡng của bộ). */
export function requiresExplanation(content: CriteriaSetContent, self: number, appraisal: number | null): boolean {
  if (appraisal == null || Number.isNaN(appraisal)) return false;
  const p = content.parameters;
  if (p.explanationThreshold > 0 && Math.abs(self - appraisal) + 1e-6 >= p.explanationThreshold) return true;
  return p.explanationOnGradeChange && gradeByScore(content, self) !== gradeByScore(content, appraisal);
}

/** Kiểm tra nhanh (tổng điểm) để báo tức thì khi sửa bản nháp; kiểm tra đầy đủ do máy chủ trả về khi lưu. */
export function quickChecks(content: CriteriaSetContent, form: string): string[] {
  const messages: string[] = [];
  const general = content.generalGroups.reduce((s, g) => s + g.items.reduce((t, i) => t + (Number(i.maxScore) || 0), 0), 0);
  if (Math.abs(general - content.parameters.generalMaxScore) > 1e-6)
    messages.push(`Tổng tiêu chí con ${fmt(general)} ≠ điểm tối đa nhóm tiêu chí chung ${fmt(content.parameters.generalMaxScore)}.`);
  if (form === "09B") {
    const axes = content.axes.reduce((s, a) => s + (Number(a.maxScore) || 0), 0);
    if (Math.abs(axes - content.parameters.totalTaskWeight) > 1e-6)
      messages.push(`Tổng điểm tối đa các trục ${fmt(axes)} ≠ điểm tối đa nhóm nhiệm vụ ${fmt(content.parameters.totalTaskWeight)}.`);
  }
  for (const frame of content.weightFrames) {
    const sum = frame.a + frame.b + frame.c + frame.d;
    if (Math.abs(sum - 1) > 1e-6) messages.push(`Khung ${frame.code}: tổng tỷ trọng ${fmt(sum * 100)}% ≠ 100%.`);
  }
  const sections = content.selfAssessmentSections ?? [];
  if ((content.requiredForms ?? []).includes("09C") && sections.length === 0)
    messages.push("Áp dụng Mẫu 09C phải khai báo ít nhất một mục tự đánh giá.");
  const codes = new Set<string>();
  for (const section of sections) {
    const code = section.code.trim();
    if (!/^[A-Za-z0-9][A-Za-z0-9._-]{0,19}$/.test(code)) messages.push(`Mã mục Mẫu 09C "${section.code}" không hợp lệ (chữ không dấu, số, . - _; tối đa 20 ký tự).`);
    else if (codes.has(code.toUpperCase())) messages.push(`Mã mục Mẫu 09C "${code}" bị trùng.`);
    codes.add(code.toUpperCase());
    if (!section.title.trim()) messages.push(`Mục Mẫu 09C "${code}" chưa có tiêu đề.`);
    if (!(section.maxLength >= FORM_CONFIG_LIMITS.sectionMin && section.maxLength <= FORM_CONFIG_LIMITS.sectionMax))
      messages.push(`Số ký tự tối đa của mục Mẫu 09C "${code}" phải từ ${FORM_CONFIG_LIMITS.sectionMin} đến ${FORM_CONFIG_LIMITS.sectionMax}.`);
  }
  const alertDays = content.parameters.improvementPlanAlertDays ?? 90;
  if (!(Number.isInteger(alertDays) && alertDays >= 1 && alertDays <= FORM_CONFIG_LIMITS.alertDaysMax))
    messages.push(`Số ngày cảnh báo lập kế hoạch 30-60-90 ngày sau khi đóng kỳ phải là số nguyên từ 1 đến ${FORM_CONFIG_LIMITS.alertDaysMax}.`);
  return messages;
}

export function fmt(value: number, digits = 2): string {
  return Number.isFinite(value) ? value.toLocaleString("vi-VN", { maximumFractionDigits: digits }) : "—";
}

export const criteriaService = {
  list(): Promise<CriteriaSetListItem[]> {
    return request<CriteriaSetListItem[]>("/criteria-sets");
  },

  get(id: string): Promise<CriteriaSetDto> {
    return request<CriteriaSetDto>(`/criteria-sets/${id}`);
  },

  defaults(form: string): Promise<CriteriaSetContent> {
    return request<CriteriaSetContent>(`/criteria-sets/defaults/${form}`);
  },

  weightFrames(): Promise<WeightFrameOptions> {
    return request<WeightFrameOptions>("/criteria-sets/weight-frames");
  },

  create(payload: { code: string; name: string; selfScoreForm: string; notes?: string }): Promise<CriteriaSetDto> {
    return request<CriteriaSetDto>("/criteria-sets", { method: "POST", body: JSON.stringify(payload) });
  },

  clone(id: string, payload: { code?: string; name?: string } = {}): Promise<CriteriaSetDto> {
    return request<CriteriaSetDto>(`/criteria-sets/${id}/clone`, { method: "POST", body: JSON.stringify(payload) });
  },

  update(
    id: string,
    payload: { version: number; code?: string; name?: string; selfScoreForm?: string; notes?: string; content?: CriteriaSetContent }
  ): Promise<CriteriaSetDto> {
    return request<CriteriaSetDto>(`/criteria-sets/${id}`, { method: "PUT", body: JSON.stringify(payload) });
  },

  publish(id: string, version: number): Promise<CriteriaSetDto> {
    return request<CriteriaSetDto>(`/criteria-sets/${id}/publish`, { method: "POST", body: JSON.stringify({ version }) });
  },

  archive(id: string, version: number): Promise<CriteriaSetDto> {
    return request<CriteriaSetDto>(`/criteria-sets/${id}/archive`, { method: "POST", body: JSON.stringify({ version }) });
  },

  async remove(id: string, version: number): Promise<void> {
    await request(`/criteria-sets/${id}?version=${version}`, { method: "DELETE" });
  },
};
