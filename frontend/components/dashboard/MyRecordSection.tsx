"use client";

import React from "react";
import Link from "next/link";
import type { EvaluationRecordDto } from "@/services/evaluationService";
import { fmt } from "@/services/criteriaService";
import { GradeBadge } from "@/components/common/StatusBadge";
import styles from "./dashboard.module.css";
import { RecordTag } from "./RecordTag";
import { ownerCurrentStep, shortDate } from "./taskUtils";

const hasGrade = (grade?: string | null) => !!grade && grade !== "ChuaXepLoai";

/** Hồ sơ của người dùng trong kỳ đang chọn, một dòng: đã công bố thì chỉ kết quả chính thức, chưa thì thông tin bước hiện tại. */
export function MyRecordSection({ record }: { record: EvaluationRecordDto }) {
  const ownStep = ownerCurrentStep(record);
  const myTurn = !!ownStep || !!record.returnReason;
  const published = record.status === "Published";
  // Trước công bố chỉ hiện điểm mới nhất của luồng để không trộn các con số.
  const latestScore =
    record.appraisalScore != null
      ? { label: "Thẩm định", value: record.appraisalScore }
      : record.selfScoredAt
        ? { label: "Tự chấm", value: record.totalSelfScore }
        : null;

  return (
    <section className={`${styles.block} ${styles.mine}`} aria-label="Hồ sơ của tôi">
      <div className={styles.mineMain}>
        <span className={styles.mineTitle}>Hồ sơ của tôi</span>
        {published ? (
          <>
            {hasGrade(record.finalGrade) ? <GradeBadge grade={record.finalGrade} /> : <RecordTag status={record.status} label={record.statusDisplayName} />}
            {hasGrade(record.finalGrade) && record.finalScore > 0 && (
              <span className={styles.muted}>
                Điểm <b className={`${styles.strong} num`}>{fmt(record.finalScore)}</b>
              </span>
            )}
          </>
        ) : (
          <>
            <RecordTag status={record.status} label={record.statusDisplayName} returned={!!record.returnReason} overdue={ownStep?.overdue} />
            {ownStep?.deadline && (
              <span className={ownStep.overdue ? styles.late : styles.muted}>Hạn {shortDate(ownStep.deadline)}</span>
            )}
            {latestScore && (
              <span className={styles.muted}>
                {latestScore.label} <b className={`${styles.strong} num`}>{fmt(latestScore.value)}</b>
              </span>
            )}
            {record.returnReason && <span className={styles.mineReason}>{record.returnReason}</span>}
          </>
        )}
      </div>
      <Link href={`/evaluations/${record.id}`} className={`btn btn-sm ${myTurn ? "btn-primary" : "btn-outline-secondary"}`}>
        Mở hồ sơ
      </Link>
    </section>
  );
}

export default MyRecordSection;
