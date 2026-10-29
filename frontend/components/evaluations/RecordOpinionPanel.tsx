"use client";

import React, { useCallback, useEffect, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { errorMessage } from "@/components/admin/adminUtils";
import { formatDateTime } from "@/lib/dateFormat";
import { EvaluationRecordDto } from "@/services/evaluationService";
import { RECORD_OPINION_KINDS, RecordOpinionKind, RecordOpinionsDto, recordOpinionService } from "@/services/recordOpinionService";

/** Ý kiến trước phê duyệt: họp rà soát, rà soát hợp lệ, Phó Giám đốc phụ trách. Không chặn bước nào. */
export function RecordOpinionPanel({ record }: { record: EvaluationRecordDto }) {
  const { toast } = useToast();
  const { user } = useAuth();
  // Chủ hồ sơ không xem được ý kiến nội bộ (máy chủ trả 403): không gọi API.
  const isOwner = !!user && user.id === record.memberId;
  const [data, setData] = useState<RecordOpinionsDto | null>(null);
  const [adding, setAdding] = useState(false);
  const [kind, setKind] = useState<RecordOpinionKind>(RECORD_OPINION_KINDS[0].value);
  const [content, setContent] = useState("");
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    if (isOwner) return;
    try {
      setData(await recordOpinionService.get(record.id));
    } catch {
      setData(null);
    }
  }, [record.id, isOwner]);

  useEffect(() => {
    load();
  }, [load, record.version]);

  if (!data || (data.items.length === 0 && !data.canRecord)) return null;

  const save = async () => {
    setBusy(true);
    try {
      setData(await recordOpinionService.create(record.id, { kind, content }));
      toast.success("Đã ghi nhận ý kiến.");
      setAdding(false);
      setContent("");
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Ghi nhận ý kiến không thành công."));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="card">
      <div className="card-header d-flex justify-content-between align-items-center gap-2 flex-wrap">
        <h2 className="mb-0">Ý kiến trước phê duyệt</h2>
        {!adding && data.canRecord && (
          <button type="button" className="btn btn-outline-primary btn-sm" onClick={() => setAdding(true)}>
            Ghi ý kiến
          </button>
        )}
      </div>
      <div className="card-body d-flex flex-column gap-3">
        {adding && (
          <div className="d-flex flex-column gap-2">
            <select className="form-select" aria-label="Loại ý kiến" value={kind} onChange={(e) => setKind(e.target.value as RecordOpinionKind)}>
              {RECORD_OPINION_KINDS.map((k) => (
                <option key={k.value} value={k.value}>
                  {k.label}
                </option>
              ))}
            </select>
            <textarea className="form-control" rows={4} aria-label="Nội dung ý kiến" maxLength={4000} value={content} onChange={(e) => setContent(e.target.value)} />
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
    </section>
  );
}

export default RecordOpinionPanel;
