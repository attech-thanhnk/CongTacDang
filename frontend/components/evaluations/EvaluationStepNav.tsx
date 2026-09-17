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

export function EvaluationStepNav({
  activeStep,
  onSelectStep,
  myRecord,
  hasPermission,
  branchRecordsCount,
  allRecordsCount,
}: EvaluationStepNavProps) {
  const steps = [
    {
      id: 1,
      name: "Đăng ký nhiệm vụ",
      code: "M01",
      count: myRecord?.tasks?.length ? `${myRecord.tasks.length}` : null,
      allowed: hasPermission("evaluations.register"),
      isCompleted: (myRecord?.tasks?.length || 0) > 0,
    },
    {
      id: 2,
      name: "Tự chấm điểm",
      code: "M02",
      count: myRecord?.totalSelfScore ? `${myRecord.totalSelfScore}đ` : null,
      allowed: hasPermission("evaluations.self_score"),
      isCompleted: (myRecord?.totalSelfScore || 0) > 0,
    },
    {
      id: 3,
      name: "Chi bộ đánh giá",
      code: "M10",
      count: branchRecordsCount > 0 ? `${branchRecordsCount}` : null,
      allowed: hasPermission("evaluations.branch_vote") || hasPermission("evaluations.branch_review"),
      isCompleted: branchRecordsCount > 0,
    },
    {
      id: 4,
      name: "Thẩm định",
      code: "M03",
      count: allRecordsCount > 0 ? `${allRecordsCount}` : null,
      allowed: hasPermission("evaluations.appraise"),
      isCompleted: allRecordsCount > 0,
    },
    {
      id: 5,
      name: "Chuẩn y",
      code: "M07",
      count: null,
      allowed: hasPermission("evaluations.approve"),
      isCompleted: false,
    },
  ];

  const visibleSteps = steps.filter((step) => step.allowed);

  return (
    <div className="step-nav">
      {visibleSteps.map((step) => {
        const isActive = activeStep === step.id;
        const isDone = step.isCompleted && !isActive;

        return (
          <button
            key={step.id}
            type="button"
            onClick={() => onSelectStep(step.id)}
            className={`step-nav-item ${isActive ? "active" : isDone ? "done" : ""}`}
            style={{ border: "none", background: "transparent", cursor: "pointer" }}
          >
            {/* Step number circle */}
            <span className="step-nav-num">
              {isDone ? (
                <i className="bi bi-check-lg" style={{ fontSize: "12px" }} />
              ) : (
                step.id
              )}
            </span>

            <span>{step.name}</span>

            {/* Code badge */}
            <span
              style={{
                fontFamily: "var(--font-mono)",
                fontSize: "12px",
                fontWeight: 600,
                padding: "2px 6px",
                borderRadius: "4px",
                background: isActive
                  ? "var(--color-primary-light)"
                  : isDone
                  ? "var(--color-success-bg)"
                  : "var(--bg-app)",
                color: isActive
                  ? "var(--color-cobalt)"
                  : isDone
                  ? "var(--color-success)"
                  : "var(--text-secondary)",
                border: `1px solid ${
                  isActive
                    ? "var(--color-primary-border)"
                    : isDone
                    ? "var(--color-success-border)"
                    : "var(--border-base)"
                }`,
              }}
            >
              {step.code}
            </span>

            {/* Count */}
            {step.count && (
              <span
                style={{
                  fontSize: "12px",
                  padding: "2px 6px",
                  borderRadius: "4px",
                  background: "var(--bg-app)",
                  color: "var(--text-secondary)",
                  border: "1px solid var(--border-base)",
                  fontWeight: 600,
                }}
              >
                {step.count}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
}

export default EvaluationStepNav;
