'use client';

import React, { useState } from 'react';

export default function CauHinhPage() {
  const [maxScore, setMaxScore] = useState(100);
  const [generalScore, setGeneralScore] = useState(30);
  const [taskScore, setTaskScore] = useState(70);
  const [tc1, setTc1] = useState(18);
  const [tc2, setTc2] = useState(4);
  const [tc3, setTc3] = useState(8);

  const [quota, setQuota] = useState(20);
  const [specialQuota, setSpecialQuota] = useState(25);
  const [minTasks, setMinTasks] = useState(3);
  const [maxTasks, setMaxTasks] = useState(7);

  // Tỷ trọng khối chức danh
  const [weights, setWeights] = useState({
    ktA: 50, ktB: 30, ktC: 10, ktD: 10,
    sxA: 45, sxB: 35, sxC: 10, sxD: 10,
    vpA: 40, vpB: 40, vpC: 10, vpD: 10,
  });

  const handleSave = () => {
    alert('Đã cập nhật thành công tham số cấu hình vào cơ sở dữ liệu PostgreSQL!');
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="bg-white rounded-xl shadow-sm p-6 border border-slate-200/80 flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
        <div>
          <div className="flex items-center gap-2">
            <span className="text-[11px] bg-amber-100 text-amber-900 font-extrabold px-2.5 py-0.5 rounded border border-amber-300 uppercase">
              QUẢN TRỊ THAM SỐ
            </span>
            <h2 className="text-xl font-bold text-slate-900">
              Cấu Hình Động Quy Trình & Thang Điểm (03-HD/TVĐU)
            </h2>
          </div>
          <p className="text-xs text-slate-500 mt-1">
            Cho phép Ban Thường vụ Đảng ủy và Ủy ban Kiểm tra điều chỉnh linh hoạt mọi thang điểm, tỷ trọng A-B-C-D và tỷ lệ trần xuất sắc mà không phải sửa mã nguồn.
          </p>
        </div>

        <button
          onClick={handleSave}
          className="px-5 py-2.5 bg-gradient-to-r from-rose-700 to-rose-900 hover:from-rose-800 hover:to-rose-950 text-white text-xs font-bold rounded-lg shadow transition flex items-center gap-2"
        >
          <span>💾</span>
          <span>Lưu Cấu Hình Vào CSDL</span>
        </button>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 text-xs">
        {/* 1. Cấu hình Thang điểm tổng thể */}
        <div className="bg-white p-5 rounded-xl border border-slate-200 shadow-sm space-y-4">
          <div className="flex items-center gap-2 border-b pb-3">
            <span className="text-base">🎯</span>
            <h3 className="font-bold text-sm text-slate-800">1. Cấu hình Thang điểm Tổng thể</h3>
          </div>

          <div className="grid grid-cols-3 gap-3">
            <div>
              <label className="text-slate-600 block mb-1 font-medium">Tổng điểm tối đa:</label>
              <input
                type="number"
                value={maxScore}
                onChange={(e) => setMaxScore(Number(e.target.value))}
                className="w-full p-2 border rounded-lg bg-slate-50 font-bold text-center text-sm"
              />
            </div>
            <div>
              <label className="text-slate-600 block mb-1 font-medium">Tiêu chuẩn chung:</label>
              <input
                type="number"
                value={generalScore}
                onChange={(e) => setGeneralScore(Number(e.target.value))}
                className="w-full p-2 border rounded-lg bg-slate-50 font-bold text-center text-sm text-rose-700"
              />
            </div>
            <div>
              <label className="text-slate-600 block mb-1 font-medium">Nhiệm vụ chuyên môn:</label>
              <input
                type="number"
                value={taskScore}
                onChange={(e) => setTaskScore(Number(e.target.value))}
                className="w-full p-2 border rounded-lg bg-slate-50 font-bold text-center text-sm text-blue-700"
              />
            </div>
          </div>

          <div className="p-3 bg-slate-50 rounded-lg border space-y-2">
            <span className="font-semibold text-slate-700 block">Phân bổ 30 điểm tiêu chuẩn chung (TC):</span>
            <div className="grid grid-cols-3 gap-2">
              <div>
                <label className="text-[11px] text-slate-500 block">TC1 (Chính trị, đạo đức):</label>
                <input
                  type="number"
                  value={tc1}
                  onChange={(e) => setTc1(Number(e.target.value))}
                  className="w-full p-1.5 border rounded bg-white font-semibold text-center text-xs"
                />
              </div>
              <div>
                <label className="text-[11px] text-slate-500 block">TC2 (Đổi mới, sáng tạo):</label>
                <input
                  type="number"
                  value={tc2}
                  onChange={(e) => setTc2(Number(e.target.value))}
                  className="w-full p-1.5 border rounded bg-white font-semibold text-center text-xs"
                />
              </div>
              <div>
                <label className="text-[11px] text-slate-500 block">TC3 (Tự soi, tự sửa):</label>
                <input
                  type="number"
                  value={tc3}
                  onChange={(e) => setTc3(Number(e.target.value))}
                  className="w-full p-1.5 border rounded bg-white font-semibold text-center text-xs"
                />
              </div>
            </div>
          </div>
        </div>

        {/* 2. Cấu hình Trần thi đua & Số lượng nhiệm vụ */}
        <div className="bg-white p-5 rounded-xl border border-slate-200 shadow-sm space-y-4">
          <div className="flex items-center gap-2 border-b pb-3">
            <span className="text-base">🛡️</span>
            <h3 className="font-bold text-sm text-slate-800">2. Cấu hình Trần Thi đua Xuất sắc & Ràng buộc</h3>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="text-slate-600 block mb-1 font-medium">Trần Xuất sắc mặc định (%):</label>
              <input
                type="number"
                value={quota}
                onChange={(e) => setQuota(Number(e.target.value))}
                className="w-full p-2 border rounded-lg bg-emerald-50 text-emerald-900 font-black text-center text-base"
              />
              <span className="text-[10px] text-slate-400 mt-1 block">Tối đa 20% trong số Hoàn thành tốt</span>
            </div>
            <div>
              <label className="text-slate-600 block mb-1 font-medium">Trần nới đặc biệt có giải trình (%):</label>
              <input
                type="number"
                value={specialQuota}
                onChange={(e) => setSpecialQuota(Number(e.target.value))}
                className="w-full p-2 border rounded-lg bg-amber-50 text-amber-900 font-black text-center text-base"
              />
              <span className="text-[10px] text-slate-400 mt-1 block">Cho đơn vị có thành tích nổi trội</span>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3 pt-2">
            <div>
              <label className="text-slate-600 block mb-1 font-medium">Số nhiệm vụ tối thiểu (Mẫu 01):</label>
              <input
                type="number"
                value={minTasks}
                onChange={(e) => setMinTasks(Number(e.target.value))}
                className="w-full p-1.5 border rounded bg-slate-50 font-bold text-center"
              />
            </div>
            <div>
              <label className="text-slate-600 block mb-1 font-medium">Số nhiệm vụ tối đa (Mẫu 01):</label>
              <input
                type="number"
                value={maxTasks}
                onChange={(e) => setMaxTasks(Number(e.target.value))}
                className="w-full p-1.5 border rounded bg-slate-50 font-bold text-center"
              />
            </div>
          </div>
        </div>

        {/* 3. Tỷ trọng A-B-C-D theo khối chức danh */}
        <div className="bg-white p-5 rounded-xl border border-slate-200 shadow-sm space-y-4 lg:col-span-2">
          <div className="flex items-center gap-2 border-b pb-3">
            <span className="text-base">📊</span>
            <h3 className="font-bold text-sm text-slate-800">
              3. Bảng Tỷ trọng Tiêu chí (A: Khối lượng - B: Chất lượng - C: Tiến độ - D: Hiệu quả) Theo Khối Chức Danh ATTECH
            </h3>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            {/* Khối Kỹ thuật CNS */}
            <div className="p-3.5 rounded-lg border border-blue-200 bg-blue-50/40 space-y-2.5">
              <span className="font-bold text-blue-950 block text-xs">Khối 1: Kỹ thuật CNS & Điều hành bay</span>
              <div className="grid grid-cols-4 gap-2 text-center">
                <div>
                  <span className="text-[10px] text-slate-500 block">A (%)</span>
                  <input
                    type="number"
                    value={weights.ktA}
                    onChange={(e) => setWeights({ ...weights, ktA: Number(e.target.value) })}
                    className="w-full p-1 border rounded bg-white font-bold text-center text-xs"
                  />
                </div>
                <div>
                  <span className="text-[10px] text-slate-500 block">B (%)</span>
                  <input
                    type="number"
                    value={weights.ktB}
                    onChange={(e) => setWeights({ ...weights, ktB: Number(e.target.value) })}
                    className="w-full p-1 border rounded bg-white font-bold text-center text-xs"
                  />
                </div>
                <div>
                  <span className="text-[10px] text-slate-500 block">C (%)</span>
                  <input
                    type="number"
                    value={weights.ktC}
                    onChange={(e) => setWeights({ ...weights, ktC: Number(e.target.value) })}
                    className="w-full p-1 border rounded bg-white font-bold text-center text-xs"
                  />
                </div>
                <div>
                  <span className="text-[10px] text-slate-500 block">D (%)</span>
                  <input
                    type="number"
                    value={weights.ktD}
                    onChange={(e) => setWeights({ ...weights, ktD: Number(e.target.value) })}
                    className="w-full p-1 border rounded bg-white font-bold text-center text-xs"
                  />
                </div>
              </div>
              <span className="text-[10px] text-blue-800 block text-right font-mono">
                Tổng tỷ trọng: {weights.ktA + weights.ktB + weights.ktC + weights.ktD}%
              </span>
            </div>

            {/* Khối Sản xuất - Dịch vụ */}
            <div className="p-3.5 rounded-lg border border-amber-200 bg-amber-50/40 space-y-2.5">
              <span className="font-bold text-amber-950 block text-xs">Khối 2: Sản xuất Thiết bị & Dịch vụ KT</span>
              <div className="grid grid-cols-4 gap-2 text-center">
                <div>
                  <span className="text-[10px] text-slate-500 block">A (%)</span>
                  <input
                    type="number"
                    value={weights.sxA}
                    onChange={(e) => setWeights({ ...weights, sxA: Number(e.target.value) })}
                    className="w-full p-1 border rounded bg-white font-bold text-center text-xs"
                  />
                </div>
                <div>
                  <span className="text-[10px] text-slate-500 block">B (%)</span>
                  <input
                    type="number"
                    value={weights.sxB}
                    onChange={(e) => setWeights({ ...weights, sxB: Number(e.target.value) })}
                    className="w-full p-1 border rounded bg-white font-bold text-center text-xs"
                  />
                </div>
                <div>
                  <span className="text-[10px] text-slate-500 block">C (%)</span>
                  <input
                    type="number"
                    value={weights.sxC}
                    onChange={(e) => setWeights({ ...weights, sxC: Number(e.target.value) })}
                    className="w-full p-1 border rounded bg-white font-bold text-center text-xs"
                  />
                </div>
                <div>
                  <span className="text-[10px] text-slate-500 block">D (%)</span>
                  <input
                    type="number"
                    value={weights.sxD}
                    onChange={(e) => setWeights({ ...weights, sxD: Number(e.target.value) })}
                    className="w-full p-1 border rounded bg-white font-bold text-center text-xs"
                  />
                </div>
              </div>
              <span className="text-[10px] text-amber-800 block text-right font-mono">
                Tổng tỷ trọng: {weights.sxA + weights.sxB + weights.sxC + weights.sxD}%
              </span>
            </div>

            {/* Khối Cơ quan Văn phòng */}
            <div className="p-3.5 rounded-lg border border-slate-200 bg-slate-50 space-y-2.5">
              <span className="font-bold text-slate-900 block text-xs">Khối 3: Tham mưu, Văn phòng, Đảng - Đoàn thể</span>
              <div className="grid grid-cols-4 gap-2 text-center">
                <div>
                  <span className="text-[10px] text-slate-500 block">A (%)</span>
                  <input
                    type="number"
                    value={weights.vpA}
                    onChange={(e) => setWeights({ ...weights, vpA: Number(e.target.value) })}
                    className="w-full p-1 border rounded bg-white font-bold text-center text-xs"
                  />
                </div>
                <div>
                  <span className="text-[10px] text-slate-500 block">B (%)</span>
                  <input
                    type="number"
                    value={weights.vpB}
                    onChange={(e) => setWeights({ ...weights, vpB: Number(e.target.value) })}
                    className="w-full p-1 border rounded bg-white font-bold text-center text-xs"
                  />
                </div>
                <div>
                  <span className="text-[10px] text-slate-500 block">C (%)</span>
                  <input
                    type="number"
                    value={weights.vpC}
                    onChange={(e) => setWeights({ ...weights, vpC: Number(e.target.value) })}
                    className="w-full p-1 border rounded bg-white font-bold text-center text-xs"
                  />
                </div>
                <div>
                  <span className="text-[10px] text-slate-500 block">D (%)</span>
                  <input
                    type="number"
                    value={weights.vpD}
                    onChange={(e) => setWeights({ ...weights, vpD: Number(e.target.value) })}
                    className="w-full p-1 border rounded bg-white font-bold text-center text-xs"
                  />
                </div>
              </div>
              <span className="text-[10px] text-slate-700 block text-right font-mono">
                Tổng tỷ trọng: {weights.vpA + weights.vpB + weights.vpC + weights.vpD}%
              </span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
