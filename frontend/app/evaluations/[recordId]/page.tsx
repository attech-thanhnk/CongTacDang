"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { GradeBadge } from "@/components/common/StatusBadge";
import { useToast } from "@/contexts/ToastContext";
import { RecordProgress, actorOf, formatShortDateTime } from "@/components/evaluations/RecordProgress";
import { RecordActionPanel } from "@/components/evaluations/RecordActionPanel";
import { ReasonDialog } from "@/components/evaluations/ReasonDialog";
import { AppealPanel } from "@/components/evaluations/AppealPanel";
import { ImprovementPlanPanel } from "@/components/evaluations/ImprovementPlanPanel";
import { EvaluationPdfModal } from "@/components/evaluations/EvaluationPdfModal";
import { RecordFormsPanel } from "@/components/evaluations/RecordFormsPanel";
import { RecordHistory } from "@/components/evaluations/RecordHistory";
import { CollapsibleSection, revealSection } from "@/components/evaluations/CollapsibleSection";
import { AxisSummaryView, SelfAssessmentView } from "@/components/evaluations/IndividualFormInputs";
import { loadCriteria, useCriteria } from "@/components/evaluations/useCriteria";
import { initialsOf } from "@/components/dashboard/taskUtils";
import styles from "@/components/evaluations/record.module.css";
import { attachmentService } from "@/services/attachmentService";
import { describeFrame, fmt } from "@/services/criteriaService";
import {
  EVALUATION_CONFLICT_EVENT,
  EvaluationConflictDetail,
  EvaluationRecordDto,
  EvaluationRecordHistoryDto,
  RecordActionDto,
  RecordFormDto,
  RecordStepProgressDto,
  WorkflowActionCode,
  WorkflowStepCode,
  STEP_NAMES,
  TASK_KIND_LABELS,
  TASK_ROLE_LABELS,
  criteriaAppliesForm,
  evaluationService,
  isConcurrencyConflict,
  selfAssessmentSections,
  buildAxisTaskBlocks,
} from "@/services/evaluationService";
import { formatDate, formatDateTime } from "@/lib/dateFormat";

/**
 * Trang hồ sơ đánh giá: đầu trang (cán bộ), tiến trình; cột trái là khu thao tác (theo `actions`) rồi dữ liệu từng bước
 * (thu gọn được), kiến nghị, kế hoạch Mẫu 17; cột phải là biểu mẫu, lịch sử, thông tin hồ sơ.
 */
