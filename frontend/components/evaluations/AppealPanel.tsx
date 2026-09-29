"use client";

import React, { useCallback, useEffect, useState } from "react";
import { useToast } from "@/contexts/ToastContext";
import { attachmentService } from "@/services/attachmentService";
import { EvaluationRecordDto, WorkflowStepCode, evaluationService } from "@/services/evaluationService";
import { AppealDto, AppealFileDto, RecordAppealsDto, postPublishService } from "@/services/postPublishService";
import { ReasonDialog } from "./ReasonDialog";

const formatDateTime = (value?: string | null) =>
  value ? new Intl.DateTimeFormat("vi-VN", { dateStyle: "short", timeStyle: "short" }).format(new Date(value)) : "—";

const STATUS_CLASS: Record<string, string> = {
  Submitted: "text-bg-warning",
  UnderReview: "text-bg-info",
  Accepted: "text-bg-success",
  Rejected: "text-bg-secondary",
};

interface Props {
  record: EvaluationRecordDto;
  /** Gọi khi hồ sơ thay đổi (mở lại theo kiến nghị) để trang tải lại. */
  onRecordChanged?: () => void;
}

/**
 * Khối "Kiến nghị" trên trang hồ sơ (task 20 — T-87, HD03 PL II mục III.2): chủ hồ sơ gửi kiến nghị sau công bố (kèm tệp),
 * người có quyền xử lý nhận xem xét và trả lời có căn cứ; kiến nghị được chấp nhận → gợi ý mở lại hồ sơ bằng thao tác "Mở lại".
 * Quyền và luật (xung đột lợi ích, không trùng khi đang xử lý) do máy chủ quyết định.
 */
