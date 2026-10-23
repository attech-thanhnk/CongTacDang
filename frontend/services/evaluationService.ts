import { ApiError, request } from "./apiClient";
import type { CriteriaSnapshot, GeneralItemScore, WeightFrame } from "./criteriaService";
import type { DeadlineRule } from "./deadlineRules";

// Không tự suy luật: bước/nút theo API `actions`, `work-queue`; tiến trình theo `progress` của máy chủ.

export type WorkflowStepCode =
  | "B1_REGISTER"
  | "B1_APPROVE"
  | "B2_SELF_SCORE"
  | "B2_CELL_CONFIRM"
  | "B3A_COLLECTIVE"
  | "B3B_APPRAISAL"
  | "B3C_DIRECTOR"
  | "B4_DECISION"
  | "B5_PUBLISH";

/** Mã hành động trả về từ `GET records/{id}/actions`. */
export type WorkflowActionCode =
  | "SubmitTasks"
  | "ApproveTasks"
  | "ReturnTasks"
  | "RevokeTaskApproval"
  | "SubmitSelfScore"
  | "ConfirmByCell"
  | "ReturnByCell"
  | "RecordCollectiveProposal"
  | "Appraise"
  | "ReturnByAppraiser"
  | "DirectorReview"
  | "RecordDecision"
  | "Publish"
  | "Reopen"
  /** Gia hạn mở riêng hồ sơ khi kỳ khóa dữ liệu. */
  | "ExtendUnlock"
  /** Ghi nhận kết quả của bước do cấp trên thực hiện (bước ở trường `step`). */
  | "RecordExternal";

export type StepMode = "Internal" | "External" | "Off";

export interface ProfileStepSetting {
  mode: StepMode;
  /** Mã quyền khi `Internal`; chế độ khác: null. */
  permission?: string | null;
  deadline?: string | null;
  /** Quy tắc tính thời hạn theo quý (null = nhập tay). */
  rule?: DeadlineRule | null;
}

/** Cấu hình bước cho một nhóm đối tượng. */
export interface WorkflowProfile {
  code: string;
  name: string;
  description?: string | null;
  steps: Record<string, ProfileStepSetting>;
}

/** Cấu hình kỳ (jsonb); tiêu chí và tham số chấm nằm trong bộ tiêu chí của kỳ. */
export interface PeriodSettings {
  schemaVersion: number;
  profiles: WorkflowProfile[];
  /** Khóa: "CoSo" | "CapTren" → mã hồ sơ luồng. */
  defaultProfiles: Record<string, string>;
  enforceDeadlines: boolean;
}

export interface EvaluationPeriodDto {
  id: string;
  version?: number;
  year: number;
  quarter: number;
  name: string;
  startDate: string;
  endDate: string;
  /** Draft / Open / Locked / Closed */
  status: string;
  statusDisplayName: string;
  statusReason?: string | null;
  totalRecords: number;
  isActive: boolean;
  settings: PeriodSettings;
  criteriaSetId?: string | null;
  /** Ảnh chụp bộ tiêu chí của kỳ; null nếu chưa chọn. */
  criteria?: CriteriaSnapshot | null;
}

export interface CreatePeriodDto {
  year: number;
  quarter: number;
  name: string;
  startDate: string;
  endDate: string;
  /** Bộ tiêu chí đã xuất bản (bắt buộc). */
  criteriaSetId: string;
}

export interface PeriodParticipantDto {
  recordId: string;
  version: number;
  memberId: string;
  username: string;
  fullName: string;
  departmentId?: string | null;
  departmentName?: string | null;
  partyCellId?: string | null;
  partyCellName?: string | null;
  /** Mã khung tỷ trọng A-B-C-D (ảnh chụp). */
  weightFrameCode: string;
  /** Tên khung theo bộ tiêu chí của kỳ; null nếu khung không có trong bộ. */
  weightFrameName?: string | null;
  /** Khung tỷ trọng đã khóa (hồ sơ đã qua bước tự chấm). */
  weightFrameLocked?: boolean;
  approvalAuthority: string;
  workflowProfileCode: string;
  workflowProfileName: string;
  status: string;
  statusDisplayName: string;
  /** Mã bước đang chờ; null khi đã công bố. */
  currentStep?: string | null;
  currentStepName?: string | null;
  /** yyyy-MM-dd */
  currentStepDeadline?: string | null;
  /** Số ngày quá hạn (≥ 1), tính theo giờ Việt Nam. */
  daysOverdue?: number | null;
  /** Số ngày còn lại tới hạn (0 = hạn hôm nay). */
  daysLeft?: number | null;
}

export interface ParticipantCandidateDto {
  memberId: string;
  username: string;
  fullName: string;
  departmentName?: string | null;
  partyCellName?: string | null;
  alreadyAdded: boolean;
}

export interface AddParticipantsResultDto {
  added: number;
  skipped: string[];
}

/** Công việc / sản phẩm (Mẫu 01, 02) */
export interface EvaluationTaskDto {
  id: string;
  version?: number;
  recordId: string;
  taskOrder: number;
  taskName: string;
  targetOutput: string;
  weight: number;
  deadline: string;
  /** Mã trục kết quả (Mẫu 01). */
  axisCode?: string | null;
  productCode?: string | null;
  /** Null với dữ liệu đăng ký trước khi có cột này. */
  kind?: TaskKind | null;
  role?: TaskRole | null;
  exceedCriteria?: string | null;
  evidenceSource?: string | null;
  confirmer?: string | null;
  /** Việc đã loại trừ / chuyển tiếp trong quý (không chấm); null = còn hiệu lực. */
  changeStatus?: "Excluded" | "CarriedOver" | null;
  /** Tỷ lệ A-B-C-D đã nhập (0..1); null khi chủ hồ sơ chưa nhập. */
  criteriaA_Ratio: number | null;
  criteriaB_Ratio: number | null;
  criteriaC_Ratio: number | null;
  criteriaD_Ratio: number | null;
  /** Đã nhập tỷ lệ A-B-C-D (lưu nháp hoặc nộp). */
  selfScoreEntered?: boolean;
  selfScore: number;
  supervisorScore?: number;
  isExceedStandard: boolean;
  /** Mức thẩm định 1/2/3 (sau khi thẩm định). */
  appraisalLevel?: number | null;
  appraisalComment?: string | null;
  attachmentId?: string | null;
  /** Theo phiên bản minh chứng hiện hành. */
  attachmentFileName?: string | null;
  attachmentOriginalName?: string | null;
}

