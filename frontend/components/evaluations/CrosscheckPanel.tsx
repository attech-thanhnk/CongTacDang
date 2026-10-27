"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { Badge } from "@/components/common/StatusBadge";
import { CrosscheckRequestForm } from "@/components/crosschecks/CrosscheckRequestForm";
import { errorMessage } from "@/components/admin/adminUtils";
import { formatDateTime } from "@/lib/dateFormat";
import { EvaluationRecordDto } from "@/services/evaluationService";
import { CROSSCHECK_STATUS_TONES, RecordCrosschecksDto, SaveCrosscheckRequest, crosscheckService } from "@/services/crosscheckService";

interface Props {
  record: EvaluationRecordDto;
  /** Gọi sau khi gửi yêu cầu để trang tải lại hồ sơ (hành động thẩm định có thể bị chặn bởi phiếu đang mở). */
  onRecordChanged?: () => void;
}

/** Đối soát chuyên ngành: danh sách phiếu của hồ sơ và nút yêu cầu; nút theo cờ máy chủ. */
export function CrosscheckPanel({ record, onRecordChanged }: Props) {
  const { toast } = useToast();
  const { user, hasPermission } = useAuth();
  // Chủ hồ sơ không xem được phiếu đối soát của chính mình (máy chủ trả 403): không gọi API.
  const isOwner = !!user && user.id === record.memberId;
  const canView = !isOwner && ["evaluation.read", "evaluation.crosscheck.request", "evaluation.crosscheck.respond", "evaluation.crosscheck.confirm"].some(hasPermission);
  const [data, setData] = useState<RecordCrosschecksDto | null>(null);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState(false);

  const load = useCallback(async () => {
    if (!canView) return;
    try {
      setData(await crosscheckService.getByRecord(record.id));
    } catch {
      setData(null);
    }
  }, [record.id, canView]);

  useEffect(() => {
    load();
  }, [load, record.version]);

  if (!data || !data.applicable) return null;
  if (data.items.length === 0 && !data.canRequest) return null;

  const create = async (body: SaveCrosscheckRequest) => {
    setBusy(true);
    try {
      await crosscheckService.create(record.id, body);
      toast.success("Đã gửi yêu cầu đối soát.");
      setForm(false);
      await load();
      onRecordChanged?.();
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Gửi yêu cầu đối soát không thành công."));
    } finally {
      setBusy(false);
    }
  };

  const tasks = record.tasks.filter((t) => !t.changeStatus).map((t) => ({ id: t.id, taskName: t.taskName }));

  return (
    <section className="card">
      <div className="card-header d-flex justify-content-between align-items-center gap-2 flex-wrap">
        <h2 className="mb-0">Đối soát chuyên ngành</h2>
        {!form && data.canRequest && (
          <button type="button" className="btn btn-outline-primary btn-sm" onClick={() => setForm(true)}>
            Yêu cầu đối soát
          </button>
        )}
      </div>
      <div className="card-body d-flex flex-column gap-3">
        {data.blockedReason && !form && !data.canRequest && <div className="small text-secondary">{data.blockedReason}</div>}
        {form && <CrosscheckRequestForm tasks={tasks} busy={busy} submitLabel="Gửi yêu cầu" onCancel={() => setForm(false)} onSubmit={create} />}
        {data.items.map((item) => (
          <Link key={item.id} href={`/crosschecks/${item.id}`} className="border rounded p-2 text-decoration-none text-reset d-flex justify-content-between align-items-start gap-2">
            <div className="small">
              <div className="fw-semibold">{item.expertUnitName}</div>
              <div className="text-secondary">{item.fieldNames.join(", ")}</div>
              <div className="text-secondary">{item.itemCount} chỉ tiêu · {item.requestedByName} · {formatDateTime(item.requestedAt)}</div>
            </div>
            <Badge tone={CROSSCHECK_STATUS_TONES[item.status] ?? "neutral"}>{item.statusName}</Badge>
          </Link>
        ))}
      </div>
    </section>
  );
}

export default CrosscheckPanel;
