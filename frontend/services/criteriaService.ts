import { request } from "./apiClient";
import type { SelfAssessmentSection, TaskLevel } from "./evaluationService";

export type { SelfAssessmentSection, TaskLevel };

// Hàm "tạm tính" ở đây chỉ để hiển thị tức thì; máy chủ tính lại và là nguồn số liệu duy nhất.

export type CriteriaScoringMode = "Binary" | "Range";
export type NotApplicableRule = "ExcludeAndRescale" | "GrantFull";
export type ScoreRoundingMode = "HalfUp" | "HalfEven" | "Truncate";
export type CriteriaSetStatus = "Draft" | "Published" | "Archived";
export type RankedGrade = "HoanThanhXuatSac" | "HoanThanhTot" | "HoanThanh" | "KhongHoanThanh";

export interface CriteriaItem {
  code: string;
  text: string;
  /** Nội dung in trên phiếu tự chấm (nguyên văn biểu mẫu gốc); trống → in `text`. */
  formText?: string | null;
  maxScore: number;
}

export interface CriteriaGroup {
  code: string;
  name: string;
  scoringMode: CriteriaScoringMode;
  items: CriteriaItem[];
}

/** Nhóm điểm của hồ sơ tập thể mà tiêu chí con cộng vào. */
export type CollectiveScoreKind = "General" | "Task";

export const COLLECTIVE_KIND_LABELS: Record<CollectiveScoreKind, string> = {
  General: "Nhóm tiêu chí chung",
  Task: "Nhóm kết quả thực hiện nhiệm vụ",
};

/** Một nhóm của khung tiêu chí tập thể (Mẫu 06, 07); mã nhóm/tiêu chí con duy nhất trong khung. */
export interface CollectiveCriteriaGroup {
  code: string;
  name: string;
  kind: CollectiveScoreKind;
  items: CriteriaItem[];
}

export interface ResultAxis {
  code: string;
  name: string;
  description?: string | null;
  maxScore: number;
  /** Tiêu đề trục in ở cột "Nội dung tiêu chí" của Mẫu 09B (trống → dựng từ tên trục). */
  formTitle?: string | null;
  /** In dưới tiêu đề trục trên Mẫu 09B, mỗi dòng một gạch đầu dòng (trống → nội dung áp dụng). */
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
  /** Biên độ điều chỉnh tỷ lệ A-B-C-D theo hồ sơ (điểm phần trăm); null/0 = không cho điều chỉnh. */
  frameAdjustLimit?: number | null;
  allowNotApplicable: boolean;
  notApplicableRule: NotApplicableRule;
  deductionReasonMinPoints?: number | null;
  explanationThreshold: number;
  explanationOnGradeChange: boolean;
  /** Trần Xuất sắc: tỷ lệ (0,20..0,25) × số "Hoàn thành tốt" của nhóm chức danh, làm tròn 0,5 lên 1. */
  excellentQuota: { ratio: number };
  rounding: { taskScore: RoundingRule; tasksTotal: RoundingRule; generalTotal: RoundingRule; total: RoundingRule };
  collectiveGeneralMaxScore: number;
  collectiveTaskMaxScore: number;
  /** Công khai kèm điểm chính thức (mặc định chỉ công khai mức). */
  publishScores?: boolean;
  /** Mức bắt buộc lập kế hoạch Mẫu 17; mặc định HoanThanh, KhongHoanThanh. */
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
  /** Mã theo `RECORD_FORMS`; 09A/09B luôn theo mẫu tự chấm của bộ. */
  requiredForms: string[];
  selfAssessmentSections: SelfAssessmentSection[];
  /** Các mức tự đánh giá của từng nhiệm vụ (ô "Mức" ở Mẫu 9D); rỗng = không dùng. */
  taskLevels: TaskLevel[];
  /** Tiền tố mỗi nhiệm vụ khi dựng Mẫu 09C, `{n}` = số thứ tự. */
  taskLineLabel?: string | null;
  /** Câu nối mức khi dựng Mẫu 09C, `{level}` = mã mức. */
  taskLevelPhrase?: string | null;
  /** Khung tiêu chí tập thể (Mẫu 06, 07); rỗng = hồ sơ tập thể nhập thẳng hai điểm nhóm. */
  collectiveGroups: CollectiveCriteriaGroup[];
}

