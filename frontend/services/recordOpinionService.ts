import { request } from "./apiClient";

export type RecordOpinionKind = "ListMeeting" | "ListCheck" | "DeputyOnList" | "DeputyOnReview";

export const RECORD_OPINION_KINDS: { value: RecordOpinionKind; label: string }[] = [
  { value: "ListMeeting", label: "Kết quả họp rà soát danh mục" },
  { value: "ListCheck", label: "Kết quả rà soát hợp lệ" },
  { value: "DeputyOnList", label: "Ý kiến Phó Giám đốc phụ trách về danh mục" },
  { value: "DeputyOnReview", label: "Ý kiến Phó Giám đốc phụ trách về đề xuất mức" },
];

export interface RecordOpinionDto {
  id: string;
  kind: RecordOpinionKind;
  kindName: string;
  content: string;
  recordedByName: string;
  recordedAt: string;
}

export interface RecordOpinionsDto {
  recordId: string;
  /** Người xem ghi được ý kiến trên hồ sơ này (máy chủ tính). */
  canRecord: boolean;
  items: RecordOpinionDto[];
}

const base = (recordId: string) => `/evaluations/records/${recordId}/opinions`;

export const recordOpinionService = {
  get(recordId: string): Promise<RecordOpinionsDto> {
    return request<RecordOpinionsDto>(base(recordId));
  },

  create(recordId: string, body: { kind: RecordOpinionKind; content: string }): Promise<RecordOpinionsDto> {
    return request<RecordOpinionsDto>(base(recordId), { method: "POST", body: JSON.stringify(body) });
  },
};
