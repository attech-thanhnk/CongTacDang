"use client";

import React, { useEffect, useId, useMemo, useState } from "react";
import { flushSync } from "react-dom";
import {
  APPRAISAL_LEVELS,
  EvaluationMeetingDto,
  EvaluationRecordDto,
  GRADED_STEPS,
  GRADE_OPTIONS,
  RecordActionDto,
  STEP_NAMES,
  TASK_KIND_LABELS,
  TASK_ROLE_LABELS,
  TaskInputDto,
  VoteTallyDto,
  WorkflowActionCode,
  WorkflowStepCode,
  CONDITION_STATE_LABELS,
  ConditionResultDto,
  ConditionState,
  ExcellenceConditionsDto,
  GRADE_CAP_OPTIONS,
  evaluationService,
  exceedsGradeCap,
  gradeConditionWarnings,
  latestProposedGrade,
  votePassedGrade,
} from "@/services/evaluationService";
import { AttachmentList } from "@/components/attachments/AttachmentList";
import { attachmentService } from "@/services/attachmentService";
import { useCriteria } from "@/components/evaluations/useCriteria";
import { SelfScoreForm } from "@/components/evaluations/SelfScoreForm";
import { DateField } from "@/components/evaluations/DateField";
import { DecimalInput, decimalText, parseDecimal } from "@/components/evaluations/DecimalInput";
import { AppraisalItemsTable, appraisalItemsPayload, initialAppraisalItems } from "@/components/evaluations/AppraisalItemsTable";
import {
  CriteriaSnapshot,
  GRADE_LABELS,
  GeneralItemScore,
  RankedGrade,
  WeightFrame,
  describeFrame,
  exceedRatioOf,
  fmt,
  generalTotal,
  gradeProposalCheck,
  requiresExplanation,
  roundScore,
} from "@/services/criteriaService";
import { PublicOrganizationInfo, useOrganizationInfo } from "@/services/organizationSettingsService";
import styles from "./record.module.css";

interface Props {
  record: EvaluationRecordDto;
  actions: RecordActionDto[];
  busy: boolean;
  /** Gửi hành động hoàn thành bước kèm dữ liệu (`step`: bước của hành động — cần cho ghi nhận kết quả của cấp trên). */
  onSubmit: (action: WorkflowActionCode, payload: Record<string, unknown>, step?: WorkflowStepCode) => void;
  /** Mở hộp nhập lý do (trả lại / mở lại). */
  onReason: (action: RecordActionDto) => void;
}

const EMPTY_VOTES: VoteTallyDto = { votesExcellent: 0, votesGood: 0, votesSatisfactory: 0, votesUnsatisfactory: 0, votesNotRated: 0, invalidVotes: 0, notes: "" };

/** Chỉ hiện thao tác máy chủ trả trong `actions`, không tự suy luật quyền hay thứ tự bước. */
export function RecordActionPanel({ record, actions, busy, onSubmit, onReason }: Props) {
  const { criteria } = useCriteria(record.periodId);
  const [appraisal, setAppraisal] = useState<number | null>(null);
  const complete = actions.filter((a) => !a.requiresReason);
  const withReason = actions.filter((a) => a.requiresReason);

  if (actions.length === 0) return null;

  // Trả lại / mở lại: viền đỏ, tách sang trái chân biểu mẫu để không nhầm với nút chính; hộp lý do là bước xác nhận.
  const reasonButtons =
    withReason.length > 0 ? (
      <>
        {withReason.map((action) => {
          const mild = action.action === "ExtendUnlock" || action.action === "MarkNotRated" || action.action === "UnmarkNotRated";
          const icon = action.action === "ExtendUnlock" ? "fa-clock-rotate-left" : action.action === "MarkNotRated" ? "fa-ban"
            : action.action === "UnmarkNotRated" ? "fa-rotate-left" : "fa-reply";
          return (
            <button key={action.action} type="button" className={`btn ${mild ? "btn-outline-secondary" : "btn-outline-danger"}`} disabled={busy} onClick={() => onReason(action)}>
              <i className={`fa-solid ${icon} me-1`} aria-hidden="true" />
              {action.label}
            </button>
          );
        })}
      </>
    ) : null;

  // Nút trả lại / mở lại đặt chung chân với nút chính của thao tác đầu tiên (nếu biểu mẫu đó có chân).
  const first = complete[0];
  const reasonsInForm = !!reasonButtons && !!first && FOOTED_FORMS.has(first.action);

  if (complete.length === 0) {
    return (
      <section className={styles.task}>
        <div className={styles.blockH}>
          <h2>Thao tác</h2>
        </div>
        <div className={styles.foot}><span className={styles.footStart}>{reasonButtons}</span></div>
      </section>
    );
  }

  return (
    <>
      {complete.map((action, index) => {
        const isAppraise = action.action === "Appraise";
        const diff = isAppraise && appraisal != null ? appraisal - record.totalSelfScore : null;
        const diffBad = isAppraise && !!criteria && requiresExplanation(criteria.content, record.totalSelfScore, appraisal);
        return (
          <section key={action.action} className={styles.task} aria-label={action.label}>
            <div className={styles.blockH}>
              <h2>{action.label}</h2>
              {isAppraise && (
                <span className={styles.cmp}>
                  Tự chấm <b className="num">{fmt(record.totalSelfScore)}</b>
                  {diff != null && (
                    <>
                      {" "}· chênh lệch <b className={`num ${diffBad ? styles.bad : ""}`}>{diff > 0 ? "+" : ""}{fmt(diff)}</b>
                    </>
                  )}
                </span>
              )}
            </div>
            <div className={styles.taskBody}>
              <ActionForm
                record={record}
                criteria={criteria}
                action={action.action}
                step={action.step}
                // Hành động bị cổng khóa: vô hiệu hóa nút gửi, lý do hiện ở dải thông báo của hồ sơ.
                busy={busy || !!action.blockedBy}
                onSubmit={(...args) => { if (!action.blockedBy) onSubmit(...args); }}
                extra={index === 0 && reasonsInForm ? reasonButtons : null}
                onAppraisalChange={setAppraisal}
              />
            </div>
            {index === 0 && reasonButtons && !reasonsInForm && <div className={styles.foot}><span className={styles.footStart}>{reasonButtons}</span></div>}
          </section>
        );
      })}
    </>
  );
}

/** Thao tác có biểu mẫu gọn, nút chính nằm ở chân khu thao tác (cùng nút trả lại). */
const FOOTED_FORMS = new Set<WorkflowActionCode>([
  "RecordExternal",
  "ForwardTasks",
  "ReviewTasks",
  "ApproveTasks",
  "ConfirmByCell",
  "RecordCollectiveProposal",
  "Appraise",
  "DirectorReview",
  "RecordDecision",
  "Publish",
]);

/** Chân biểu mẫu tràn tới mép khu thao tác: nút trả lại bên trái, nút chính bên phải; `row` khi nằm trong lưới `.row.g-2`. */
function FormFoot({ extra, row = false, only = false, children }: { extra?: React.ReactNode; row?: boolean; only?: boolean; children: React.ReactNode }) {
  return (
    <div className={row ? `col-12 ${styles.formFootRow}` : only ? styles.formFootOnly : styles.formFoot}>
      {extra && <span className={styles.footStart}>{extra}</span>}
      {children}
    </div>
  );
}

interface FormProps {
  record: EvaluationRecordDto;
  criteria: CriteriaSnapshot | null;
  action: WorkflowActionCode;
  step: WorkflowStepCode;
  busy: boolean;
  onSubmit: (action: WorkflowActionCode, payload: Record<string, unknown>, step?: WorkflowStepCode) => void;
  /** Nút phụ đặt ở chân biểu mẫu (trả lại / mở lại). */
  extra?: React.ReactNode;
  onAppraisalChange?: (score: number | null) => void;
}

