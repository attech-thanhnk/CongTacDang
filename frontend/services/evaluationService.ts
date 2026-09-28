import { ApiError, request } from "./apiClient";

/** Thông tin kỳ đánh giá hằng quý */
export interface EvaluationPeriodDto {
  id: string;
  /** Phiên bản dữ liệu (xmin) dùng để kiểm tra cập nhật đồng thời */
  version?: number;
  year: number;
  quarter: number;
  name: string;
  startDate: string;
  endDate: string;
  status: string;
  statusDisplayName: string;
  isActive: boolean;
}

/** Dữ liệu tạo mới kỳ đánh giá */
export interface CreatePeriodDto {
  year: number;
  quarter: number;
  name: string;
  startDate: string;
  endDate: string;
}

/** Chi tiết công việc / sản phẩm chuyên môn đăng ký và chấm điểm */
export interface EvaluationTaskDto {
  id: string;
  version?: number;
  recordId: string;
  taskOrder: number;
  taskName: string;
  targetOutput: string;
  weight: number;
  deadline: string;
  criteriaA_Ratio: number;
  criteriaB_Ratio: number;
  criteriaC_Ratio: number;
  criteriaD_Ratio: number;
  selfScore: number;
  supervisorScore?: number;
  isExceedStandard: boolean;
  attachmentId?: string | null;
  attachmentFileName?: string | null;
  attachmentOriginalName?: string | null;
}

/** Hồ sơ đánh giá cá nhân của Cán bộ theo Hướng dẫn 03-HD/TVĐU */
export interface EvaluationRecordDto {
  id: string;
  version?: number;
  periodId: string;
  periodName: string;
  memberId: string;
  fullName: string;
  partyCardNumber?: string;
  partyRole: string;
  positionTitle: string;
  partyCellName?: string;
  partyCellId?: string;
  departmentName?: string;
  jobGroup: string;
  generalScores: number[];
  generalCriteriaScore: number;
  tasksScore: number;
  totalSelfScore: number;
  selfProposedGrade: string;
  partyCellComment: string;
  partyCellProposedGrade: string;
  votesExcellent: number;
  votesGood: number;
  votesSatisfactory: number;
  votesUnsatisfactory: number;
  totalVoters: number;
  appraisalScore?: number;
  appraisalComment: string;
  appraisalProposedGrade: string;
  finalScore: number;
  finalGrade: string;
  status: string;
  statusDisplayName: string;
  tasks: EvaluationTaskDto[];
}

/** Dữ liệu nhập 1 nhiệm vụ khi đăng ký đầu quý */
export interface TaskInputDto {
  taskName: string;
  targetOutput: string;
  weight: number;
  deadline: string;
  attachmentId?: string | null;
  attachmentFileName?: string | null;
}

/** Bước 1: Đăng ký 3-7 nhiệm vụ chuyên môn đầu quý (Mẫu 01) */
export interface RegisterTasksRequestDto {
  periodId: string;
  /** Phiên bản hồ sơ đã đọc; bỏ trống thì service tự lấy từ lần đọc gần nhất */
  version?: number;
  tasks: TaskInputDto[];
}

/** Dữ liệu tự chấm điểm 4 tiêu chí A-B-C-D cho từng việc */
export interface TaskScoreInputDto {
  taskId: string;
  version?: number;
  criteriaA_Ratio: number;
  criteriaB_Ratio: number;
  criteriaC_Ratio: number;
  criteriaD_Ratio: number;
  isExceedStandard: boolean;
  attachmentId?: string | null;
}

/** Bước 2: Tự chấm điểm 100đ (Mẫu 02 & Mẫu 09) */
export interface SubmitSelfScoreRequestDto {
  recordId: string;
  version?: number;
  generalScores: number[];
  taskScores: TaskScoreInputDto[];
  selfProposedGrade: string;
}

