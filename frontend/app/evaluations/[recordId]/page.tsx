"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { GradeBadge } from "@/components/common/StatusBadge";
import { useToast } from "@/contexts/ToastContext";
import { useAuth } from "@/contexts/AuthContext";
import { RecordProgress, formatShortDateTime } from "@/components/evaluations/RecordProgress";
import { RecordActionPanel } from "@/components/evaluations/RecordActionPanel";
import { ReasonDialog } from "@/components/evaluations/ReasonDialog";
import { AppealPanel } from "@/components/evaluations/AppealPanel";
import { ImprovementPlanPanel } from "@/components/evaluations/ImprovementPlanPanel";
import { EvaluationPdfModal } from "@/components/evaluations/EvaluationPdfModal";
import { RecordFormsPanel } from "@/components/evaluations/RecordFormsPanel";
import { RecordHistory } from "@/components/evaluations/RecordHistory";
import { TaskChangeMark, TaskChangeRequests } from "@/components/evaluations/TaskChangeRequests";
import { CrosscheckPanel } from "@/components/evaluations/CrosscheckPanel";
import { RecordOpinionPanel } from "@/components/evaluations/RecordOpinionPanel";
import { CollapsibleSection, revealSection } from "@/components/evaluations/CollapsibleSection";
import { AxisSummaryView, SelfAssessmentView } from "@/components/evaluations/IndividualFormInputs";
import { loadCriteria, useCriteria } from "@/components/evaluations/useCriteria";
import { BackButton } from "@/components/common/BackButton";
import { initialsOf } from "@/components/dashboard/taskUtils";
import styles from "@/components/evaluations/record.module.css";
import { attachmentService } from "@/services/attachmentService";
import { EvidenceFiles, evidenceSlot, useRecordEvidence } from "@/components/evaluations/EvidenceFiles";
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
import { errorMessage } from "@/components/admin/adminUtils";