function ActionForm({ record, criteria, action, step, busy, onSubmit, extra, onAppraisalChange }: FormProps) {
  switch (action) {
    case "RecordExternal":
      return <ExternalResultForm record={record} step={step} busy={busy} extra={extra} onSubmit={(payload) => onSubmit(action, payload, step)} />;
    case "SubmitTasks":
      return <TasksForm record={record} criteria={criteria} busy={busy} onSubmit={(payload) => onSubmit(action, payload)} />;
    case "UpdateTasks":
      return <UpdateTasksForm record={record} criteria={criteria} busy={busy} onSubmit={(payload) => onSubmit(action, payload)} />;
    case "SubmitSelfScore":
      return <SelfScoreForm record={record} criteria={criteria} busy={busy} onSubmit={(payload) => onSubmit(action, payload)} />;
    case "ForwardTasks":
      return <CommentForm busy={busy} extra={extra} label="Ý kiến" submitText="Gửi" onSubmit={(payload) => onSubmit(action, payload)} />;
    case "ReviewTasks":
      return <CommentForm busy={busy} extra={extra} label="Ý kiến" submitText="Trình duyệt" onSubmit={(payload) => onSubmit(action, payload)} />;
    case "ApproveTasks":
      return <ApproveTasksForm record={record} busy={busy} extra={extra} onSubmit={(payload) => onSubmit(action, payload)} />;
    case "RevokeTaskApproval":
      return (
        <FormFoot only>
          <button type="button" className="btn btn-outline-danger" disabled={busy} onClick={() => onSubmit(action, {})}>
            <i className="fa-solid fa-rotate-left me-1" aria-hidden="true" />Thu hồi duyệt
          </button>
        </FormFoot>
      );
    case "ConfirmByCell":
      return <CommentForm busy={busy} extra={extra} label="Ý kiến" submitText="Xác nhận" onSubmit={(payload) => onSubmit(action, payload)} />;
    case "RecordCollectiveProposal":
      return <ProposalForm record={record} criteria={criteria} busy={busy} extra={extra} stage="B3A_COLLECTIVE" submitText="Ghi nhận đề xuất" onSubmit={(payload) => onSubmit(action, payload)} />;
    case "Appraise":
      return <AppraisalForm record={record} criteria={criteria} busy={busy} extra={extra} onScoreChange={onAppraisalChange} onSubmit={(payload) => onSubmit(action, payload)} />;
    case "DirectorReview":
      return <GradeCommentForm record={record} criteria={criteria} busy={busy} extra={extra} defaultGrade={record.reviewedGrade || record.appraisalProposedGrade} submitText="Ghi nhận nhận xét" onSubmit={(payload) => onSubmit(action, payload)} />;
    case "RecordDecision":
      return <ProposalForm record={record} criteria={criteria} busy={busy} extra={extra} stage="B4_DECISION" submitText="Ghi nhận quyết định" onSubmit={(payload) => onSubmit(action, payload)} />;
    case "Publish":
      return (
        <FormFoot only extra={extra}>
          <button type="button" className="btn btn-primary" disabled={busy} onClick={() => onSubmit(action, {})}><i className="fa-solid fa-bullhorn me-1" aria-hidden="true" />Công bố</button>
        </FormFoot>
      );
    default:
      return null;
  }
}

function CommentForm({ busy, label, submitText, extra, onSubmit }: { busy: boolean; label: string; submitText: string; extra?: React.ReactNode; onSubmit: (p: Record<string, unknown>) => void }) {
  const [comment, setComment] = useState("");
  const id = useId();
  return (
    <form onSubmit={(e) => { e.preventDefault(); onSubmit({ comment }); }}>
      <label className="form-label small" htmlFor={id}>{label}</label>
      <textarea id={id} className="form-control form-control-sm" rows={2} maxLength={4000} value={comment} onChange={(e) => setComment(e.target.value)} />
      <FormFoot extra={extra}>
        <button type="submit" className="btn btn-primary" disabled={busy}>{submitText}</button>
      </FormFoot>
    </form>
  );
}

const FRAME_PARTS = [
  { key: "a", label: "A — Khối lượng" },
  { key: "b", label: "B — Chất lượng, an toàn" },
  { key: "c", label: "C — Tiến độ" },
  { key: "d", label: "D — Tổ chức, hiệu quả" },
] as const;
type FramePercents = Record<(typeof FRAME_PARTS)[number]["key"], number>;
const toPercents = (f: WeightFrame): FramePercents => ({ a: Math.round(f.a * 10000) / 100, b: Math.round(f.b * 10000) / 100, c: Math.round(f.c * 10000) / 100, d: Math.round(f.d * 10000) / 100 });

/** Duyệt danh mục sản phẩm: ý kiến + điều chỉnh tỷ lệ A-B-C-D trong biên độ của bộ tiêu chí (mặc định giữ nguyên). */
function ApproveTasksForm({ record, busy, extra, onSubmit }: { record: EvaluationRecordDto; busy: boolean; extra?: React.ReactNode; onSubmit: (p: Record<string, unknown>) => void }) {
  const [comment, setComment] = useState("");
  const id = useId();
  const base = record.baseWeightFrame ?? null;
  const limit = record.frameAdjustLimit ?? 0;
  const current = useMemo(() => toPercents(record.weightFrame ?? base ?? { code: "", name: "", a: 0, b: 0, c: 0, d: 0 }), [record.weightFrame, base]);
  const [values, setValues] = useState<FramePercents>(current);
  const canAdjust = !!base && limit > 0;
  const basePercents = base ? toPercents(base) : null;

  const sum = values.a + values.b + values.c + values.d;
  const errors: string[] = [];
  if (canAdjust && basePercents) {
    for (const part of FRAME_PARTS) {
      const v = values[part.key];
      if (!Number.isFinite(v) || v < 0) errors.push(`Tỷ lệ ${part.key.toUpperCase()} không hợp lệ.`);
      else if (Math.abs(v - basePercents[part.key]) > limit + 1e-6)
        errors.push(`Tỷ lệ ${part.key.toUpperCase()} chỉ được từ ${fmt(Math.max(0, basePercents[part.key] - limit))}% đến ${fmt(basePercents[part.key] + limit)}%.`);
    }
    if (Math.abs(sum - 100) > 0.01) errors.push(`Tổng tỷ lệ phải bằng 100% (đang là ${fmt(sum)}%).`);
  }
  const changed = FRAME_PARTS.some((part) => Math.abs(values[part.key] - current[part.key]) > 1e-6);

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    if (canAdjust && changed && errors.length > 0) return;
    onSubmit(canAdjust && changed ? { comment, frameAdjustment: values } : { comment });
  };

  return (
    <form onSubmit={submit}>
      {canAdjust && basePercents && (
        <details className="border rounded-2 px-2 py-1 mb-2 small" open={record.weightFrameAdjusted || changed}>
          <summary className="fw-semibold">
            Tỷ lệ A-B-C-D: {describeFrame({ a: values.a / 100, b: values.b / 100, c: values.c / 100, d: values.d / 100 })}
            {FRAME_PARTS.some((p) => Math.abs(values[p.key] - basePercents[p.key]) > 1e-6) && <span className="text-primary"> (đã điều chỉnh)</span>}
          </summary>
          <div className="text-secondary my-1">
            Khung {base!.code}: {describeFrame(base!)}. Mỗi thành phần ±{fmt(limit)} điểm %, tổng 100%.
          </div>
          <div className="row g-2 align-items-end">
            {FRAME_PARTS.map((part) => (
              <div className="col-6 col-md-3" key={part.key}>
                <label className="form-label small mb-0" htmlFor={`${id}-${part.key}`}>{part.label} (%)</label>
                <input id={`${id}-${part.key}`} type="number" step={1} min={Math.max(0, basePercents[part.key] - limit)} max={basePercents[part.key] + limit}
                  className="form-control form-control-sm" value={Number.isFinite(values[part.key]) ? values[part.key] : ""}
                  onChange={(e) => setValues({ ...values, [part.key]: e.target.value === "" ? NaN : Number(e.target.value) })} />
              </div>
            ))}
          </div>
          <div className="d-flex justify-content-between align-items-center mt-1">
            <span className={Math.abs(sum - 100) > 0.01 ? "text-danger" : "text-success"}>Tổng: {fmt(sum)}%</span>
            <button type="button" className="btn btn-link btn-sm p-0" onClick={() => setValues(basePercents)}>Dùng tỷ lệ của khung</button>
          </div>
          {changed && errors.map((m) => <div key={m} className="text-danger">{m}</div>)}
        </details>
      )}
      <label className="form-label small" htmlFor={id}>Ý kiến</label>
      <textarea id={id} className="form-control form-control-sm" rows={2} maxLength={4000} value={comment} onChange={(e) => setComment(e.target.value)} />
      <FormFoot extra={extra}>
        <button type="submit" className="btn btn-primary" disabled={busy || (canAdjust && changed && errors.length > 0)}>Duyệt</button>
      </FormFoot>
    </form>
  );
}

