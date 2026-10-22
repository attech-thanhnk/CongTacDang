"use client";

import React, { useCallback, useEffect, useRef, useState } from "react";
import { attachmentService, AttachmentItem } from "@/services/attachmentService";
import { useToast } from "@/contexts/ToastContext";
import { errorMessage } from "@/components/admin/adminUtils";

/** Tiền tố mã phân loại của tệp minh chứng gắn theo mục của phiếu tự chấm (trục, nhóm tiêu chí chung). */
const EVIDENCE_PREFIX = "MC-";
const ACCEPT = ".pdf,.docx,.xlsx,.jpg,.jpeg,.png";

/** Mã phân loại của tệp minh chứng cho một mục: trục "T1" → "MC-T1", nhóm tiêu chí chung "1" → "MC-N1". */
export function evidenceSlot(kind: "axis" | "group", code: string): string {
  const clean = code.replace(/[^A-Za-z0-9_-]/g, "").toUpperCase();
  return `${EVIDENCE_PREFIX}${kind === "group" ? "N" : ""}${clean}`.slice(0, 20);
}

/** Tệp minh chứng theo mục của một hồ sơ, nạp một lần cho cả phiếu. */
export function useRecordEvidence(recordId: string) {
  const [files, setFiles] = useState<AttachmentItem[]>([]);
  const reload = useCallback(async () => {
    try {
      const list = await attachmentService.getByOwner("EvaluationRecord", recordId);
      setFiles(list.filter((f) => (f.formCode || "").toUpperCase().startsWith(EVIDENCE_PREFIX)));
    } catch {
      setFiles([]);
    }
  }, [recordId]);
  useEffect(() => {
    reload();
  }, [reload]);
  return { files, reload };
}

interface Props {
  recordId: string;
  slot: string;
  /** Tên mục hiển thị trong trích yếu mặc định, ví dụ "T1 — Nhiệm vụ chính trị…". */
  label: string;
  files: AttachmentItem[];
  editable: boolean;
  onChanged?: () => void;
}

/**
 * Tệp minh chứng (tùy chọn) của một mục trong phiếu tự chấm. Tệp gắn vào hồ sơ; người xem được hồ sơ tải được tệp.
 * Chủ hồ sơ thay được tệp bằng bản mới hoặc gỡ tệp; máy chủ vẫn lưu bản cũ và ghi người gỡ vào nhật ký.
 */
export function EvidenceFiles({ recordId, slot, label, files, editable, onChanged }: Props) {
  const { toast, confirm } = useToast();
  const mine = files.filter((f) => (f.formCode || "").toUpperCase() === slot);
  const addRef = useRef<HTMLInputElement>(null);
  const replaceRef = useRef<HTMLInputElement>(null);
  const [replacing, setReplacing] = useState<AttachmentItem | null>(null);
  const [busy, setBusy] = useState(false);

  if (!editable && mine.length === 0) return null;

  const run = async (action: () => Promise<unknown>, done: string) => {
    setBusy(true);
    try {
      await action();
      toast.success(done);
      onChanged?.();
    } catch (error: unknown) {
      toast.error(errorMessage(error, "Không tải được tệp lên."));
    } finally {
      setBusy(false);
    }
  };

  const onAdd = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (file) run(() => attachmentService.uploadToRecord(recordId, file, slot, `Minh chứng ${label}`), "Đã đính kèm minh chứng.");
  };

  const onReplace = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    const target = replacing;
    e.target.value = "";
    setReplacing(null);
    if (file && target) run(() => attachmentService.replaceVersion(target.id, file), "Đã thay tệp bằng bản mới.");
  };

  const download = (f: AttachmentItem) =>
    attachmentService.downloadAttachment(f.id, f.fileName).catch((error: unknown) => toast.error(errorMessage(error, "Không tải được tệp.")));

  return (
    <div className="d-flex flex-wrap align-items-center gap-2 small mt-1">
      <span className="text-secondary">Tệp minh chứng:</span>
      {mine.length === 0 && <span className="text-secondary">chưa có</span>}
      {mine.map((f) => (
        <span key={f.id} className="d-inline-flex align-items-center gap-1 border rounded px-2 py-0">
          <button type="button" className="btn btn-link btn-sm p-0 text-truncate" style={{ maxWidth: 220 }} title={f.fileName} onClick={() => download(f)}>
            <i className="bi bi-paperclip me-1" />{f.fileName}
          </button>
          {editable && (
            <>
              <button type="button" className="btn btn-link btn-sm p-0 text-secondary" disabled={busy} title="Thay bằng bản mới (bản cũ vẫn được lưu)"
                aria-label={`Thay tệp ${f.fileName}`} onClick={() => { setReplacing(f); replaceRef.current?.click(); }}>
                <i className="bi bi-arrow-repeat" />
              </button>
              <button type="button" className="btn btn-link btn-sm p-0 text-danger" disabled={busy} title="Gỡ tệp" aria-label={`Gỡ tệp ${f.fileName}`}
                onClick={() => confirm({
                  title: "Gỡ tệp minh chứng",
                  message: `Gỡ tệp "${f.fileName}" khỏi hồ sơ?`,
                  confirmText: "Gỡ tệp",
                  isDanger: true,
                  onConfirm: () => run(() => attachmentService.remove(f.id), "Đã gỡ tệp minh chứng."),
                })}>
                <i className="bi bi-x-lg" />
              </button>
            </>
          )}
        </span>
      ))}
      {editable && (
        <>
          <button type="button" className="btn btn-link btn-sm p-0" disabled={busy} onClick={() => addRef.current?.click()}>
            {busy ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-plus-lg me-1" />}Đính kèm
          </button>
          <input ref={addRef} type="file" accept={ACCEPT} hidden onChange={onAdd} />
          <input ref={replaceRef} type="file" accept={ACCEPT} hidden onChange={onReplace} />
        </>
      )}
    </div>
  );
}
