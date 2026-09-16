"use client";

import React, { useState } from "react";
import Link from "next/link";
import { Plus, Check, ArrowRight, AlertTriangle, Paperclip, FileText, Send, Sparkles, Sliders } from "lucide-react";
import { EvidenceUploadModal } from "../../components/forms/EvidenceUploadModal";

interface TaskItem {
  id: string;
  code: string;
  name: string;
  axis: string;
  weight: number;
  deadline: string;
  standard: string;
  evidence: string;
  attachmentCount?: number;
  isUrgent?: boolean;
}

export default function Mau01Page() {
  const [tasks, setTasks] = useState<TaskItem[]>([
    {
      id: "1",
      code: "SP-01",
      name: "Chỉ đạo hoàn thành bảo dưỡng định kỳ đài VOR/DME Nội Bài đạt chuẩn ICAO",
      axis: "T1 (Kỹ thuật CNS)",
      weight: 30,
      deadline: "15/09/2026",
      standard: "Đạt kiểm định 100%, không gián đoạn bay",
      evidence: "Biên bản nghiệm thu kỹ thuật",
      attachmentCount: 2
    },
    {
      id: "2",
      code: "SP-02",
      name: "Triển khai dự án nâng cấp hệ thống Radar giám sát sơ cấp/thứ cấp",
      axis: "T1 (Kỹ thuật CNS)",
      weight: 20,
      deadline: "30/09/2026",
      standard: "Đúng tiến độ quý, an toàn tuyệt đối",
      evidence: "Báo cáo tiến độ Ban QLDA",
      attachmentCount: 1
    },
    {
      id: "3",
      code: "SP-03",
      name: "Tổ chức sinh hoạt chuyên đề Chi bộ quý III về phòng chống lãng phí, tiêu cực",
      axis: "T4 (Xây dựng Đảng)",
      weight: 10,
      deadline: "20/08/2026",
      standard: "100% đảng viên tham gia đầy đủ",
      evidence: "Nghị quyết sinh hoạt Chi bộ",
      attachmentCount: 1
    },
    {
      id: "4",
      code: "SP-04",
      name: "Thực hiện chuyển đổi số báo cáo kỹ thuật và hồ sơ công việc tại đơn vị",
      axis: "T5 (Khoa học công nghệ)",
      weight: 10,
      deadline: "25/09/2026",
      standard: "Số hóa 100% nhật ký kỹ thuật trên máy chủ nội bộ",
      evidence: "Dữ liệu phần mềm nội bộ ATTECH",
      attachmentCount: 3
    }
  ]);

  // State Modal Mẫu 04 (Ngoại lệ điều chỉnh)
  const [showMau04Modal, setShowMau04Modal] = useState(false);
  const [mau04Data, setMau04Data] = useState({
    taskId: "2",
    reason: "Đối tác nước ngoài chậm bàn giao linh kiện do bất khả kháng chuỗi cung ứng.",
    proposal: "Gia hạn thời gian hoàn thành sang Quý IV/2026 và giữ nguyên trọng số."
  });

  // State Modal Mẫu 05 (Đột xuất)
  const [showMau05Modal, setShowMau05Modal] = useState(false);
  const [mau05Data, setMau05Data] = useState({
    name: "Khắc phục khẩn cấp sự cố hệ thống cấp điện nguồn UPS trạm Radar",
    weight: 10,
    deadline: "20/09/2026",
    balanceMethod: "reduce", // 'reduce' hoặc 'replace'
    targetTaskId: "4"
  });

  // State Upload Modal
  const [uploadModalOpen, setUploadModalOpen] = useState(false);
  const [uploadTaskTitle, setUploadTaskTitle] = useState("");
  const [uploadTaskId, setUploadTaskId] = useState("");

  const totalWeight = tasks.reduce((sum, t) => sum + t.weight, 0);

  const handleUpdateWeight = (id: string, weight: number) => {
    setTasks(tasks.map((t) => (t.id === id ? { ...t, weight: Number(weight) } : t)));
  };

  const handleApplyMau05 = () => {
    if (mau05Data.balanceMethod === "replace") {
      const newTasks = tasks.filter(t => t.id !== mau05Data.targetTaskId);
      newTasks.push({
        id: Date.now().toString(),
        code: `SP-${String(newTasks.length + 1).padStart(2, "0")}`,
        name: `[ĐỘT XUẤT] ${mau05Data.name}`,
        axis: "T1 (Kỹ thuật CNS)",
        weight: mau05Data.weight,
        deadline: mau05Data.deadline,
        standard: "Khắc phục ngay, đảm bảo an toàn tuyệt đối",
        evidence: "Lệnh điều động khẩn cấp & Biên bản bàn giao",
        attachmentCount: 1,
        isUrgent: true
      });
      setTasks(newTasks);
    } else {
      const newTasks = tasks.map(t => {
        if (t.id === mau05Data.targetTaskId) {
          return { ...t, weight: Math.max(5, t.weight - mau05Data.weight) };
        }
        return t;
      });
      newTasks.push({
        id: Date.now().toString(),
        code: `SP-${String(newTasks.length + 1).padStart(2, "0")}`,
        name: `[ĐỘT XUẤT] ${mau05Data.name}`,
        axis: "T1 (Kỹ thuật CNS)",
        weight: mau05Data.weight,
        deadline: mau05Data.deadline,
        standard: "Khắc phục ngay, an toàn bay",
        evidence: "Lệnh điều động khẩn cấp",
        attachmentCount: 1,
        isUrgent: true
      });
      setTasks(newTasks);
    }
    setShowMau05Modal(false);
  };

  const openUploadModal = (task: TaskItem) => {
    setUploadTaskId(task.id);
    setUploadTaskTitle(`${task.code} - ${task.name}`);
    setUploadModalOpen(true);
  };

  return (
    <div className="space-y-6 animate-in fade-in duration-300">
      {/* Header Profile Bar */}
      <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-sm flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <span className="px-2.5 py-0.5 rounded-full text-[11px] font-bold uppercase tracking-wide bg-red-100 text-dang-crimson border border-red-200">
              Mẫu 01 (Ban hành kèm 03-HD/TVĐU)
            </span>
            <span className="text-xs text-slate-400">• Đăng ký đầu quý (Hạn nộp: 05/07/2026)</span>
          </div>
          <h1 className="text-xl font-bold text-slate-900 tracking-tight">
            Bản Đăng ký Giao việc & Sản phẩm Trọng tâm Quý III/2026
          </h1>
          <p className="text-xs text-slate-500 mt-1">
            Cán bộ: <strong className="text-slate-800">Nguyễn Văn A</strong> • Chức danh: Trưởng phòng Kế hoạch KD • Chi bộ Khối Kỹ thuật
          </p>
        </div>

        <div className="flex items-center gap-3">
          <div className={`p-3 rounded-xl border flex items-center gap-3 ${
            totalWeight === 70 
              ? "bg-emerald-50 border-emerald-200 text-emerald-800" 
              : "bg-red-50 border-red-200 text-red-800"
          }`}>
            <div>
              <div className="text-[10px] font-bold uppercase tracking-wider">Tổng trọng số công việc</div>
              <div className="text-2xl font-black">{totalWeight} <span className="text-sm font-normal">/ 70 điểm</span></div>
            </div>
            {totalWeight === 70 ? (
              <Check className="w-6 h-6 text-emerald-600" />
            ) : (
              <AlertTriangle className="w-6 h-6 text-red-600 animate-pulse" />
            )}
          </div>
        </div>
      </div>

      {/* Action Toolbar */}
      <div className="flex flex-wrap items-center justify-between gap-3 bg-slate-50 p-3 rounded-xl border border-slate-200 text-xs">
        <div className="flex items-center gap-2">
          <button
            onClick={() => setShowMau05Modal(true)}
            className="flex items-center gap-1.5 px-3 py-1.5 bg-amber-500 hover:bg-amber-600 text-white rounded-lg font-semibold shadow-sm transition"
          >
            <Sparkles className="w-3.5 h-3.5" />
            Khai báo việc Đột xuất (Mẫu 05)
          </button>
          <button
            onClick={() => setShowMau04Modal(true)}
            className="flex items-center gap-1.5 px-3 py-1.5 bg-slate-700 hover:bg-slate-800 text-white rounded-lg font-semibold shadow-sm transition"
          >
            <Sliders className="w-3.5 h-3.5" />
            Đề nghị Điều chỉnh ngoại lệ (Mẫu 04)
          </button>
        </div>

        <div className="flex items-center gap-2">
          <Link
            href="/mau-02"
            className="flex items-center gap-1.5 px-3.5 py-1.5 bg-attech-navy hover:bg-slate-800 text-white rounded-lg font-semibold shadow-sm transition"
          >
            Chuyển sang Mẫu 02 (Tự chấm)
            <ArrowRight className="w-3.5 h-3.5" />
          </Link>
        </div>
      </div>

      {/* Task List Table */}
      <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
        <div className="p-4 border-b border-slate-100 flex items-center justify-between">
          <h2 className="text-sm font-bold text-slate-800 uppercase tracking-wider">
            Danh mục Sản phẩm / Nhiệm vụ Trọng tâm (Từ 3 đến 7 việc)
          </h2>
          <span className="text-xs text-slate-500 font-medium">Hiện có: {tasks.length} nhiệm vụ</span>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs border-collapse">
            <thead>
              <tr className="bg-slate-800 text-white font-semibold">
                <th className="p-3 w-14 text-center">MÃ</th>
                <th className="p-3">Tên sản phẩm / nhiệm vụ trọng tâm</th>
                <th className="p-3 w-36">Trục kết quả</th>
                <th className="p-3 w-28 text-center">Trọng số (đ)</th>
                <th className="p-3 w-28 text-center">Hạn mức quý</th>
                <th className="p-3">Tiêu chuẩn đạt</th>
                <th className="p-3 w-48">Minh chứng & Tệp đính kèm</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {tasks.map((task) => (
                <tr key={task.id} className={`hover:bg-slate-50/80 transition ${task.isUrgent ? "bg-amber-50/50" : ""}`}>
                  <td className="p-3 text-center font-mono font-bold text-slate-700">
                    {task.code}
                  </td>
                  <td className="p-3 font-semibold text-slate-900">
                    {task.isUrgent && (
                      <span className="inline-block bg-amber-500 text-white text-[10px] font-bold px-1.5 py-0.5 rounded mr-1.5 shadow-sm">
                        ĐỘT XUẤT
                      </span>
                    )}
                    {task.name}
                  </td>
                  <td className="p-3 text-slate-600 font-medium">{task.axis}</td>
                  <td className="p-3 text-center">
                    <input
                      type="number"
                      value={task.weight}
                      onChange={(e) => handleUpdateWeight(task.id, Number(e.target.value))}
                      className="w-16 p-1 border rounded text-center font-bold text-attech-navy bg-slate-50 text-xs focus:bg-white focus:border-attech-blue"
                    />
                  </td>
                  <td className="p-3 text-center text-slate-600 font-mono">{task.deadline}</td>
                  <td className="p-3 text-slate-600">{task.standard}</td>
                  <td className="p-3">
                    <div className="flex flex-col gap-1">
                      <span className="text-[11px] text-slate-500 truncate max-w-[170px]">{task.evidence}</span>
                      <button
                        onClick={() => openUploadModal(task)}
                        className="inline-flex items-center gap-1 px-2 py-1 bg-blue-50 text-attech-blue hover:bg-blue-100 border border-blue-200/60 rounded text-[11px] font-semibold transition w-fit"
                      >
                        <Paperclip className="w-3 h-3" />
                        Minh chứng ({task.attachmentCount || 0})
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
            * Nguyên tắc bảo toàn: Tổng trọng số các nhiệm vụ phải tròn <strong>70 điểm</strong> theo Điều 7 Hướng dẫn 03.
          </span>
          <button
            onClick={() => alert("Đã lưu bản đăng ký Mẫu 01 thành công!")}
            className="flex items-center gap-2 px-4 py-2 bg-dang-crimson hover:bg-red-800 text-white rounded-xl text-xs font-bold shadow-md transition"
          >
            <Send className="w-3.5 h-3.5" />
            Lưu & Trình Cấp ủy Duyệt
          </button>
        </div>
      </div>

      {/* Modal Mẫu 04: Ngoại lệ */}
      {showMau04Modal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4 animate-in fade-in">
          <div className="bg-white rounded-2xl p-6 max-w-lg w-full shadow-2xl border border-slate-200">
            <h3 className="font-bold text-base text-slate-900 mb-1">
              Phiếu Đề nghị Điều chỉnh Ngoại lệ (Mẫu 04)
            </h3>
            <p className="text-xs text-slate-500 mb-4">Căn cứ yếu tố khách quan ngoài tầm kiểm soát của cán bộ</p>

            <div className="space-y-3 text-xs">
              <div>
                <label className="block font-semibold text-slate-700 mb-1">Nhiệm vụ đề nghị điều chỉnh:</label>
                <select
                  value={mau04Data.taskId}
                  onChange={(e) => setMau04Data({ ...mau04Data, taskId: e.target.value })}
                  className="w-full p-2 border border-slate-200 rounded-lg bg-slate-50 text-xs"
                >
                  {tasks.map(t => (
                    <option key={t.id} value={t.id}>{t.code} - {t.name} ({t.weight}đ)</option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">Lý do khách quan:</label>
                <textarea
                  rows={3}
                  value={mau04Data.reason}
                  onChange={(e) => setMau04Data({ ...mau04Data, reason: e.target.value })}
                  className="w-full p-2 border border-slate-200 rounded-lg text-xs"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">Phương án đề xuất:</label>
                <textarea
                  rows={2}
                  value={mau04Data.proposal}
                  onChange={(e) => setMau04Data({ ...mau04Data, proposal: e.target.value })}
                  className="w-full p-2 border border-slate-200 rounded-lg text-xs"
                />
              </div>
            </div>

            <div className="flex justify-end gap-2 mt-5 pt-3 border-t border-slate-100">
              <button
                onClick={() => setShowMau04Modal(false)}
                className="px-3.5 py-1.5 border border-slate-200 rounded-lg text-slate-600 hover:bg-slate-50 text-xs font-semibold"
              >
                Hủy bỏ
              </button>
              <button
                onClick={() => {
                  alert("Đã gửi Đề nghị điều chỉnh Mẫu 04 tới Cấp ủy Chi bộ!");
                  setShowMau04Modal(false);
                }}
                className="px-3.5 py-1.5 bg-attech-navy hover:bg-slate-800 text-white rounded-lg text-xs font-semibold"
              >
                Gửi đề nghị
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal Mẫu 05: Đột xuất */}
      {showMau05Modal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4 animate-in fade-in">
          <div className="bg-white rounded-2xl p-6 max-w-lg w-full shadow-2xl border border-slate-200">
            <h3 className="font-bold text-base text-slate-900 mb-1">
              Khai báo Bổ sung Nhiệm vụ Đột xuất (Mẫu 05)
            </h3>
            <p className="text-xs text-slate-500 mb-4">Tự động cân đối bảo toàn tổng 70 điểm công việc chuyên môn</p>

            <div className="space-y-3 text-xs">
              <div>
                <label className="block font-semibold text-slate-700 mb-1">Tên nhiệm vụ đột xuất phát sinh:</label>
                <input
                  type="text"
                  value={mau05Data.name}
                  onChange={(e) => setMau05Data({ ...mau05Data, name: e.target.value })}
                  className="w-full p-2 border border-slate-200 rounded-lg text-xs"
                />
              </div>

              <div className="grid grid-cols-2 gap-2">
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Trọng số (đ):</label>
                  <input
                    type="number"
                    value={mau05Data.weight}
                    onChange={(e) => setMau05Data({ ...mau05Data, weight: Number(e.target.value) })}
                    className="w-full p-2 border border-slate-200 rounded-lg text-xs font-bold"
                  />
                </div>
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Thời hạn hoàn thành:</label>
                  <input
                    type="text"
                    value={mau05Data.deadline}
                    onChange={(e) => setMau05Data({ ...mau05Data, deadline: e.target.value })}
                    className="w-full p-2 border border-slate-200 rounded-lg text-xs font-mono"
                  />
                </div>
              </div>

              <div className="p-3 bg-slate-50 rounded-lg border border-slate-200 space-y-2">
                <label className="block font-semibold text-slate-800">Phương án cân đối để giữ 70 điểm:</label>
                <div className="flex gap-4">
                  <label className="flex items-center gap-1.5 cursor-pointer">
                    <input
                      type="radio"
                      name="balanceMethod"
                      checked={mau05Data.balanceMethod === "reduce"}
                      onChange={() => setMau05Data({ ...mau05Data, balanceMethod: "reduce" })}
                    />
                    <span>Giảm trừ điểm việc khác</span>
                  </label>
                  <label className="flex items-center gap-1.5 cursor-pointer">
                    <input
                      type="radio"
                      name="balanceMethod"
                      checked={mau05Data.balanceMethod === "replace"}
                      onChange={() => setMau05Data({ ...mau05Data, balanceMethod: "replace" })}
                    />
                    <span>Thay thế hoàn toàn một việc</span>
                  </label>
                </div>

                <div>
                  <label className="block font-semibold mt-2 mb-1 text-slate-700">Nhiệm vụ bị điều chỉnh / thay thế:</label>
                  <select
                    value={mau05Data.targetTaskId}
                    onChange={(e) => setMau05Data({ ...mau05Data, targetTaskId: e.target.value })}
                    className="w-full p-1.5 border border-slate-200 rounded-lg bg-white text-xs"
                  >
                    {tasks.map(t => (
                      <option key={t.id} value={t.id}>{t.code} - {t.name} ({t.weight}đ)</option>
                    ))}
                  </select>
                </div>
              </div>
            </div>

            <div className="flex justify-end gap-2 mt-5 pt-3 border-t border-slate-100">
              <button
                onClick={() => setShowMau05Modal(false)}
                className="px-3.5 py-1.5 border border-slate-200 rounded-lg text-slate-600 hover:bg-slate-50 text-xs font-semibold"
              >
                Hủy bỏ
              </button>
              <button
                onClick={handleApplyMau05}
                className="px-3.5 py-1.5 bg-amber-600 hover:bg-amber-700 text-white rounded-lg text-xs font-semibold"
              >
                Áp dụng Mẫu 05
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
        taskId={uploadTaskId}
        formCode="M01"
      />
    </div>
  );
}
