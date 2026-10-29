"use client";

import React, { useCallback, useEffect, useRef, useState } from "react";
import { attachmentService, AttachmentItem, AttachmentOwnerType } from "@/services/attachmentService";
import { useToast } from "@/contexts/ToastContext";
import { errorMessage } from "@/components/admin/adminUtils";
import { formatDateTime } from "@/lib/dateFormat";
import styles from "./Attachments.module.css";

const ACCEPT = ".pdf,.docx,.xlsx,.jpg,.jpeg,.png";

interface Props {
  ownerType: AttachmentOwnerType;
  ownerId: string;
  /** Người xem được tải lên / thay / gỡ tệp (máy chủ quyết định); không thì chỉ xem và tải xuống. */
  editable: boolean;
  /** Mã mẫu, ví dụ "M07" — lưu làm mã phân loại tệp. */
  formCode: string;
  description?: string;
  /** Tên đối tượng trong câu xác nhận gỡ tệp, ví dụ "phiếu kiểm tra". */
  subject?: string;
  /** Mở sẵn danh sách (khối riêng trên trang chi tiết); mặc định thu gọn, chỉ tải khi mở. */
  defaultOpen?: boolean;
}

/** Tệp đính kèm của một đối tượng kèm lịch sử phiên bản; danh sách chỉ tải khi mở phần này. */
export function OwnerAttachments({
  ownerType,
  ownerId,
  editable,
  formCode,
  description = "Tệp đính kèm",
  subject = "hồ sơ",
  defaultOpen = false,
}: Props) {
  const { toast, confirm } = useToast();
  const [open, setOpen] = useState(defaultOpen);
  const [files, setFiles] = useState<AttachmentItem[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [replacing, setReplacing] = useState<AttachmentItem | null>(null);
  const [busy, setBusy] = useState(false);
  const [versionsOf, setVersionsOf] = useState<string | null>(null);
  const [versions, setVersions] = useState<AttachmentItem[] | null>(null);
  const [versionsError, setVersionsError] = useState<string | null>(null);
  const addRef = useRef<HTMLInputElement>(null);
  const replaceRef = useRef<HTMLInputElement>(null);

  const reload = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      setFiles(await attachmentService.getByOwner(ownerType, ownerId));
    } catch (error: unknown) {
      setLoadError(errorMessage(error, "Tải danh sách tệp không thành công."));
    } finally {
      setLoading(false);
    }
  }, [ownerType, ownerId]);

  // Đổi đối tượng → bỏ danh sách cũ, chỉ tải lại khi phần tệp đang mở.
  useEffect(() => {
    setFiles(null);
    setLoadError(null);
    setVersionsOf(null);
  }, [ownerType, ownerId]);

  useEffect(() => {
    if (open && files === null && !loading && !loadError) reload();
  }, [open, files, loading, loadError, reload]);

  const run = async (action: () => Promise<unknown>, done: string, failure: string) => {
    setBusy(true);
    try {
      await action();
      toast.success(done);
      setVersionsOf(null);
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
      run(() => attachmentService.uploadToOwner(ownerType, ownerId, file, formCode, description),
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

  const downloadVersion = (current: AttachmentItem, v: AttachmentItem) =>
    attachmentService.downloadVersion(current.id, v.versionNumber ?? 1, v.fileName)
      .catch((error: unknown) => toast.error(errorMessage(error, "Tải phiên bản xuống không thành công.")));

  const toggleVersions = async (f: AttachmentItem) => {
    if (versionsOf === f.id) {
      setVersionsOf(null);
      return;
    }
    setVersionsOf(f.id);
    setVersions(null);
    setVersionsError(null);
    try {
      setVersions(await attachmentService.getVersions(f.id));
    } catch (error: unknown) {
      setVersionsError(errorMessage(error, "Tải các phiên bản không thành công."));
    }
  };

  return (
    <div className={styles.attach}>
      <button type="button" className={styles.attachToggle} aria-expanded={open} onClick={() => setOpen(!open)}>
        <i className={`bi ${open ? "bi-chevron-down" : "bi-chevron-right"}`} aria-hidden="true" />
        <i className="bi bi-paperclip" aria-hidden="true" />
        Tệp đính kèm{files ? ` (${files.length})` : ""}
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
            <React.Fragment key={f.id}>
              <span className={styles.fileChip}>
                <button type="button" className={styles.fileName} title={`Tải xuống ${f.fileName}`} onClick={() => download(f)}>
                  {f.fileName}
                </button>
                {(f.versionNumber ?? 1) > 1 && (
                  <button type="button" className={styles.fileBtn} title="Các phiên bản" aria-label={`Các phiên bản của tệp ${f.fileName}`}
                    aria-expanded={versionsOf === f.id} onClick={() => toggleVersions(f)}>
                    <i className="bi bi-clock-history" aria-hidden="true" />
                  </button>
                )}
                {editable && (
                  <>
                    <button type="button" className={styles.fileBtn} disabled={busy} title="Thay bằng bản mới"
                      aria-label={`Thay tệp ${f.fileName}`} onClick={() => { setReplacing(f); replaceRef.current?.click(); }}>
                      <i className="bi bi-arrow-repeat" aria-hidden="true" />
                    </button>
                    <button type="button" className={`${styles.fileBtn} ${styles.fileBtnDanger}`} disabled={busy} title="Gỡ tệp" aria-label={`Gỡ tệp ${f.fileName}`}
                      onClick={() => confirm({
                        title: "Gỡ tệp đính kèm",
                        message: `Gỡ tệp "${f.fileName}" khỏi ${subject}? Tệp không còn hiện trong ${subject}.`,
                        confirmText: "Gỡ tệp",
                        isDanger: true,
                        onConfirm: () => run(() => attachmentService.remove(f.id), "Đã gỡ tệp.", "Gỡ tệp không thành công."),
                      })}>
                      <i className="bi bi-x-lg" aria-hidden="true" />
                    </button>
                  </>
                )}
              </span>
              {versionsOf === f.id && (
                <ul className={styles.versions} aria-label={`Các phiên bản của tệp ${f.fileName}`}>
                  {versions === null && !versionsError && (
                    <li className={styles.versionRow}><span className="spinner-border spinner-border-sm me-1" />Đang tải...</li>
                  )}
                  {versionsError && <li className={`${styles.versionRow} text-danger`}>{versionsError}</li>}
                  {versions?.map((v) => (
                    <li key={v.id} className={styles.versionRow}>
                      <span className={`${styles.versionMeta} ${v.isCurrent ? styles.versionCurrent : ""}`}>
                        Phiên bản {v.versionNumber}{v.isCurrent ? " (hiện hành)" : ""} — {v.uploadedBy}, {formatDateTime(v.uploadedAt)},{" "}
                        {attachmentService.formatFileSize(v.fileSize)}
                      </span>
                      <button type="button" className={styles.versionDownload} onClick={() => downloadVersion(f, v)}>
                        <i className="bi bi-download me-1" aria-hidden="true" />Tải xuống
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </React.Fragment>
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
