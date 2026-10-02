"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { GradeBadge } from "@/components/common/StatusBadge";
import { useToast } from "@/contexts/ToastContext";
import { RecordProgress } from "@/components/evaluations/RecordProgress";
import { RecordActionPanel } from "@/components/evaluations/RecordActionPanel";
import { ReasonDialog } from "@/components/evaluations/ReasonDialog";
import { AppealPanel } from "@/components/evaluations/AppealPanel";
import { ImprovementPlanPanel } from "@/components/evaluations/ImprovementPlanPanel";
import { EvaluationPdfModal } from "@/components/evaluations/EvaluationPdfModal";
import { RecordFormsPanel } from "@/components/evaluations/RecordFormsPanel";
import { RecordHistory } from "@/components/evaluations/RecordHistory";
import { CollapsibleSection } from "@/components/evaluations/CollapsibleSection";
import { SelfAssessmentView, TaskResultsView } from "@/components/evaluations/IndividualFormInputs";
import { useCriteria } from "@/components/evaluations/useCriteria";
import { initialsOf } from "@/components/dashboard/taskUtils";
import styles from "@/components/evaluations/record.module.css";
import { attachmentService } from "@/services/attachmentService";
import { fmt } from "@/services/criteriaService";
import {
  EVALUATION_CONFLICT_EVENT,
  EvaluationConflictDetail,
  EvaluationRecordDto,
  EvaluationRecordHistoryDto,
  RecordActionDto,
  RecordFormDto,
  WorkflowActionCode,
  WorkflowStepCode,
  STEP_NAMES,
  criteriaAppliesForm,
  evaluationService,
  isConcurrencyConflict,
  selfAssessmentSections,
  taskLevels,
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

  if (loading) {
    return <div className="page-wrapper"><div className="text-secondary p-4"><span className="spinner-border spinner-border-sm me-2" />Đang tải hồ sơ...</div></div>;
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
          <div>
            <h1>{record.fullName}</h1>
            <p className={styles.lede}>{[record.positionTitle, record.partyCellName].filter(Boolean).join(" · ") || record.departmentName}</p>
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
          <RecordProgress progress={record.progress} />
        </section>

        <div className={styles.cols}>
          <div className={styles.stack}>
            <RecordActionPanel record={record} actions={actions} busy={busy} onSubmit={perform} onReason={setReasonAction} />
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
            ["Hồ sơ luồng", record.workflowProfileName || record.workflowProfileCode],
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
  const reached = (step: WorkflowStepCode) => {
    const state = stepOf(step)?.state;
    return state === undefined || state === "done" || state === "current";
  };
  const tag = (step: WorkflowStepCode) => {
    const item = stepOf(step);
    if (item?.mode !== "External" && item?.state !== "pending") return undefined;
    return (
      <>
        {item?.mode === "External" && <span className={`tag ${styles.tNeutral}`}>Cấp trên thực hiện</span>}
        {item?.state === "pending" && <span className={`tag ${styles.tNeutral}`}>Chưa tới</span>}
      </>
    );
  };
  const axes = criteria?.content.axes ?? [];
  const axisName = (code: string) => axes.find((a) => a.code.toLowerCase() === code.toLowerCase())?.name;
  const axisMax = (code: string) => axes.find((a) => a.code.toLowerCase() === code.toLowerCase())?.maxScore;
  const params = criteria?.content.parameters;
  const selfScored = !!record.selfScoredAt || stepOf("B2_SELF_SCORE")?.state === "done";
  const axisEntries = record.axisScores ? Object.entries(record.axisScores) : [];

  return (
    <>
      {record.externalResults?.length > 0 && (
        <CollapsibleSection title="Kết quả do cấp trên thực hiện" flush>
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

      {enabled("B1_REGISTER") && (
        <CollapsibleSection title="Danh mục sản phẩm (Mẫu 01)" tag={tag("B1_REGISTER")} defaultOpen={reached("B1_REGISTER")} flush>
          {record.tasks.length === 0 ? (
            <div className={`${styles.body} ${styles.small}`}>Chưa đăng ký.</div>
          ) : (
            <div className={styles.tw}><table className={styles.table}>
              <thead><tr><th>#</th><th>Sản phẩm</th><th>Trọng số</th><th>Điểm</th><th>Minh chứng</th></tr></thead>
              <tbody>
                {record.tasks.map((t) => (
                  <tr key={t.id}>
                    <td className="num">{t.taskOrder}</td>
                    <td>{t.taskName}<span className={styles.sub}>{t.targetOutput}</span></td>
                    <td className="num">{t.weight}</td>
                    <td className="num">{t.selfScore}</td>
                    <td>{t.attachmentOriginalName || t.attachmentFileName || "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table></div>
          )}
          {record.tasksApprovedAt && (
            <div className={styles.subline}>
              Duyệt: {record.tasksApprovedByName} · {formatDateTime(record.tasksApprovedAt)}{record.tasksApprovalComment ? ` — ${record.tasksApprovalComment}` : ""}
            </div>
          )}
        </CollapsibleSection>
      )}

      <CollapsibleSection
        title={`Phiếu tự chấm (Mẫu 09${record.selfScoreForm === "09B" ? "B" : "A"})`}
        tag={tag("B2_SELF_SCORE")}
        defaultOpen={reached("B2_SELF_SCORE")}
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

      {criteriaAppliesForm(criteria, "9D") && (
        <CollapsibleSection title="Kết quả thực hiện nhiệm vụ (Mẫu 9D)" tag={tag("B2_SELF_SCORE")} defaultOpen={false}>
          <TaskResultsView axes={axes} rows={record.taskResults} levels={taskLevels(criteria)} />
        </CollapsibleSection>
      )}

      {criteriaAppliesForm(criteria, "09C") && (
        <CollapsibleSection title="Bản tự đánh giá (Mẫu 09C)" tag={tag("B2_SELF_SCORE")} defaultOpen={false}>
          <SelfAssessmentView sections={selfAssessmentSections(criteria)} value={record.selfAssessment} />
        </CollapsibleSection>
      )}

      {enabled("B2_CELL_CONFIRM") && (
        <CollapsibleSection title="Xác nhận của Chi bộ" tag={tag("B2_CELL_CONFIRM")} defaultOpen={reached("B2_CELL_CONFIRM")}>
          <Fields
            items={[
              ["Người xác nhận", record.cellConfirmedByName],
              ["Thời điểm", record.cellConfirmedAt ? formatDateTime(record.cellConfirmedAt) : null],
              ["Ý kiến", record.partyCellComment],
            ]}
          />
        </CollapsibleSection>
      )}

      {enabled("B3A_COLLECTIVE") && (
        <CollapsibleSection title={STEP_NAMES.B3A_COLLECTIVE} tag={tag("B3A_COLLECTIVE")} defaultOpen={reached("B3A_COLLECTIVE")}>
          <Fields
            items={[
              ["Mức đề xuất", hasGrade(record.collectiveProposedGrade) ? <GradeBadge grade={record.collectiveProposedGrade} /> : null],
              ["Ghi nhận bởi", record.collectiveRecordedByName],
              ["Nhận xét", record.collectiveComment],
            ]}
          />
        </CollapsibleSection>
      )}

      {enabled("B3B_APPRAISAL") && (
        <CollapsibleSection title="Thẩm định" tag={tag("B3B_APPRAISAL")} defaultOpen={reached("B3B_APPRAISAL")}>
          <Fields
            items={[
              ["Điểm thẩm định", record.appraisedAt || stepOf("B3B_APPRAISAL")?.state === "done" ? record.appraisalScore : null],
              ["Mức đề xuất", hasGrade(record.appraisalProposedGrade) ? <GradeBadge grade={record.appraisalProposedGrade} /> : null],
              ["Người / cơ quan thẩm định", record.appraisedByName],
              ["Ý kiến", record.appraisalComment],
              ["Giải trình chênh lệch", record.appraisalExplanation],
            ]}
          />
        </CollapsibleSection>
      )}

      {enabled("B3C_DIRECTOR") && (
        <CollapsibleSection title={STEP_NAMES.B3C_DIRECTOR} tag={tag("B3C_DIRECTOR")} defaultOpen={reached("B3C_DIRECTOR")}>
          <Fields
            items={[
              ["Mức đề xuất", hasGrade(record.directorProposedGrade) ? <GradeBadge grade={record.directorProposedGrade} /> : null],
              ["Người / cơ quan nhận xét", record.directorReviewedByName],
              ["Nhận xét", record.directorComment],
            ]}
          />
        </CollapsibleSection>
      )}

      {stepOf("B4_DECISION")?.state !== "skipped" && (
        <CollapsibleSection title="Quyết định và công bố" tag={tag("B4_DECISION")} defaultOpen={reached("B4_DECISION")}>
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
