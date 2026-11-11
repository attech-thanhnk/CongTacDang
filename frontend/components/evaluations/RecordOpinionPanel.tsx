"use client";

import React, { useCallback, useEffect, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { errorMessage } from "@/components/admin/adminUtils";
import { formatDateTime } from "@/lib/dateFormat";
import { EvaluationRecordDto } from "@/services/evaluationService";
import { fmt, requiresExplanation } from "@/services/criteriaService";
import { RECORD_OPINION_KINDS, RecordOpinionKind, RecordOpinionsDto, recordOpinionService } from "@/services/recordOpinionService";
import { useCriteria } from "./useCriteria";

/** Ý kiến trước phê duyệt và giải trình của cá nhân sau thẩm định. Không chặn bước nào. */
export function RecordOpinionPanel({ record }: { record: EvaluationRecordDto }) {
  const { toast } = useToast();
  const { user } = useAuth();
  // Chủ hồ sơ chỉ nhận về giải trình của chính mình (máy chủ lọc).
  const isOwner = !!user && user.id === record.memberId;
  const { criteria } = useCriteria(isOwner ? record.periodId : undefined);
  const [data, setData] = useState<RecordOpinionsDto | null>(null);
  const [adding, setAdding] = useState(false);
  const [kind, setKind] = useState<RecordOpinionKind | null>(null);
  const [content, setContent] = useState("");
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      setData(await recordOpinionService.get(record.id));
    } catch {
      setData(null);
    }
  }, [record.id]);

  useEffect(() => {
    load();
  }, [load, record.version]);

  if (!data || (data.items.length === 0 && !data.canRecord)) return null;

  // Loại theo quyền người ghi (máy chủ tính); chưa chọn thì dùng loại mặc định.
  const kinds = data.kinds?.length ? data.kinds : RECORD_OPINION_KINDS.map((k) => ({ value: k.value, name: k.label }));
  const selectedKind = kind && kinds.some((k) => k.value === kind) ? kind : kinds[0].value;
  const hasExplanation = data.items.some((i) => i.kind === "MemberExplanation");
  const title = isOwner ? "Giải trình của cá nhân" : hasExplanation ? "Ý kiến và giải trình" : "Ý kiến trước phê duyệt";
  const appraisal = record.appraisalScore ?? null;
  // Nhắc khi chênh lệch đạt ngưỡng của bộ tiêu chí; dưới ngưỡng vẫn ghi được.
  const suggest = isOwner && data.canRecord && !hasExplanation && !!criteria
    && requiresExplanation(criteria.content, record.totalSelfScore, appraisal);

  const save = async () => {
    setBusy(true);
    try {
      setData(await recordOpinionService.create(record.id, { kind: selectedKind, content }));
      toast.success(isOwner ? "Đã ghi nhận giải trình." : "Đã ghi nhận ý kiến.");
      setAdding(false);
      setContent("");
    } catch (err: unknown) {
      toast.error(errorMessage(err, isOwner ? "Ghi nhận giải trình không thành công." : "Ghi nhận ý kiến không thành công."));
    } finally {
      setBusy(false);
    }
  };

  // Chưa có ý kiến và chưa mở ô nhập: chỉ một dòng tiêu đề.
  const hasBody = adding || suggest || data.items.length > 0;

  return (
    <section className="card">
      <div className={`card-header d-flex justify-content-between align-items-center gap-2 flex-wrap ${hasBody ? "" : "border-0"}`}>
        <h2 className="mb-0">{title}</h2>
        {!adding && data.canRecord && (
          <button type="button" className="btn btn-outline-primary btn-sm" onClick={() => setAdding(true)}>
            {isOwner ? "Ghi giải trình" : "Ghi ý kiến"}
          </button>
        )}
      </div>
      {hasBody && (
      <div className="card-body d-flex flex-column gap-3">
        {suggest && appraisal != null && (
          <div className="alert alert-info small mb-0">
            Điểm thẩm định {fmt(appraisal)} chênh lệch {fmt(Math.abs(appraisal - record.totalSelfScore))} điểm so với điểm tự chấm{" "}
            {fmt(record.totalSelfScore)}. Bạn có thể ghi giải trình để đưa vào phiếu thẩm định.
          </div>
        )}
        {adding && (
          <div className="d-flex flex-column gap-2">
            {kinds.length > 1 && (
              <select className="form-select" aria-label="Loại ý kiến" value={selectedKind} onChange={(e) => setKind(e.target.value as RecordOpinionKind)}>
                {kinds.map((k) => (
                  <option key={k.value} value={k.value}>
                    {k.name}
                  </option>
                ))}
              </select>
            )}
            <textarea className="form-control" rows={4} aria-label={isOwner ? "Nội dung giải trình" : "Nội dung ý kiến"} maxLength={4000}
              value={content} onChange={(e) => setContent(e.target.value)} />
            <div className="d-flex gap-2">
              <button type="button" className="btn btn-primary btn-sm" disabled={busy || !content.trim()} onClick={save}>
                Lưu
              </button>
              <button type="button" className="btn btn-outline-secondary btn-sm" disabled={busy} onClick={() => setAdding(false)}>
                Hủy
              </button>
            </div>
          </div>
        )}
        {data.items.map((item) => (
          <div key={item.id} className="border rounded p-2 small">
            <div className="fw-semibold">{item.kindName}</div>
            <div style={{ whiteSpace: "pre-wrap" }}>{item.content}</div>
            <div className="text-secondary">
              {item.recordedByName} · {formatDateTime(item.recordedAt)}
            </div>
          </div>
        ))}
      </div>
      )}
    </section>
  );
}

export default RecordOpinionPanel;
