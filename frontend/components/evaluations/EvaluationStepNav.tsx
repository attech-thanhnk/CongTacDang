"use client";

import React from "react";
import { EvaluationRecordDto } from "@/services/evaluationService";

interface EvaluationStepNavProps {
  activeStep: number;
  onSelectStep: (step: number) => void;
  myRecord: EvaluationRecordDto | null;
  hasPermission: (permission: string) => boolean;
  branchRecordsCount: number;
  allRecordsCount: number;
}

interface StepItem {
  id: number;
  num: string;
  name: string;
  role: string;
  count: string | null;
  allowed: boolean;
  isCompleted: boolean;
}

export function EvaluationStepNav({
  activeStep,
  onSelectStep,
  myRecord,
  hasPermission,
  branchRecordsCount,
  allRecordsCount,
}: EvaluationStepNavProps) {
  const steps: StepItem[] = [
    {
      id: 1,
      num: "01",
      name: "Đăng ký công việc",
      role: "Cá nhân",
      count: myRecord?.tasks?.length ? `${myRecord.tasks.length} việc` : null,
      allowed: hasPermission("evaluation.self"),
      isCompleted: (myRecord?.tasks?.length || 0) > 0,
    },
    {
      id: 2,
      num: "02",
      name: "Tự chấm điểm",
      role: "Cá nhân",
      count: myRecord?.totalSelfScore ? `${myRecord.totalSelfScore}đ` : null,
      allowed: hasPermission("evaluation.self"),
      isCompleted: (myRecord?.totalSelfScore || 0) > 0,
    },
    {
      id: 3,
      num: "03",
      name: "Chi bộ đánh giá",
      role: "Chi bộ",
      count: branchRecordsCount > 0 ? `${branchRecordsCount} cán bộ` : null,
      allowed: hasPermission("evaluation.cell.confirm"),
      isCompleted: branchRecordsCount > 0,
    },
    {
      id: 4,
      num: "04",
      name: "Thẩm định hồ sơ",
      role: "Tổ Thẩm định",
      count: allRecordsCount > 0 ? `${allRecordsCount} hồ sơ` : null,
      allowed: hasPermission("evaluation.appraise"),
      isCompleted: allRecordsCount > 0,
    },
    {
      id: 5,
      num: "05",
      name: "Phê duyệt xếp loại",
      role: "Ban Thường vụ",
      count: null,
      allowed: hasPermission("evaluation.decide") || hasPermission("evaluation.decide.external"),
      isCompleted: false,
    },
  ];

  const visibleSteps = steps.filter((step) => step.allowed);

  return (
    <nav
      aria-label="Quy trình đánh giá cán bộ"
      style={{
        backgroundColor: "#ffffff",
        borderBottom: "1px solid #e2e8f0",
        padding: "10px calc(var(--page-px, 24px) - 6px)",
      }}
    >
      <div className="d-flex align-items-stretch overflow-x-auto no-scrollbar gap-1 py-0.5">
        {visibleSteps.map((step, idx) => {
          const isActive = activeStep === step.id;
          const isCompleted = step.isCompleted;
          const isFirst = idx === 0;
          const isLast = idx === visibleSteps.length - 1;

          // Tạo hình khối mũi tên Chevron chính xác 100% bằng clip-path
          const clipPath = isFirst
            ? "polygon(0% 0%, calc(100% - 14px) 0%, 100% 50%, calc(100% - 14px) 100%, 0% 100%)"
            : isLast
            ? "polygon(0% 0%, 100% 0%, 100% 100%, 0% 100%, 14px 50%)"
            : "polygon(0% 0%, calc(100% - 14px) 0%, 100% 50%, calc(100% - 14px) 100%, 0% 100%, 14px 50%)";

          return (
            <button
              key={step.id}
              type="button"
              onClick={() => onSelectStep(step.id)}
              className="flex-fill border-0 text-start transition-all text-nowrap d-flex align-items-center"
              style={{
                clipPath,
                backgroundColor: isActive
                  ? "#1d4ed8"
                  : isCompleted
                  ? "#f1f5f9"
                  : "#f8fafc",
                color: isActive ? "#ffffff" : "#1e293b",
                paddingTop: "9px",
                paddingBottom: "9px",
                paddingLeft: isFirst ? "14px" : "24px",
                paddingRight: isLast ? "14px" : "24px",
                cursor: "pointer",
                outline: "none",
                minWidth: "160px",
                transition: "all 0.15s ease",
              }}
              title={`Bước ${step.id}: ${step.name} (${step.role})`}
            >
              {/* Số thứ tự tròn thanh lịch, không dùng ô vuông thô ráp */}
              <span
                style={{
                  width: "22px",
                  height: "22px",
                  borderRadius: "50%",
                  display: "inline-flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontSize: "11px",
                  fontWeight: 700,
                  flexShrink: 0,
                  backgroundColor: isActive
                    ? "#ffffff"
                    : isCompleted
                    ? "#cbd5e1"
                    : "#e2e8f0",
                  color: isActive
                    ? "#1d4ed8"
                    : isCompleted
                    ? "#1e293b"
                    : "#94a3b8",
                  marginRight: "8px",
                }}
              >
                {step.id}
              </span>

              {/* Tên bước & Phụ đề */}
              <div className="d-flex flex-column min-w-0" style={{ lineHeight: 1.2 }}>
                <span
                  className="text-truncate"
                  style={{
                    fontSize: "12.5px",
                    fontWeight: isActive ? 700 : 600,
                    color: isActive ? "#ffffff" : isCompleted ? "#0f172a" : "#64748b",
                  }}
                >
                  {step.name}
                </span>
                <span
                  className="text-truncate"
                  style={{
                    fontSize: "10.5px",
                    fontWeight: 500,
                    color: isActive ? "#bfdbfe" : "#94a3b8",
                    marginTop: "1px",
                  }}
                >
                  {step.role} {step.count ? `• ${step.count}` : ""}
                </span>
              </div>
            </button>
          );
        })}
      </div>
    </nav>
  );
}

export default EvaluationStepNav;