/** Khớp `RecordFormCodes` phía máy chủ. */
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

export interface GeneralItemScore {
  score: number;
  notApplicable?: boolean;
  reason?: string | null;
}

/** Tên cách chấm hiển thị cho người dùng (số mẫu chỉ dùng khi in). */
export const SCORING_METHOD_LABELS: Record<string, string> = {
  "09A": "Chấm theo việc đã đăng ký",
  "09B": "Chấm theo 6 trục",
};

export function scoringMethodLabel(form?: string | null): string {
  return (form && SCORING_METHOD_LABELS[form]) || form || "";
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

/** Cùng quy tắc làm tròn với máy chủ. */
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

/** Có xử lý K/AD theo bộ. */
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

/** Mức theo điểm như máy chủ: ngưỡng điểm và tỷ lệ nhiệm vụ vượt chuẩn (khi có). */
export function suggestGrade(content: CriteriaSetContent, score: number, exceedRatio?: number | null): RankedGrade {
  for (const grade of RANKED_GRADES) {
    const rule = content.grades.find((g) => g.grade === grade);
    if (!rule || score + 1e-6 < rule.minScore) continue;
    if (rule.minExceedStandardRatio != null && exceedRatio != null && exceedRatio + 1e-6 < rule.minExceedStandardRatio) continue;
    return grade;
  }
  return "KhongHoanThanh";
}

/** Tỷ lệ nhiệm vụ còn hiệu lực đánh dấu vượt chuẩn (09A); chấm theo trục hoặc chưa có nhiệm vụ → null. */
export function exceedRatioOf(tasks: { changeStatus?: string | null; isExceedStandard?: boolean }[], axisScoring: boolean): number | null {
  if (axisScoring) return null;
  const active = tasks.filter((t) => !t.changeStatus);
  return active.length > 0 ? active.filter((t) => t.isExceedStandard).length / active.length : null;
}

/** So mức đề xuất với mức theo điểm: dương = cao hơn, âm = thấp hơn, 0 = khớp hoặc chưa chọn. */
export function compareWithScoreGrade(content: CriteriaSetContent, grade: string | null | undefined, score: number, exceedRatio?: number | null): number {
  const proposed = RANKED_GRADES.indexOf(grade as RankedGrade);
  if (proposed < 0) return 0;
  return RANKED_GRADES.indexOf(suggestGrade(content, score, exceedRatio)) - proposed;
}

/** Cảnh báo khi mức chọn khác mức theo điểm; `required` = bộ tiêu chí bắt buộc nêu lý do (như máy chủ). */
export function gradeProposalCheck(
  content: CriteriaSetContent,
  grade: string | null | undefined,
  score: number,
  exceedRatio?: number | null
): { direction: number; required: boolean; message: string } | null {
  const direction = compareWithScoreGrade(content, grade, score, exceedRatio);
  if (direction === 0) return null;
  const byScore = suggestGrade(content, score, exceedRatio);
  return {
    direction,
    required: content.parameters.explanationOnGradeChange,
    message: `Mức đã chọn ${direction > 0 ? "cao hơn" : "thấp hơn"} mức theo điểm (${fmt(score)} điểm → ${GRADE_LABELS[byScore]}).`,
  };
}

/** Chênh lệch tự chấm – thẩm định cần giải trình (theo ngưỡng của bộ). */
export function requiresExplanation(content: CriteriaSetContent, self: number, appraisal: number | null): boolean {
  if (appraisal == null || Number.isNaN(appraisal)) return false;
  const p = content.parameters;
  if (p.explanationThreshold > 0 && Math.abs(self - appraisal) + 1e-6 >= p.explanationThreshold) return true;
  return p.explanationOnGradeChange && gradeByScore(content, self) !== gradeByScore(content, appraisal);
}

export function collectiveTotal(content: CriteriaSetContent, kind: CollectiveScoreKind): number {
  return (content.collectiveGroups ?? [])
    .filter((g) => g.kind === kind)
    .reduce((s, g) => s + g.items.reduce((t, i) => t + (Number(i.maxScore) || 0), 0), 0);
}

export function collectiveMax(content: CriteriaSetContent, kind: CollectiveScoreKind): number {
  return kind === "General" ? content.parameters.collectiveGeneralMaxScore : content.parameters.collectiveTaskMaxScore;
}

/** Kiểm tra nhanh để báo tức thì; kiểm tra đầy đủ do máy chủ trả về khi lưu. */
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
  const collective = content.collectiveGroups ?? [];
  if (collective.length > 0) {
    for (const kind of ["General", "Task"] as CollectiveScoreKind[]) {
      const sum = collectiveTotal(content, kind);
      const max = collectiveMax(content, kind);
      if (Math.abs(sum - max) > 1e-6)
        messages.push(`Khung tiêu chí tập thể: tổng ${COLLECTIVE_KIND_LABELS[kind].toLowerCase()} ${fmt(sum)} ≠ điểm tối đa ${fmt(max)}.`);
    }
    const collectiveCodes = new Set<string>();
    for (const code of collective.flatMap((g) => [g.code, ...g.items.map((i) => i.code)]).map((c) => c.trim())) {
      if (!/^[A-Za-z0-9][A-Za-z0-9._-]{0,19}$/.test(code)) messages.push(`Mã nhóm/tiêu chí tập thể "${code}" không hợp lệ (chữ không dấu, số, . - _; tối đa 20 ký tự).`);
      else if (collectiveCodes.has(code.toUpperCase())) messages.push(`Mã nhóm/tiêu chí tập thể "${code}" bị trùng.`);
      collectiveCodes.add(code.toUpperCase());
    }
    for (const group of collective)
      if (group.items.length === 0) messages.push(`Nhóm tiêu chí tập thể "${group.name.trim() || group.code}" phải có ít nhất một tiêu chí con.`);
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
  const adjust = content.parameters.frameAdjustLimit;
  if (adjust != null && !(adjust >= 0 && adjust <= MAX_FRAME_ADJUST_LIMIT))
    messages.push(`Biên độ điều chỉnh tỷ lệ A-B-C-D phải từ 0 đến ${MAX_FRAME_ADJUST_LIMIT} điểm phần trăm.`);
  if (excellentQuotaInvalid(content.parameters.excellentQuota.ratio))
    messages.push(`Trần tỷ lệ Hoàn thành xuất sắc phải từ ${EXCELLENT_QUOTA_PERCENT.min}% đến ${EXCELLENT_QUOTA_PERCENT.max}%.`);
  const alertDays = content.parameters.improvementPlanAlertDays ?? 90;
  if (!(Number.isInteger(alertDays) && alertDays >= 1 && alertDays <= FORM_CONFIG_LIMITS.alertDaysMax))
    messages.push(`Số ngày cảnh báo lập kế hoạch 30-60-90 ngày sau khi đóng kỳ phải là số nguyên từ 1 đến ${FORM_CONFIG_LIMITS.alertDaysMax}.`);
  return messages;
}

export function fmt(value: number, digits = 2): string {
  return Number.isFinite(value) ? value.toLocaleString("vi-VN", { maximumFractionDigits: digits }) : "—";
}

/** Tỷ lệ 4 mặt chấm của khung, ví dụ "A 15% · B 50% · C 15% · D 20%". */
export function describeFrame(frame: Pick<WeightFrame, "a" | "b" | "c" | "d">): string {
  return `A ${fmt(frame.a * 100)}% · B ${fmt(frame.b * 100)}% · C ${fmt(frame.c * 100)}% · D ${fmt(frame.d * 100)}%`;
}

/** Biên độ tối đa (điểm phần trăm) khi khai báo trong bộ tiêu chí — khớp máy chủ. */
export const MAX_FRAME_ADJUST_LIMIT = 50;

/** Khoảng cho phép của trần tỷ lệ Hoàn thành xuất sắc (%) — khớp máy chủ. */
export const EXCELLENT_QUOTA_PERCENT = { min: 20, max: 25 } as const;

/** Tỷ lệ trần (0..1) nằm ngoài 20%–25%. */
export function excellentQuotaInvalid(ratio: number): boolean {
  const percent = Math.round(ratio * 10000) / 100;
  return !(Number.isFinite(percent) && percent >= EXCELLENT_QUOTA_PERCENT.min && percent <= EXCELLENT_QUOTA_PERCENT.max);
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

  defaultCollectiveGroups(): Promise<CollectiveCriteriaGroup[]> {
    return request<CollectiveCriteriaGroup[]>("/criteria-sets/defaults/collective-groups");
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
