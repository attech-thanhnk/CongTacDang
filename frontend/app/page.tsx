import React from 'react';
import StatCards from '@/components/dashboard/StatCards';
import ProcessTimeline from '@/components/dashboard/ProcessTimeline';
import BranchProgressGrid from '@/components/dashboard/BranchProgressGrid';
import Link from 'next/link';
import { FileSpreadsheet, ShieldCheck, ArrowRight, Award, Vote } from 'lucide-react';

export default function DashboardPage() {
  return (
    <div className="space-y-6 animate-in fade-in duration-300">
      {/* Banner Chào mừng & Thông báo nhiệm vụ quý */}
      <div className="rounded-2xl bg-gradient-to-r from-slate-900 via-rose-950 to-slate-900 text-white p-6 shadow-md border border-rose-900/40 relative overflow-hidden flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
        <div className="space-y-1.5 z-10">
          <div className="inline-flex items-center gap-2 bg-amber-400/20 text-amber-300 border border-amber-400/30 px-2.5 py-0.5 rounded-full text-xs font-semibold">
            <span>★</span>
            <span>HỆ THỐNG QUẢN TRỊ ĐẢNG BỘ ATTECH</span>
          </div>
          <h2 className="text-xl md:text-2xl font-black text-white tracking-tight">
            Đánh Giá Định Kỳ Cán Bộ Lãnh Đạo, Quản Lý Quý III / 2026
          </h2>
          <p className="text-xs md:text-sm text-slate-300 max-w-2xl">
            Thực hiện theo Hướng dẫn số <strong>03-HD/TVĐU ngày 10/9/2026</strong> của Ban Thường vụ Đảng ủy Tổng công ty Quản lý bay Việt Nam.
          </p>
        </div>

        <div className="flex items-center gap-2.5 z-10">
          <Link
            href="/bieu-mau"
            className="px-4 py-2 bg-white/10 hover:bg-white/20 text-white rounded-lg text-xs font-bold backdrop-blur border border-white/20 transition flex items-center gap-1.5"
          >
            <FileSpreadsheet className="w-4 h-4 text-amber-300" />
            Xem 16 Biểu mẫu
          </Link>
          <Link
            href="/bo-phieu"
            className="px-4 py-2 bg-dang-crimson hover:bg-red-800 text-white rounded-lg text-xs font-bold shadow-md transition flex items-center gap-1.5"
          >
            <Vote className="w-4 h-4 text-dang-gold" />
            Hòm phiếu Chi bộ
          </Link>
        </div>
      </div>

      {/* 4 Thẻ chỉ số tổng quan điều hành */}
      <StatCards />

      {/* Timeline Tiến trình 5 Bước theo 03-HD/TVĐU */}
      <ProcessTimeline />

      {/* Tiến độ chi tiết 4 Chi bộ trực thuộc ATTECH */}
      <BranchProgressGrid />
    </div>
  );
}
