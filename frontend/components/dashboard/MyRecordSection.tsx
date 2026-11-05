"use client";

import React from "react";
import Link from "next/link";
import type { EvaluationRecordDto } from "@/services/evaluationService";
import { fmt } from "@/services/criteriaService";
import { GradeBadge } from "@/components/common/StatusBadge";
import { actorOf, performersText } from "@/components/evaluations/RecordProgress";
import styles from "./dashboard.module.css";
import { RecordTag } from "./RecordTag";
import { ownerCurrentStep, shortDate, taskActionLabel } from "./taskUtils";

const hasGrade = (grade?: string | null) => !!grade && grade !== "ChuaXepLoai";

type Fact = { label: string; value: React.ReactNode; late?: boolean };

/** Thẻ hồ sơ của người dùng trong kỳ đang chọn: trạng thái, bước đang chờ, ai làm, hạn; đã công bố thì kết quả chính thức. */
export function MyRecordSection({ record }: { record: EvaluationRecordDto }) {
  const ownStep = ownerCurrentStep(record);
  const current = record.progress?.find((s) => s.state === "current") ?? null;
  const myTurn = !!ownStep || !!record.returnReason;
  const published = record.status === "Published";
  // Trước công bố chỉ hiện điểm mới nhất của luồng để không trộn các con số.
  const latestScore =
    record.appraisalScore != null
      ? { label: "Điểm thẩm định", value: record.appraisalScore }
      : record.selfScoredAt
        ? { label: "Điểm tự chấm", value: record.totalSelfScore }
        : null;

  const facts: Fact[] = [];
  if (published) {
    if (hasGrade(record.finalGrade)) facts.push({ label: "Mức xếp loại", value: <GradeBadge grade={record.finalGrade} /> });
    if (hasGrade(record.finalGrade) && record.finalScore > 0) facts.push({ label: "Điểm", value: <b className="num">{fmt(record.finalScore)}</b> });
  } else if (current && !record.notRatedReason) {
    facts.push({ label: "Bước đang chờ", value: current.name });
    const who = ownStep ? "Bạn" : performersText(current) || actorOf(current);
    if (who) facts.push({ label: "Người làm", value: who });
    if (current.deadline) facts.push({ label: "Hạn", value: shortDate(current.deadline), late: current.overdue });
    if (latestScore) facts.push({ label: latestScore.label, value: <b className="num">{fmt(latestScore.value)}</b> });
  }

  const actionLabel = myTurn && current ? taskActionLabel({ step: current.step, mode: current.mode, status: record.status }) : "Mở hồ sơ";

  return (
    <section className={`${styles.block} ${styles.mine}`} aria-label="Hồ sơ của tôi">
      <div className={styles.mineMain}>
        <div className={styles.mineHead}>
          <span className={styles.mineTitle}>Hồ sơ của tôi</span>
          {record.notRatedReason && !published ? (
            // Người không đánh giá trong quý cần biết ngay mình Chưa xếp loại, không chỉ thấy "Chờ công bố".
            <RecordTag status={record.status} label="Chưa xếp loại" />
          ) : (
            !(published && hasGrade(record.finalGrade)) && (
              <RecordTag status={record.status} label={record.statusDisplayName} returned={!!record.returnReason} overdue={ownStep?.overdue} />
            )
          )}
        </div>
        {facts.length > 0 && (
          <dl className={styles.mineFacts}>
            {facts.map((f) => (
              <div key={f.label}>
                <dt>{f.label}</dt>
                <dd className={f.late ? styles.late : undefined}>{f.value}</dd>
              </div>
            ))}
          </dl>
        )}
        {record.notRatedReason && !published && <span className={styles.mineReason}>{record.notRatedReason}</span>}
        {record.returnReason && <span className={styles.mineReason}>Bị trả lại: {record.returnReason}</span>}
      </div>
      <Link href={`/evaluations/${record.id}`} className={`btn btn-sm ${myTurn ? "btn-primary" : "btn-outline-secondary"}`}>
        {actionLabel}
      </Link>
    </section>
  );
}

export default MyRecordSection;
