import { request } from "./apiClient";
import type {
  CollectiveEvaluationRecordDto,
  EvaluationMeetingDto,
  SaveCollectiveEvaluationRequestDto,
  SaveEvaluationMeetingRequestDto,
} from "./evaluationService";

/** Mục/nhóm nội dung biểu mẫu tập thể (nguyên văn biểu mẫu gốc). */
export interface CollectiveFormSection {
  code: string;
  title: string;
}

export interface CollectiveCriteriaItem {
  code: string;
  text: string;
  maxScore: number;
}

/** Nhóm điểm mà tiêu chí con cộng vào: nhóm tiêu chí chung hoặc nhóm kết quả thực hiện nhiệm vụ. */
export type CollectiveScoreKind = "General" | "Task";

export interface CollectiveCriteriaGroup {
  code: string;
  name: string;
  kind: CollectiveScoreKind;
  items: CollectiveCriteriaItem[];
}

/** Danh mục mục nhập Mẫu 07 (I.1–I.4), nhóm nội dung Mẫu 08 (1–13) và khung tiêu chí tập thể của kỳ. */
export interface CollectiveFormCatalog {
  form07Strengths: CollectiveFormSection[];
  /** Mẫu 06: mã T.* là số nhiệm vụ, S.* là nội dung văn bản, H.* là người đứng đầu. */
  form06Fields: CollectiveFormSection[];
  form08Categories: CollectiveFormSection[];
  /** Rỗng = bộ tiêu chí của kỳ không có khung tập thể (nhập thẳng hai điểm nhóm). */
  collectiveGroups: CollectiveCriteriaGroup[];
  generalMaxScore: number;
  taskMaxScore: number;
}

export interface CollectiveRecord extends CollectiveEvaluationRecordDto {
  sections: Record<string, string>;
  /** Khóa = mã tiêu chí con; rỗng = nhập thẳng hai điểm nhóm. */
  criteriaScores: Record<string, number>;
  /** Máy chủ tính theo quyền và trạng thái kỳ. */
  canAttach?: boolean;
}

export type CatalogState = "loading" | "error" | "ready";

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

export interface MeetingRecord extends EvaluationMeetingDto {
  details: MeetingDetails;
}

export interface SaveMeetingRecord extends SaveEvaluationMeetingRequestDto {
  details: MeetingDetails;
}

export const collectiveService = {
  /** Có kỳ thì kèm khung tiêu chí tập thể của kỳ. */
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
