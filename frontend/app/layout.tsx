import "./globals.css";
import React from "react";
import { AppSidebar } from "@/components/layout/AppSidebar";
import { AppHeader } from "@/components/layout/AppHeader";

export const metadata = {
  title: "Đảng bộ ATTECH - Quản trị Đánh giá Cán bộ Định kỳ",
  description: "Hệ thống Enterprise GovTech số hóa 5 bước quy trình và 16 biểu mẫu theo Hướng dẫn 03-HD/TVĐU của Đảng ủy VATM",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="vi">
      <head>
        <meta charSet="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
      </head>
      <body className="bg-slate-50 text-slate-900 min-h-screen flex antialiased">
        {/* Sidebar Enterprise Cố định bên trái */}
        <AppSidebar />

        {/* Khu vực Content chính bên phải */}
        <div className="flex-1 flex flex-col min-w-0">
          <AppHeader />
          <main className="flex-1 p-6 max-w-7xl w-full mx-auto">
            {children}
          </main>
          <footer className="py-4 px-6 bg-white border-t text-center text-xs text-slate-400">
            Hệ thống Quản trị Công tác Đảng — Đánh giá Cán bộ Lãnh đạo, Quản lý Công ty TNHH Kỹ thuật Quản lý bay (ATTECH) theo Hướng dẫn 03-HD/TVĐU.
          </footer>
        </div>
      </body>
    </html>
  );
}
