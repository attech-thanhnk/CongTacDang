"use client";

import React, { useState } from "react";
import Link from "next/link";
import { reportService } from "@/services/reportService";

interface StandardForm {
  id: string;
  code: string;
  title: string;
  group: string;
  targetUser: string;
  purpose: string;
  legalBase: string;
  downloadEndpoint?: string;
  downloadFileName?: string;
}

const STANDARD_FORMS: StandardForm[] = [
  {
    id: "form-01",
    code: "Mẫu số 01",
    title: "Phiếu giao việc / Đăng ký sản phẩm, công việc chuyên môn hằng quý",
    group: "Nhóm I: Giao việc & Tự đăng ký",
    targetUser: "Cán bộ lãnh đạo, quản lý các cấp đăng ký 03 - 07 công việc với cấp trên",
    purpose: "Thống nhất đầu ra, mốc quý, tiêu chuẩn kỹ thuật và trọng số (tổng 70 điểm). Thực hiện trong 05 ngày làm việc đầu quý.",
    legalBase: "Hướng dẫn 03-HD/TVĐU (Trang 40)"
  },
  {
    id: "form-02",
    code: "Mẫu số 02",
    title: "Phiếu tự đánh giá kết quả thực hiện sản phẩm, công việc hằng quý",
    group: "Nhóm I: Giao việc & Tự đăng ký",
    targetUser: "Cá nhân cán bộ tự chấm điểm kết quả thực hiện từng sản phẩm chuyên môn",
    purpose: "Đánh giá theo 4 tiêu chí A (Khối lượng), B (Chất lượng), C (Tiến độ), D (Hiệu quả) theo hệ số khối chức danh.",
    legalBase: "Hướng dẫn 03-HD/TVĐU (Trang 41)"
  },
  {
    id: "form-09a",
    code: "Mẫu số 09A",
    title: "Phiếu tự chấm điểm đánh giá, xếp loại chất lượng cán bộ lãnh đạo, quản lý",
    group: "Nhóm II: Tự chấm điểm & Đánh giá",
    targetUser: "Cán bộ lãnh đạo, quản lý thuộc diện Đảng ủy quản lý",
    purpose: "Thang điểm 100: Nhóm tiêu chí chung tối đa 30 điểm (T1-T6: tư tưởng chính trị, đạo đức, lối sống, đổi mới sáng tạo) và Nhóm sản phẩm tối đa 70 điểm.",
    legalBase: "Hướng dẫn 03-HD/TVĐU (Trang 50-52)"
  },
  {
    id: "form-09b",
    code: "Mẫu số 09B",
    title: "Phiếu tự chấm điểm áp dụng cho kỳ chuyển tiếp Quý III/2026",
    group: "Nhóm II: Tự chấm điểm & Đánh giá",
    targetUser: "Cán bộ, lãnh đạo áp dụng trong giai đoạn đầu chuẩn hóa",
    purpose: "Đánh giá chuyển tiếp bảo đảm tính kế thừa, kiểm soát tiến độ và phân loại sơ bộ trước khi áp dụng toàn diện.",
    legalBase: "Hướng dẫn 03-HD/TVĐU (Trang 53-54)"
  },
  {
    id: "form-10",
    code: "Mẫu số 10",
    title: "Phiếu nhận xét, đánh giá của cấp ủy cơ sở / Chi bộ đối với cán bộ",
    group: "Nhóm III: Nhận xét cấp ủy & Thẩm định",
    targetUser: "Chi ủy, Bí thư Chi bộ nơi cán bộ sinh hoạt Đảng",
    purpose: "Ghi nhận ý kiến nhận xét của Chi bộ về phẩm chất, đạo đức, sự gương mẫu và kết quả hoàn thành nhiệm vụ.",
    legalBase: "Hướng dẫn 03-HD/TVĐU (Trang 55)"
  },
  {
    id: "form-14",
    code: "Mẫu số 14",
    title: "Bảng tổng hợp kết quả đánh giá, xếp loại cán bộ 2 vai toàn Đảng bộ",
    group: "Nhóm IV: Tổng hợp & Xếp loại",
    targetUser: "Tổ Thẩm định, Ban Tổ chức Đảng ủy, Ban Thường vụ",
    purpose: "Tổng hợp kết quả hồ sơ cán bộ lãnh đạo, quản lý 2 vai phục vụ đánh giá, xếp loại công tác cán bộ toàn công ty ATTECH.",
    legalBase: "Hướng dẫn 03-HD/TVĐU (Trang 60)",
    downloadEndpoint: "/api/reports/form-14",
    downloadFileName: "Mau_14_DanhSachHoSoCanBo_Q3_2026.xlsx"
  },
  {
    id: "form-15",
    code: "Mẫu số 15",
    title: "Bảng tổng hợp kiểm soát tỷ lệ hoàn thành xuất sắc nhiệm vụ (Trần 20%)",
    group: "Nhóm IV: Tổng hợp & Xếp loại",
    targetUser: "Ban Thường vụ Đảng ủy Công ty ATTECH",
    purpose: "Kiểm soát chặt chẽ tỷ lệ cán bộ hoàn thành xuất sắc không vượt quá 20% tổng số cán bộ hoàn thành tốt nhiệm vụ trở lên theo từng Chi bộ.",
    legalBase: "Hướng dẫn 03-HD/TVĐU (Trang 61)",
    downloadEndpoint: "/api/reports/form-15",
    downloadFileName: "Mau_15_TongHopTran20_Q3_2026.xlsx"
  }
];

