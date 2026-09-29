"use client";

import React, { useMemo, useState } from "react";
import {
  AxisNote,
  EvaluationRecordDto,
  GRADE_OPTIONS,
  TaskResultRow,
  criteriaAppliesForm,
  selfAssessmentSections,
} from "@/services/evaluationService";
import {
  CriteriaSnapshot,
  GRADE_LABELS,
  GeneralItemScore,
  fmt,
  generalTotal,
  gradeByScore,
  roundScore,
} from "@/services/criteriaService";
import { FileUploadModal } from "@/components/attachments/FileUploadModal";
import { AxisNoteFields, SelfAssessmentInput, TaskResultsInput } from "@/components/evaluations/IndividualFormInputs";

interface TaskRatio {
  a: number;
  b: number;
  c: number;
  d: number;
  exceed: boolean;
  attachmentId?: string | null;
  attachmentName?: string | null;
}

interface Props {
  record: EvaluationRecordDto;
  criteria: CriteriaSnapshot | null;
  busy: boolean;
  onSubmit: (payload: Record<string, unknown>) => void;
}

/**
 * Phiếu tự chấm dựng động từ bộ tiêu chí của kỳ: tiêu chí con "Đảm bảo / Không đảm bảo / K/AD (lý do)" hoặc ô điểm (theo cách chấm
 * của nhóm), trục theo mã (Mẫu 09B) hoặc A-B-C-D theo khung tỷ trọng của hồ sơ (Mẫu 09A); tổng tạm tính — máy chủ tính lại.
 * Task 18: nhập kèm Mẫu 09C (mục tự luận theo bộ tiêu chí), 9D (nhiệm vụ theo trục) khi kỳ áp dụng và phần tự luận theo trục của
 * Mẫu 09B; nội dung đã lưu được nạp lại để sửa tiếp khi hồ sơ bị trả lại.
 */
export function SelfScoreForm({ record, criteria, busy, onSubmit }: Props) {
  if (!criteria) {
    return (
      <div className="alert alert-warning small mb-0">
        Kỳ đánh giá chưa có bộ tiêu chí nên chưa tự chấm được. Hãy liên hệ người quản lý kỳ.
      </div>
    );
  }
  return <SelfScoreFormInner record={record} criteria={criteria} busy={busy} onSubmit={onSubmit} />;
}

