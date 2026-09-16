"use client";

import React from "react";
import Link from "next/link";
import { CheckCircle2, Clock, ChevronRight, FileText } from "lucide-react";

interface Step {
  stepNum: number;
  code: string;
  title: string;
  subTitle: string;
  forms: string;
  status: "completed" | "in_progress" | "upcoming";
  url: string;
  dueDate: string;
}

export default function ProcessTimeline() {
  const steps: Step[] = [
    {
      stepNum: 1,
      code: "BƯỚC 1",
      title: "Đăng Ký & Giao Việc",
      subTitle: "Cán bộ đăng ký 3-7 việc, chốt định mức đúng 70đ việc",
      forms: "Mẫu 01, 04, 05",
      status: "completed",
      url: "/mau-01",
      dueDate: "Trước 15/07/2026"
    },
    {
      stepNum: 2,
      code: "BƯỚC 2",
      title: "Tự Chấm Điểm & Báo Cáo",
      subTitle: "Chấm 30đ chung + tự nhân tỷ trọng % nhiệm vụ chuyên môn",
      forms: "Mẫu 02, 09A-D",
      status: "completed",
      url: "/mau-02",
      dueDate: "Trước 20/09/2026"
    },
    {
      stepNum: 3,
      code: "BƯỚC 3",
      title: "Hội Nghị Chi Bộ & Bỏ Phiếu",
      subTitle: "Bỏ phiếu kín nặc danh, tự động lập Biên bản kiểm phiếu",
      forms: "Mẫu 11, 12, 13",
      status: "in_progress",
      url: "/bo-phieu",
      dueDate: "Trước 25/09/2026"
    },
    {
      stepNum: 4,
      code: "BƯỚC 4",
      title: "Tổ Thẩm Định Đối Soát",
      subTitle: "Lập Mẫu 10 khi vênh >= 5đ; kiểm soát chỉ tiêu trần 20%",
      forms: "Mẫu 03, Mẫu 10",
      status: "upcoming",
      url: "/tham-dinh",
      dueDate: "Trước 28/09/2026"
    },
    {
      stepNum: 5,
      code: "BƯỚC 5",
      title: "BTV Đảng Ủy Phê Duyệt",
      subTitle: "Ban Thường vụ chuẩn y kết quả, ký báo cáo gửi VATM",
      forms: "Mẫu 14, 15, 16",
      status: "upcoming",
      url: "/bieu-mau",
      dueDate: "Trước 30/09/2026"
    }
  ];

  return (
    <div className="bg-white rounded-2xl shadow-sm border border-slate-200/80 p-6 space-y-4">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2 border-b border-slate-100 pb-3">
        <div>
          <h3 className="font-bold text-base text-slate-900 tracking-tight">
            Quy Trình 5 Bước Đánh Giá Cán Bộ Định Kỳ Quý III/2026
          </h3>
          <p className="text-xs text-slate-500">
            Chuẩn hóa tuần tự theo Hướng dẫn số 03-HD/TVĐU của Ban Thường vụ Đảng ủy Tổng công ty
          </p>
        </div>
        <span className="text-[11px] font-bold px-2.5 py-1 rounded-full bg-amber-50 text-amber-800 border border-amber-200 w-fit">
          Đang ở Bước 3 (Bỏ phiếu kín)
        </span>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-5 gap-3">
        {steps.map((step) => {
          const isDone = step.status === "completed";
          const isCurr = step.status === "in_progress";

          return (
            <Link
              key={step.stepNum}
              href={step.url}
              className={`p-4 rounded-xl border transition-all flex flex-col justify-between group ${
                isCurr
                  ? "bg-amber-50/60 border-amber-300 ring-2 ring-amber-400/30 shadow-sm"
                  : isDone
                  ? "bg-slate-50/60 border-slate-200 hover:border-slate-300"
                  : "bg-white border-slate-200/60 opacity-70 hover:opacity-100"
              }`}
            >
              <div>
                <div className="flex items-center justify-between mb-2">
                  <span className={`text-[10px] font-mono font-bold px-1.5 py-0.5 rounded ${
                    isCurr ? "bg-amber-200 text-amber-900" : isDone ? "bg-emerald-100 text-emerald-800" : "bg-slate-100 text-slate-500"
                  }`}>
                    {step.code}
                  </span>
                  {isDone ? (
                    <CheckCircle2 className="w-4 h-4 text-emerald-600" />
                  ) : isCurr ? (
                    <Clock className="w-4 h-4 text-amber-600 animate-spin" />
                  ) : null}
                </div>

                <h4 className="font-bold text-xs text-slate-900 group-hover:text-dang-crimson transition-colors">
                  {step.title}
                </h4>
                <p className="text-[11px] text-slate-500 mt-1 leading-snug">
                  {step.subTitle}
                </p>
              </div>

              <div className="mt-4 pt-2 border-t border-slate-200/60 flex items-center justify-between text-[10px]">
                <span className="font-semibold text-dang-crimson">{step.forms}</span>
                <span className="text-slate-400">{step.dueDate}</span>
              </div>
            </Link>
          );
        })}
      </div>
    </div>
  );
}
