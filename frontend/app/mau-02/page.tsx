"use client";

import React, { useState } from "react";
import Link from "next/link";
import { UserCheck, CheckCircle2, AlertCircle, Paperclip, Send, ArrowRight, Award, ShieldAlert } from "lucide-react";
import { EvidenceUploadModal } from "../../components/forms/EvidenceUploadModal";

export default function Mau02Page() {
  // Tiêu chí chung: Tối đa 30đ (18 - 4 - 8)
  const [tc1, setTc1] = useState<number>(18);
  const [tc2, setTc2] = useState<number>(4);
  const [tc3, setTc3] = useState<number>(8);
  const generalScore = tc1 + tc2 + tc3;

  // Danh mục nhiệm vụ chuyên môn 70đ
  const [tasks, setTasks] = useState([
    {
      id: 1,
      name: "Hoàn thiện đề án hiện đại hóa hệ thống radar giám sát ADS-B",
      weight: 25,
      wA: 0.15, wB: 0.50, wC: 0.15, wD: 0.20,
      scoreA: 100, scoreB: 95, scoreC: 100, scoreD: 95,
      isExceeded: true,
      evidence: "Quyết định nghiệm thu số 45/QĐ-ATTECH",
      attachmentCount: 2
    },
    {
      id: 2,
      name: "Đảm bảo kỹ thuật đài dẫn đường VOR/DME hoạt động 24/7",
      weight: 25,
      wA: 0.15, wB: 0.50, wC: 0.15, wD: 0.20,
      scoreA: 100, scoreB: 100, scoreC: 95, scoreD: 95,
      isExceeded: true,
      evidence: "Nhật ký vận hành kỹ thuật tháng 7, 8, 9",
      attachmentCount: 3
    },
    {
      id: 3,
      name: "Kiểm tra giám sát an toàn lao động xưởng cơ điện CNS",
      weight: 20,
      wA: 0.25, wB: 0.35, wC: 0.20, wD: 0.20,
      scoreA: 100, scoreB: 90, scoreC: 100, scoreD: 90,
      isExceeded: false,
      evidence: "02 Biên bản kiểm tra an toàn số 12, 14",
      attachmentCount: 1
    }
  ]);

  // State Upload Modal
  const [uploadModalOpen, setUploadModalOpen] = useState(false);
  const [uploadTaskTitle, setUploadTaskTitle] = useState("");
  const [uploadTaskId, setUploadTaskId] = useState("");

  // Công thức chuẩn Hướng dẫn 03:
  // ResultPercent = scoreA * wA + scoreB * wB + scoreC * wC + scoreD * wD (%)
  // TaskScore = ResultPercent * Weight / 100
  const calculatedTasks = tasks.map(t => {
    const resultPercent = (t.scoreA * t.wA + t.scoreB * t.wB + t.scoreC * t.wC + t.scoreD * t.wD);
    const taskScore = (resultPercent * t.weight) / 100;
    return { 
      ...t, 
      resultPercent: Math.round(resultPercent * 10) / 10, 
      taskScore: Math.round(taskScore * 10) / 10 
    };
  });

  const taskScoreTotal = Math.round(calculatedTasks.reduce((sum, t) => sum + t.taskScore, 0) * 10) / 10;
  const totalScore = Math.round((generalScore + taskScoreTotal) * 10) / 10;

  // Điều kiện Xuất sắc: >= 90 điểm VÀ >= 30% nhiệm vụ vượt chuẩn
  const exceededCount = calculatedTasks.filter(t => t.isExceeded).length;
  const exceededPercent = Math.round((exceededCount / calculatedTasks.length) * 100);

  const isEligibleExcellent = totalScore >= 90 && exceededPercent >= 30;

  const suggestedGrade = isEligibleExcellent ? "Hoàn thành xuất sắc nhiệm vụ" :
                         totalScore >= 70 ? "Hoàn thành tốt nhiệm vụ" :
                         totalScore >= 50 ? "Hoàn thành nhiệm vụ" : "Không hoàn thành nhiệm vụ";

  const openUpload = (t: typeof tasks[0]) => {
    setUploadTaskId(t.id.toString());
    setUploadTaskTitle(t.name);
    setUploadModalOpen(true);
  };

  return (
    <div className="space-y-6 animate-in fade-in duration-300">
      {/* Header Profile Bar */}
      <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <span className="px-2.5 py-0.5 rounded-full text-[11px] font-bold uppercase tracking-wide bg-amber-100 text-amber-900 border border-amber-200">
              Mẫu số 02 (03-HD/TVĐU)
            </span>
            <span className="text-xs text-slate-400">• Tự chấm điểm cuối quý (Hạn nộp: 12/09/2026)</span>
          </div>
          <h1 className="text-xl font-bold text-slate-900 tracking-tight">
            Bản Tự Chấm Điểm Đánh Giá & Xếp Loại Cán bộ Quản lý Quý III/2026
          </h1>
          <p className="text-xs text-slate-500 mt-1">
            Đồng chí: <strong className="text-slate-800">Nguyễn Văn A</strong> • Bí thư Chi bộ Khối Kỹ thuật • Trưởng phòng Kế hoạch KD
          </p>
        </div>

        <div className="flex items-center gap-3">
          <div className="p-3.5 bg-slate-900 text-white rounded-2xl shadow-sm flex items-center gap-4">
            <div>
              <div className="text-[10px] uppercase font-bold tracking-wider text-slate-400">Tổng điểm tự chấm</div>
              <div className="text-2xl font-black text-dang-gold">{totalScore} <span className="text-xs text-slate-300 font-normal">/ 100đ</span></div>
            </div>
            <div className="border-l border-slate-700 pl-3">
              <div className="text-[10px] uppercase font-bold tracking-wider text-slate-400">Tỷ lệ vượt chuẩn</div>
              <div className="text-sm font-bold text-emerald-400">{exceededPercent}% ({exceededCount}/{calculatedTasks.length} việc)</div>
            </div>
          </div>
        </div>
      </div>

      {/* Suggested Grade Banner */}
      <div className={`p-4 rounded-xl border flex items-center justify-between gap-3 text-xs ${
        isEligibleExcellent
          ? "bg-amber-50 border-amber-200 text-amber-900"
          : "bg-blue-50 border-blue-200 text-blue-900"
      }`}>
        <div className="flex items-center gap-2.5">
          <Award className="w-5 h-5 text-amber-600 shrink-0" />
          <div>
            <span>Mức xếp loại tự đề xuất: </span>
            <strong className="text-sm font-bold uppercase tracking-wide">{suggestedGrade}</strong>
            <p className="text-[11px] text-slate-500 mt-0.5">
              {isEligibleExcellent 
                ? "✓ Đạt điều kiện: Tổng điểm >= 90 và có trên 30% nhiệm vụ vượt chuẩn chất lượng/tiến độ."
                : "Lưu ý: Mức Xuất sắc cần tối thiểu 90 điểm và ít nhất 30% nhiệm vụ có minh chứng vượt chuẩn."
              }
            </p>
          </div>
        </div>

        <Link
          href="/bo-phieu"
          className="flex items-center gap-1.5 px-3.5 py-1.5 bg-attech-navy hover:bg-slate-800 text-white rounded-lg text-xs font-semibold shadow-sm transition shrink-0"
        >
          Đến Màn Bỏ phiếu Chi bộ
          <ArrowRight className="w-3.5 h-3.5" />
        </Link>
      </div>

      {/* PHẦN I: TIÊU CHÍ CHUNG 30 ĐIỂM */}
      <div className="bg-white rounded-xl border border-slate-200 shadow-sm p-5 space-y-4">
        <div className="flex items-center justify-between border-b border-slate-100 pb-2">
          <h2 className="text-sm font-bold text-slate-900 uppercase tracking-wider">
            Phần I: Nhóm Tiêu chí Chung (Tối đa 30 điểm)
          </h2>
          <span className="text-xs font-bold text-attech-navy">{generalScore} / 30 điểm</span>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-xs">
          <div className="p-3 bg-slate-50 rounded-lg border border-slate-200 space-y-2">
            <div className="flex justify-between items-center">
              <span className="font-semibold text-slate-800">1. Phẩm chất chính trị, đạo đức, kỷ luật</span>
              <span className="font-bold text-dang-crimson">{tc1}/18đ</span>
            </div>
            <p className="text-[11px] text-slate-500">Giữ gìn đoàn kết nội bộ, chấp hành Điều lệ Đảng, Quy chế văn hóa công sở.</p>
            <input
              type="range"
              min="0"
              max="18"
              step="0.5"
              value={tc1}
              onChange={(e) => setTc1(Number(e.target.value))}
              className="w-full cursor-pointer accent-dang-crimson"
            />
          </div>

          <div className="p-3 bg-slate-50 rounded-lg border border-slate-200 space-y-2">
            <div className="flex justify-between items-center">
              <span className="font-semibold text-slate-800">2. Đổi mới, dám nghĩ dám làm</span>
              <span className="font-bold text-dang-crimson">{tc2}/4đ</span>
            </div>
            <p className="text-[11px] text-slate-500">Chủ động đề xuất sáng kiến cải tiến quy trình kỹ thuật, giải pháp số hóa.</p>
            <input
              type="range"
              min="0"
              max="4"
              step="0.5"
              value={tc2}
              onChange={(e) => setTc2(Number(e.target.value))}
              className="w-full cursor-pointer accent-dang-crimson"
            />
          </div>

          <div className="p-3 bg-slate-50 rounded-lg border border-slate-200 space-y-2">
            <div className="flex justify-between items-center">
              <span className="font-semibold text-slate-800">3. Tự phê bình, khắc phục hạn chế</span>
              <span className="font-bold text-dang-crimson">{tc3}/8đ</span>
            </div>
            <p className="text-[11px] text-slate-500">Thực hiện kế hoạch khắc phục các tồn tại đã chỉ ra tại kỳ đánh giá trước.</p>
            <input
              type="range"
              min="0"
              max="8"
              step="0.5"
              value={tc3}
              onChange={(e) => setTc3(Number(e.target.value))}
              className="w-full cursor-pointer accent-dang-crimson"
            />
          </div>
        </div>
      </div>

      {/* PHẦN II: NHIỆM VỤ CHUYÊN MÔN 70 ĐIỂM */}
      <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
        <div className="p-4 border-b border-slate-100 flex items-center justify-between">
          <div>
            <h2 className="text-sm font-bold text-slate-900 uppercase tracking-wider">
              Phần II: Kết quả Thực hiện Nhiệm vụ Chuyên môn (Tối đa 70 điểm)
            </h2>
            <p className="text-xs text-slate-400 mt-0.5">Tự chấm tỷ lệ % hoàn thành 4 thành phần A-B-C-D theo Điều 7 Hướng dẫn 03</p>
          </div>
          <div className="text-xs font-bold text-attech-navy">
            Tổng điểm chuyên môn: <span className="text-dang-crimson text-sm">{taskScoreTotal}</span> / 70đ
          </div>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs border-collapse">
            <thead>
              <tr className="bg-slate-800 text-white font-semibold">
                <th className="p-3 w-10 text-center">STT</th>
                <th className="p-3">Tên nhiệm vụ / Sản phẩm</th>
                <th className="p-3 w-20 text-center">Trọng số</th>
                <th className="p-3 w-24 text-center">Khối lượng (A)</th>
                <th className="p-3 w-24 text-center">Chất lượng (B)</th>
                <th className="p-3 w-24 text-center">Tiến độ (C)</th>
                <th className="p-3 w-24 text-center">Hiệu quả (D)</th>
                <th className="p-3 w-20 text-center">Điểm việc</th>
                <th className="p-3 w-24 text-center">Vượt chuẩn?</th>
                <th className="p-3 w-40">Minh chứng đính kèm</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {calculatedTasks.map((t, idx) => (
                <tr key={t.id} className="hover:bg-slate-50/80 transition">
                  <td className="p-3 text-center text-slate-400 font-medium">{idx + 1}</td>
                  <td className="p-3 font-semibold text-slate-900">
                    <div>{t.name}</div>
                    <div className="text-[11px] text-slate-400 font-normal">Tỷ trọng A-B-C-D: {(t.wA*100)}% - {(t.wB*100)}% - {(t.wC*100)}% - {(t.wD*100)}%</div>
                  </td>
                  <td className="p-3 text-center font-bold text-slate-700">{t.weight}đ</td>
                  <td className="p-3 text-center">
                    <input
                      type="number"
                      value={t.scoreA}
                      onChange={(e) => {
                        const val = Number(e.target.value);
                        setTasks(tasks.map(x => x.id === t.id ? { ...x, scoreA: val } : x));
                      }}
                      className="w-14 p-1 border rounded text-center text-xs font-bold text-slate-800 bg-slate-50"
                    />%
                  </td>
                  <td className="p-3 text-center">
                    <input
                      type="number"
                      value={t.scoreB}
                      onChange={(e) => {
                        const val = Number(e.target.value);
                        setTasks(tasks.map(x => x.id === t.id ? { ...x, scoreB: val } : x));
                      }}
                      className="w-14 p-1 border rounded text-center text-xs font-bold text-slate-800 bg-slate-50"
                    />%
                  </td>
                  <td className="p-3 text-center">
                    <input
                      type="number"
                      value={t.scoreC}
                      onChange={(e) => {
                        const val = Number(e.target.value);
                        setTasks(tasks.map(x => x.id === t.id ? { ...x, scoreC: val } : x));
                      }}
                      className="w-14 p-1 border rounded text-center text-xs font-bold text-slate-800 bg-slate-50"
                    />%
                  </td>
                  <td className="p-3 text-center">
                    <input
                      type="number"
                      value={t.scoreD}
                      onChange={(e) => {
                        const val = Number(e.target.value);
                        setTasks(tasks.map(x => x.id === t.id ? { ...x, scoreD: val } : x));
                      }}
                      className="w-14 p-1 border rounded text-center text-xs font-bold text-slate-800 bg-slate-50"
                    />%
                  </td>
                  <td className="p-3 text-center font-black text-dang-crimson text-sm">
                    {t.taskScore}đ
                  </td>
                  <td className="p-3 text-center">
                    <input
                      type="checkbox"
                      checked={t.isExceeded}
                      onChange={(e) => {
                        setTasks(tasks.map(x => x.id === t.id ? { ...x, isExceeded: e.target.checked } : x));
                      }}
                      className="w-4 h-4 cursor-pointer accent-emerald-600"
                    />
                  </td>
                  <td className="p-3">
                    <div className="flex flex-col gap-1">
                      <span className="text-[11px] text-slate-500 truncate max-w-[140px]">{t.evidence}</span>
                      <button
                        onClick={() => openUpload(t)}
                        className="inline-flex items-center gap-1 px-2 py-1 bg-blue-50 text-attech-blue hover:bg-blue-100 border border-blue-200/60 rounded text-[11px] font-semibold transition w-fit"
                      >
                        <Paperclip className="w-3 h-3" />
                        Minh chứng ({t.attachmentCount || 0})
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="p-4 bg-slate-50 border-t border-slate-100 flex items-center justify-between">
          <span className="text-xs text-slate-500">
            Công thức: Điểm việc = (A×wA + B×wB + C×wC + D×wD)% × Trọng số việc.
          </span>
          <button
            onClick={() => alert("Đã lưu kết quả tự chấm điểm Mẫu 02 thành công!")}
            className="flex items-center gap-2 px-4 py-2 bg-dang-crimson hover:bg-red-800 text-white rounded-xl text-xs font-bold shadow-md transition"
          >
            <Send className="w-3.5 h-3.5" />
            Nộp Bản Tự Đánh Giá Mẫu 02
          </button>
        </div>
      </div>

      {/* Modal Upload Tệp Minh Chứng */}
      <EvidenceUploadModal
        isOpen={uploadModalOpen}
        onClose={() => setUploadModalOpen(false)}
        taskTitle={uploadTaskTitle}
        taskId={uploadTaskId}
        formCode="M02"
      />
    </div>
  );
}
