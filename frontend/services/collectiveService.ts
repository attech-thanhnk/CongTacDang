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

/** Danh mục mục nhập Mẫu 07 (I.1–I.4) và nhóm nội dung Mẫu 08 (1–13). */
export interface CollectiveFormCatalog {
  form07Strengths: CollectiveFormSection[];
  form08Categories: CollectiveFormSection[];
}

/** Hồ sơ tập thể kèm các mục con theo mã (Mẫu 07: I.1–I.4). */
export interface CollectiveRecord extends CollectiveEvaluationRecordDto {
  sections: Record<string, string>;
}

/** Dữ liệu lưu hồ sơ tập thể (tạo hoặc sửa). */
export interface SaveCollectiveRecord extends SaveCollectiveEvaluationRequestDto {
  sections: Record<string, string>;
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
  /** Danh mục mục nhập của Mẫu 07/08 theo biểu mẫu gốc. */
  getCatalog(): Promise<CollectiveFormCatalog> {
    return request<CollectiveFormCatalog>("/evaluations/collective-forms/catalog");
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
