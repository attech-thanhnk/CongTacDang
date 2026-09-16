"use client";

import React, { useState } from "react";
import { 
  Bell, 
  Calendar, 
  Building, 
  Clock, 
  ShieldCheck, 
  UserCircle2, 
  ChevronDown,
  ArrowRightLeft
} from "lucide-react";

export function AppHeader() {
  const [currentRole, setCurrentRole] = useState("BI_THU_CHI_BO");
  const [selectedBranch, setSelectedBranch] = useState("Chi bộ Khối Kỹ thuật");

  const rolesList = [
    { code: "CAN_BO", name: "Cán bộ Lãnh đạo (Nguyễn Văn A)", desc: "Mẫu 01, 02 cá nhân" },
    { code: "BI_THU_CHI_BO", name: "Bí thư Chi bộ Khối Kỹ thuật", desc: "Chủ trì bỏ phiếu kín Mẫu 11-13" },
    { code: "TO_THAM_DINH", name: "Tổ Thẩm định Đảng ủy", desc: "Soát vênh điểm >= 5đ & Trần 20%" },
    { code: "BAN_THUONG_VU", name: "Ban Thường vụ Đảng ủy", desc: "Chuẩn y xếp loại & Mẫu 16" },
    { code: "ADMIN_HE_THONG", name: "Quản trị viên Hệ thống", desc: "Toàn quyền quản trị & 68 CB" }
  ];

  const currentRoleObj = rolesList.find(r => r.code === currentRole) || rolesList[1];

  return (
    <header className="bg-white border-b border-slate-200 sticky top-0 z-20 shadow-sm">
      <div className="px-6 py-3 flex items-center justify-between gap-4">
        {/* Left: Organization context */}
        <div className="flex items-center gap-4">
          <div className="flex items-center gap-2 px-3 py-1.5 bg-slate-100 rounded-lg text-xs font-semibold text-slate-700">
            <Building className="w-3.5 h-3.5 text-attech-blue" />
            <span>Công ty TNHH Kỹ thuật Quản lý bay (ATTECH)</span>
          </div>

          <div className="flex items-center gap-2 px-3 py-1.5 bg-amber-50 border border-amber-200/80 rounded-lg text-xs font-semibold text-amber-900">
            <Calendar className="w-3.5 h-3.5 text-dang-crimson" />
            <span>Kỳ đánh giá: <strong>Quý III/2026</strong></span>
          </div>

          <div className="hidden lg:flex items-center gap-2 text-xs text-slate-500">
            <Clock className="w-3.5 h-3.5 text-amber-600" />
            <span>Hạn nộp Mẫu 02: <strong className="text-slate-800">12/09/2026</strong> (Còn 2 ngày)</span>
          </div>
        </div>

        {/* Right: Role Switcher & Notifications */}
        <div className="flex items-center gap-3">
          {/* Role Simulator Dropdown */}
          <div className="flex items-center gap-2 bg-slate-50 border border-slate-200 rounded-xl px-2.5 py-1 text-xs">
            <ArrowRightLeft className="w-3.5 h-3.5 text-slate-400" />
            <span className="text-slate-400 text-[11px] font-medium hidden sm:inline">Góc nhìn:</span>
            <select
              value={currentRole}
              onChange={(e) => setCurrentRole(e.target.value)}
              className="bg-transparent font-bold text-slate-800 focus:outline-none cursor-pointer text-xs py-0.5"
            >
              {rolesList.map(r => (
                <option key={r.code} value={r.code}>
                  {r.name}
                </option>
              ))}
            </select>
          </div>

          {/* Quick Notification Bell */}
          <button 
            title="Thông báo hệ thống"
            className="w-9 h-9 rounded-xl border border-slate-200 hover:bg-slate-100 flex items-center justify-center text-slate-600 relative transition"
          >
            <Bell className="w-4 h-4" />
            <span className="w-2 h-2 rounded-full bg-dang-crimson absolute top-2 right-2 ring-2 ring-white"></span>
          </button>
        </div>
      </div>
    </header>
  );
}

export default AppHeader;