export interface RecordStepProgressDto {
  step: WorkflowStepCode;
  name: string;
  enabled: boolean;
  mode: StepMode;
  state: "done" | "current" | "pending" | "skipped";
  deadline?: string | null;
  overdue: boolean;
  /** Máy chủ cũ có thể chưa trả. */
  actor?: string | null;
  doneAt?: string | null;
  doneByName?: string | null;
}

export interface EvaluationRecordDto {
  id: string;
  version?: number;
  periodId: string;
  periodName: string;
  periodStatus: string;
  selfScoreForm: string;
  memberId: string;
  fullName: string;
  partyCardNumber?: string;
  positionTitle: string;
  partyCellName?: string;
  partyCellId?: string;
  departmentId?: string;
  departmentName?: string;
  /** Mã khung tỷ trọng A-B-C-D (ảnh chụp). */
  weightFrameCode: string;
  /** Khung gốc trong bộ tiêu chí của kỳ (tỷ trọng 0..1). */
  baseWeightFrame?: WeightFrame | null;
  /** Khung hiệu lực dùng để chấm: khung gốc với tỷ trọng đã điều chỉnh (nếu có). */
  weightFrame?: WeightFrame | null;
  /** Tỷ lệ A-B-C-D đã được điều chỉnh khi duyệt danh mục. */
  weightFrameAdjusted?: boolean;
  /** Biên độ điều chỉnh (điểm phần trăm); null = bộ không cho điều chỉnh. */
  frameAdjustLimit?: number | null;
  approvalAuthority: string;
  workflowProfileCode: string;
  workflowProfileName: string;
  status: string;
  statusDisplayName: string;
  currentStep?: WorkflowStepCode | null;
  returnReason?: string | null;
  /** Hạn mở riêng hồ sơ khi kỳ khóa dữ liệu (null = không mở riêng). */
  unlockedUntil?: string | null;
  /** Đang mở riêng còn hạn và kỳ đang khóa dữ liệu. */
  unlockActive?: boolean;
  unlockedByName?: string | null;
  unlockReason?: string | null;
  progress: RecordStepProgressDto[];

  tasksApprovedByName?: string | null;
  tasksApprovedAt?: string | null;
  tasksApprovalComment?: string | null;

  /** Điểm từng tiêu chí con (khóa = mã tiêu chí con của bộ tiêu chí của kỳ). */
  generalScores: Record<string, GeneralItemScore>;
  generalCriteriaScore: number;
  tasksScore: number;
  /** Điểm theo trục (Mẫu 09B, khóa = mã trục). */
  axisScores?: Record<string, number> | null;
  totalSelfScore: number;
  selfProposedGrade: string;
  selfScoredAt?: string | null;

  partyCellComment: string;
  cellConfirmedByName?: string | null;
  cellConfirmedAt?: string | null;

  collectiveProposedGrade: string;
  collectiveComment?: string | null;
  collectiveMeetingId?: string | null;
  collectiveRecordedByName?: string | null;
  collectiveRecordedAt?: string | null;

  /** Tổng điểm thẩm định = nhóm tiêu chí chung + nhóm kết quả thực hiện nhiệm vụ. */
  appraisalScore?: number;
  /** Điểm thẩm định nhóm tiêu chí chung (null khi chỉ có điểm tổng). */
  appraisalGeneralScore?: number | null;
  /** Điểm thẩm định nhóm kết quả thực hiện nhiệm vụ (null khi chỉ có điểm tổng). */
  appraisalTasksScore?: number | null;
  appraisalComment: string;
  /** Giải trình khi chênh lệch tự chấm – thẩm định. */
  appraisalExplanation?: string | null;
  appraisalProposedGrade: string;
  appraisedByName?: string | null;
  appraisedAt?: string | null;

  directorComment?: string | null;
  directorProposedGrade: string;
  directorReviewedByName?: string | null;
  directorReviewedAt?: string | null;

  finalScore: number;
  finalGrade: string;
  decisionDocumentNumber?: string | null;
  decisionDocumentDate?: string | null;
  decisionAuthorityName?: string | null;
  /** Đề xuất nội dung liên quan về công tác cán bộ (cột 13 Mẫu 14). */
  cadreWorkProposal?: string | null;
  decisionMeetingId?: string | null;
  decisionRecordedByName?: string | null;
  decisionRecordedAt?: string | null;

  publishedByName?: string | null;
  publishedAt?: string | null;

  /** Kết quả ghi nhận của các bước do cấp trên thực hiện. */
  externalResults: ExternalResultDto[];

  tasks: EvaluationTaskDto[];
  /** Cảnh báo điều kiện xếp loại theo mức thẩm định từng nhiệm vụ. */
  gradeWarnings?: string[];
  /** Tỷ lệ nhiệm vụ vượt chuẩn tối thiểu (0..1) của mức Xuất sắc theo bộ tiêu chí; null = không xét. */
  excellentMinExceedRatio?: number | null;

  /** Mẫu 09C — nội dung tự đánh giá theo mã mục của bộ tiêu chí (mục I: phần nhận xét chung). */
  selfAssessment?: Record<string, string>;
  /** Nhiệm vụ (kèm mức) và kết quả theo trục, khóa = mã trục — mục I Mẫu 09C và Mẫu 9D. */
  axisReports?: Record<string, AxisReport>;
  /** Mẫu 09B — tự luận theo trục, khóa = mã trục. */
  axisNotes?: Record<string, AxisNote>;
  /** Nhận xét theo nhóm tiêu chí chung, khóa = mã nhóm (in ở dòng nhóm của phiếu tự chấm). */
  generalGroupNotes?: Record<string, string>;
}

// Biểu mẫu cá nhân: 09C, 9D nhập cùng phiếu tự chấm (theo trục).

/** Ý con "+ …" của một nhiệm vụ (Mẫu 09C). */
export interface AxisTaskItem {
  content: string;
  level?: number | null;
}

/** Một nhiệm vụ của trục (Mẫu 09C): nội dung, tình hình thực hiện và mức tự đánh giá. */
export interface AxisTask {
  content: string;
  level?: number | null;
  items?: AxisTaskItem[];
}

