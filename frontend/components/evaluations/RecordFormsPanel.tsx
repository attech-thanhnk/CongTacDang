"use client";

import React, { useState } from "react";
import { useToast } from "@/contexts/ToastContext";
import { RecordFormDto, evaluationService } from "@/services/evaluationService";
import { ReportFileFormat, reportService } from "@/services/reportService";

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

/**
 * Biểu mẫu cá nhân của hồ sơ theo bộ tiêu chí của kỳ (task 18): mỗi mẫu tải Word hoặc PDF từ
 * `GET /reports/docx/record/{recordId}/{mã}` — máy chủ kiểm tra quyền xem hồ sơ.
 */
export function RecordFormsPanel({ recordId, fullName, forms }: { recordId: string; fullName: string; forms: RecordFormDto[] }) {
  const { toast } = useToast();
  const [busy, setBusy] = useState<string | null>(null);

  const download = async (code: string, format: ReportFileFormat) => {
    setBusy(`${code}-${format}`);
    try {
      await reportService.downloadReport(evaluationService.recordFormEndpoint(recordId, code), fileName(code, fullName, format), format);
    } catch (error: any) {
      toast.error(error?.message || "Không xuất được biểu mẫu.");
    } finally {
      setBusy(null);
    }
  };

  return (
    <section className="card border-0 shadow-sm">
      <div className="card-body">
        <h2 className="h6 mb-2">Biểu mẫu của hồ sơ</h2>
        {forms.length === 0 ? (
          <div className="small text-secondary">Kỳ đánh giá chưa có bộ tiêu chí nên chưa có biểu mẫu áp dụng.</div>
        ) : (
          <ul className="list-unstyled mb-0 d-flex flex-column gap-2">
            {forms.map((form) => (
              <li key={form.code} className="d-flex justify-content-between align-items-center gap-2">
                <span className="small">{form.name}</span>
                <span className="btn-group btn-group-sm flex-shrink-0">
                  <button type="button" className="btn btn-outline-primary" disabled={busy !== null} onClick={() => download(form.code, "original")}>
                    {busy === `${form.code}-original` ? <span className="spinner-border spinner-border-sm" /> : <><i className="bi bi-file-earmark-word me-1" />Word</>}
                  </button>
                  <button type="button" className="btn btn-outline-secondary" disabled={busy !== null} onClick={() => download(form.code, "pdf")}>
                    {busy === `${form.code}-pdf` ? <span className="spinner-border spinner-border-sm" /> : <><i className="bi bi-file-earmark-pdf me-1" />PDF</>}
                  </button>
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  );
}

export default RecordFormsPanel;
