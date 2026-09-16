"use client";

import React from "react";
import Link from "next/link";
import { Building2, CheckCircle2, ChevronRight, Vote, Users } from "lucide-react";

interface BranchItem {
  code: string;
  name: string;
  secretary: string;
  totalMembers: number;
  stepStatus: string;
  votedRatio: string;
  excellentCount: number;
}

export default function BranchProgressGrid() {
  const branches: BranchItem[] = [
    {
      code: "CB-KT",
      name: "Chi bộ Khối Kỹ thuật",
      secretary: "Đ/c Nguyễn Văn A",
      totalMembers: 18,
      stepStatus: "Đang bỏ phiếu kín (Bước 3)",
      votedRatio: "16 / 18 phiếu (88.9%)",
      excellentCount: 3
    },
    {
      code: "CB-SX",
      name: "Chi bộ Khối Sản xuất",
      secretary: "Đ/c Lê Quang C",
      totalMembers: 16,
      stepStatus: "Đã hoàn thành kiểm phiếu",
      votedRatio: "16 / 16 phiếu (100%)",
      excellentCount: 2
    },
    {
      code: "CB-DV",
      name: "Chi bộ Khối Dịch vụ",
      secretary: "Đ/c Hoàng Minh E",
      totalMembers: 14,
      stepStatus: "Đã mở hòm phiếu điện tử",
      votedRatio: "12 / 14 phiếu (85.7%)",
      excellentCount: 2
    },
    {
      code: "CB-VP",
      name: "Chi bộ Cơ quan Văn phòng",
      secretary: "Đ/c Trần Thị B",
      totalMembers: 20,
      stepStatus: "Đang bỏ phiếu kín (Bước 3)",
      votedRatio: "19 / 20 phiếu (95.0%)",
      excellentCount: 4
    }
  ];

  return (
    <div className="bg-white rounded-2xl shadow-sm border border-slate-200/80 p-6 space-y-4">
      <div className="flex items-center justify-between border-b border-slate-100 pb-3">
        <div>
          <h3 className="font-bold text-base text-slate-900 tracking-tight">
            Tiến Độ Triển Khai Tại 4 Chi Bộ Trực Thuộc ATTECH
          </h3>
          <p className="text-xs text-slate-500">
            Giám sát thời gian thực tiến độ nộp Mẫu 01, Mẫu 02 và tỷ lệ bỏ phiếu kín Mẫu 11
          </p>
        </div>
        <Link
          href="/bo-phieu"
          className="text-xs font-semibold text-dang-crimson hover:underline flex items-center gap-1"
        >
          Hòm phiếu chi bộ
          <ChevronRight className="w-3.5 h-3.5" />
        </Link>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {branches.map((b) => (
          <div
            key={b.code}
            className="p-4 rounded-xl border border-slate-200 hover:border-slate-300 hover:shadow-sm transition-all flex flex-col justify-between"
          >
            <div>
              <div className="flex items-center justify-between mb-1.5">
                <span className="font-mono font-bold text-[11px] px-2 py-0.5 rounded bg-dang-crimson/10 text-dang-crimson">
                  {b.code}
                </span>
                <span className="text-xs text-slate-400 font-semibold">{b.totalMembers} cán bộ</span>
              </div>

              <h4 className="font-bold text-sm text-slate-900">{b.name}</h4>
              <p className="text-xs text-slate-500 mt-1">Bí thư: <span className="font-semibold text-slate-700">{b.secretary}</span></p>

              <div className="mt-3 p-2 bg-slate-50 rounded-lg text-xs space-y-1">
                <div className="flex justify-between text-[11px]">
                  <span className="text-slate-500">Tiến độ phiếu:</span>
                  <span className="font-bold text-slate-800">{b.votedRatio}</span>
                </div>
                <div className="flex justify-between text-[11px]">
                  <span className="text-slate-500">Đề xuất Xuất sắc:</span>
                  <span className="font-bold text-dang-crimson">{b.excellentCount} chỉ tiêu</span>
                </div>
              </div>
            </div>

            <div className="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between text-[11px]">
              <span className="text-emerald-700 font-semibold flex items-center gap-1">
                <CheckCircle2 className="w-3 h-3" />
                {b.stepStatus}
              </span>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
