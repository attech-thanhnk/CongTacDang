import { request } from "./apiClient";

/** Thông tin kỳ đánh giá hằng quý */
export interface EvaluationPeriodDto {
  id: string;
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
  tasks: TaskInputDto[];
}

/** Dữ liệu tự chấm điểm 4 tiêu chí A-B-C-D cho từng việc */
export interface TaskScoreInputDto {
  taskId: string;
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
  generalScores: number[];
  taskScores: TaskScoreInputDto[];
  selfProposedGrade: string;
}

/** Bước 3: Chi bộ nhận xét và nhập kết quả bỏ phiếu kín (Mẫu 10, 11, 13) */
export interface SubmitBranchReviewRequestDto {
  recordId: string;
  comment: string;
  proposedGrade: string;
  votesExcellent: number;
  votesGood: number;
  votesSatisfactory: number;
  votesUnsatisfactory: number;
  totalVoters: number;
}

/** Bước 4: Tổ Thẩm định đối soát và đề xuất (Mẫu 03) */
export interface SubmitAppraisalRequestDto {
  recordId: string;
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

/** Bước 5: Ban Thường vụ chuẩn y xếp loại chính thức (Mẫu 14 & 16) */
export interface ApproveFinalGradeRequestDto {
  recordId: string;
  finalScore: number;
  finalGrade: string;
}

/** Dịch vụ gọi API Đánh giá & Xếp loại Cán bộ 5 bước theo Hướng dẫn 03-HD/TVĐU */
export const evaluationService = {
  /** Lấy danh sách tất cả các kỳ đánh giá */
  async getPeriods(): Promise<EvaluationPeriodDto[]> {
    return request<EvaluationPeriodDto[]>("/evaluations/periods");
  },

  /** Lấy kỳ đánh giá đang hoạt động */
  async getActivePeriod(): Promise<EvaluationPeriodDto | null> {
    return request<EvaluationPeriodDto | null>("/evaluations/periods/active");
  },

  /** Khởi tạo kỳ đánh giá mới */
  async createPeriod(dto: CreatePeriodDto): Promise<EvaluationPeriodDto> {
    return request<EvaluationPeriodDto>("/evaluations/periods", {
      method: "POST",
      body: JSON.stringify(dto),
    });
  },

  /** Kích hoạt kỳ đánh giá làm kỳ hiện hành */
  async setActivePeriod(id: string): Promise<EvaluationPeriodDto> {
    return request<EvaluationPeriodDto>(`/evaluations/periods/${id}/activate`, {
      method: "PUT",
    });
  },

  /** Cập nhật trạng thái tiến trình của kỳ đánh giá */
  async updatePeriodStatus(id: string, status: number): Promise<EvaluationPeriodDto> {
    return request<EvaluationPeriodDto>(`/evaluations/periods/${id}/status?status=${status}`, {
      method: "PUT",
    });
  },

  /** Lấy hồ sơ đánh giá của cá nhân cán bộ đang đăng nhập trong kỳ */
  async getMyRecord(periodId: string): Promise<EvaluationRecordDto | null> {
    return request<EvaluationRecordDto | null>(`/evaluations/my-record?periodId=${periodId}`);
  },

  /** Lấy chi tiết hồ sơ đánh giá theo Id */
  async getRecordById(id: string): Promise<EvaluationRecordDto> {
    return request<EvaluationRecordDto>(`/evaluations/records/${id}`);
  },

  /** Lấy toàn bộ danh sách hồ sơ đánh giá của một kỳ */
  async getRecordsByPeriod(periodId: string): Promise<EvaluationRecordDto[]> {
    return request<EvaluationRecordDto[]>(`/evaluations/records?periodId=${periodId}`);
  },

  /** Lấy danh sách hồ sơ đánh giá thuộc một Chi bộ (mặc định lấy theo Chi bộ cán bộ) */
  async getRecordsByBranch(periodId: string, branchId?: string): Promise<EvaluationRecordDto[]> {
    const query = branchId ? `periodId=${periodId}&branchId=${branchId}` : `periodId=${periodId}`;
    return request<EvaluationRecordDto[]>(`/evaluations/branch-records?${query}`);
  },

  /** Bước 1: Cán bộ đăng ký 3-7 nhiệm vụ chuyên môn đầu quý (Mẫu 01) */
  async registerTasks(dto: RegisterTasksRequestDto): Promise<EvaluationRecordDto> {
    return request<EvaluationRecordDto>("/evaluations/tasks/register", {
      method: "POST",
      body: JSON.stringify(dto),
    });
  },

  /** Bước 2: Cán bộ tự chấm điểm Tiêu chí chung (Mẫu 09) và Sản phẩm chuyên môn (Mẫu 02) */
  async submitSelfScore(dto: SubmitSelfScoreRequestDto): Promise<EvaluationRecordDto> {
    return request<EvaluationRecordDto>("/evaluations/self-score", {
      method: "POST",
      body: JSON.stringify(dto),
    });
  },

  /** Bước 3: Chi bộ nhận xét và ghi nhận kết quả bỏ phiếu kín (Mẫu 10 & 13) */
  async submitBranchReview(dto: SubmitBranchReviewRequestDto): Promise<EvaluationRecordDto> {
    return request<EvaluationRecordDto>("/evaluations/branch-review", {
      method: "POST",
      body: JSON.stringify(dto),
    });
  },

  /** Bước 4: Tổ Thẩm định đối soát điểm và đề xuất xếp loại (Mẫu 03) */
  async submitAppraisal(dto: SubmitAppraisalRequestDto): Promise<EvaluationRecordDto> {
    return request<EvaluationRecordDto>("/evaluations/appraisal", {
      method: "POST",
      body: JSON.stringify(dto),
    });
  },

  /** Bước 4b: Kiểm tra tỷ lệ trần 20% Hoàn thành xuất sắc nhiệm vụ theo Chi bộ (Mẫu 15) */
  async checkBranchQuotas(periodId: string): Promise<BranchQuotaCheckDto[]> {
    return request<BranchQuotaCheckDto[]>(`/evaluations/branch-quotas?periodId=${periodId}`);
  },

  /** Bước 5: Ban Thường vụ chuẩn y mức xếp loại chính thức (Mẫu 14 & 16) */
  async approveFinalGrade(dto: ApproveFinalGradeRequestDto): Promise<EvaluationRecordDto> {
    return request<EvaluationRecordDto>("/evaluations/approve-final", {
      method: "POST",
      body: JSON.stringify(dto),
    });
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
