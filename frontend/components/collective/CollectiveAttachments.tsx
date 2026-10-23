"use client";

import React, { useCallback, useEffect, useRef, useState } from "react";
import { attachmentService, AttachmentItem } from "@/services/attachmentService";
import { useToast } from "@/contexts/ToastContext";
import { errorMessage } from "@/components/admin/adminUtils";
import styles from "./Collective.module.css";

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
      setLoadError(errorMessage(error, "Tải danh sách tệp không thành công."));
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
        "Đã đính kèm tệp.", "Tải tệp lên không thành công.");
  };

  const onReplace = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    const target = replacing;
    e.target.value = "";
    setReplacing(null);
    if (file && target) run(() => attachmentService.replaceVersion(target.id, file), "Đã thay tệp bằng bản mới.", "Thay tệp không thành công.");
  };

  const download = (f: AttachmentItem) =>
    attachmentService.downloadAttachment(f.id, f.fileName).catch((error: unknown) => toast.error(errorMessage(error, "Tải tệp xuống không thành công.")));

  return (
    <div className={styles.attach}>
      <button type="button" className={styles.attachToggle} aria-expanded={open} onClick={() => setOpen(!open)}>
        <i className={`bi ${open ? "bi-chevron-down" : "bi-chevron-right"}`} aria-hidden="true" />
        <i className="bi bi-paperclip" aria-hidden="true" />
        Tệp kèm theo{files ? ` (${files.length})` : ""}
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
          {files && files.length === 0 && !editable && <span className="text-secondary">Chưa có tệp</span>}
          {files?.map((f) => (
            <span key={f.id} className={styles.fileChip}>
              <button type="button" className={styles.fileName} title={`Tải xuống ${f.fileName}`} onClick={() => download(f)}>
                {f.fileName}
              </button>
              {editable && (
                <>
                  <button type="button" className={styles.fileBtn} disabled={busy} title="Thay bằng bản mới (bản cũ vẫn được lưu)"
                    aria-label={`Thay tệp ${f.fileName}`} onClick={() => { setReplacing(f); replaceRef.current?.click(); }}>
                    <i className="bi bi-arrow-repeat" aria-hidden="true" />
                  </button>
                  <button type="button" className={`${styles.fileBtn} ${styles.fileBtnDanger}`} disabled={busy} title="Gỡ tệp" aria-label={`Gỡ tệp ${f.fileName}`}
                    onClick={() => confirm({
                      title: "Gỡ tệp đính kèm",
                      message: `Gỡ tệp "${f.fileName}" khỏi hồ sơ tập thể? Tệp không còn hiện trong hồ sơ.`,
                      confirmText: "Gỡ tệp",
                      isDanger: true,
                      onConfirm: () => run(() => attachmentService.remove(f.id), "Đã gỡ tệp.", "Gỡ tệp không thành công."),
                    })}>
                    <i className="bi bi-x-lg" aria-hidden="true" />
                  </button>
                </>
              )}
            </span>
          ))}
          {editable && files !== null && (
            <>
              <button type="button" className="btn btn-sm btn-outline-secondary" disabled={busy} onClick={() => addRef.current?.click()}>
                {busy ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-upload me-1" aria-hidden="true" />}Tải lên
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
