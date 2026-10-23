"use client";

import React, { useCallback, useEffect, useRef, useState } from "react";
import { attachmentService, AttachmentItem } from "@/services/attachmentService";
import { useToast } from "@/contexts/ToastContext";
import { errorMessage } from "@/components/admin/adminUtils";

const ACCEPT = ".pdf,.docx,.xlsx,.jpg,.jpeg,.png";

interface Props {
  recordId: string;
  /** Người xem được đính kèm / thay / gỡ tệp của hồ sơ (máy chủ quyết định). */
  editable: boolean;
  /** Mã mẫu của hồ sơ, ví dụ "M07" — lưu làm mã phân loại tệp. */
  formCode?: string;
  description?: string;
}

/** Phụ lục, báo cáo kèm hồ sơ tập thể; danh sách tệp chỉ tải khi mở phần này. */
export function CollectiveAttachments({ recordId, editable, formCode = "M07", description = "Phụ lục, báo cáo kèm theo" }: Props) {
  const { toast, confirm } = useToast();
  const [open, setOpen] = useState(false);
  const [files, setFiles] = useState<AttachmentItem[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [replacing, setReplacing] = useState<AttachmentItem | null>(null);
  const [busy, setBusy] = useState(false);
  const addRef = useRef<HTMLInputElement>(null);
  const replaceRef = useRef<HTMLInputElement>(null);

  const reload = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      setFiles(await attachmentService.getByOwner("CollectiveEvaluationRecord", recordId));
    } catch (error: unknown) {
      setLoadError(errorMessage(error, "Không tải được danh sách tệp đính kèm."));
    } finally {
      setLoading(false);
    }
  }, [recordId]);

  // Đổi hồ sơ → bỏ danh sách cũ, chỉ tải lại khi phần tệp đang mở.
  useEffect(() => {
    setFiles(null);
    setLoadError(null);
  }, [recordId]);

  useEffect(() => {
    if (open && files === null && !loading && !loadError) reload();
  }, [open, files, loading, loadError, reload]);

  const run = async (action: () => Promise<unknown>, done: string, failure: string) => {
    setBusy(true);
    try {
      await action();
      toast.success(done);
      await reload();
    } catch (error: unknown) {
      toast.error(errorMessage(error, failure));
    } finally {
      setBusy(false);
    }
  };

  const onAdd = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (file)
      run(() => attachmentService.uploadToOwner("CollectiveEvaluationRecord", recordId, file, formCode, description),
        "Đã đính kèm tệp.", "Không tải được tệp lên.");
  };

  const onReplace = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    const target = replacing;
    e.target.value = "";
    setReplacing(null);
    if (file && target) run(() => attachmentService.replaceVersion(target.id, file), "Đã thay tệp bằng bản mới.", "Không thay được tệp.");
  };

  const download = (f: AttachmentItem) =>
    attachmentService.downloadAttachment(f.id, f.fileName).catch((error: unknown) => toast.error(errorMessage(error, "Không tải được tệp.")));

  return (
    <div className="d-flex flex-wrap align-items-center gap-2 small mt-2">
      <button type="button" className="btn btn-link btn-sm p-0 text-secondary text-decoration-none" aria-expanded={open}
        onClick={() => setOpen(!open)}>
        <i className={`bi ${open ? "bi-chevron-down" : "bi-chevron-right"} me-1`} />
        Phụ lục, báo cáo kèm theo{files ? ` (${files.length})` : ""}
      </button>
      {open && (
        <>
          {loading && files === null && <span className="text-secondary"><span className="spinner-border spinner-border-sm me-1" />Đang tải...</span>}
          {loadError && (
            <span className="text-danger">
              {loadError}{" "}
              <button type="button" className="btn btn-link btn-sm p-0 align-baseline" onClick={() => reload()}>Thử lại</button>
            </span>
          )}
          {files && files.length === 0 && <span className="text-secondary">chưa có</span>}
          {files?.map((f) => (
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
                      onConfirm: () => run(() => attachmentService.remove(f.id), "Đã gỡ tệp.", "Không gỡ được tệp."),
                    })}>
                    <i className="bi bi-x-lg" />
                  </button>
                </>
              )}
            </span>
          ))}
          {editable && files !== null && (
            <>
              <button type="button" className="btn btn-link btn-sm p-0" disabled={busy} onClick={() => addRef.current?.click()}>
                {busy ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-plus-lg me-1" />}Đính kèm
              </button>
              <input ref={addRef} type="file" accept={ACCEPT} hidden onChange={onAdd} />
              <input ref={replaceRef} type="file" accept={ACCEPT} hidden onChange={onReplace} />
            </>
          )}
        </>
      )}
    </div>
  );
}