export default function FormsPage() {
  const [searchTerm, setSearchTerm] = useState("");
  const [selectedGroup, setSelectedGroup] = useState("all");
  const [downloadingId, setDownloadingId] = useState<string | null>(null);

  const handleDownload = (endpoint: string, fileName: string, id: string) => {
    setDownloadingId(id);
    reportService.downloadReport(endpoint, fileName);
    setTimeout(() => setDownloadingId(null), 1000);
  };

  const handlePrint = () => {
    reportService.printDocument();
  };

  const filteredForms = STANDARD_FORMS.filter(f => {
    const matchSearch = f.title.toLowerCase().includes(searchTerm.toLowerCase()) ||
                        f.code.toLowerCase().includes(searchTerm.toLowerCase()) ||
                        f.purpose.toLowerCase().includes(searchTerm.toLowerCase());
    const matchGroup = selectedGroup === "all" || f.group === selectedGroup;
    return matchSearch && matchGroup;
  });

  return (
    <div className="space-y-6 font-serif max-w-5xl mx-auto">
      {/* Tiêu đề trang */}
      <div className="bg-white border border-slate-300 rounded p-5 shadow-sm">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-300 pb-3">
          <div>
            <h1 className="text-base font-bold text-slate-900 uppercase">
              TRUNG TÂM BIỂU MẪU CHUẨN CÔNG TÁC ĐẢNG
            </h1>
            <p className="text-xs text-slate-600 mt-0.5">
              Hệ thống danh mục biểu mẫu phục vụ công tác đánh giá, xếp loại theo Hướng dẫn số 03-HD/TVĐU và Nghị định 30/2020/NĐ-CP
            </p>
          </div>

          <button
            onClick={handlePrint}
            className="self-start sm:self-auto px-3.5 py-1.5 border border-slate-400 text-slate-800 rounded text-xs font-semibold hover:bg-slate-50 transition"
          >
            In danh mục (Khổ A4)
          </button>
        </div>

        {/* Bộ lọc tìm kiếm */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 my-4">
          <input 
            type="text"
            placeholder="Tìm kiếm theo mã số (Mẫu 01, Mẫu 14...) hoặc tên biểu mẫu..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="border border-slate-300 rounded px-3 py-1.5 text-xs w-full sm:w-80"
          />

          <div className="flex items-center gap-2">
            <span className="text-xs text-slate-600">Nhóm nghiệp vụ:</span>
            <select
              value={selectedGroup}
              onChange={(e) => setSelectedGroup(e.target.value)}
              className="border border-slate-300 rounded px-2.5 py-1.5 text-xs bg-white text-slate-800"
            >
              <option value="all">-- Toàn bộ nhóm biểu mẫu --</option>
              <option value="Nhóm I: Giao việc & Tự đăng ký">Nhóm I: Giao việc & Tự đăng ký</option>
              <option value="Nhóm II: Tự chấm điểm & Đánh giá">Nhóm II: Tự chấm điểm & Đánh giá</option>
              <option value="Nhóm III: Nhận xét cấp ủy & Thẩm định">Nhóm III: Nhận xét cấp ủy & Thẩm định</option>
              <option value="Nhóm IV: Tổng hợp & Xếp loại">Nhóm IV: Tổng hợp & Xếp loại</option>
            </select>
          </div>
        </div>

        {/* Bảng danh mục biểu mẫu */}
        <div className="overflow-x-auto border border-slate-300 rounded">
          <table className="w-full text-xs text-slate-800 border-collapse">
            <thead>
              <tr className="bg-slate-100 font-bold border-b border-slate-300 text-left">
                <th className="border-r border-slate-300 p-2.5 text-center w-10">STT</th>
                <th className="border-r border-slate-300 p-2.5 w-24">Ký hiệu</th>
                <th className="border-r border-slate-300 p-2.5">Tên biểu mẫu & Mục đích sử dụng</th>
                <th className="border-r border-slate-300 p-2.5 w-44">Đối tượng áp dụng</th>
                <th className="border-r border-slate-300 p-2.5 w-32">Căn cứ ban hành</th>
                <th className="p-2.5 text-center w-32">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {filteredForms.map((f, idx) => (
                <tr key={f.id} className="border-b border-slate-200 hover:bg-slate-50">
                  <td className="border-r border-slate-200 p-2 text-center">{idx + 1}</td>
                  <td className="border-r border-slate-200 p-2 font-mono font-bold text-slate-900">{f.code}</td>
                  <td className="border-r border-slate-200 p-2">
                    <div className="font-semibold text-slate-950">{f.title}</div>
                    <div className="text-[11px] text-slate-600 mt-0.5">{f.purpose}</div>
                  </td>
                  <td className="border-r border-slate-200 p-2 text-slate-700">{f.targetUser}</td>
                  <td className="border-r border-slate-200 p-2 text-slate-600 italic">{f.legalBase}</td>
                  <td className="p-2 text-center">
                    {f.downloadEndpoint ? (
                      <button
                        onClick={() => handleDownload(f.downloadEndpoint!, f.downloadFileName!, f.id)}
                        disabled={downloadingId === f.id}
                        className="px-3 py-1 bg-slate-900 text-white rounded text-[11px] font-semibold hover:bg-slate-800 disabled:opacity-50 transition"
                      >
                        {downloadingId === f.id ? "Đang xuất..." : "Xuất dữ liệu"}
                      </button>
                    ) : (
                      <Link
                        href="/attachments"
                        className="px-2.5 py-1 border border-slate-300 text-slate-700 rounded text-[11px] hover:bg-slate-100 transition inline-block"
                      >
                        Tải văn bản
                      </Link>
                    )}
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