/** Nội dung một trục: các nhiệm vụ (mục I Mẫu 09C) và dòng tóm tắt "Trục n" của Mẫu 9D (cột (3)–(7)). */
export interface AxisReport {
  tasks?: AxisTask[];
  deadline?: string | null;
  status?: string | null;
  product?: string | null;
  progress?: string | null;
  note?: string | null;
}

export interface AxisReportsPrefillResult {
  reports: Record<string, AxisReport>;
  taskCount: number;
  summaryFieldCount: number;
}

const nonEmpty = (value?: string | null) => value?.trim() || "";

const shortDate = (value: string) => {
  const date = value.split("T")[0];
  const parts = date.split("-");
  return parts.length === 3 ? `${parts[2]}/${parts[1]}/${parts[0]}` : value;
};

/** Gợi ý Mẫu 09C/9D từ danh mục Mẫu 01: chỉ điền ô còn trống, giữ nguyên nội dung đã lưu. */
export function prefillAxisReportsFromTasks(
  axes: { code: string }[],
  tasks: Pick<EvaluationTaskDto, "axisCode" | "taskName" | "targetOutput" | "deadline" | "productCode" | "changeStatus">[],
  existing?: Record<string, AxisReport>
): AxisReportsPrefillResult {
  const source = existing ?? {};
  const reports: Record<string, AxisReport> = {};
  let taskCount = 0;
  let summaryFieldCount = 0;
  const activeTasks = tasks.filter((task) => !task.changeStatus && nonEmpty(task.axisCode));

  for (const axis of axes) {
    const existingKey = Object.keys(source).find((key) => key.toLowerCase() === axis.code.toLowerCase());
    const current: AxisReport = { ...(existingKey ? source[existingKey] : undefined) };
    const axisTasks = activeTasks.filter((task) => task.axisCode!.trim().toLowerCase() === axis.code.toLowerCase());

    if ((!current.tasks || current.tasks.length === 0) && axisTasks.length > 0) {
      current.tasks = axisTasks.map((task) => ({
        content: [
          task.productCode?.trim() ? `${task.productCode.trim()} —` : "",
          nonEmpty(task.taskName) || nonEmpty(task.targetOutput),
          nonEmpty(task.targetOutput) ? `Đầu ra/chuẩn đạt: ${task.targetOutput.trim()}` : "",
        ].filter(Boolean).join(" "),
        level: null,
        items: [],
      }));
      taskCount += current.tasks.length;
    }

    if (!nonEmpty(current.deadline) && axisTasks.length > 0) {
      const deadlines = Array.from(new Set(axisTasks.map((task) => shortDate(task.deadline)).filter(Boolean)));
      if (deadlines.length > 0) {
        current.deadline = deadlines.join(", ");
        summaryFieldCount++;
      }
    }

    if (!nonEmpty(current.product) && axisTasks.length > 0) {
      const outputs = Array.from(new Set(axisTasks.map((task) => nonEmpty(task.targetOutput) || nonEmpty(task.taskName)).filter(Boolean)));
      if (outputs.length > 0) {
        current.product = outputs.join("\n");
        summaryFieldCount++;
      }
    }

    reports[axis.code] = current;
  }

  return { reports, taskCount, summaryFieldCount };
}

/** Một dòng nhiệm vụ / ý con của Mẫu 09C: nội dung và câu nối mức (in nghiêng; null khi không kết luận mức). */
export interface AxisTaskLine {
  text: string;
  level: string | null;
}

/** Khối một trục khi in mục I Mẫu 09C. */
export interface AxisTaskBlock {
  title: string;
  lines: AxisTaskLine[];
}

/** Cả dòng như bản in (nội dung + câu nối mức). */
export const axisTaskLineText = (line: AxisTaskLine) => (line.level ? `${line.text} ${line.level}` : line.text);

/** Nội dung tự luận của một trục trên Mẫu 09B. */
export interface AxisNote {
  target?: string | null;
  result?: string | null;
  note?: string | null;
}

/** Một mục tự luận của Mẫu 09C (khai báo trong bộ tiêu chí). */
export interface SelfAssessmentSection {
  code: string;
  title: string;
  guidance?: string | null;
  note?: string | null;
  maxLength: number;
  required: boolean;
}

/** Một mức tự đánh giá của nhiệm vụ (khai báo trong bộ tiêu chí). */
export interface TaskLevel {
  code: number;
  label: string;
}

/** Cấu hình biểu mẫu cá nhân trong nội dung bộ tiêu chí. */
export interface IndividualFormConfig {
  requiredForms?: string[];
  selfAssessmentSections?: SelfAssessmentSection[];
  taskLevels?: TaskLevel[];
  taskLineLabel?: string | null;
  taskLevelPhrase?: string | null;
}

/** Biểu mẫu xuất được cho hồ sơ (`GET /reports/docx/record/{recordId}`). */
export interface RecordFormDto {
  code: string;
  name: string;
}

/** Khớp giới hạn kiểm tra phía máy chủ. */
export const AXIS_REPORT_LIMITS = { tasks: 30, items: 20, long: 2000, short: 500, axisNote: 4000 } as const;

/** Bộ tiêu chí áp dụng biểu mẫu `code` (mẫu tự chấm theo `selfScoreForm` của bộ). */
export function criteriaAppliesForm(criteria: { selfScoreForm: string; content: unknown } | null | undefined, code: string): boolean {
  if (!criteria) return false;
  if (code === "09A" || code === "09B") return criteria.selfScoreForm === code;
  const forms = (criteria.content as IndividualFormConfig).requiredForms ?? [];
  return forms.includes(code);
}

/** Mục Mẫu 09C của bộ tiêu chí (rỗng nếu bộ không khai báo). */
export function selfAssessmentSections(criteria: { content: unknown } | null | undefined): SelfAssessmentSection[] {
  return (criteria?.content as IndividualFormConfig | undefined)?.selfAssessmentSections ?? [];
}

/** Các mức tự đánh giá nhiệm vụ của bộ tiêu chí (rỗng nếu bộ không dùng mức). */
export function taskLevels(criteria: { content: unknown } | null | undefined): TaskLevel[] {
  return (criteria?.content as IndividualFormConfig | undefined)?.taskLevels ?? [];
}

const endSentence = (value: string) => {
  const t = value.trim().replace(/\r\n?/g, "\n");
  return t.length === 0 || ".!?;:".includes(t[t.length - 1]) ? t : `${t}.`;
};

