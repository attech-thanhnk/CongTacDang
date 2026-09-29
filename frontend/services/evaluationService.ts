import { ApiError, request } from "./apiClient";
import type { CriteriaSnapshot, GeneralItemScore } from "./criteriaService";

// ---------------------------------------------------------------------------
// Kiểu dữ liệu — luồng đánh giá 9 bước theo cấu hình kỳ.
// Frontend không tự suy luật: bước/nút hiển thị theo API `actions` và `work-queue`,
// tiến trình theo trường `progress` do máy chủ tính.
// ---------------------------------------------------------------------------

/** Mã bước (cố định trong code, theo docs/thiet-ke/luong-danh-gia.md). */
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
  /** Ghi nhận kết quả của bước do cấp trên thực hiện (bước ở trường `step`). */
  | "RecordExternal";

/** Chế độ thực hiện một bước trong hồ sơ luồng. */
export type StepMode = "Internal" | "External" | "Off";

/** Cấu hình một bước trong hồ sơ luồng. */
export interface ProfileStepSetting {
  mode: StepMode;
  /** Mã quyền thực hiện khi `Internal` (bước của chủ hồ sơ luôn là evaluation.self; chế độ khác: null). */
  permission?: string | null;
  deadline?: string | null;
}

/** Hồ sơ luồng: cấu hình bước cho một nhóm đối tượng (ví dụ Diện Đảng ủy cơ sở). */
export interface WorkflowProfile {
  code: string;
  name: string;
  description?: string | null;
  steps: Record<string, ProfileStepSetting>;
}

/**
 * Cấu hình kỳ (cột jsonb, schema 2): các hồ sơ luồng + hồ sơ luồng mặc định theo cấp quyết định. Mẫu tự chấm, tiêu chí và
 * tham số chấm điểm thuộc bộ tiêu chí của kỳ (`EvaluationPeriodDto.criteria`, task 16).
 */
export interface PeriodSettings {
  schemaVersion: number;
  profiles: WorkflowProfile[];
  /** Khóa: "CoSo" | "CapTren" → mã hồ sơ luồng. */
  defaultProfiles: Record<string, string>;
  enforceDeadlines: boolean;
}

/** Thông tin kỳ đánh giá */
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
  /** Bộ tiêu chí đã chọn. */
  criteriaSetId?: string | null;
  /** Ảnh chụp bộ tiêu chí của kỳ (null nếu chưa chọn) — form tự chấm/thẩm định dựng từ đây. */
  criteria?: CriteriaSnapshot | null;
}

export interface PeriodPresetDto {
  code: string;
  name: string;
  description: string;
  /** Mẫu tự chấm gợi ý (09A/09B) — chọn sẵn bộ tiêu chí đã xuất bản mới nhất có mẫu này. */
  suggestedForm: string;
  settings: PeriodSettings;
}

export interface CreatePeriodDto {
  year: number;
  quarter: number;
  name: string;
  startDate: string;
  endDate: string;
  preset: string;
  /** Bộ tiêu chí đã xuất bản; bỏ trống → bộ mới nhất theo mẫu gợi ý của kiểu kỳ. */
  criteriaSetId?: string | null;
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
  approvalAuthority: string;
  workflowProfileCode: string;
  workflowProfileName: string;
  status: string;
  statusDisplayName: string;
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
  criteriaA_Ratio: number;
  criteriaB_Ratio: number;
  criteriaC_Ratio: number;
  criteriaD_Ratio: number;
  selfScore: number;
  supervisorScore?: number;
  isExceedStandard: boolean;
  attachmentId?: string | null;
  /** Tên tệp minh chứng theo phiên bản hiện hành (T-53). */
  attachmentFileName?: string | null;
  attachmentOriginalName?: string | null;
}

