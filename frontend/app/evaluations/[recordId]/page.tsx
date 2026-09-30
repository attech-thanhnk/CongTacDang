"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { PageHeader } from "@/components/common/PageHeader";
import { Badge, GradeBadge, RecordStatusBadge } from "@/components/common/StatusBadge";
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
import { attachmentService } from "@/services/attachmentService";
import {
  EVALUATION_CONFLICT_EVENT,
  EvaluationConflictDetail,
  EvaluationRecordDto,
  EvaluationRecordHistoryDto,
  RecordActionDto,
  RecordFormDto,
  WorkflowActionCode,
  WorkflowStepCode,
  criteriaAppliesForm,
  evaluationService,
  isConcurrencyConflict,
  selfAssessmentSections,
} from "@/services/evaluationService";
import { formatDate, formatDateTime } from "@/lib/dateFormat";

/**
 * Trang hồ sơ đánh giá: đầu trang là tiến trình; khu thao tác (theo `actions`) ngay dưới tiến trình khi có;
 * dữ liệu từng bước chia mục thu gọn được; cột phải là biểu mẫu và lịch sử.
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

  const isReturned = !!record.returnReason;
  const overdue = record.progress.some((p) => p.state === "current" && p.overdue);

  return (
    <div className="page-wrapper">
      <PageHeader
        title={record.fullName}
        subTitle={`${record.periodName} · ${record.departmentName || "Chưa gắn Phòng"} · ${record.partyCellName || "Chưa gắn Chi bộ"} · ${record.approvalAuthority === "CapTren" ? "Cấp trên quyết định" : "Đảng ủy cơ sở quyết định"} · Hồ sơ luồng: ${record.workflowProfileName || record.workflowProfileCode}`}
        badge={<RecordStatusBadge status={record.status} label={record.statusDisplayName} returned={isReturned} overdue={overdue} />}
        actions={
          <div className="d-flex gap-2">
            <Link href="/work-queue" className="btn btn-outline-secondary btn-sm"><i className="bi bi-arrow-left me-1" />Việc cần xử lý</Link>
            {forms.length > 0 && (
              <button type="button" className="btn btn-outline-primary btn-sm" onClick={() => setPdf(forms[0].code)}>
                <i className="bi bi-file-earmark-pdf me-1" />Xem bản in (PDF)
              </button>
            )}
          </div>
        }
      />

      <div className="page-body d-flex flex-column gap-3">
        {conflict && (
          <div className="alert alert-warning d-flex justify-content-between align-items-center mb-0" role="alert">
            <span><i className="bi bi-exclamation-triangle me-2" />{conflict}</span>
            <button type="button" className="btn btn-warning btn-sm" onClick={() => load()}>Tải lại hồ sơ</button>
          </div>
        )}
        {isReturned && (
          <div className="alert alert-danger mb-0">
            <strong>Hồ sơ bị trả lại:</strong> {record.returnReason}
          </div>
        )}

        <section className="card border-0 shadow-sm" aria-label="Tiến trình hồ sơ">
          <div className="card-body py-2 px-3">
            <RecordProgress progress={record.progress} />
          </div>
        </section>

        {actions.length > 0 && (
          <section className="card shadow-sm" style={{ outline: "2px solid #3b82f6", outlineOffset: -1 }} aria-labelledby="record-actions-title">
            <div className="card-header bg-primary-subtle border-0 py-2">
              <h2 id="record-actions-title" className="h6 mb-0 text-primary-emphasis">
                <i className="bi bi-pencil-square me-2" />Thao tác của bạn
              </h2>
            </div>
            <div className="card-body" style={{ fontSize: 14 }}>
              <RecordActionPanel record={record} actions={actions} busy={busy} onSubmit={perform} onReason={setReasonAction} />
            </div>
          </section>
        )}

        <div className="row g-3">
          <div className="col-12 col-xl-8 d-flex flex-column gap-3">
            <RecordData record={record} />
            <AppealPanel record={record} onRecordChanged={load} />
            <ImprovementPlanPanel record={record} />
          </div>
          <div className="col-12 col-xl-4 d-flex flex-column gap-3">
            <RecordFormsPanel recordId={record.id} fullName={record.fullName} forms={forms} />
            <RecordHistory history={history} />
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

type FieldItem = [label: string, value: React.ReactNode];

const isEmpty = (value: React.ReactNode) => value === undefined || value === null || value === "" || value === false;

/** Lưới trường chỉ đọc: bỏ trường chưa có giá trị; không còn trường nào thì báo chưa có dữ liệu. */
function Fields({ items }: { items: FieldItem[] }) {
  const shown = items.filter(([, value]) => !isEmpty(value));
  if (shown.length === 0) return <div className="text-secondary">Chưa có dữ liệu.</div>;
  return (
    <div className="row g-2">
      {shown.map(([label, value]) => (
        <div key={label} className="col-6 col-md-4">
          <div className="text-secondary" style={{ fontSize: 13 }}>{label}</div>
          <div className="fw-semibold" style={{ whiteSpace: "pre-wrap" }}>{value}</div>
        </div>
      ))}
    </div>
  );
}

