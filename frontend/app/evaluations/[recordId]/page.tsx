"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { PageHeader } from "@/components/common/PageHeader";
import { useToast } from "@/contexts/ToastContext";
import { RecordProgress } from "@/components/evaluations/RecordProgress";
import { RecordActionPanel } from "@/components/evaluations/RecordActionPanel";
import { ReasonDialog } from "@/components/evaluations/ReasonDialog";
import { EvaluationPdfModal } from "@/components/evaluations/EvaluationPdfModal";
import { PrintTemplateType } from "@/components/evaluations/EvaluationPrintTemplate";
import {
  EVALUATION_CONFLICT_EVENT,
  EvaluationConflictDetail,
  EvaluationRecordDto,
  EvaluationRecordHistoryDto,
  RecordActionDto,
  WorkflowActionCode,
  WorkflowStepCode,
  evaluationService,
  gradeLabel,
  isConcurrencyConflict,
} from "@/services/evaluationService";

const formatDateTime = (value?: string | null) =>
  value ? new Intl.DateTimeFormat("vi-VN", { dateStyle: "short", timeStyle: "short" }).format(new Date(value)) : "—";

const formatDate = (value?: string | null) =>
  value ? new Intl.DateTimeFormat("vi-VN", { dateStyle: "short" }).format(new Date(value)) : "—";

