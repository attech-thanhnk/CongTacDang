"use client";

import React, { useEffect, useState } from "react";
import {
  EvaluationRecordDto,
  EvaluationPeriodDto,
  BranchQuotaCheckDto
} from "@/services/evaluationService";
import { reportService } from "@/services/reportService";
import { PrintTemplateType } from "./EvaluationPrintTemplate";

interface Props {
  isOpen: boolean;
  onClose: () => void;
  templateType: PrintTemplateType;
  record?: EvaluationRecordDto | null;
  period?: EvaluationPeriodDto | null;
  /** Giữ để tương thích: bản PDF do máy chủ sinh từ dữ liệu đã lưu, không dùng dữ liệu phía trình duyệt. */
  allRecords?: EvaluationRecordDto[];
  /** Giữ để tương thích: bản PDF do máy chủ sinh từ dữ liệu đã lưu, không dùng dữ liệu phía trình duyệt. */
  branchQuotas?: BranchQuotaCheckDto[];
}

interface PdfSource {
  endpoint: string;
  fileName: string;
}

const safeName = (value?: string | null, fallback = "CanBo") =>
  value ? value.trim().replace(/\s+/g, "_") : fallback;

/**
 * Xác định biểu mẫu máy chủ tương ứng với loại văn bản. Mẫu chưa có template trên máy chủ trả về null.
 * Mọi bản xem/tải đều lấy từ máy chủ (một nguồn số liệu với bản Word/Excel).
 */
const resolveSource = (
  type: PrintTemplateType,
  record?: EvaluationRecordDto | null,
  period?: EvaluationPeriodDto | null
): PdfSource | null => {
  const periodId = record?.periodId || period?.id;
  switch (type) {
    case "mau01":
      return record ? { endpoint: `/reports/docx/mau-01/${record.id}`, fileName: `Mau_01_DangKyNhiemVu_${safeName(record.fullName)}.pdf` } : null;
    case "mau02":
      return record ? { endpoint: `/reports/docx/mau-02/${record.id}`, fileName: `Mau_02_TuDanhGia_${safeName(record.fullName)}.pdf` } : null;
    case "mau10":
      return record ? { endpoint: `/reports/docx/mau-10/${record.id}`, fileName: `Mau_10_PhieuThamDinh_${safeName(record.fullName)}.pdf` } : null;
    case "mau13": {
      if (!periodId) return null;
      const branch = record?.partyCellId ? `&branchId=${record.partyCellId}` : "";
      return {
        endpoint: `/reports/docx/mau-13?periodId=${periodId}${branch}`,
        fileName: `Mau_13_BienBanKiemPhieu_${safeName(record?.partyCellName, "ToanDangBo")}.pdf`
      };
    }
    case "mau14":
      return {
        endpoint: periodId ? `/reports/form-14?periodId=${periodId}` : "/reports/form-14",
        fileName: "Mau_14_TongHopXepLoaiCanBo.pdf"
      };
    case "mau15":
      return {
        endpoint: periodId ? `/reports/form-15?periodId=${periodId}` : "/reports/form-15",
        fileName: "Mau_15_KiemSoatTran20_ChiBo.pdf"
      };
    default:
      // mau09, individual: chưa có template trên máy chủ (chờ chốt danh sách biểu mẫu).
      return null;
  }
};