function GradeSelect({ value, onChange, id }: { value: string; onChange: (v: string) => void; id: string }) {
  return (
    <select id={id} className="form-select form-select-sm" value={value} onChange={(e) => onChange(e.target.value)} required>
      <option value="">— Chọn mức —</option>
      {GRADE_OPTIONS.map((g) => <option key={g.value} value={g.value}>{g.label}</option>)}
    </select>
  );
}

const initialGrade = (value?: string) => (value && value !== "ChuaXepLoai" ? value : "");

/** Mức chọn khác mức theo điểm (cùng quy tắc máy chủ); `required` = bộ tiêu chí bắt buộc nêu lý do. */
function proposalCheck(record: EvaluationRecordDto, criteria: CriteriaSnapshot | null, grade: string, score: number | null | undefined) {
  if (!criteria || !grade || score == null || Number.isNaN(score)) return null;
  return gradeProposalCheck(criteria.content, grade, score, exceedRatioOf(record.tasks));
}

const reasonMissing = (direction: number) =>
  direction > 0 ? "Mức đề xuất cao hơn mức theo điểm: hãy nêu lý do." : "Mức đề xuất thấp hơn mức theo điểm: hãy nêu căn cứ.";

/** Ô lý do / nhận xét; lỗi bắt buộc hiện ngay dưới ô. */
function ReasonField({ id, label, value, onChange, required, error, rows = 2 }: {
  id: string; label: string; value: string; onChange: (v: string) => void; required?: boolean; error?: string | null; rows?: number;
}) {
  return (
    <div className="col-12">
      <label className="form-label small" htmlFor={id}>{label} {required && <span className="text-danger">*</span>}</label>
      <textarea id={id} className={`form-control form-control-sm ${error ? "is-invalid" : ""}`} rows={rows} maxLength={4000} value={value} onChange={(e) => onChange(e.target.value)} />
      {error && <div className="invalid-feedback">{error}</div>}
    </div>
  );
}

function GradeCommentForm({ record, criteria, busy, defaultGrade, submitText, extra, onSubmit }: { record: EvaluationRecordDto; criteria: CriteriaSnapshot | null; busy: boolean; defaultGrade?: string; submitText: string; extra?: React.ReactNode; onSubmit: (p: Record<string, unknown>) => void }) {
  const [grade, setGrade] = useState(initialGrade(defaultGrade));
  const [comment, setComment] = useState("");
  const [error, setError] = useState<string | null>(null);
  // Điểm thẩm định, thay bằng điểm sau họp thống nhất nếu có (cùng nguồn với máy chủ).
  const check = proposalCheck(record, criteria, grade, record.reviewedScore ?? record.appraisalScore ?? record.totalSelfScore);
  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    if (check?.required && !comment.trim()) {
      setError(reasonMissing(check.direction));
      document.getElementById("gc-comment")?.focus();
      return;
    }
    onSubmit({ proposedGrade: grade, comment });
  };
  return (
    <form onSubmit={submit} className="row g-2">
      <div className="col-md-6"><label className="form-label small" htmlFor="gc-grade">Mức đề xuất</label><GradeSelect id="gc-grade" value={grade} onChange={setGrade} /></div>
      <GradeWarningList warnings={[...recordGradeWarnings(record, grade), ...(check ? [check.message] : [])]} />
      <ReasonField id="gc-comment" label="Nhận xét" rows={3} value={comment} required={!!check?.required} error={error}
        onChange={(v) => { setComment(v); setError(null); }} />
      <FormFoot row extra={extra}><button type="submit" className="btn btn-primary" disabled={busy || !grade}>{submitText}</button></FormFoot>
    </form>
  );
}

