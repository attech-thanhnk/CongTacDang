"use client";

import React, { useState, useEffect } from "react";
import {
  EvaluationRecordDto,
  EvaluationPeriodDto,
  BranchQuotaCheckDto
} from "@/services/evaluationService";
import { EvaluationPrintTemplate, PrintTemplateType } from "./EvaluationPrintTemplate";

interface Props {
  isOpen: boolean;
  onClose: () => void;
  templateType: PrintTemplateType;
  record?: EvaluationRecordDto | null;
  period?: EvaluationPeriodDto | null;
  allRecords?: EvaluationRecordDto[];
  branchQuotas?: BranchQuotaCheckDto[];
}

export const EvaluationPdfModal: React.FC<Props> = ({
  isOpen,
  onClose,
  templateType,
  record,
  period,
  allRecords = [],
  branchQuotas = []
}) => {
  const [currentType, setCurrentType] = useState<PrintTemplateType>(templateType);

  useEffect(() => {
    setCurrentType(templateType);
  }, [templateType, isOpen]);

  if (!isOpen) return null;

  const handlePrint = () => {
    window.print();
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
        return `Biên bản kiểm phiếu bỏ phiếu kín của Chi bộ — ${record?.fullName || ""}`;
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

  return (
    <div className="fixed inset-0 z-50 overflow-y-auto bg-black/60 backdrop-blur-sm flex justify-center items-start p-4 sm:p-6 print:p-0 print:bg-white">
      {/* Dynamic Print CSS */}
      <style jsx global>{`
        @media print {
          @page {
            size: ${currentType === "mau14" ? "A4 landscape" : "A4 portrait"};
            margin: ${currentType === "mau14" ? "10mm" : "12mm 12mm 12mm 15mm"};
          }
          body {
            background: white !important;
            color: black !important;
            -webkit-print-color-adjust: exact !important;
            print-color-adjust: exact !important;
          }
          body * {
            visibility: hidden;
          }
          #printable-party-document,
          #printable-party-document * {
            visibility: visible;
          }
          #printable-party-document {
            position: absolute !important;
            left: 0 !important;
            top: 0 !important;
            width: 100% !important;
            margin: 0 !important;
            padding: 0 !important;
            box-shadow: none !important;
          }
          .no-print {
            display: none !important;
          }
          .avoid-break,
          tr,
          thead,
          .signature-box,
          .party-doc-print table {
            page-break-inside: avoid !important;
            break-inside: avoid !important;
          }
        }
      `}</style>

      <div className="bg-white rounded-xl shadow-2xl w-full max-w-5xl overflow-hidden border border-slate-300 print:border-none print:shadow-none print:w-full print:max-w-none">
        {/* Modal Header */}
        <div className="no-print bg-slate-900 text-white px-6 py-3.5 flex items-center justify-between border-b border-slate-800">
          <div>
            <h3 className="font-bold text-base text-slate-100">{getTitle()}</h3>
            <p className="text-xs text-slate-400">Thể thức văn bản chuẩn Đảng ủy VATM</p>
          </div>

          <div className="flex items-center space-x-2">
            <button
              onClick={handlePrint}
              className="px-3.5 py-1.5 bg-blue-800 hover:bg-blue-900 text-white text-xs font-semibold rounded shadow transition cursor-pointer d-inline-flex align-items-center gap-1.5"
            >
              <i className="bi bi-printer"></i>
              <span>Lưu PDF / In tài liệu</span>
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
          <div className="no-print bg-slate-800 px-6 py-2 border-b border-slate-700 flex flex-wrap gap-1.5 text-xs">
            <span className="text-slate-400 py-1 mr-2 self-center font-semibold">Chọn loại văn bản:</span>
            <button
              onClick={() => setCurrentType("mau01")}
              className={`px-2.5 py-1 rounded transition cursor-pointer ${currentType === "mau01" ? "bg-blue-800 text-white font-bold" : "bg-slate-700 text-slate-300 hover:bg-slate-600"}`}
            >
              Đăng ký nhiệm vụ (Đầu quý)
            </button>
            <button
              onClick={() => setCurrentType("mau02")}
              className={`px-2.5 py-1 rounded transition cursor-pointer ${currentType === "mau02" ? "bg-blue-800 text-white font-bold" : "bg-slate-700 text-slate-300 hover:bg-slate-600"}`}
            >
              Tự chấm nhiệm vụ (70đ)
            </button>
            <button
              onClick={() => setCurrentType("mau09")}
              className={`px-2.5 py-1 rounded transition cursor-pointer ${currentType === "mau09" ? "bg-blue-800 text-white font-bold" : "bg-slate-700 text-slate-300 hover:bg-slate-600"}`}
            >
              Tự chấm tiêu chí chung (30đ)
            </button>
            <button
              onClick={() => setCurrentType("mau10")}
              className={`px-2.5 py-1 rounded transition cursor-pointer ${currentType === "mau10" ? "bg-blue-800 text-white font-bold" : "bg-slate-700 text-slate-300 hover:bg-slate-600"}`}
            >
              Nhận xét Chi bộ
            </button>
            <button
              onClick={() => setCurrentType("mau13")}
              className={`px-2.5 py-1 rounded transition cursor-pointer ${currentType === "mau13" ? "bg-blue-800 text-white font-bold" : "bg-slate-700 text-slate-300 hover:bg-slate-600"}`}
            >
              Biên bản kiểm phiếu
            </button>
            <button
              onClick={() => setCurrentType("individual")}
              className={`px-2.5 py-1 rounded transition cursor-pointer ${currentType === "individual" ? "bg-blue-800 text-white font-bold" : "bg-slate-700 text-slate-300 hover:bg-slate-600"}`}
            >
              Hồ sơ tổng hợp cá nhân
            </button>
          </div>
        )}

        {/* Action instruction bar */}
        <div className="no-print bg-slate-100 border-b border-slate-300 px-6 py-2 text-xs text-slate-700 flex items-center justify-between">
          <span><strong>Lưu ý:</strong> Bấm nút <strong>"Lưu PDF / In tài liệu"</strong>, tại hộp thoại in chọn <strong>"Destination: Save as PDF"</strong> để lưu tệp PDF.</span>
          <span className="font-mono text-slate-500">Khổ: A4 ({currentType === "mau14" ? "Ngang" : "Dọc"})</span>
        </div>

        {/* Document Preview Canvas */}
        <div className="p-6 bg-slate-200/60 overflow-x-auto max-h-[80vh] print:p-0 print:max-h-none print:bg-white print:overflow-visible">
          <div id="printable-party-document" className="bg-white shadow-xl mx-auto border border-slate-300 print:border-none print:shadow-none">
            <EvaluationPrintTemplate
              templateType={currentType}
              record={record}
              period={period}
              allRecords={allRecords}
              branchQuotas={branchQuotas}
            />
          </div>
        </div>

        {/* Modal Footer */}
        <div className="no-print bg-slate-100 px-6 py-3 border-t border-slate-200 flex justify-between items-center text-xs text-slate-500">
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