export default function EvaluationRecordPage() {
  const params = useParams<{ recordId: string }>();
  const recordId = params?.recordId;
  const { toast } = useToast();
  const [record, setRecord] = useState<EvaluationRecordDto | null>(null);
  const [actions, setActions] = useState<RecordActionDto[]>([]);
  const [history, setHistory] = useState<EvaluationRecordHistoryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [conflict, setConflict] = useState<string | null>(null);
  const [reasonAction, setReasonAction] = useState<RecordActionDto | null>(null);
  const [pdf, setPdf] = useState<string | null>(null);
  const [forms, setForms] = useState<RecordFormDto[]>([]);

  const load = useCallback(async () => {
    if (!recordId) return;
    setLoadError(null);
    try {
      const [loadedRecord, loadedActions, loadedHistory, loadedForms] = await Promise.all([
        evaluationService.getRecordById(recordId),
        evaluationService.getRecordActions(recordId),
        evaluationService.getRecordHistory(recordId),
        evaluationService.getRecordForms(recordId).catch(() => [] as RecordFormDto[]),
      ]);
      // Chờ bộ tiêu chí của kỳ (thường đã có trong bộ nhớ) để trang hiện đủ một lần, không dựng lại bố cục khi bộ tiêu chí về sau.
      await loadCriteria(loadedRecord.periodId);
      setRecord(loadedRecord);
      setActions(loadedActions.actions);
      setHistory(loadedHistory);
      setForms(loadedForms);
      setConflict(null);
    } catch (error: any) {
      setLoadError(error?.message || "Không tải được hồ sơ.");
    } finally {
      setLoading(false);
    }
  }, [recordId]);

  useEffect(() => {
    load();
  }, [load]);

  // T-24: người khác đã cập nhật / hồ sơ đã sang bước khác → thông báo và đề nghị tải lại.
  useEffect(() => {
    const handler = (event: Event) => {
      const detail = (event as CustomEvent<EvaluationConflictDetail>).detail;
      setConflict(detail?.message || "Dữ liệu đã thay đổi. Hãy tải lại.");
    };
    window.addEventListener(EVALUATION_CONFLICT_EVENT, handler);
    return () => window.removeEventListener(EVALUATION_CONFLICT_EVENT, handler);
  }, []);

  const perform = async (action: WorkflowActionCode, payload: Record<string, unknown>, step?: WorkflowStepCode) => {
    if (!record) return;
    setBusy(true);
    try {
      await evaluationService.performAction(record.id, action, record.version ?? 0, payload, step);
      toast.success("Đã lưu thao tác trên hồ sơ.");
      setReasonAction(null);
      await load();
    } catch (error: any) {
      if (!isConcurrencyConflict(error)) toast.error(error?.message || "Không thực hiện được thao tác.");
    } finally {
      setBusy(false);
    }
  };

  // Bước đang làm mà người xem có thao tác → tới khu thao tác; còn lại → mở và cuộn tới khối nội dung của bước.
  const selectStep = (item: RecordStepProgressDto) => {
    if (item.state === "current" && actions.some((a) => a.step === item.step)) {
      goToActions();
      return;
    }
    if (!revealSection(STEP_SECTION[item.step]) && item.mode === "External") revealSection(EXTERNAL_SECTION);
  };

  if (loading) {
    return <div className="page-wrapper"><div className="text-secondary p-4 loading-delay"><span className="spinner-border spinner-border-sm me-2" />Đang tải hồ sơ...</div></div>;
  }

  if (loadError || !record) {
    return (
      <div className="page-wrapper">
        <div className="alert alert-warning m-4">
          {loadError || "Không tìm thấy hồ sơ."} <Link href="/work-queue">Về danh sách việc cần xử lý</Link>
        </div>
      </div>
    );
  }

  return (
    <div className="page-wrapper">
      <div className="page-body">
        <div className={styles.head}>
          <div className={`initials ${styles.bigInitials}`}>{initialsOf(record.fullName)}</div>
          <div className={styles.headMain}>
            <h1>{record.fullName}</h1>
            <p className={styles.lede}>{[record.positionTitle, record.partyCellName].filter(Boolean).join(" · ") || record.departmentName}</p>
            <RecordStatusLine record={record} actions={actions} onGo={goToActions} />
          </div>
        </div>

        {conflict && (
          <div className="alert alert-warning d-flex justify-content-between align-items-center mb-0" role="alert">
            <span>{conflict}</span>
            <button type="button" className="btn btn-warning btn-sm" onClick={() => load()}>Tải lại hồ sơ</button>
          </div>
        )}
        {record.returnReason && (
          <div className="alert alert-danger mb-0">
            <strong>Bị trả lại:</strong> {record.returnReason}
          </div>
        )}

        <section className={styles.block} aria-label="Tiến trình hồ sơ">
          <RecordProgress progress={record.progress} onSelect={selectStep} />
        </section>

        <div className={styles.cols}>
          <div className={styles.stack}>
            <div id={ACTIONS_ANCHOR} className={styles.actionsAnchor} tabIndex={-1}>
              <RecordActionPanel record={record} actions={actions} busy={busy} onSubmit={perform} onReason={setReasonAction} />
            </div>
            <RecordData record={record} />
            <AppealPanel record={record} onRecordChanged={load} />
            <ImprovementPlanPanel record={record} />
          </div>
          <div className={styles.stack}>
            <RecordFormsPanel
              recordId={record.id}
              fullName={record.fullName}
              forms={forms}
              onPreview={forms.length > 0 ? () => setPdf(forms[0].code) : undefined}
            />
            <RecordHistory history={history} />
            <RecordInfo record={record} />
          </div>
        </div>
      </div>

      <ReasonDialog
        isOpen={!!reasonAction}
        title={reasonAction?.label || ""}
        description={
          reasonAction?.action === "Reopen"
            ? "Mở lại hồ sơ đã công bố để đính chính."
            : "Hồ sơ sẽ quay về chủ hồ sơ để sửa. Lý do được hiển thị cho chủ hồ sơ."
        }
        confirmText={reasonAction?.action === "Reopen" ? "Mở lại hồ sơ" : "Trả lại"}
        targetSteps={reasonAction?.targetSteps}
        busy={busy}
        onCancel={() => setReasonAction(null)}
        onConfirm={(reason, targetStep?: WorkflowStepCode) =>
          reasonAction && perform(reasonAction.action, reasonAction.action === "Reopen" ? { reason, targetStep } : { reason })
        }
      />

      {pdf && <EvaluationPdfModal isOpen onClose={() => setPdf(null)} record={record} forms={forms} initialCode={pdf} />}
    </div>
  );
}

