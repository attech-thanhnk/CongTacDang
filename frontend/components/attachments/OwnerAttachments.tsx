"use client";

import React, { useCallback, useEffect, useState } from "react";
import { attachmentService, AttachmentItem, AttachmentOwnerType } from "@/services/attachmentService";
import { useToast } from "@/contexts/ToastContext";
import { errorMessage } from "@/components/admin/adminUtils";
import { AttachmentList } from "./AttachmentList";
import styles from "./Attachments.module.css";

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
  const { toast } = useToast();
  const [open, setOpen] = useState(defaultOpen);
  const [files, setFiles] = useState<AttachmentItem[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

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
  }, [ownerType, ownerId]);

  useEffect(() => {
    if (open && files === null && !loading && !loadError) reload();
  }, [open, files, loading, loadError, reload]);

  return (
    <div className={styles.attach}>
      <button type="button" className={styles.toggle} aria-expanded={open} onClick={() => setOpen(!open)}>
        <i className={`fa-solid ${open ? "fa-chevron-down" : "fa-chevron-right"}`} aria-hidden="true" />
        Tệp đính kèm{files ? <span className={styles.count}>({files.length})</span> : null}
      </button>
      {open && (
        <>
          {loading && files === null && <span className="small text-secondary"><span className="spinner-border spinner-border-sm me-1" />Đang tải...</span>}
          {loadError && (
            <span className="small text-danger">
              {loadError}{" "}
              <button type="button" className="btn btn-link btn-sm p-0 align-baseline" onClick={() => reload()}>Thử lại</button>
            </span>
          )}
          {files && (
            <AttachmentList
              files={files}
              context={subject}
              showVersions
              multiple
              onAdd={editable ? async (file) => {
                await attachmentService.uploadToOwner(ownerType, ownerId, file, formCode, description);
                toast.success(`Đã đính kèm tệp "${file.name}".`);
                await reload();
              } : undefined}
              onReplace={editable ? async (f, file) => {
                await attachmentService.replaceVersion(f.id as string, file);
                toast.success("Đã thay tệp bằng bản mới.");
                await reload();
              } : undefined}
              onRemove={editable ? async (f) => {
                await attachmentService.remove(f.id as string);
                toast.success("Đã gỡ tệp.");
                await reload();
              } : undefined}
              confirmRemove={{ title: "Gỡ tệp đính kèm", message: (name) => `Gỡ tệp "${name}" khỏi ${subject}?` }}
            />
          )}
        </>
      )}
    </div>
  );
}
