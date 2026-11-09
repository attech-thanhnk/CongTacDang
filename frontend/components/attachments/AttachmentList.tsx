"use client";

import React, { useRef, useState } from "react";
import { attachmentService, AttachmentItem } from "@/services/attachmentService";
import { useToast } from "@/contexts/ToastContext";
import { errorMessage } from "@/components/admin/adminUtils";
import { formatDateTime } from "@/lib/dateFormat";
import { ATTACH_ACCEPT, ATTACH_HINT, checkAttachFile, fileKind, formatFileSize } from "./fileRules";
import styles from "./Attachments.module.css";

/** Một tệp trong danh sách; không có `id` là tệp chưa tải lên (không tải xuống được). */
export interface FileEntry {
  id?: string;
  key?: string;
  fileName: string;
  fileSize?: number | null;
  versionNumber?: number;
}

type Action = (file: File) => Promise<unknown> | void;

interface Props {
  files: FileEntry[];
  /** Tên mục dùng trong nhãn nút, ví dụ "nhóm 1" → "Đính kèm tệp — nhóm 1". */
  context?: string;
  onAdd?: Action;
  onReplace?: (entry: FileEntry, file: File) => Promise<unknown> | void;
  onRemove?: (entry: FileEntry) => Promise<unknown> | void;
  /** Hỏi lại trước khi gỡ; false khi gỡ chỉ đổi lựa chọn trên biểu mẫu chưa lưu. */
  confirmRemove?: { title: string; message: (fileName: string) => string } | false;
  /** Đủ số tệp thì ẩn nút thêm (ví dụ một tệp cho mỗi nhiệm vụ). */
  max?: number;
  addLabel?: string;
  emptyText?: string | null;
  /** Hiện lịch sử phiên bản cho tệp đã thay. */
  showVersions?: boolean;
  /** Dòng tệp không viền, dùng trong ô bảng chỉ xem. */
  inline?: boolean;
  /** Nhận nhiều tệp một lần (chỉ khi thêm). */
  multiple?: boolean;
  disabled?: boolean;
}

const keyOf = (f: FileEntry) => f.id ?? f.key ?? f.fileName;

