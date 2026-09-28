"use client";

import React, { useEffect, useMemo, useState } from "react";
import {
  EvaluationMeetingDto,
  EvaluationRecordDto,
  GRADE_OPTIONS,
  RecordActionDto,
  TaskInputDto,
  VoteTallyDto,
  WorkflowActionCode,
  evaluationService,
} from "@/services/evaluationService";
import { FileUploadModal } from "@/components/attachments/FileUploadModal";

interface Props {
  record: EvaluationRecordDto;
  actions: RecordActionDto[];
  busy: boolean;
  /** Gửi hành động hoàn thành bước kèm dữ liệu. */
  onSubmit: (action: WorkflowActionCode, payload: Record<string, unknown>) => void;
  /** Mở hộp nhập lý do (trả lại / mở lại). */
  onReason: (action: RecordActionDto) => void;
}

const EMPTY_VOTES: VoteTallyDto = { votesExcellent: 0, votesGood: 0, votesSatisfactory: 0, votesUnsatisfactory: 0, invalidVotes: 0, notes: "" };

/**
 * Khu vực thao tác của hồ sơ: chỉ hiển thị biểu mẫu/nút cho các hành động mà máy chủ trả về trong `actions`
 * (frontend không tự suy luật quyền hay thứ tự bước).
 */