/** Dựng nhiệm vụ theo trục của Mẫu 09C giống bản in phía máy chủ; câu nối mức tách riêng để in nghiêng. */
export function buildAxisTaskBlocks(
  axes: { code: string; name: string }[],
  reports: Record<string, AxisReport> | undefined,
  criteria: { content: unknown } | null | undefined
): AxisTaskBlock[] {
  const cfg = (criteria?.content as IndividualFormConfig | undefined) ?? {};
  const levels = cfg.taskLevels ?? [];
  const prefix = cfg.taskLineLabel?.trim() || "{n}.";
  const phrase = (level?: number | null) =>
    level != null && levels.some((l) => l.code === level) && cfg.taskLevelPhrase?.trim()
      ? cfg.taskLevelPhrase.trim().split("{level}").join(String(level))
      : null;
  const find = (code: string) => Object.entries(reports ?? {}).find(([k]) => k.toLowerCase() === code.toLowerCase())?.[1];
  const blocks: AxisTaskBlock[] = [];
  axes.forEach((axis, i) => {
    const tasks = (find(axis.code)?.tasks ?? []).filter((t) => t.content?.trim());
    if (tasks.length === 0) return;
    const lines: AxisTaskLine[] = [];
    tasks.forEach((task, k) => {
      lines.push({ text: `${prefix.split("{n}").join(String(k + 1))} ${endSentence(task.content)}`, level: phrase(task.level) });
      (task.items ?? [])
        .filter((x) => x.content?.trim())
        .forEach((x) => lines.push({ text: `+ ${endSentence(x.content)}`, level: phrase(x.level) }));
    });
    blocks.push({ title: `${i + 1}. ${axis.name} (trục ${i + 1})`, lines });
  });
  return blocks;
}

/** Số ký tự phần nhiệm vụ theo trục khi in (tính vào giới hạn mục I Mẫu 09C, giống máy chủ). */
export function axisTaskTextLength(blocks: AxisTaskBlock[]): number {
  return blocks.reduce((sum, b) => sum + b.title.length + 1 + b.lines.reduce((s, l) => s + axisTaskLineText(l).length + 1, 0), 0);
}

/** Kết quả của một bước do cấp trên thực hiện (đã ghi nhận). */
export interface ExternalResultDto {
  step: WorkflowStepCode;
  stepName: string;
  authorityName: string;
  documentNumber?: string | null;
  documentDate?: string | null;
  comment?: string | null;
  grade: string;
  score?: number | null;
  attachmentId?: string | null;
  recordedByName?: string | null;
  recordedAt: string;
}

/** Cảnh báo kẹt luồng. */
export interface ReadinessIssueDto {
  recordId: string;
  memberId: string;
  fullName: string;
  workflowProfileCode: string;
  workflowProfileName: string;
  step: WorkflowStepCode;
  stepName: string;
  mode: StepMode;
  permission: string;
  permissionName: string;
  /** Vai trò đang chứa quyền cần có. */
  roleNames: string[];
  scope: string;
  /** Tên ngắn nơi cần gán (gán ở cấp trên hoặc Toàn công ty cũng được). */
  scopeName: string;
  departmentId?: string | null;
  partyCellId?: string | null;
  message: string;
}

export interface PeriodReadinessDto {
  periodId: string;
  ready: boolean;
  checkedRecords: number;
  issues: ReadinessIssueDto[];
  /** Rà soát dữ liệu trước khi mở kỳ (không chặn mở kỳ). */
  dataReview?: DataReviewItemDto[];
}

export interface DataReviewItemDto {
  kind: string;
  title: string;
  hint: string;
  names: string[];
}

/** Mã quyền chọn được làm quyền thực hiện bước. */
export interface StepPermissionOptionDto {
  code: string;
  name: string;
  description: string;
}

export interface BulkChangeProfileResultDto {
  updated: number;
  skipped: string[];
}

export interface EvaluationRecordHistoryDto {
  id: string;
  recordId: string;
  fromStatus?: string | null;
  fromStatusName?: string | null;
  toStatus: string;
  toStatusName: string;
  step?: string | null;
  stepName?: string | null;
  action: string;
  actionName: string;
  reason?: string | null;
  scoreBefore?: number | null;
  scoreAfter?: number | null;
  gradeBefore?: string | null;
  gradeAfter?: string | null;
  actorId?: string | null;
  actorName: string;
  comment?: string | null;
  createdAt: string;
}

export interface RecordActionDto {
  action: WorkflowActionCode;
  step: WorkflowStepCode;
  label: string;
  requiresReason: boolean;
  reasonOptional: boolean;
  overdue: boolean;
  targetSteps?: WorkflowStepCode[] | null;
  /** Phải nhập hạn mở riêng hồ sơ (kỳ khóa dữ liệu, hồ sơ quay về bước trước thẩm định). */
  requiresUnlockDeadline?: boolean;
  /** Với mở lại: các bước quay về cần hạn mở riêng. */
  unlockDeadlineSteps?: WorkflowStepCode[] | null;
  /** Hạn hiện tại khi hồ sơ đang được mở riêng. */
  currentUnlockedUntil?: string | null;
  defaultUnlockDays?: number | null;
  maxUnlockDays?: number | null;
}

export interface RecordActionsDto {
  recordId: string;
  version: number;
  status: string;
  actions: RecordActionDto[];
}

export interface WorkQueueItemDto {
  recordId: string;
  version: number;
  periodId: string;
  periodName: string;
  memberId: string;
  fullName: string;
  departmentName?: string | null;
  partyCellName?: string | null;
  approvalAuthority: string;
  workflowProfileName: string;
  status: string;
  statusDisplayName: string;
  mode: StepMode;
  isOwnRecord: boolean;
  returnReason?: string | null;
  deadline?: string | null;
  overdue: boolean;
}

export interface WorkQueueGroupDto {
  step: WorkflowStepCode;
  stepName: string;
  count: number;
  items: WorkQueueItemDto[];
}

export interface WorkQueueDto {
  total: number;
  groups: WorkQueueGroupDto[];
}

/** Dữ liệu một nhiệm vụ khi đăng ký (Mẫu 01) */
export type TaskKind = "TrongTam" | "ThuongXuyen" | "DotXuat";
export type TaskRole = "ChuTri" | "PhoiHop";