export function AppealPanel({ record, onRecordChanged }: Props) {
  const { toast } = useToast();
  const [data, setData] = useState<RecordAppealsDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [showForm, setShowForm] = useState(false);
  const [content, setContent] = useState("");
  const [steps, setSteps] = useState<string[]>([]);
  const [files, setFiles] = useState<File[]>([]);
  const [reopenAppeal, setReopenAppeal] = useState<AppealDto | null>(null);
  const [reopenTargets, setReopenTargets] = useState<WorkflowStepCode[] | null>(null);

  const load = useCallback(async () => {
    try {
      setData(await postPublishService.getRecordAppeals(record.id));
      setError(null);
    } catch (err: any) {
      setError(err?.message || "Không tải được kiến nghị.");
    }
  }, [record.id]);

  useEffect(() => {
    load();
  }, [load, record.status, record.version]);

  // Hồ sơ chưa công bố và chưa từng có kiến nghị: không hiện khối.
  if (!data && !error) return null;
  if (data && data.appeals.length === 0 && !data.canSubmit && record.status !== "Published") return null;

  const run = async (action: () => Promise<unknown>, message: string) => {
    setBusy(true);
    try {
      await action();
      toast.success(message);
      await load();
      return true;
    } catch (err: any) {
      toast.error(err?.message || "Không thực hiện được thao tác.");
      await load();
      return false;
    } finally {
      setBusy(false);
    }
  };

  const submit = async () => {
    if (!content.trim()) {
      toast.error("Hãy nhập nội dung kiến nghị.");
      return;
    }
    const ok = await run(async () => {
      const appeal = await postPublishService.submitAppeal(record.id, content.trim(), steps);
      for (const file of files) await postPublishService.uploadAppealFile(appeal.id, file);
    }, "Đã gửi kiến nghị. Kết quả được ghi chú \"đang xem xét\" tới khi có trả lời.");
    if (ok) {
      setShowForm(false);
      setContent("");
      setSteps([]);
      setFiles([]);
    }
  };

  const openReopen = async (appeal: AppealDto) => {
    try {
      const actions = await evaluationService.getRecordActions(record.id);
      const reopen = actions.actions.find((a) => a.action === "Reopen");
      if (!reopen?.targetSteps?.length) {
        toast.error("Hiện không mở lại được hồ sơ này (kỳ hoặc trạng thái hồ sơ không cho phép).");
        return;
      }
      setReopenTargets(reopen.targetSteps);
      setReopenAppeal(appeal);
    } catch (err: any) {
      toast.error(err?.message || "Không tải được thao tác của hồ sơ.");
    }
  };

  return (
    <section className="card border-0 shadow-sm">
      <div className="card-body d-flex flex-column gap-3">
        <div className="d-flex justify-content-between align-items-center gap-2">
          <h2 className="h6 mb-0">
            Kiến nghị về kết quả
            {data?.underReview && <span className="badge text-bg-warning ms-2">Đang xem xét kiến nghị</span>}
          </h2>
          {data?.canSubmit && !showForm && (
            <button type="button" className="btn btn-outline-primary btn-sm" onClick={() => setShowForm(true)}>
              <i className="bi bi-chat-square-text me-1" />Gửi kiến nghị
            </button>
          )}
        </div>
        {error && <div className="alert alert-warning mb-0 small">{error}</div>}
        {data?.submitBlockedReason && !data.canSubmit && record.status === "Published" && (
          <div className="small text-secondary">{data.submitBlockedReason}</div>
        )}

        {showForm && data && (
          <div className="border rounded p-3 d-flex flex-column gap-2">
            <label className="form-label small fw-semibold mb-0" htmlFor="appeal-content">Nội dung kiến nghị</label>
            <textarea id="appeal-content" className="form-control form-control-sm" rows={4} maxLength={4000} value={content}
              placeholder="Điểm/mức nào cần xem xét lại, căn cứ và minh chứng kèm theo."
              onChange={(e) => setContent(e.target.value)} />
            {data.stepOptions.length > 0 && (
              <div>
                <div className="small fw-semibold">Kiến nghị liên quan tới bước (người đã thực hiện bước được chọn sẽ không chủ trì xử lý)</div>
                {data.stepOptions.map((option) => (
                  <label key={option.step} className="form-check small mb-0">
                    <input type="checkbox" className="form-check-input" checked={steps.includes(option.step)}
                      onChange={(e) => setSteps(e.target.checked ? [...steps, option.step] : steps.filter((s) => s !== option.step))} />
                    <span className="form-check-label">{option.stepName}{option.actorName ? ` — ${option.actorName}` : ""}</span>
                  </label>
                ))}
              </div>
            )}
            <div>
              <label className="form-label small fw-semibold mb-1" htmlFor="appeal-files">Tệp minh chứng (tùy chọn)</label>
              <input id="appeal-files" type="file" multiple className="form-control form-control-sm" accept=".pdf,.docx,.xlsx,.jpg,.jpeg,.png"
                onChange={(e) => setFiles(Array.from(e.target.files ?? []))} />
            </div>
            <div className="d-flex gap-2 justify-content-end">
              <button type="button" className="btn btn-outline-secondary btn-sm" disabled={busy} onClick={() => setShowForm(false)}>Hủy</button>
              <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={submit}>Gửi kiến nghị</button>
            </div>
          </div>
        )}

        {data && data.appeals.length === 0 && !showForm && <div className="small text-secondary">Chưa có kiến nghị nào.</div>}
        {data?.appeals.map((appeal) => (
          <AppealItem key={appeal.id} appeal={appeal} busy={busy}
            onStartReview={() => run(() => postPublishService.startReview(appeal.id, appeal.version), "Đã nhận xem xét kiến nghị.")}
            onResolve={(accepted, response) => run(() => postPublishService.resolveAppeal(appeal.id, appeal.version, accepted, response), "Đã trả lời kiến nghị.")}
            onAttach={(file) => run(() => postPublishService.uploadAppealFile(appeal.id, file), "Đã gắn tệp vào kiến nghị.")}
            onReopen={() => openReopen(appeal)} />
        ))}
      </div>

      <ReasonDialog
        isOpen={!!reopenAppeal}
        title="Mở lại hồ sơ theo kiến nghị"
        description="Kiến nghị đã được chấp nhận. Hồ sơ quay về bước được chọn để đính chính; lý do được ghi vào lịch sử hồ sơ kèm dẫn chiếu kiến nghị."
        confirmText="Mở lại hồ sơ"
        targetSteps={reopenTargets}
        busy={busy}
        onCancel={() => setReopenAppeal(null)}
        onConfirm={async (reason, targetStep) => {
          if (!reopenAppeal || !targetStep) return;
          const ok = await run(() => postPublishService.reopenFromAppeal(reopenAppeal.id, record.version ?? 0, targetStep, reason), "Đã mở lại hồ sơ theo kiến nghị.");
          if (ok) {
            setReopenAppeal(null);
            onRecordChanged?.();
          }
        }}
      />
    </section>
  );
}

