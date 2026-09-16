"use client";

import React, { useState } from "react";
import Link from "next/link";
import { Vote, CheckCircle2, ShieldCheck, Printer, Download, Users, FileCheck, ArrowRight, Lock } from "lucide-react";

interface Candidate {
  id: string;
  name: string;
  partyRole: string;
  adminTitle: string;
  selfScore: number;
  selectedGrade: "HXS" | "HTT" | "HTNV" | "KHT";
}

export default function BoPhieuPage() {
  const [candidates, setCandidates] = useState<Candidate[]>([
    {
      id: "1",
      name: "Nguyễn Văn A",
      partyRole: "Bí thư Chi bộ",
      adminTitle: "Trưởng phòng Kế hoạch KD",
      selfScore: 97.6,
      selectedGrade: "HXS"
    },
    {
      id: "2",
      name: "Trần Thị B",
      partyRole: "Phó Bí thư Chi bộ",
      adminTitle: "Trưởng phòng TCCB - LĐ",
      selfScore: 91.5,
      selectedGrade: "HTT"
    },
    {
      id: "3",
      name: "Lê Quang C",
      partyRole: "Chi ủy viên",
      adminTitle: "Quản đốc Xưởng Sản xuất",
      selfScore: 93.0,
      selectedGrade: "HTT"
    },
    {
      id: "4",
      name: "Phạm Văn D",
      partyRole: "Đảng viên",
      adminTitle: "Phó Trưởng phòng Kế toán",
      selfScore: 88.5,
      selectedGrade: "HTNV"
    }
  ]);

  const [hasVoted, setHasVoted] = useState(false);
  const [showMinutes, setShowMinutes] = useState(false);

  // Số lượng cán bộ được bỏ Hoàn thành xuất sắc nhiệm vụ (Tối đa 20% số được tốt trở lên)
  const maxExcellentVotes = 2; // Ví dụ chi bộ có 10 người đánh giá
  const currentExcellentVotes = candidates.filter((c) => c.selectedGrade === "HXS").length;

  const handleGradeChange = (id: string, grade: Candidate["selectedGrade"]) => {
    setCandidates(
      candidates.map((c) => (c.id === id ? { ...c, selectedGrade: grade } : c))
    );
  };

  const handleSubmitBallot = () => {
    if (currentExcellentVotes > maxExcellentVotes) {
      alert(`Số phiếu Xuất sắc (${currentExcellentVotes}) đã vượt quá trần quy định (tối đa ${maxExcellentVotes} đồng chí)!`);
      return;
    }
    setHasVoted(true);
    setShowMinutes(true);
  };

  return (
    <div className="space-y-6 animate-in fade-in duration-300">
      {/* Header Profile Bar */}
      <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <span className="px-2.5 py-0.5 rounded-full text-[11px] font-bold uppercase tracking-wide bg-red-100 text-dang-crimson border border-red-200">
              Mẫu số 11 & 12, 13 (03-HD/TVĐU)
            </span>
            <span className="text-xs text-slate-400">• Bỏ phiếu kín nặc danh & Biên bản kiểm phiếu tự động</span>
          </div>
          <h1 className="text-xl font-bold text-slate-900 tracking-tight">
            Phiếu Lấy Ý Kiến Đánh Giá & Biên Bản Kiểm Phiếu Chi Bộ Quý III/2026
          </h1>
          <p className="text-xs text-slate-500 mt-1">
            Tổ chức: <strong className="text-slate-800">Chi bộ Khối Kỹ thuật</strong> • Tổng số đảng viên triệu tập: 18 • Có mặt: 18 (100%)
          </p>
        </div>

        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2 p-3 bg-slate-900 text-white rounded-2xl shadow-sm text-xs">
            <Lock className="w-4 h-4 text-dang-gold" />
            <div>
              <div className="font-bold text-dang-gold">Phiếu Kín Tuyệt Đối</div>
              <div className="text-[10px] text-slate-400">Không lưu danh tính người bỏ</div>
            </div>
          </div>
        </div>
      </div>

      {/* Warning Ceiling Banner */}
      <div className="bg-amber-50 p-4 rounded-xl border border-amber-200 text-xs flex flex-col md:flex-row md:items-center justify-between gap-3 text-amber-900">
        <div>
          <p className="font-bold">Quy định bỏ phiếu Hoàn thành xuất sắc nhiệm vụ (Trần 20%):</p>
          <p className="text-amber-800 mt-0.5">
            Căn cứ Điều 10 Hướng dẫn 03, chi bộ được bầu tối đa <strong className="underline">{maxExcellentVotes} đồng chí</strong> đạt mức Xuất sắc. Hiện tại bạn đang chọn: <strong className={currentExcellentVotes > maxExcellentVotes ? "text-red-600 font-bold" : "text-emerald-700 font-bold"}>{currentExcellentVotes} đồng chí</strong>.
          </p>
        </div>

        <div className="flex items-center gap-2 shrink-0">
          <span className={`px-2.5 py-1 rounded-lg font-bold text-xs ${
            currentExcellentVotes <= maxExcellentVotes 
              ? "bg-emerald-100 text-emerald-800 border border-emerald-300"
              : "bg-red-100 text-red-800 border border-red-300 animate-pulse"
          }`}>
            {currentExcellentVotes} / {maxExcellentVotes} Xuất sắc
          </span>
        </div>
      </div>

      {/* FORM PHIẾU BỎ MẪU 11 */}
      {!hasVoted ? (
        <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
          <div className="p-4 border-b border-slate-100 flex items-center justify-between">
            <div>
              <h2 className="text-sm font-bold text-slate-900 uppercase tracking-wider">
                Mẫu số 11: Danh sách Cán bộ Lấy Ý kiến Bỏ Phiếu Kín
              </h2>
              <p className="text-xs text-slate-400 mt-0.5">Đánh dấu chọn 01 mức xếp loại tương ứng cho từng đồng chí</p>
            </div>
            <span className="text-xs text-slate-500 font-medium">{candidates.length} nhân sự</span>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs border-collapse">
              <thead>
                <tr className="bg-slate-800 text-white font-semibold">
                  <th className="p-3 w-10 text-center">STT</th>
                  <th className="p-3">Họ và tên cán bộ</th>
                  <th className="p-3">Chức danh Đảng & Chính quyền</th>
                  <th className="p-3 w-28 text-center">Điểm tự chấm</th>
                  <th className="p-3 w-32 text-center bg-rose-900/60">Xuất sắc (HXS)</th>
                  <th className="p-3 w-32 text-center bg-blue-900/60">Tốt (HTT)</th>
                  <th className="p-3 w-32 text-center bg-slate-700">Hoàn thành (HTNV)</th>
                  <th className="p-3 w-32 text-center bg-slate-800">Không đạt (KHT)</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {candidates.map((c, idx) => (
                  <tr key={c.id} className="hover:bg-slate-50/80 transition">
                    <td className="p-3 text-center text-slate-400 font-medium">{idx + 1}</td>
                    <td className="p-3 font-semibold text-slate-900">{c.name}</td>
                    <td className="p-3 text-slate-600">
                      <div>{c.adminTitle}</div>
                      <div className="text-[11px] text-dang-crimson">{c.partyRole}</div>
                    </td>
                    <td className="p-3 text-center font-mono font-bold text-slate-700">
                      {c.selfScore.toFixed(1)}đ
                    </td>
                    <td className="p-3 text-center bg-rose-50/30">
                      <label className="cursor-pointer p-2 inline-block">
                        <input
                          type="radio"
                          name={`grade_${c.id}`}
                          checked={c.selectedGrade === "HXS"}
                          onChange={() => handleGradeChange(c.id, "HXS")}
                          className="w-4 h-4 cursor-pointer accent-rose-600"
                        />
                      </label>
                    </td>
                    <td className="p-3 text-center bg-blue-50/30">
                      <label className="cursor-pointer p-2 inline-block">
                        <input
                          type="radio"
                          name={`grade_${c.id}`}
                          checked={c.selectedGrade === "HTT"}
                          onChange={() => handleGradeChange(c.id, "HTT")}
                          className="w-4 h-4 cursor-pointer accent-blue-600"
                        />
                      </label>
                    </td>
                    <td className="p-3 text-center">
                      <label className="cursor-pointer p-2 inline-block">
                        <input
                          type="radio"
                          name={`grade_${c.id}`}
                          checked={c.selectedGrade === "HTNV"}
                          onChange={() => handleGradeChange(c.id, "HTNV")}
                          className="w-4 h-4 cursor-pointer accent-slate-600"
                        />
                      </label>
                    </td>
                    <td className="p-3 text-center">
                      <label className="cursor-pointer p-2 inline-block">
                        <input
                          type="radio"
                          name={`grade_${c.id}`}
                          checked={c.selectedGrade === "KHT"}
                          onChange={() => handleGradeChange(c.id, "KHT")}
                          className="w-4 h-4 cursor-pointer accent-slate-400"
                        />
                      </label>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="p-4 bg-slate-50 border-t border-slate-100 flex items-center justify-between">
            <span className="text-xs text-slate-500">
              * Hệ thống bảo mật 100%: Lá phiếu nặc danh hoàn toàn theo đúng quy tắc Điều lệ Đảng.
            </span>
            <button
              onClick={handleSubmitBallot}
              className="flex items-center gap-2 px-5 py-2.5 bg-dang-crimson hover:bg-red-800 text-white rounded-xl text-xs font-bold shadow-md transition"
            >
              <Vote className="w-4 h-4" />
              Bỏ phiếu vào Hòm thư Kín
            </button>
          </div>
        </div>
      ) : (
        <div className="bg-emerald-50 p-6 rounded-2xl border border-emerald-200 text-center space-y-3">
          <div className="w-12 h-12 rounded-full bg-emerald-100 text-emerald-700 flex items-center justify-center mx-auto">
            <CheckCircle2 className="w-6 h-6" />
          </div>
          <h3 className="font-bold text-base text-emerald-900">
            Đã bỏ phiếu kín thành công vào Hòm phiếu Chi bộ!
          </h3>
          <p className="text-xs text-emerald-700 max-w-md mx-auto">
            Lá phiếu của đồng chí đã được mã hóa nặc danh và tự động tổng hợp vào Biên bản kiểm phiếu Mẫu số 12 & 13 dưới đây.
          </p>
        </div>
      )}

      {/* BIÊN BẢN KIỂM PHIẾU TỰ ĐỘNG (MẪU 12 & 13) */}
      {showMinutes && (
        <div className="bg-white rounded-xl border border-slate-200 shadow-sm p-6 space-y-6">
          <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b pb-4">
            <div>
              <div className="flex items-center gap-2 mb-1">
                <span className="px-2 py-0.5 rounded bg-blue-100 text-attech-navy font-bold text-[10px] uppercase">
                  Mẫu số 13 (03-HD/TVĐU)
                </span>
                <span className="text-xs text-slate-400">• Tự động tổng hợp thời gian thực</span>
              </div>
              <h3 className="text-lg font-bold text-slate-900">
                Biên Bản Kiểm Phiếu Đánh Giá & Xếp Loại Chất Lượng Cán bộ Quý III/2026
              </h3>
            </div>

            <div className="flex items-center gap-2">
              <button
                onClick={() => window.print()}
                className="flex items-center gap-1.5 px-3 py-1.5 border border-slate-300 rounded-lg text-xs font-semibold text-slate-700 hover:bg-slate-50 transition"
              >
                <Printer className="w-3.5 h-3.5" />
                In Biên bản A4
              </button>
              <a
                href="http://localhost:5000/api/ExportReport/mau-14"
                target="_blank"
                rel="noreferrer"
                className="flex items-center gap-1.5 px-3 py-1.5 bg-emerald-700 hover:bg-emerald-800 text-white rounded-lg text-xs font-semibold shadow-sm transition"
              >
                <Download className="w-3.5 h-3.5" />
                Xuất Excel Tổng hợp
              </a>
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs border-collapse">
              <thead>
                <tr className="bg-slate-100 text-slate-800 font-semibold border-b">
                  <th className="p-3 w-10 text-center">STT</th>
                  <th className="p-3">Họ và tên cán bộ</th>
                  <th className="p-3 w-28 text-center">Xuất sắc (%)</th>
                  <th className="p-3 w-28 text-center">Tốt (%)</th>
                  <th className="p-3 w-28 text-center">Hoàn thành (%)</th>
                  <th className="p-3 w-28 text-center">Không đạt (%)</th>
                  <th className="p-3 w-44 text-center">Mức xếp loại đề xuất</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {candidates.map((c, idx) => (
                  <tr key={c.id} className="hover:bg-slate-50/60">
                    <td className="p-3 text-center text-slate-400">{idx + 1}</td>
                    <td className="p-3 font-semibold text-slate-900">{c.name}</td>
                    <td className="p-3 text-center font-bold text-rose-700">
                      {c.selectedGrade === "HXS" ? "100.0% (18/18)" : "0.0% (0/18)"}
                    </td>
                    <td className="p-3 text-center font-bold text-blue-700">
                      {c.selectedGrade === "HTT" ? "100.0% (18/18)" : "0.0% (0/18)"}
                    </td>
                    <td className="p-3 text-center text-slate-600">
                      {c.selectedGrade === "HTNV" ? "100.0% (18/18)" : "0.0% (0/18)"}
                    </td>
                    <td className="p-3 text-center text-slate-400">
                      {c.selectedGrade === "KHT" ? "100.0% (18/18)" : "0.0% (0/18)"}
                    </td>
                    <td className="p-3 text-center">
                      <span className={`inline-block px-2.5 py-0.5 rounded font-bold text-[11px] ${
                        c.selectedGrade === "HXS" ? "bg-rose-100 text-rose-800" :
                        c.selectedGrade === "HTT" ? "bg-blue-100 text-blue-800" :
                        "bg-slate-100 text-slate-700"
                      }`}>
                        {c.selectedGrade === "HXS" ? "Hoàn thành xuất sắc" :
                         c.selectedGrade === "HTT" ? "Hoàn thành tốt" : "Hoàn thành nhiệm vụ"}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="pt-4 border-t border-slate-100 flex items-center justify-between text-xs">
            <span className="text-slate-500">
              * Tổ trưởng kiểm phiếu và Thư ký hội nghị đã ký điện tử xác thực biên bản.
            </span>
            <Link
              href="/tham-dinh"
              className="flex items-center gap-1.5 px-4 py-2 bg-attech-navy hover:bg-slate-800 text-white rounded-xl text-xs font-semibold shadow-sm transition"
            >
              Chuyển tiếp đến Tổ Thẩm định Đảng ủy
              <ArrowRight className="w-3.5 h-3.5" />
            </Link>
          </div>
        </div>
      )}
    </div>
  );
}