/** Cột trái: tiến trình, khu thao tác (theo `actions`) và dữ liệu từng bước; cột phải: biểu mẫu, lịch sử, thông tin hồ sơ. */
export default function EvaluationRecordPage() {
  const params = useParams<{ recordId: string }>();
  const recordId = params?.recordId;
  const { toast, confirm } = useToast();
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
  const [requestForms, setRequestForms] = useState<RecordFormDto[]>([]);
  // Kiến nghị / kế hoạch đang chờ chính người xem: đưa lên ngay dưới tiến trình.
  const [appealPending, setAppealPending] = useState(false);
  const [planPending, setPlanPending] = useState(false);

  const load = useCallback(async () => {
    if (!recordId) return;
    setLoadError(null);
    try {
      const [loadedRecord, loadedActions, loadedHistory, loadedForms, loadedRequestForms] = await Promise.all([
        evaluationService.getRecordById(recordId),
        evaluationService.getRecordActions(recordId),
        evaluationService.getRecordHistory(recordId),
        evaluationService.getRecordForms(recordId).catch(() => [] as RecordFormDto[]),
        evaluationService.getRecordRequestForms(recordId).catch(() => [] as RecordFormDto[]),
      ]);
      // Chờ bộ tiêu chí để trang hiện đủ một lần, không dựng lại bố cục.
      await loadCriteria(loadedRecord.periodId);
      setRecord(loadedRecord);
      setActions(loadedActions.actions);
      setHistory(loadedHistory);
      setForms(loadedForms);
      setRequestForms(loadedRequestForms);
      setConflict(null);
    } catch (error: unknown) {
      setLoadError(errorMessage(error, "Tải hồ sơ không thành công."));
    } finally {
      setLoading(false);
    }
  }, [recordId]);

  useEffect(() => {
    load();
  }, [load]);

  // Người khác đã cập nhật / hồ sơ đã sang bước khác → đề nghị tải lại.
  useEffect(() => {
    const handler = (event: Event) => {
      const detail = (event as CustomEvent<EvaluationConflictDetail>).detail;
      setConflict(detail?.message || "Dữ liệu đã thay đổi. Hãy tải lại.");
    };
    window.addEventListener(EVALUATION_CONFLICT_EVENT, handler);
    return () => window.removeEventListener(EVALUATION_CONFLICT_EVENT, handler);
  }, []);

  // Lỗi 409 hiện ở dải đầu trang, trong khi người dùng đang ở biểu mẫu phía dưới: đưa dải vào tầm nhìn.
  useEffect(() => {
    if (conflict) document.getElementById(CONFLICT_ANCHOR)?.scrollIntoView({ behavior: "smooth", block: "nearest" });
  }, [conflict]);

  const perform = async (action: WorkflowActionCode, payload: Record<string, unknown>, step?: WorkflowStepCode) => {
    if (!record) return;
    const run = async () => {
      setBusy(true);
      try {
        await evaluationService.performAction(record.id, action, record.version ?? 0, payload, step);
        toast.success("Đã lưu thao tác trên hồ sơ.");
        setReasonAction(null);
        await load();
      } catch (error: unknown) {
        if (!isConcurrencyConflict(error)) toast.error(errorMessage(error, "Thao tác không thành công."));
      } finally {
        setBusy(false);
      }
    };
    const confirmation = ACTION_CONFIRMATIONS[action];
    if (confirmation) {
      const danger = DANGER_ACTIONS.has(action);
      confirm({ title: "Xác nhận thao tác", message: confirmation, confirmText: danger ? "Thu hồi" : "Đồng ý", isDanger: danger, onConfirm: run });
      return;
    }
    await run();
  };

  // Bước đang làm mà người xem có thao tác → tới khu thao tác; còn lại → mở và cuộn tới khối nội dung của bước.
  const selectStep = (item: RecordStepProgressDto) => {
    if (item.state === "current" && actions.some((a) => a.step === item.step)) {
      goToActions();
      return;
    }
    revealSection(STEP_SECTION[item.step]);
  };

  // Hành động bị cổng khóa vẫn có trong danh sách: chỉ hiện lý do máy chủ trả về, không tự suy luật.
  const blockedActions = actions.filter((a) => !!a.blockedBy && !!a.blockedReason);
  const gateWarnings = Array.from(new Set(actions.flatMap((a) => a.warnings ?? [])));

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
        <div><BackButton /></div>
        <div className={styles.head}>
          <div className={`initials ${styles.bigInitials}`} aria-hidden="true">{initialsOf(record.fullName)}</div>
          <div className={styles.headMain}>
            <div className={styles.titleRow}>
              <h1>{record.fullName}</h1>
              {record.status === "Published" && hasGrade(record.finalGrade) && <GradeBadge grade={record.finalGrade} />}
            </div>
            {(record.positionTitle || record.departmentName) && (
              <p className={styles.lede}>{[record.positionTitle, record.departmentName].filter(Boolean).join(" · ")}</p>
            )}
          </div>
        </div>

        {conflict && (
          <div id={CONFLICT_ANCHOR} className="alert alert-warning d-flex flex-wrap justify-content-between align-items-center gap-2 mb-0" role="alert">
            <span>{conflict}</span>
            <button type="button" className="btn btn-outline-secondary btn-sm" onClick={() => load()}>
              <i className="bi bi-arrow-clockwise me-1" aria-hidden="true" />Tải lại
            </button>
          </div>
        )}
        {record.returnReason && (
          <div className="alert alert-danger mb-0">
            <i className="bi bi-arrow-return-left me-2" aria-hidden="true" /><strong>Bị trả lại:</strong> {record.returnReason}
          </div>
        )}
        {record.unlockedUntil && record.periodStatus === "Locked" && (
          <div className={`alert ${record.unlockActive ? "alert-info" : "alert-warning"} mb-0`}>
            {record.unlockActive
              ? `Hồ sơ được mở riêng đến ${formatDateTime(record.unlockedUntil)}.`
              : `Thời hạn mở riêng hồ sơ đã hết lúc ${formatDateTime(record.unlockedUntil)}.`}
          </div>
        )}

        {blockedActions.map((a) => (
          <div key={a.action + a.step} className="alert alert-warning mb-0" role="alert">
            <i className="bi bi-lock me-2" aria-hidden="true" /><strong>{a.label}:</strong> {a.blockedReason}
          </div>
        ))}
        {gateWarnings.length > 0 && (
          <div className="alert alert-info mb-0" role="status">
            {gateWarnings.map((w) => <div key={w}>{w}</div>)}
          </div>
        )}

        <section className={styles.block} aria-label="Tiến trình hồ sơ">
          <RecordProgress progress={record.progress} onSelect={selectStep} />
        </section>

        <div className={styles.cols}>
          <div className={styles.stack}>
            {orderBlocks([
              {
                key: "actions",
                // Thao tác chỉ còn trả lại / mở lại (không phải việc chính) thì nhường chỗ cho việc đang chờ.
                rank: actions.some((a) => !a.requiresReason) ? 0 : 2,
                node: actions.length > 0 && (
                  <div id={ACTIONS_ANCHOR} className={styles.actionsAnchor} tabIndex={-1}>
                    <RecordActionPanel record={record} actions={actions} busy={busy} onSubmit={perform} onReason={setReasonAction} />
                  </div>
                ),
              },
              { key: "appeal", rank: appealPending ? 1 : 4, node: <AppealPanel record={record} onRecordChanged={load} onPendingChange={setAppealPending} /> },
              { key: "plan", rank: planPending ? 1 : 5, node: <ImprovementPlanPanel record={record} onPendingChange={setPlanPending} /> },
              { key: "data", rank: 3, node: <RecordData record={record} reviewing={actions.length > 0} /> },
              { key: "changes", rank: 3, node: <TaskChangeRequests record={record} onRecordChanged={load} /> },
              { key: "opinions", rank: 3, node: <RecordOpinionPanel record={record} /> },
              { key: "crosschecks", rank: 3, node: <CrosscheckPanel record={record} onRecordChanged={load} /> },
            ])}
          </div>
          <div className={`${styles.stack} ${styles.side}`}>
            <RecordFormsPanel
              recordId={record.id}
              fullName={record.fullName}
              forms={forms}
              requestForms={requestForms}
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
            : reasonAction?.action === "ExtendUnlock"
              ? "Gia hạn thời gian chủ hồ sơ và các bước trước thẩm định được thao tác trên hồ sơ này."
              : "Hồ sơ sẽ quay về chủ hồ sơ để sửa."
        }
        confirmText={reasonAction?.action === "Reopen" ? "Mở lại hồ sơ" : reasonAction?.action === "ExtendUnlock" ? "Gia hạn" : "Trả lại"}
        targetSteps={reasonAction?.targetSteps}
        unlock={reasonAction}
        busy={busy}
        onCancel={() => setReasonAction(null)}
        onConfirm={(reason, targetStep?: WorkflowStepCode, unlockUntil?: string) =>
          reasonAction &&
          perform(reasonAction.action, reasonAction.action === "Reopen" ? { reason, targetStep, unlockUntil } : { reason, unlockUntil })
        }
      />

      {pdf && <EvaluationPdfModal isOpen onClose={() => setPdf(null)} record={record} forms={forms} initialCode={pdf} />}
    </div>
  );
}