export function RecordActionPanel({ record, actions, busy, onSubmit, onReason }: Props) {
  const complete = actions.filter((a) => !a.requiresReason);
  const withReason = actions.filter((a) => a.requiresReason);

  if (actions.length === 0) {
    return (
      <div className="text-secondary small">
        <i className="bi bi-info-circle me-1" />
        Bạn không có thao tác nào trên hồ sơ ở bước hiện tại.
      </div>
    );
  }

  return (
    <div className="d-flex flex-column gap-3">
      {complete.map((action) => (
        <div key={action.action} className="border rounded-3 p-3">
          <div className="d-flex align-items-center justify-content-between mb-2">
            <h3 className="h6 mb-0">{action.label}</h3>
            {action.overdue && <span className="badge text-bg-warning">Đã quá thời hạn của bước</span>}
          </div>
          <ActionForm record={record} action={action.action} busy={busy} onSubmit={onSubmit} />
        </div>
      ))}
      {withReason.length > 0 && (
        <div className="d-flex flex-wrap gap-2">
          {withReason.map((action) => (
            <button key={action.action} type="button" className="btn btn-outline-danger btn-sm" disabled={busy} onClick={() => onReason(action)}>
              <i className={`bi ${action.action === "Reopen" ? "bi-unlock" : "bi-arrow-return-left"} me-1`} />
              {action.label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

interface FormProps {
  record: EvaluationRecordDto;
  action: WorkflowActionCode;
  busy: boolean;
  onSubmit: (action: WorkflowActionCode, payload: Record<string, unknown>) => void;
}

function ActionForm({ record, action, busy, onSubmit }: FormProps) {
  switch (action) {
    case "SubmitTasks":
      return <TasksForm record={record} busy={busy} onSubmit={(payload) => onSubmit(action, payload)} />;
    case "SubmitSelfScore":
      return <SelfScoreForm record={record} busy={busy} onSubmit={(payload) => onSubmit(action, payload)} />;
    case "ApproveTasks":
    case "ConfirmByCell":
      return <CommentForm busy={busy} label="Ý kiến (không bắt buộc)" submitText={action === "ApproveTasks" ? "Duyệt danh mục" : "Xác nhận phiếu tự chấm"} onSubmit={(payload) => onSubmit(action, payload)} />;
    case "RecordCollectiveProposal":
      return <ProposalForm record={record} busy={busy} stage="B3A_COLLECTIVE" submitText="Ghi nhận đề xuất" onSubmit={(payload) => onSubmit(action, payload)} />;
    case "Appraise":
      return <AppraisalForm record={record} busy={busy} onSubmit={(payload) => onSubmit(action, payload)} />;
    case "DirectorReview":
      return <GradeCommentForm busy={busy} defaultGrade={record.appraisalProposedGrade} submitText="Ghi nhận nhận xét" onSubmit={(payload) => onSubmit(action, payload)} />;
    case "RecordDecision":
      return <ProposalForm record={record} busy={busy} stage="B4_DECISION" submitText="Ghi nhận quyết định" onSubmit={(payload) => onSubmit(action, payload)} />;
    case "Publish":
      return (
        <div className="d-flex justify-content-between align-items-center gap-2">
          <div className="small text-secondary">Công bố sẽ khóa hồ sơ; mọi sửa đổi sau đó phải qua "Mở lại hồ sơ" có lý do.</div>
          <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={() => onSubmit(action, {})}>Công bố, khóa kết quả</button>
        </div>
      );
    default:
      return null;
  }
}

function CommentForm({ busy, label, submitText, onSubmit }: { busy: boolean; label: string; submitText: string; onSubmit: (p: Record<string, unknown>) => void }) {
  const [comment, setComment] = useState("");
  return (
    <form onSubmit={(e) => { e.preventDefault(); onSubmit({ comment }); }}>
      <label className="form-label small">{label}</label>
      <textarea className="form-control form-control-sm mb-2" rows={2} maxLength={4000} value={comment} onChange={(e) => setComment(e.target.value)} />
      <button type="submit" className="btn btn-primary btn-sm" disabled={busy}>{submitText}</button>
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

function GradeCommentForm({ busy, defaultGrade, submitText, onSubmit }: { busy: boolean; defaultGrade?: string; submitText: string; onSubmit: (p: Record<string, unknown>) => void }) {
  const [grade, setGrade] = useState(initialGrade(defaultGrade));
  const [comment, setComment] = useState("");
  return (
    <form onSubmit={(e) => { e.preventDefault(); onSubmit({ proposedGrade: grade, comment }); }} className="row g-2">
      <div className="col-md-5"><label className="form-label small" htmlFor="gc-grade">Mức đề xuất</label><GradeSelect id="gc-grade" value={grade} onChange={setGrade} /></div>
      <div className="col-12"><label className="form-label small">Nhận xét</label><textarea className="form-control form-control-sm" rows={3} maxLength={4000} value={comment} onChange={(e) => setComment(e.target.value)} /></div>
      <div className="col-12"><button type="submit" className="btn btn-primary btn-sm" disabled={busy || !grade}>{submitText}</button></div>
    </form>
  );
}

function AppraisalForm({ record, busy, onSubmit }: { record: EvaluationRecordDto; busy: boolean; onSubmit: (p: Record<string, unknown>) => void }) {
  const [score, setScore] = useState<string>(record.appraisalScore != null ? String(record.appraisalScore) : String(record.totalSelfScore ?? ""));
  const [grade, setGrade] = useState(initialGrade(record.appraisalProposedGrade) || initialGrade(record.collectiveProposedGrade) || initialGrade(record.selfProposedGrade));
  const [comment, setComment] = useState(record.appraisalComment || "");
  return (
    <form onSubmit={(e) => { e.preventDefault(); onSubmit({ appraisalScore: score === "" ? null : Number(score), proposedGrade: grade, comment }); }} className="row g-2">
      <div className="col-md-3"><label className="form-label small">Điểm thẩm định</label><input type="number" min={0} max={100} step={0.1} className="form-control form-control-sm" value={score} onChange={(e) => setScore(e.target.value)} /></div>
      <div className="col-md-5"><label className="form-label small" htmlFor="ap-grade">Mức đề xuất</label><GradeSelect id="ap-grade" value={grade} onChange={setGrade} /></div>
      <div className="col-12"><label className="form-label small">Ý kiến thẩm định</label><textarea className="form-control form-control-sm" rows={3} maxLength={4000} value={comment} onChange={(e) => setComment(e.target.value)} /></div>
      <div className="col-12"><button type="submit" className="btn btn-primary btn-sm" disabled={busy || !grade}>Ghi nhận thẩm định</button></div>
    </form>
  );
}

/** Ghi nhận đề xuất tập thể (B3a) hoặc quyết định (B4); kết quả kiểm phiếu (tùy chọn) gắn với biên bản. */
function ProposalForm({ record, busy, stage, submitText, onSubmit }: { record: EvaluationRecordDto; busy: boolean; stage: "B3A_COLLECTIVE" | "B4_DECISION"; submitText: string; onSubmit: (p: Record<string, unknown>) => void }) {
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
  const totalVotes = votes.votesExcellent + votes.votesGood + votes.votesSatisfactory + votes.votesUnsatisfactory + votes.invalidVotes;

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
      {isDecision ? (
        <>
          <div className="col-md-3"><label className="form-label small">Điểm chính thức (tùy chọn)</label><input type="number" min={0} max={100} step={0.1} className="form-control form-control-sm" value={finalScore} onChange={(e) => setFinalScore(e.target.value)} placeholder="Mặc định: điểm thẩm định" /></div>
          <div className="col-md-4"><label className="form-label small">Cơ quan quyết định</label><input className="form-control form-control-sm" value={authorityName} maxLength={300} onChange={(e) => setAuthorityName(e.target.value)} /></div>
          <div className="col-md-4"><label className="form-label small">Số văn bản</label><input className="form-control form-control-sm" value={documentNumber} maxLength={100} onChange={(e) => setDocumentNumber(e.target.value)} /></div>
          <div className="col-md-4"><label className="form-label small">Ngày văn bản</label><input type="date" className="form-control form-control-sm" value={documentDate} onChange={(e) => setDocumentDate(e.target.value)} /></div>
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
        <div className="form-text">Lập biên bản ở trang "Đánh giá tập thể &amp; Hội nghị". Hệ thống chỉ lưu kết quả kiểm phiếu tổng hợp, không lưu phiếu từng người.</div>
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
      <div className="col-12"><button type="submit" className="btn btn-primary btn-sm" disabled={busy || !grade}>{submitText}</button></div>
    </form>
  );
}

function TasksForm({ record, busy, onSubmit }: { record: EvaluationRecordDto; busy: boolean; onSubmit: (p: Record<string, unknown>) => void }) {
  const initial: TaskInputDto[] = record.tasks.length > 0
    ? record.tasks.map((t) => ({ taskName: t.taskName, targetOutput: t.targetOutput, weight: t.weight, deadline: t.deadline?.substring(0, 10), attachmentId: t.attachmentId }))
    : [0, 1, 2].map(() => ({ taskName: "", targetOutput: "", weight: 0, deadline: "" }));
  const [tasks, setTasks] = useState<TaskInputDto[]>(initial);
  const total = useMemo(() => Math.round(tasks.reduce((sum, t) => sum + (Number(t.weight) || 0), 0) * 100) / 100, [tasks]);

  const update = (index: number, key: keyof TaskInputDto, value: string) =>
    setTasks(tasks.map((t, i) => (i === index ? { ...t, [key]: key === "weight" ? Number(value) : value } : t)));

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        onSubmit({ tasks: tasks.map((t) => ({ ...t, deadline: t.deadline || null })) });
      }}
    >
      <div className="table-responsive">
        <table className="table table-sm align-middle mb-2">
          <thead>
            <tr className="small text-secondary">
              <th style={{ width: 32 }}>#</th>
              <th>Sản phẩm / nhiệm vụ</th>
              <th>Kết quả cần đạt</th>
              <th style={{ width: 100 }}>Trọng số</th>
              <th style={{ width: 150 }}>Thời hạn</th>
              <th style={{ width: 40 }} />
            </tr>
          </thead>
          <tbody>
            {tasks.map((t, i) => (
              <tr key={i}>
                <td className="small text-secondary">{i + 1}</td>
                <td><input className="form-control form-control-sm" value={t.taskName} maxLength={500} required onChange={(e) => update(i, "taskName", e.target.value)} /></td>
                <td><input className="form-control form-control-sm" value={t.targetOutput} onChange={(e) => update(i, "targetOutput", e.target.value)} /></td>
                <td><input type="number" min={0} step={0.5} className="form-control form-control-sm" value={t.weight} onChange={(e) => update(i, "weight", e.target.value)} /></td>
                <td><input type="date" className="form-control form-control-sm" value={t.deadline || ""} onChange={(e) => update(i, "deadline", e.target.value)} /></td>
                <td>
                  <button type="button" className="btn btn-link btn-sm text-danger p-0" aria-label="Xóa dòng" onClick={() => setTasks(tasks.filter((_, idx) => idx !== i))}>
                    <i className="bi bi-x-lg" />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="d-flex justify-content-between align-items-center">
        <div className="d-flex gap-2 align-items-center">
          <button type="button" className="btn btn-outline-secondary btn-sm" onClick={() => setTasks([...tasks, { taskName: "", targetOutput: "", weight: 0, deadline: "" }])}>
            <i className="bi bi-plus-lg me-1" />Thêm dòng
          </button>
          <span className="small text-secondary">Tổng trọng số: <strong>{total}</strong></span>
        </div>
        <button type="submit" className="btn btn-primary btn-sm" disabled={busy}>Nộp danh mục</button>
      </div>
      <div className="form-text">Số lượng sản phẩm và tổng trọng số theo tham số của kỳ; máy chủ kiểm tra khi nộp.</div>
    </form>
  );
}

interface TaskRatio {
  a: number;
  b: number;
  c: number;
  d: number;
  exceed: boolean;
  attachmentId?: string | null;
  attachmentName?: string | null;
}

const AXIS_NAMES = [
  "Trục 1 — Nhiệm vụ chính trị, SXKD, cung cấp dịch vụ",
  "Trục 2 — Thể chế, phân cấp, kiểm tra, giám sát",
  "Trục 3 — KHCN, đổi mới sáng tạo, chuyển đổi số",
  "Trục 4 — Xây dựng Đảng, hệ thống chính trị",
  "Trục 5 — Văn hóa, đời sống người lao động",
  "Trục 6 — Quốc phòng, an ninh, đối ngoại",
];

const GENERAL_NAMES = ["T1 Tư tưởng chính trị", "T2 Đạo đức, lối sống", "T3 Tác phong, lề lối", "T4 Ý thức tổ chức kỷ luật", "T5 Đổi mới sáng tạo", "T6 Trách nhiệm nêu gương"];

function SelfScoreForm({ record, busy, onSubmit }: { record: EvaluationRecordDto; busy: boolean; onSubmit: (p: Record<string, unknown>) => void }) {
  const uses09B = record.selfScoreForm === "09B";
  const [general, setGeneral] = useState<number[]>(record.generalScores?.length === 6 ? record.generalScores : [5, 5, 5, 5, 5, 5]);
  const [axis, setAxis] = useState<number[]>(record.axisScores?.length === 6 ? record.axisScores : [0, 0, 0, 0, 0, 0]);
  const [grade, setGrade] = useState(initialGrade(record.selfProposedGrade));
  const [ratios, setRatios] = useState<Record<string, TaskRatio>>(() =>
    Object.fromEntries(
      record.tasks.map((t) => [
        t.id,
        {
          a: t.criteriaA_Ratio ?? 1,
          b: t.criteriaB_Ratio ?? 1,
          c: t.criteriaC_Ratio ?? 1,
          d: t.criteriaD_Ratio ?? 1,
          exceed: t.isExceedStandard,
          attachmentId: t.attachmentId,
          attachmentName: t.attachmentOriginalName || t.attachmentFileName,
        },
      ])
    )
  );
  const [uploadFor, setUploadFor] = useState<string | null>(null);

  const setRatio = (taskId: string, key: keyof TaskRatio, value: number | boolean | string | null) =>
    setRatios({ ...ratios, [taskId]: { ...ratios[taskId], [key]: value } });

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    const payload: Record<string, unknown> = { generalScores: general, selfProposedGrade: grade || null };
    if (uses09B) {
      payload.axisScores = axis;
    } else {
      payload.taskScores = record.tasks.map((t) => ({
        taskId: t.id,
        criteriaA_Ratio: ratios[t.id].a,
        criteriaB_Ratio: ratios[t.id].b,
        criteriaC_Ratio: ratios[t.id].c,
        criteriaD_Ratio: ratios[t.id].d,
        isExceedStandard: ratios[t.id].exceed,
        attachmentId: ratios[t.id].attachmentId || null,
      }));
    }
    onSubmit(payload);
  };

  const sum = (values: number[]) => Math.round(values.reduce((s, v) => s + (Number(v) || 0), 0) * 100) / 100;

  return (
    <form onSubmit={submit}>
      <h4 className="h6 small fw-bold text-secondary">I. Tiêu chí chung (Mẫu 09{uses09B ? "B" : "A"})</h4>
      <div className="row g-2 mb-3">
        {GENERAL_NAMES.map((name, i) => (
          <div className="col-6 col-md-4" key={name}>
            <label className="form-label small mb-0">{name}</label>
            <input type="number" min={0} step={0.1} className="form-control form-control-sm" value={general[i]}
              onChange={(e) => setGeneral(general.map((v, idx) => (idx === i ? Number(e.target.value) : v)))} />
          </div>
        ))}
        <div className="col-12 small text-secondary">Cộng tiêu chí chung: <strong>{sum(general)}</strong></div>
      </div>

      {uses09B ? (
        <>
          <h4 className="h6 small fw-bold text-secondary">II. Kết quả thực hiện nhiệm vụ — chấm trực tiếp 6 trục (Mẫu 09B)</h4>
          <div className="row g-2 mb-3">
            {AXIS_NAMES.map((name, i) => (
              <div className="col-12 col-md-6" key={name}>
                <label className="form-label small mb-0">{name}</label>
                <input type="number" min={0} step={0.1} className="form-control form-control-sm" value={axis[i]}
                  onChange={(e) => setAxis(axis.map((v, idx) => (idx === i ? Number(e.target.value) : v)))} />
              </div>
            ))}
            <div className="col-12 small text-secondary">Cộng 6 trục: <strong>{sum(axis)}</strong> (điểm tối đa từng trục theo tham số của kỳ)</div>
          </div>
        </>
      ) : (
        <>
          <h4 className="h6 small fw-bold text-secondary">II. Kết quả thực hiện sản phẩm (Mẫu 02) — tỷ lệ đạt từng tiêu chí A-B-C-D (0–1)</h4>
          <div className="table-responsive mb-3">
            <table className="table table-sm align-middle">
              <thead>
                <tr className="small text-secondary">
                  <th>Sản phẩm</th><th style={{ width: 70 }}>Trọng số</th><th style={{ width: 80 }}>A</th><th style={{ width: 80 }}>B</th><th style={{ width: 80 }}>C</th><th style={{ width: 80 }}>D</th><th style={{ width: 70 }}>Vượt</th><th style={{ width: 170 }}>Minh chứng</th>
                </tr>
              </thead>
              <tbody>
                {record.tasks.map((t) => {
                  const r = ratios[t.id];
                  return (
                    <tr key={t.id}>
                      <td><div className="small fw-semibold">{t.taskName}</div><div className="small text-secondary">{t.targetOutput}</div></td>
                      <td className="small">{t.weight}</td>
                      {(["a", "b", "c", "d"] as const).map((k) => (
                        <td key={k}>
                          <input type="number" min={0} max={1} step={0.05} className="form-control form-control-sm" value={r[k]}
                            onChange={(e) => setRatio(t.id, k, Number(e.target.value))} aria-label={`Tiêu chí ${k.toUpperCase()} — ${t.taskName}`} />
                        </td>
                      ))}
                      <td className="text-center"><input type="checkbox" className="form-check-input" checked={r.exceed} onChange={(e) => setRatio(t.id, "exceed", e.target.checked)} aria-label="Vượt chuẩn" /></td>
                      <td className="small">
                        {r.attachmentName ? <span className="d-block text-truncate" style={{ maxWidth: 160 }} title={r.attachmentName}><i className="bi bi-paperclip me-1" />{r.attachmentName}</span> : <span className="text-secondary">Chưa có</span>}
                        <button type="button" className="btn btn-link btn-sm p-0" onClick={() => setUploadFor(t.id)}>Đính kèm</button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </>
      )}

      <div className="row g-2 align-items-end">
        <div className="col-md-5">
          <label className="form-label small" htmlFor="self-grade">Mức tự đề xuất</label>
          <select id="self-grade" className="form-select form-select-sm" value={grade} onChange={(e) => setGrade(e.target.value)}>
            <option value="">— Để hệ thống gợi ý theo tổng điểm —</option>
            {GRADE_OPTIONS.filter((g) => g.value !== "ChuaXepLoai").map((g) => <option key={g.value} value={g.value}>{g.label}</option>)}
          </select>
        </div>
        <div className="col-md-7 text-end"><button type="submit" className="btn btn-primary btn-sm" disabled={busy}>Nộp phiếu tự chấm</button></div>
      </div>

      {uploadFor && (
        <FileUploadModal
          isOpen
          onClose={() => setUploadFor(null)}
          formCode="MAU02"
          taskTitle={record.tasks.find((t) => t.id === uploadFor)?.taskName}
          currentAttachmentId={ratios[uploadFor]?.attachmentId}
          onUploadSuccess={(attachment) => {
            setRatios({ ...ratios, [uploadFor]: { ...ratios[uploadFor], attachmentId: attachment.id, attachmentName: attachment.fileName } });
            setUploadFor(null);
          }}
        />
      )}
    </form>
  );
}

export default RecordActionPanel;