/** Chênh lệch với tự chấm từ ngưỡng của bộ tiêu chí (hoặc làm đổi mức) → bắt buộc giải trình; máy chủ kiểm tra lại. */
function AppraisalForm({ record, criteria, busy, extra, onScoreChange, onSubmit }: { record: EvaluationRecordDto; criteria: CriteriaSnapshot | null; busy: boolean; extra?: React.ReactNode; onScoreChange?: (score: number | null) => void; onSubmit: (p: Record<string, unknown>) => void }) {
  // Việc đã loại trừ / chuyển tiếp không chấm.
  const scoredTasks = record.tasks.filter((t) => !t.changeStatus);
  const usesTasks = scoredTasks.length > 0;
  const [general, setGeneral] = useState<string>(decimalText(record.appraisalGeneralScore ?? record.generalCriteriaScore));
  const [tasksScore, setTasksScore] = useState<string>(decimalText(record.appraisalTasksScore ?? record.tasksScore));
  const [levels, setLevels] = useState<Record<string, number | null>>(() => Object.fromEntries(record.tasks.map((t) => [t.id, t.appraisalLevel ?? null])));
  const [taskComments, setTaskComments] = useState<Record<string, string>>(() => Object.fromEntries(record.tasks.map((t) => [t.id, t.appraisalComment ?? ""])));
  const [grade, setGrade] = useState(initialGrade(record.appraisalProposedGrade) || initialGrade(record.collectiveProposedGrade) || initialGrade(record.selfProposedGrade));
  const [comment, setComment] = useState(record.appraisalComment || "");
  const [explanation, setExplanation] = useState(record.appraisalExplanation || "");
  const content = criteria?.content;
  // Chấm từng tiêu chí con cần bộ tiêu chí của kỳ; mặc định theo cách đã lưu, chưa lưu thì theo nhóm.
  const [byItems, setByItems] = useState(!!record.appraisalGeneralScores);
  const [items, setItems] = useState<Record<string, GeneralItemScore>>(() =>
    content ? initialAppraisalItems(content, record.appraisalGeneralScores, record.generalScores) : {});
  useEffect(() => {
    if (content) setItems((prev) => (Object.keys(prev).length ? prev : initialAppraisalItems(content, record.appraisalGeneralScores, record.generalScores)));
  }, [content, record.appraisalGeneralScores, record.generalScores]);
  const itemMode = byItems && !!content;
  const generalValue = itemMode ? generalTotal(content, items) : parseDecimal(general);
  const tasksValue = parseDecimal(tasksScore);
  const p = content?.parameters;
  // Tổng tạm tính theo cùng quy tắc làm tròn của máy chủ (chung + nhiệm vụ).
  const appraisal = generalValue == null || tasksValue == null
    ? null
    : p
      ? roundScore(roundScore(generalValue, p.rounding.generalTotal) + roundScore(tasksValue, p.rounding.tasksTotal), p.rounding.total)
      : generalValue + tasksValue;
  const needsExplanation = !!criteria && requiresExplanation(criteria.content, record.totalSelfScore, appraisal);
  const rule = criteria
    ? `Bắt buộc khi chênh lệch từ ${fmt(criteria.content.parameters.explanationThreshold)} điểm${criteria.content.parameters.explanationOnGradeChange ? " hoặc đổi mức xếp loại" : ""}`
    : undefined;
  const leveledTasks = scoredTasks.map((t) => ({ ...t, appraisalLevel: levels[t.id] ?? null }));
  const missingLevels = usesTasks && leveledTasks.some((t) => t.appraisalLevel == null);
  const check = proposalCheck(record, criteria, grade, appraisal);
  const [cap, setCap] = useState(record.appraisalGradeCap || "");
  const [capReason, setCapReason] = useState(record.appraisalCapReason || "");
  const [capReasonError, setCapReasonError] = useState<string | null>(null);
  const [overrides, setOverrides] = useState<Record<string, boolean>>(() => ({ ...(record.appraisalConditionOverrides ?? {}) }));
  const [conditions, setConditions] = useState<ExcellenceConditionsDto | null>(null);
  useEffect(() => {
    let active = true;
    evaluationService.getExcellenceConditions(record.id)
      .then((c) => { if (active) setConditions(c); })
      .catch(() => { if (active) setConditions(null); });
    return () => { active = false; };
  }, [record.id]);
  const conditionRows = conditions ? currentConditions(conditions.computed, cap, capReason, leveledTasks) : [];
  const finalStates = conditionRows.map((c) => (c.code in overrides ? (overrides[c.code] ? "Met" : "NotMet") : c.state) as ConditionState);
  const failedConditions = conditionRows.filter((_, i) => finalStates[i] === "NotMet").map((c) => c.label);
  const warnings = [
    ...(usesTasks ? gradeConditionWarnings(leveledTasks, grade, record.excellentMinExceedRatio) : []),
    ...(check ? [check.message] : []),
    ...(exceedsGradeCap(grade, cap) ? [`Mức đề xuất cao hơn mức khống chế (tối đa "${GRADE_LABELS[cap as RankedGrade] ?? cap}").`] : []),
    ...(!cap && conditions?.hasSafetyDefect ? ["Phiếu đối soát an toàn - chất lượng (Mẫu 03) có mục khiếm khuyết: xem lại việc khống chế mức xếp loại."] : []),
    ...(grade === "HoanThanhXuatSac" && failedConditions.length > 0 ? [`Điều kiện Hoàn thành xuất sắc không đạt: ${failedConditions.join(", ")}.`] : []),
  ];
  const explanationRequired = needsExplanation || !!check?.required;
  const [explanationError, setExplanationError] = useState<string | null>(null);

  useEffect(() => {
    onScoreChange?.(appraisal);
  }, [appraisal, onScoreChange]);

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    if (explanationRequired && !explanation.trim()) {
      setExplanationError(check?.required ? reasonMissing(check.direction) : "Chênh lệch tự chấm – thẩm định cần giải trình, căn cứ.");
      document.getElementById("ap-explanation")?.focus();
      return;
    }
    if (cap && !capReason.trim()) {
      setCapReasonError("Hãy ghi lý do khống chế mức xếp loại.");
      document.getElementById("ap-cap-reason")?.focus();
      return;
    }
    onSubmit({
      gradeCap: cap || null,
      capReason: cap ? capReason.trim() : null,
      conditionOverrides: overrides,
      generalScore: itemMode ? null : generalValue,
      generalItemScores: itemMode ? appraisalItemsPayload(items) : null,
      tasksScore: tasksValue,
      proposedGrade: grade,
      comment,
      explanation: explanation.trim() || null,
      taskLevels: usesTasks
        ? scoredTasks.map((t) => ({ taskId: t.id, level: levels[t.id], comment: taskComments[t.id]?.trim() || null }))
        : null,
    });
  };

  return (
    <form onSubmit={submit} className="row g-2">
      {usesTasks && (
        <div className="col-12">
          <div className={styles.tw}><table className={styles.table}>
            {/* Loại, vượt chuẩn ghi dưới tên; mức và nhận xét chung một cột để vừa khung ở màn hình 1366px. */}
            <thead><tr><th>#</th><th style={{ minWidth: 160 }}>Sản phẩm</th><th>Tự chấm</th><th style={{ width: "45%", minWidth: 220 }}>Mức thẩm định, nhận xét</th></tr></thead>
            <tbody>
              {scoredTasks.map((t) => {
                const level = APPRAISAL_LEVELS.find((l) => l.value === levels[t.id]);
                const tags = [t.kind ? TASK_KIND_LABELS[t.kind] : null, t.isExceedStandard ? "Vượt chuẩn" : null].filter(Boolean);
                return (
                  <tr key={t.id}>
                    <td className="num">{t.taskOrder}</td>
                    <td>
                      {t.taskName}
                      {tags.length > 0 && <div className="small text-secondary">{tags.join(" · ")}</div>}
                    </td>
                    <td className="num">{t.weight > 0 ? `${fmt((t.selfScore / t.weight) * 100, 1)}%` : "—"}</td>
                    <td>
                      <div className="d-flex flex-column gap-1">
                        <select
                          aria-label={`Mức thẩm định: ${t.taskName}`}
                          className="form-select form-select-sm"
                          required
                          value={levels[t.id] ?? ""}
                          onChange={(e) => setLevels((prev) => ({ ...prev, [t.id]: e.target.value === "" ? null : Number(e.target.value) }))}
                        >
                          <option value="">— Chọn —</option>
                          {APPRAISAL_LEVELS.map((l) => <option key={l.value} value={l.value} title={l.hint}>{l.label}</option>)}
                        </select>
                        {level && <div className="small text-secondary">{level.hint}</div>}
                        <input
                          aria-label={`Nhận xét: ${t.taskName}`}
                          className="form-control form-control-sm"
                          placeholder="Nhận xét"
                          maxLength={1000}
                          value={taskComments[t.id] ?? ""}
                          onChange={(e) => setTaskComments((prev) => ({ ...prev, [t.id]: e.target.value }))}
                        />
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table></div>
        </div>
      )}
      {content && (
        <div className="col-12 d-flex flex-wrap gap-3 align-items-center small" role="radiogroup" aria-label="Cách chấm tiêu chí chung">
          <span className="text-secondary">Chấm tiêu chí chung:</span>
          <label className="form-check form-check-inline mb-0">
            <input type="radio" className="form-check-input" name="ap-mode" checked={!byItems} onChange={() => setByItems(false)} />
            <span className="form-check-label">Theo nhóm</span>
          </label>
          <label className="form-check form-check-inline mb-0">
            <input type="radio" className="form-check-input" name="ap-mode" checked={byItems} onChange={() => setByItems(true)} />
            <span className="form-check-label">Từng tiêu chí</span>
          </label>
        </div>
      )}
      {itemMode && content && (
        <div className="col-12">
          <AppraisalItemsTable content={content} selfScores={record.generalScores ?? {}} value={items} onChange={setItems} />
        </div>
      )}
      <div className="col-md-3">
        <label className="form-label small" htmlFor="ap-general">Tiêu chí chung</label>
        {itemMode
          ? <div id="ap-general" className="num fw-bold">{generalValue != null ? fmt(generalValue) : "—"}</div>
          : <DecimalInput id="ap-general" min={0} max={p?.generalMaxScore ?? 100} required className="form-control form-control-sm num" value={general} onChange={setGeneral} />}
      </div>
      <div className="col-md-3"><label className="form-label small" htmlFor="ap-tasks">Kết quả nhiệm vụ</label><DecimalInput id="ap-tasks" min={0} max={p?.totalTaskWeight ?? 100} required className="form-control form-control-sm num" value={tasksScore} onChange={setTasksScore} /></div>
      <div className="col-md-2"><span className="form-label small d-block">Tổng</span><b className="num">{appraisal != null ? fmt(appraisal) : "—"}</b></div>
      <div className="col-md-6"><label className="form-label small" htmlFor="ap-grade">Mức đề xuất</label><GradeSelect id="ap-grade" value={grade} onChange={setGrade} /></div>
      <div className="col-md-6">
        <label className="form-label small" htmlFor="ap-cap">Khống chế mức xếp loại</label>
        <select id="ap-cap" className="form-select form-select-sm" value={cap} onChange={(e) => { setCap(e.target.value); setCapReasonError(null); }}>
          <option value="">Không</option>
          {GRADE_CAP_OPTIONS.map((g) => <option key={g} value={g}>Có, tối đa {GRADE_LABELS[g as RankedGrade] ?? g}</option>)}
        </select>
      </div>
      {cap && (
        <div className="col-md-6">
          <label className="form-label small" htmlFor="ap-cap-reason">Lý do khống chế <span className="text-danger">*</span></label>
          <textarea id="ap-cap-reason" rows={1} maxLength={4000} className={`form-control form-control-sm ${capReasonError ? "is-invalid" : ""}`}
            value={capReason} onChange={(e) => { setCapReason(e.target.value); setCapReasonError(null); }} />
          {capReasonError && <div className="invalid-feedback">{capReasonError}</div>}
        </div>
      )}
      {conditionRows.length > 0 && (
        <div className="col-12">
          <div className="small fw-semibold mb-1">Điều kiện Hoàn thành xuất sắc</div>
          <div className={styles.tw}><table className={styles.table}>
            <thead><tr><th>Điều kiện</th><th>Hệ thống</th><th style={{ width: 150 }}>Kết luận</th></tr></thead>
            <tbody>
              {conditionRows.map((c, i) => (
                <tr key={c.code}>
                  <td>{c.label} {c.name}</td>
                  <td className="small"><b>{CONDITION_STATE_LABELS[c.state]}</b>: {c.reason}</td>
                  <td>
                    <select aria-label={`Kết luận điều kiện ${c.label}`} className="form-select form-select-sm"
                      value={c.code in overrides ? (overrides[c.code] ? "Met" : "NotMet") : ""}
                      onChange={(e) => setOverrides((prev) => {
                        const next = { ...prev };
                        if (e.target.value === "") delete next[c.code]; else next[c.code] = e.target.value === "Met";
                        return next;
                      })}>
                      <option value="">Theo hệ thống ({CONDITION_STATE_LABELS[c.state]})</option>
                      <option value="Met">Đạt</option>
                      <option value="NotMet">Không</option>
                    </select>
                    {finalStates[i] === "Unknown" && <div className="small text-secondary">Chưa xác định</div>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table></div>
        </div>
      )}
      <GradeWarningList warnings={warnings} />
      <div className="col-12"><label className="form-label small" htmlFor="ap-comment">Ý kiến thẩm định</label><textarea id="ap-comment" className="form-control form-control-sm" rows={3} maxLength={4000} value={comment} onChange={(e) => setComment(e.target.value)} /></div>
      <div className="col-12">
        <label className="form-label small" htmlFor="ap-explanation" title={rule}>
          Giải trình {explanationRequired && <span className="text-danger">*</span>}
        </label>
        <textarea id="ap-explanation" className={`form-control form-control-sm ${explanationError ? "is-invalid" : ""}`} rows={2} maxLength={4000} title={rule} value={explanation}
          onChange={(e) => { setExplanation(e.target.value); setExplanationError(null); }} />
        {explanationError && <div className="invalid-feedback">{explanationError}</div>}
      </div>
      <FormFoot row extra={extra}><button type="submit" className="btn btn-primary" disabled={busy || !grade || missingLevels || appraisal == null}>Ghi nhận thẩm định</button></FormFoot>
    </form>
  );
}

/** Điều kiện (v), (vi) tính lại theo lựa chọn đang sửa trên phiếu; các điều kiện khác giữ kết quả máy chủ. */
function currentConditions(
  computed: ConditionResultDto[], cap: string, capReason: string,
  tasks: { taskOrder: number; kind?: string | null; appraisalLevel?: number | null }[],
): ConditionResultDto[] {
  return computed.map((c) => {
    if (c.code === "V") {
      return cap
        ? { ...c, state: "NotMet", reason: `khống chế tối đa mức ${GRADE_LABELS[cap as RankedGrade] ?? cap}${capReason.trim() ? `: ${capReason.trim()}` : ""}` }
        : { ...c, state: "Met", reason: "không vi phạm" };
    }
    if (c.code === "VI") {
      const focus = tasks.filter((t) => t.kind === "TrongTam");
      const bad = focus.filter((t) => t.appraisalLevel === 2 || t.appraisalLevel === 3);
      if (bad.length > 0) return { ...c, state: "NotMet", reason: `nhiệm vụ trọng tâm ${bad.map((t) => `SP${t.taskOrder} Mức ${t.appraisalLevel}`).join(", ")}` };
      if (focus.some((t) => t.appraisalLevel == null)) return { ...c, state: "Unknown", reason: "chưa xếp mức đủ nhiệm vụ trọng tâm" };
      return { ...c, state: "Met", reason: "không có nhiệm vụ trọng tâm Mức 2, Mức 3" };
    }
    return c;
  });
}

/** Cảnh báo điều kiện xếp loại (chỉ cảnh báo, không chặn). */
function GradeWarningList({ warnings }: { warnings: string[] }) {
  if (warnings.length === 0) return null;
  return (
    <div className="col-12">
      <div className="alert alert-warning small py-2 mb-0" role="status">
        {warnings.map((w) => <div key={w}>{w}</div>)}
      </div>
    </div>
  );
}

/** Cảnh báo điều kiện xếp loại với mức đang chọn, theo mức thẩm định đã lưu của từng nhiệm vụ. */
function recordGradeWarnings(record: EvaluationRecordDto, grade: string): string[] {
  return gradeConditionWarnings(record.tasks.filter((t) => !t.changeStatus), grade, record.excellentMinExceedRatio);
}

/** Ghi nhận đề xuất tập thể hoặc Trưởng đơn vị (B3a) hoặc quyết định (B4); kết quả kiểm phiếu gắn với biên bản. */
function ProposalForm({ record, criteria, busy, stage, submitText, extra, onSubmit }: { record: EvaluationRecordDto; criteria: CriteriaSnapshot | null; busy: boolean; stage: "B3A_COLLECTIVE" | "B4_DECISION"; submitText: string; extra?: React.ReactNode; onSubmit: (p: Record<string, unknown>) => void }) {
  const isDecision = stage === "B4_DECISION";
  // Trưởng đơn vị đề xuất sau cuộc họp: được chọn mức khác kết quả kiểm phiếu nhưng phải ghi lý do.
  const unitLeader = !isDecision && record.unitLeaderProposal;
  const minutesMode = isDecision ? "Recorded" : record.collectiveMinutesMode ?? "Off";
  const minutesRequired = minutesMode === "Required";
  const showMeetings = isDecision || !unitLeader || minutesMode !== "Off";
  const [grade, setGrade] = useState(
    isDecision
      ? initialGrade(latestProposedGrade(record))
      : initialGrade(record.selfProposedGrade)
  );
  const [comment, setComment] = useState("");
  const [finalScore, setFinalScore] = useState("");
  const [explanation, setExplanation] = useState(record.decisionExplanation ?? "");
  const [reasonError, setReasonError] = useState<string | null>(null);
  const [documentNumber, setDocumentNumber] = useState("");
  const [documentDate, setDocumentDate] = useState("");
  const org = useOrganizationInfo();
  const [authorityName, setAuthorityName] = useState("");
  // Gợi ý cơ quan quyết định theo thông tin đơn vị; người ghi nhận sửa được.
  useEffect(() => {
    if (org) setAuthorityName((current) => current || (record.approvalAuthority === "CapTren" ? `Ban Thường vụ Đảng ủy ${org.parentCompanyName}` : `Đảng ủy ${org.companyName}`));
  }, [org, record.approvalAuthority]);
  const [cadreWorkProposal, setCadreWorkProposal] = useState(record.cadreWorkProposal || "");
  const [meetings, setMeetings] = useState<EvaluationMeetingDto[]>([]);
  const [meetingId, setMeetingId] = useState("");
  const [withVotes, setWithVotes] = useState(false);
  const [votes, setVotes] = useState<VoteTallyDto>(EMPTY_VOTES);
  const [minutesError, setMinutesError] = useState<string | null>(null);

  useEffect(() => {
    if (!showMeetings) return;
    evaluationService
      .getMeetings(record.periodId)
      .then((list) => {
        const usable = list.filter((m) => !m.stage || m.stage === stage);
        setMeetings(usable);
        // Biên bản đã có kết quả kiểm phiếu của hồ sơ → chọn sẵn.
        const own = usable.find((m) => m.voteSummaries?.some((v) => v.recordId === record.id));
        if (own) setMeetingId((current) => current || own.id);
      })
      .catch(() => setMeetings([]));
  }, [record.periodId, record.id, stage, showMeetings]);

  const withVotesEffective = minutesRequired || withVotes;
  const selectedMeeting = meetings.find((m) => m.id === meetingId);
  const totalVotes = votes.votesExcellent + votes.votesGood + votes.votesSatisfactory + votes.votesUnsatisfactory + votes.votesNotRated + votes.invalidVotes;
  // Kiểm phiếu đã có: mức của tập thể phải là mức đạt quá nửa số có mặt; Trưởng đơn vị được khác nếu ghi lý do (máy chủ kiểm lại).
  const voteResult = !isDecision && meetingId && withVotesEffective ? votePassedGrade(votes, selectedMeeting?.presentCount ?? 0) : null;
  const voteMismatch = !!voteResult && voteResult.grade !== grade;
  const leaderDiffers = unitLeader && voteMismatch;
  const scoreForCheck = isDecision ? (parseDecimal(finalScore) ?? record.reviewedScore ?? record.totalSelfScore) : record.totalSelfScore;
  const check = proposalCheck(record, criteria, grade, scoreForCheck);
  const reason = isDecision ? explanation : comment;

  // Chọn biên bản đã có kết quả kiểm phiếu của hồ sơ → điền sẵn số phiếu.
  useEffect(() => {
    const existing = selectedMeeting?.voteSummaries?.find((v) => v.recordId === record.id);
    if (!existing) return;
    setVotes({ ...EMPTY_VOTES, ...existing, notes: existing.notes ?? "" });
    setWithVotes(true);
  }, [selectedMeeting, record.id]);

  // Kết quả kiểm phiếu đổi → chọn sẵn mức được thông qua.
  const passedGrade = voteResult?.grade ?? null;
  useEffect(() => {
    if (passedGrade) setGrade(passedGrade);
  }, [passedGrade]);

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    if (check?.required && !reason.trim()) {
      setReasonError(reasonMissing(check.direction));
      document.getElementById(`${stage}-reason`)?.focus();
      return;
    }
    if (leaderDiffers && !reason.trim()) {
      setReasonError("Mức đề xuất khác kết quả kiểm phiếu: hãy ghi lý do.");
      document.getElementById(`${stage}-reason`)?.focus();
      return;
    }
    if (minutesRequired && (!meetingId || totalVotes === 0)) {
      setMinutesError("Chọn biên bản họp và nhập kết quả kiểm phiếu của hồ sơ này.");
      return;
    }
    const payload: Record<string, unknown> = showMeetings
      ? { meetingId: meetingId || null, votes: withVotesEffective && meetingId ? votes : null }
      : { meetingId: null, votes: null };
    if (isDecision) {
      Object.assign(payload, {
        finalGrade: grade,
        finalScore: parseDecimal(finalScore),
        documentNumber,
        documentDate: documentDate || null,
        authorityName,
        cadreWorkProposal: cadreWorkProposal.trim() || null,
        explanation: explanation.trim() || null,
      });
    } else {
      Object.assign(payload, { proposedGrade: grade, comment });
    }
    onSubmit(payload);
  };

  const setVote = (key: keyof VoteTallyDto, value: string) => {
    setVotes({ ...votes, [key]: key === "notes" ? value : Math.max(0, Number(value) || 0) });
    setMinutesError(null);
  };

  return (
    <form onSubmit={submit} className="row g-2">
      <div className="col-md-6"><label className="form-label small" htmlFor={`${stage}-grade`}>{isDecision ? "Mức xếp loại được quyết định" : unitLeader ? "Mức Trưởng đơn vị đề xuất" : "Mức tập thể đề xuất"}</label><GradeSelect id={`${stage}-grade`} value={grade} onChange={setGrade} /></div>
      <GradeWarningList warnings={[...(isDecision ? recordGradeWarnings(record, grade) : []), ...(check ? [check.message] : [])]} />
      {isDecision ? (
        <>
          <div className="col-md-3"><label className="form-label small text-nowrap" htmlFor={`${stage}-score`}>Điểm chính thức</label><DecimalInput id={`${stage}-score`} min={0} max={100} className="form-control form-control-sm num" value={finalScore} onChange={setFinalScore} placeholder={(record.reviewedScore ?? record.appraisalScore) != null ? `Thẩm định: ${fmt((record.reviewedScore ?? record.appraisalScore) as number)}` : "Theo thẩm định"} /></div>
          <div className="col-md-4"><label className="form-label small" htmlFor={`${stage}-authority`}>Cơ quan quyết định</label><input id={`${stage}-authority`} className="form-control form-control-sm" value={authorityName} maxLength={300} onChange={(e) => setAuthorityName(e.target.value)} title={authorityName} /></div>
          <div className="col-md-5"><label className="form-label small" htmlFor={`${stage}-docno`}>Số văn bản</label><input id={`${stage}-docno`} className="form-control form-control-sm" value={documentNumber} maxLength={100} onChange={(e) => setDocumentNumber(e.target.value)} /></div>
          <div className="col-md-4"><label className="form-label small" htmlFor={`${stage}-docdate`}>Ngày văn bản</label><DateField id={`${stage}-docdate`} value={documentDate} onChange={setDocumentDate} /></div>
          <CadreProposalInput id={`${stage}-cadre`} value={cadreWorkProposal} onChange={setCadreWorkProposal} />
          {(check || explanation) && (
            <ReasonField id={`${stage}-reason`} label="Căn cứ quyết định mức" value={explanation} required={!!check?.required} error={reasonError}
              onChange={(v) => { setExplanation(v); setReasonError(null); }} />
          )}
        </>
      ) : (
        <ReasonField id={`${stage}-reason`} label={unitLeader ? (leaderDiffers ? "Lý do đề xuất khác kết quả kiểm phiếu" : "Lý do, nhận xét của Trưởng đơn vị") : "Nhận xét của tập thể"}
          value={comment} required={!!check?.required || leaderDiffers} error={reasonError}
          onChange={(v) => { setComment(v); setReasonError(null); }} />
      )}
      {showMeetings && (
      <div className="col-md-8">
        <label className="form-label small" htmlFor={`${stage}-meeting`}>Biên bản hội nghị (Mẫu 12/13){minutesRequired ? " *" : ""}</label>
        <select id={`${stage}-meeting`} className="form-select form-select-sm" value={meetingId} required={minutesRequired}
          onChange={(e) => { setMeetingId(e.target.value); setMinutesError(null); }}>
          <option value="">{minutesRequired ? "— Chọn biên bản —" : "— Không gắn biên bản —"}</option>
          {meetings.map((m) => (
            <option key={m.id} value={m.id}>
              {m.formCode} · {m.departmentName || m.partyCellName || "Cấp Công ty"} · có mặt {m.presentCount}
            </option>
          ))}
        </select>
        {minutesRequired && meetings.length === 0 && (
          <div className="small text-danger mt-1">Chưa có biên bản họp ở bước này. Thư ký lập biên bản (Mẫu 12/13) trước, sau đó mới ghi nhận được.</div>
        )}
        {minutesError && <div className="small text-danger mt-1" role="alert">{minutesError}</div>}
      </div>
      )}
      {meetingId && (
        <div className="col-12">
          {!minutesRequired && (
          <div className="form-check">
            <input id={`${stage}-votes`} className="form-check-input" type="checkbox" checked={withVotes} onChange={(e) => setWithVotes(e.target.checked)} />
            <label className="form-check-label small" htmlFor={`${stage}-votes`}>Ghi kết quả kiểm phiếu của hồ sơ này</label>
          </div>
          )}
          {withVotesEffective && (
            <div className="row g-1 mt-1">
              {([
                ["votesExcellent", "Xuất sắc"],
                ["votesGood", "Tốt"],
                ["votesSatisfactory", "Hoàn thành"],
                ["votesUnsatisfactory", "Không hoàn thành"],
                ["votesNotRated", "Chưa xếp loại"],
                ["invalidVotes", "Không hợp lệ"],
              ] as [keyof VoteTallyDto, string][]).map(([key, label]) => (
                <div className="col" key={key}>
                  <label className="form-label small mb-0">{label}</label>
                  <input type="number" min={0} className="form-control form-control-sm" value={votes[key] as number} onChange={(e) => setVote(key, e.target.value)} />
                </div>
              ))}
              <div className={`col-12 small ${selectedMeeting && totalVotes > selectedMeeting.presentCount ? "text-danger" : "text-secondary"}`}>
                Tổng phiếu: {totalVotes}{selectedMeeting ? ` / số có mặt ${selectedMeeting.presentCount}` : ""}
              </div>
              {voteResult && (
                <div className={`col-12 small ${voteResult.grade && !voteMismatch ? "text-success" : "text-danger"}`} role="status">
                  {voteResult.grade
                    ? `Theo kết quả kiểm phiếu: ${GRADE_LABELS[voteResult.grade as RankedGrade]} (${voteResult.votes}/${voteResult.basis} phiếu, cộng dồn từ mức cao).`
                      + (voteMismatch ? (unitLeader ? " Mức đang chọn khác kết quả kiểm phiếu: ghi lý do ở trên." : " Mức đang chọn khác kết quả kiểm phiếu.") : "")
                    : `Không mức nào đạt quá nửa (${voteResult.basis}): hãy kiểm tra lại số phiếu.`}
                </div>
              )}
            </div>
          )}
        </div>
      )}
      <FormFoot row extra={extra}><button type="submit" className="btn btn-primary" disabled={busy || !grade || (minutesRequired && meetings.length === 0) || (!!voteResult && (!voteResult.grade || (voteMismatch && !unitLeader)))}>{submitText}</button></FormFoot>
    </form>
  );
}

/** Cơ quan cấp trên gợi ý theo bước, dựng từ thông tin đơn vị — người ghi nhận sửa được. */
function defaultAuthority(step: WorkflowStepCode, org: PublicOrganizationInfo): string {
  const parent = org.parentCompanyName;
  switch (step) {
    case "B3A_COLLECTIVE": return "Tập thể lãnh đạo, quản lý cấp trên";
    case "B3B_APPRAISAL": return `Ban Tổ chức Đảng ủy ${parent}`;
    case "B3C_DIRECTOR": return `Hội đồng thành viên ${parent}`;
    case "B4_DECISION": return `Ban Thường vụ Đảng ủy ${parent}`;
    default: return "";
  }
}

/** Ghi nhận kết quả bước do cấp trên / cơ quan ngoài hệ thống thực hiện. */
function ExternalResultForm({ record, step, busy, extra, onSubmit }: { record: EvaluationRecordDto; step: WorkflowStepCode; busy: boolean; extra?: React.ReactNode; onSubmit: (p: Record<string, unknown>) => void }) {
  const graded = GRADED_STEPS.includes(step);
  const withScore = step === "B3B_APPRAISAL" || step === "B4_DECISION";
  const previous = record.externalResults?.find((r) => r.step === step);
  const org = useOrganizationInfo();
  const [authorityName, setAuthorityName] = useState(previous?.authorityName || "");
  useEffect(() => {
    if (org) setAuthorityName((current) => current || defaultAuthority(step, org));
  }, [org, step]);
  const [documentNumber, setDocumentNumber] = useState(previous?.documentNumber || "");
  const [documentDate, setDocumentDate] = useState(previous?.documentDate?.substring(0, 10) || "");
  const [comment, setComment] = useState(previous?.comment || "");
  const [grade, setGrade] = useState(
    initialGrade(previous?.grade) || initialGrade(latestProposedGrade(record))
  );
  const [score, setScore] = useState(previous?.score != null ? String(previous.score) : "");
  const [cadreWorkProposal, setCadreWorkProposal] = useState(record.cadreWorkProposal || "");
  const [attachment, setAttachment] = useState<{ id: string; fileName: string; fileSize?: number } | null>(
    previous?.attachmentId ? { id: previous.attachmentId, fileName: "Văn bản đã đính kèm" } : null
  );
  // Kết quả chỉ lưu mã tệp: lấy tên, dung lượng để hiện trong danh sách.
  useEffect(() => {
    const id = previous?.attachmentId;
    if (!id) return;
    attachmentService.getAttachment(id)
      .then((f) => setAttachment((current) => (current?.id === id ? { id, fileName: f.fileName, fileSize: f.fileSize } : current)))
      .catch(() => undefined);
  }, [previous?.attachmentId]);
  const uploadDocument = async (file: File) => {
    const created = await attachmentService.uploadAttachment(file, "CAPTREN", `Văn bản của cấp trên: ${STEP_NAMES[step]} — ${record.fullName}`.slice(0, 500));
    setAttachment({ id: created.id, fileName: created.fileName, fileSize: created.fileSize });
  };

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit({
      authorityName,
      documentNumber: documentNumber || null,
      documentDate: documentDate || null,
      comment: comment || null,
      grade: graded ? grade : null,
      score: withScore ? parseDecimal(score) : null,
      attachmentId: attachment?.id ?? null,
      cadreWorkProposal: step === "B4_DECISION" ? cadreWorkProposal.trim() || null : null,
    });
  };

  return (
    <form onSubmit={submit} className="row g-2">
      <div className="col-md-6"><label className="form-label small" htmlFor={`ext-${step}-authority`}>Cơ quan / cấp thực hiện <span className="text-danger">*</span></label>
        <input id={`ext-${step}-authority`} className="form-control form-control-sm" required maxLength={300} value={authorityName} onChange={(e) => setAuthorityName(e.target.value)} />
      </div>
      <div className="col-md-3"><label className="form-label small" htmlFor={`ext-${step}-docno`}>Số văn bản</label><input id={`ext-${step}-docno`} className="form-control form-control-sm" maxLength={100} value={documentNumber} onChange={(e) => setDocumentNumber(e.target.value)} /></div>
      <div className="col-md-3"><label className="form-label small" htmlFor={`ext-${step}-docdate`}>Ngày văn bản</label><DateField id={`ext-${step}-docdate`} value={documentDate} onChange={setDocumentDate} /></div>
      {graded && (
        <div className="col-md-6"><label className="form-label small" htmlFor={`ext-${step}-grade`}>{step === "B4_DECISION" ? "Mức xếp loại được quyết định" : "Mức đề xuất"}</label>
          <GradeSelect id={`ext-${step}-grade`} value={grade} onChange={setGrade} />
        </div>
      )}
      {withScore && (
        <div className="col-md-3"><label className="form-label small" htmlFor={`ext-${step}-score`}>Điểm (tùy chọn)</label>
          <DecimalInput id={`ext-${step}-score`} min={0} max={100} className="form-control form-control-sm num" value={score} onChange={setScore} />
        </div>
      )}
      <div className="col-12"><label className="form-label small">Nhận xét / nội dung kết luận</label><textarea className="form-control form-control-sm" rows={3} maxLength={4000} value={comment} onChange={(e) => setComment(e.target.value)} /></div>
      {step === "B4_DECISION" && <CadreProposalInput id={`ext-${step}-cadre`} value={cadreWorkProposal} onChange={setCadreWorkProposal} />}
      <div className="col-12 col-md-8">
        <div className="form-label small">Văn bản đính kèm (tùy chọn)</div>
        <AttachmentList
          files={attachment ? [attachment] : []}
          context={STEP_NAMES[step]}
          max={1}
          emptyText={null}
          addLabel="Đính kèm văn bản"
          confirmRemove={false}
          onAdd={uploadDocument}
          onReplace={(_, file) => uploadDocument(file)}
          onRemove={() => setAttachment(null)}
        />
      </div>
      <FormFoot row extra={extra}><button type="submit" className="btn btn-primary" disabled={busy || !authorityName.trim() || (graded && !grade)}>Ghi nhận kết quả</button></FormFoot>
    </form>
  );
}

function TasksForm({ record, criteria, busy, onSubmit, submitText = "Nộp danh mục", submitIcon = "fa-paper-plane", onCancel }: { record: EvaluationRecordDto; criteria: CriteriaSnapshot | null; busy: boolean; onSubmit: (p: Record<string, unknown>) => void; submitText?: string; submitIcon?: string; onCancel?: () => void }) {
  const axes = criteria?.content.axes ?? [];
  const p = criteria?.content.parameters;
  const blank = (): TaskInputDto => ({
    taskName: "", targetOutput: "", weight: 0, deadline: "", axisCode: "", productCode: "", kind: "", role: "", exceedCriteria: "", evidenceSource: "", confirmer: "",
  });
  const initial: TaskInputDto[] = record.tasks.length > 0
    ? record.tasks.map((t) => ({
        taskName: t.taskName, targetOutput: t.targetOutput, weight: t.weight, deadline: t.deadline?.substring(0, 10), attachmentId: t.attachmentId,
        axisCode: t.axisCode ?? "", productCode: t.productCode ?? "", kind: t.kind === "DotXuat" ? "" : t.kind ?? "", role: t.role ?? "",
        exceedCriteria: t.exceedCriteria ?? "", evidenceSource: t.evidenceSource ?? "", confirmer: t.confirmer ?? "",
      }))
    : Array.from({ length: p?.minTasks ?? 3 }, blank);
  const [tasks, setTasks] = useState<TaskInputDto[]>(initial);
  // Phần "Thêm chi tiết" của từng sản phẩm: mở sẵn khi đã có dữ liệu.
  const [moreOpen, setMoreOpen] = useState<boolean[]>(() => initial.map(hasTaskDetails));
  const total =useMemo(() => Math.round(tasks.reduce((sum, t) => sum + (Number(t.weight) || 0), 0) * 100) / 100, [tasks]);

  const update = (index: number, key: keyof TaskInputDto, value: string) =>
    setTasks(tasks.map((t, i) => (i === index ? { ...t, [key]: key === "weight" ? Number(value) : value } : t)));

  const setMore = (index: number, open: boolean) => setMoreOpen((list) => tasks.map((_, i) => (i === index ? open : !!list[i])));

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        onSubmit({ tasks: tasks.map((t) => ({ ...t, deadline: t.deadline || null, axisCode: t.axisCode || null, productCode: t.productCode || null })) });
      }}
      // Ô bắt buộc nằm trong phần chi tiết đang gập: mở ra ngay để trình duyệt đưa con trỏ tới ô thiếu.
      onInvalidCapture={(e) => {
        const index = Number((e.target as HTMLElement).dataset.task);
        if (Number.isInteger(index) && !moreOpen[index]) flushSync(() => setMore(index, true));
      }}
    >
      <div className="d-flex flex-column gap-2 mb-2">
        {tasks.map((t, i) => (
          <fieldset key={i} className="border rounded-3 p-3">
            <div className="d-flex justify-content-between align-items-center mb-2">
              <legend className="h6 mb-0 float-none w-auto">Sản phẩm {i + 1}</legend>
              <button
                type="button"
                className="btn btn-link btn-sm text-danger p-0"
                onClick={() => {
                  setTasks(tasks.filter((_, idx) => idx !== i));
                  setMoreOpen(moreOpen.filter((_, idx) => idx !== i));
                }}
              >
                <i className="fa-solid fa-xmark me-1" />Xóa
              </button>
            </div>
            <div className="row g-2">
              <div className="col-12">
                <label className="form-label small mb-0" htmlFor={`t${i}-name`}>Tên sản phẩm, công việc</label>
                <input id={`t${i}-name`} className="form-control form-control-sm" value={t.taskName} maxLength={500} required onChange={(e) => update(i, "taskName", e.target.value)} />
              </div>
              <div className="col-md-3">
                <label className="form-label small mb-0" htmlFor={`t${i}-kind`}>Loại việc</label>
                <select id={`t${i}-kind`} className="form-select form-select-sm" value={t.kind || ""} required onChange={(e) => update(i, "kind", e.target.value)}>
                  <option value="">— Chọn —</option>
                  <option value="TrongTam">{TASK_KIND_LABELS.TrongTam}</option>
                  <option value="ThuongXuyen">{TASK_KIND_LABELS.ThuongXuyen}</option>
                </select>
              </div>
              <div className="col-md-3">
                <label className="form-label small mb-0" htmlFor={`t${i}-role`}>Vai trò</label>
                <select id={`t${i}-role`} className="form-select form-select-sm" value={t.role || ""} required onChange={(e) => update(i, "role", e.target.value)}>
                  <option value="">— Chọn —</option>
                  <option value="ChuTri">{TASK_ROLE_LABELS.ChuTri}</option>
                  <option value="PhoiHop">{TASK_ROLE_LABELS.PhoiHop}</option>
                </select>
              </div>
              <div className="col-md-6">
                <label className="form-label small mb-0" htmlFor={`t${i}-axis`}>Trục kết quả</label>
                <select id={`t${i}-axis`} className="form-select form-select-sm text-truncate" value={t.axisCode || ""} required={axes.length > 0}
                  onChange={(e) => update(i, "axisCode", e.target.value)} title={axes.find((a) => a.code === t.axisCode)?.name}>
                  <option value="">— Chọn —</option>
                  {axes.map((a) => <option key={a.code} value={a.code} title={a.description ? `${a.name}\n${a.description}` : a.name}>{a.code} — {a.name}</option>)}
                </select>
              </div>
              <div className="col-md-3">
                <label className="form-label small mb-0" htmlFor={`t${i}-weight`}>Trọng số (điểm)</label>
                <input id={`t${i}-weight`} type="number" min={0} step={0.5} className="form-control form-control-sm" value={t.weight} onChange={(e) => update(i, "weight", e.target.value)} />
              </div>
              <div className="col-md-3">
                <label className="form-label small mb-0" htmlFor={`t${i}-deadline`}>Thời hạn / mốc quý</label>
                <DateField id={`t${i}-deadline`} value={t.deadline || ""} onChange={(v) => update(i, "deadline", v)} />
              </div>
              <div className="col-md-6">
                <label className="form-label small mb-0" htmlFor={`t${i}-code`}>Mã sản phẩm</label>
                <input id={`t${i}-code`} className="form-control form-control-sm" value={t.productCode || ""} maxLength={50}
                  placeholder="Để trống: hệ thống tự đánh SP01, SP02…" onChange={(e) => update(i, "productCode", e.target.value)} />
              </div>
              <div className="col-12">
                <label className="form-label small mb-0" htmlFor={`t${i}-standard`}>Chuẩn đạt</label>
                <textarea id={`t${i}-standard`} className="form-control form-control-sm" rows={2} maxLength={2000} required value={t.targetOutput} onChange={(e) => update(i, "targetOutput", e.target.value)} />
              </div>
              <div className="col-12">
                <button type="button" id={`t${i}-more`} className="btn btn-link btn-sm p-0" aria-expanded={!!moreOpen[i]} aria-controls={`t${i}-details`}
                  onClick={() => setMore(i, !moreOpen[i])}>
                  <i className={`fa-solid ${moreOpen[i] ? "fa-chevron-down" : "fa-chevron-right"} me-1`} aria-hidden="true" />Thêm chi tiết
                </button>
              </div>
            </div>
            {/* Luôn dựng để kiểm tra ô bắt buộc khi nộp; chỉ ẩn khi gập. */}
            <div id={`t${i}-details`} className="row g-2 mt-0" hidden={!moreOpen[i]}>
              <div className="col-md-6">
                <label className="form-label small mb-0" htmlFor={`t${i}-exceed`}>Tiêu chí vượt chuẩn</label>
                <textarea id={`t${i}-exceed`} data-task={i} className="form-control form-control-sm" rows={2} maxLength={2000} required value={t.exceedCriteria || ""} onChange={(e) => update(i, "exceedCriteria", e.target.value)} />
              </div>
              <div className="col-md-6">
                <label className="form-label small mb-0" htmlFor={`t${i}-evidence`}>Nguồn dữ liệu / minh chứng</label>
                <input id={`t${i}-evidence`} data-task={i} className="form-control form-control-sm" maxLength={1000} required value={t.evidenceSource || ""} onChange={(e) => update(i, "evidenceSource", e.target.value)} />
              </div>
              <div className="col-md-6">
                <label className="form-label small mb-0" htmlFor={`t${i}-confirmer`}>Thẩm quyền xác nhận</label>
                <input id={`t${i}-confirmer`} data-task={i} className="form-control form-control-sm" maxLength={500} required value={t.confirmer || ""} onChange={(e) => update(i, "confirmer", e.target.value)} />
              </div>
            </div>
          </fieldset>
        ))}
      </div>
      <div className={`d-flex flex-wrap gap-2 justify-content-between align-items-center border-top py-2 ${styles.stickyFoot}`}>
        <div className="d-flex gap-2 align-items-center">
          <button type="button" className="btn btn-outline-secondary" onClick={() => { setTasks([...tasks, blank()]); setMoreOpen([...moreOpen, false]); }}>
            <i className="fa-solid fa-plus me-1" />Thêm dòng
          </button>
          <span className="small text-secondary">
            Tổng trọng số: <strong className={p && Math.abs(total - p.totalTaskWeight) > p.taskWeightTolerance ? "text-danger" : ""}>{total}</strong>
            {p && <> / {fmt(p.totalTaskWeight)}</>}
          </span>
          {p && <span className="small text-secondary">· {p.minTasks}–{p.maxTasks} sản phẩm</span>}
        </div>
        <div className="d-flex gap-2">
          {onCancel && <button type="button" className="btn btn-outline-secondary" disabled={busy} onClick={onCancel}>Hủy</button>}
          <button type="submit" className="btn btn-primary" disabled={busy}><i className={`fa-solid ${submitIcon} me-1`} aria-hidden="true" />{submitText}</button>
        </div>
      </div>
    </form>
  );
}

/** Chờ cấp trên duyệt: sửa danh mục cho khớp kết quả duyệt, mở bằng nút để không lẫn với ghi nhận kết quả. */
function UpdateTasksForm({ record, criteria, busy, onSubmit }: { record: EvaluationRecordDto; criteria: CriteriaSnapshot | null; busy: boolean; onSubmit: (p: Record<string, unknown>) => void }) {
  const [open, setOpen] = useState(false);
  if (!open) {
    return (
      <FormFoot only>
        <button type="button" className="btn btn-outline-primary" disabled={busy} onClick={() => setOpen(true)}>
          <i className="fa-solid fa-pen-to-square me-1" aria-hidden="true" />Cập nhật danh mục
        </button>
      </FormFoot>
    );
  }
  return <TasksForm record={record} criteria={criteria} busy={busy} submitText="Lưu danh mục" submitIcon="fa-floppy-disk" onCancel={() => setOpen(false)} onSubmit={onSubmit} />;
}

export default RecordActionPanel;

/** Sản phẩm đã có dữ liệu ở phần "Thêm chi tiết". */
const hasTaskDetails = (t: TaskInputDto) => !!(t.exceedCriteria || t.evidenceSource || t.confirmer);

/** Cột 13 Mẫu 14 (tùy chọn, ghi ở bước quyết định). */
function CadreProposalInput({ id, value, onChange }: { id: string; value: string; onChange: (value: string) => void }) {
  return (
    <div className="col-12">
      <label className="form-label small" htmlFor={id}>Đề xuất nội dung liên quan về công tác cán bộ (nếu có)</label>
      <textarea id={id} className="form-control form-control-sm" rows={2} maxLength={2000} value={value} onChange={(e) => onChange(e.target.value)} />
    </div>
  );
}
