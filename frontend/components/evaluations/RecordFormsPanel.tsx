"use client";

import React, { useState } from "react";
import { useToast } from "@/contexts/ToastContext";
import { RecordFormDto, evaluationService } from "@/services/evaluationService";
import { ReportFileFormat, reportService } from "@/services/reportService";
import { RowActions } from "@/components/common/RowActions";
import styles from "./record.module.css";
import { CollapsibleSection } from "./CollapsibleSection";
import { errorMessage } from "@/components/admin/adminUtils";

/** Tên tệp tải về (không dấu), ví dụ "Mau_09A_Nguyen_Van_A.docx". */
function fileName(code: string, fullName: string, format: ReportFileFormat): string {
  const ascii = fullName
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/đ/g, "d")
    .replace(/Đ/g, "D")
    .replace(/[^A-Za-z0-9]+/g, "_")
    .replace(/^_+|_+$/g, "");
  return `Mau_${code}_${ascii || "CanBo"}.${format === "original" ? "docx" : format}`;
}

/** Biểu mẫu cá nhân áp dụng theo bộ tiêu chí của kỳ; khối thu gọn được, mở từ nút "Biểu mẫu" ở đầu hồ sơ. */
export function RecordFormsPanel({
  recordId,
  fullName,
  forms,
  requestForms = [],
  onPreview,
  anchorId,
  defaultOpen = true,
}: {
  recordId: string;
  fullName: string;
  forms: RecordFormDto[];
  /** Mẫu 04/05 theo từng đề nghị thay đổi việc; không có đề nghị thì không hiện. */
  requestForms?: RecordFormDto[];
  /** Mở bản in (PDF) xem trước trong hộp thoại. */
  onPreview?: () => void;
  anchorId?: string;
  defaultOpen?: boolean;
}) {
  const { toast } = useToast();
  const [busy, setBusy] = useState<string | null>(null);
  const all = [...forms, ...requestForms];

  const download = async (code: string, format: ReportFileFormat, requestId?: string | null) => {
    setBusy(`${requestId ?? code}-${format}`);
    try {
      const endpoint = requestId
        ? evaluationService.recordRequestFormEndpoint(recordId, code, requestId)
        : evaluationService.recordFormEndpoint(recordId, code);
      await reportService.downloadReport(endpoint, fileName(code, fullName, format), format);
    } catch (error: unknown) {
      toast.error(errorMessage(error, "Tải biểu mẫu không thành công."));
    } finally {
      setBusy(null);
    }
  };

  return (
    <CollapsibleSection
      title="Biểu mẫu"
      anchorId={anchorId}
      defaultOpen={defaultOpen}
      flush
      tag={all.length > 0 ? <span className={`tag ${styles.tNeutral} num`}>{all.length}</span> : undefined}
    >
      {all.length === 0 ? (
        <div className={`${styles.body} ${styles.small}`}>Kỳ đánh giá chưa có bộ tiêu chí.</div>
      ) : (
        <>
          {forms.length > 0 && onPreview && (
            <div className={styles.filesBar}>
              <button type="button" className="btn btn-outline-secondary btn-sm" onClick={onPreview}>
                <i className="bi bi-printer me-1" />
                Xem bản in
              </button>
            </div>
          )}
          <ul className={styles.files}>
            {all.map((form) => {
              const key = form.requestId ?? form.code;
              return (
                <li key={key}>
                  <span>{form.name.replace(" — ", " · ")}</span>
                  <RowActions
                    itemLabel={form.name}
                    disabled={busy !== null}
                    primary={[
                      {
                        key: "word",
                        label: busy === `${key}-original` ? "Đang tải..." : "Tải Word",
                        icon: "bi-download",
                        onClick: () => download(form.code, "original", form.requestId),
                      },
                    ]}
                    menu={[
                      { key: "pdf", label: "Tải PDF", icon: "bi-file-earmark-pdf", onClick: () => download(form.code, "pdf", form.requestId) },
                      ...(form.hasExcel && !form.requestId
                        ? [{ key: "xlsx", label: "Excel", icon: "bi-file-earmark-excel", onClick: () => download(form.code, "xlsx") }]
                        : []),
                    ]}
                  />
                </li>
              );
            })}
          </ul>
        </>
      )}
    </CollapsibleSection>
  );
}

export default RecordFormsPanel;
