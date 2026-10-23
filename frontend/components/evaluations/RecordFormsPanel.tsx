"use client";

import React, { useState } from "react";
import { useToast } from "@/contexts/ToastContext";
import { RecordFormDto, evaluationService } from "@/services/evaluationService";
import { ReportFileFormat, reportService } from "@/services/reportService";
import { RowActions } from "@/components/common/RowActions";
import styles from "./record.module.css";
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
  return `Mau_${code}_${ascii || "CanBo"}.${format === "pdf" ? "pdf" : "docx"}`;
}

/** Biểu mẫu cá nhân áp dụng theo bộ tiêu chí của kỳ. */
export function RecordFormsPanel({
  recordId,
  fullName,
  forms,
  onPreview,
}: {
  recordId: string;
  fullName: string;
  forms: RecordFormDto[];
  /** Mở bản in (PDF) xem trước trong hộp thoại. */
  onPreview?: () => void;
}) {
  const { toast } = useToast();
  const [busy, setBusy] = useState<string | null>(null);

  const download = async (code: string, format: ReportFileFormat) => {
    setBusy(`${code}-${format}`);
    try {
      await reportService.downloadReport(evaluationService.recordFormEndpoint(recordId, code), fileName(code, fullName, format), format);
    } catch (error: unknown) {
      toast.error(errorMessage(error, "Xuất biểu mẫu không thành công."));
    } finally {
      setBusy(null);
    }
  };

  return (
    <section className={styles.block}>
      <div className={styles.blockH}>
        <h2>Biểu mẫu</h2>
        {forms.length > 0 && onPreview && (
          <span className={styles.end}>
            <button type="button" className="btn btn-outline-secondary btn-sm" onClick={onPreview}>
              <i className="bi bi-printer me-1" />
              Xem bản in
            </button>
          </span>
        )}
      </div>
      {forms.length === 0 ? (
        <div className={`${styles.body} ${styles.small}`}>Kỳ đánh giá chưa có bộ tiêu chí.</div>
      ) : (
        <ul className={styles.files}>
          {forms.map((form) => (
            <li key={form.code}>
              <span>{form.name.replace(" — ", " · ")}</span>
              <RowActions
                itemLabel={form.name}
                disabled={busy !== null}
                primary={[
                  {
                    key: "word",
                    label: busy === `${form.code}-original` ? "Đang xuất..." : "Xuất Word",
                    icon: "bi-download",
                    onClick: () => download(form.code, "original"),
                  },
                ]}
                menu={[{ key: "pdf", label: "Xuất PDF", icon: "bi-file-earmark-pdf", onClick: () => download(form.code, "pdf") }]}
              />
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

export default RecordFormsPanel;