/** Sắp khối cột trái theo hạng (ổn định); key cố định để khối đổi chỗ không bị dựng lại. */
function orderBlocks(blocks: { key: string; rank: number; node: React.ReactNode }[]) {
  return blocks
    .map((b, i) => ({ ...b, i }))
    .sort((a, b) => a.rank - b.rank || a.i - b.i)
    .map((b) => <React.Fragment key={b.key}>{b.node}</React.Fragment>);
}

/** Thao tác làm lùi hồ sơ: hộp xác nhận kiểu nguy hiểm. */
const DANGER_ACTIONS = new Set<WorkflowActionCode>(["RevokeTaskApproval"]);

const ACTION_CONFIRMATIONS: Partial<Record<WorkflowActionCode, string>> = {
  SubmitTasks: "Nộp danh mục sản phẩm? Sau khi nộp, danh mục sẽ chờ duyệt.",
  ApproveTasks: "Duyệt danh mục sản phẩm?",
  RevokeTaskApproval: "Thu hồi duyệt? Hồ sơ sẽ quay lại bước đăng ký sản phẩm.",
  ConfirmByCell: "Xác nhận phiếu tự chấm?",
  RecordCollectiveProposal: "Ghi nhận đề xuất của tập thể?",
  Appraise: "Ghi nhận kết quả thẩm định?",
  DirectorReview: "Ghi nhận nhận xét và đề xuất?",
  RecordDecision: "Ghi nhận quyết định xếp loại?",
  Publish: "Công bố và khóa kết quả? Sau đó chỉ có thể sửa bằng chức năng mở lại.",
  RecordExternal: "Ghi nhận kết quả của cấp trên?",
};

