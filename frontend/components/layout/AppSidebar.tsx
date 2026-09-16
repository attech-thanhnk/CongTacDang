"use client";

import React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";

import { userService, UserProfile } from "@/services/userService";

export function AppSidebar() {
  const pathname = usePathname();
  const [profile, setProfile] = React.useState<UserProfile | null>(null);

  const [cadreCount, setCadreCount] = React.useState<number | null>(null);

  React.useEffect(() => {
    userService.getProfile()
      .then(data => setProfile(data))
      .catch(() => {});

    userService.getUsers()
      .then(data => setCadreCount(data.length))
      .catch(() => {});
  }, []);

  const menuItems = [
    {
      index: "1",
      title: "Bảng điều hành tổng hợp",
      href: "/",
      note: ""
    },
    {
      index: "2",
      title: "Quản trị người dùng & đơn vị",
      href: "/users",
      note: cadreCount !== null ? `${cadreCount} cán bộ` : ""
    },
    {
      index: "3",
      title: "Trung tâm biểu mẫu chuẩn",
      href: "/forms",
      note: "Mẫu A4"
    },
    {
      index: "4",
      title: "Quản lý tệp tin & upload",
      href: "/attachments",
      note: "Tệp đính kèm"
    },
    {
      index: "5",
      title: "Kết xuất dữ liệu & báo cáo",
      href: "/reports",
      note: "Excel / In ấn"
    }
  ];

  return (
    <aside className="w-64 bg-slate-900 text-slate-100 flex flex-col h-screen sticky top-0 border-r border-slate-800 shrink-0 select-none z-30 font-serif">
      {/* Tiêu ngữ & Đơn vị theo quy chuẩn hành chính */}
      <div className="p-4 border-b border-slate-800 bg-slate-950/60 text-center">
        <p className="text-[11px] font-semibold text-slate-300 uppercase tracking-wide leading-tight">
          Đảng ủy Tổng công ty QLB VN
        </p>
        <h1 className="text-xs font-bold text-amber-300 uppercase mt-1 leading-snug">
          ĐẢNG BỘ CÔNG TY ATTECH
        </h1>
        <div className="w-12 h-0.5 bg-amber-400/60 mx-auto my-1.5"></div>
        <p className="text-[11px] text-slate-400 italic">
          Hệ thống Quản trị & Đánh giá Cán bộ
        </p>
      </div>

      {/* Thông tin Cán bộ công vụ */}
      <div className="p-3 border-b border-slate-800 bg-slate-900/90 text-xs">
        <div className="border border-slate-700/80 rounded p-2.5 bg-slate-950/30 space-y-1">
          <div className="flex justify-between items-center text-[11px] text-slate-400 border-b border-slate-800 pb-1">
            <span>Tài khoản:</span>
            <span className="text-amber-300 font-bold">
              {profile ? profile.fullName : "Đang kiểm tra hồ sơ..."}
            </span>
          </div>
          <p className="text-[11px] text-slate-300">
            <span className="text-slate-400">Đảng vụ:</span> {profile ? profile.partyRole : "—"}
          </p>
          <p className="text-[11px] text-slate-300">
            <span className="text-slate-400">Chính quyền:</span> {profile ? profile.adminTitle : "—"}
          </p>
        </div>
      </div>

      {/* Danh mục Chức năng chuẩn văn bản */}
      <div className="flex-1 overflow-y-auto p-3 space-y-1">
        <p className="px-2 py-1 text-[11px] font-bold text-slate-400 uppercase tracking-wider border-b border-slate-800 mb-2">
          Danh mục chức năng
        </p>

        <nav className="space-y-1">
          {menuItems.map((item) => {
            const isActive = pathname === item.href;
            return (
              <Link
                key={item.href}
                href={item.href}
                className={`flex items-center justify-between px-3 py-2 rounded text-xs transition-all ${
                  isActive
                    ? "bg-rose-950 text-amber-300 font-bold border-l-4 border-amber-400 shadow-sm"
                    : "text-slate-300 hover:bg-slate-800 hover:text-white"
                }`}
              >
                <div className="flex items-baseline gap-2 min-w-0">
                  <span className="font-mono text-slate-400 text-[11px]">
                    {item.index}.
                  </span>
                  <span className="truncate">{item.title}</span>
                </div>

                {item.note && (
                  <span className="text-[10px] text-slate-400 italic shrink-0">
                    ({item.note})
                  </span>
                )}
              </Link>
            );
          })}
        </nav>
      </div>

      {/* Chân trang thông tin quản lý nội bộ */}
      <div className="p-3 border-t border-slate-800 bg-slate-950/60 text-[11px] text-slate-400 text-center">
        <p>Mạng nội bộ LAN • Chuẩn 03-HD/TVĐU</p>
      </div>
    </aside>
  );
}

export default AppSidebar;
