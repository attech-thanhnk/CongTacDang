"use client";

import React, { useState } from "react";
import Link from "next/link";
import { 
  CheckCheck, 
  AlertCircle, 
  Download, 
  FileSpreadsheet, 
  ShieldAlert, 
  CheckCircle2, 
  FileText, 
  Search, 
  SlidersHorizontal,
  Paperclip
} from "lucide-react";
import { EvidenceUploadModal } from "../../components/forms/EvidenceUploadModal";

interface CandidateReview {
  id: string;
  name: string;
  partyRole: string;
  adminTitle: string;
  branch: string;
  selfScoreCommon: number;
  selfScoreTasks: number;
  selfScoreTotal: number;
  reviewScoreCommon: number;
  reviewScoreTasks: number;
  reviewScoreTotal: number;
  isExcellentProposed: boolean;
  explanationNeeded: boolean;
  explanationNote?: string;
  condition5Met: boolean;
}

export default function ThamDinhPage() {
  const [candidates, setCandidates] = useState<CandidateReview[]>([
    {
      id: "1",
      name: "Nguyễn Văn A",
      partyRole: "Bí thư Chi bộ",
      adminTitle: "Trưởng phòng Kế hoạch KD",
      branch: "Chi bộ Khối Kỹ thuật",
      selfScoreCommon: 30.0,
      selfScoreTasks: 67.6,
      selfScoreTotal: 97.6,
      reviewScoreCommon: 30.0,
      reviewScoreTasks: 67.6,
      reviewScoreTotal: 97.6,
      isExcellentProposed: true,
      explanationNeeded: false,
      condition5Met: true
    },
    {
      id: "2",
      name: "Trần Thị B",
      partyRole: "Phó Bí thư Chi bộ",
      adminTitle: "Trưởng phòng TCCB - LĐ",
      branch: "Chi bộ Cơ quan Văn phòng",
      selfScoreCommon: 29.5,
      selfScoreTasks: 62.0,
      selfScoreTotal: 91.5,
      reviewScoreCommon: 28.0,
      reviewScoreTasks: 58.0,
      reviewScoreTotal: 86.0,
      isExcellentProposed: false,
      explanationNeeded: true,
      explanationNote: "Tổ thẩm định đánh giá lại tiến độ thẩm định đề án nhân sự bị chậm 05 ngày; chênh lệch 5.5 điểm -> Yêu cầu giải trình Mẫu 10.",
      condition5Met: false
    },
    {
      id: "3",
      name: "Lê Quang C",
      partyRole: "Bí thư Chi bộ",
      adminTitle: "Quản đốc Xưởng Sản xuất",
      branch: "Chi bộ Khối Sản xuất",
      selfScoreCommon: 29.0,
      selfScoreTasks: 65.0,
      selfScoreTotal: 94.0,
      reviewScoreCommon: 29.0,
      reviewScoreTasks: 63.0,
      reviewScoreTotal: 92.0,
      isExcellentProposed: true,
      explanationNeeded: false,
      condition5Met: true
    },
    {
      id: "4",
      name: "Phạm Văn D",
      partyRole: "Đảng viên",
      adminTitle: "Phó Trưởng phòng Kế toán",
      branch: "Chi bộ Cơ quan Văn phòng",
      selfScoreCommon: 28.5,
      selfScoreTasks: 60.0,
      selfScoreTotal: 88.5,
      reviewScoreCommon: 28.5,
      reviewScoreTasks: 60.0,
      reviewScoreTotal: 88.5,
      isExcellentProposed: false,
      explanationNeeded: false,
      condition5Met: false
    }
  ]);

  // State Mẫu 10 Modal
  const [selectedCandidate, setSelectedCandidate] = useState<CandidateReview | null>(null);
  const [mau10Note, setMau10Note] = useState("");

  // State Upload Modal
  const [uploadModalOpen, setUploadModalOpen] = useState(false);
  const [uploadTaskTitle, setUploadTaskTitle] = useState("");

  // Số liệu tổng hợp & Trần 20%
  const totalEvaluated = 68; // 68 Cán bộ quản lý ATTECH
  const maxExcellentCap = Math.floor(totalEvaluated * 0.20); // 13 đồng chí
  const proposedExcellentCount = candidates.filter(c => c.isExcellentProposed).length + 10; // Giả lập tổng số 12

  const handleOpenMau10 = (candidate: CandidateReview) => {
    setSelectedCandidate(candidate);
    setMau10Note(candidate.explanationNote || "");
  };

  const handleSaveMau10 = () => {
    if (!selectedCandidate) return;
    setCandidates(candidates.map(c => 
      c.id === selectedCandidate.id ? { ...c, explanationNote: mau10Note } : c
    ));
    alert(`Đã lưu Biên bản Phiếu Thẩm định Mẫu 10 cho đồng chí: ${selectedCandidate.name}`);
    setSelectedCandidate(null);
  };

  return (
    <div className="space-y-6 animate-in fade-in duration-300">
      {/* Header Profile Bar */}
      <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <span className="px-2.5 py-0.5 rounded-full text-[11px] font-bold uppercase tracking-wide bg-purple-100 text-purple-900 border border-purple-200">
              Tổ Thẩm định & Ban Thường vụ
            </span>
            <span className="text-xs text-slate-400">• Đối soát chênh lệch ≥ 5đ (Mẫu 10) & Trần 20% (Mẫu 15)</span>
          </div>
          <h1 className="text-xl font-bold text-slate-900 tracking-tight">
            Thẩm định Hồ sơ, Xử lý Chênh lệch Điểm & Kiểm soát Trần Xuất sắc
          </h1>
          <p className="text-xs text-slate-500 mt-1">
            Đảng ủy Công ty TNHH Kỹ thuật Quản lý bay (ATTECH) • Kỳ đánh giá: Quý III/2026
          </p>
        </div>

        {/* Excel Export Buttons */}
        <div className="flex items-center gap-2">
          <a
            href="http://localhost:5000/api/ExportReport/mau-14"
            target="_blank"
            rel="noreferrer"
            className="flex items-center gap-2 px-3.5 py-2 bg-emerald-700 hover:bg-emerald-800 text-white rounded-xl text-xs font-semibold shadow-sm transition"
          >
            <FileSpreadsheet className="w-4 h-4" />
            Xuất Excel Mẫu 14
          </a>
          <a
            href="http://localhost:5000/api/ExportReport/mau-15"
            target="_blank"
            rel="noreferrer"
            className="flex items-center gap-2 px-3.5 py-2 bg-slate-800 hover:bg-slate-900 text-white rounded-xl text-xs font-semibold shadow-sm transition"
          >
            <Download className="w-4 h-4" />
            Xuất Excel Mẫu 15
          </a>
        </div>
      </div>

      {/* KPI Ceiling Card: Trần 20% Xuất sắc */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div className="bg-white p-5 rounded-xl border border-slate-200 shadow-sm">
          <span className="text-[11px] font-bold uppercase tracking-wider text-slate-400">Tổng cán bộ quản lý</span>
          <div className="text-3xl font-black text-slate-900 mt-1">{totalEvaluated} <span className="text-sm font-normal text-slate-500">cán bộ</span></div>
          <p className="text-xs text-slate-400 mt-1">4 Chi bộ trực thuộc ATTECH</p>
        </div>

        <div className="bg-white p-5 rounded-xl border border-slate-200 shadow-sm">
          <span className="text-[11px] font-bold uppercase tracking-wider text-slate-400">Hạn ngạch Xuất sắc tối đa (Trần 20%)</span>
          <div className="text-3xl font-black text-dang-crimson mt-1">{maxExcellentCap} <span className="text-sm font-normal text-slate-500">chỉ tiêu</span></div>
          <p className="text-xs text-slate-400 mt-1">Căn cứ Khoản 2 Điều 10 Hướng dẫn 03</p>
        </div>

        <div className={`p-5 rounded-xl border shadow-sm ${
          proposedExcellentCount <= maxExcellentCap
            ? "bg-emerald-50 border-emerald-200 text-emerald-900"
            : "bg-red-50 border-red-200 text-red-900"
        }`}>
          <div className="flex items-center justify-between">
            <span className="text-[11px] font-bold uppercase tracking-wider">Đề xuất hiện tại</span>
            {proposedExcellentCount <= maxExcellentCap ? (
              <span className="px-2 py-0.5 rounded bg-emerald-200 text-emerald-800 font-bold text-[10px]">HỢP LỆ (≤ 20%)</span>
            ) : (
              <span className="px-2 py-0.5 rounded bg-red-200 text-red-800 font-bold text-[10px] animate-pulse">VƯỢT TRẦN!</span>
            )}
          </div>
          <div className="text-3xl font-black mt-1">{proposedExcellentCount} <span className="text-sm font-normal">/ {maxExcellentCap}</span></div>
          <p className="text-xs opacity-80 mt-1">Tỷ lệ thực tế: {((proposedExcellentCount/totalEvaluated)*100).toFixed(1)}%</p>
        </div>
      </div>

      {/* Review Table */}
      <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
        <div className="p-4 border-b border-slate-100 flex items-center justify-between">
          <h2 className="text-sm font-bold text-slate-900 uppercase tracking-wider">
            Bảng Đối Soát Điểm Tự Chấm vs Điểm Thẩm Định & Kích Hoạt Mẫu 10
          </h2>
          <span className="text-xs text-slate-500">Cảnh báo đỏ tự động khi chênh lệch ≥ 5.0 điểm</span>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs border-collapse">
            <thead>
              <tr className="bg-slate-800 text-white font-semibold">
                <th className="p-3 w-10 text-center">STT</th>
                <th className="p-3">Họ và tên cán bộ</th>
                <th className="p-3">Chức vụ & Chi bộ</th>
                <th className="p-3 w-24 text-center">Tự chấm (100đ)</th>
                <th className="p-3 w-24 text-center">Thẩm định (100đ)</th>
                <th className="p-3 w-24 text-center">Chênh lệch</th>
                <th className="p-3 w-28 text-center">5 Đ/K Xuất sắc?</th>
                <th className="p-3 text-center w-36">Thao tác Thẩm định</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {candidates.map((c, idx) => {
                const diff = Math.abs(c.selfScoreTotal - c.reviewScoreTotal);
                const isWarning = diff >= 5.0;

                return (
                  <tr key={c.id} className={`hover:bg-slate-50/80 transition ${isWarning ? "bg-red-50/40" : ""}`}>
                    <td className="p-3 text-center text-slate-400 font-medium">{idx + 1}</td>
                    <td className="p-3 font-semibold text-slate-900">
                      <div>{c.name}</div>
                      <div className="text-[11px] text-slate-400 font-normal">{c.adminTitle}</div>
                    </td>
                    <td className="p-3">
                      <div className="font-semibold text-dang-crimson">{c.branch}</div>
                      <div className="text-[11px] text-slate-500">{c.partyRole}</div>
                    </td>
                    <td className="p-3 text-center font-mono font-bold text-slate-700">
                      {c.selfScoreTotal.toFixed(1)}
                    </td>
                    <td className="p-3 text-center font-mono font-bold text-attech-navy">
                      {c.reviewScoreTotal.toFixed(1)}
                    </td>
                    <td className="p-3 text-center">
                      <span className={`inline-block px-2 py-0.5 rounded font-mono font-bold text-[11px] ${
                        isWarning 
                          ? "bg-red-100 text-red-700 border border-red-300 animate-pulse" 
                          : "bg-slate-100 text-slate-700"
                      }`}>
                        {diff > 0 ? `-${diff.toFixed(1)}` : "0.0"}
                      </span>
                    </td>
                    <td className="p-3 text-center">
                      {c.condition5Met ? (
                        <span className="px-2 py-0.5 rounded bg-emerald-50 text-emerald-700 border border-emerald-200 font-bold text-[10px]">
                          ✓ Đạt 5 Đ/K
                        </span>
                      ) : (
                        <span className="px-2 py-0.5 rounded bg-slate-100 text-slate-500 text-[10px]">
                          Không đề xuất
                        </span>
                      )}
                    </td>
                    <td className="p-3 text-center">
                      {isWarning ? (
                        <button
                          onClick={() => handleOpenMau10(c)}
                          className="px-2.5 py-1.5 bg-red-600 hover:bg-red-700 text-white rounded-lg text-[11px] font-bold shadow-sm flex items-center gap-1 mx-auto transition"
                        >
                          <FileText className="w-3 h-3" />
                          Phiếu Mẫu 10 ({diff.toFixed(1)}đ)
                        </button>
                      ) : (
                        <button
                          onClick={() => alert(`Hồ sơ hợp lệ, chênh lệch ${diff.toFixed(1)} điểm nằm trong ngưỡng cho phép (<5đ).`)}
                          className="px-2.5 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-lg text-[11px] font-semibold transition"
                        >
                          Đã khớp điểm
                        </button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>

        <div className="p-4 bg-slate-50 border-t border-slate-100 flex items-center justify-between text-xs">
          <span className="text-slate-500">
            * Căn cứ Điều 9 Hướng dẫn 03: Mọi trường hợp chênh lệch từ <strong>5.0 điểm trở lên</strong> giữa điểm tự chấm và điểm thẩm định bắt buộc phải lập <strong>Phiếu thẩm định (Mẫu 10)</strong> có giải trình của cán bộ.
          </span>
        </div>
      </div>

      {/* Modal Phiếu Mẫu 10 */}
      {selectedCandidate && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 animate-in fade-in">
          <div className="bg-white rounded-2xl p-6 max-w-xl w-full shadow-2xl border border-slate-200">
            <div className="flex items-center gap-2 mb-2">
              <span className="px-2.5 py-0.5 rounded-full text-[11px] font-bold uppercase bg-red-100 text-dang-crimson border border-red-200">
                Phiếu Mẫu số 10 (03-HD/TVĐU)
              </span>
            </div>
            <h3 className="font-bold text-base text-slate-900">
              Phiếu Thẩm định Chênh lệch Điểm Đánh giá
            </h3>
            <p className="text-xs text-slate-500 mb-4">
              Cán bộ: <strong>{selectedCandidate.name}</strong> • Điểm tự chấm: {selectedCandidate.selfScoreTotal}đ • Điểm thẩm định: {selectedCandidate.reviewScoreTotal}đ (Vênh {Math.abs(selectedCandidate.selfScoreTotal - selectedCandidate.reviewScoreTotal).toFixed(1)}đ)
            </p>

            <div className="space-y-4 text-xs">
              <div>
                <label className="block font-semibold text-slate-700 mb-1">
                  Ý kiến đánh giá & Nguyên nhân chênh lệch của Tổ Thẩm định:
                </label>
                <textarea
                  rows={4}
                  value={mau10Note}
                  onChange={(e) => setMau10Note(e.target.value)}
                  className="w-full p-3 border border-slate-200 rounded-xl text-xs focus:outline-none focus:border-attech-blue"
                  placeholder="Ghi rõ nội dung tiêu chí/nhiệm vụ không đạt chuẩn hoặc vượt chuẩn..."
                />
              </div>

              <div className="p-3 bg-amber-50 rounded-xl border border-amber-200 flex items-center justify-between">
                <span className="text-amber-900 font-medium">Tệp biên bản giải trình đính kèm:</span>
                <button
                  onClick={() => {
                    setUploadTaskTitle(`Giải trình Mẫu 10: ${selectedCandidate.name}`);
                    setUploadModalOpen(true);
                  }}
                  className="flex items-center gap-1 px-3 py-1.5 bg-amber-700 hover:bg-amber-800 text-white rounded-lg font-semibold text-[11px] shadow-sm transition"
                >
                  <Paperclip className="w-3.5 h-3.5" />
                  Đính kèm Biên bản Mẫu 10
                </button>
              </div>
            </div>

            <div className="flex justify-end gap-2 mt-6 pt-3 border-t border-slate-100">
              <button
                onClick={() => setSelectedCandidate(null)}
                className="px-4 py-2 border border-slate-200 rounded-xl text-slate-600 hover:bg-slate-50 text-xs font-semibold"
              >
                Hủy bỏ
              </button>
              <button
                onClick={handleSaveMau10}
                className="px-4 py-2 bg-dang-crimson hover:bg-red-800 text-white rounded-xl text-xs font-bold shadow-md"
              >
                Lưu & Ký Biên bản Mẫu 10
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal Upload Tệp Minh Chứng */}
      <EvidenceUploadModal
        isOpen={uploadModalOpen}
        onClose={() => setUploadModalOpen(false)}
        taskTitle={uploadTaskTitle}
        formCode="M10"
      />
    </div>
  );
}