export const TASK_KIND_LABELS: Record<TaskKind, string> = { TrongTam: "Trọng tâm", ThuongXuyen: "Thường xuyên", DotXuat: "Đột xuất" };
export const TASK_ROLE_LABELS: Record<TaskRole, string> = { ChuTri: "Chủ trì", PhoiHop: "Phối hợp" };

export const APPRAISAL_LEVELS: { value: number; label: string; hint: string }[] = [
  { value: 1, label: "Mức 1", hint: "Chủ động, có kết quả cụ thể, chất lượng cao" },
  { value: 2, label: "Mức 2", hint: "Cơ bản đáp ứng, còn hạn chế" },
  { value: 3, label: "Mức 3", hint: "Không đáp ứng" },
];

const GRADE_CONDITION_NAMES: Record<string, string> = {
  HoanThanhXuatSac: "Hoàn thành xuất sắc nhiệm vụ",
  KhongHoanThanh: "Không hoàn thành nhiệm vụ",
};

/** Cùng quy tắc với `GradeConditions` của máy chủ, để báo ngay khi đổi mức; chỉ cảnh báo, không chặn. */
export function gradeConditionWarnings(
  tasks: { kind?: TaskKind | null; appraisalLevel?: number | null; isExceedStandard: boolean }[],
  grade: string,
  minExceedRatio: number | null | undefined
): string[] {
  const warnings: string[] = [];
  if (tasks.length === 0 || !grade || grade === "ChuaXepLoai") return warnings;
  const focus = tasks.filter((t) => t.kind === "TrongTam");
  const level3 = focus.filter((t) => t.appraisalLevel === 3).length;
  const level2 = focus.filter((t) => t.appraisalLevel === 2).length;
  if (level3 > 0 && grade !== "KhongHoanThanh")
    warnings.push(`Có ${level3} nhiệm vụ trọng tâm xếp Mức 3: chỉ xếp được "${GRADE_CONDITION_NAMES.KhongHoanThanh}".`);
  if (grade === "HoanThanhXuatSac") {
    if (level2 > 0) warnings.push(`Có ${level2} nhiệm vụ trọng tâm xếp Mức 2: chưa đủ điều kiện "${GRADE_CONDITION_NAMES.HoanThanhXuatSac}".`);
    if (minExceedRatio != null && minExceedRatio > 0) {
      const exceed = tasks.filter((t) => t.isExceedStandard).length;
      const ratio = exceed / tasks.length;
      const pct = (r: number) => `${String(Math.round(r * 1000) / 10).replace(".", ",")}%`;
      if (ratio + 1e-6 < minExceedRatio)
        warnings.push(`Mới có ${exceed}/${tasks.length} nhiệm vụ vượt chuẩn (${pct(ratio)}), cần từ ${pct(minExceedRatio)}: chưa đủ điều kiện "${GRADE_CONDITION_NAMES.HoanThanhXuatSac}".`);
    }
  }
  return warnings;
}

/** Một dòng Mẫu 01 khi đăng ký: mọi cột bắt buộc trừ mã SP và thời hạn (trống = ngày cuối kỳ). */
export interface TaskInputDto {
  taskName: string;
  /** Chuẩn đạt. */
  targetOutput: string;
  weight: number;
  deadline?: string | null;
  attachmentId?: string | null;
  /** Mã trục kết quả thuộc bộ tiêu chí của kỳ. */
  axisCode?: string | null;
  productCode?: string | null;
  /** Khi đăng ký chỉ "TrongTam" hoặc "ThuongXuyen". */
  kind?: TaskKind | "";
  role?: TaskRole | "";
  exceedCriteria?: string;
  evidenceSource?: string;
  confirmer?: string;
}

export interface TaskScoreInputDto {
  taskId: string;
  criteriaA_Ratio: number;
  criteriaB_Ratio: number;
  criteriaC_Ratio: number;
  criteriaD_Ratio: number;
  isExceedStandard: boolean;
  attachmentId?: string | null;
}

export interface VoteTallyDto {
  votesExcellent: number;
  votesGood: number;
  votesSatisfactory: number;
  votesUnsatisfactory: number;
  /** Phiếu "Chưa đánh giá, xếp loại" (cột 8 Mẫu 13). */
  votesNotRated: number;
  invalidVotes: number;
  notes?: string;
}

/** Trần Hoàn thành xuất sắc của một nhóm chức danh trong một cấp quyết định. */
export interface ExcellentQuotaGroupDto {
  approvalAuthority: "CoSo" | "CapTren";
  authorityName: string;
  statCode?: string | null;
  groupName: string;
  totalCadres: number;
  /** Người đứng đầu — không tính vào trần. */
  headCount: number;
  goodCount: number;
  excellentCount: number;
  maxExcellentAllowed: number;
  excellentPercentOfGood?: number | null;
  isExceedingQuota: boolean;
  excessCount: number;
}

/** Kiểm soát trần Hoàn thành xuất sắc của kỳ (chỉ cảnh báo). */
export interface ExcellentQuotaCheckDto {
  periodId: string;
  /** Tỷ lệ trần (0,20..0,25). */
  ratio: number;
  partyCellId?: string | null;
  /** false = số liệu chỉ trong phạm vi được xem/được chọn. */
  isFullScope: boolean;
  exceedingGroupCount: number;
  groups: ExcellentQuotaGroupDto[];
}

export interface CollectiveEvaluationItemDto {
  id?: string;
  itemOrder: number;
  category: string;
  taskName: string;
  planOrDirection: string;
  result: string;
  limitations: string;
  notes: string;
}

export interface CollectiveEvaluationRecordDto {
  id: string;
  version?: number;
  periodId: string;
  form: string;
  partyCellId?: string;
  partyCellName?: string;
  departmentId?: string;
  departmentName?: string;
  headId?: string;
  headName?: string;
  subjectName: string;
  strengths: string;
  limitations: string;
  causes: string;
  previousRemediation: string;
  explanation: string;
  responsibilities: string;
  remediationPlan: string;
  generalCriteriaScore: number;
  taskCriteriaScore: number;
  totalScore: number;
  selfProposedGrade: string;
  status: string;
  items: CollectiveEvaluationItemDto[];
}

