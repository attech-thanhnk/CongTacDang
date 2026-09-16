"use client";

import React, { useState, useEffect } from "react";
import { userService, RoleItem, UserProfile } from "@/services/userService";

export function AppHeader() {
  const [currentRole, setCurrentRole] = useState("BI_THU_CHI_BO");
  const [roles, setRoles] = useState<RoleItem[]>([]);
  const [profile, setProfile] = useState<UserProfile | null>(null);

  useEffect(() => {
    userService.getRoles()
      .then(data => {
        setRoles(data);
        if (data.length > 0) setCurrentRole(data[0].code);
      })
      .catch(() => {});

    userService.getProfile()
      .then(p => setProfile(p))
      .catch(() => {});
  }, []);

  return (
    <header className="bg-white border-b border-slate-300 sticky top-0 z-20 font-serif">
      <div className="px-6 py-2.5 flex flex-col md:flex-row md:items-center justify-between gap-3">
        {/* Tiêu đề Đơn vị & Cơ quan hành chính */}
        <div className="flex flex-wrap items-center gap-4 text-xs">
          <div className="border-r border-slate-300 pr-4">
            <span className="font-bold text-slate-800 uppercase tracking-wide">
              Công ty TNHH Kỹ thuật Quản lý bay (ATTECH)
            </span>
          </div>

          <div className="text-slate-700">
            <span>Kỳ làm việc: </span>
            <strong className="text-rose-950">Quý III năm 2026</strong>
          </div>

          <div className="hidden lg:block text-slate-500 italic">
            (Căn cứ Hướng dẫn số 03-HD/TVĐU ngày 10/9/2026)
          </div>
        </div>

        {/* Chuyển đổi vai trò công vụ */}
        <div className="flex items-center gap-2 text-xs">
          <label htmlFor="roleSelect" className="text-slate-600 font-medium whitespace-nowrap">
            Vai trò làm việc:
          </label>
          <select
            id="roleSelect"
            value={currentRole}
            onChange={(e) => setCurrentRole(e.target.value)}
            className="border border-slate-300 rounded px-2.5 py-1 text-xs text-slate-800 bg-white font-semibold focus:outline-none focus:border-rose-900 cursor-pointer"
          >
            {roles.map(r => (
              <option key={r.code} value={r.code}>
                {r.name}
              </option>
            ))}
          </select>
        </div>
      </div>
    </header>
  );
}

export default AppHeader;
