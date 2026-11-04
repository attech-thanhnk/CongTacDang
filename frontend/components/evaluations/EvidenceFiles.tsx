"use client";

import React, { useCallback, useEffect, useState } from "react";
import { attachmentService, AttachmentItem, EvidenceRights } from "@/services/attachmentService";
import { useToast } from "@/contexts/ToastContext";
import { AttachmentList } from "@/components/attachments/AttachmentList";

/** Tiền tố mã phân loại của tệp minh chứng gắn theo mục của phiếu tự chấm (trục, nhóm tiêu chí chung). */
const EVIDENCE_PREFIX = "MC-";

/** Mã phân loại của tệp minh chứng cho một mục: trục "T1" → "MC-T1", nhóm tiêu chí chung "1" → "MC-N1". */
export function evidenceSlot(kind: "axis" | "group", code: string): string {
  const clean = code.replace(/[^A-Za-z0-9_-]/g, "").toUpperCase();
  return `${EVIDENCE_PREFIX}${kind === "group" ? "N" : ""}${clean}`.slice(0, 20);
}

/** Nạp một lần cho cả phiếu kèm thao tác được làm (máy chủ tính); `revision` đổi → tải lại. */
export function useRecordEvidence(recordId: string, revision?: string | number | null) {
  const [files, setFiles] = useState<AttachmentItem[]>([]);
  const [rights, setRights] = useState<EvidenceRights | null>(null);
  const reload = useCallback(async () => {
    const [list, loadedRights] = await Promise.all([
      attachmentService.getByOwner("EvaluationRecord", recordId).catch(() => [] as AttachmentItem[]),
      attachmentService.getEvidenceRights(recordId).catch(() => null),
    ]);
    setFiles(list.filter((f) => (f.formCode || "").toUpperCase().startsWith(EVIDENCE_PREFIX)));
    setRights(loadedRights);
  }, [recordId]);
  useEffect(() => {
    reload();
  }, [reload, revision]);
  return { files, rights, reload };
}

interface Props {
  recordId: string;
  slot: string;
  /** Tên mục trong trích yếu và nhãn nút, ví dụ "nhóm 1 — Chính trị tư tưởng". */
  label: string;
  files: AttachmentItem[];
  /** Thao tác được làm (từ máy chủ); null khi chưa tải được → chỉ xem. */
  rights: EvidenceRights | null;
  onChanged?: () => void | Promise<void>;
}

/** Tệp minh chứng của một mục; nút thao tác hiện theo `rights` máy chủ trả về. */
export function EvidenceFiles({ recordId, slot, label, files, rights, onChanged }: Props) {
  const { toast } = useToast();
  const mine = files.filter((f) => (f.formCode || "").toUpperCase() === slot);
  if (!rights?.canAdd && mine.length === 0) return null;

  // Tải lại cả danh sách và thao tác được làm (giai đoạn hồ sơ có thể đã đổi), kể cả khi lỗi.
  const after = async (action: () => Promise<unknown>, done: string) => {
    try {
      await action();
      toast.success(done);
    } finally {
      await onChanged?.();
    }
  };

  return (
    <AttachmentList
      files={mine}
      context={label}
      showVersions
      multiple
      emptyText={null}
      onAdd={rights?.canAdd ? (file) => after(() => attachmentService.uploadToRecord(recordId, file, slot, `Minh chứng ${label}`), `Đã đính kèm "${file.name}".`) : undefined}
      onReplace={rights?.canReplace ? (f, file) => after(() => attachmentService.replaceVersion(f.id as string, file), "Đã thay tệp bằng bản mới.") : undefined}
      onRemove={rights?.canRemove ? (f) => after(() => attachmentService.remove(f.id as string), "Đã gỡ tệp minh chứng.") : undefined}
      confirmRemove={{ title: "Gỡ tệp minh chứng", message: (name) => `Gỡ tệp "${name}" khỏi hồ sơ?` }}
    />
  );
}