/** Bước 3: Chi bộ nhận xét và nhập kết quả bỏ phiếu kín (Mẫu 10, 11, 13) */
export interface SubmitBranchReviewRequestDto {
  recordId: string;
  version?: number;
  comment: string;
  proposedGrade: string;
  votesExcellent: number;
  votesGood: number;
  votesSatisfactory: number;
  votesUnsatisfactory: number;
  totalVoters: number;
}

export interface BranchMemberVoteInputDto {
  recordId: string;
  version?: number;
  comment: string;
  proposedGrade: string;
  votesExcellent: number;
  votesGood: number;
  votesSatisfactory: number;
  votesUnsatisfactory: number;
}

export interface SubmitBranchMeetingRequestDto {
  periodId: string;
  partyCellId: string;
  totalVoters: number;
  memberVotes: BranchMemberVoteInputDto[];
}

/** Bước 4: Tổ Thẩm định đối soát và đề xuất (Mẫu 03) */
export interface SubmitAppraisalRequestDto {
  recordId: string;
  version?: number;
  appraisalScore?: number;
  comment: string;
  proposedGrade: string;
}

/** Bước 4b: Kiểm soát trần 20% theo Chi bộ (Mẫu 15) */
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
  /** Hiện backend chỉ tạo mới hồ sơ tập thể; trường này dành cho lệnh cập nhật sau này */
  version?: number;
  periodId: string;
  form: "M06" | "M07" | "M08";
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
  partyCellId?: string;
  partyCellName?: string;
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
  /** Hiện backend chỉ tạo mới biên bản; trường này dành cho lệnh cập nhật sau này */
  version?: number;
  periodId: string;
  partyCellId: string;
  formCode: "M12" | "M13";
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

/** Bước 5: Ban Thường vụ chuẩn y xếp loại chính thức (Mẫu 14 & 16) */
export interface ApproveFinalGradeRequestDto {
  recordId: string;
  version?: number;
  finalScore: number;
  finalGrade: string;
}

// ---------------------------------------------------------------------------
// Kiểm tra cập nhật đồng thời (optimistic concurrency, T-24)
//
// Mọi DTO đọc về có `version` (xmin của PostgreSQL). Service ghi nhớ version của
// lần đọc gần nhất theo Id và tự gửi lại trong lệnh cập nhật tương ứng (nếu caller
// không truyền). Khi người khác đã cập nhật trước, backend trả 409: service KHÔNG
// tự làm mới version (để không ghi đè mù), mà báo lỗi CONCURRENCY_CONFLICT_MESSAGE
// và phát sự kiện EVALUATION_CONFLICT_EVENT để giao diện đề nghị tải lại dữ liệu.
// ---------------------------------------------------------------------------

export const CONCURRENCY_CONFLICT_MESSAGE =
  "Dữ liệu đã được người khác cập nhật. Vui lòng tải lại để xem dữ liệu mới nhất — nội dung bạn đang nhập vẫn được giữ nguyên.";

/** Sự kiện `window` phát ra khi lệnh cập nhật bị từ chối do xung đột phiên bản (409). */
export const EVALUATION_CONFLICT_EVENT = "evaluations:conflict";

export interface EvaluationConflictDetail {
  operation: string;
  message: string;
}

type VersionedKind = "period" | "record" | "task";

const versionCache = new Map<string, number>();
/** Hồ sơ của chính người dùng theo kỳ: periodId → recordId (Bước 1 không gửi recordId). */
const myRecordIdByPeriod = new Map<string, string>();

function rememberVersion(kind: VersionedKind, entity?: { id?: string; version?: number } | null) {
  if (entity?.id && typeof entity.version === "number") {
    versionCache.set(`${kind}:${entity.id}`, entity.version);
  }
}

function versionOf(kind: VersionedKind, id?: string | null): number | undefined {
  return id ? versionCache.get(`${kind}:${id}`) : undefined;
}

function rememberRecord(record?: EvaluationRecordDto | null): EvaluationRecordDto | null | undefined {
  if (record) {
    rememberVersion("record", record);
    record.tasks?.forEach((task) => rememberVersion("task", task));
  }
  return record;
}