/** Trang hồ sơ đánh giá: tiến trình 9 bước, dữ liệu từng bước, nút thao tác theo `actions`, lịch sử. */
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
  const [pdf, setPdf] = useState<PrintTemplateType | null>(null);

  const load = useCallback(async () => {
    if (!recordId) return;
    setLoadError(null);
    try {
      const [loadedRecord, loadedActions, loadedHistory] = await Promise.all([
        evaluationService.getRecordById(recordId),
        evaluationService.getRecordActions(recordId),
        evaluationService.getRecordHistory(recordId),
      ]);
      setRecord(loadedRecord);
      setActions(loadedActions.actions);
      setHistory(loadedHistory);
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

  return (
    <div className="page-wrapper">
      <PageHeader
        title={record.fullName}
        subTitle={`${record.periodName} · ${record.departmentName || "Chưa gắn Phòng"} · ${record.partyCellName || "Chưa gắn Chi bộ"} · ${record.approvalAuthority === "CapTren" ? "Cấp trên quyết định" : "Đảng ủy cơ sở quyết định"} · Hồ sơ luồng: ${record.workflowProfileName || record.workflowProfileCode}`}
        badge={<span className="badge text-bg-primary">{record.statusDisplayName}</span>}
        actions={
          <div className="d-flex gap-2">
            <Link href="/work-queue" className="btn btn-outline-secondary btn-sm"><i className="bi bi-arrow-left me-1" />Việc cần xử lý</Link>
            <div className="btn-group btn-group-sm">
              <button type="button" className="btn btn-outline-primary" onClick={() => setPdf("mau01")}>Mẫu 01</button>
              <button type="button" className="btn btn-outline-primary" onClick={() => setPdf("mau02")}>Mẫu 02</button>
              <button type="button" className="btn btn-outline-primary" onClick={() => setPdf("mau10")}>Mẫu 10</button>
            </div>
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

        <section className="card border-0 shadow-sm">
          <div className="card-body">
            <h2 className="h6 mb-3">Tiến trình</h2>
            <RecordProgress progress={record.progress} />
          </div>
        </section>

        <div className="row g-3">
          <div className="col-12 col-xl-7 d-flex flex-column gap-3">
            <section className="card border-0 shadow-sm">
              <div className="card-body">
                <h2 className="h6 mb-3">Thao tác của bạn</h2>
                <RecordActionPanel record={record} actions={actions} busy={busy} onSubmit={perform} onReason={setReasonAction} />
              </div>
            </section>
            <RecordData record={record} />
          </div>
          <div className="col-12 col-xl-5">
            <section className="card border-0 shadow-sm">
              <div className="card-body">
                <h2 className="h6 mb-3">Lịch sử hồ sơ</h2>
                {history.length === 0 ? (
                  <div className="text-secondary small">Chưa có mốc nào.</div>
                ) : (
                  <ul className="list-unstyled mb-0 d-flex flex-column gap-2">
                    {history.map((h) => (
                      <li key={h.id} className="border-start border-3 ps-2" style={{ borderColor: h.action === "Return" || h.action === "Reopen" ? "#dc2626" : "#93c5fd" }}>
                        <div className="small fw-semibold">
                          {h.actionName}
                          {h.stepName ? ` · ${h.stepName}` : ""}
                        </div>
                        <div className="small text-secondary">
                          {formatDateTime(h.createdAt)} · {h.actorName} · {h.fromStatusName || "—"} → {h.toStatusName}
                        </div>
                        {h.reason && <div className="small"><strong>Lý do:</strong> {h.reason}</div>}
                        {h.gradeBefore !== h.gradeAfter && (
                          <div className="small text-secondary">Mức: {gradeLabel(h.gradeBefore)} → {gradeLabel(h.gradeAfter)}</div>
                        )}
                        {h.scoreBefore !== h.scoreAfter && (
                          <div className="small text-secondary">Điểm: {h.scoreBefore ?? "—"} → {h.scoreAfter ?? "—"}</div>
                        )}
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </section>
          </div>
        </div>
      </div>

      <ReasonDialog
        isOpen={!!reasonAction}
        title={reasonAction?.label || ""}
        description={
          reasonAction?.action === "Reopen"
            ? "Mở lại hồ sơ đã công bố để đính chính. Dữ liệu các bước sau được giữ để đối chiếu; mọi lần mở lại được ghi lịch sử."
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

      {pdf && <EvaluationPdfModal isOpen onClose={() => setPdf(null)} templateType={pdf} record={record} />}
    </div>
  );
}

function Field({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="col-6 col-md-4">
      <div className="small text-secondary">{label}</div>
      <div className="small fw-semibold">{value === undefined || value === null || value === "" ? "—" : value}</div>
    </div>
  );
}

/** Nhãn chế độ bước cạnh tiêu đề mục dữ liệu: cấp trên thực hiện / không áp dụng cho nhóm này. */
function ModeNote({ record, step }: { record: EvaluationRecordDto; step: WorkflowStepCode }) {
  const mode = record.progress.find((p) => p.step === step)?.mode;
  if (mode === "External") return <span className="badge text-bg-light border ms-2 fw-normal">Do cấp trên thực hiện — ghi nhận kết quả</span>;
  if (mode === "Off") return <span className="badge text-bg-light border ms-2 fw-normal">Không áp dụng cho nhóm này</span>;
  return null;
}

/** Dữ liệu đã lưu của các bước (chỉ đọc). */
function RecordData({ record }: { record: EvaluationRecordDto }) {
  const enabled = (step: WorkflowStepCode) => record.progress.find((p) => p.step === step)?.enabled ?? true;
  const offSteps = record.progress.filter((p) => p.mode === "Off");
  return (
    <section className="card border-0 shadow-sm">
      <div className="card-body d-flex flex-column gap-3">
        <h2 className="h6 mb-0">Dữ liệu hồ sơ</h2>
        {offSteps.length > 0 && (
          <div className="small text-secondary">
            <i className="bi bi-slash-circle me-1" />
            Không áp dụng cho nhóm này ({record.workflowProfileName}): {offSteps.map((p) => p.name).join(", ")}.
          </div>
        )}

        {record.externalResults?.length > 0 && (
          <div>
            <h3 className="small fw-bold text-secondary">Kết quả do cấp trên thực hiện (đã ghi nhận)</h3>
            <table className="table table-sm small mb-0">
              <thead><tr><th>Bước</th><th>Cơ quan</th><th>Văn bản</th><th>Mức / điểm</th><th>Ghi nhận</th></tr></thead>
              <tbody>
                {record.externalResults.map((r) => (
                  <tr key={r.step}>
                    <td>{r.stepName}</td>
                    <td>{r.authorityName}{r.comment && <div className="text-secondary">{r.comment}</div>}</td>
                    <td>
                      {r.documentNumber || "—"}{r.documentDate ? ` (${formatDate(r.documentDate)})` : ""}
                      {r.attachmentId && <div><i className="bi bi-paperclip me-1" />Có tệp đính kèm</div>}
                    </td>
                    <td>{gradeLabel(r.grade)}{r.score != null ? ` · ${r.score}` : ""}</td>
                    <td>{r.recordedByName || "—"}<div className="text-secondary">{formatDateTime(r.recordedAt)}</div></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {enabled("B1_REGISTER") && (
          <div>
            <h3 className="small fw-bold text-secondary">Danh mục sản phẩm (Mẫu 01)</h3>
            {record.tasks.length === 0 ? (
              <div className="small text-secondary">Chưa đăng ký.</div>
            ) : (
              <table className="table table-sm small mb-1">
                <thead><tr><th>#</th><th>Sản phẩm</th><th>Trọng số</th><th>Điểm</th><th>Minh chứng</th></tr></thead>
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
              </table>
            )}
            {record.tasksApprovedAt && (
              <div className="small text-secondary">Duyệt bởi {record.tasksApprovedByName} lúc {formatDateTime(record.tasksApprovedAt)}{record.tasksApprovalComment ? ` — ${record.tasksApprovalComment}` : ""}</div>
            )}
          </div>
        )}

        <div>
          <h3 className="small fw-bold text-secondary">Tự chấm (Mẫu 09{record.selfScoreForm === "09B" ? "B" : "A"})</h3>
          <div className="row g-2">
            <Field label="Tiêu chí chung" value={record.generalCriteriaScore} />
            <Field label="Kết quả nhiệm vụ" value={record.tasksScore} />
            <Field label="Tổng tự chấm" value={record.totalSelfScore} />
            <Field label="Mức tự đề xuất" value={gradeLabel(record.selfProposedGrade)} />
            <Field label="Nộp lúc" value={record.selfScoredAt ? formatDateTime(record.selfScoredAt) : null} />
            {record.axisScores && <Field label="Điểm 6 trục" value={record.axisScores.join(" · ")} />}
          </div>
        </div>

        {enabled("B2_CELL_CONFIRM") && (
          <div>
            <h3 className="small fw-bold text-secondary">Xác nhận của Chi bộ<ModeNote record={record} step="B2_CELL_CONFIRM" /></h3>
            <div className="row g-2">
              <Field label="Người xác nhận" value={record.cellConfirmedByName} />
              <Field label="Thời điểm" value={record.cellConfirmedAt ? formatDateTime(record.cellConfirmedAt) : null} />
              <Field label="Ý kiến" value={record.partyCellComment} />
            </div>
          </div>
        )}

        {enabled("B3A_COLLECTIVE") && (
          <div>
            <h3 className="small fw-bold text-secondary">Đề xuất của tập thể lãnh đạo<ModeNote record={record} step="B3A_COLLECTIVE" /></h3>
            <div className="row g-2">
              <Field label="Mức đề xuất" value={gradeLabel(record.collectiveProposedGrade)} />
              <Field label="Ghi nhận bởi" value={record.collectiveRecordedByName} />
              <Field label="Nhận xét" value={record.collectiveComment} />
            </div>
          </div>
        )}

        {enabled("B3B_APPRAISAL") && (
          <div>
            <h3 className="small fw-bold text-secondary">Thẩm định<ModeNote record={record} step="B3B_APPRAISAL" /></h3>
            <div className="row g-2">
              <Field label="Điểm thẩm định" value={record.appraisalScore} />
              <Field label="Mức đề xuất" value={gradeLabel(record.appraisalProposedGrade)} />
              <Field label="Người / cơ quan thẩm định" value={record.appraisedByName} />
              <Field label="Ý kiến" value={record.appraisalComment} />
            </div>
          </div>
        )}

        {enabled("B3C_DIRECTOR") && (
          <div>
            <h3 className="small fw-bold text-secondary">Cấp trực tiếp sử dụng / lãnh đạo đơn vị đề xuất<ModeNote record={record} step="B3C_DIRECTOR" /></h3>
            <div className="row g-2">
              <Field label="Mức đề xuất" value={gradeLabel(record.directorProposedGrade)} />
              <Field label="Người / cơ quan nhận xét" value={record.directorReviewedByName} />
              <Field label="Nhận xét" value={record.directorComment} />
            </div>
          </div>
        )}

        <div>
          <h3 className="small fw-bold text-secondary">Quyết định và công bố<ModeNote record={record} step="B4_DECISION" /></h3>
          <div className="row g-2">
            <Field label="Mức xếp loại" value={gradeLabel(record.finalGrade)} />
            <Field label="Điểm chính thức" value={record.finalGrade && record.finalGrade !== "ChuaXepLoai" ? record.finalScore : null} />
            <Field label="Văn bản" value={record.decisionDocumentNumber ? `${record.decisionDocumentNumber} (${formatDate(record.decisionDocumentDate)})` : null} />
            <Field label="Cơ quan quyết định" value={record.decisionAuthorityName} />
            <Field label="Công bố" value={record.publishedAt ? `${record.publishedByName} · ${formatDateTime(record.publishedAt)}` : null} />
          </div>
        </div>
      </div>
    </section>
  );
}
