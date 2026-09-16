"use client";

import React from "react";
import { Users, UserCheck, Vote, Award, TrendingUp } from "lucide-react";

export default function StatCards() {
  const stats = [
    {
      label: "TỔNG SỐ CÁN BỘ QUẢN LÝ",
      value: "68",
      unit: "cán bộ",
      change: "100% đã đăng ký Mẫu 01",
      changeType: "positive",
      icon: Users,
      gradient: "from-blue-600 to-indigo-700",
      accent: "border-blue-500",
      iconBg: "bg-blue-50 text-blue-600"
    },
    {
      label: "ĐÃ HOÀN THÀNH TỰ CHẤM",
      value: "65 / 68",
      unit: "cán bộ",
      change: "Tỷ lệ hoàn thành: 95.6%",
      changeType: "positive",
      icon: UserCheck,
      gradient: "from-emerald-600 to-teal-700",
      accent: "border-emerald-500",
      iconBg: "bg-emerald-50 text-emerald-600"
    },
    {
      label: "HỘI NGHỊ CHI BỘ & BỎ PHIẾU",
      value: "4 / 4",
      unit: "Chi bộ",
      change: "4 Chi bộ đã mở hòm phiếu",
      changeType: "positive",
      icon: Vote,
      gradient: "from-amber-500 to-orange-600",
      accent: "border-amber-500",
      iconBg: "bg-amber-50 text-amber-600"
    },
    {
      label: "CẢNH BÁO TRẦN 20% XUẤT SẮC",
      value: "18.5%",
      unit: "Hợp lệ",
      change: "Ngưỡng khống chế tối đa: 20.0%",
      changeType: "neutral",
      icon: Award,
      gradient: "from-rose-600 to-rose-800",
      accent: "border-rose-500",
      iconBg: "bg-rose-50 text-dang-crimson"
    }
  ];

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
      {stats.map((stat, idx) => {
        const Icon = stat.icon;
        return (
          <div
            key={idx}
            className="bg-white rounded-2xl shadow-sm border border-slate-200/80 p-5 relative overflow-hidden flex flex-col justify-between hover:shadow-md transition-shadow"
          >
            <div className={`absolute top-0 left-0 right-0 h-1 bg-gradient-to-r ${stat.gradient}`} />
            <div className="flex items-start justify-between">
              <div>
                <p className="text-[10px] font-extrabold uppercase tracking-wider text-slate-400">
                  {stat.label}
                </p>
                <div className="mt-2 flex items-baseline gap-1.5">
                  <span className="text-2xl font-black text-slate-900 tracking-tight font-sans">
                    {stat.value}
                  </span>
                  <span className="text-xs font-semibold text-slate-500">{stat.unit}</span>
                </div>
              </div>
              <div className={`w-10 h-10 rounded-xl flex items-center justify-center shrink-0 ${stat.iconBg}`}>
                <Icon className="w-5 h-5" />
              </div>
            </div>

            <div className="mt-4 pt-3 border-t border-slate-100 flex items-center gap-1.5 text-xs">
              <TrendingUp className="w-3.5 h-3.5 text-emerald-600" />
              <span className="text-slate-600 font-medium text-[11px]">{stat.change}</span>
            </div>
          </div>
        );
      })}
    </div>
  );
}
