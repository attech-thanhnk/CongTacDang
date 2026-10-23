"use client";

import React from "react";
import Link from "next/link";
import type { EvaluationRecordDto } from "@/services/evaluationService";
import { fmt } from "@/services/criteriaService";
import { GradeBadge } from "@/components/common/StatusBadge";
import styles from "./dashboard.module.css";
import { RecordTag } from "./RecordTag";
import { ownerCurrentStep, shortDate } from "./taskUtils";

/** Hồ sơ của người dùng trong kỳ đang chọn, gọn một dòng: trạng thái, hạn (khi đến lượt mình), điểm, mức. */
export function MyRecordSection({ record }: { record: EvaluationRecordDto }) {
  const ownStep = ownerCurrentStep(record);
  const myTurn = !!ownStep || !!record.returnReason;
  const published = record.status === "Published";

  return (
    <section className={`${styles.block} ${styles.mine}`} aria-label="Hồ sơ của tôi">
      <div className={styles.mineMain}>
        <span className={styles.mineTitle}>Hồ sơ của tôi</span>
        <RecordTag status={record.status} label={record.statusDisplayName} returned={!!record.returnReason} overdue={ownStep?.overdue} />
        {ownStep?.deadline && (
          <span className={ownStep.overdue ? styles.late : styles.muted}>Hạn {shortDate(ownStep.deadline)}</span>
        )}
        {record.selfScoredAt && (
          <span className={styles.muted}>
            Tự chấm <b className={`${styles.strong} num`}>{fmt(record.totalSelfScore)}</b>
          </span>
        )}
        {record.appraisalScore != null && (
          <span className={styles.muted}>
            Thẩm định <b className={`${styles.strong} num`}>{fmt(record.appraisalScore)}</b>
          </span>
        )}
        {published && <GradeBadge grade={record.finalGrade} />}
        {record.returnReason && <span className={styles.mineReason}>{record.returnReason}</span>}
      </div>
      <Link href={`/evaluations/${record.id}`} className={`btn btn-sm ${myTurn ? "btn-primary" : "btn-outline-secondary"}`}>
        Mở hồ sơ
      </Link>
    </section>
  );
}

export default MyRecordSection;