export interface RecordStepProgressDto {
  step: WorkflowStepCode;
  name: string;
  enabled: boolean;
  /** Chế độ của bước trong hồ sơ luồng của hồ sơ. */
  mode: StepMode;
  /** done | current | pending | skipped */
  state: "done" | "current" | "pending" | "skipped";
  deadline?: string | null;
  overdue: boolean;
}

/** Hồ sơ đánh giá cá nhân */
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
  approvalAuthority: string;
  workflowProfileCode: string;
  workflowProfileName: string;
  status: string;
  statusDisplayName: string;
  currentStep?: WorkflowStepCode | null;
  returnReason?: string | null;
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

  /** Ý kiến xác nhận của Chi bộ */
  partyCellComment: string;
  cellConfirmedByName?: string | null;
  cellConfirmedAt?: string | null;

  collectiveProposedGrade: string;
  collectiveComment?: string | null;
  collectiveMeetingId?: string | null;
  collectiveRecordedByName?: string | null;
  collectiveRecordedAt?: string | null;

  appraisalScore?: number;
  appraisalComment: string;
  /** Nội dung giải trình/căn cứ khi chênh lệch tự chấm – thẩm định (B-09). */
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

  /** Mẫu 09C — nội dung tự đánh giá theo mã mục của bộ tiêu chí (task 18). */
  selfAssessment?: Record<string, string>;
  /** Mẫu 9D — kết quả thực hiện nhiệm vụ theo trục (task 18). */
  taskResults?: TaskResultRow[];
  /** Mẫu 09B — nội dung tự luận theo trục, khóa = mã trục (task 18). */
  axisNotes?: Record<string, AxisNote>;
}

// ---------------------------------------------------------------------------
// Biểu mẫu cá nhân (task 18): 09C, 9D nhập cùng phiếu tự chấm; xuất 01/02/09A/09B/09C/9D/10 theo bộ tiêu chí của kỳ.
// ---------------------------------------------------------------------------

/** Một dòng Mẫu 9D (cột (2)–(7) của biểu mẫu gốc), mã trục theo bộ tiêu chí của kỳ. */
export interface TaskResultRow {
  axisCode: string;
  content: string;
  deadline?: string | null;
  status?: string | null;
  product?: string | null;
  progress?: string | null;
  note?: string | null;
}

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

/** Phần cấu hình biểu mẫu cá nhân trong nội dung bộ tiêu chí (`requiredForms`, mục 09C). */
export interface IndividualFormConfig {
  requiredForms?: string[];
  selfAssessmentSections?: SelfAssessmentSection[];
}

/** Biểu mẫu xuất được cho hồ sơ (`GET /reports/docx/record/{recordId}`). */
export interface RecordFormDto {
  code: string;
  name: string;
}

/** Giới hạn độ dài ô Mẫu 9D / 09B (khớp kiểm tra phía máy chủ). */
export const TASK_RESULT_LIMITS = { rows: 100, long: 2000, short: 500, axisNote: 4000 } as const;

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
  scope: string;
  message: string;
}

