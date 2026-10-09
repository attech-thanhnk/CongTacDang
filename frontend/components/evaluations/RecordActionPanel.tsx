"use client";

import React, { useEffect, useId, useMemo, useState } from "react";
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
  evaluationService,
  gradeConditionWarnings,
} from "@/services/evaluationService";
import { FileUploadModal } from "@/components/attachments/FileUploadModal";
import { useCriteria } from "@/components/evaluations/useCriteria";
import { SelfScoreForm } from "@/components/evaluations/SelfScoreForm";
import { CriteriaSnapshot, WeightFrame, describeFrame, fmt, requiresExplanation, roundScore } from "@/services/criteriaService";
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

/**
 * Khu vực thao tác của hồ sơ: chỉ hiển thị biểu mẫu/nút cho các hành động mà máy chủ trả về trong `actions`
 * (frontend không tự suy luật quyền hay thứ tự bước).
 */
export function RecordActionPanel({ record, actions, busy, onSubmit, onReason }: Props) {
  const { criteria } = useCriteria(record.periodId);
  const [appraisal, setAppraisal] = useState<number | null>(null);
  const complete = actions.filter((a) => !a.requiresReason);
  const withReason = actions.filter((a) => a.requiresReason);

  if (actions.length === 0) return null;

  const reasonButtons =
    withReason.length > 0 ? (
      <span className={styles.pushRight}>
        {withReason.map((action) => (
          <button key={action.action} type="button" className="btn btn-outline-danger btn-sm" disabled={busy} onClick={() => onReason(action)}>
            {action.label}
          </button>
        ))}
      </span>
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
        <div className={styles.foot}>{reasonButtons}</div>
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
              {action.overdue && <span className="tag t-bad">Quá hạn</span>}
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
                busy={busy}
                onSubmit={onSubmit}
                extra={index === 0 && reasonsInForm ? reasonButtons : null}
                onAppraisalChange={setAppraisal}
              />
            </div>
            {index === 0 && reasonButtons && !reasonsInForm && <div className={styles.foot}>{reasonButtons}</div>}
          </section>
        );
      })}
    </>
  );
}

/** Thao tác có biểu mẫu gọn, nút chính nằm ở chân khu thao tác (cùng nút trả lại). */
const FOOTED_FORMS = new Set<WorkflowActionCode>([
  "RecordExternal",
  "ApproveTasks",
  "ConfirmByCell",
  "RecordCollectiveProposal",
  "Appraise",
  "DirectorReview",
  "RecordDecision",
  "Publish",
]);