function SelfScoreFormInner({ record, criteria, busy, onSubmit }: Props & { criteria: CriteriaSnapshot }) {
  const content = criteria.content;
  const p = content.parameters;
  const uses09B = criteria.selfScoreForm === "09B";
  const frame = content.weightFrames.find((f) => f.code.toLowerCase() === (record.weightFrameCode || "").toLowerCase());

  const [general, setGeneral] = useState<Record<string, GeneralItemScore>>(() => {
    const initial: Record<string, GeneralItemScore> = {};
    for (const group of content.generalGroups)
      for (const item of group.items)
        initial[item.code] = record.generalScores?.[item.code] ?? { score: item.maxScore, notApplicable: false, reason: "" };
    return initial;
  });
  const [axis, setAxis] = useState<Record<string, number>>(() =>
    Object.fromEntries(content.axes.map((a) => [a.code, record.axisScores?.[a.code] ?? a.maxScore]))
  );
  const [grade, setGrade] = useState(record.selfProposedGrade && record.selfProposedGrade !== "ChuaXepLoai" ? record.selfProposedGrade : "");
  const [ratios, setRatios] = useState<Record<string, TaskRatio>>(() =>
    Object.fromEntries(
      record.tasks.map((t) => [
        t.id,
        {
          a: Math.round((t.criteriaA_Ratio ?? 1) * 100),
          b: Math.round((t.criteriaB_Ratio ?? 1) * 100),
          c: Math.round((t.criteriaC_Ratio ?? 1) * 100),
          d: Math.round((t.criteriaD_Ratio ?? 1) * 100),
          exceed: t.isExceedStandard,
          attachmentId: t.attachmentId,
          attachmentName: t.attachmentOriginalName || t.attachmentFileName,
        },
      ])
    )
  );
  const [uploadFor, setUploadFor] = useState<string | null>(null);

  // Task 18: biểu mẫu cá nhân nhập cùng phiếu tự chấm.
  const uses09C = criteriaAppliesForm(criteria, "09C");
  const uses9D = criteriaAppliesForm(criteria, "9D");
  const sections = selfAssessmentSections(criteria);
  const [selfAssessment, setSelfAssessment] = useState<Record<string, string>>(() => ({ ...(record.selfAssessment ?? {}) }));
  const [taskResults, setTaskResults] = useState<TaskResultRow[]>(() => (record.taskResults ?? []).map((r) => ({ ...r })));
  const [axisNotes, setAxisNotes] = useState<Record<string, AxisNote>>(() =>
    Object.fromEntries(content.axes.map((a) => [a.code, { ...(record.axisNotes?.[a.code] ?? {}) }]))
  );

  const setItem = (code: string, change: Partial<GeneralItemScore>) => setGeneral({ ...general, [code]: { ...general[code], ...change } });
  const setRatio = (taskId: string, key: keyof TaskRatio, value: number | boolean | string | null) =>
    setRatios({ ...ratios, [taskId]: { ...ratios[taskId], [key]: value } });

  const generalScore = useMemo(() => generalTotal(content, general), [content, general]);
  const taskScore = (weight: number, r: TaskRatio) => {
    if (!frame) return 0;
    const clamp = (v: number) => Math.min(1, Math.max(0, (Number(v) || 0) / 100));
    const ratio = clamp(r.a) * frame.a + clamp(r.b) * frame.b + clamp(r.c) * frame.c + clamp(r.d) * frame.d;
    return roundScore(weight * ratio, p.rounding.taskScore);
  };
  const tasksScore = useMemo(() => {
    if (uses09B) return roundScore(Object.values(axis).reduce((s, v) => s + (Number(v) || 0), 0), p.rounding.tasksTotal);
    return roundScore(record.tasks.reduce((s, t) => s + (ratios[t.id] ? taskScore(t.weight, ratios[t.id]) : t.selfScore), 0), p.rounding.tasksTotal);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [uses09B, axis, ratios, record.tasks, frame, p]);
  const total = roundScore(generalScore + tasksScore, p.rounding.total);

  const needsReason = (item: { maxScore: number }, s: GeneralItemScore) =>
    !!s.notApplicable || (p.deductionReasonMinPoints != null && item.maxScore - (Number(s.score) || 0) + 1e-6 >= p.deductionReasonMinPoints);

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    const generalScores = Object.fromEntries(
      Object.entries(general).map(([code, s]) => [code, { score: s.notApplicable ? 0 : Number(s.score) || 0, notApplicable: !!s.notApplicable, reason: s.reason?.trim() || null }])
    );
    const payload: Record<string, unknown> = { generalScores, selfProposedGrade: grade || null };
    if (uses09C) payload.selfAssessment = selfAssessment;
    if (uses9D) payload.taskResults = taskResults;
    if (uses09B) {
      payload.axisScores = Object.fromEntries(Object.entries(axis).map(([k, v]) => [k, Number(v) || 0]));
      payload.axisNotes = axisNotes;
    } else {
      payload.taskScores = record.tasks.map((t) => ({
        taskId: t.id,
        criteriaA_Ratio: (Number(ratios[t.id].a) || 0) / 100,
        criteriaB_Ratio: (Number(ratios[t.id].b) || 0) / 100,
        criteriaC_Ratio: (Number(ratios[t.id].c) || 0) / 100,
        criteriaD_Ratio: (Number(ratios[t.id].d) || 0) / 100,
        isExceedStandard: ratios[t.id].exceed,
        attachmentId: ratios[t.id].attachmentId || null,
      }));
    }
    onSubmit(payload);
  };

  const band = (percent: number) =>
    [...content.conversionScale].sort((x, y) => y.minPercent - x.minPercent).find((b) => percent + 1e-6 >= b.minPercent);

  return (
    <form onSubmit={submit}>
      <div className="small text-secondary mb-2">
        Bộ tiêu chí: <strong>{criteria.name}</strong> (Mẫu {criteria.selfScoreForm}). Mặc định ghi nhận "Đảm bảo"; khoản giảm
        {p.deductionReasonMinPoints != null ? ` từ ${fmt(p.deductionReasonMinPoints)} điểm` : ""} trở lên và tiêu chí "K/AD" phải nêu lý do/căn cứ.
      </div>

      <h4 className="h6 small fw-bold text-secondary">I. Nhóm tiêu chí chung (tối đa {fmt(p.generalMaxScore)} điểm)</h4>
      <div className="table-responsive mb-2">
        <table className="table table-sm align-middle">
          <thead>
            <tr className="small text-secondary">
              <th style={{ width: 48 }}>Mã</th><th>Tiêu chí</th><th style={{ width: 60 }}>Tối đa</th><th style={{ width: 260 }}>Chấm</th><th style={{ width: 220 }}>Lý do / căn cứ</th>
            </tr>
          </thead>
          <tbody>
            {content.generalGroups.map((group) => (
              <React.Fragment key={group.code}>
                <tr className="table-light small fw-semibold">
                  <td>{group.code}</td>
                  <td colSpan={4}>{group.name} — {fmt(group.items.reduce((s, i) => s + i.maxScore, 0))} điểm</td>
                </tr>
                {group.items.map((item) => {
                  const s = general[item.code];
                  const na = !!s.notApplicable;
                  const reasonRequired = needsReason(item, s);
                  return (
                    <tr key={item.code} className="small">
                      <td>{item.code}</td>
                      <td>{item.text}</td>
                      <td>{fmt(item.maxScore)}</td>
                      <td>
                        {group.scoringMode === "Binary" ? (
                          <div className="d-flex flex-wrap gap-2">
                            {[
                              { key: "ok", label: "Đảm bảo", checked: !na && Number(s.score) >= item.maxScore, apply: { notApplicable: false, score: item.maxScore } },
                              { key: "no", label: "Không đảm bảo", checked: !na && Number(s.score) < item.maxScore, apply: { notApplicable: false, score: 0 } },
                              ...(p.allowNotApplicable ? [{ key: "na", label: "K/AD", checked: na, apply: { notApplicable: true, score: 0 } }] : []),
                            ].map((option) => (
                              <label className="form-check form-check-inline mb-0" key={option.key}>
                                <input type="radio" className="form-check-input" name={`gi-${item.code}`} checked={option.checked} onChange={() => setItem(item.code, option.apply)} />
                                <span className="form-check-label">{option.label}</span>
                              </label>
                            ))}
                          </div>
                        ) : (
                          <div className="d-flex gap-2 align-items-center">
                            <input type="number" min={0} max={item.maxScore} step={0.25} disabled={na} className="form-control form-control-sm" style={{ width: 90 }}
                              value={na ? "" : s.score} onChange={(e) => setItem(item.code, { score: Number(e.target.value) })} aria-label={`Điểm tiêu chí ${item.code}`} />
                            {p.allowNotApplicable && (
                              <label className="form-check mb-0">
                                <input type="checkbox" className="form-check-input" checked={na} onChange={(e) => setItem(item.code, { notApplicable: e.target.checked })} />
                                <span className="form-check-label">K/AD</span>
                              </label>
                            )}
                          </div>
                        )}
                      </td>
                      <td>
                        {reasonRequired || s.reason ? (
                          <input className="form-control form-control-sm" maxLength={4000} required={reasonRequired} placeholder={na ? "Lý do không áp dụng" : "Căn cứ giảm điểm"}
                            value={s.reason || ""} onChange={(e) => setItem(item.code, { reason: e.target.value })} aria-label={`Lý do tiêu chí ${item.code}`} />
                        ) : (
                          <span className="text-secondary">—</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </React.Fragment>
            ))}
          </tbody>
        </table>
      </div>
      <div className="small text-secondary mb-3">
        Cộng tiêu chí chung (tạm tính): <strong>{fmt(generalScore)}</strong> / {fmt(p.generalMaxScore)}
        {p.allowNotApplicable && <> · K/AD: {p.notApplicableRule === "GrantFull" ? "tính như đạt tối đa" : "bỏ khỏi mẫu số rồi quy đổi"}</>}
      </div>

      {uses09B ? (
        <>
          <h4 className="h6 small fw-bold text-secondary">II. Kết quả thực hiện nhiệm vụ — chấm trực tiếp theo trục (Mẫu 09B, tối đa {fmt(p.totalTaskWeight)} điểm)</h4>
          <div className="d-flex flex-column gap-2 mb-3">
            {content.axes.map((a) => (
              <div className="border rounded p-2" key={a.code}>
                <div className="row g-2 align-items-end mb-1">
                  <div className="col-12 col-md-9">
                    <label className="form-label small fw-semibold mb-0" htmlFor={`axis-${a.code}`} title={a.description || undefined}>{a.code} — {a.name}</label>
                  </div>
                  <div className="col-12 col-md-3">
                    <div className="input-group input-group-sm">
                      <input id={`axis-${a.code}`} type="number" min={0} max={a.maxScore} step={0.5} className="form-control form-control-sm" value={axis[a.code]}
                        onChange={(e) => setAxis({ ...axis, [a.code]: Number(e.target.value) })} />
                      <span className="input-group-text">/ {fmt(a.maxScore)}</span>
                    </div>
                  </div>
                </div>
                <AxisNoteFields axisCode={a.code} value={axisNotes[a.code] ?? {}} onChange={(note) => setAxisNotes({ ...axisNotes, [a.code]: note })} />
              </div>
            ))}
            <div className="small text-secondary">Cộng các trục (tạm tính): <strong>{fmt(tasksScore)}</strong> / {fmt(p.totalTaskWeight)}</div>
          </div>
        </>
      ) : (
        <>
          <h4 className="h6 small fw-bold text-secondary">II. Kết quả thực hiện sản phẩm (Mẫu 02) — % đạt từng thành phần A-B-C-D</h4>
          <div className="small text-secondary mb-1">
            {frame
              ? <>Khung tỷ trọng của hồ sơ: <strong>{frame.code} — {frame.name}</strong> (A {fmt(frame.a * 100)}% · B {fmt(frame.b * 100)}% · C {fmt(frame.c * 100)}% · D {fmt(frame.d * 100)}%)</>
              : <span className="text-danger">Khung tỷ trọng "{record.weightFrameCode || "—"}" của hồ sơ không có trong bộ tiêu chí — liên hệ người quản lý kỳ.</span>}
          </div>
          <div className="table-responsive mb-2">
            <table className="table table-sm align-middle">
              <thead>
                <tr className="small text-secondary">
                  <th>Sản phẩm</th><th style={{ width: 55 }}>Trục</th><th style={{ width: 65 }}>Trọng số</th><th style={{ width: 75 }}>A %</th><th style={{ width: 75 }}>B %</th><th style={{ width: 75 }}>C %</th><th style={{ width: 75 }}>D %</th><th style={{ width: 70 }}>Điểm</th><th style={{ width: 55 }}>Vượt</th><th style={{ width: 160 }}>Minh chứng</th>
                </tr>
              </thead>
              <tbody>
                {record.tasks.map((t) => {
                  const r = ratios[t.id];
                  return (
                    <tr key={t.id}>
                      <td><div className="small fw-semibold">{t.taskName}</div><div className="small text-secondary">{t.targetOutput}</div></td>
                      <td className="small">{t.axisCode || "—"}</td>
                      <td className="small">{t.weight}</td>
                      {(["a", "b", "c", "d"] as const).map((k) => (
                        <td key={k}>
                          <input type="number" min={0} max={100} step={1} className="form-control form-control-sm" value={r[k]} title={band(Number(r[k]) || 0)?.label}
                            onChange={(e) => setRatio(t.id, k, Number(e.target.value))} aria-label={`Thành phần ${k.toUpperCase()} — ${t.taskName}`} />
                        </td>
                      ))}
                      <td className="small fw-semibold">{fmt(taskScore(t.weight, r))}</td>
                      <td className="text-center"><input type="checkbox" className="form-check-input" checked={r.exceed} onChange={(e) => setRatio(t.id, "exceed", e.target.checked)} aria-label="Vượt chuẩn" /></td>
                      <td className="small">
                        {r.attachmentName ? <span className="d-block text-truncate" style={{ maxWidth: 150 }} title={r.attachmentName}><i className="bi bi-paperclip me-1" />{r.attachmentName}</span> : <span className="text-secondary">Chưa có</span>}
                        <button type="button" className="btn btn-link btn-sm p-0" onClick={() => setUploadFor(t.id)}>Đính kèm</button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <details className="small text-secondary mb-3">
            <summary>Thang quy đổi % A-B-C-D của bộ tiêu chí</summary>
            <ul className="mb-0">
              {content.conversionScale.map((b) => <li key={b.minPercent}><strong>{b.label}</strong>{b.description ? `: ${b.description}` : ""}</li>)}
            </ul>
          </details>
          <div className="small text-secondary mb-3">Cộng điểm sản phẩm (tạm tính): <strong>{fmt(tasksScore)}</strong> / {fmt(p.totalTaskWeight)}</div>
        </>
      )}

      {uses9D && (
        <>
          <h4 className="h6 small fw-bold text-secondary">Mẫu 9D — Phụ lục kết quả thực hiện nhiệm vụ, công việc được giao trong quý</h4>
          <div className="small text-secondary mb-1">Nhập theo từng trục (bấm <i className="bi bi-plus-lg" /> để thêm dòng). Không bắt buộc có nhiệm vụ ở cả sáu trục.</div>
          <div className="mb-3">
            <TaskResultsInput axes={content.axes} rows={taskResults} onChange={setTaskResults} />
          </div>
        </>
      )}

      {uses09C && sections.length > 0 && (
        <>
          <h4 className="h6 small fw-bold text-secondary">Mẫu 09C — Bản tự đánh giá, xếp loại của cá nhân</h4>
          <div className="small text-secondary mb-2">
            Phần "Tự đề xuất xếp loại" của Mẫu 09C lấy từ tổng điểm và mức tự đề xuất của phiếu này. Nội dung trình bày không quá 02 trang A4.
          </div>
          <div className="mb-3">
            <SelfAssessmentInput sections={sections} value={selfAssessment} onChange={setSelfAssessment} />
          </div>
        </>
      )}

      <div className="alert alert-light border small py-2">
        Tổng điểm tạm tính: <strong>{fmt(total)}</strong> — theo ngưỡng điểm: <strong>{GRADE_LABELS[gradeByScore(content, total)]}</strong>
        {" "}(chưa xét điều kiện kèm theo; máy chủ tính lại khi nộp).
      </div>

      <div className="row g-2 align-items-end">
        <div className="col-md-5">
          <label className="form-label small" htmlFor="self-grade">Mức tự đề xuất</label>
          <select id="self-grade" className="form-select form-select-sm" value={grade} onChange={(e) => setGrade(e.target.value)}>
            <option value="">— Để hệ thống gợi ý theo ngưỡng và điều kiện của bộ —</option>
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

export default SelfScoreForm;
