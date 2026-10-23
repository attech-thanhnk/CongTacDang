"use client";

import React, { useId, useRef, useState } from "react";
import styles from "./FileDropZone.module.css";

export interface FileDropZoneProps {
  /** File đang chọn (component được điều khiển từ ngoài). */
  file: File | null;
  /** Gọi khi chọn file hợp lệ hoặc bỏ chọn (null). */
  onChange: (file: File | null) => void;
  /** Phần mở rộng nhận, vd. [".xlsx"]. Rỗng = nhận mọi file. */
  extensions?: string[];
  /** Giá trị `accept` cho hộp chọn file; mặc định ghép từ `extensions`. */
  accept?: string;
  /** Dung lượng tối đa (byte). */
  maxBytes?: number;
  disabled?: boolean;
  id?: string;
  /** Nhãn cho trình đọc màn hình. */
  ariaLabel?: string;
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1).replace(".", ",")} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1).replace(".", ",")} MB`;
}

/** Vùng kéo-thả chọn một file, thay cho input file gốc (chữ của trình duyệt không phải tiếng Việt). */
export function FileDropZone({
  file,
  onChange,
  extensions = [],
  accept,
  maxBytes,
  disabled = false,
  id,
  ariaLabel = "Chọn file",
}: FileDropZoneProps) {
  const autoId = useId();
  const inputId = id ?? `fdz-${autoId}`;
  const input = useRef<HTMLInputElement>(null);
  const [dragging, setDragging] = useState(false);
  const [error, setError] = useState("");
  // Đếm dragenter/dragleave vì sự kiện nổi lên từ phần tử con làm nháy trạng thái.
  const dragDepth = useRef(0);

  const extText = extensions.join(", ");

  const pick = (picked: File | null) => {
    if (!picked) return;
    const name = picked.name.toLowerCase();
    if (extensions.length > 0 && !extensions.some((ext) => name.endsWith(ext.toLowerCase()))) {
      setError(`File "${picked.name}" không đúng định dạng. Chỉ nhận ${extText}.`);
      return;
    }
    if (maxBytes && picked.size > maxBytes) {
      setError(`File "${picked.name}" lớn quá ${formatFileSize(maxBytes)}. Hãy chia thành nhiều file nhỏ hơn.`);
      return;
    }
    setError("");
    onChange(picked);
  };

  const open = () => {
    if (!disabled) input.current?.click();
  };

  const onKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault();
      open();
    }
  };

  const onDragEnter = (e: React.DragEvent) => {
    e.preventDefault();
    if (disabled) return;
    dragDepth.current += 1;
    setDragging(true);
  };
  const onDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    dragDepth.current = Math.max(0, dragDepth.current - 1);
    if (dragDepth.current === 0) setDragging(false);
  };
  const onDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = disabled ? "none" : "copy";
  };
  const onDrop = (e: React.DragEvent) => {
    e.preventDefault();
    dragDepth.current = 0;
    setDragging(false);
    if (disabled) return;
    if (e.dataTransfer.files.length > 1) {
      setError("Chỉ chọn một file mỗi lần.");
      return;
    }
    pick(e.dataTransfer.files[0] ?? null);
  };

  const clear = () => {
    setError("");
    onChange(null);
  };

  const hiddenInput = (
    <input
      id={inputId}
      ref={input}
      type="file"
      className="visually-hidden"
      tabIndex={-1}
      accept={accept ?? (extensions.join(",") || undefined)}
      disabled={disabled}
      onChange={(e) => {
        pick(e.target.files?.[0] ?? null);
        // Xóa giá trị để chọn lại đúng file đó vẫn phát sự kiện change.
        e.target.value = "";
      }}
    />
  );

  return (
    <div>
      {hiddenInput}
      {file ? (
        <div className={`${styles.selected} ${disabled ? styles.disabled : ""}`}>
          <i className={`bi bi-file-earmark-spreadsheet ${styles.fileIcon}`} aria-hidden="true" />
          <div className={styles.fileInfo}>
            <div className={styles.fileName} title={file.name}>
              {file.name}
            </div>
            <div className={styles.fileSize}>{formatFileSize(file.size)}</div>
          </div>
          <button type="button" className={`btn btn-sm btn-outline-secondary ${styles.action}`} disabled={disabled} onClick={open}>
            Đổi file
          </button>
          <button
            type="button"
            className={`btn btn-sm btn-outline-secondary ${styles.iconBtn}`}
            disabled={disabled}
            onClick={clear}
            aria-label="Bỏ chọn file"
            title="Bỏ chọn file"
          >
            <i className="bi bi-x-lg" aria-hidden="true" />
          </button>
        </div>
      ) : (
        <div
          role="button"
          tabIndex={disabled ? -1 : 0}
          aria-label={ariaLabel}
          aria-disabled={disabled || undefined}
          aria-describedby={error ? `${inputId}-error` : undefined}
          className={`${styles.zone} ${dragging ? styles.dragging : ""} ${error ? styles.invalid : ""} ${disabled ? styles.disabled : ""}`}
          onClick={open}
          onKeyDown={onKeyDown}
          onDragEnter={onDragEnter}
          onDragLeave={onDragLeave}
          onDragOver={onDragOver}
          onDrop={onDrop}
        >
          <i className={`bi ${dragging ? "bi-box-arrow-in-down" : "bi-cloud-arrow-up"} ${styles.zoneIcon}`} aria-hidden="true" />
          <div className={styles.zoneText}>
            {dragging ? (
              "Thả file vào đây"
            ) : (
              <>
                Kéo thả file vào đây hoặc <span className={styles.link}>bấm để chọn</span>
              </>
            )}
          </div>
          {(extText || maxBytes) && (
            <div className={styles.zoneHint}>
              {extText && `Định dạng ${extText}`}
              {extText && maxBytes ? " · " : ""}
              {maxBytes ? `tối đa ${formatFileSize(maxBytes)}` : ""}
            </div>
          )}
        </div>
      )}
      {error && (
        <div id={`${inputId}-error`} className={styles.error} role="alert">
          <i className="bi bi-exclamation-circle me-1" aria-hidden="true" />
          {error}
        </div>
      )}
    </div>
  );
}

export default FileDropZone;
