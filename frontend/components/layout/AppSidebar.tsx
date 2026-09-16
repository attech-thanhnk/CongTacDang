"use client";

import React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { 
  LayoutDashboard, 
  FileSpreadsheet, 
  FileEdit, 
  Vote, 
  CheckCheck, 
  SlidersHorizontal, 
  ShieldCheck, 
  UserCheck, 
  HelpCircle,
  ExternalLink,
  ChevronRight
} from "lucide-react";

export function AppSidebar() {
  const pathname = usePathname();

  const menuSections = [
    {
      title: "NGHIỆP VỤ ĐÁNH GIÁ (03-HD/TVĐU)",
      items: [
        {
          title: "Bảng điều hành Tổng hợp",
          href: "/",
          icon: LayoutDashboard,
          badge: null
        },
        {
          title: "Trung tâm Biểu mẫu (M01-M16)",
          href: "/bieu-mau",
          icon: FileSpreadsheet,
          badge: "16 Mẫu"
        },
        {
          title: "Đăng ký Giao việc (Mẫu 01)",
          href: "/mau-01",
          icon: FileEdit,
          badge: "Quý III"
        },
        {
          title: "Tự chấm điểm (Mẫu 02)",
          href: "/mau-02",
          icon: UserCheck,
          badge: "Tối đa 100đ"
        },
        {
          title: "Bỏ phiếu kín Chi bộ (Mẫu 11)",
          href: "/bo-phieu",
          icon: Vote,
          badge: "Mẫu 11-13"
        },
        {
          title: "Thẩm định & Phê duyệt",
          href: "/tham-dinh",
          icon: CheckCheck,
          badge: "Trần 20%"
        }
      ]
    },
    {
      title: "HỆ THỐNG & QUẢN TRỊ 2 VAI",
      items: [
        {
          title: "Quản trị Nhân sự & Phân quyền",
          href: "/quan-tri",
          icon: ShieldCheck,
          badge: "68 Cán bộ"
        },
        {
          title: "Cấu hình Thang điểm & Trần",
          href: "/cau-hinh",
          icon: SlidersHorizontal,
          badge: "Động 100%"
        }
      ]
    }
  ];

  return (
    <aside className="w-64 bg-slate-900 text-slate-100 flex flex-col h-screen sticky top-0 border-r border-slate-800 shrink-0 select-none z-30">
      {/* Brand Header */}
      <div className="p-4 border-b border-slate-800/80 bg-slate-950/40">
        <div className="flex items-center gap-3">
          <div className="w-9 h-9 rounded-xl bg-dang-crimson flex items-center justify-center text-dang-gold font-serif font-black text-lg shadow-gold-glow border border-dang-gold/40">
            ★
          </div>
          <div className="min-w-0">
            <h1 className="font-extrabold text-xs tracking-wider text-dang-gold uppercase font-serif">
              ĐẢNG BỘ ATTECH
            </h1>
            <p className="text-[10px] text-slate-400 truncate tracking-tight font-medium">
              Đánh giá Cán bộ Lãnh đạo
            </p>
          </div>
        </div>
      </div>

      {/* Cadre Dual-Role Profile Card */}
      <div className="px-3 py-3 border-b border-slate-800 bg-slate-950/20">
        <div className="p-2.5 rounded-xl bg-gradient-to-br from-slate-800/80 to-slate-900 border border-slate-700/60 shadow-sm">
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-lg bg-dang-crimson/90 border border-dang-gold/30 flex items-center justify-center text-dang-gold font-bold text-xs shrink-0">
              NA
            </div>
            <div className="min-w-0 flex-1">
              <p className="text-xs font-bold text-slate-100 truncate">Nguyễn Văn A</p>
              <p className="text-[10px] text-amber-300 truncate">Bí thư Chi bộ Khối Kỹ thuật</p>
            </div>
          </div>
          <div className="mt-2 pt-2 border-t border-slate-700/50 flex items-center justify-between text-[10px] text-slate-400">
            <span>Chính quyền:</span>
            <span className="text-slate-200 font-medium truncate max-w-[130px]">Trưởng phòng KH-KD</span>
          </div>
        </div>
      </div>

      {/* Navigation Sections */}
      <div className="flex-1 overflow-y-auto px-3 py-3 space-y-5">
        {menuSections.map((sec, idx) => (
          <div key={idx} className="space-y-1">
            <h3 className="px-3 text-[10px] font-bold tracking-wider text-slate-400 uppercase font-sans">
              {sec.title}
            </h3>
            <div className="space-y-0.5">
              {sec.items.map((item) => {
                const Icon = item.icon;
                const isActive = pathname === item.href;
                return (
                  <Link
                    key={item.href}
                    href={item.href}
                    className={`flex items-center justify-between px-3 py-2 rounded-lg text-xs font-medium transition-all group ${
                      isActive
                        ? "bg-dang-crimson text-white shadow-md font-semibold"
                        : "text-slate-300 hover:bg-slate-800/70 hover:text-white"
                    }`}
                  >
                    <div className="flex items-center gap-2.5 min-w-0">
                      <Icon className={`w-4 h-4 shrink-0 transition-colors ${
                        isActive ? "text-dang-gold" : "text-slate-400 group-hover:text-slate-200"
                      }`} />
                      <span className="truncate">{item.title}</span>
                    </div>

                    {item.badge && (
                      <span className={`text-[10px] px-1.5 py-0.5 rounded font-bold uppercase tracking-tight shrink-0 ${
                        isActive
                          ? "bg-black/30 text-dang-gold border border-dang-gold/30"
                          : "bg-slate-800 text-slate-400 group-hover:text-slate-300"
                      }`}>
                        {item.badge}
                      </span>
                    )}
                  </Link>
                );
              })}
            </div>
          </div>
        ))}
      </div>

      {/* Footer Info */}
      <div className="p-3 border-t border-slate-800 bg-slate-950/40 text-[10px] text-slate-400">
        <div className="flex items-center justify-between">
          <span className="text-slate-400 font-mono">LAN On-Premise</span>
          <span className="text-dang-gold font-bold">03-HD/TVĐU</span>
        </div>
      </div>
    </aside>
  );
}

export default AppSidebar;
