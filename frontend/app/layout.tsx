import "bootstrap/dist/css/bootstrap.min.css";
import "bootstrap-icons/font/bootstrap-icons.css";
import "./globals.css";
import React from "react";
import { Plus_Jakarta_Sans } from "next/font/google";
import { AuthProvider } from "@/contexts/AuthContext";
import { ToastProvider } from "@/contexts/ToastContext";
import { AuthGuard } from "@/components/layout/AuthGuard";

// Font tải lúc build, phục vụ cùng ứng dụng: chạy trong LAN không cần Internet.
const jakarta = Plus_Jakarta_Sans({
  subsets: ["latin", "vietnamese"],
  weight: ["400", "500", "600", "700"],
  variable: "--font-sans",
  display: "swap",
});

export const metadata = {
  // Tên đơn vị lấy từ cài đặt (Quản trị → Thông tin đơn vị) và hiển thị trong trang; tiêu đề tĩnh không ghi tên riêng.
  title: "Đánh giá cán bộ",
  description: "Hệ thống đánh giá, xếp loại cán bộ định kỳ",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="vi" className={jakarta.variable}>
      <head>
        <meta charSet="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
      </head>
      <body className="min-vh-100 antialiased">
        <AuthProvider>
          <ToastProvider>
            <AuthGuard>
              {children}
            </AuthGuard>
          </ToastProvider>
        </AuthProvider>
      </body>
    </html>
  );
}