function AppealItem({
  appeal,
  busy,
  onStartReview,
  onResolve,
  onAttach,
  onReopen,
}: {
  appeal: AppealDto;
  busy: boolean;
  onStartReview: () => void;
  onResolve: (accepted: boolean, response: string) => Promise<boolean>;
  onAttach: (file: File) => void;
  onReopen: () => void;
}) {
  const { toast } = useToast();
  const [files, setFiles] = useState<AppealFileDto[]>([]);
  const [resolving, setResolving] = useState(false);
  const [accepted, setAccepted] = useState<boolean | null>(null);
  const [response, setResponse] = useState("");

  useEffect(() => {
    postPublishService.getAppealFiles(appeal.id).then(setFiles).catch(() => setFiles([]));
  }, [appeal.id, appeal.version]);

  const confirmResolve = async () => {
    if (accepted === null || !response.trim()) {
      toast.error("Hãy chọn chấp nhận/không chấp nhận và nhập căn cứ trả lời.");
      return;
    }
    if (await onResolve(accepted, response.trim())) setResolving(false);
  };

  return (
    <div className="border rounded p-3 d-flex flex-column gap-2">
      <div className="d-flex justify-content-between flex-wrap gap-2">
        <div className="small">
          <span className="fw-semibold">{appeal.submittedByName}</span>
          <span className="text-secondary"> · gửi {formatDateTime(appeal.submittedAt)}</span>
        </div>
        <span className={`badge ${STATUS_CLASS[appeal.status] ?? "text-bg-light"}`}>{appeal.statusName}</span>
      </div>
      <div className="small" style={{ whiteSpace: "pre-wrap" }}>{appeal.content}</div>
      {appeal.concernedStepNames.length > 0 && (
        <div className="small text-secondary">Liên quan tới: {appeal.concernedStepNames.join(", ")}</div>
      )}
      {files.length > 0 && (
        <ul className="list-unstyled small mb-0">
          {files.map((f) => (
            <li key={f.id}>
              <button type="button" className="btn btn-link btn-sm p-0 small"
                onClick={() => attachmentService.downloadAttachment(f.id, f.fileName).catch((err: any) => toast.error(err?.message || "Không tải được tệp."))}>
                <i className="bi bi-paperclip me-1" />{f.fileName}
              </button>
            </li>
          ))}
        </ul>
      )}
      {appeal.canAttach && (
        <label className="small mb-0">
          <span className="text-secondary me-2">Bổ sung tệp:</span>
          <input type="file" className="form-control form-control-sm d-inline-block w-auto" disabled={busy} accept=".pdf,.docx,.xlsx,.jpg,.jpeg,.png"
            onChange={(e) => { const file = e.target.files?.[0]; if (file) onAttach(file); e.target.value = ""; }} />
        </label>
      )}
      {appeal.reviewStartedByName && appeal.status === "UnderReview" && (
        <div className="small text-secondary">Đang xem xét: {appeal.reviewStartedByName} ({formatDateTime(appeal.reviewStartedAt)})</div>
      )}
      {appeal.response && (
        <div className="small border-start border-3 ps-2">
          <div className="fw-semibold">Trả lời của {appeal.resolvedByName} ({formatDateTime(appeal.resolvedAt)})</div>
          <div style={{ whiteSpace: "pre-wrap" }}>{appeal.response}</div>
        </div>
      )}
      {appeal.status === "Accepted" && !appeal.reopenedAt && (
        <div className="small text-success">
          Kiến nghị được chấp nhận — nếu cần đính chính kết quả, người có quyền mở lại hồ sơ bằng thao tác &quot;Mở lại hồ sơ&quot;.
        </div>
      )}
      {appeal.reopenedAt && <div className="small text-secondary">Hồ sơ đã được mở lại theo kiến nghị lúc {formatDateTime(appeal.reopenedAt)}.</div>}
      {appeal.resolveBlockedReason && <div className="small text-danger">{appeal.resolveBlockedReason}</div>}

      <div className="d-flex gap-2 flex-wrap">
        {appeal.canResolve && appeal.status === "Submitted" && (
          <button type="button" className="btn btn-outline-secondary btn-sm" disabled={busy} onClick={onStartReview}>Nhận xem xét</button>
        )}
        {appeal.canResolve && !resolving && (
          <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={() => setResolving(true)}>Trả lời kiến nghị</button>
        )}
        {appeal.canReopen && (
          <button type="button" className="btn btn-outline-danger btn-sm" disabled={busy} onClick={onReopen}>Mở lại hồ sơ theo kiến nghị</button>
        )}
      </div>

      {resolving && (
        <div className="border rounded p-2 d-flex flex-column gap-2">
          <div className="d-flex gap-3 small">
            <label className="form-check mb-0">
              <input type="radio" className="form-check-input" name={`accept-${appeal.id}`} checked={accepted === true} onChange={() => setAccepted(true)} />
              <span className="form-check-label">Chấp nhận</span>
            </label>
            <label className="form-check mb-0">
              <input type="radio" className="form-check-input" name={`accept-${appeal.id}`} checked={accepted === false} onChange={() => setAccepted(false)} />
              <span className="form-check-label">Không chấp nhận</span>
            </label>
          </div>
          <textarea className="form-control form-control-sm" rows={3} maxLength={4000} value={response}
            placeholder="Nội dung trả lời — bắt buộc nêu căn cứ chấp nhận hoặc không chấp nhận."
            onChange={(e) => setResponse(e.target.value)} aria-label="Nội dung trả lời" />
          <div className="d-flex gap-2 justify-content-end">
            <button type="button" className="btn btn-outline-secondary btn-sm" disabled={busy} onClick={() => setResolving(false)}>Hủy</button>
            <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={confirmResolve}>Gửi trả lời</button>
          </div>
        </div>
      )}
    </div>
  );
}

export default AppealPanel;
