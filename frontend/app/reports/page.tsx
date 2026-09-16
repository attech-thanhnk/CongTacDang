"use client";

import React, { useState } from "react";
import Link from "next/link";
import { reportService, REPORT_LIST, ReportItem } from "@/services/reportService";

export default function ReportsPage() {
  const [searchTerm, setSearchTerm] = useState("");
  const [selectedCategory, setSelectedCategory] = useState("all");
  const [downloadingId, setDownloadingId] = useState<string | null>(null);

  const handleDownload = (report: ReportItem) => {
    if (!report.endpoint) return;
    setDownloadingId(report.id);
    reportService.downloadReport(report.endpoint, report.fileName);
    setTimeout(() => setDownloadingId(null), 1000);
  };

  const handlePrint = () => {
    reportService.printDocument();
  };

  const filteredReports = REPORT_LIST.filter(rep => {
    const matchesSearch = rep.title.toLowerCase().includes(searchTerm.toLowerCase()) ||
                          rep.code.toLowerCase().includes(searchTerm.toLowerCase()) ||
                          rep.description.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesCat = selectedCategory === "all" || rep.category === selectedCategory;
    return matchesSearch && matchesCat;
  });

  return (
    <div className="space-y-6 font-serif max-w-5xl mx-auto">
      {/* Tiêu đề & Nút in A4 */}
      <div className="bg-white border border-slate-300 rounded p-5 shadow-sm">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-300 pb-3">
          <div>
            <h1 className="text-base font-bold text-slate-900 uppercase">
              TRUNG TÂM KẾT XUẤT DỮ LIỆU & BÁO CÁO BẢNG TÍNH
            </h1>
            <p className="text-xs text-slate-600 mt-0.5">
              Xuất tệp bảng tính trực tiếp từ cơ sở dữ liệu và hỗ trợ in ấn văn bản hành chính theo quy chuẩn khổ giấy A4
            </p>
          </div>

          <button
            onClick={handlePrint}
            className="self-start sm:self-auto px-3.5 py-1.5 border border-slate-400 text-slate-800 rounded text-xs font-semibold hover:bg-slate-50 transition"
          >
            In văn bản (Khổ A4)
          </button>
        </div>

        {/* Bộ lọc tìm kiếm */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 my-4">
          <input 
            type="text"
            placeholder="Tìm kiếm mã hiệu hoặc tên báo cáo..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="border border-slate-300 rounded px-3 py-1.5 text-xs w-full sm:w-80"
          />

          <div className="flex items-center gap-2">
            <span className="text-xs text-slate-600">Phân loại:</span>
            <select
              value={selectedCategory}
              onChange={(e) => setSelectedCategory(e.target.value)}
              className="border border-slate-300 rounded px-2.5 py-1.5 text-xs bg-white text-slate-800"
            >
              <option value="all">-- Toàn bộ báo cáo --</option>
              <option value="TỔNG HỢP ĐẢNG ỦY">Báo cáo Tổng hợp Đảng ủy</option>
              <option value="ĐÁNH GIÁ CÁN BỘ">Đánh giá & Xếp loại Cán bộ</option>
              <option value="KIỂM SOÁT TỶ LỆ">Kiểm soát tỷ lệ trần 20%</option>
            </select>
          </div>
        </div>

        {/* Danh sách báo cáo */}
        <div className="overflow-x-auto border border-slate-300 rounded">
          <table className="w-full text-xs text-slate-800 border-collapse">
            <thead>
              <tr className="bg-slate-100 font-bold border-b border-slate-300 text-left">
                <th className="border-r border-slate-300 p-2.5 text-center w-10">STT</th>
                <th className="border-r border-slate-300 p-2.5 w-24">Mã số</th>
                <th className="border-r border-slate-300 p-2.5">Tên văn bản / Báo cáo thống kê</th>
                <th className="border-r border-slate-300 p-2.5 w-44">Nhóm báo cáo</th>
                <th className="border-r border-slate-300 p-2.5 w-24 text-center">Định dạng</th>
                <th className="p-2.5 text-center w-36">Thao tác kết xuất</th>
              </tr>
            </thead>
            <tbody>
              {filteredReports.map((rep, idx) => (
                <tr key={rep.id} className="border-b border-slate-200 hover:bg-slate-50">
                  <td className="border-r border-slate-200 p-2 text-center">{idx + 1}</td>
                  <td className="border-r border-slate-200 p-2 font-mono font-bold text-slate-900">{rep.code}</td>
                  <td className="border-r border-slate-200 p-2">
                    <div className="font-semibold text-slate-950">{rep.title}</div>
                    <div className="text-[11px] text-slate-600 mt-0.5">{rep.description}</div>
                  </td>
                  <td className="border-r border-slate-200 p-2 text-slate-700">{rep.category}</td>
                  <td className="border-r border-slate-200 p-2 text-center font-mono font-bold text-slate-800">
                    {rep.fileType}
                  </td>
                  <td className="p-2 text-center">
                    <button
                      onClick={() => handleDownload(rep)}
                      disabled={downloadingId === rep.id}
                      className="px-3 py-1 bg-slate-900 text-white rounded text-[11px] font-semibold hover:bg-slate-800 disabled:opacity-50 transition"
                    >
                      {downloadingId === rep.id ? "Đang xuất..." : "Tải tệp bảng tính"}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