/** Id khu thao tác và khối nội dung của từng bước (cuộn tới từ dòng trạng thái / thanh tiến trình). */
const ACTIONS_ANCHOR = "record-actions";
const EXTERNAL_SECTION = "step-external";
const STEP_SECTION: Record<WorkflowStepCode, string> = {
  B1_REGISTER: "step-b1",
  B1_APPROVE: "step-b1",
  B2_SELF_SCORE: "step-b2-self",
  B2_CELL_CONFIRM: "step-b2-cell",
  B3A_COLLECTIVE: "step-b3a",
  B3B_APPRAISAL: "step-b3b",
  B3C_DIRECTOR: "step-b3c",
  B4_DECISION: "step-b4",
  B5_PUBLISH: "step-b4",
};
const B1_STEPS: WorkflowStepCode[] = ["B1_REGISTER", "B1_APPROVE"];
const DECISION_STEPS: WorkflowStepCode[] = ["B4_DECISION", "B5_PUBLISH"];

/** Cuộn mượt tới khu thao tác và đặt focus vào ô nhập / nút đầu tiên. */
function goToActions() {
  const area = document.getElementById(ACTIONS_ANCHOR);
  if (!area) return;
  area.scrollIntoView({ behavior: "smooth", block: "start" });
  const first = area.querySelector<HTMLElement>(
    "input:not([disabled]):not([type='hidden']), select:not([disabled]), textarea:not([disabled]), button:not([disabled])",
  );
  (first ?? area).focus({ preventScroll: true });
}

/** Số ngày từ hôm nay (giờ máy) tới thời hạn dạng yyyy-MM-dd; null khi không đọc được. */
function daysUntil(deadline: string): number | null {
  const [y, m, d] = deadline.slice(0, 10).split("-").map(Number);
  if (!y || !m || !d) return null;
  const now = new Date();
  const today = Date.UTC(now.getFullYear(), now.getMonth(), now.getDate());
  return Math.round((Date.UTC(y, m - 1, d) - today) / 86_400_000);
}

/** "Hạn dd/MM/yyyy (còn N ngày / hôm nay / quá hạn N ngày)". */
function DueText({ item }: { item?: RecordStepProgressDto }) {
  if (!item?.deadline) return null;
  const days = daysUntil(item.deadline);
  const late = item.overdue || (days !== null && days < 0);
  const rest =
    days === null ? "" : days > 0 ? ` (còn ${days} ngày)` : days === 0 ? " (hôm nay)" : ` (quá hạn ${-days} ngày)`;
  return (
    <>
      <span className={styles.statusSep} aria-hidden="true">·</span>
      <span className={`num ${styles.statusPart} ${late ? styles.late : ""}`}>Hạn {formatDate(item.deadline)}{rest}</span>
    </>
  );
}