function rememberRecords(records: EvaluationRecordDto[]): EvaluationRecordDto[] {
  records?.forEach((r) => rememberRecord(r));
  return records;
}

function rememberPeriod(period?: EvaluationPeriodDto | null) {
  rememberVersion("period", period);
  return period;
}

/** Lỗi có phải do xung đột phiên bản (người khác đã cập nhật trước) không. */
export function isConcurrencyConflict(error: unknown): boolean {
  return error instanceof ApiError && error.status === 409;
}

/** Chạy lệnh cập nhật; 409 → chuẩn hóa thông báo và phát sự kiện xung đột. */
async function withConflictHandling<T>(operation: string, action: () => Promise<T>): Promise<T> {
  try {
    return await action();
  } catch (error) {
    if (isConcurrencyConflict(error)) {
      const apiError = error as ApiError;
      if (typeof window !== "undefined") {
        window.dispatchEvent(
          new CustomEvent<EvaluationConflictDetail>(EVALUATION_CONFLICT_EVENT, {
            detail: { operation, message: CONCURRENCY_CONFLICT_MESSAGE },
          })
        );
      }
      throw new ApiError(CONCURRENCY_CONFLICT_MESSAGE, 409, apiError.errors, apiError.data);
    }
    throw error;
  }
}

function versionQuery(version?: number): string {
  return typeof version === "number" ? `version=${version}` : "";
}