/** Chân biểu mẫu tràn tới mép khu thao tác; `row` khi nằm trong lưới Bootstrap `.row.g-2`. */
function FormFoot({ extra, row = false, only = false, children }: { extra?: React.ReactNode; row?: boolean; only?: boolean; children: React.ReactNode }) {
  return (
    <div className={row ? `col-12 ${styles.formFootRow}` : only ? styles.formFootOnly : styles.formFoot}>
      {children}
      {extra}
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
    case "SubmitSelfScore":
      return <SelfScoreForm record={record} criteria={criteria} busy={busy} onSubmit={(payload) => onSubmit(action, payload)} />;
    case "ApproveTasks":
      return <ApproveTasksForm record={record} busy={busy} extra={extra} onSubmit={(payload) => onSubmit(action, payload)} />;
    case "ConfirmByCell":
      return <CommentForm busy={busy} extra={extra} label="Ý kiến" submitText="Xác nhận phiếu tự chấm" onSubmit={(payload) => onSubmit(action, payload)} />;
    case "RecordCollectiveProposal":
      return <ProposalForm record={record} busy={busy} extra={extra} stage="B3A_COLLECTIVE" submitText="Ghi nhận đề xuất" onSubmit={(payload) => onSubmit(action, payload)} />;
    case "Appraise":
      return <AppraisalForm record={record} criteria={criteria} busy={busy} extra={extra} onScoreChange={onAppraisalChange} onSubmit={(payload) => onSubmit(action, payload)} />;
    case "DirectorReview":
      return <GradeCommentForm record={record} busy={busy} extra={extra} defaultGrade={record.appraisalProposedGrade} submitText="Ghi nhận nhận xét" onSubmit={(payload) => onSubmit(action, payload)} />;
    case "RecordDecision":
      return <ProposalForm record={record} busy={busy} extra={extra} stage="B4_DECISION" submitText="Ghi nhận quyết định" onSubmit={(payload) => onSubmit(action, payload)} />;
    case "Publish":
      return (
        <FormFoot only extra={extra}>
          <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={() => onSubmit(action, {})}>Công bố, khóa kết quả</button>
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
        <button type="submit" className="btn btn-primary btn-sm" disabled={busy}>{submitText}</button>
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

/** Duyệt danh mục sản phẩm: ý kiến + điều chỉnh tỷ lệ 4 mặt chấm A-B-C-D trong biên độ của bộ tiêu chí (mặc định giữ nguyên). */
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
            Tỷ lệ 4 mặt chấm: {describeFrame({ a: values.a / 100, b: values.b / 100, c: values.c / 100, d: values.d / 100 })}
            {FRAME_PARTS.some((p) => Math.abs(values[p.key] - basePercents[p.key]) > 1e-6) && <span className="text-primary"> (đã điều chỉnh)</span>}
          </summary>
          <div className="text-secondary my-1">
            Khung {base!.code}: {describeFrame(base!)}. Được điều chỉnh mỗi mặt ±{fmt(limit)} điểm %, tổng 100%.
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
        <button type="submit" className="btn btn-primary btn-sm" disabled={busy || (canAdjust && changed && errors.length > 0)}>Duyệt danh mục</button>
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

function GradeCommentForm({ record, busy, defaultGrade, submitText, extra, onSubmit }: { record: EvaluationRecordDto; busy: boolean; defaultGrade?: string; submitText: string; extra?: React.ReactNode; onSubmit: (p: Record<string, unknown>) => void }) {
  const [grade, setGrade] = useState(initialGrade(defaultGrade));
  const [comment, setComment] = useState("");
  return (
    <form onSubmit={(e) => { e.preventDefault(); onSubmit({ proposedGrade: grade, comment }); }} className="row g-2">
      <div className="col-md-5"><label className="form-label small" htmlFor="gc-grade">Mức đề xuất</label><GradeSelect id="gc-grade" value={grade} onChange={setGrade} /></div>
      <GradeWarningList warnings={recordGradeWarnings(record, grade)} />
      <div className="col-12"><label className="form-label small" htmlFor="gc-comment">Nhận xét</label><textarea id="gc-comment" className="form-control form-control-sm" rows={3} maxLength={4000} value={comment} onChange={(e) => setComment(e.target.value)} /></div>
      <FormFoot row extra={extra}><button type="submit" className="btn btn-primary btn-sm" disabled={busy || !grade}>{submitText}</button></FormFoot>
    </form>
  );
}

/**
 * Thẩm định: chênh lệch |tự chấm − thẩm định| từ ngưỡng của bộ tiêu chí (hoặc làm đổi mức, nếu bộ bật) → bắt buộc nội dung
 * giải trình/căn cứ (máy chủ kiểm tra lại). Điểm tự chấm và chênh lệch hiện ở tiêu đề khu thao tác (`onScoreChange`).
 */
function AppraisalForm({ record, criteria, busy, extra, onScoreChange, onSubmit }: { record: EvaluationRecordDto; criteria: CriteriaSnapshot | null; busy: boolean; extra?: React.ReactNode; onScoreChange?: (score: number | null) => void; onSubmit: (p: Record<string, unknown>) => void }) {
  // Bộ chấm theo danh mục nhiệm vụ (09A): xếp Mức 1/2/3 cho từng nhiệm vụ; bộ chấm theo trục (09B) không có phần này.
  // Việc đã loại trừ / chuyển tiếp trong quý không chấm, không xếp mức.
  const scoredTasks = record.tasks.filter((t) => !t.changeStatus);
  const usesTasks = record.selfScoreForm !== "09B" && scoredTasks.length > 0;
  const [general, setGeneral] = useState<string>(String(record.appraisalGeneralScore ?? record.generalCriteriaScore ?? ""));
  const [tasksScore, setTasksScore] = useState<string>(String(record.appraisalTasksScore ?? record.tasksScore ?? ""));
  const [levels, setLevels] = useState<Record<string, number | null>>(() => Object.fromEntries(record.tasks.map((t) => [t.id, t.appraisalLevel ?? null])));
  const [taskComments, setTaskComments] = useState<Record<string, string>>(() => Object.fromEntries(record.tasks.map((t) => [t.id, t.appraisalComment ?? ""])));
  const [grade, setGrade] = useState(initialGrade(record.appraisalProposedGrade) || initialGrade(record.collectiveProposedGrade) || initialGrade(record.selfProposedGrade));
  const [comment, setComment] = useState(record.appraisalComment || "");
  const [explanation, setExplanation] = useState(record.appraisalExplanation || "");
  const generalValue = general === "" ? null : Number(general);
  const tasksValue = tasksScore === "" ? null : Number(tasksScore);
  const p = criteria?.content.parameters;
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
  const warnings = usesTasks ? gradeConditionWarnings(leveledTasks, grade, record.excellentMinExceedRatio) : [];

  useEffect(() => {
    onScoreChange?.(appraisal);
  }, [appraisal, onScoreChange]);

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit({
      generalScore: generalValue,
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
            <thead><tr><th>#</th><th>Nhiệm vụ</th><th>Loại</th><th>Tự chấm</th><th>Vượt chuẩn</th><th>Mức thẩm định</th><th>Nhận xét</th></tr></thead>
            <tbody>
              {scoredTasks.map((t) => (
                <tr key={t.id}>
                  <td className="num">{t.taskOrder}</td>
                  <td>{t.taskName}</td>
                  <td>{t.kind ? TASK_KIND_LABELS[t.kind] : "—"}</td>
                  <td className="num">{t.weight > 0 ? `${fmt((t.selfScore / t.weight) * 100, 1)}%` : "—"}</td>
                  <td>{t.isExceedStandard ? "Có" : "—"}</td>
                  <td>
                    <select
                      aria-label={`Mức thẩm định: ${t.taskName}`}
                      className="form-select form-select-sm"
                      required
                      value={levels[t.id] ?? ""}
                      onChange={(e) => setLevels((prev) => ({ ...prev, [t.id]: e.target.value === "" ? null : Number(e.target.value) }))}
                    >
                      <option value="">— Chọn —</option>
                      {APPRAISAL_LEVELS.map((l) => <option key={l.value} value={l.value} title={l.hint}>{l.label} — {l.hint}</option>)}
                    </select>
                  </td>
                  <td>
                    <input
                      aria-label={`Nhận xét: ${t.taskName}`}
                      className="form-control form-control-sm"
                      maxLength={1000}
                      value={taskComments[t.id] ?? ""}
                      onChange={(e) => setTaskComments((prev) => ({ ...prev, [t.id]: e.target.value }))}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table></div>
        </div>
      )}
      <div className="col-md-3"><label className="form-label small" htmlFor="ap-general">Tiêu chí chung</label><input id="ap-general" type="number" min={0} max={p?.generalMaxScore ?? 100} step={0.1} required className="form-control form-control-sm num" value={general} onChange={(e) => setGeneral(e.target.value)} /></div>
      <div className="col-md-3"><label className="form-label small" htmlFor="ap-tasks">Kết quả nhiệm vụ</label><input id="ap-tasks" type="number" min={0} max={p?.totalTaskWeight ?? 100} step={0.1} required className="form-control form-control-sm num" value={tasksScore} onChange={(e) => setTasksScore(e.target.value)} /></div>
      <div className="col-md-2"><span className="form-label small d-block">Tổng</span><b className="num">{appraisal != null ? fmt(appraisal) : "—"}</b></div>
      <div className="col-md-4"><label className="form-label small" htmlFor="ap-grade">Mức đề xuất</label><GradeSelect id="ap-grade" value={grade} onChange={setGrade} /></div>
      <GradeWarningList warnings={warnings} />
      <div className="col-12"><label className="form-label small" htmlFor="ap-comment">Ý kiến thẩm định</label><textarea id="ap-comment" className="form-control form-control-sm" rows={3} maxLength={4000} value={comment} onChange={(e) => setComment(e.target.value)} /></div>
      <div className="col-12">
        <label className="form-label small" htmlFor="ap-explanation" title={rule}>
          Giải trình {needsExplanation && <span className="text-danger">*</span>}
        </label>
        <textarea id="ap-explanation" className="form-control form-control-sm" rows={2} maxLength={4000} required={needsExplanation} title={rule} value={explanation} onChange={(e) => setExplanation(e.target.value)} />
      </div>
      <FormFoot row extra={extra}><button type="submit" className="btn btn-primary btn-sm" disabled={busy || !grade || missingLevels || appraisal == null || (needsExplanation && !explanation.trim())}>Ghi nhận thẩm định</button></FormFoot>
    </form>
  );
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
  if (record.selfScoreForm === "09B") return [];
  return gradeConditionWarnings(record.tasks.filter((t) => !t.changeStatus), grade, record.excellentMinExceedRatio);
}

/** Ghi nhận đề xuất tập thể (B3a) hoặc quyết định (B4); kết quả kiểm phiếu (tùy chọn) gắn với biên bản. */
function ProposalForm({ record, busy, stage, submitText, extra, onSubmit }: { record: EvaluationRecordDto; busy: boolean; stage: "B3A_COLLECTIVE" | "B4_DECISION"; submitText: string; extra?: React.ReactNode; onSubmit: (p: Record<string, unknown>) => void }) {
  const isDecision = stage === "B4_DECISION";
  const [grade, setGrade] = useState(
    isDecision
      ? initialGrade(record.directorProposedGrade) || initialGrade(record.appraisalProposedGrade)
      : initialGrade(record.selfProposedGrade)
  );
  const [comment, setComment] = useState("");
  const [finalScore, setFinalScore] = useState("");
  const [documentNumber, setDocumentNumber] = useState("");
  const [documentDate, setDocumentDate] = useState("");
  const [authorityName, setAuthorityName] = useState(record.approvalAuthority === "CapTren" ? "Ban Thường vụ Đảng ủy Tổng công ty" : "Đảng ủy Công ty");
  const [cadreWorkProposal, setCadreWorkProposal] = useState(record.cadreWorkProposal || "");
  const [meetings, setMeetings] = useState<EvaluationMeetingDto[]>([]);
  const [meetingId, setMeetingId] = useState("");
  const [withVotes, setWithVotes] = useState(false);
  const [votes, setVotes] = useState<VoteTallyDto>(EMPTY_VOTES);

  useEffect(() => {
    evaluationService
      .getMeetings(record.periodId)
      .then((list) => setMeetings(list.filter((m) => !m.stage || m.stage === stage)))
      .catch(() => setMeetings([]));
  }, [record.periodId, stage]);

  const selectedMeeting = meetings.find((m) => m.id === meetingId);
  const totalVotes = votes.votesExcellent + votes.votesGood + votes.votesSatisfactory + votes.votesUnsatisfactory + votes.votesNotRated + votes.invalidVotes;

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    const payload: Record<string, unknown> = { meetingId: meetingId || null, votes: withVotes && meetingId ? votes : null };
    if (isDecision) {
      Object.assign(payload, {
        finalGrade: grade,
        finalScore: finalScore === "" ? null : Number(finalScore),
        documentNumber,
        documentDate: documentDate || null,
        authorityName,
        cadreWorkProposal: cadreWorkProposal.trim() || null,
      });
    } else {
      Object.assign(payload, { proposedGrade: grade, comment });
    }
    onSubmit(payload);
  };

  const setVote = (key: keyof VoteTallyDto, value: string) =>
    setVotes({ ...votes, [key]: key === "notes" ? value : Math.max(0, Number(value) || 0) });

  return (
    <form onSubmit={submit} className="row g-2">
      <div className="col-md-5"><label className="form-label small" htmlFor={`${stage}-grade`}>{isDecision ? "Mức xếp loại được quyết định" : "Mức tập thể đề xuất"}</label><GradeSelect id={`${stage}-grade`} value={grade} onChange={setGrade} /></div>
      {isDecision && <GradeWarningList warnings={recordGradeWarnings(record, grade)} />}
      {isDecision ? (
        <>
          <div className="col-md-3"><label className="form-label small">Điểm chính thức (tùy chọn)</label><input type="number" min={0} max={100} step={0.1} className="form-control form-control-sm" value={finalScore} onChange={(e) => setFinalScore(e.target.value)} placeholder="Mặc định: điểm thẩm định" /></div>
          <div className="col-md-4"><label className="form-label small">Cơ quan quyết định</label><input className="form-control form-control-sm" value={authorityName} maxLength={300} onChange={(e) => setAuthorityName(e.target.value)} /></div>
          <div className="col-md-4"><label className="form-label small">Số văn bản</label><input className="form-control form-control-sm" value={documentNumber} maxLength={100} onChange={(e) => setDocumentNumber(e.target.value)} /></div>
          <div className="col-md-4"><label className="form-label small">Ngày văn bản</label><input type="date" className="form-control form-control-sm" value={documentDate} onChange={(e) => setDocumentDate(e.target.value)} /></div>
          <CadreProposalInput id={`${stage}-cadre`} value={cadreWorkProposal} onChange={setCadreWorkProposal} />
        </>
      ) : (
        <div className="col-12"><label className="form-label small">Nhận xét của tập thể</label><textarea className="form-control form-control-sm" rows={2} maxLength={4000} value={comment} onChange={(e) => setComment(e.target.value)} /></div>
      )}
      <div className="col-md-8">
        <label className="form-label small">Biên bản hội nghị (Mẫu 12/13)</label>
        <select className="form-select form-select-sm" value={meetingId} onChange={(e) => setMeetingId(e.target.value)}>
          <option value="">— Không gắn biên bản —</option>
          {meetings.map((m) => (
            <option key={m.id} value={m.id}>
              {m.formCode} · {m.departmentName || m.partyCellName || "Cấp Công ty"} · có mặt {m.presentCount}
            </option>
          ))}
        </select>
      </div>
      {meetingId && (
        <div className="col-12">
          <div className="form-check">
            <input id={`${stage}-votes`} className="form-check-input" type="checkbox" checked={withVotes} onChange={(e) => setWithVotes(e.target.checked)} />
            <label className="form-check-label small" htmlFor={`${stage}-votes`}>Ghi kết quả kiểm phiếu của hồ sơ này</label>
          </div>
          {withVotes && (
            <div className="row g-1 mt-1">
              {([
                ["votesExcellent", "Xuất sắc"],
                ["votesGood", "Tốt"],
                ["votesSatisfactory", "Hoàn thành"],
                ["votesUnsatisfactory", "Không HT"],
                ["votesNotRated", "Chưa đánh giá"],
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
            </div>
          )}
        </div>
      )}
      <FormFoot row extra={extra}><button type="submit" className="btn btn-primary btn-sm" disabled={busy || !grade}>{submitText}</button></FormFoot>
    </form>
  );
}

/** Cơ quan cấp trên gợi ý theo bước (PL III ví dụ 2) — người ghi nhận sửa được. */
const DEFAULT_AUTHORITY: Partial<Record<WorkflowStepCode, string>> = {
  B3A_COLLECTIVE: "Tập thể lãnh đạo, quản lý cấp trên",
  B3B_APPRAISAL: "Ban Tổ chức Đảng ủy Tổng công ty",
  B3C_DIRECTOR: "Hội đồng thành viên Tổng công ty",
  B4_DECISION: "Ban Thường vụ Đảng ủy Tổng công ty",
};

/**
 * Ghi nhận kết quả của bước do cấp trên / cơ quan ngoài hệ thống thực hiện: cơ quan, số/ngày văn bản, nhận xét,
 * mức đề xuất/quyết định (bước có mức), điểm (tùy chọn), tệp đính kèm (tùy chọn).
 */
function ExternalResultForm({ record, step, busy, extra, onSubmit }: { record: EvaluationRecordDto; step: WorkflowStepCode; busy: boolean; extra?: React.ReactNode; onSubmit: (p: Record<string, unknown>) => void }) {
  const graded = GRADED_STEPS.includes(step);
  const withScore = step === "B3B_APPRAISAL" || step === "B4_DECISION";
  const previous = record.externalResults?.find((r) => r.step === step);
  const [authorityName, setAuthorityName] = useState(previous?.authorityName || DEFAULT_AUTHORITY[step] || "");
  const [documentNumber, setDocumentNumber] = useState(previous?.documentNumber || "");
  const [documentDate, setDocumentDate] = useState(previous?.documentDate?.substring(0, 10) || "");
  const [comment, setComment] = useState(previous?.comment || "");
  const [grade, setGrade] = useState(
    initialGrade(previous?.grade) || initialGrade(record.directorProposedGrade) || initialGrade(record.appraisalProposedGrade)
      || initialGrade(record.collectiveProposedGrade) || initialGrade(record.selfProposedGrade)
  );
  const [score, setScore] = useState(previous?.score != null ? String(previous.score) : "");
  const [cadreWorkProposal, setCadreWorkProposal] = useState(record.cadreWorkProposal || "");
  const [attachment, setAttachment] = useState<{ id: string; name: string } | null>(
    previous?.attachmentId ? { id: previous.attachmentId, name: "Tệp đã đính kèm" } : null
  );
  const [uploading, setUploading] = useState(false);

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit({
      authorityName,
      documentNumber: documentNumber || null,
      documentDate: documentDate || null,
      comment: comment || null,
      grade: graded ? grade : null,
      score: withScore && score !== "" ? Number(score) : null,
      attachmentId: attachment?.id ?? null,
      cadreWorkProposal: step === "B4_DECISION" ? cadreWorkProposal.trim() || null : null,
    });
  };

  return (
    <form onSubmit={submit} className="row g-2">
      <div className="col-md-6"><label className="form-label small" htmlFor={`ext-${step}-authority`}>Cơ quan / cấp thực hiện (bắt buộc)</label>
        <input id={`ext-${step}-authority`} className="form-control form-control-sm" required maxLength={300} value={authorityName} onChange={(e) => setAuthorityName(e.target.value)} />
      </div>
      <div className="col-md-3"><label className="form-label small">Số văn bản</label><input className="form-control form-control-sm" maxLength={100} value={documentNumber} onChange={(e) => setDocumentNumber(e.target.value)} /></div>
      <div className="col-md-3"><label className="form-label small">Ngày văn bản</label><input type="date" className="form-control form-control-sm" value={documentDate} onChange={(e) => setDocumentDate(e.target.value)} /></div>
      {graded && (
        <div className="col-md-6"><label className="form-label small" htmlFor={`ext-${step}-grade`}>{step === "B4_DECISION" ? "Mức xếp loại được quyết định" : "Mức đề xuất"}</label>
          <GradeSelect id={`ext-${step}-grade`} value={grade} onChange={setGrade} />
        </div>
      )}
      {withScore && (
        <div className="col-md-3"><label className="form-label small">Điểm (tùy chọn)</label>
          <input type="number" min={0} max={100} step={0.1} className="form-control form-control-sm" value={score} onChange={(e) => setScore(e.target.value)} />
        </div>
      )}
      <div className="col-12"><label className="form-label small">Nhận xét / nội dung kết luận</label><textarea className="form-control form-control-sm" rows={3} maxLength={4000} value={comment} onChange={(e) => setComment(e.target.value)} /></div>
      {step === "B4_DECISION" && <CadreProposalInput id={`ext-${step}-cadre`} value={cadreWorkProposal} onChange={setCadreWorkProposal} />}
      <div className="col-12 small">
        {attachment ? (
          <span><i className="bi bi-paperclip me-1" />{attachment.name}
            <button type="button" className="btn btn-link btn-sm p-0 ms-2 text-danger" onClick={() => setAttachment(null)}>Bỏ tệp</button>
          </span>
        ) : (
          <span className="text-secondary">Chưa đính kèm văn bản.</span>
        )}
        <button type="button" className="btn btn-link btn-sm p-0 ms-2" onClick={() => setUploading(true)}>Đính kèm văn bản (tùy chọn)</button>
      </div>
      <FormFoot row extra={extra}><button type="submit" className="btn btn-primary btn-sm" disabled={busy || !authorityName.trim() || (graded && !grade)}>Ghi nhận kết quả của cấp trên</button></FormFoot>
      {uploading && (
        <FileUploadModal
          isOpen
          onClose={() => setUploading(false)}
          formCode="CAPTREN"
          targetTitle={`${STEP_NAMES[step]} — ${record.fullName}`}
          defaultDescription={`Văn bản của cấp trên: ${STEP_NAMES[step]} — ${record.fullName}`}
          currentAttachmentId={attachment?.id}
          onUploadSuccess={(file) => {
            setAttachment({ id: file.id, name: file.fileName });
            setUploading(false);
          }}
        />
      )}
    </form>
  );
}

function TasksForm({ record, criteria, busy, onSubmit }: { record: EvaluationRecordDto; criteria: CriteriaSnapshot | null; busy: boolean; onSubmit: (p: Record<string, unknown>) => void }) {
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
  const total = useMemo(() => Math.round(tasks.reduce((sum, t) => sum + (Number(t.weight) || 0), 0) * 100) / 100, [tasks]);

  const update = (index: number, key: keyof TaskInputDto, value: string) =>
    setTasks(tasks.map((t, i) => (i === index ? { ...t, [key]: key === "weight" ? Number(value) : value } : t)));

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        onSubmit({ tasks: tasks.map((t) => ({ ...t, deadline: t.deadline || null, axisCode: t.axisCode || null, productCode: t.productCode || null })) });
      }}
    >
      <div className="d-flex flex-column gap-2 mb-2">
        {tasks.map((t, i) => (
          <fieldset key={i} className="border rounded-3 p-3">
            <div className="d-flex justify-content-between align-items-center mb-2">
              <legend className="h6 mb-0 float-none w-auto">Sản phẩm {i + 1}</legend>
              <button type="button" className="btn btn-link btn-sm text-danger p-0" onClick={() => setTasks(tasks.filter((_, idx) => idx !== i))}>
                <i className="bi bi-x-lg me-1" />Xóa
              </button>
            </div>
            <div className="row g-2">
              <div className="col-md-8">
                <label className="form-label small mb-0" htmlFor={`t${i}-name`}>Tên sản phẩm, công việc</label>
                <input id={`t${i}-name`} className="form-control form-control-sm" value={t.taskName} maxLength={500} required onChange={(e) => update(i, "taskName", e.target.value)} />
              </div>
              <div className="col-md-4">
                <label className="form-label small mb-0" htmlFor={`t${i}-code`}>Mã SP (nếu có)</label>
                <input id={`t${i}-code`} className="form-control form-control-sm" value={t.productCode || ""} maxLength={50} onChange={(e) => update(i, "productCode", e.target.value)} />
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
                <input id={`t${i}-deadline`} type="date" className="form-control form-control-sm" value={t.deadline || ""} onChange={(e) => update(i, "deadline", e.target.value)} />
              </div>
              <div className="col-md-6">
                <label className="form-label small mb-0" htmlFor={`t${i}-standard`}>Chuẩn đạt</label>
                <textarea id={`t${i}-standard`} className="form-control form-control-sm" rows={2} maxLength={2000} required value={t.targetOutput} onChange={(e) => update(i, "targetOutput", e.target.value)} />
              </div>
              <div className="col-md-6">
                <label className="form-label small mb-0" htmlFor={`t${i}-exceed`}>Tiêu chí vượt chuẩn</label>
                <textarea id={`t${i}-exceed`} className="form-control form-control-sm" rows={2} maxLength={2000} required value={t.exceedCriteria || ""} onChange={(e) => update(i, "exceedCriteria", e.target.value)} />
              </div>
              <div className="col-md-6">
                <label className="form-label small mb-0" htmlFor={`t${i}-evidence`}>Nguồn dữ liệu / minh chứng</label>
                <input id={`t${i}-evidence`} className="form-control form-control-sm" maxLength={1000} required value={t.evidenceSource || ""} onChange={(e) => update(i, "evidenceSource", e.target.value)} />
              </div>
              <div className="col-md-6">
                <label className="form-label small mb-0" htmlFor={`t${i}-confirmer`}>Thẩm quyền xác nhận</label>
                <input id={`t${i}-confirmer`} className="form-control form-control-sm" maxLength={500} required value={t.confirmer || ""} onChange={(e) => update(i, "confirmer", e.target.value)} />
              </div>
            </div>
          </fieldset>
        ))}
      </div>
      <div className="d-flex justify-content-between align-items-center bg-white border-top py-2" style={{ position: "sticky", bottom: 0, zIndex: 5 }}>
        <div className="d-flex gap-2 align-items-center">
          <button type="button" className="btn btn-outline-secondary btn-sm" onClick={() => setTasks([...tasks, blank()])}>
            <i className="bi bi-plus-lg me-1" />Thêm dòng
          </button>
          <span className="small text-secondary">
            Tổng trọng số: <strong className={p && Math.abs(total - p.totalTaskWeight) > p.taskWeightTolerance ? "text-danger" : ""}>{total}</strong>
            {p && <> / {fmt(p.totalTaskWeight)}</>}
          </span>
        </div>
        <button type="submit" className="btn btn-primary btn-sm" disabled={busy}>Nộp danh mục</button>
      </div>
      {p && <div className="form-text">{`${p.minTasks}–${p.maxTasks} sản phẩm, tổng trọng số ${fmt(p.totalTaskWeight)}.`}</div>}
    </form>
  );
}

export default RecordActionPanel;

/** Cột 13 Mẫu 14 — "Đề xuất nội dung liên quan về công tác cán bộ (nếu có)" (tùy chọn, ghi ở bước quyết định). */
function CadreProposalInput({ id, value, onChange }: { id: string; value: string; onChange: (value: string) => void }) {
  return (
    <div className="col-12">
      <label className="form-label small" htmlFor={id}>Đề xuất nội dung liên quan về công tác cán bộ (nếu có)</label>
      <textarea id={id} className="form-control form-control-sm" rows={2} maxLength={2000} value={value} onChange={(e) => onChange(e.target.value)} />
    </div>
  );
}