/** Id khu thao tác và khối nội dung của từng bước (cuộn tới từ dòng trạng thái / thanh tiến trình). */
const ACTIONS_ANCHOR = "record-actions";
const CONFLICT_ANCHOR = "record-conflict";
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

/** Thông tin hồ sơ: kỳ, Chi bộ, thẩm quyền, diện quyết định (chức vụ, phòng đã ở đầu trang). */
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
            ["Chi bộ", record.partyCellName],
            ["Thẩm quyền", record.approvalAuthority === "CapTren" ? "Cấp trên quyết định" : "Đảng ủy cơ sở quyết định"],
            ["Diện quyết định", record.workflowProfileName || record.workflowProfileCode],
          ]}
        />
      </div>
    </section>
  );
}

type FieldItem = [label: string, value: React.ReactNode];

const isEmpty = (value: React.ReactNode) => value === undefined || value === null || value === "" || value === false;

/** Danh sách trường chỉ đọc: bỏ trường chưa có giá trị; không còn trường nào thì báo chưa có dữ liệu. */
function Fields({ items, empty = "Chưa có dữ liệu." }: { items: FieldItem[]; empty?: string }) {
  const shown = items.filter(([, value]) => !isEmpty(value));
  if (shown.length === 0) return <div className={styles.small}>{empty}</div>;
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

/** Số văn bản và ngày (một trong hai có thể để trống). */
const documentText = (number?: string | null, date?: string | null): string | null => {
  if (number) return date ? `${number} (${formatDate(date)})` : number;
  return date ? `Ngày ${formatDate(date)}` : null;
};

/** Mô tả ngắn bước đã xong cho title của nhãn khối gộp. */
const doneText = (p: RecordStepProgressDto) => `${p.name}: ${[p.doneByName, formatShortDateTime(p.doneAt)].filter(Boolean).join(" ")}`;

/** Dữ liệu đã lưu của các bước (chỉ đọc), mỗi bước một khối; bước chưa có dữ liệu và bước không áp dụng ẩn. */
function RecordData({ record, reviewing }: { record: EvaluationRecordDto; reviewing: boolean }) {
  const { toast } = useToast();
  const { criteria } = useCriteria(record.periodId);
  const evidence = useRecordEvidence(record.id, record.version);
  const { user } = useAuth();
  const isOwner = !!user && user.id === record.memberId;
  /** Thêm minh chứng: theo cờ máy chủ trả về (quyền + giai đoạn hồ sơ). */
  const canAddEvidence = !!evidence.rights?.canAdd;
  const stepOf = (step: WorkflowStepCode) => record.progress.find((p) => p.step === step);
  const enabled = (step: WorkflowStepCode) => stepOf(step)?.enabled ?? true;
  /** Các bước áp dụng của một khối (khối "Danh mục" gồm B1 đăng ký + duyệt; "Quyết định và công bố" gồm B4 + B5). */
  const itemsOf = (steps: WorkflowStepCode[]) =>
    steps.map(stepOf).filter((p): p is RecordStepProgressDto => !!p && p.state !== "skipped");
  const isCurrent = (steps: WorkflowStepCode[]) => itemsOf(steps).some((p) => p.state === "current");
  /**
   * Chỉ hiện khối đã có dữ liệu (kể cả dữ liệu cũ sau khi mở lại) hoặc bước nội bộ đã xong;
   * bước đang chờ chưa có gì để xem — tiến trình đã nói đang chờ ai, việc nhập nằm ở khu thao tác.
   */
  const visible = (steps: WorkflowStepCode[], hasData: boolean) => {
    const items = itemsOf(steps);
    if (items.length === 0) return steps.every((s) => !stepOf(s)) || hasData;
    return hasData || items.some((p) => p.state === "done" && p.mode !== "External");
  };
  /**
   * Nhãn ở tiêu đề khối: ✓ ngày · người thực hiện, hoặc "Chưa tới" khi còn dữ liệu cũ.
   * Khối gộp nhiều bước chỉ ghi ngày; người của từng bước ghi trong thân khối (stepDoneItems).
   * Bước cấp trên không gắn nhãn riêng: tiến trình đã đánh dấu, tên cơ quan nằm ở nhãn này.
   */
  const tag = (steps: WorkflowStepCode[]) => {
    const items = itemsOf(steps);
    if (items.length === 0 || items.some((p) => p.state === "current")) return undefined;
    if (!items.every((p) => p.state === "done")) return <span className={`tag ${styles.tNeutral}`}>Chưa tới</span>;
    const last = items[items.length - 1];
    const day = formatShortDateTime(last.doneAt).slice(0, 5);
    const by = items.length === 1 ? last.doneByName : null;
    const text = items.length === 1 ? [day, by].filter(Boolean).join(" · ") : items.map(doneText).join(" · ");
    return (
      <span className={`tag ${styles.stepTagDone}`} title={text || undefined}>
        <i className="bi bi-check-lg" aria-hidden="true" />
        {day ? (
          <span className={styles.stepTagText}>
            <span className="num">{day}</span>
            {by && <span className={styles.hideNarrow}> · {by}</span>}
          </span>
        ) : "Đã xong"}
      </span>
    );
  };
  const externalOf = (step: WorkflowStepCode) => record.externalResults?.find((r) => r.step === step);
  /** Dòng "Quyết định: … · Công bố: …" trong thân khối gộp nhiều bước; bước cấp trên ghi người ghi nhận kết quả. */
  const stepDoneItems = (steps: WorkflowStepCode[], labels: Partial<Record<WorkflowStepCode, string>>): FieldItem[] =>
    itemsOf(steps)
      .filter((p) => p.state === "done" && labels[p.step])
      .map((p) => {
        const ext = p.mode === "External" ? externalOf(p.step) : undefined;
        const who = ext ? [ext.recordedByName, formatDateTime(ext.recordedAt, "")] : [p.doneByName, formatDateTime(p.doneAt, "")];
        return [labels[p.step] as string, who.filter(Boolean).join(" · ")];
      });
  /** Phần riêng của kết quả cấp trên (văn bản, tệp, người ghi nhận); mức, ý kiến đã nằm trong trường của bước. */
  const externalItems = (step: WorkflowStepCode, { withDocument = true, withRecorder = true } = {}): FieldItem[] => {
    const result = externalOf(step);
    if (!result) return [];
    const file = result.attachmentId && (
      <button
        type="button"
        className={styles.linkBtn}
        onClick={() => {
          attachmentService.downloadById(result.attachmentId as string)
            .catch((error: unknown) => toast.error(errorMessage(error, "Tải văn bản đính kèm không thành công.")));
        }}
      >
        <i className="bi bi-paperclip me-1" aria-hidden="true" />Tệp đính kèm
      </button>
    );
    const doc = withDocument ? documentText(result.documentNumber, result.documentDate) : null;
    return [
      [withDocument ? "Văn bản" : "Văn bản đính kèm", doc || file ? <>{doc}{doc && file ? " · " : null}{file}</> : null],
      ["Ghi nhận", withRecorder ? [result.recordedByName, formatDateTime(result.recordedAt, "")].filter(Boolean).join(" · ") : null],
    ];
  };
  // Người đang xử lý bước: mở sẵn kết quả của bước liền trước để xem trước khi quyết định.
  const lastDone = [...record.progress].reverse().find((p) => p.state === "done" && p.mode !== "External")?.step;
  const section = (steps: WorkflowStepCode[]) => ({
    anchorId: STEP_SECTION[steps[0]],
    tag: tag(steps),
    defaultOpen: isCurrent(steps) || (reviewing && !!lastDone && steps.includes(lastDone)),
    current: isCurrent(steps),
  });
  const axes = criteria?.content.axes ?? [];
  const groupName = (code: string) => criteria?.content.generalGroups.find((g) => g.code === code)?.name;
  const params = criteria?.content.parameters;
  const selfScored = !!record.selfScoredAt || stepOf("B2_SELF_SCORE")?.state === "done";
  const selfVisible = visible(["B2_SELF_SCORE"], !!record.selfScoredAt);
  const appraisedLevels = record.tasks.some((t) => t.appraisalLevel != null);

  return (
    <>
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
                      <TaskChangeMark task={t} />
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
          {record.weightFrame && (
            <div className={styles.subline}>
              Khung {record.weightFrame.code} · Tỷ lệ 4 mặt: {describeFrame(record.weightFrame)}{record.weightFrameAdjusted ? " (đã điều chỉnh)" : ""}
            </div>
          )}
          {record.tasksApprovedAt && (
            <div className={styles.subline}>
              Duyệt: {record.tasksApprovedByName} · {formatDateTime(record.tasksApprovedAt)}{record.tasksApprovalComment ? ` — ${record.tasksApprovalComment}` : ""}
            </div>
          )}
          {externalOf("B1_APPROVE") && <div className={styles.subline}><Fields items={externalItems("B1_APPROVE")} /></div>}
        </CollapsibleSection>
      )}

      {selfVisible && (
      <CollapsibleSection
        title="Phiếu tự chấm (Mẫu 09A)"
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
            {hasGrade(record.selfProposedGrade) && (
              <div className={`${styles.subline} d-flex flex-wrap align-items-center gap-2`}>
                Mức tự đề xuất <GradeBadge grade={record.selfProposedGrade} />
              </div>
            )}
            {record.selfGradeExplanation && (
              <div className={styles.subline}>Lý do đề xuất mức: {record.selfGradeExplanation}</div>
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
        {(evidence.files.length > 0 || (canAddEvidence && selfScored)) && (
          <details className={styles.sub2}>
            <summary>
              Minh chứng
              <span className={`tag ${styles.tNeutral} num ms-auto`}>{evidence.files.length} tệp</span>
            </summary>
            <div className={`${styles.sub2Body} d-flex flex-column gap-1`}>
            {isOwner && selfScored && evidence.rights?.reason && <div className="small text-secondary mb-1">{evidence.rights.reason}</div>}
            {[
              ...(criteria?.content.generalGroups ?? []).map((g) => ({ slot: evidenceSlot("group", g.code), label: `Nhóm ${g.code}` })),
            ]
              .filter(({ slot }) => canAddEvidence || evidence.files.some((f) => (f.formCode || "").toUpperCase() === slot))
              .map(({ slot, label }) => (
                <div key={slot} className="d-flex flex-wrap align-items-center gap-2 small">
                  <strong className={styles.evidenceLabel}>{label}</strong>
                  <EvidenceFiles recordId={record.id} slot={slot} label={label} files={evidence.files} rights={evidence.rights} onChanged={evidence.reload} hideLabel />
                </div>
              ))}
            </div>
          </details>
        )}
        {criteriaAppliesForm(criteria, "9D") && (
          <details className={styles.sub2}>
            <summary>Kết quả thực hiện nhiệm vụ (Mẫu 9D)</summary>
            <div className={styles.sub2Body}>
              <AxisSummaryView axes={axes} reports={record.axisReports} />
            </div>
          </details>
        )}
        {criteriaAppliesForm(criteria, "09C") && (
          <details className={styles.sub2}>
            <summary>Bản tự đánh giá (Mẫu 09C)</summary>
            <div className={styles.sub2Body}>
              <SelfAssessmentView sections={selfAssessmentSections(criteria)} value={record.selfAssessment}
                blocks={buildAxisTaskBlocks(axes, record.axisReports, criteria)} />
            </div>
          </details>
        )}
      </CollapsibleSection>
      )}

      {enabled("B2_CELL_CONFIRM") && visible(["B2_CELL_CONFIRM"], !!record.cellConfirmedAt) && (
        <CollapsibleSection title="Xác nhận của Chi bộ" {...section(["B2_CELL_CONFIRM"])}>
          <Fields
            items={[["Ý kiến", record.partyCellComment], ...externalItems("B2_CELL_CONFIRM")]}
            empty="Không có ý kiến."
          />
        </CollapsibleSection>
      )}

      {enabled("B3A_COLLECTIVE") && visible(["B3A_COLLECTIVE"], !!record.collectiveRecordedAt) && (
        <CollapsibleSection title={STEP_NAMES.B3A_COLLECTIVE} {...section(["B3A_COLLECTIVE"])}>
          <Fields
            items={[
              ["Mức đề xuất", hasGrade(record.collectiveProposedGrade) ? <GradeBadge grade={record.collectiveProposedGrade} /> : null],
              ["Nhận xét", record.collectiveComment],
              ...externalItems("B3A_COLLECTIVE"),
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
              ["Mức sau họp thống nhất", hasGrade(record.calibratedGrade) ? <GradeBadge grade={record.calibratedGrade} /> : null],
              ["Điểm sau họp thống nhất", hasGrade(record.calibratedGrade) ? record.calibratedScore : null],
              ["Biên bản họp thống nhất", record.calibrationSessionId && hasGrade(record.calibratedGrade)
                ? <Link href={`/quality-control/calibrations/${record.calibrationSessionId}`}>Xem biên bản{record.calibratedAt ? ` (${formatDate(record.calibratedAt)})` : ""}</Link>
                : null],
              ...externalItems("B3B_APPRAISAL"),
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
              ...externalItems("B3C_DIRECTOR"),
            ]}
          />
        </CollapsibleSection>
      )}

      {stepOf("B4_DECISION")?.state !== "skipped" && visible(DECISION_STEPS, !!record.decisionRecordedAt || !!record.publishedAt) && (
        <CollapsibleSection title="Quyết định và công bố" {...section(DECISION_STEPS)}>
          <Fields
            items={[
              ["Mức xếp loại", hasGrade(record.finalGrade) && record.status !== "Published" ? <GradeBadge grade={record.finalGrade} /> : null],
              ["Điểm chính thức", hasGrade(record.finalGrade) ? record.finalScore : null],
              ["Căn cứ quyết định mức", record.decisionExplanation],
              ["Văn bản", documentText(record.decisionDocumentNumber, record.decisionDocumentDate)],
              // Kết luận của cấp trên ở bước quyết định chưa có trường riêng trên hồ sơ.
              ["Nội dung kết luận", externalOf("B4_DECISION")?.comment],
              ["Cơ quan quyết định", record.decisionAuthorityName],
              ["Đề xuất về công tác cán bộ", record.cadreWorkProposal],
              ...externalItems("B4_DECISION", { withDocument: false, withRecorder: false }),
              ...stepDoneItems(DECISION_STEPS, { B4_DECISION: "Quyết định", B5_PUBLISH: "Công bố" }),
            ]}
          />
        </CollapsibleSection>
      )}
    </>
  );
}
