"use client";

import React, { useState } from "react";
import { useToast } from "@/contexts/ToastContext";
import { RecordFormDto, evaluationService } from "@/services/evaluationService";
import { ReportFileFormat, reportService } from "@/services/reportService";
import styles from "./record.module.css";
import { errorMessage } from "@/components/admin/adminUtils";

/** Tên tệp tải về (không dấu), ví dụ "Mau_09B_Nguyen_Van_A.docx". */
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
      toast.error(errorMessage(error, "Không xuất được biểu mẫu."));
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
              Bản in (PDF)
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
              <span className={styles.fmt}>
                <button type="button" disabled={busy !== null} onClick={() => download(form.code, "original")} aria-label={`${form.name} — Word`}>
                  {busy === `${form.code}-original` ? <span className="spinner-border spinner-border-sm" /> : "Word"}
                </button>
                <button type="button" disabled={busy !== null} onClick={() => download(form.code, "pdf")} aria-label={`${form.name} — PDF`}>
                  {busy === `${form.code}-pdf` ? <span className="spinner-border spinner-border-sm" /> : "PDF"}
                </button>
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

export default RecordFormsPanel;
