"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { userService } from "@/services/userService";
import { organizationService } from "@/services/organizationService";
import { attachmentService } from "@/services/attachmentService";

export default function DashboardPage() {
  const [cadreCount, setCadreCount] = useState<number | null>(null);
  const [branchCount, setBranchCount] = useState<number | null>(null);
  const [fileCount, setFileCount] = useState<number | null>(null);

  useEffect(() => {
    // Gọi thông qua các Services độc lập đã bóc tách
    userService.getUsers()
      .then(data => setCadreCount(data.length))
      .catch(() => {});

    organizationService.getBranches()
      .then(data => setBranchCount(data.length))
      .catch(() => {});

    attachmentService.getAttachments()
      .then(data => setFileCount(data.length))
      .catch(() => {});
  }, []);

  const modules = [
    {
      num: "I",
      title: "QUẢN TRỊ NGƯỜI DÙNG & ĐƠN VỊ",
      desc: "Quản lý danh sách hồ sơ cán bộ lãnh đạo, quản lý 2 vai (Đảng vụ và Chính quyền), cơ cấu Chi bộ và Phòng ban chuyên môn.",
      href: "/users",
      action: "Truy cập quản trị"
    },
    {
      num: "II",
      title: "PHÂN QUYỀN & KIỂM SOÁT VAI TRÒ HỆ THỐNG",
      desc: "Kiểm soát ma trận phân quyền trên 5 nhóm vai trò: Quản trị viên, Cán bộ, Bí thư Chi bộ, Tổ Thẩm định, Ban Thường vụ.",
      href: "/users?tab=roles",
      action: "Xem ma trận phân quyền"
    },
    {
      num: "III",
      title: "QUẢN LÝ TỆP TIN & VĂN BẢN ĐÍNH KÈM",
      desc: "Lưu trữ tập trung tệp minh chứng, biểu mẫu đã ký, văn bản hướng dẫn trong mạng nội bộ công ty.",
      href: "/attachments",
      action: "Quản lý tệp tin"
    },
    {
      num: "IV",
      title: "KẾT XUẤT BÁO CÁO & DỮ LIỆU BẢNG TÍNH",
      desc: "Xuất dữ liệu bảng tính phục vụ công tác báo cáo và in ấn văn bản hành chính theo quy chuẩn khổ giấy A4.",
      href: "/reports",
      action: "Kết xuất báo cáo"
    },
    {
      num: "V",
      title: "TRUNG TÂM BIỂU MẪU CHUẨN",
      desc: "Hệ thống danh mục biểu mẫu phục vụ công tác đánh giá, xếp loại định kỳ theo đúng quy định của Đảng ủy.",
      href: "/forms",
      action: "Xem danh mục biểu mẫu"
    }
  ];

  return (
    <div className="space-y-6 font-serif max-w-5xl mx-auto">
      {/* Tiêu ngữ & Quốc hiệu văn bản chuẩn nhà nước */}
      <div className="bg-white border border-slate-300 rounded p-6 shadow-sm">
        <div className="flex flex-col sm:flex-row justify-between items-start text-center sm:text-left gap-4 pb-4 border-b border-slate-300">
          <div>
            <p className="text-xs font-semibold text-slate-700 uppercase">
              Đảng bộ Tổng công ty Quản lý bay Việt Nam
            </p>
            <p className="text-xs font-bold text-slate-900 uppercase">
              Đảng bộ Công ty TNHH Kỹ thuật Quản lý bay
            </p>
            <div className="w-20 h-0.5 bg-slate-800 my-1 mx-auto sm:mx-0"></div>
            <p className="text-[11px] text-slate-600">Số: 03-HD/TVĐU</p>
          </div>

          <div className="text-center sm:text-right w-full sm:w-auto">
            <p className="text-xs font-bold text-slate-900 uppercase">
              CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM
            </p>
            <p className="text-xs font-bold text-slate-900">
              Độc lập - Tự do - Hạnh phúc
            </p>
            <div className="w-28 h-0.5 bg-slate-800 my-1 mx-auto sm:ml-auto sm:mr-0"></div>
            <p className="text-[11px] text-slate-600 italic">
              Hà Nội, ngày 16 tháng 09 năm 2026
            </p>
          </div>
        </div>

        {/* Tên Bảng điều hành */}
        <div className="text-center py-5">
          <h1 className="text-lg md:text-xl font-bold text-slate-950 uppercase tracking-wide">
            HỆ THỐNG QUẢN TRỊ & ĐÁNH GIÁ CÁN BỘ LÃNH ĐẠO, QUẢN LÝ
          </h1>
          <p className="text-xs text-slate-700 italic mt-1">
            (Áp dụng trong toàn Đảng bộ Công ty TNHH Kỹ thuật Quản lý bay - ATTECH)
          </p>
        </div>

        {/* Bảng số liệu tổng hợp thuần Việt */}
        <div className="overflow-x-auto">
          <table className="w-full text-xs border border-slate-400 text-slate-800">
            <thead>
              <tr className="bg-slate-100 font-bold border-b border-slate-400">
                <th className="border-r border-slate-400 p-2 text-center w-12">STT</th>
                <th className="border-r border-slate-400 p-2 text-left">Chỉ tiêu theo dõi</th>
                <th className="border-r border-slate-400 p-2 text-center w-36">Hiện trạng dữ liệu</th>
                <th className="p-2 text-left">Ghi chú quản lý</th>
              </tr>
            </thead>
            <tbody>
              <tr className="border-b border-slate-300">
                <td className="border-r border-slate-300 p-2 text-center">1</td>
                <td className="border-r border-slate-300 p-2 font-medium">Tổng số Cán bộ lãnh đạo, quản lý</td>
                <td className="border-r border-slate-300 p-2 text-center font-bold">
                  {cadreCount !== null ? `${cadreCount} đồng chí` : "Đang kiểm tra dữ liệu..."}
                </td>
                <td className="p-2 text-slate-600">Hồ sơ cán bộ lãnh đạo, quản lý (Đã đồng bộ)</td>
              </tr>
              <tr className="border-b border-slate-300 bg-slate-50/50">
                <td className="border-r border-slate-300 p-2 text-center">2</td>
                <td className="border-r border-slate-300 p-2 font-medium">Chi bộ Đảng trực thuộc</td>
                <td className="border-r border-slate-300 p-2 text-center font-bold">
                  {branchCount !== null ? `${branchCount} Chi bộ` : "Đang kiểm tra dữ liệu..."}
                </td>
                <td className="p-2 text-slate-600">Danh mục Chi bộ trực thuộc Đảng bộ công ty</td>
              </tr>
              <tr className="border-b border-slate-300">
                <td className="border-r border-slate-300 p-2 text-center">3</td>
                <td className="border-r border-slate-300 p-2 font-medium">Tệp tin & Văn bản đã lưu trữ</td>
                <td className="border-r border-slate-300 p-2 text-center font-bold">
                  {fileCount !== null ? `${fileCount} tệp tin` : "Đang kiểm tra dữ liệu..."}
                </td>
                <td className="p-2 text-slate-600">Kho lưu trữ tệp tin văn bản điện tử</td>
              </tr>
              <tr className="bg-slate-50/50">
                <td className="border-r border-slate-300 p-2 text-center">4</td>
                <td className="border-r border-slate-300 p-2 font-medium">Mô hình vận hành hạ tầng</td>
                <td className="border-r border-slate-300 p-2 text-center font-bold text-emerald-800">Mạng nội bộ</td>
                <td className="p-2 text-slate-600">Máy chủ cơ sở dữ liệu nội bộ Công ty ATTECH</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      {/* Danh mục 5 Phân hệ công tác */}
      <div className="bg-white border border-slate-300 rounded p-6 shadow-sm space-y-4">
        <h2 className="text-sm font-bold text-slate-900 uppercase border-b border-slate-300 pb-2">
          DANH MỤC CÁC PHÂN HỆ VẬN HÀNH SẴN SÀNG
        </h2>

        <div className="divide-y divide-slate-200">
          {modules.map((m) => (
            <div key={m.num} className="py-3 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
              <div className="space-y-0.5">
                <p className="text-xs font-bold text-slate-900">
                  {m.num}. {m.title}
                </p>
                <p className="text-xs text-slate-600 leading-relaxed max-w-2xl">
                  {m.desc}
                </p>
              </div>

              <div className="shrink-0">
                <Link
                  href={m.href}
                  className="inline-block px-3 py-1.5 border border-slate-400 hover:border-slate-800 text-slate-800 hover:bg-slate-50 rounded text-xs font-semibold transition"
                >
                  {m.action} &rarr;
                </Link>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
