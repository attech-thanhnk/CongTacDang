"use client";

import React, { useCallback, useEffect, useRef, useState } from "react";
import { attachmentService, AttachmentItem } from "@/services/attachmentService";
import { useToast } from "@/contexts/ToastContext";

const ACCEPT = ".pdf,.docx,.xlsx,.jpg,.jpeg,.png";

interface Props {
  recordId: string;
  /** Có quyền lập hồ sơ tập thể trên tổ chức của hồ sơ và kỳ chưa đóng. */
  editable: boolean;
  /** Mã mẫu của hồ sơ, ví dụ "M07" — lưu làm mã phân loại tệp. */
  formCode?: string;
  description?: string;
}

/**
 * Phụ lục, báo cáo kèm hồ sơ tập thể. Tệp thuộc hồ sơ: người xem được hồ sơ tải được tệp; người lập hồ sơ tập thể
 * đính kèm, thay bản mới, gỡ tệp (máy chủ vẫn lưu bản cũ và ghi người gỡ vào nhật ký).
 */
export function CollectiveAttachments({ recordId, editable, formCode = "M07", description = "Phụ lục, báo cáo kèm theo" }: Props) {
  const { toast, confirm } = useToast();
  const [files, setFiles] = useState<AttachmentItem[]>([]);
  const [replacing, setReplacing] = useState<AttachmentItem | null>(null);
  const [busy, setBusy] = useState(false);
  const addRef = useRef<HTMLInputElement>(null);
  const replaceRef = useRef<HTMLInputElement>(null);

  const reload = useCallback(async () => {
    try {
      setFiles(await attachmentService.getByOwner("CollectiveEvaluationRecord", recordId));
    } catch {
      setFiles([]);
    }
  }, [recordId]);

  useEffect(() => {
    reload();
  }, [reload]);

  if (!editable && files.length === 0) return null;

  const run = async (action: () => Promise<unknown>, done: string) => {
    setBusy(true);
    try {
      await action();
      toast.success(done);
      await reload();
    } catch (error: any) {
      toast.error(error?.message || "Không tải được tệp lên.");
    } finally {
      setBusy(false);
    }
  };

  const onAdd = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (file)
      run(() => attachmentService.uploadToOwner("CollectiveEvaluationRecord", recordId, file, formCode, description), "Đã đính kèm tệp.");
  };

  const onReplace = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    const target = replacing;
    e.target.value = "";
    setReplacing(null);
    if (file && target) run(() => attachmentService.replaceVersion(target.id, file), "Đã thay tệp bằng bản mới.");
  };

  const download = (f: AttachmentItem) =>
    attachmentService.downloadAttachment(f.id, f.fileName).catch((error: any) => toast.error(error?.message || "Không tải được tệp."));

  return (
    <div className="d-flex flex-wrap align-items-center gap-2 small mt-2">
      <span className="text-secondary">Phụ lục, báo cáo kèm theo:</span>
      {files.length === 0 && <span className="text-secondary">chưa có</span>}
      {files.map((f) => (
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
                  title: "Gỡ tệp đính kèm",
                  message: `Gỡ tệp "${f.fileName}" khỏi hồ sơ tập thể?`,
                  confirmText: "Gỡ tệp",
                  isDanger: true,
                  onConfirm: () => run(() => attachmentService.remove(f.id), "Đã gỡ tệp."),
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
