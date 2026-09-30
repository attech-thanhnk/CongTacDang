"use client";

import React from "react";
import Link from "next/link";
import type { EvaluationRecordDto } from "@/services/evaluationService";
import { GradeBadge } from "@/components/common/StatusBadge";
import styles from "./dashboard.module.css";
import { RecordTag } from "./RecordTag";
import { shortDate } from "./taskUtils";

function formatScore(value?: number | null): string {
  return value || value === 0 ? String(value) : "—";
}

/** Hồ sơ của người dùng trong kỳ đang chọn: trạng thái, hạn bước hiện tại, điểm, mức. */
export function MyRecordSection({ record }: { record: EvaluationRecordDto }) {
  const current = record.progress.find((s) => s.state === "current");
  const published = record.status === "Published";

  return (
    <section className={styles.block}>
      <div className={styles.blockH}>
        <h2>Hồ sơ của tôi</h2>
        <Link href={`/evaluations/${record.id}`} className={styles.link}>
          Mở hồ sơ
        </Link>
      </div>
      <dl className={styles.dl}>
        <dt>Trạng thái</dt>
        <dd>
          <RecordTag status={record.status} label={record.statusDisplayName} returned={!!record.returnReason} overdue={current?.overdue} />
        </dd>
        {record.returnReason && (
          <>
            <dt>Lý do trả lại</dt>
            <dd className={styles.late}>{record.returnReason}</dd>
          </>
        )}
        {current?.deadline && (
          <>
            <dt>Hạn</dt>
            <dd className={current.overdue ? styles.late : undefined}>{shortDate(current.deadline)}</dd>
          </>
        )}
        <dt>Tự chấm</dt>
        <dd>{formatScore(record.totalSelfScore || null)}</dd>
        <dt>Thẩm định</dt>
        <dd>{formatScore(record.appraisalScore)}</dd>
        {published && (
          <>
            <dt>Mức xếp loại</dt>
            <dd>
              <GradeBadge grade={record.finalGrade} />
            </dd>
          </>
        )}
      </dl>
    </section>
  );
}

export default MyRecordSection;
