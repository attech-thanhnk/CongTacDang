"use client";

import React, { useEffect, useState } from "react";
import { EvaluationRecordDto, RecordFormDto, evaluationService } from "@/services/evaluationService";
import { reportService } from "@/services/reportService";
import { organizationSettingsService, OrganizationSettings } from "@/services/organizationSettingsService";

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
  const [org, setOrg] = useState<OrganizationSettings | null>(null);

  // Dòng chân trang lấy từ Thông tin đơn vị (không ghi cứng tên đơn vị).
  useEffect(() => {
    if (!isOpen) return;
    let active = true;
    organizationSettingsService
      .get()
      .then((value) => {
        if (active) setOrg(value);
      })
      .catch(() => {
        // Không chặn xem bản PDF khi chưa tải được thông tin đơn vị.
      });
    return () => {
      active = false;
    };
  }, [isOpen]);

  useEffect(() => {
    setCode(initialCode);
  }, [initialCode, isOpen]);

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

  const title = `${form?.name || "Biểu mẫu"} — ${record.fullName}`;

  const handleDownload = () => {
    if (pdfBlob && form) reportService.saveBlob(pdfBlob, `Mau_${form.code}_${safeName(record.fullName)}.pdf`);
  };

  return (
    <div className="fixed inset-0 z-50 overflow-y-auto bg-black/60 backdrop-blur-sm flex justify-center items-start p-4 sm:p-6">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-5xl overflow-hidden border border-slate-300">
        <div className="bg-slate-900 text-white px-6 py-3.5 flex items-center justify-between border-b border-slate-800">
          <div>
            <h3 className="font-bold text-base text-slate-100">{title}</h3>
            <p className="text-xs text-slate-400">Bản PDF do máy chủ sinh từ biểu mẫu Word</p>
          </div>

          <div className="flex items-center space-x-2">
            <button
              onClick={handleDownload}
              disabled={!pdfBlob}
              className="px-3.5 py-1.5 bg-blue-800 hover:bg-blue-900 disabled:opacity-50 disabled:cursor-not-allowed text-white text-xs font-semibold rounded shadow transition cursor-pointer d-inline-flex align-items-center gap-1.5"
            >
              <i className="bi bi-file-earmark-pdf"></i>
              <span>Tải PDF</span>
            </button>
            <button
              onClick={onClose}
              className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 hover:text-white text-xs rounded transition cursor-pointer"
            >
              Đóng
            </button>
          </div>
        </div>

        {forms.length > 1 && (
          <div className="bg-slate-800 px-6 py-2 border-b border-slate-700 flex flex-wrap gap-1.5 text-xs">
            <span className="text-slate-400 py-1 mr-2 self-center font-semibold">Biểu mẫu:</span>
            {forms.map((f) => (
              <button
                key={f.code}
                onClick={() => setCode(f.code)}
                className={`px-2.5 py-1 rounded transition cursor-pointer ${code === f.code ? "bg-blue-800 text-white font-bold" : "bg-slate-700 text-slate-300 hover:bg-slate-600"}`}
              >
                Mẫu {f.code}
              </button>
            ))}
          </div>
        )}

        <div className="bg-slate-100 border-b border-slate-300 px-6 py-2 text-xs text-slate-700">
          <strong>Lưu ý:</strong> Nội dung PDF giống hệt bản Word xuất từ máy chủ. Bấm <strong>&quot;Tải PDF&quot;</strong> để lưu tệp; dùng nút in của trình xem PDF để in.
        </div>

        <div className="p-4 bg-slate-200/60">
          {!form && (
            <div className="bg-white border border-slate-300 rounded p-6 text-sm text-slate-700">
              Kỳ đánh giá không áp dụng biểu mẫu này cho hồ sơ.
            </div>
          )}
          {form && loading && (
            <div className="bg-white border border-slate-300 rounded p-6 text-sm text-slate-600">Đang tạo bản PDF trên máy chủ…</div>
          )}
          {form && !loading && error && (
            <div className="bg-white border border-red-300 rounded p-6 text-sm text-red-700">{error}</div>
          )}
          {form && !loading && !error && pdfUrl && (
            <iframe title={title} src={pdfUrl} className="w-full bg-white border border-slate-300 rounded" style={{ height: "75vh" }} />
          )}
        </div>

        <div className="bg-slate-100 px-6 py-3 border-t border-slate-200 flex justify-between items-center text-xs text-slate-500">
          <span>{org ? [org.superiorPartyName, org.partyCommitteeName].filter(Boolean).join(" — ") : ""}</span>
          <button
            onClick={onClose}
            className="px-4 py-1.5 bg-white border border-slate-300 hover:bg-slate-50 text-slate-700 rounded text-sm cursor-pointer"
          >
            Đóng
          </button>
        </div>
      </div>
    </div>
  );
};

export default EvaluationPdfModal;