const hasGrade = (grade?: string | null) => !!grade && grade !== "ChuaXepLoai";

/** Dữ liệu đã lưu của các bước (chỉ đọc), mỗi bước một mục; bước chưa tới thu gọn, bước không áp dụng ẩn. */
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
    return (
      <span className="d-inline-flex gap-1">
        {item?.mode === "External" && <Badge tone="neutral">Cấp trên thực hiện</Badge>}
        {item?.state === "pending" && <Badge tone="neutral">Chưa tới</Badge>}
      </span>
    );
  };
  const axisName = (code: string) =>
    criteria?.content.axes.find((a) => a.code.toLowerCase() === code.toLowerCase())?.name;

  return (
    <section className="card border-0 shadow-sm">
      <div className="card-body d-flex flex-column gap-2">
        <h2 className="h6 mb-1">Dữ liệu hồ sơ</h2>

        {record.externalResults?.length > 0 && (
          <CollapsibleSection title="Kết quả do cấp trên thực hiện">
            <div className="table-responsive"><table className="table table-sm mb-0">
              <thead><tr className="text-secondary" style={{ fontSize: 13 }}><th>Bước</th><th>Cơ quan</th><th>Văn bản</th><th>Mức / điểm</th><th>Ghi nhận</th></tr></thead>
              <tbody>
                {record.externalResults.map((r) => (
                  <tr key={r.step}>
                    <td>{r.stepName}</td>
                    <td>{r.authorityName}{r.comment && <div className="text-secondary">{r.comment}</div>}</td>
                    <td>
                      {r.documentNumber || "—"}{r.documentDate ? ` (${formatDate(r.documentDate)})` : ""}
                      {r.attachmentId && (
                        <div>
                          <button
                            type="button"
                            className="btn btn-link btn-sm p-0"
                            onClick={() => {
                              attachmentService.downloadById(r.attachmentId as string)
                                .catch((error: any) => toast.error(error?.message || "Không tải được văn bản đính kèm."));
                            }}
                          >
                            <i className="bi bi-paperclip me-1" />Tải văn bản đính kèm
                          </button>
                        </div>
                      )}
                    </td>
                    <td><GradeBadge grade={r.grade} />{r.score != null ? ` · ${r.score}` : ""}</td>
                    <td>{r.recordedByName || "—"}<div className="text-secondary">{formatDateTime(r.recordedAt)}</div></td>
                  </tr>
                ))}
              </tbody>
            </table></div>
          </CollapsibleSection>
        )}

        {enabled("B1_REGISTER") && (
          <CollapsibleSection title="Danh mục sản phẩm (Mẫu 01)" tag={tag("B1_REGISTER")} defaultOpen={reached("B1_REGISTER")}>
            {record.tasks.length === 0 ? (
              <div className="text-secondary">Chưa đăng ký.</div>
            ) : (
              <div className="table-responsive"><table className="table table-sm mb-1">
                <thead><tr className="text-secondary" style={{ fontSize: 13 }}><th>#</th><th>Sản phẩm</th><th>Trọng số</th><th>Điểm</th><th>Minh chứng</th></tr></thead>
                <tbody>
                  {record.tasks.map((t) => (
                    <tr key={t.id}>
                      <td>{t.taskOrder}</td>
                      <td>{t.taskName}<div className="text-secondary">{t.targetOutput}</div></td>
                      <td>{t.weight}</td>
                      <td>{t.selfScore}</td>
                      <td>{t.attachmentOriginalName || t.attachmentFileName || "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table></div>
            )}
            {record.tasksApprovedAt && (
              <div className="text-secondary" style={{ fontSize: 13 }}>Duyệt bởi {record.tasksApprovedByName} lúc {formatDateTime(record.tasksApprovedAt)}{record.tasksApprovalComment ? ` — ${record.tasksApprovalComment}` : ""}</div>
            )}
          </CollapsibleSection>
        )}

        <CollapsibleSection
          title={`Tự chấm (Mẫu 09${record.selfScoreForm === "09B" ? "B" : "A"})`}
          tag={tag("B2_SELF_SCORE")}
          defaultOpen={reached("B2_SELF_SCORE")}
        >
          {record.selfScoredAt || stepOf("B2_SELF_SCORE")?.state === "done" ? (
            <Fields
              items={[
                ["Tiêu chí chung", record.generalCriteriaScore],
                ["Kết quả nhiệm vụ", record.tasksScore],
                ["Tổng tự chấm", record.totalSelfScore],
                ["Mức tự đề xuất", hasGrade(record.selfProposedGrade) ? <GradeBadge grade={record.selfProposedGrade} /> : null],
                ["Nộp lúc", record.selfScoredAt ? formatDateTime(record.selfScoredAt) : null],
                [
                  "Điểm theo trục",
                  record.axisScores && Object.keys(record.axisScores).length > 0
                    ? Object.entries(record.axisScores).map(([code, score], i, all) => (
                        <span key={code} title={axisName(code)}>{code}: {score}{i < all.length - 1 ? " · " : ""}</span>
                      ))
                    : null,
                ],
              ]}
            />
          ) : (
            <div className="text-secondary">Chưa có dữ liệu.</div>
          )}
          {record.axisNotes && Object.keys(record.axisNotes).length > 0 && (
            <ul className="mb-0 mt-2 ps-3">
              {Object.entries(record.axisNotes).map(([code, note]) => (
                <li key={code}>
                  <strong>{code}</strong>{axisName(code) ? ` (${axisName(code)})` : ""}
                  {note.target ? ` — Mục tiêu: ${note.target}` : ""}
                  {note.result ? ` — Kết quả: ${note.result}` : ""}
                  {note.note ? ` — Ghi chú: ${note.note}` : ""}
                </li>
              ))}
            </ul>
          )}
        </CollapsibleSection>

        {criteriaAppliesForm(criteria, "9D") && (
          <CollapsibleSection title="Kết quả thực hiện nhiệm vụ theo trục (Mẫu 9D)" tag={tag("B2_SELF_SCORE")} defaultOpen={reached("B2_SELF_SCORE")}>
            <TaskResultsView axes={criteria?.content.axes ?? []} rows={record.taskResults} />
          </CollapsibleSection>
        )}

        {criteriaAppliesForm(criteria, "09C") && (
          <CollapsibleSection title="Bản tự đánh giá (Mẫu 09C)" tag={tag("B2_SELF_SCORE")} defaultOpen={reached("B2_SELF_SCORE")}>
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
          <CollapsibleSection title="Đề xuất của tập thể lãnh đạo" tag={tag("B3A_COLLECTIVE")} defaultOpen={reached("B3A_COLLECTIVE")}>
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
                ["Giải trình chênh lệch với điểm tự chấm", record.appraisalExplanation],
              ]}
            />
          </CollapsibleSection>
        )}

        {enabled("B3C_DIRECTOR") && (
          <CollapsibleSection title="Cấp trực tiếp sử dụng / lãnh đạo đơn vị đề xuất" tag={tag("B3C_DIRECTOR")} defaultOpen={reached("B3C_DIRECTOR")}>
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
      </div>
    </section>
  );
}