/** Kết quả kiểm tra kẹt luồng của kỳ. */
export interface PeriodReadinessDto {
  periodId: string;
  ready: boolean;
  checkedRecords: number;
  issues: ReadinessIssueDto[];
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
  /** Chế độ của bước đang chờ: Internal | External. */
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
export interface TaskInputDto {
  taskName: string;
  targetOutput: string;
  weight: number;
  deadline?: string | null;
  attachmentId?: string | null;
  /** Mã trục kết quả thuộc bộ tiêu chí của kỳ (không bắt buộc). */
  axisCode?: string | null;
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
  invalidVotes: number;
  notes?: string;
}

/** Kiểm soát trần tỷ lệ theo Chi bộ (Mẫu 15) */
export interface BranchQuotaCheckDto {
  branchId: string;
  branchName: string;
  totalCadres: number;
  goodOrBetterCount: number;
  maxExcellentAllowed: number;
  proposedExcellentCount: number;
  actualExcellentPercentage: number;
  isExceedingQuota: boolean;
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

// ---------------------------------------------------------------------------
// Tên hiển thị
// ---------------------------------------------------------------------------

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

// ---------------------------------------------------------------------------
// Cập nhật đồng thời (T-24): mọi hành động ghi gửi `version` đã đọc; 409 → phát sự kiện
// để giao diện đề nghị tải lại, giữ nguyên thông báo của máy chủ (nêu rõ lý do).
// ---------------------------------------------------------------------------

export const CONCURRENCY_CONFLICT_MESSAGE =
  "Dữ liệu đã được người khác cập nhật. Vui lòng tải lại để xem dữ liệu mới nhất — nội dung bạn đang nhập vẫn được giữ nguyên.";

/** Sự kiện `window` phát ra khi lệnh cập nhật bị từ chối (409). */
export const EVALUATION_CONFLICT_EVENT = "evaluations:conflict";

export interface EvaluationConflictDetail {
  operation: string;
  message: string;
}

/** Lỗi có phải 409 (xung đột phiên bản / sai trạng thái) không. */
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

/** Đường dẫn API của từng hành động. */
const ACTION_PATH: Record<WorkflowActionCode, string> = {
  SubmitTasks: "tasks/submit",
  ApproveTasks: "tasks/approve",
  ReturnTasks: "tasks/return",
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
  RecordExternal: "external",
};

// ---------------------------------------------------------------------------
// Dịch vụ
// ---------------------------------------------------------------------------

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

  async getPresets(): Promise<PeriodPresetDto[]> {
    return request<PeriodPresetDto[]>("/evaluations/periods/presets");
  },

  async createPeriod(dto: CreatePeriodDto): Promise<EvaluationPeriodDto> {
    return post<EvaluationPeriodDto>("/evaluations/periods", dto);
  },

  async updatePeriod(
    id: string,
    version: number | undefined,
    changes: { name?: string; startDate?: string; endDate?: string; settings?: PeriodSettings; criteriaSetId?: string }
  ): Promise<EvaluationPeriodDto> {
    return withConflictHandling("updatePeriod", () =>
      request<EvaluationPeriodDto>(`/evaluations/periods/${id}`, {
        method: "PUT",
        body: JSON.stringify({ version, ...changes }),
      })
    );
  },

  /**
   * Chuyển trạng thái kỳ: open | lock | unlock | close. Mở kỳ còn cảnh báo kẹt luồng → 409 (`ApiError.data` = kết quả
   * kiểm tra kẹt luồng); mở bắt buộc: `force = true` kèm lý do.
   */
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

  /** Mã quyền chọn được làm quyền thực hiện bước nội bộ. */
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

  async checkBranchQuotas(periodId: string): Promise<BranchQuotaCheckDto[]> {
    return request<BranchQuotaCheckDto[]>(`/evaluations/branch-quotas?periodId=${periodId}`);
  },

  /**
   * Thực hiện một hành động theo bước. `version` là phiên bản hồ sơ đã đọc — bắt buộc (máy chủ trả 400 nếu thiếu).
   * 409 (người khác đã cập nhật / hồ sơ đã sang bước khác) → phát sự kiện EVALUATION_CONFLICT_EVENT.
   * `RecordExternal` cần `step` (bước do cấp trên thực hiện được ghi nhận).
   */
  async performAction(
    recordId: string,
    action: WorkflowActionCode,
    version: number,
    payload: Record<string, unknown> = {},
    step?: WorkflowStepCode
  ): Promise<EvaluationRecordDto> {
    const path = action === "RecordExternal" ? `${ACTION_PATH[action]}/${step}` : ACTION_PATH[action];
    return withConflictHandling(action, () =>
      post<EvaluationRecordDto>(`/evaluations/records/${recordId}/${path}`, { ...payload, version })
    );
  },

  // ----- Biểu mẫu cá nhân của hồ sơ (task 18) -----
  /** Biểu mẫu áp dụng cho hồ sơ theo bộ tiêu chí của kỳ. */
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