/**
 * Dòng trạng thái dưới tên cán bộ: việc của người đang xem (theo `actions`) kèm nút tới khu thao tác;
 * nếu không có việc thì bước đang chờ và chủ thể thực hiện (máy chủ mô tả); hồ sơ đã công bố thì mức xếp loại.
 */
function RecordStatusLine({ record, actions, onGo }: { record: EvaluationRecordDto; actions: RecordActionDto[]; onGo: () => void }) {
  if (record.status === "Published") {
    return (
      <div className={`${styles.statusLine} ${styles.statusDone}`}>
        <span className={styles.statusText}>
          <i className="bi bi-check-circle-fill" aria-hidden="true" />
          <strong>Đã công bố kết quả</strong>
          {hasGrade(record.finalGrade) && <GradeBadge grade={record.finalGrade} />}
        </span>
      </div>
    );
  }

  const primary = actions.find((a) => !a.requiresReason) ?? actions[0];
  if (primary) {
    const step = record.progress.find((p) => p.step === primary.step);
    return (
      <div className={`${styles.statusLine} ${styles.statusMine}`}>
        <span className={styles.statusText}>
          <i className="bi bi-pencil-square" aria-hidden="true" />
          <span>Việc của bạn: <strong>{primary.label}</strong></span>
          <DueText item={step} />
        </span>
        <button type="button" className={`btn btn-primary btn-sm ${styles.statusGo}`} onClick={onGo}>
          Làm ngay <i className="bi bi-arrow-down-short" aria-hidden="true" />
        </button>
      </div>
    );
  }

  const steps = record.progress.filter((p) => p.state !== "skipped");
  const index = steps.findIndex((p) => p.state === "current");
  if (index < 0) return null;
  const current = steps[index];
  const actor = actorOf(current);
  return (
    <div className={styles.statusLine}>
      <span className={styles.statusText}>
        <span>Đang ở bước {index + 1} – <strong>{current.name}</strong></span>
        {actor && (
          <>
            <span className={styles.statusSep} aria-hidden="true">·</span>
            <span className={styles.statusPart}>{current.mode === "External" ? actor : `Chờ: ${actor}`}</span>
          </>
        )}
        <DueText item={current} />
      </span>
    </div>
  );
}

/** Thông tin hồ sơ: kỳ, phòng, Chi bộ, thẩm quyền, hồ sơ luồng. */
function RecordInfo({ record }: { record: EvaluationRecordDto }) {
  return (
    <section className={styles.block}>
      <div className={styles.blockH}>
        <h2>Thông tin</h2>
      </div>
      <div className={styles.body}>
        <Fields
          items={[
            ["Kỳ", record.periodName],
            ["Phòng", record.departmentName],
            ["Chi bộ", record.partyCellName],
            ["Thẩm quyền", record.approvalAuthority === "CapTren" ? "Cấp trên quyết định" : "Đảng ủy cơ sở quyết định"],
            ["Cách xét duyệt", record.workflowProfileName || record.workflowProfileCode],
          ]}
        />
      </div>
    </section>
  );
}

type FieldItem = [label: string, value: React.ReactNode];

const isEmpty = (value: React.ReactNode) => value === undefined || value === null || value === "" || value === false;

/** Danh sách trường chỉ đọc: bỏ trường chưa có giá trị; không còn trường nào thì báo chưa có dữ liệu. */
function Fields({ items }: { items: FieldItem[] }) {
  const shown = items.filter(([, value]) => !isEmpty(value));
  if (shown.length === 0) return <div className={styles.small}>Chưa có dữ liệu.</div>;
  return (
    <dl className={styles.dl}>
      {shown.map(([label, value]) => (
        <React.Fragment key={label}>
          <dt>{label}</dt>
          <dd>{value}</dd>
        </React.Fragment>
      ))}
    </dl>
  );
}

const hasGrade = (grade?: string | null) => !!grade && grade !== "ChuaXepLoai";