export interface SaveCollectiveEvaluationRequestDto {
  version?: number;
  periodId: string;
  form: string;
  partyCellId?: string;
  departmentId?: string;
  headId?: string;
  subjectName: string;
  strengths: string;
  limitations: string;
  causes: string;
  previousRemediation: string;
  explanation: string;
  responsibilities: string;
  remediationPlan: string;
  generalCriteriaScore: number;
  taskCriteriaScore: number;
  selfProposedGrade: string;
  items: CollectiveEvaluationItemDto[];
}

export interface EvaluationMeetingVoteSummaryDto {
  id?: string;
  recordId: string;
  fullName?: string;
  votesExcellent: number;
  votesGood: number;
  votesSatisfactory: number;
  votesUnsatisfactory: number;
  /** Phiếu "Chưa đánh giá, xếp loại" (cột 8 Mẫu 13). */
  votesNotRated: number;
  invalidVotes: number;
  notes: string;
}

export interface EvaluationMeetingDto {
  id: string;
  version?: number;
  periodId: string;
  partyCellId?: string | null;
  partyCellName?: string | null;
  departmentId?: string | null;
  departmentName?: string | null;
  /** B3A_COLLECTIVE / B4_DECISION */
  stage?: string | null;
  formCode: string;
  meetingType: string;
  location: string;
  startedAt: string;
  endedAt?: string;
  invitedCount: number;
  presentCount: number;
  absentCount: number;
  absentReasons: string;
  chairId?: string;
  chairName: string;
  secretaryId?: string;
  secretaryName: string;
  minutesContent: string;
  outcomeContent: string;
  voteCountingContent: string;
  voteSummaries: EvaluationMeetingVoteSummaryDto[];
}

export interface SaveEvaluationMeetingRequestDto {
  version?: number;
  periodId: string;
  partyCellId?: string;
  departmentId?: string;
  stage?: string;
  formCode: string;
  meetingType: string;
  location: string;
  startedAt: string;
  endedAt?: string;
  invitedCount: number;
  presentCount: number;
  absentCount: number;
  absentReasons: string;
  chairId?: string;
  chairName: string;
  secretaryId?: string;
  secretaryName: string;
  minutesContent: string;
  outcomeContent: string;
  voteCountingContent: string;
  voteSummaries: EvaluationMeetingVoteSummaryDto[];
}

export const GRADE_OPTIONS: { value: string; label: string }[] = [
  { value: "HoanThanhXuatSac", label: "Hoàn thành xuất sắc nhiệm vụ" },
  { value: "HoanThanhTot", label: "Hoàn thành tốt nhiệm vụ" },
  { value: "HoanThanh", label: "Hoàn thành nhiệm vụ" },
  { value: "KhongHoanThanh", label: "Không hoàn thành nhiệm vụ" },
  { value: "ChuaXepLoai", label: "Chưa xếp loại" },
];

export function gradeLabel(value?: string | null): string {
  if (!value || value === "ChuaXepLoai") return "—";
  return GRADE_OPTIONS.find((g) => g.value === value)?.label || value;
}

export const STEP_NAMES: Record<WorkflowStepCode, string> = {
  B1_REGISTER: "Đăng ký sản phẩm, nhiệm vụ",
  B1_APPROVE: "Duyệt danh mục sản phẩm",
  B2_SELF_SCORE: "Tự chấm điểm, đề xuất mức",
  B2_CELL_CONFIRM: "Chi bộ xác nhận phiếu tự chấm",
  B3A_COLLECTIVE: "Đề xuất của tập thể lãnh đạo",
  B3B_APPRAISAL: "Thẩm định",
  B3C_DIRECTOR: "Nhận xét của cấp trực tiếp sử dụng",
  B4_DECISION: "Quyết định mức xếp loại",
  B5_PUBLISH: "Công bố, khóa kết quả",
};

/** Bước bắt buộc (không đặt "Không áp dụng") — chỉ để khóa ô chọn; máy chủ kiểm tra lại. */
export const MANDATORY_STEPS: WorkflowStepCode[] = ["B2_SELF_SCORE", "B4_DECISION", "B5_PUBLISH"];

/** Bước không giao cho cấp trên thực hiện (bước của chủ hồ sơ, công bố) — chỉ để khóa ô chọn. */
export const NEVER_EXTERNAL_STEPS: WorkflowStepCode[] = ["B1_REGISTER", "B2_SELF_SCORE", "B5_PUBLISH"];

/** Bước của chủ hồ sơ (quyền thực hiện luôn là "Tham gia đánh giá (bản thân)"). */
export const OWNER_STEPS: WorkflowStepCode[] = ["B1_REGISTER", "B2_SELF_SCORE"];

/** Bước có mức xếp loại (ghi nhận kết quả của cấp trên bắt buộc chọn mức). */
export const GRADED_STEPS: WorkflowStepCode[] = ["B3A_COLLECTIVE", "B3B_APPRAISAL", "B3C_DIRECTOR", "B4_DECISION"];

/** Quyền thực hiện mặc định của bước nội bộ (giống máy chủ) — dùng khi chuyển chế độ về "Nội bộ" trên giao diện. */
export const DEFAULT_STEP_PERMISSIONS: Record<WorkflowStepCode, string> = {
  B1_REGISTER: "evaluation.self",
  B1_APPROVE: "evaluation.tasks.approve",
  B2_SELF_SCORE: "evaluation.self",
  B2_CELL_CONFIRM: "evaluation.cell.confirm",
  B3A_COLLECTIVE: "evaluation.collective.record",
  B3B_APPRAISAL: "evaluation.appraise",
  B3C_DIRECTOR: "evaluation.director.review",
  B4_DECISION: "evaluation.decide",
  B5_PUBLISH: "evaluation.publish",
};

export const STEP_MODE_LABELS: Record<StepMode, string> = {
  Internal: "Nội bộ",
  External: "Cấp trên thực hiện",
  Off: "Không áp dụng",
};

export const STEP_ORDER: WorkflowStepCode[] = [
  "B1_REGISTER",
  "B1_APPROVE",
  "B2_SELF_SCORE",
  "B2_CELL_CONFIRM",
  "B3A_COLLECTIVE",
  "B3B_APPRAISAL",
  "B3C_DIRECTOR",
  "B4_DECISION",
  "B5_PUBLISH",
];

// Lệnh ghi gửi `version` đã đọc; 409 → phát sự kiện để giao diện đề nghị tải lại.

