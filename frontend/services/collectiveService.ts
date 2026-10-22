import { request } from "./apiClient";
import type {
  CollectiveEvaluationRecordDto,
  EvaluationMeetingDto,
  SaveCollectiveEvaluationRequestDto,
  SaveEvaluationMeetingRequestDto,
} from "./evaluationService";

/** Một mục/nhóm nội dung của biểu mẫu tập thể (nguyên văn biểu mẫu HD03). */
export interface CollectiveFormSection {
  code: string;
  title: string;
}

/** Một tiêu chí con của khung tiêu chí tập thể. */
export interface CollectiveCriteriaItem {
  code: string;
  text: string;
  maxScore: number;
}

/** Nhóm điểm mà tiêu chí con cộng vào: nhóm tiêu chí chung hoặc nhóm kết quả thực hiện nhiệm vụ. */
export type CollectiveScoreKind = "General" | "Task";

/** Một nhóm của khung tiêu chí tập thể theo bộ tiêu chí của kỳ. */
export interface CollectiveCriteriaGroup {
  code: string;
  name: string;
  kind: CollectiveScoreKind;
  items: CollectiveCriteriaItem[];
}

/** Danh mục mục nhập Mẫu 07 (I.1–I.4), nhóm nội dung Mẫu 08 (1–13) và khung tiêu chí tập thể của kỳ. */
export interface CollectiveFormCatalog {
  form07Strengths: CollectiveFormSection[];
  form08Categories: CollectiveFormSection[];
  /** Rỗng = bộ tiêu chí của kỳ không có khung tập thể (nhập thẳng hai điểm nhóm). */
  collectiveGroups: CollectiveCriteriaGroup[];
  generalMaxScore: number;
  taskMaxScore: number;
}

/** Hồ sơ tập thể kèm các mục con theo mã (Mẫu 07: I.1–I.4). */
export interface CollectiveRecord extends CollectiveEvaluationRecordDto {
  sections: Record<string, string>;
  /** Điểm từng tiêu chí của khung tập thể theo mã tiêu chí con (rỗng = nhập thẳng hai điểm nhóm). */
  criteriaScores: Record<string, number>;
}

/** Dữ liệu lưu hồ sơ tập thể (tạo hoặc sửa). */
export interface SaveCollectiveRecord extends SaveCollectiveEvaluationRequestDto {
  sections: Record<string, string>;
  /** Gửi khi kỳ có khung tiêu chí tập thể (Mẫu 06, 07). */
  criteriaScores?: Record<string, number>;
}

/** Người dự hội nghị ở mục 3.2 của Mẫu 12. */
export interface MeetingAttendee {
  name: string;
  title: string;
}

/** Các mục của biên bản Mẫu 12, 13 chưa có cột riêng. */
export interface MeetingDetails {
  workingRules?: string | null;
  reportingUnit?: string | null;
  chairTitle?: string | null;
  secretaryTitle?: string | null;
  attendees: MeetingAttendee[];
  /** Mẫu 13: Tổ kiểm phiếu — người đầu là Tổ trưởng, những người sau là Thành viên. */
  countingCommittee: MeetingAttendee[];
  /** Mẫu 13: số phiếu phát ra, thu về, hợp lệ, không hợp lệ (null = chưa ghi). */
  ballotsIssued?: number | null;
  ballotsCollected?: number | null;
  ballotsValid?: number | null;
  ballotsInvalid?: number | null;
}

/** Biên bản hội nghị kèm các mục Mẫu 12. */
export interface MeetingRecord extends EvaluationMeetingDto {
  details: MeetingDetails;
}

/** Dữ liệu lưu biên bản (tạo hoặc sửa). */
export interface SaveMeetingRecord extends SaveEvaluationMeetingRequestDto {
  details: MeetingDetails;
}

export const collectiveService = {
  /** Danh mục mục nhập của Mẫu 07/08; có kỳ thì kèm khung tiêu chí tập thể của bộ tiêu chí của kỳ. */
  getCatalog(periodId?: string): Promise<CollectiveFormCatalog> {
    const query = periodId ? `?periodId=${encodeURIComponent(periodId)}` : "";
    return request<CollectiveFormCatalog>(`/evaluations/collective-forms/catalog${query}`);
  },

  getRecords(periodId: string): Promise<CollectiveRecord[]> {
    return request<CollectiveRecord[]>(`/evaluations/collective-records?periodId=${encodeURIComponent(periodId)}`);
  },

  createRecord(dto: SaveCollectiveRecord): Promise<CollectiveRecord> {
    return request<CollectiveRecord>("/evaluations/collective-records", { method: "POST", body: JSON.stringify(dto) });
  },

  updateRecord(id: string, dto: SaveCollectiveRecord): Promise<CollectiveRecord> {
    return request<CollectiveRecord>(`/evaluations/collective-records/${id}`, { method: "PUT", body: JSON.stringify(dto) });
  },

  getMeetings(periodId: string): Promise<MeetingRecord[]> {
    return request<MeetingRecord[]>(`/evaluations/meetings?periodId=${encodeURIComponent(periodId)}`);
  },

  createMeeting(dto: SaveMeetingRecord): Promise<MeetingRecord> {
    return request<MeetingRecord>("/evaluations/meetings", { method: "POST", body: JSON.stringify(dto) });
  },

  updateMeeting(id: string, dto: SaveMeetingRecord): Promise<MeetingRecord> {
    return request<MeetingRecord>(`/evaluations/meetings/${id}`, { method: "PUT", body: JSON.stringify(dto) });
  },
};