export const EvaluationPdfModal: React.FC<Props> = ({
  isOpen,
  onClose,
  templateType,
  record,
  period
}) => {
  const [currentType, setCurrentType] = useState<PrintTemplateType>(templateType);
  const [pdfBlob, setPdfBlob] = useState<Blob | null>(null);
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setCurrentType(templateType);
  }, [templateType, isOpen]);

  const source = resolveSource(currentType, record, period);
  const sourceKey = source ? source.endpoint : null;

  useEffect(() => {
    if (!isOpen || !sourceKey) {
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
      .fetchReportBlob(sourceKey, "pdf")
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
  }, [isOpen, sourceKey]);

  if (!isOpen) return null;

  const handleDownload = () => {
    if (pdfBlob && source) reportService.saveBlob(pdfBlob, source.fileName);
  };

  const getTitle = () => {
    switch (currentType) {
      case "mau01":
        return `Bản đăng ký công việc chuyên môn (Đầu quý) — ${record?.fullName || ""}`;
      case "mau02":
        return `Bản tự đánh giá sản phẩm chuyên môn (70đ) — ${record?.fullName || ""}`;
      case "mau09":
        return `Phiếu tự đánh giá tiêu chí chung (30đ) — ${record?.fullName || ""}`;
      case "mau10":
        return `Bản nhận xét đánh giá của Cấp ủy Chi bộ — ${record?.fullName || ""}`;
      case "mau13":
        return `Biên bản kiểm phiếu bỏ phiếu kín của Chi bộ — ${record?.partyCellName || record?.fullName || ""}`;
      case "mau14":
        return `Bảng tổng hợp xếp loại toàn Đảng bộ — ${period?.name || ""}`;
      case "mau15":
        return `Báo cáo kiểm soát trần 20% Hoàn thành xuất sắc — ${period?.name || ""}`;
      case "individual":
      default:
        return `Hồ sơ cá nhân tổng hợp — ${record?.fullName || "Cán bộ"}`;
    }
  };

  const isCadreForm = ["mau01", "mau02", "mau09", "mau10", "mau13", "individual"].includes(currentType);

  const selectorButton = (type: PrintTemplateType, label: string) => (
    <button
      onClick={() => setCurrentType(type)}
      className={`px-2.5 py-1 rounded transition cursor-pointer ${currentType === type ? "bg-blue-800 text-white font-bold" : "bg-slate-700 text-slate-300 hover:bg-slate-600"}`}
    >
      {label}
    </button>
  );

  return (
    <div className="fixed inset-0 z-50 overflow-y-auto bg-black/60 backdrop-blur-sm flex justify-center items-start p-4 sm:p-6">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-5xl overflow-hidden border border-slate-300">
        {/* Modal Header */}
        <div className="bg-slate-900 text-white px-6 py-3.5 flex items-center justify-between border-b border-slate-800">
          <div>
            <h3 className="font-bold text-base text-slate-100">{getTitle()}</h3>
            <p className="text-xs text-slate-400">Bản PDF do máy chủ sinh từ biểu mẫu Word/Excel</p>
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

        {/* Form Selector Bar for Cadre evaluation */}
        {isCadreForm && (
          <div className="bg-slate-800 px-6 py-2 border-b border-slate-700 flex flex-wrap gap-1.5 text-xs">
            <span className="text-slate-400 py-1 mr-2 self-center font-semibold">Chọn loại văn bản:</span>
            {selectorButton("mau01", "Đăng ký nhiệm vụ (Đầu quý)")}
            {selectorButton("mau02", "Tự chấm nhiệm vụ (70đ)")}
            {selectorButton("mau09", "Tự chấm tiêu chí chung (30đ)")}
            {selectorButton("mau10", "Nhận xét Chi bộ")}
            {selectorButton("mau13", "Biên bản kiểm phiếu")}
            {selectorButton("individual", "Hồ sơ tổng hợp cá nhân")}
          </div>
        )}

        {/* Action instruction bar */}
        <div className="bg-slate-100 border-b border-slate-300 px-6 py-2 text-xs text-slate-700 flex items-center justify-between">
          <span>
            <strong>Lưu ý:</strong> Nội dung PDF giống hệt bản Word/Excel xuất từ máy chủ. Bấm <strong>&quot;Tải PDF&quot;</strong> để lưu tệp; dùng nút in của trình xem PDF để in.
          </span>
        </div>

        {/* Document Preview */}
        <div className="p-4 bg-slate-200/60">
          {!source && (
            <div className="bg-white border border-slate-300 rounded p-6 text-sm text-slate-700">
              Mẫu này chưa có template trên máy chủ nên chưa xem/tải PDF được. Danh sách và nội dung biểu mẫu sẽ được bổ sung khi nghiệp vụ chốt.
            </div>
          )}
          {source && loading && (
            <div className="bg-white border border-slate-300 rounded p-6 text-sm text-slate-600">
              Đang tạo bản PDF trên máy chủ…
            </div>
          )}
          {source && !loading && error && (
            <div className="bg-white border border-red-300 rounded p-6 text-sm text-red-700">
              {error}
            </div>
          )}
          {source && !loading && !error && pdfUrl && (
            <iframe
              title={getTitle()}
              src={pdfUrl}
              className="w-full bg-white border border-slate-300 rounded"
              style={{ height: "75vh" }}
            />
          )}
        </div>

        {/* Modal Footer */}
        <div className="bg-slate-100 px-6 py-3 border-t border-slate-200 flex justify-between items-center text-xs text-slate-500">
          <span>Đảng ủy Tổng công ty Quản lý bay Việt Nam — Đảng bộ Công ty TNHH Kỹ thuật Quản lý bay</span>
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