export const CONCURRENCY_CONFLICT_MESSAGE =
  "Dữ liệu đã được người khác cập nhật. Vui lòng tải lại để xem dữ liệu mới nhất — nội dung bạn đang nhập vẫn được giữ nguyên.";

/** Sự kiện `window` phát ra khi lệnh cập nhật bị từ chối (409). */
export const EVALUATION_CONFLICT_EVENT = "evaluations:conflict";

export interface EvaluationConflictDetail {
  operation: string;
  message: string;
}

export function isConcurrencyConflict(error: unknown): boolean {
  return error instanceof ApiError && error.status === 409;
}

async function withConflictHandling<T>(operation: string, action: () => Promise<T>): Promise<T> {
  try {
    return await action();
  } catch (error) {
    if (isConcurrencyConflict(error)) {
      const apiError = error as ApiError;
      const message = apiError.message || CONCURRENCY_CONFLICT_MESSAGE;
      if (typeof window !== "undefined") {
        window.dispatchEvent(
          new CustomEvent<EvaluationConflictDetail>(EVALUATION_CONFLICT_EVENT, { detail: { operation, message } })
        );
      }
      throw new ApiError(message, 409, apiError.errors, apiError.data);
    }
    throw error;
  }
}

function post<T>(endpoint: string, body: unknown): Promise<T> {
  return request<T>(endpoint, { method: "POST", body: JSON.stringify(body ?? {}) });
}

const ACTION_PATH: Record<WorkflowActionCode, string> = {
  SubmitTasks: "tasks/submit",
  ApproveTasks: "tasks/approve",
  ReturnTasks: "tasks/return",
  RevokeTaskApproval: "tasks/approve/revoke",
  SubmitSelfScore: "self-score/submit",
  ConfirmByCell: "cell/confirm",
  ReturnByCell: "cell/return",
  RecordCollectiveProposal: "collective",
  Appraise: "appraisal",
  ReturnByAppraiser: "appraisal/return",
  DirectorReview: "director-review",
  RecordDecision: "decision",
  Publish: "publish",
  Reopen: "reopen",
  ExtendUnlock: "unlock/extend",
  RecordExternal: "external",
};

