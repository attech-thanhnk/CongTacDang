"use client";

import React from "react";
import Link from "next/link";
import type { EvaluationRecordDto, RecordStepProgressDto } from "@/services/evaluationService";
import { formatDate } from "@/lib/dateFormat";
import { GradeBadge, RecordStatusBadge } from "@/components/common/StatusBadge";
import styles from "./dashboard.module.css";

function stepClass(step: RecordStepProgressDto): string {
  switch (step.state) {
    case "done":
      return styles.stepDone;
    case "current":
      return step.overdue ? styles.stepCurrentOverdue : styles.stepCurrent;
    case "skipped":
      return styles.stepSkipped;
    default:
      return styles.step;
  }
}

function formatScore(value?: number | null): string {
  return value || value === 0 ? String(value) : "—";
}

/** Hồ sơ của người dùng trong kỳ: trạng thái, tiến trình các bước, điểm, mức. */
export function MyRecordSection({ record }: { record: EvaluationRecordDto }) {
  const current = record.progress.find((s) => s.state === "current");
  const steps = record.progress;

  return (
    <section className={styles.section}>
      <div className={styles.sectionHeader}>
        <h2 className={styles.sectionTitle}>
          Hồ sơ của tôi
          <RecordStatusBadge
            status={record.status}
            label={record.statusDisplayName}
            returned={!!record.returnReason}
            overdue={current?.overdue}
          />
        </h2>
        <Link href={`/evaluations/${record.id}`} className="btn btn-primary btn-sm">
          Mở hồ sơ
        </Link>
      </div>

      <div className={styles.sectionBody}>
        {record.returnReason && (
          <div className="alert alert-danger d-flex gap-2 align-items-start py-2 mb-3">
            <i className="bi bi-arrow-return-left" />
            <div>
              <strong>Bị trả lại:</strong> {record.returnReason}
            </div>
          </div>
        )}

        <ol className={styles.steps}>
          {steps.map((step, index) => (
            <li key={step.step} className={stepClass(step)} title={step.name}>
              <span className={styles.stepNo}>
                {step.state === "done" && <i className="bi bi-check-lg me-1" />}
                Bước {index + 1}
              </span>
              <span className={styles.stepName}>{step.name}</span>
              {step.state === "current" && step.deadline && <span>Hạn {formatDate(step.deadline)}</span>}
            </li>
          ))}
        </ol>

        <div className={styles.facts}>
          <div className={styles.fact}>
            <span className={styles.factLabel}>Tự chấm</span>
            <span className={styles.factValue}>{formatScore(record.totalSelfScore || null)}</span>
          </div>
          <div className={styles.fact}>
            <span className={styles.factLabel}>Thẩm định</span>
            <span className={styles.factValue}>{formatScore(record.appraisalScore)}</span>
          </div>
          <div className={styles.fact}>
            <span className={styles.factLabel}>Mức xếp loại</span>
            <span className="pt-1">
              <GradeBadge grade={record.status === "Published" ? record.finalGrade : null} />
            </span>
          </div>
        </div>
      </div>
    </section>
  );
}

export default MyRecordSection;