/** Dữ liệu đã lưu của các bước (chỉ đọc), mỗi bước một khối; bước chưa tới thu gọn, bước không áp dụng ẩn. */
function RecordData({ record }: { record: EvaluationRecordDto }) {
  const { toast } = useToast();
  const { criteria } = useCriteria(record.periodId);
  const stepOf = (step: WorkflowStepCode) => record.progress.find((p) => p.step === step);
  const enabled = (step: WorkflowStepCode) => stepOf(step)?.enabled ?? true;
  /** Các bước áp dụng của một khối (khối "Danh mục" gồm B1 đăng ký + duyệt; "Quyết định và công bố" gồm B4 + B5). */
  const itemsOf = (steps: WorkflowStepCode[]) =>
    steps.map(stepOf).filter((p): p is RecordStepProgressDto => !!p && p.state !== "skipped");
  const isCurrent = (steps: WorkflowStepCode[]) => itemsOf(steps).some((p) => p.state === "current");
  /** Khối hiện khi bước đã xong / đang làm, hoặc còn dữ liệu cũ (sau khi mở lại); bước chưa tới chưa có dữ liệu thì ẩn. */
  const visible = (steps: WorkflowStepCode[], hasData: boolean) => {
    const items = itemsOf(steps);
    if (items.length === 0) return steps.every((s) => !stepOf(s)) || hasData;
    return hasData || items.some((p) => p.state === "done" || p.state === "current");
  };
  /** Trạng thái ngắn ở tiêu đề khối: ✓ ngày · người / Đang làm / Chưa tới (+ "Cấp trên thực hiện"). */
  const tag = (steps: WorkflowStepCode[]) => {
    const items = itemsOf(steps);
    if (items.length === 0) return undefined;
    const external = items.some((p) => p.mode === "External");
    let status: React.ReactNode;
    if (items.some((p) => p.state === "current")) {
      status = <span className={`tag ${styles.stepTagNow}`}>Đang làm</span>;
    } else if (items.every((p) => p.state === "done")) {
      const last = items[items.length - 1];
      const day = formatShortDateTime(last.doneAt).slice(0, 5);
      const text = [day, last.doneByName].filter(Boolean).join(" · ");
      status = (
        <span className={`tag ${styles.stepTagDone}`} title={text || undefined}>
          <i className="bi bi-check-lg" aria-hidden="true" />
          {text ? <span className={styles.stepTagText}>{text}</span> : "Đã xong"}
        </span>
      );
    } else {
      status = <span className={`tag ${styles.tNeutral}`}>Chưa tới</span>;
    }
    return (
      <>
        {external && <span className={`tag ${styles.tNeutral} ${styles.hideNarrow}`}>Cấp trên thực hiện</span>}
        {status}
      </>
    );
  };
  const section = (steps: WorkflowStepCode[]) => ({
    anchorId: STEP_SECTION[steps[0]],
    tag: tag(steps),
    defaultOpen: isCurrent(steps),
  });
  const axes = criteria?.content.axes ?? [];
  const axisName = (code: string) => axes.find((a) => a.code.toLowerCase() === code.toLowerCase())?.name;
  const groupName = (code: string) => criteria?.content.generalGroups.find((g) => g.code === code)?.name;
  const axisMax = (code: string) => axes.find((a) => a.code.toLowerCase() === code.toLowerCase())?.maxScore;
  const params = criteria?.content.parameters;
  const selfScored = !!record.selfScoredAt || stepOf("B2_SELF_SCORE")?.state === "done";
  const axisEntries = record.axisScores ? Object.entries(record.axisScores) : [];
  const selfVisible = visible(["B2_SELF_SCORE"], !!record.selfScoredAt);
  const appraisedLevels = record.tasks.some((t) => t.appraisalLevel != null);

  return (
    <>
      {record.externalResults?.length > 0 && (
        <CollapsibleSection title="Kết quả do cấp trên thực hiện" anchorId={EXTERNAL_SECTION} defaultOpen={false} flush>
          <div className={styles.tw}><table className={styles.table}>
            <thead><tr><th>Bước</th><th>Cơ quan</th><th>Văn bản</th><th>Mức / điểm</th><th>Ghi nhận</th></tr></thead>
            <tbody>
              {record.externalResults.map((r) => (
                <tr key={r.step}>
                  <td>{r.stepName}</td>
                  <td>{r.authorityName}{r.comment && <span className={styles.sub}>{r.comment}</span>}</td>
                  <td>
                    {r.documentNumber || "—"}{r.documentDate ? ` (${formatDate(r.documentDate)})` : ""}
                    {r.attachmentId && (
                      <div>
                        <button
                          type="button"
                          className={styles.linkBtn}
                          onClick={() => {
                            attachmentService.downloadById(r.attachmentId as string)
                              .catch((error: any) => toast.error(error?.message || "Không tải được văn bản đính kèm."));
                          }}
                        >
                          <i className="bi bi-paperclip me-1" />Văn bản đính kèm
                        </button>
                      </div>
                    )}
                  </td>
                  <td><GradeBadge grade={r.grade} />{r.score != null ? ` · ${r.score}` : ""}</td>
                  <td>{r.recordedByName || "—"}<span className={styles.sub}>{formatDateTime(r.recordedAt)}</span></td>
                </tr>
              ))}
            </tbody>
          </table></div>
        </CollapsibleSection>
      )}

      {enabled("B1_REGISTER") && visible(B1_STEPS, record.tasks.length > 0) && (
        <CollapsibleSection title="Danh mục sản phẩm (Mẫu 01)" {...section(B1_STEPS)} flush>
          {record.tasks.length === 0 ? (
            <div className={`${styles.body} ${styles.small}`}>Chưa đăng ký.</div>
          ) : (
            <div className={styles.tw}><table className={styles.table}>
              <thead><tr><th>#</th><th>Sản phẩm</th><th>Loại · vai trò · trục</th><th>Trọng số</th><th>Điểm</th>{appraisedLevels && <th>Thẩm định</th>}<th>Minh chứng</th></tr></thead>
              <tbody>
                {record.tasks.map((t) => (
                  <tr key={t.id}>
                    <td className="num">{t.taskOrder}</td>
                    <td>
                      {t.taskName}{t.productCode ? ` (${t.productCode})` : ""}
                      {t.targetOutput && <span className={styles.sub}>Chuẩn đạt: {t.targetOutput}</span>}
                      {t.exceedCriteria && <span className={styles.sub}>Vượt chuẩn: {t.exceedCriteria}</span>}
                      {t.evidenceSource && <span className={styles.sub}>Minh chứng: {t.evidenceSource}</span>}
                      {t.confirmer && <span className={styles.sub}>Xác nhận: {t.confirmer}</span>}
                    </td>
                    <td>{[t.kind && TASK_KIND_LABELS[t.kind], t.role && TASK_ROLE_LABELS[t.role], t.axisCode].filter(Boolean).join(" · ") || "—"}</td>
                    <td className="num">{t.weight}</td>
                    <td className="num">{t.selfScore}</td>
                    {appraisedLevels && (
                      <td>
                        {t.appraisalLevel ? `Mức ${t.appraisalLevel}` : "—"}
                        {t.appraisalComment && <span className={styles.sub}>{t.appraisalComment}</span>}
                      </td>
                    )}
                    <td>{t.attachmentOriginalName || t.attachmentFileName || "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table></div>
          )}
          {record.selfScoreForm === "09A" && record.weightFrame && (
            <div className={styles.subline}>
              Khung {record.weightFrame.code} · Tỷ lệ 4 mặt: {describeFrame(record.weightFrame)}{record.weightFrameAdjusted ? " (đã điều chỉnh)" : ""}
            </div>
          )}
          {record.tasksApprovedAt && (
            <div className={styles.subline}>
              Duyệt: {record.tasksApprovedByName} · {formatDateTime(record.tasksApprovedAt)}{record.tasksApprovalComment ? ` — ${record.tasksApprovalComment}` : ""}
            </div>
          )}
        </CollapsibleSection>
      )}

      {selfVisible && (
      <CollapsibleSection
        title={`Phiếu tự chấm (Mẫu 09${record.selfScoreForm === "09B" ? "B" : "A"})`}
        {...section(["B2_SELF_SCORE"])}
        flush
      >
        {selfScored ? (
          <>
            <div className={styles.totals}>
              <div>
                <small>Tiêu chí chung</small>
                <b>{fmt(record.generalCriteriaScore)}</b>
                {params && <span className={`${styles.muted} num`}> / {fmt(params.generalMaxScore)}</span>}
              </div>
              <div>
                <small>Kết quả nhiệm vụ</small>
                <b>{fmt(record.tasksScore)}</b>
                {params && <span className={`${styles.muted} num`}> / {fmt(params.totalTaskWeight)}</span>}
              </div>
              <div>
                <small>Tổng điểm</small>
                <b>{fmt(record.totalSelfScore)}</b>
              </div>
            </div>
            {axisEntries.length > 0 && (
              <div className={styles.axes}>
                {axisEntries.map(([code, score]) => {
                  const max = axisMax(code);
                  return (
                    <div key={code} title={axisName(code)}>
                      <small>{code}</small>
                      <b>{fmt(score)}</b>
                      {max != null && <i className="num">/{fmt(max)}</i>}
                    </div>
                  );
                })}
              </div>
            )}
            {(hasGrade(record.selfProposedGrade) || record.selfScoredAt) && (
              <div className={`${styles.subline} d-flex flex-wrap align-items-center gap-2`}>
                {hasGrade(record.selfProposedGrade) && <>Mức tự đề xuất <GradeBadge grade={record.selfProposedGrade} /></>}
                {record.selfScoredAt && <span className="num ms-auto">Nộp {formatDateTime(record.selfScoredAt)}</span>}
              </div>
            )}
          </>
        ) : (
          <div className={`${styles.body} ${styles.small}`}>Chưa có dữ liệu.</div>
        )}
        {record.generalGroupNotes && Object.keys(record.generalGroupNotes).length > 0 && (
          <div className={styles.subline}>
            <ul>
              {Object.entries(record.generalGroupNotes).map(([code, text]) => (
                <li key={code}>
                  <strong>Nhóm {code}</strong>{groupName(code) ? ` (${groupName(code)})` : ""} — {text}
                </li>
              ))}
            </ul>
          </div>
        )}
        {record.axisNotes && Object.keys(record.axisNotes).length > 0 && (
          <div className={styles.subline}>
            <ul>
              {Object.entries(record.axisNotes).map(([code, note]) => (
                <li key={code}>
                  <strong>{code}</strong>{axisName(code) ? ` (${axisName(code)})` : ""}
                  {note.target ? ` — Mục tiêu: ${note.target}` : ""}
                  {note.result ? ` — Kết quả: ${note.result}` : ""}
                  {note.note ? ` — Ghi chú: ${note.note}` : ""}
                </li>
              ))}
            </ul>
          </div>
        )}
      </CollapsibleSection>
      )}

      {selfVisible && criteriaAppliesForm(criteria, "9D") && (
        <CollapsibleSection title="Kết quả thực hiện nhiệm vụ (Mẫu 9D)" defaultOpen={false}>
          <AxisSummaryView axes={axes} reports={record.axisReports} />
        </CollapsibleSection>
      )}

      {selfVisible && criteriaAppliesForm(criteria, "09C") && (
        <CollapsibleSection title="Bản tự đánh giá (Mẫu 09C)" defaultOpen={false}>
          <SelfAssessmentView sections={selfAssessmentSections(criteria)} value={record.selfAssessment}
            blocks={buildAxisTaskBlocks(axes, record.axisReports, criteria)} />
        </CollapsibleSection>
      )}

      {enabled("B2_CELL_CONFIRM") && visible(["B2_CELL_CONFIRM"], !!record.cellConfirmedAt) && (
        <CollapsibleSection title="Xác nhận của Chi bộ" {...section(["B2_CELL_CONFIRM"])}>
          <Fields
            items={[
              ["Người xác nhận", record.cellConfirmedByName],
              ["Thời điểm", record.cellConfirmedAt ? formatDateTime(record.cellConfirmedAt) : null],
              ["Ý kiến", record.partyCellComment],
            ]}
          />
        </CollapsibleSection>
      )}

      {enabled("B3A_COLLECTIVE") && visible(["B3A_COLLECTIVE"], !!record.collectiveRecordedAt) && (
        <CollapsibleSection title={STEP_NAMES.B3A_COLLECTIVE} {...section(["B3A_COLLECTIVE"])}>
          <Fields
            items={[
              ["Mức đề xuất", hasGrade(record.collectiveProposedGrade) ? <GradeBadge grade={record.collectiveProposedGrade} /> : null],
              ["Ghi nhận bởi", record.collectiveRecordedByName],
              ["Nhận xét", record.collectiveComment],
            ]}
          />
        </CollapsibleSection>
      )}

      {enabled("B3B_APPRAISAL") && visible(["B3B_APPRAISAL"], !!record.appraisedAt) && (
        <CollapsibleSection title="Thẩm định" {...section(["B3B_APPRAISAL"])}>
          <Fields
            items={[
              ["Điểm thẩm định", record.appraisedAt || stepOf("B3B_APPRAISAL")?.state === "done" ? record.appraisalScore : null],
              ["Tiêu chí chung", record.appraisalGeneralScore],
              ["Kết quả nhiệm vụ", record.appraisalTasksScore],
              ["Mức đề xuất", hasGrade(record.appraisalProposedGrade) ? <GradeBadge grade={record.appraisalProposedGrade} /> : null],
              ["Người / cơ quan thẩm định", record.appraisedByName],
              ["Ý kiến", record.appraisalComment],
              ["Giải trình chênh lệch", record.appraisalExplanation],
            ]}
          />
          {(record.gradeWarnings?.length ?? 0) > 0 && (
            <div className="alert alert-warning small py-2 mt-2 mb-0" role="status">
              {record.gradeWarnings!.map((w) => <div key={w}>{w}</div>)}
            </div>
          )}
        </CollapsibleSection>
      )}

      {enabled("B3C_DIRECTOR") && visible(["B3C_DIRECTOR"], !!record.directorReviewedAt) && (
        <CollapsibleSection title={STEP_NAMES.B3C_DIRECTOR} {...section(["B3C_DIRECTOR"])}>
          <Fields
            items={[
              ["Mức đề xuất", hasGrade(record.directorProposedGrade) ? <GradeBadge grade={record.directorProposedGrade} /> : null],
              ["Người / cơ quan nhận xét", record.directorReviewedByName],
              ["Nhận xét", record.directorComment],
            ]}
          />
        </CollapsibleSection>
      )}

      {stepOf("B4_DECISION")?.state !== "skipped" && visible(DECISION_STEPS, !!record.decisionRecordedAt || !!record.publishedAt) && (
        <CollapsibleSection title="Quyết định và công bố" {...section(DECISION_STEPS)}>
          <Fields
            items={[
              ["Mức xếp loại", hasGrade(record.finalGrade) ? <GradeBadge grade={record.finalGrade} /> : null],
              ["Điểm chính thức", hasGrade(record.finalGrade) ? record.finalScore : null],
              ["Văn bản", record.decisionDocumentNumber ? `${record.decisionDocumentNumber} (${formatDate(record.decisionDocumentDate)})` : null],
              ["Cơ quan quyết định", record.decisionAuthorityName],
              ["Đề xuất về công tác cán bộ", record.cadreWorkProposal],
              ["Công bố", record.publishedAt ? `${record.publishedByName} · ${formatDateTime(record.publishedAt)}` : null],
            ]}
          />
        </CollapsibleSection>
      )}
    </>
  );
}
