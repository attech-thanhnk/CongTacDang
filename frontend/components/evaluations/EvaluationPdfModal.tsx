"use client";

import React, { useEffect, useState } from "react";
import { EvaluationRecordDto, RecordFormDto, evaluationService } from "@/services/evaluationService";
import { reportService } from "@/services/reportService";
import styles from "./pdfViewer.module.css";

interface Props {
  isOpen: boolean;
  onClose: () => void;
  record: EvaluationRecordDto;
  /** Biểu mẫu áp dụng cho hồ sơ theo bộ tiêu chí của kỳ (`GET /reports/docx/record/{recordId}`). */
  forms: RecordFormDto[];
  /** Mã biểu mẫu mở đầu tiên. */
  initialCode: string;
}

const safeName = (value?: string | null) => (value ? value.trim().replace(/\s+/g, "_") : "CanBo");

/**
 * Xem / tải bản PDF của biểu mẫu cá nhân — máy chủ sinh từ cùng template Word và dữ liệu đã lưu
 * (`GET /reports/docx/record/{recordId}/{mã}?format=pdf`, kiểm tra quyền xem hồ sơ). Chỉ các mẫu kỳ áp dụng.
 */
export const EvaluationPdfModal: React.FC<Props> = ({ isOpen, onClose, record, forms, initialCode }) => {
  const [code, setCode] = useState(initialCode);
  const [pdfBlob, setPdfBlob] = useState<Blob | null>(null);
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setCode(initialCode);
  }, [initialCode, isOpen]);

  // Esc để đóng; khóa cuộn trang nền khi đang xem.
  useEffect(() => {
    if (!isOpen) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    document.addEventListener("keydown", onKey);
    const overflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKey);
      document.body.style.overflow = overflow;
    };
  }, [isOpen, onClose]);

  const form = forms.find((f) => f.code === code);
  const endpoint = form ? evaluationService.recordFormEndpoint(record.id, form.code) : null;

  useEffect(() => {
    if (!isOpen || !endpoint) {
      setPdfBlob(null);
      setPdfUrl(null);
      setError(null);
      setLoading(false);
      return;
    }

    let cancelled = false;
    let objectUrl: string | null = null;
    setLoading(true);
    setError(null);
    setPdfBlob(null);
    setPdfUrl(null);

    reportService
      .fetchReportBlob(endpoint, "pdf")
      .then((blob) => {
        if (cancelled) return;
        objectUrl = window.URL.createObjectURL(blob);
        setPdfBlob(blob);
        setPdfUrl(objectUrl);
      })
      .catch((err: any) => {
        if (cancelled) return;
        setError(err?.message || "Không tải được bản PDF từ máy chủ.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
      if (objectUrl) window.URL.revokeObjectURL(objectUrl);
    };
  }, [isOpen, endpoint]);

  if (!isOpen) return null;

  const handleDownload = () => {
    if (pdfBlob && form) reportService.saveBlob(pdfBlob, `Mau_${form.code}_${safeName(record.fullName)}.pdf`);
  };

  return (
    <div className={styles.backdrop} onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div className={styles.dialog} role="dialog" aria-modal="true" aria-label={form?.name || "Biểu mẫu"}>
        <header className={styles.bar}>
          <div className={styles.title}>
            <b className="text-truncate">{form?.name || "Biểu mẫu"}</b>
            <small className="text-truncate">{record.fullName}</small>
          </div>
          {forms.length > 1 && (
            <div className={styles.tabs} role="tablist">
              {forms.map((f) => (
                <button
                  key={f.code}
                  type="button"
                  role="tab"
                  aria-selected={code === f.code}
                  className={code === f.code ? styles.on : undefined}
                  onClick={() => setCode(f.code)}
                  title={f.name}
                >
                  Mẫu {f.code}
                </button>
              ))}
            </div>
          )}
          <div className={styles.actions}>
            <button type="button" className="btn btn-primary btn-sm" onClick={handleDownload} disabled={!pdfBlob}>
              <i className="bi bi-download me-1" />
              Tải PDF
            </button>
            <button type="button" className={styles.close} onClick={onClose} aria-label="Đóng">
              <i className="bi bi-x-lg" />
            </button>
          </div>
        </header>

        <div className={styles.body}>
          {!form && <div className={styles.state}>Kỳ đánh giá không áp dụng biểu mẫu này cho hồ sơ.</div>}
          {form && loading && (
            <div className={styles.state}>
              <span className="spinner-border spinner-border-sm me-2" />
              Đang tạo bản PDF…
            </div>
          )}
          {form && !loading && error && <div className={`${styles.state} text-danger`}>{error}</div>}
          {form && !loading && !error && pdfUrl && (
            <iframe title={form.name} src={`${pdfUrl}#navpanes=0&view=FitH`} className={styles.frame} />
          )}
        </div>
      </div>
    </div>
  );
};

export default EvaluationPdfModal;