/** Dịch vụ gọi API Đánh giá & Xếp loại Cán bộ 5 bước theo Hướng dẫn 03-HD/TVĐU */
export const evaluationService = {
  /** Lấy danh sách tất cả các kỳ đánh giá */
  async getPeriods(): Promise<EvaluationPeriodDto[]> {
    const periods = await request<EvaluationPeriodDto[]>("/evaluations/periods");
    periods?.forEach((p) => rememberPeriod(p));
    return periods;
  },

  /** Lấy kỳ đánh giá đang hoạt động */
  async getActivePeriod(): Promise<EvaluationPeriodDto | null> {
    const period = await request<EvaluationPeriodDto | null>("/evaluations/periods/active");
    rememberPeriod(period);
    return period;
  },

  /** Khởi tạo kỳ đánh giá mới */
  async createPeriod(dto: CreatePeriodDto): Promise<EvaluationPeriodDto> {
    const period = await request<EvaluationPeriodDto>("/evaluations/periods", {
      method: "POST",
      body: JSON.stringify(dto),
    });
    rememberPeriod(period);
    return period;
  },

  /** Kích hoạt kỳ đánh giá làm kỳ hiện hành */
  async setActivePeriod(id: string, version?: number): Promise<EvaluationPeriodDto> {
    const query = versionQuery(version ?? versionOf("period", id));
    return withConflictHandling("setActivePeriod", async () =>
      rememberPeriod(
        await request<EvaluationPeriodDto>(`/evaluations/periods/${id}/activate${query ? `?${query}` : ""}`, {
          method: "PUT",
        })
      ) as EvaluationPeriodDto
    );
  },

  /** Cập nhật trạng thái tiến trình của kỳ đánh giá */
  async updatePeriodStatus(id: string, status: number, version?: number): Promise<EvaluationPeriodDto> {
    const query = versionQuery(version ?? versionOf("period", id));
    return withConflictHandling("updatePeriodStatus", async () =>
      rememberPeriod(
        await request<EvaluationPeriodDto>(
          `/evaluations/periods/${id}/status?status=${status}${query ? `&${query}` : ""}`,
          { method: "PUT" }
        )
      ) as EvaluationPeriodDto
    );
  },

  /** Lấy hồ sơ đánh giá của cá nhân cán bộ đang đăng nhập trong kỳ */
  async getMyRecord(periodId: string): Promise<EvaluationRecordDto | null> {
    const record = await request<EvaluationRecordDto | null>(`/evaluations/my-record?periodId=${periodId}`);
    if (record) myRecordIdByPeriod.set(periodId, record.id);
    return rememberRecord(record) ?? null;
  },

  /** Lấy chi tiết hồ sơ đánh giá theo Id */
  async getRecordById(id: string): Promise<EvaluationRecordDto> {
    return rememberRecord(await request<EvaluationRecordDto>(`/evaluations/records/${id}`)) as EvaluationRecordDto;
  },

  /** Lấy toàn bộ danh sách hồ sơ đánh giá của một kỳ */
  async getRecordsByPeriod(periodId: string): Promise<EvaluationRecordDto[]> {
    return rememberRecords(await request<EvaluationRecordDto[]>(`/evaluations/records?periodId=${periodId}`));
  },

  /** Lấy danh sách hồ sơ đánh giá thuộc một Chi bộ (mặc định lấy theo Chi bộ cán bộ) */
  async getRecordsByBranch(periodId: string, branchId?: string): Promise<EvaluationRecordDto[]> {
    const query = branchId ? `periodId=${periodId}&branchId=${branchId}` : `periodId=${periodId}`;
    return rememberRecords(await request<EvaluationRecordDto[]>(`/evaluations/branch-records?${query}`));
  },

  /** Bước 1: Cán bộ đăng ký 3-7 nhiệm vụ chuyên môn đầu quý (Mẫu 01) */
  async registerTasks(dto: RegisterTasksRequestDto): Promise<EvaluationRecordDto> {
    const payload: RegisterTasksRequestDto = {
      ...dto,
      version: dto.version ?? versionOf("record", myRecordIdByPeriod.get(dto.periodId)),
    };
    return withConflictHandling("registerTasks", async () => {
      const record = await request<EvaluationRecordDto>("/evaluations/tasks/register", {
        method: "POST",
        body: JSON.stringify(payload),
      });
      if (record) myRecordIdByPeriod.set(dto.periodId, record.id);
      return rememberRecord(record) as EvaluationRecordDto;
    });
  },

  /** Bước 2: Cán bộ tự chấm điểm Tiêu chí chung (Mẫu 09) và Sản phẩm chuyên môn (Mẫu 02) */
  async submitSelfScore(dto: SubmitSelfScoreRequestDto): Promise<EvaluationRecordDto> {
    const payload: SubmitSelfScoreRequestDto = {
      ...dto,
      version: dto.version ?? versionOf("record", dto.recordId),
      taskScores: dto.taskScores.map((t) => ({ ...t, version: t.version ?? versionOf("task", t.taskId) })),
    };
    return withConflictHandling("submitSelfScore", async () =>
      rememberRecord(
        await request<EvaluationRecordDto>("/evaluations/self-score", {
          method: "POST",
          body: JSON.stringify(payload),
        })
      ) as EvaluationRecordDto
    );
  },

  /** Bước 3: Chi bộ nhận xét và ghi nhận kết quả bỏ phiếu kín (Mẫu 10 & 13) */
  async submitBranchReview(dto: SubmitBranchReviewRequestDto): Promise<EvaluationRecordDto> {
    const payload: SubmitBranchReviewRequestDto = { ...dto, version: dto.version ?? versionOf("record", dto.recordId) };
    return withConflictHandling("submitBranchReview", async () =>
      rememberRecord(
        await request<EvaluationRecordDto>("/evaluations/branch-review", {
          method: "POST",
          body: JSON.stringify(payload),
        })
      ) as EvaluationRecordDto
    );
  },

  /** Bước 3b: Chi bộ lưu toàn bộ Biên bản kiểm phiếu của Chi bộ trong cuộc họp (Mẫu 13) */
  async submitBranchMeeting(dto: SubmitBranchMeetingRequestDto): Promise<EvaluationRecordDto[]> {
    const payload: SubmitBranchMeetingRequestDto = {
      ...dto,
      memberVotes: dto.memberVotes.map((v) => ({ ...v, version: v.version ?? versionOf("record", v.recordId) })),
    };
    return withConflictHandling("submitBranchMeeting", async () =>
      rememberRecords(
        await request<EvaluationRecordDto[]>("/evaluations/branch-meeting-review", {
          method: "POST",
          body: JSON.stringify(payload),
        })
      )
    );
  },

  /** Bước 4: Tổ Thẩm định đối soát điểm và đề xuất xếp loại (Mẫu 03) */
  async submitAppraisal(dto: SubmitAppraisalRequestDto): Promise<EvaluationRecordDto> {
    const payload: SubmitAppraisalRequestDto = { ...dto, version: dto.version ?? versionOf("record", dto.recordId) };
    return withConflictHandling("submitAppraisal", async () =>
      rememberRecord(
        await request<EvaluationRecordDto>("/evaluations/appraisal", {
          method: "POST",
          body: JSON.stringify(payload),
        })
      ) as EvaluationRecordDto
    );
  },

  /** Bước 4b: Kiểm tra tỷ lệ trần 20% Hoàn thành xuất sắc nhiệm vụ theo Chi bộ (Mẫu 15) */
  async checkBranchQuotas(periodId: string): Promise<BranchQuotaCheckDto[]> {
    return request<BranchQuotaCheckDto[]>(`/evaluations/branch-quotas?periodId=${periodId}`);
  },

  /** Lấy hồ sơ đánh giá tập thể M06-M08. */
  async getCollectiveRecords(periodId: string, form?: string): Promise<CollectiveEvaluationRecordDto[]> {
    const query = form ? `periodId=${periodId}&form=${encodeURIComponent(form)}` : `periodId=${periodId}`;
    return request<CollectiveEvaluationRecordDto[]>(`/evaluations/collective-records?${query}`);
  },

  /** Tạo hồ sơ đánh giá tập thể M06-M08. */
  async createCollectiveRecord(dto: SaveCollectiveEvaluationRequestDto): Promise<CollectiveEvaluationRecordDto> {
    return request<CollectiveEvaluationRecordDto>("/evaluations/collective-records", {
      method: "POST",
      body: JSON.stringify(dto),
    });
  },

  /** Lấy biên bản hội nghị M12-M13. */
  async getMeetings(periodId: string, partyCellId?: string): Promise<EvaluationMeetingDto[]> {
    const query = partyCellId ? `periodId=${periodId}&partyCellId=${partyCellId}` : `periodId=${periodId}`;
    return request<EvaluationMeetingDto[]>(`/evaluations/meetings?${query}`);
  },

  /** Tạo biên bản hội nghị hoặc kiểm phiếu M12-M13. */
  async createMeeting(dto: SaveEvaluationMeetingRequestDto): Promise<EvaluationMeetingDto> {
    return request<EvaluationMeetingDto>("/evaluations/meetings", {
      method: "POST",
      body: JSON.stringify(dto),
    });
  },

  /** Bước 5: Ban Thường vụ chuẩn y mức xếp loại chính thức (Mẫu 14 & 16) */
  async approveFinalGrade(dto: ApproveFinalGradeRequestDto): Promise<EvaluationRecordDto> {
    const payload: ApproveFinalGradeRequestDto = { ...dto, version: dto.version ?? versionOf("record", dto.recordId) };
    return withConflictHandling("approveFinalGrade", async () =>
      rememberRecord(
        await request<EvaluationRecordDto>("/evaluations/approve-final", {
          method: "POST",
          body: JSON.stringify(payload),
        })
      ) as EvaluationRecordDto
    );
  },

  async getAllRecords(periodId: string): Promise<EvaluationRecordDto[]> {
    return this.getRecordsByPeriod(periodId);
  },

  async getBranchRecords(periodId: string, branchId?: string): Promise<EvaluationRecordDto[]> {
    return this.getRecordsByBranch(periodId, branchId || "");
  },

  async approveEvaluation(dto: ApproveFinalGradeRequestDto): Promise<EvaluationRecordDto> {
    return this.approveFinalGrade(dto);
  },
};