/** Danh sách tệp đính kèm dùng chung: tải xuống, thêm, thay, gỡ, kéo thả; lỗi hiện ngay dưới danh sách. */
export function AttachmentList({
  files,
  context,
  onAdd,
  onReplace,
  onRemove,
  confirmRemove = { title: "Gỡ tệp đính kèm", message: (name) => `Gỡ tệp "${name}"?` },
  max,
  addLabel = "Đính kèm tệp",
  emptyText = "Chưa có tệp",
  showVersions = false,
  multiple = false,
  disabled = false,
  inline = false,
}: Props) {
  const { confirm } = useToast();
  const addRef = useRef<HTMLInputElement>(null);
  const replaceRef = useRef<HTMLInputElement>(null);
  const [replacing, setReplacing] = useState<FileEntry | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [dragging, setDragging] = useState(false);
  const dragDepth = useRef(0);
  const [versionsOf, setVersionsOf] = useState<string | null>(null);
  const [versions, setVersions] = useState<AttachmentItem[] | null>(null);

  const canAdd = !!onAdd && (max == null || files.length < max) && !disabled;
  const suffix = context ? ` — ${context}` : "";

  const run = async (key: string, action: () => Promise<unknown> | void, failure: string) => {
    setBusy(key);
    setError(null);
    try {
      await action();
    } catch (err: unknown) {
      setError(errorMessage(err, failure));
    } finally {
      setBusy(null);
    }
  };

  const addFiles = async (picked: File[]) => {
    if (!onAdd || picked.length === 0) return;
    const invalid = picked.map(checkAttachFile).find(Boolean);
    if (invalid) {
      setError(invalid);
      return;
    }
    await run("add", async () => {
      for (const file of picked) await onAdd(file);
    }, "Tải tệp lên không thành công.");
  };

  const replaceWith = async (target: FileEntry, file: File) => {
    const invalid = checkAttachFile(file);
    if (invalid) {
      setError(invalid);
      return;
    }
    await run(keyOf(target), () => onReplace?.(target, file), "Thay tệp không thành công.");
    setVersionsOf(null);
  };

  const remove = (f: FileEntry) => {
    const go = () => run(keyOf(f), () => onRemove?.(f), "Gỡ tệp không thành công.");
    if (!confirmRemove) {
      go();
      return;
    }
    confirm({ title: confirmRemove.title, message: confirmRemove.message(f.fileName), confirmText: "Gỡ tệp", isDanger: true, onConfirm: go });
  };

  const download = (f: FileEntry) => {
    if (!f.id) return;
    setError(null);
    attachmentService.downloadAttachment(f.id, f.fileName)
      .catch((err: unknown) => setError(errorMessage(err, "Tải tệp xuống không thành công.")));
  };

  const toggleVersions = async (f: FileEntry) => {
    if (!f.id || versionsOf === f.id) {
      setVersionsOf(null);
      return;
    }
    setVersionsOf(f.id);
    setVersions(null);
    try {
      setVersions(await attachmentService.getVersions(f.id));
    } catch (err: unknown) {
      setVersionsOf(null);
      setError(errorMessage(err, "Tải các phiên bản không thành công."));
    }
  };

  const dropHandlers = canAdd ? {
    onDragEnter: (e: React.DragEvent) => { e.preventDefault(); dragDepth.current += 1; setDragging(true); },
    onDragLeave: (e: React.DragEvent) => { e.preventDefault(); dragDepth.current = Math.max(0, dragDepth.current - 1); if (dragDepth.current === 0) setDragging(false); },
    onDragOver: (e: React.DragEvent) => { e.preventDefault(); e.dataTransfer.dropEffect = "copy"; },
    onDrop: (e: React.DragEvent) => {
      e.preventDefault();
      dragDepth.current = 0;
      setDragging(false);
      const dropped = Array.from(e.dataTransfer.files);
      addFiles(multiple ? dropped : dropped.slice(0, 1));
    },
  } : {};

  return (
    <div className={`${styles.attach} ${inline ? styles.inline : ""} ${dragging ? styles.dragging : ""}`} {...dropHandlers}>
      {files.length > 0 && (
        <ul className={styles.list}>
          {files.map((f) => {
            const kind = fileKind(f.fileName);
            const key = keyOf(f);
            const rowBusy = busy === key;
            return (
              <li key={key} className={styles.item}>
                <div className={styles.row}>
                  <i className={`fa-solid ${kind.icon} ${styles.icon} ${styles[kind.tone]}`} aria-hidden="true" />
                  {f.id ? (
                    <button type="button" className={styles.name} title={f.fileName} aria-label={`Tải xuống ${f.fileName}`} onClick={() => download(f)}>
                      {f.fileName}
                    </button>
                  ) : (
                    <span className={`${styles.name} ${styles.nameStatic}`} title={f.fileName}>{f.fileName}</span>
                  )}
                  {f.fileSize ? <span className={styles.size}>{formatFileSize(f.fileSize)}</span> : null}
                  {rowBusy && <span className="spinner-border spinner-border-sm text-secondary" role="status" aria-label="Đang xử lý" />}
                  {showVersions && f.id && (f.versionNumber ?? 1) > 1 && (
                    <button type="button" className={styles.act} title="Các phiên bản" aria-label={`Các phiên bản của tệp ${f.fileName}`}
                      aria-expanded={versionsOf === f.id} onClick={() => toggleVersions(f)}>
                      <i className="fa-solid fa-clock-rotate-left" aria-hidden="true" /><span className={styles.actText}>Bản {f.versionNumber}</span>
                    </button>
                  )}
                  {onReplace && !disabled && (
                    <button type="button" className={styles.act} disabled={!!busy} title="Thay bằng tệp khác" aria-label={`Thay tệp ${f.fileName}`}
                      onClick={() => { setReplacing(f); replaceRef.current?.click(); }}>
                      <i className="fa-solid fa-arrows-rotate" aria-hidden="true" /><span className={styles.actText}>Thay</span>
                    </button>
                  )}
                  {onRemove && !disabled && (
                    <button type="button" className={`${styles.act} ${styles.actDanger}`} disabled={!!busy} title="Gỡ tệp" aria-label={`Gỡ tệp ${f.fileName}`}
                      onClick={() => remove(f)}>
                      <i className="fa-solid fa-trash" aria-hidden="true" /><span className={styles.actText}>Gỡ</span>
                    </button>
                  )}
                </div>
                {versionsOf === f.id && (
                  <ul className={styles.versions} aria-label={`Các phiên bản của tệp ${f.fileName}`}>
                    {versions === null && <li className={styles.versionRow}><span className="spinner-border spinner-border-sm me-1" />Đang tải...</li>}
                    {versions?.map((v) => (
                      <li key={v.id} className={styles.versionRow}>
                        <span className={`${styles.versionMeta} ${v.isCurrent ? styles.versionCurrent : ""}`}>
                          Bản {v.versionNumber}{v.isCurrent ? " (hiện hành)" : ""} · {v.uploadedBy} · {formatDateTime(v.uploadedAt)} · {formatFileSize(v.fileSize)}
                        </span>
                        <button type="button" className={styles.act} aria-label={`Tải xuống bản ${v.versionNumber} của ${f.fileName}`}
                          onClick={() => attachmentService.downloadVersion(f.id as string, v.versionNumber ?? 1, v.fileName)
                            .catch((err: unknown) => setError(errorMessage(err, "Tải phiên bản xuống không thành công.")))}>
                          <i className="fa-solid fa-download" aria-hidden="true" /><span className={styles.actText}>Tải</span>
                        </button>
                      </li>
                    ))}
                  </ul>
                )}
              </li>
            );
          })}
        </ul>
      )}
      {files.length === 0 && !canAdd && emptyText && <span className={styles.empty}>{emptyText}</span>}
      {canAdd && (
        <div className={styles.addLine}>
          <button type="button" className={`btn btn-sm btn-outline-primary ${styles.addBtn}`} disabled={!!busy}
            aria-label={`${addLabel}${suffix}`} onClick={() => { setError(null); addRef.current?.click(); }}>
            {busy === "add" ? <span className="spinner-border spinner-border-sm" aria-hidden="true" /> : <i className="fa-solid fa-paperclip" aria-hidden="true" />}
            {busy === "add" ? "Đang tải lên..." : addLabel}
          </button>
          <span className={styles.hint}>{ATTACH_HINT}</span>
        </div>
      )}
      {error && (
        <div className={styles.error} role="alert">
          <i className="fa-solid fa-circle-exclamation" aria-hidden="true" />
          <span>{error}</span>
        </div>
      )}
      {onAdd && (
        <input ref={addRef} type="file" accept={ATTACH_ACCEPT} multiple={multiple} hidden
          onChange={(e) => { const picked = Array.from(e.target.files ?? []); e.target.value = ""; addFiles(picked); }} />
      )}
      {onReplace && (
        <input ref={replaceRef} type="file" accept={ATTACH_ACCEPT} hidden
          onChange={(e) => {
            const file = e.target.files?.[0];
            const target = replacing;
            e.target.value = "";
            setReplacing(null);
            if (file && target) replaceWith(target, file);
          }} />
      )}
    </div>
  );
}
