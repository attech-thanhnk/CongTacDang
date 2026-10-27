"use client";

import React from "react";
import { OwnerAttachments } from "@/components/attachments/OwnerAttachments";

interface Props {
  recordId: string;
  /** Người xem được đính kèm / thay / gỡ tệp của hồ sơ (máy chủ quyết định). */
  editable: boolean;
  /** Mã mẫu của hồ sơ, ví dụ "M07" — lưu làm mã phân loại tệp. */
  formCode?: string;
  description?: string;
}

/** Phụ lục, báo cáo kèm hồ sơ tập thể. */
export function CollectiveAttachments({ recordId, editable, formCode = "M07", description = "Phụ lục, báo cáo kèm theo" }: Props) {
  return (
    <OwnerAttachments ownerType="CollectiveEvaluationRecord" ownerId={recordId} editable={editable}
      formCode={formCode} description={description} subject="hồ sơ tập thể" />
  );
}
