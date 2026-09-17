import "bootstrap/dist/css/bootstrap.min.css";
import "bootstrap-icons/font/bootstrap-icons.css";
import "./globals.css";
import React from "react";
import { Inter } from "next/font/google";
import { AuthProvider } from "@/contexts/AuthContext";
import { ToastProvider } from "@/contexts/ToastContext";
import { AuthGuard } from "@/components/layout/AuthGuard";

const inter = Inter({
  subsets: ["latin", "vietnamese"],
  weight: ["400", "500", "600", "700"],
  variable: "--font-inter",
  display: "swap",
});

export const metadata = {
  title: "Đảng bộ ATTECH - Đánh giá cán bộ",
  description: "Hệ thống đánh giá, xếp loại cán bộ định kỳ",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="vi" className={`${inter.variable} ${inter.className}`}>
      <head>
        <meta charSet="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
      </head>
      <body className={`${inter.className} min-vh-100 antialiased`} style={{ backgroundColor: "#f8fafc", color: "#0f172a" }}>
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