export const evaluationService = {
  // ----- Kỳ đánh giá -----
  async getPeriods(): Promise<EvaluationPeriodDto[]> {
    return request<EvaluationPeriodDto[]>("/evaluations/periods");
  },

  async getActivePeriod(): Promise<EvaluationPeriodDto | null> {
    return request<EvaluationPeriodDto | null>("/evaluations/periods/active");
  },

  async getPeriod(id: string): Promise<EvaluationPeriodDto> {
    return request<EvaluationPeriodDto>(`/evaluations/periods/${id}`);
  },

  async createPeriod(dto: CreatePeriodDto): Promise<EvaluationPeriodDto> {
    return post<EvaluationPeriodDto>("/evaluations/periods", dto);
  },

  async updatePeriod(
    id: string,
    version: number | undefined,
    changes: { name?: string; startDate?: string; endDate?: string; settings?: PeriodSettings; applyDeadlineRules?: boolean; criteriaSetId?: string }
  ): Promise<EvaluationPeriodDto> {
    return withConflictHandling("updatePeriod", () =>
      request<EvaluationPeriodDto>(`/evaluations/periods/${id}`, {
        method: "PUT",
        body: JSON.stringify({ version, ...changes }),
      })
    );
  },

  /** Mở kỳ còn kẹt luồng → 409 (`ApiError.data` = kết quả kiểm tra); mở bắt buộc: `force` kèm lý do. */
  async transitionPeriod(
    id: string,
    action: "open" | "lock" | "unlock" | "close",
    version: number | undefined,
    reason?: string,
    force?: boolean
  ): Promise<EvaluationPeriodDto> {
    // Mở kỳ không qua withConflictHandling: 409 do kẹt luồng không phải xung đột phiên bản.
    if (action === "open")
      return post<EvaluationPeriodDto>(`/evaluations/periods/${id}/open`, { version, reason, force: !!force });
    return withConflictHandling(`period:${action}`, () =>
      post<EvaluationPeriodDto>(`/evaluations/periods/${id}/${action}`, { version, reason })
    );
  },

  /** Kiểm tra kẹt luồng: hồ sơ nào sẽ kẹt ở bước nào vì không ai có quyền trong phạm vi. */
  async getReadiness(periodId: string): Promise<PeriodReadinessDto> {
    return request<PeriodReadinessDto>(`/evaluations/periods/${periodId}/readiness`);
  },

  async getStepPermissions(): Promise<StepPermissionOptionDto[]> {
    return request<StepPermissionOptionDto[]>("/evaluations/periods/step-permissions");
  },

  /** Đổi hồ sơ luồng của một người được đánh giá (bắt buộc lý do). */
  async changeProfile(
    periodId: string,
    recordId: string,
    payload: { version: number; workflowProfileCode: string; reason: string }
  ): Promise<PeriodParticipantDto> {
    return withConflictHandling("changeProfile", () =>
      request<PeriodParticipantDto>(`/evaluations/periods/${periodId}/participants/${recordId}/profile`, {
        method: "PUT",
        body: JSON.stringify(payload),
      })
    );
  },

  /** Đổi hồ sơ luồng hàng loạt; hồ sơ không đổi được trả về trong `skipped` kèm lý do. */
  async bulkChangeProfile(
    periodId: string,
    payload: { items: { recordId: string; version: number }[]; workflowProfileCode: string; reason: string }
  ): Promise<BulkChangeProfileResultDto> {
    return request<BulkChangeProfileResultDto>(`/evaluations/periods/${periodId}/participants/profile`, {
      method: "PUT",
      body: JSON.stringify(payload),
    });
  },

  async getParticipants(periodId: string): Promise<PeriodParticipantDto[]> {
    return request<PeriodParticipantDto[]>(`/evaluations/periods/${periodId}/participants`);
  },

  async getCandidates(
    periodId: string,
    filter: { departmentId?: string; partyCellId?: string; q?: string }
  ): Promise<ParticipantCandidateDto[]> {
    const params = new URLSearchParams();
    if (filter.departmentId) params.set("departmentId", filter.departmentId);
    if (filter.partyCellId) params.set("partyCellId", filter.partyCellId);
    if (filter.q) params.set("q", filter.q);
    const query = params.toString();
    return request<ParticipantCandidateDto[]>(`/evaluations/periods/${periodId}/candidates${query ? `?${query}` : ""}`);
  },

  async addParticipants(
    periodId: string,
    payload: { memberIds?: string[]; departmentId?: string; partyCellId?: string; workflowProfileCode?: string }
  ): Promise<AddParticipantsResultDto> {
    return post<AddParticipantsResultDto>(`/evaluations/periods/${periodId}/participants`, {
      memberIds: payload.memberIds ?? [],
      departmentId: payload.departmentId || undefined,
      partyCellId: payload.partyCellId || undefined,
      workflowProfileCode: payload.workflowProfileCode || undefined,
    });
  },

  async removeParticipant(periodId: string, recordId: string, version: number): Promise<void> {
    await withConflictHandling("removeParticipant", () =>
      request(`/evaluations/periods/${periodId}/participants/${recordId}?version=${version}`, { method: "DELETE" })
    );
  },

  async updateSnapshot(
    periodId: string,
    recordId: string,
    payload: {
      version: number;
      departmentId?: string | null;
      partyCellId?: string | null;
      weightFrameCode?: string;
      approvalAuthority?: string;
      reason: string;
    }
  ): Promise<PeriodParticipantDto> {
    return withConflictHandling("updateSnapshot", () =>
      request<PeriodParticipantDto>(`/evaluations/periods/${periodId}/participants/${recordId}/snapshot`, {
        method: "PUT",
        body: JSON.stringify(payload),
      })
    );
  },

  // ----- Hồ sơ -----
  async getMyRecord(periodId: string): Promise<EvaluationRecordDto | null> {
    return request<EvaluationRecordDto | null>(`/evaluations/my-record?periodId=${periodId}`);
  },

  async getRecordById(id: string): Promise<EvaluationRecordDto> {
    return request<EvaluationRecordDto>(`/evaluations/records/${id}`);
  },

  async getRecordHistory(id: string): Promise<EvaluationRecordHistoryDto[]> {
    return request<EvaluationRecordHistoryDto[]>(`/evaluations/records/${id}/history`);
  },

  async getRecordActions(id: string): Promise<RecordActionsDto> {
    return request<RecordActionsDto>(`/evaluations/records/${id}/actions`);
  },

  async getWorkQueue(periodId?: string): Promise<WorkQueueDto> {
    return request<WorkQueueDto>(`/evaluations/work-queue${periodId ? `?periodId=${periodId}` : ""}`);
  },

  async getRecordsByPeriod(periodId: string): Promise<EvaluationRecordDto[]> {
    return request<EvaluationRecordDto[]>(`/evaluations/records?periodId=${periodId}`);
  },

  async getRecordsByBranch(periodId: string, branchId?: string): Promise<EvaluationRecordDto[]> {
    const query = branchId ? `periodId=${periodId}&branchId=${branchId}` : `periodId=${periodId}`;
    return request<EvaluationRecordDto[]>(`/evaluations/branch-records?${query}`);
  },

  /** Trần Hoàn thành xuất sắc theo nhóm chức danh trong từng cấp quyết định (hồ sơ trong phạm vi được xem). */
  async checkExcellentQuotas(periodId: string, partyCellId?: string): Promise<ExcellentQuotaCheckDto> {
    const query = partyCellId ? `periodId=${periodId}&partyCellId=${partyCellId}` : `periodId=${periodId}`;
    return request<ExcellentQuotaCheckDto>(`/evaluations/excellent-quotas?${query}`);
  },

  /** `RecordExternal` cần `step`. Sau lưu nháp, trang gửi phiên bản mới trong `payload.version` — ưu tiên hơn `version`. */
  async performAction(
    recordId: string,
    action: WorkflowActionCode,
    version: number,
    payload: Record<string, unknown> = {},
    step?: WorkflowStepCode
  ): Promise<EvaluationRecordDto> {
    const path = action === "RecordExternal" ? `${ACTION_PATH[action]}/${step}` : ACTION_PATH[action];
    const body = { ...payload, version: typeof payload.version === "number" ? payload.version : version };
    return withConflictHandling(action, () => post<EvaluationRecordDto>(`/evaluations/records/${recordId}/${path}`, body));
  },

  /** Hồ sơ giữ nguyên bước; trả hồ sơ với phiên bản mới. */
  async saveSelfScoreDraft(recordId: string, version: number, payload: Record<string, unknown>): Promise<EvaluationRecordDto> {
    return withConflictHandling("SaveSelfScoreDraft", () =>
      post<EvaluationRecordDto>(`/evaluations/records/${recordId}/self-score/draft`, { ...payload, version })
    );
  },

  // ----- Biểu mẫu cá nhân của hồ sơ -----
  async getRecordForms(recordId: string): Promise<RecordFormDto[]> {
    return request<RecordFormDto[]>(`/reports/docx/record/${recordId}`);
  },

  /** Đường dẫn API xuất một biểu mẫu của hồ sơ (dùng với `reportService.downloadReport`, định dạng docx/pdf). */
  recordFormEndpoint(recordId: string, code: string): string {
    return `/reports/docx/record/${recordId}/${encodeURIComponent(code)}`;
  },

  // ----- Hồ sơ tập thể, biên bản -----
  async getCollectiveRecords(periodId: string, form?: string): Promise<CollectiveEvaluationRecordDto[]> {
    const query = form ? `periodId=${periodId}&form=${form}` : `periodId=${periodId}`;
    return request<CollectiveEvaluationRecordDto[]>(`/evaluations/collective-records?${query}`);
  },

  async createCollectiveRecord(dto: SaveCollectiveEvaluationRequestDto): Promise<CollectiveEvaluationRecordDto> {
    return post<CollectiveEvaluationRecordDto>("/evaluations/collective-records", dto);
  },

  async getMeetings(periodId: string, partyCellId?: string): Promise<EvaluationMeetingDto[]> {
    const query = partyCellId ? `periodId=${periodId}&partyCellId=${partyCellId}` : `periodId=${periodId}`;
    return request<EvaluationMeetingDto[]>(`/evaluations/meetings?${query}`);
  },

  async createMeeting(dto: SaveEvaluationMeetingRequestDto): Promise<EvaluationMeetingDto> {
    return post<EvaluationMeetingDto>("/evaluations/meetings", dto);
  },
};
